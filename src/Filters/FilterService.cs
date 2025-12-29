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
    /// </summary>
    public class FilterService : IDisposable
    {
        private RealTimeDataCache realTimeCache;
        private bool _ownsCache = true;  // 是否拥有缓存的生命周期管理权
        private KDCalculator kdCalculator;
        private StockFilter stockFilter;  // 保留旧版本用于兼容
        private StockFilterOrchestrator filterOrchestrator;  // 新的过滤器协调器
        private System.Timers.Timer filterTimer;
        private readonly object _timerLock = new object();
        private int intervalMinutes;
        private volatile bool _isRunning = false;
        private volatile bool _disposed = false;

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
        /// 获取实时数据缓存（只读访问）
        /// </summary>
        public RealTimeDataCache RealTimeCache => realTimeCache;

        /// <summary>
        /// 获取KD计算器（只读访问）
        /// </summary>
        public KDCalculator KDCalculator => kdCalculator;

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

                    kdCalculator = new KDCalculator();
                    stockFilter = new StockFilter(kdCalculator, realTimeCache);  // 保留旧版本
                    filterOrchestrator = new StockFilterOrchestrator(kdCalculator, realTimeCache);  // 新版本
                    Console.WriteLine("KD计算器初始化成功");
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
                    stockFilter = null;
                    filterOrchestrator = null;

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
        /// 手动触发一次过滤
        /// </summary>
        public void TriggerFilter()
        {
            Task.Run(() => ExecuteFilter());
        }

        /// <summary>
        /// 执行过滤
        /// </summary>
        private void ExecuteFilter()
        {
            try
            {
                if (filterOrchestrator == null || realTimeCache == null)
                {
                    Console.WriteLine("警告: KD过滤器未初始化，跳过本次过滤");
                    return;
                }

                Console.WriteLine();
                Console.WriteLine("========== 开始执行KD过滤 ==========");
                Console.WriteLine("时间: {0}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Console.WriteLine("缓存中的股票数量: {0}", realTimeCache.Count);
                Console.WriteLine("缓存最后更新时间: {0}",
                    realTimeCache.LastUpdateTime != DateTime.MinValue
                        ? realTimeCache.LastUpdateTime.ToString("yyyy-MM-dd HH:mm:ss")
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

                // 获取数据库中最新的交易日期，而不是使用今天的日期
                var repository = new PostgresStockDataRepository();
                DateTime? latestDate = repository.GetLatestTradeDate();
                DateTime targetDate = latestDate ?? DateTime.Now.Date;

                Console.WriteLine($"使用的目标日期: {targetDate:yyyy-MM-dd}" +
                    (latestDate.HasValue ? " (数据库最新日期)" : " (当前日期)"));

                // ========== 过滤条件1：周K、月K、季K 金叉过滤 ==========
                Console.WriteLine("【过滤条件1】周K、月K、季K 金叉过滤");
                Console.WriteLine("----------------------------------------");
                var condition1 = FilterConditionBuilder.BuildGoldenCrossCondition();
                var sw1 = Stopwatch.StartNew();
                var results1 = filterOrchestrator.FilterByGoldenCrossParallel(condition1, targetDate);
                sw1.Stop();
                Console.WriteLine("结果数量: {0} 只股票", results1.Count);
                Console.WriteLine("处理时间: {0:F2} 秒", sw1.Elapsed.TotalSeconds);
                Console.WriteLine();

                // 获取昨天的K值并排序
                var enrichedResults1 = FilterDisplayHelper.EnrichWithHistory(results1, kdCalculator, targetDate);
                enrichedResults1 = FilterDisplayHelper.SortByQuarterlyK(enrichedResults1, descending: true);
                Console.WriteLine($"条件1结果: {enrichedResults1.Count} 只股票");

                // ========== 过滤条件2：周K>月K>季K（不含金叉） ==========
                Console.WriteLine("【过滤条件2】周K>月K>季K（不含金叉）");
                Console.WriteLine("----------------------------------------");
                var condition2 = FilterConditionBuilder.BuildKValueOrderCondition();
                var sw2 = Stopwatch.StartNew();
                var results2 = filterOrchestrator.FilterByKValueOrderParallel(condition2, targetDate);
                sw2.Stop();
                Console.WriteLine("结果数量: {0} 只股票", results2.Count);
                Console.WriteLine("处理时间: {0:F2} 秒", sw2.Elapsed.TotalSeconds);
                Console.WriteLine();

                // 获取昨天的K值并排序
                var enrichedResults2 = FilterDisplayHelper.EnrichWithHistory(results2, kdCalculator, targetDate);
                enrichedResults2 = FilterDisplayHelper.SortByQuarterlyK(enrichedResults2, descending: true);
                Console.WriteLine($"条件2结果: {enrichedResults2.Count} 只股票");

                // ========== 过滤条件3：月K>季K or 周K<月K ==========
                Console.WriteLine("【过滤条件3】月K>季K or 周K<月K");
                Console.WriteLine("----------------------------------------");
                var condition3 = FilterConditionBuilder.BuildKValueRelationCondition();
                var sw3 = Stopwatch.StartNew();
                var results3 = filterOrchestrator.FilterByKValueRelationParallel(condition3, targetDate);
                sw3.Stop();
                Console.WriteLine("结果数量: {0} 只股票", results3.Count);
                Console.WriteLine("处理时间: {0:F2} 秒", sw3.Elapsed.TotalSeconds);
                Console.WriteLine();

                // 获取昨天的K值并排序
                var enrichedResults3 = FilterDisplayHelper.EnrichWithHistory(results3, kdCalculator, targetDate);
                enrichedResults3 = FilterDisplayHelper.SortByQuarterlyK(enrichedResults3, descending: true);
                Console.WriteLine($"条件3结果: {enrichedResults3.Count} 只股票");

                stopwatch.Stop();

                // 通过事件通知订阅者（UI层）
                OnFilterCompleted(new FilterResultEventArgs
                {
                    Condition1Results = enrichedResults1,
                    Condition2Results = enrichedResults2,
                    Condition3Results = enrichedResults3,
                    FilterTime = DateTime.Now,
                    ElapsedSeconds = stopwatch.Elapsed.TotalSeconds,
                    ProcessedCount = realTimeCache.Count
                });

                Console.WriteLine();
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
        /// 触发过滤完成事件
        /// </summary>
        protected virtual void OnFilterCompleted(FilterResultEventArgs e)
        {
            FilterCompleted?.Invoke(this, e);
        }

        /// <summary>
        /// 从数据库加载股票代码列表到缓存
        /// 当实时数据缓存为空时调用，使过滤服务可以基于日线数据运行
        /// </summary>
        private void LoadStockCodesFromDatabase()
        {
            try
            {
                var repository = new PostgresStockDataRepository();
                var stockCodes = repository.GetAllStockCodes();

                if (stockCodes != null && stockCodes.Count > 0)
                {
                    Console.WriteLine($"从数据库获取到 {stockCodes.Count} 只股票代码");

                    // 获取股票名称
                    var stockNames = repository.GetAllStockNames();
                    Console.WriteLine($"从stock_info表获取到 {stockNames.Count} 个股票名称");

                    // 如果stock_info表没有数据，尝试从日线数据初始化
                    if (stockNames.Count == 0)
                    {
                        Console.WriteLine("stock_info表为空，尝试从日线数据初始化...");
                        repository.InitializeStockInfoFromDailyData();
                        // 重新获取（此时只有股票代码，没有名称）
                        stockNames = repository.GetAllStockNames();
                    }

                    // 统计数据覆盖情况
                    PrintDataCoverageStatistics(repository, stockCodes);

                    // 为每个股票代码创建一个基本的实时数据记录
                    var records = new List<RealTimeDataRecord>();
                    foreach (var code in stockCodes)
                    {
                        // 尝试获取股票名称，如果没有则使用代码作为名称
                        string name = stockNames.ContainsKey(code) ? stockNames[code] : code;

                        records.Add(new RealTimeDataRecord
                        {
                            StockCode = code,
                            StockName = name,
                            MarketCode = code.StartsWith("6") ? (ushort)1 : (ushort)0,  // 6开头是上海
                            UpdateTime = DateTime.Now
                        });
                    }

                    realTimeCache.UpdateData(records);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"从数据库加载股票代码失败: {ex.Message}");
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
