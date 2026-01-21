using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using MQReceiver.Cache;
using MQReceiver.Calculators;
using MQReceiver.Configuration;
using MQReceiver.Events;
using MQReceiver.Filters;
using MQReceiver.Helpers;
using MQReceiver.Models;
using MQReceiver.Repositories;
using Npgsql;

namespace MQReceiver.Services
{
    /// <summary>
    /// KD过滤器服务
    /// 负责定时执行股票过滤
    /// 使用事件模式通知订阅者（如UI层），实现解耦
    /// 线程安全：使用volatile确保多线程间的可见性
    ///
    /// 数据边界管理：
    /// - 盘前/非交易日：使用数据库历史数据
    /// - 盘中：合并实时数据和历史数据
    /// - 盘后：检查当日数据是否已入库
    /// </summary>
    public class FilterService : IDisposable
    {
        private RealTimeDataCache realTimeCache;
        private bool _ownsCache = true;  // 是否拥有缓存的生命周期管理权
        private KDCalculator kdCalculator;
        private StockFilterOrchestrator filterOrchestrator;  // 过滤器协调器（旧版6个过滤器）
        private UnifiedStockFilter unifiedFilter;  // 新版统一过滤器（6个过滤条件）
        private DataBoundaryManager dataBoundaryManager;  // 数据边界管理器
        private System.Timers.Timer filterTimer;
        private readonly object _timerLock = new object();
        private int intervalMinutes;
        private volatile bool _isRunning = false;
        private volatile bool _disposed = false;
        
        // 性能优化：内存缓存和批量计算器
        private KlineDataMemoryCache _klineMemoryCache;
        private BatchKDCalculator _batchKDCalculator;

        // 数据变化检测
        private DateTime _lastCacheUpdateTime = DateTime.MinValue;  // 上次过滤时缓存的更新时间
        private DateTime? _lastDbDate = null;  // 上次过滤时数据库的最新日期
        private bool _forceNextFilter = true;  // 强制下次过滤（首次启动时）

        /// <summary>
        /// 过滤完成事件 - 订阅者（如UI层）订阅此事件以获取过滤结果
        /// </summary>
        public event EventHandler<FilterResultEventArgs> FilterCompleted;

        /// <summary>
        /// 服务启动事件
        /// </summary>
        public event EventHandler ServiceStarted;

        /// <summary>
        /// 服务停止事件
        /// </summary>
        public event EventHandler ServiceStopped;
        
        /// <summary>
        /// 日志消息事件 - 用于将日志输出到UI
        /// </summary>
        public event Action<string> LogMessage;

        /// <summary>
        /// 获取实时数据缓存（只读访问）
        /// </summary>
        public RealTimeDataCache RealTimeCache => realTimeCache;

        /// <summary>
        /// 获取KD计算器（只读访问）
        /// </summary>
        public KDCalculator KDCalculator => kdCalculator;

        /// <summary>
        /// 获取数据边界管理器（只读访问）
        /// </summary>
        public DataBoundaryManager DataBoundaryManager => dataBoundaryManager;

        /// <summary>
        /// 服务是否正在运行
        /// </summary>
        public bool IsRunning => _isRunning;

        private static readonly IConfigurationProvider _configProvider = AppConfigProvider.Instance;

        public FilterService()
        {
            // 读取配置
            intervalMinutes = _configProvider.GetInt("FilterService_IntervalMinutes", 30);
        }

        /// <summary>
        /// 设置外部缓存（用于与MQ服务共享）
        /// 必须在Initialize()之前调用
        /// </summary>
        public void SetExternalCache(RealTimeDataCache cache)
        {
            if (_isRunning)
                throw new InvalidOperationException("Cannot set cache while service is running");

            realTimeCache = cache;
            _ownsCache = false;  // 不拥有缓存的生命周期
        }

        /// <summary>
        /// 初始化服务（不启动定时器，不阻塞）
        /// </summary>
        /// <returns>初始化是否成功</returns>
        public bool Initialize()
        {
            try
            {
                Console.WriteLine("========================================");
                Console.WriteLine("  初始化KD过滤器服务");
                Console.WriteLine("========================================");
                Console.WriteLine();

                // 初始化Redis连接
                try
                {
                    RedisHelper.Initialize();
                    Console.WriteLine("Redis连接初始化成功");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"警告: Redis初始化失败: {ex.Message}，将使用数据库直接查询");
                }

                // 初始化实时数据缓存和KD计算器
                try
                {
                    // 如果没有设置外部缓存，则创建新的缓存
                    if (realTimeCache == null)
                    {
                        realTimeCache = new RealTimeDataCache();
                        _ownsCache = true;
                        Console.WriteLine("实时数据缓存初始化成功（内部创建）");
                    }
                    else
                    {
                        Console.WriteLine($"使用外部共享缓存（当前数据量: {realTimeCache.Count}）");
                    }

                    // 初始化数据边界管理器
                    dataBoundaryManager = new DataBoundaryManager(realTimeCache);
                    Console.WriteLine("数据边界管理器初始化成功");

                    // 使用依赖注入创建KD计算器（支持实时数据合并）
                    var klineRepository = new PostgresKlineDataRepository(DatabaseConnectionHelper.BuildConnectionString());
                    kdCalculator = new KDCalculator(klineRepository, realTimeCache, dataBoundaryManager);
                    
                    // 使用真实数据计算KD（不前复权，与图表KD计算保持一致）
                    kdCalculator.EnableForwardAdjustment = false;
                    
                    filterOrchestrator = new StockFilterOrchestrator(kdCalculator, realTimeCache);
                    
                    // 创建标准统一过滤器（内存缓存将在首次执行过滤时初始化）
                    unifiedFilter = new UnifiedStockFilter(kdCalculator, realTimeCache);
                    
                    // 订阅过滤器的日志事件
                    unifiedFilter.LogMessage += (msg) => LogMessage?.Invoke(msg);
                    
                    Console.WriteLine("KD计算器初始化成功（支持实时数据合并，启用前复权计算）");
                    Console.WriteLine("新版统一过滤器初始化成功（6个条件：强多排列/中多排列/强多缠绕/中多缠绕/强多反弹/中多反弹）");
                    LogMessage?.Invoke("KD计算器和过滤器初始化成功");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("错误: 初始化实时数据缓存失败: " + ex.Message);
                    return false;
                }

                // 检查数据库连接（只读）
                try
                {
                    var dbWriter = new DailyDataDBWriter();
                    if (!dbWriter.TestConnection())
                    {
                        Console.WriteLine("错误: PostgreSQL数据库连接失败！");
                        return false;
                    }
                    Console.WriteLine("PostgreSQL数据库连接成功");

                    // 自动创建数据库表（如果不存在）
                    if (!DatabaseInitializer.Initialize())
                    {
                        Console.WriteLine("警告: 数据库表初始化失败，但服务将继续运行");
                    }
                }
                catch (Npgsql.PostgresException ex)
                {
                    // 检查是否是数据库不存在的错误（错误代码 3D000）
                    if (ex.SqlState == "3D000" || ex.Message.Contains("不存在") || ex.Message.Contains("does not exist"))
                    {
                        string dbName = _configProvider.GetString("DatabaseName", "stockdb");
                        Console.WriteLine("错误: 数据库连接失败！");
                        Console.WriteLine($"原因: 数据库 '{dbName}' 不存在");
                        Console.WriteLine();
                        Console.WriteLine("解决方案:");
                        Console.WriteLine($"1. 使用 psql 连接到 PostgreSQL:");
                        Console.WriteLine($"   psql -h localhost -p 8532 -U postgres");
                        Console.WriteLine($"2. 执行以下 SQL 创建数据库:");
                        Console.WriteLine($"   CREATE DATABASE {dbName};");
                        Console.WriteLine($"3. 或者使用提供的 SQL 脚本:");
                        Console.WriteLine($"   psql -h localhost -p 8532 -U postgres -f db\\create_database.sql");
                        Console.WriteLine();
                    }
                    else
                    {
                        Console.WriteLine("错误: 数据库连接失败: " + ex.Message);
                        Console.WriteLine($"错误代码: {ex.SqlState}");
                    }
                    return false;
                }
                catch (Npgsql.NpgsqlException ex)
                {
                    Console.WriteLine("错误: 数据库连接失败: " + ex.Message);
                    return false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("错误: 数据库连接失败: " + ex.Message);
                    return false;
                }

                Console.WriteLine("过滤器服务初始化完成");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("错误: 初始化过滤器服务失败: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 启动定时过滤任务
        /// </summary>
        public void StartTimer()
        {
            lock (_timerLock)
            {
                try
                {
                    // 如果已经在运行，直接返回
                    if (_isRunning)
                        return;

                    var timer = new System.Timers.Timer(intervalMinutes * 60 * 1000);
                    timer.Elapsed += OnFilterTimerElapsed;
                    timer.AutoReset = true;
                    timer.Start();
                    filterTimer = timer;
                    _isRunning = true;
                    Console.WriteLine("KD过滤定时任务已启动（每{0}分钟执行一次）", intervalMinutes);

                    // 触发服务启动事件
                    ServiceStarted?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("警告: 启动定时过滤任务失败: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// 启动过滤器服务（阻塞模式，兼容旧调用方式）
        /// </summary>
        public bool Start()
        {
            if (!Initialize())
            {
                return false;
            }

            StartTimer();

            // 立即执行一次过滤
            Console.WriteLine();
            Console.WriteLine("立即执行首次过滤...");
            Console.WriteLine();
            ExecuteFilter();

            Console.WriteLine();
            Console.WriteLine("过滤器服务运行中...");
            Console.WriteLine("提示: 按 'Q' 键退出服务");
            Console.WriteLine("提示: 过滤结果将在新窗口中显示，点击股票名称可打开图表");
            Console.WriteLine();

            // 等待用户输入退出
            while (_isRunning)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Q || key.Key == ConsoleKey.Escape)
                {
                    Console.WriteLine();
                    Console.WriteLine("正在停止过滤器服务...");
                    Stop();
                    break;
                }
            }

            return true;
        }

        /// <summary>
        /// 停止过滤器服务
        /// </summary>
        public void Stop()
        {
            lock (_timerLock)
            {
                _isRunning = false;
                try
                {
                    // 获取本地引用，避免竞态条件
                    var timer = filterTimer;
                    if (timer != null)
                    {
                        timer.Stop();
                        timer.Elapsed -= OnFilterTimerElapsed;
                        timer.Dispose();
                        filterTimer = null;
                        Console.WriteLine("定时器已停止");
                    }

                    // 触发服务停止事件
                    ServiceStopped?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("停止定时器时出错: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 停止服务
                    Stop();

                    // 只有当我们拥有缓存时才释放它
                    if (realTimeCache != null && _ownsCache)
                    {
                        realTimeCache.Dispose();
                        Console.WriteLine("实时数据缓存已释放");
                    }
                    realTimeCache = null;

                    // 清理其他资源引用
                    kdCalculator = null;
                    filterOrchestrator = null;
                    dataBoundaryManager = null;

                    // 清理事件订阅
                    FilterCompleted = null;
                    ServiceStarted = null;
                    ServiceStopped = null;

                    // 关闭Redis连接
                    RedisHelper.Close();
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// 定时器触发事件 - 执行KD过滤
        /// </summary>
        private void OnFilterTimerElapsed(object sender, ElapsedEventArgs e)
        {
            // 检查服务是否仍在运行
            if (!_isRunning)
                return;

            // 使用后台线程异步执行过滤
            Task.Run(() =>
            {
                // 再次检查，避免在Stop后仍执行
                if (!_isRunning)
                    return;

                try
                {
                    Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
                    ExecuteFilter();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("执行KD过滤时出错: " + ex.Message);
                }
                finally
                {
                    Thread.CurrentThread.Priority = ThreadPriority.Normal;
                }
            });
        }

        /// <summary>
        /// 手动触发一次过滤（强制执行，不检查数据变化）
        /// </summary>
        public void TriggerFilter()
        {
            Task.Run(() => ExecuteFilter(true));  // 手动触发时强制执行
        }

        /// <summary>
        /// 执行过滤（带数据变化检测）
        /// </summary>
        private void ExecuteFilter()
        {
            ExecuteFilter(false);
        }

        /// <summary>
        /// 执行过滤
        /// </summary>
        /// <param name="forceExecute">是否强制执行（忽略数据变化检测）</param>
        private void ExecuteFilter(bool forceExecute)
        {
            try
            {
                if (filterOrchestrator == null || realTimeCache == null)
                {
                    Console.WriteLine("警告: KD过滤器未初始化，跳过本次过滤");
                    return;
                }

                // 获取当前数据状态
                var repository = new PostgresStockDataRepository();
                DateTime? latestDbDate = repository.GetLatestTradeDate();
                DateTime currentCacheUpdateTime = realTimeCache.LastUpdateTime;

                // 检查是否有新数据
                bool hasNewData = CheckForNewData(currentCacheUpdateTime, latestDbDate);

                if (!forceExecute && !_forceNextFilter && !hasNewData)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 数据未变化，跳过本次过滤（缓存: {currentCacheUpdateTime:HH:mm:ss}, 数据库: {latestDbDate?.ToString("yyyy-MM-dd") ?? "无"}）");
                    return;
                }

                // 重置强制过滤标志
                _forceNextFilter = false;

                Console.WriteLine();
                Console.WriteLine("========== 开始执行KD过滤 ==========");
                Console.WriteLine("时间: {0}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Console.WriteLine("缓存中的股票数量: {0}", realTimeCache.Count);
                Console.WriteLine("缓存最后更新时间: {0}",
                    currentCacheUpdateTime != DateTime.MinValue
                        ? currentCacheUpdateTime.ToString("yyyy-MM-dd HH:mm:ss")
                        : "从未更新");

                if (realTimeCache.Count == 0)
                {
                    Console.WriteLine("缓存中没有实时数据，尝试从数据库加载股票代码列表...");
                    LoadStockCodesFromDatabase();

                    if (realTimeCache.Count == 0)
                    {
                        Console.WriteLine("数据库中也没有股票数据，跳过过滤");
                        Console.WriteLine("提示: 请先通过MQ服务接收日线数据或实时数据");
                        return;
                    }
                    Console.WriteLine($"从数据库加载了 {realTimeCache.Count} 只股票");
                }

                // 检查数据是否过期
                if (realTimeCache.IsExpired(TimeSpan.FromHours(1)))
                {
                    Console.WriteLine("警告: 实时数据可能已过期（超过1小时未更新）");
                }

                // 性能优化：首次执行时初始化内存缓存和批量KD计算器
                // 注意：BatchKDCalculator已修复，使用与ChartService相同的SMA函数和RSV计算逻辑
                if (_klineMemoryCache == null && _batchKDCalculator == null && realTimeCache.Count > 0)
                {
                    try
                    {
                        Console.WriteLine("[性能优化] 首次执行，正在初始化内存缓存和批量KD计算器...");
                        
                        // 获取所有股票代码
                        var stockCodes = realTimeCache.GetAllData().Select(d => d.StockCode).ToList();
                        DateTime initTargetDate = latestDbDate ?? DateTime.Today;
                        
                        // 1. 创建内存缓存并预加载数据
                        _klineMemoryCache = new KlineDataMemoryCache();
                        _klineMemoryCache.PreloadAllData(stockCodes);
                        
                        // 2. 创建批量KD计算器并预计算所有KD（使用ChartService确保一致性）
                        _batchKDCalculator = new BatchKDCalculator(_klineMemoryCache, realTimeCache);
                        _batchKDCalculator.PreCalculateAllKD(stockCodes, initTargetDate);
                        
                        // 3. 使用批量计算器重新创建统一过滤器
                        unifiedFilter = new UnifiedStockFilter(kdCalculator, realTimeCache, _batchKDCalculator);
                        unifiedFilter.LogMessage += (msg) => LogMessage?.Invoke(msg);
                        
                        Console.WriteLine("[性能优化] ✅ 内存缓存和批量KD计算器初始化成功！");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[性能优化] ⚠️ 初始化内存缓存失败，使用标准模式: {ex.Message}");
                        
                        // 降级到标准模式
                        _klineMemoryCache = null;
                        _batchKDCalculator = null;
                    }
                }

                // 性能估算
                if (realTimeCache.Count >= 100)
                {
                    PerformanceAnalyzer.PrintEstimateReport(realTimeCache.Count);
                }

                Console.WriteLine();
                Console.WriteLine("开始数据完整性检查和过滤...");
                Console.WriteLine();

                // 记录开始时间
                var stopwatch = Stopwatch.StartNew();

                // 使用数据边界管理器决定目标日期和数据策略
                DataStatus dataStatus = dataBoundaryManager.GetCurrentDataStatus(latestDbDate);
                DateTime targetDate = dataStatus.RecommendedTargetDate;

                // 输出数据状态信息
                Console.WriteLine($"当前时段: {DataBoundaryManager.GetSessionDescription(dataStatus.Session)}");
                Console.WriteLine($"数据策略: {DataBoundaryManager.GetStrategyDescription(dataStatus.Strategy)}");
                Console.WriteLine($"目标日期: {targetDate:yyyy-MM-dd}");
                Console.WriteLine($"数据库最新日期: {(latestDbDate.HasValue ? latestDbDate.Value.ToString("yyyy-MM-dd") : "无")}");
                if (dataStatus.Strategy == DataSourceStrategy.DatabasePlusRealTime)
                {
                    Console.WriteLine($"实时缓存数量: {dataStatus.RealTimeCacheCount}");
                    Console.WriteLine($"实时数据状态: {(dataStatus.IsDataFresh ? "新鲜" : "可能延迟")}");
                }
                Console.WriteLine($"状态描述: {dataStatus.StatusDescription}");

                // 更新上次过滤时的数据状态（在过滤开始时更新，确保即使过滤失败也不会重复执行）
                _lastCacheUpdateTime = currentCacheUpdateTime;
                _lastDbDate = latestDbDate;

                // 关键修复：确保 StockInfoCache 已加载（用于获取正确的股票名称）
                Console.WriteLine("[过滤准备] 确保 StockInfoCache 已加载...");
                StockInfoCache.Instance.EnsureLoaded();
                if (StockInfoCache.Instance.Count == 0)
                {
                    Console.WriteLine("[过滤准备] StockInfoCache 为空，尝试同步...");
                    StockInfoCache.Instance.SyncFromDailyData();
                }
                Console.WriteLine($"[过滤准备] StockInfoCache 已加载 {StockInfoCache.Instance.Count} 只股票名称");

                // 性能优化：显示内存缓存状态
                if (_klineMemoryCache != null && _batchKDCalculator != null)
                {
                    var kdStats = _batchKDCalculator.GetCacheStats();
                    Console.WriteLine($"[性能优化] 使用内存模式 - 已缓存 {kdStats.stockCount} 只股票, {kdStats.totalResults} 个KD结果");
                }

                // ========== 执行6个新的过滤条件（并行执行，提升速度）==========
                Console.WriteLine("开始并行执行6个过滤条件...");
                Console.WriteLine($"股票数量: {realTimeCache.Count}");
                Console.WriteLine();
                
                // 并行执行6个过滤器，大幅提升速度
                var filter1Task = Task.Run(() => ExecuteNewFilter(1, "强多排列", targetDate));
                var filter2Task = Task.Run(() => ExecuteNewFilter(2, "中多排列", targetDate));
                var filter3Task = Task.Run(() => ExecuteNewFilter(3, "强多缠绕", targetDate));
                var filter4Task = Task.Run(() => ExecuteNewFilter(4, "中多缠绕", targetDate));
                var filter5Task = Task.Run(() => ExecuteNewFilter(5, "强多反弹", targetDate));
                var filter6Task = Task.Run(() => ExecuteNewFilter(6, "中多反弹", targetDate));
                
                // 等待所有过滤器完成，并显示进度
                string waitMsg = "等待所有过滤器完成...";
                Console.WriteLine(waitMsg);
                LogMessage?.Invoke(waitMsg);
                Task.WaitAll(filter1Task, filter2Task, filter3Task, filter4Task, filter5Task, filter6Task);
                
                var enrichedResults1 = filter1Task.Result;
                var enrichedResults2 = filter2Task.Result;
                var enrichedResults3 = filter3Task.Result;
                var enrichedResults4 = filter4Task.Result;
                var enrichedResults5 = filter5Task.Result;
                var enrichedResults6 = filter6Task.Result;
                
                Console.WriteLine("所有过滤器执行完成！");
                Console.WriteLine();

                stopwatch.Stop();

                // 通过事件通知订阅者（UI层）
                OnFilterCompleted(new FilterResultEventArgs
                {
                    Table1Results = enrichedResults1,
                    Table2Results = enrichedResults2,
                    Table3Results = enrichedResults3,
                    Table4Results = enrichedResults4,
                    Table5Results = enrichedResults5,
                    Table6Results = enrichedResults6,
                    Table7Results = new List<FilterResultWithHistory>(), // 空列表
                    Table8Results = new List<FilterResultWithHistory>(), // 空列表
                    FilterTime = DateTime.Now,
                    ElapsedSeconds = stopwatch.Elapsed.TotalSeconds,
                    ProcessedCount = realTimeCache.Count
                });

                Console.WriteLine();
                // 输出K线缓存统计信息
                var klineRepository = new PostgresKlineDataRepository(DatabaseConnectionHelper.BuildConnectionString());
                var (cacheCount, totalDataPoints) = klineRepository.GetCacheStats();
                Console.WriteLine($"[K线缓存统计] 缓存股票数: {cacheCount}, 总数据点数: {totalDataPoints:N0}");
                
                Console.WriteLine("========== KD过滤完成 ==========");
                Console.WriteLine("实际执行时间: {0:F2} 秒 ({1:F1} 分钟)",
                    stopwatch.Elapsed.TotalSeconds, stopwatch.Elapsed.TotalMinutes);
                if (realTimeCache.Count > 0)
                {
                    Console.WriteLine("平均每只股票耗时: {0:F2} 毫秒",
                        stopwatch.Elapsed.TotalMilliseconds / realTimeCache.Count);
                    Console.WriteLine("吞吐量: {0:F2} 股票/秒",
                        realTimeCache.Count / stopwatch.Elapsed.TotalSeconds);
                }
                Console.WriteLine("并行度: {0}", DatabaseConnectionHelper.GetOptimalParallelism());
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine("执行过滤时出错: " + ex.Message);
                Console.WriteLine("异常堆栈: " + ex.StackTrace);
            }
        }

        /// <summary>
        /// 执行单个新过滤条件
        /// 添加异常处理，确保单个过滤器失败不影响其他过滤器
        /// 添加进度提示，让用户知道执行状态
        /// </summary>
        private List<FilterResultWithHistory> ExecuteNewFilter(int filterId, string filterName, DateTime targetDate)
        {
            try
            {
                var startTime = DateTime.Now;
                string startMsg = $"【{filterName}】（过滤器{filterId}）开始执行...";
                Console.WriteLine($"[{startTime:HH:mm:ss}] {startMsg}");
                LogMessage?.Invoke($"[{startTime:HH:mm:ss}] {startMsg}");
                
                var condition = new NewFilterCondition(filterId);
                var sw = Stopwatch.StartNew();
                var results = unifiedFilter.FilterParallel(condition, targetDate);
                sw.Stop();
                
                var endTime = DateTime.Now;
                string completeMsg = $"【{filterName}】（过滤器{filterId}）完成 - 结果: {results.Count} 只股票，耗时: {sw.Elapsed.TotalSeconds:F2} 秒";
                Console.WriteLine($"[{endTime:HH:mm:ss}] {completeMsg}");
                LogMessage?.Invoke($"[{endTime:HH:mm:ss}] {completeMsg}");
                
                return results;
            }
            catch (Exception ex)
            {
                var errorTime = DateTime.Now;
                string errorMsg = $"错误: 过滤器{filterId}（{filterName}）执行失败: {ex.Message}";
                Console.WriteLine($"[{errorTime:HH:mm:ss}] {errorMsg}");
                LogMessage?.Invoke($"[{errorTime:HH:mm:ss}] {errorMsg}");
                
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"  内部异常: {ex.InnerException.Message}");
                }
                Console.WriteLine($"  异常堆栈: {ex.StackTrace}");
                // 返回空列表，确保不影响其他过滤器的执行
                return new List<FilterResultWithHistory>();
            }
        }

        /// <summary>
        /// 触发过滤完成事件
        /// </summary>
        protected virtual void OnFilterCompleted(FilterResultEventArgs e)
        {
            FilterCompleted?.Invoke(this, e);
        }

        /// <summary>
        /// 检查是否有新数据需要重新计算
        /// </summary>
        /// <param name="currentCacheTime">当前缓存更新时间</param>
        /// <param name="currentDbDate">当前数据库最新日期</param>
        /// <returns>是否有新数据</returns>
        private bool CheckForNewData(DateTime currentCacheTime, DateTime? currentDbDate)
        {
            // 检查实时缓存是否有更新
            bool cacheUpdated = currentCacheTime != DateTime.MinValue && currentCacheTime > _lastCacheUpdateTime;

            // 检查数据库是否有新数据
            bool dbUpdated = false;
            if (currentDbDate.HasValue)
            {
                if (!_lastDbDate.HasValue)
                {
                    dbUpdated = true;  // 之前没有数据库日期，现在有了
                }
                else if (currentDbDate.Value > _lastDbDate.Value)
                {
                    dbUpdated = true;  // 数据库有新的交易日数据
                }
            }

            return cacheUpdated || dbUpdated;
        }

        /// <summary>
        /// 从数据库加载股票代码列表到缓存
        /// 当实时数据缓存为空时调用，使过滤服务可以基于日线数据运行
        /// 改进：1. 过滤非A股股票  2. 确保股票名称正确加载
        /// </summary>
        private void LoadStockCodesFromDatabase()
        {
            try
            {
                var repository = new PostgresStockDataRepository();
                var allStockCodes = repository.GetAllStockCodes();

                if (allStockCodes != null && allStockCodes.Count > 0)
                {
                    Console.WriteLine($"[股票加载] 从数据库获取到 {allStockCodes.Count} 只股票代码");

                    // 关键修复1：在加载时就过滤掉非A股股票（指数、基金、B股、已退市等）
                    var validStockCodes = allStockCodes.Where(code => StockDataParser.IsValidStockCode(code)).ToList();
                    int filteredCount = allStockCodes.Count - validStockCodes.Count;
                    
                    Console.WriteLine($"[股票加载] 过滤后剩余 {validStockCodes.Count} 只有效A股（已过滤 {filteredCount} 只非A股）");

                    // 关键修复2：确保 StockInfoCache 已加载（包含完整的股票名称）
                    Console.WriteLine($"[股票加载] 正在加载 StockInfoCache...");
                    StockInfoCache.Instance.EnsureLoaded();
                    
                    // 如果缓存为空，尝试从日线数据同步
                    if (StockInfoCache.Instance.Count == 0)
                    {
                        Console.WriteLine($"[股票加载] StockInfoCache 为空，尝试从日线数据同步...");
                        StockInfoCache.Instance.SyncFromDailyData();
                    }
                    
                    Console.WriteLine($"[股票加载] StockInfoCache 已加载 {StockInfoCache.Instance.Count} 只股票名称");

                    // 关键修复3：过滤掉所有ST股票（*ST、ST等）
                    var nonSTStockCodes = new List<string>();
                    int stFilteredCount = 0;
                    
                    foreach (var code in validStockCodes)
                    {
                        string stockName = StockInfoCache.Instance.GetStockName(code);
                        // 使用 StockDataParser 检查是否为ST股票
                        if (StockDataParser.IsSTStock(stockName))
                        {
                            stFilteredCount++;
                            continue; // 跳过ST股票
                        }
                        nonSTStockCodes.Add(code);
                    }
                    
                    Console.WriteLine($"[股票加载] 过滤ST股票后剩余 {nonSTStockCodes.Count} 只股票（已过滤 {stFilteredCount} 只ST股票）");

                    // 获取股票名称（从 StockInfoCache，而不是直接查数据库）
                    var stockNames = repository.GetAllStockNames();
                    Console.WriteLine($"[股票加载] 从stock_info表获取到 {stockNames.Count} 个股票名称");

                    // 统计数据覆盖情况
                    PrintDataCoverageStatistics(repository, nonSTStockCodes);

                    // 为每个有效股票代码创建实时数据记录
                    var records = new List<RealTimeDataRecord>();
                    int hasNameCount = 0;
                    int noNameCount = 0;
                    
                    foreach (var code in nonSTStockCodes)
                    {
                        // 关键修复3：优先从 StockInfoCache 获取名称（包含内置字典）
                        string name = StockInfoCache.Instance.GetStockName(code);
                        
                        // 如果名称等于代码（说明没有找到名称），标记为无名称
                        if (name == code)
                        {
                            noNameCount++;
                            if (noNameCount <= 10) // 只输出前10个示例
                            {
                                Console.WriteLine($"  [警告] {code} 没有找到股票名称");
                            }
                        }
                        else
                        {
                            hasNameCount++;
                        }

                        records.Add(new RealTimeDataRecord
                        {
                            StockCode = code,
                            StockName = name,
                            MarketCode = code.StartsWith("6") ? (ushort)1 : (ushort)0,  // 6开头是上海
                            UpdateTime = DateTime.Now
                        });
                    }

                    Console.WriteLine($"[股票加载] 名称统计: 有名称={hasNameCount}, 无名称={noNameCount}");
                    
                    if (noNameCount > 0)
                    {
                        Console.WriteLine($"[股票加载] 提示: {noNameCount} 只股票缺少名称，将显示代码");
                    }

                    realTimeCache.UpdateData(records);
                    Console.WriteLine($"[股票加载] 已将 {records.Count} 只有效A股加载到缓存");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[股票加载] 错误: 从数据库加载股票代码失败 - {ex.Message}");
                Console.WriteLine($"[股票加载] 堆栈: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 打印数据覆盖统计信息
        /// </summary>
        private void PrintDataCoverageStatistics(PostgresStockDataRepository repository, List<string> stockCodes)
        {
            try
            {
                int hasWeeklyData = 0;  // 至少14天数据（可算周KD）
                int hasMonthlyData = 0; // 至少60天数据（可算月KD）
                int hasQuarterlyData = 0; // 至少180天数据（可算季KD）
                string sampleCode = null;
                DateTime? sampleStart = null;
                DateTime? sampleEnd = null;

                // 只检查前100只股票
                var sampleCodes = stockCodes.Take(100).ToList();
                foreach (var code in sampleCodes)
                {
                    var dateRange = repository.GetDataDateRange(code);
                    if (dateRange.StartDate.HasValue && dateRange.EndDate.HasValue)
                    {
                        int days = (dateRange.EndDate.Value - dateRange.StartDate.Value).Days;
                        if (days >= 14) hasWeeklyData++;
                        if (days >= 60) hasMonthlyData++;
                        if (days >= 180) hasQuarterlyData++;

                        if (sampleCode == null)
                        {
                            sampleCode = code;
                            sampleStart = dateRange.StartDate;
                            sampleEnd = dateRange.EndDate;
                        }
                    }
                }

                Console.WriteLine($"\n数据覆盖统计（基于前{sampleCodes.Count}只股票）：");
                Console.WriteLine($"  - 可计算周KD (>=14天): {hasWeeklyData} ({hasWeeklyData * 100 / sampleCodes.Count}%)");
                Console.WriteLine($"  - 可计算月KD (>=60天): {hasMonthlyData} ({hasMonthlyData * 100 / sampleCodes.Count}%)");
                Console.WriteLine($"  - 可计算季KD (>=180天): {hasQuarterlyData} ({hasQuarterlyData * 100 / sampleCodes.Count}%)");
                if (sampleCode != null)
                {
                    Console.WriteLine($"  - 示例股票 {sampleCode}: {sampleStart:yyyy-MM-dd} 到 {sampleEnd:yyyy-MM-dd}");
                }
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"统计数据覆盖情况失败: {ex.Message}");
            }
        }
    }
}
