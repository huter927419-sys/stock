using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MQReceiver.Cache;
using MQReceiver.Calculators;
using MQReceiver.Helpers;
using MQReceiver.Models;
using MQReceiver.Repositories;
using MQReceiver.Services;

namespace MQReceiver.Services
{
    /// <summary>
    /// 图表数据服务
    /// 负责从内存缓存加载K线数据和KD指标数据
    /// 支持合并实时数据显示当日K线
    /// 优化：使用Repository接口，充分利用内存缓存，避免直接数据库查询
    /// </summary>
    public class ChartService
    {
        private readonly IKlineDataRepository _klineRepository;
        private readonly KDCalculator kdCalculator;
        private readonly ExRightsAdjustmentCalculator exRightsCalculator;
        private readonly RealTimeDataCache realTimeCache;
        
        // 缓存最后计算的KD值（用于计算面板，确保与图表一致）
        private readonly Dictionary<string, (double k, double d)> _lastKDValues = new Dictionary<string, (double k, double d)>();

        public ChartService() : this(null)
        {
        }

        public ChartService(RealTimeDataCache cache) : this(cache, null)
        {
        }

        /// <summary>
        /// 使用指定的Repository初始化（支持依赖注入和测试）
        /// </summary>
        public ChartService(RealTimeDataCache cache, IKlineDataRepository klineRepository)
        {
            _klineRepository = klineRepository ?? new PostgresKlineDataRepository();
            realTimeCache = cache;
            exRightsCalculator = new ExRightsAdjustmentCalculator();
            
            // 初始化数据边界管理器（用于实时数据合并）
            var dataBoundaryManager = cache != null ? new DataBoundaryManager(cache) : null;
            
            // 创建KD计算器，传入实时数据缓存和数据边界管理器，确保KD计算时也能使用实时数据
            kdCalculator = cache != null && dataBoundaryManager != null
                ? new KDCalculator(_klineRepository, cache, dataBoundaryManager)
                : new KDCalculator(_klineRepository);
            
            // 确保启用实时数据合并
            kdCalculator.EnableRealTimeDataMerge = true;
            
            // 使用真实数据计算KD（不使用前复权价格）
            kdCalculator.EnableForwardAdjustment = false;
        }

        /// <summary>
        /// 加载股票图表数据
        /// </summary>
        /// <param name="stockCode">股票代码</param>
        /// <param name="days">加载最近多少天的数据（默认0表示加载所有历史数据，180表示最近180天）</param>
        public Models.ChartData LoadChartData(string stockCode, int days = 0)
        {
            var chartData = new Models.ChartData
            {
                StockCode = stockCode,
                StockName = GetStockName(stockCode)
            };

            try
            {
                // 1. 加载日K线数据（前复权，用于图表显示）
                chartData.DailyKline = LoadDailyKlineData(stockCode, days);

                // 2. 并行计算周/月/季KD值（使用真实数据，不前复权）
                // 重要：为了与计算面板保持一致，KD计算必须使用完整历史数据
                // 特别是季K需要至少9个季度（约27个月）的数据
                if (chartData.DailyKline.Count > 0)
                {
                    // 性能优化：减少日志输出
                    // Console.WriteLine($"[图表加载] {stockCode}: 开始计算KD指标，K线数据量={chartData.DailyKline.Count}");
                    
                    // 加载真实数据（不前复权）用于KD计算
                    // 对于KD计算，必须加载所有历史数据（days=0），确保与计算面板一致
                    var realKlineData = LoadDailyKlineDataReal(stockCode, 0); // 强制加载所有历史数据
                    
                    // 并行计算3个周期的KD指标
                    var weeklyTask = Task.Run(() => CalculateKDWithSmoothing(stockCode, realKlineData, "week"));
                    var monthlyTask = Task.Run(() => CalculateKDWithSmoothing(stockCode, realKlineData, "month"));
                    var quarterlyTask = Task.Run(() => CalculateKDWithSmoothing(stockCode, realKlineData, "quarter"));

                    // 等待所有任务完成
                    Task.WaitAll(weeklyTask, monthlyTask, quarterlyTask);

                    chartData.WeeklyKD = weeklyTask.Result;
                    chartData.MonthlyKD = monthlyTask.Result;
                    chartData.QuarterlyKD = quarterlyTask.Result;

                    // 性能优化：减少日志输出
                    // Console.WriteLine($"[图表加载] {stockCode}: KD计算完成 - 周KD={chartData.WeeklyKD?.Count ?? 0}, 月KD={chartData.MonthlyKD?.Count ?? 0}, 季KD={chartData.QuarterlyKD?.Count ?? 0}");
                    
                    // 如果KD数据为空，输出警告
                    if ((chartData.WeeklyKD == null || chartData.WeeklyKD.Count == 0) &&
                        (chartData.MonthlyKD == null || chartData.MonthlyKD.Count == 0) &&
                        (chartData.QuarterlyKD == null || chartData.QuarterlyKD.Count == 0))
                    {
                        Console.WriteLine($"[图表加载] ⚠️ 警告: {stockCode} 所有KD数据为空，可能原因：");
                        Console.WriteLine($"  1. KD计算失败（检查控制台错误信息）");
                        Console.WriteLine($"  2. 数据不足（需要至少9个周期的数据）");
                        Console.WriteLine($"  3. 数据库连接问题");
                    }
                }
                else
                {
                    Console.WriteLine($"[图表加载] {stockCode}: K线数据为空，跳过KD计算");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载图表数据失败: {ex.Message}");
            }

            return chartData;
        }

        /// <summary>
        /// 异步加载股票图表数据（不阻塞UI线程）
        /// </summary>
        /// <param name="stockCode">股票代码</param>
        /// <param name="days">加载最近多少天的数据（默认0表示加载所有历史数据，180表示最近180天）</param>
        public async Task<Models.ChartData> LoadChartDataAsync(string stockCode, int days = 0)
        {
            var chartData = new Models.ChartData
            {
                StockCode = stockCode,
                StockName = GetStockName(stockCode)
            };

            try
            {
                // 1. 异步加载日K线数据（前复权，用于图表显示）
                var klineData = await Task.Run(() => LoadDailyKlineData(stockCode, days));
                chartData.DailyKline = klineData;

                // 2. 并行计算周/月/季KD值（使用真实数据，不前复权）
                // 重要：为了与计算面板保持一致，KD计算必须使用完整历史数据
                if (klineData.Count > 0)
                {
                    // 性能优化：减少日志输出
                    // Console.WriteLine($"[图表加载] {stockCode}: 开始计算KD指标，K线数据量={klineData.Count}");
                    
                    // 加载真实数据（不前复权）用于KD计算
                    // 对于KD计算，必须加载所有历史数据（days=0），确保与计算面板一致
                    var realKlineData = await Task.Run(() => LoadDailyKlineDataReal(stockCode, 0)); // 强制加载所有历史数据
                    
                    // 并行计算3个周期的KD指标（使用平滑插值）
                    var weeklyTask = Task.Run(() => CalculateKDWithSmoothing(stockCode, realKlineData, "week"));
                    var monthlyTask = Task.Run(() => CalculateKDWithSmoothing(stockCode, realKlineData, "month"));
                    var quarterlyTask = Task.Run(() => CalculateKDWithSmoothing(stockCode, realKlineData, "quarter"));

                    // 等待所有任务完成
                    await Task.WhenAll(weeklyTask, monthlyTask, quarterlyTask);

                    chartData.WeeklyKD = await weeklyTask;
                    chartData.MonthlyKD = await monthlyTask;
                    chartData.QuarterlyKD = await quarterlyTask;

                    // 性能优化：减少日志输出
                    // Console.WriteLine($"[图表加载] {stockCode}: KD计算完成 - 周KD={chartData.WeeklyKD?.Count ?? 0}, 月KD={chartData.MonthlyKD?.Count ?? 0}, 季KD={chartData.QuarterlyKD?.Count ?? 0}");
                    
                    // 如果KD数据为空，输出警告
                    if ((chartData.WeeklyKD == null || chartData.WeeklyKD.Count == 0) &&
                        (chartData.MonthlyKD == null || chartData.MonthlyKD.Count == 0) &&
                        (chartData.QuarterlyKD == null || chartData.QuarterlyKD.Count == 0))
                    {
                        Console.WriteLine($"[图表加载] ⚠️ 警告: {stockCode} 所有KD数据为空，可能原因：");
                        Console.WriteLine($"  1. KD计算失败（检查控制台错误信息）");
                        Console.WriteLine($"  2. 数据不足（需要至少9个周期的数据）");
                        Console.WriteLine($"  3. 数据库连接问题");
                    }
                }
                else
                {
                    Console.WriteLine($"[图表加载] {stockCode}: K线数据为空，跳过KD计算");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载图表数据失败: {ex.Message}");
            }

            return chartData;
        }

        /// <summary>
        /// 计算KD并进行平滑处理（优化版：快速批量计算+线性插值）
        /// </summary>
        private List<Models.KDDataPoint> CalculateKDWithSmoothing(string stockCode, List<Models.CandleDataPoint> dailyKline, string cycleType)
        {
            var result = new List<Models.KDDataPoint>();
            if (dailyKline == null || dailyKline.Count == 0) return result;

            var startTime = DateTime.Now;
            // 性能优化：减少日志输出（仅在需要调试时启用）
            // Console.WriteLine($"[KD平滑计算] {stockCode} {cycleType}周期: 开始，数据量={dailyKline.Count}");

            try
            {
                // 步骤1：按周期聚合
                var periods = new Dictionary<string, List<Models.CandleDataPoint>>();
                foreach (var candle in dailyKline)
                {
                    string key = GetCycleKey(candle.Date, cycleType);
                    if (!periods.ContainsKey(key))
                        periods[key] = new List<Models.CandleDataPoint>();
                    periods[key].Add(candle);
                }

                var sortedPeriods = periods.OrderBy(p => p.Key).ToList();
                
                // 步骤2：先聚合每个周期为周期K线（标准算法：在聚合后的周期K线上计算KD）
                var aggregatedPeriods = new List<(string key, double open, double high, double low, double close, int startIdx, int endIdx)>();
                
                // 构建日期到索引的映射（加速查找）
                var dateToIndex = new Dictionary<DateTime, int>();
                for (int i = 0; i < dailyKline.Count; i++)
                {
                    dateToIndex[dailyKline[i].Date] = i;
                }
                
                // 聚合每个周期为周期K线
                foreach (var period in sortedPeriods)
                {
                    var candles = period.Value.OrderBy(c => c.Date).ToList();
                    if (candles.Count > 0)
                    {
                        var periodDates = candles.Select(c => c.Date).OrderBy(dt => dt).ToList();
                        int startIdx = dateToIndex[periodDates.First()];
                        int endIdx = dateToIndex[periodDates.Last()];
                        
                        aggregatedPeriods.Add((
                            period.Key,
                            candles.First().Open,      // 开盘价：周期第一天
                            candles.Max(c => c.High),   // 最高价：周期内最高
                            candles.Min(c => c.Low),    // 最低价：周期内最低
                            candles.Last().Close,       // 收盘价：周期最后一天
                            startIdx,
                            endIdx
                        ));
                    }
                }
                
                // 步骤3：在聚合后的周期K线上计算KD值
                const int kdPeriod = 9;
                int actualPeriod = Math.Min(kdPeriod, aggregatedPeriods.Count);
                if (actualPeriod < 2)
                {
                    Console.WriteLine($"[KD平滑计算] {stockCode} {cycleType}: 数据不足");
                    return result;
                }

                var kdByPeriod = new List<(string key, double k, double d, int startIdx, int endIdx)>();
                decimal k = 50m, d = 50m;

                // KD计算参数（与标准公式一致：M1=3, M2=3）
                const int m1 = 3; // K值平滑周期
                const int m2 = 3; // D值平滑周期

                for (int i = actualPeriod - 1; i < aggregatedPeriods.Count; i++)
                {
                    // 使用过去N个周期的聚合K线数据计算RSV（标准算法）
                    var recentPeriods = aggregatedPeriods.Skip(i - actualPeriod + 1).Take(actualPeriod).ToList();
                    decimal highest = (decimal)recentPeriods.Max(p => p.high);  // 过去N个周期的最高价
                    decimal lowest = (decimal)recentPeriods.Min(p => p.low);     // 过去N个周期的最低价
                    decimal close = (decimal)aggregatedPeriods[i].close;         // 当前周期的收盘价

                    decimal rsv = (highest == lowest) ? 50m : (close - lowest) / (highest - lowest) * 100m;
                    
                    // 使用SMA函数计算K值：K = SMA(RSV, M1, 1)
                    k = SMA(rsv, m1, k);
                    
                    // 使用SMA函数计算D值：D = SMA(K, M2, 1)
                    d = SMA(k, m2, d);

                    kdByPeriod.Add((
                        aggregatedPeriods[i].key, 
                        (double)k, 
                        (double)d, 
                        aggregatedPeriods[i].startIdx, 
                        aggregatedPeriods[i].endIdx
                    ));
                }
                
                // 保存最后一个周期的KD值（用于计算面板，确保与图表一致）
                if (kdByPeriod.Count > 0)
                {
                    var lastPeriod = kdByPeriod.Last();
                    // 将最后一个周期的KD值存储，供外部获取（用于计算面板）
                    string cacheKey = $"{stockCode}_{cycleType}";
                    _lastKDValues[cacheKey] = (lastPeriod.k, lastPeriod.d);
                }

                // 步骤3：优化的插值处理 - 确保与日线完全对齐
                double lastK = 50.0, lastD = 50.0; // 默认初始值
                
                for (int i = 0; i < dailyKline.Count; i++)
                {
                    // 找到包含当前索引的周期
                    int periodIdx = kdByPeriod.FindIndex(p => i >= p.startIdx && i <= p.endIdx);
                    
                    double kValue, dValue;
                    
                    if (periodIdx < 0)
                    {
                        // 如果找不到对应周期（数据不足的前几天），使用默认值或前一个值
                        // 确保每个日期都有KD值，实现完全对齐
                        kValue = lastK;
                        dValue = lastD;
                    }
                    else
                    {
                        // 边界检查：确保periodIdx在有效范围内
                        if (periodIdx >= 0 && periodIdx < kdByPeriod.Count)
                        {
                            var currentPeriod = kdByPeriod[periodIdx];
                            int totalDaysInPeriod = currentPeriod.endIdx - currentPeriod.startIdx + 1;
                            int dayIndexInPeriod = i - currentPeriod.startIdx;

                            // 第一个周期或只有一天的周期，不插值
                            if (periodIdx == 0 || totalDaysInPeriod == 1)
                            {
                                kValue = currentPeriod.k;
                                dValue = currentPeriod.d;
                            }
                            else
                            {
                                // 线性插值：从上一周期平滑过渡到当前周期
                                // 确保上一周期索引有效
                                if (periodIdx - 1 >= 0 && periodIdx - 1 < kdByPeriod.Count)
                                {
                                    var prevPeriod = kdByPeriod[periodIdx - 1];
                                    double ratio = (double)dayIndexInPeriod / (totalDaysInPeriod - 1);
                                    
                                    kValue = prevPeriod.k + (currentPeriod.k - prevPeriod.k) * ratio;
                                    dValue = prevPeriod.d + (currentPeriod.d - prevPeriod.d) * ratio;
                                }
                                else
                                {
                                    // 如果无法获取上一周期，使用当前周期的值
                                    kValue = currentPeriod.k;
                                    dValue = currentPeriod.d;
                                }
                            }
                            
                            // 更新最后有效值
                            lastK = kValue;
                            lastD = dValue;
                        }
                        else
                        {
                            // periodIdx超出范围，使用默认值
                            kValue = lastK;
                            dValue = lastD;
                        }
                    }

                    // 确保每个K线日期都有对应的KD值，实现完全对齐
                    result.Add(new Models.KDDataPoint
                    {
                        Date = dailyKline[i].Date,
                        K = Math.Round(kValue, 2),
                        D = Math.Round(dValue, 2)
                    });
                }

                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                // 性能优化：减少日志输出（仅在需要时启用）
                // Console.WriteLine($"[KD平滑计算] {stockCode} {cycleType}: 完成！耗时={elapsed:F0}ms, 结果数={result.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KD平滑计算] {stockCode} {cycleType}: 异常 - {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 获取指定日期的KD值（使用与图表相同的计算逻辑，确保一致性）
        /// 按 targetDate 取「该日或之前最近一个交易日」的KD，用于金叉等需要「今日/昨日」对比的计算。
        /// 若 targetDate 晚于所有K线日期则用最后一笔；若早于第一笔则返回 null。
        /// </summary>
        /// <param name="stockCode">股票代码</param>
        /// <param name="targetDate">目标日期</param>
        /// <param name="cycleType">周期类型（week/month/quarter）</param>
        /// <returns>KD结果，如果计算失败返回null</returns>
        public KDResult GetKDValue(string stockCode, DateTime targetDate, string cycleType)
        {
            try
            {
                // 使用与图表相同的计算逻辑：加载所有历史数据，使用真实价格
                var realKlineData = LoadDailyKlineDataReal(stockCode, 0);
                if (realKlineData == null || realKlineData.Count == 0)
                    return null;

                // 计算KD（使用与图表相同的逻辑，包括插值处理）
                var kdData = CalculateKDWithSmoothing(stockCode, realKlineData, cycleType);
                if (kdData == null || kdData.Count == 0)
                    return null;

                // 取 targetDate 及之前最近的交易日的KD值，使 今日/昨日 能区分，金叉等公式才有效
                var kdAtDate = kdData.Where(k => k.Date <= targetDate.Date).OrderByDescending(k => k.Date).FirstOrDefault();
                if (kdAtDate == null)
                    return null;

                return new KDResult
                {
                    StockCode = stockCode,
                    Date = kdAtDate.Date,
                    K = (decimal)kdAtDate.K,
                    D = (decimal)kdAtDate.D,
                    RSV = 0
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChartService.GetKDValue] 计算失败 {stockCode} {cycleType}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// SMA函数（加权移动平均）：SMA(value, period, weight)
        /// 当weight=1时，公式为：SMA(value, period, 1) = (period-1)/period * 前值 + 1/period * 当前值
        /// 对应标准KD公式：K = SMA(RSV, M1, 1), D = SMA(K, M2, 1)
        /// </summary>
        /// <param name="currentValue">当前值（RSV或K值）</param>
        /// <param name="period">平滑周期（M1或M2，默认3）</param>
        /// <param name="previousValue">前一个值（前一个K值或前一个D值）</param>
        /// <returns>SMA计算结果</returns>
        private decimal SMA(decimal currentValue, int period, decimal previousValue)
        {
            // SMA(value, period, 1) = (period-1)/period * 前值 + 1/period * 当前值
            return ((period - 1m) / period) * previousValue + (1m / period) * currentValue;
        }

        /// <summary>
        /// 获取日期所属的周期键（用于优化，判断是否在同一周期内）
        /// </summary>
        private string GetCycleKey(DateTime date, string cycleType)
        {
            switch (cycleType.ToLower())
            {
                case "week":
                    // 获取该周的周一日期作为键
                    int daysFromMonday = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
                    DateTime monday = date.AddDays(-daysFromMonday);
                    return $"W{monday:yyyyMMdd}";
                case "month":
                    return $"M{date:yyyyMM}";
                case "quarter":
                    int quarter = (date.Month - 1) / 3 + 1;
                    return $"Q{date.Year}{quarter}";
                default:
                    return date.ToString("yyyyMMdd");
            }
        }

        /// <summary>
        /// 加载日K线数据（使用前复权价格，确保图表显示连续无跳空）
        /// 优化：从内存缓存加载，避免直接数据库查询
        /// 如果有实时数据且当日数据库无数据，则添加实时K线
        /// </summary>
        /// <param name="stockCode">股票代码</param>
        /// <param name="days">加载最近多少天的数据。如果days <= 0，则加载所有历史数据</param>
        private List<Models.CandleDataPoint> LoadDailyKlineData(string stockCode, int days)
        {
            var result = new List<Models.CandleDataPoint>();
            DateTime today = DateTime.Now.Date;
            DateTime endDate = today;
            DateTime startDate;

            try
            {
                // 获取股票的数据日期范围
                var dateRange = _klineRepository.GetDataDateRange(stockCode);
                
                if (!dateRange.StartDate.HasValue)
                {
                    // 如果没有数据，返回空列表
                    return result;
                }

                // 确定加载的日期范围
                if (days > 0)
                {
                    // 加载指定天数范围的数据
                    startDate = endDate.AddDays(-days);
                    // 确保不早于股票的最早数据
                    if (dateRange.StartDate.HasValue && startDate < dateRange.StartDate.Value)
                    {
                        startDate = dateRange.StartDate.Value;
                    }
                }
                else
                {
                    // 加载所有历史数据
                    startDate = dateRange.StartDate.Value;
                }

                // 从Repository获取K线数据（优先从内存缓存读取）
                var dailyData = _klineRepository.GetDailyData(stockCode, startDate, endDate);

                // 使用前复权价格计算K线数据（确保图表显示连续，无跳空）
                // 性能优化：减少日志输出
                // Console.WriteLine($"[图表K线] {stockCode}: 使用前复权数据，总数据={dailyData.Count}");
                
                // 性能优化：使用批量前复权计算（一次性计算所有OHLC）
                var ohlcDict = dailyData.ToDictionary(
                    k => k.TradeDate,
                    k => (k.Open, k.High, k.Low, k.Close)
                );
                
                var adjustedOHLC = exRightsCalculator.BatchCalculateOHLCAdjustedPrices(stockCode, ohlcDict);
                
                // 性能优化：移除调试日志（仅在需要时启用）
                // 如需调试，可以取消下面的注释
                /*
                int adjustedCount = 0;
                foreach (var kvp in adjustedOHLC)
                {
                    var original = ohlcDict[kvp.Key];
                    var adjusted = kvp.Value;
                    decimal priceDiff = Math.Abs(original.Close - adjusted.Close);
                    if (priceDiff > 0.01m)
                    {
                        adjustedCount++;
                        if (adjustedCount <= 3)
                        {
                            Console.WriteLine($"[前复权调试] {stockCode} {kvp.Key:yyyy-MM-dd}: 原价={original.Close:F2}, 复权价={adjusted.Close:F2}, 差异={priceDiff:F2}");
                        }
                    }
                }
                if (adjustedCount > 0)
                {
                    Console.WriteLine($"[前复权调试] {stockCode}: 共发现 {adjustedCount} 个交易日有价格调整");
                }
                */
                
                // 转换为图表数据格式（使用前复权价格）
                foreach (var data in dailyData.OrderBy(d => d.TradeDate))
                {
                    var adjusted = adjustedOHLC.ContainsKey(data.TradeDate)
                        ? adjustedOHLC[data.TradeDate]
                        : (data.Open, data.High, data.Low, data.Close);
                    
                    result.Add(new Models.CandleDataPoint
                    {
                        Date = data.TradeDate,
                        Open = (double)adjusted.Open,
                        High = (double)adjusted.High,
                        Low = (double)adjusted.Low,
                        Close = (double)adjusted.Close,
                        Volume = (double)data.Volume
                    });
                }

                // 尝试添加实时数据作为当日K线
                AppendRealTimeCandle(result, stockCode, today);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载日K线数据失败 [{stockCode}]: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 加载日K线数据（使用真实数据，不前复权，专门用于KD计算）
        /// </summary>
        /// <param name="stockCode">股票代码</param>
        /// <param name="days">加载最近多少天的数据。如果days <= 0，则加载所有历史数据</param>
        private List<Models.CandleDataPoint> LoadDailyKlineDataReal(string stockCode, int days)
        {
            var result = new List<Models.CandleDataPoint>();
            DateTime today = DateTime.Now.Date;
            DateTime endDate = today;
            DateTime startDate;

            try
            {
                // 获取股票的数据日期范围
                var dateRange = _klineRepository.GetDataDateRange(stockCode);
                
                if (!dateRange.StartDate.HasValue)
                {
                    // 如果没有数据，返回空列表
                    return result;
                }

                // 确定加载的日期范围
                if (days > 0)
                {
                    // 加载指定天数范围的数据
                    startDate = endDate.AddDays(-days);
                    // 确保不早于股票的最早数据
                    if (dateRange.StartDate.HasValue && startDate < dateRange.StartDate.Value)
                    {
                        startDate = dateRange.StartDate.Value;
                    }
                }
                else
                {
                    // 加载所有历史数据
                    startDate = dateRange.StartDate.Value;
                }

                // 从Repository获取K线数据（优先从内存缓存读取）
                var dailyData = _klineRepository.GetDailyData(stockCode, startDate, endDate);

                // 使用真实数据（不使用前复权价格）
                // 性能优化：减少日志输出
                // Console.WriteLine($"[图表K线-真实数据] {stockCode}: 使用真实数据用于KD计算，总数据={dailyData.Count}");
                
                // 转换为图表数据格式（直接使用原始价格）
                foreach (var data in dailyData.OrderBy(d => d.TradeDate))
                {
                    result.Add(new Models.CandleDataPoint
                    {
                        Date = data.TradeDate,
                        Open = (double)data.Open,
                        High = (double)data.High,
                        Low = (double)data.Low,
                        Close = (double)data.Close,
                        Volume = (double)data.Volume
                    });
                }

                // 尝试添加实时数据作为当日K线（使用真实价格）
                AppendRealTimeCandleReal(result, stockCode, today);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载日K线真实数据失败 [{stockCode}]: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 将实时数据添加为当日K线（使用真实价格，不前复权）
        /// </summary>
        private void AppendRealTimeCandleReal(List<Models.CandleDataPoint> result, string stockCode, DateTime today)
        {
            if (realTimeCache == null)
                return;

            var realTimeData = realTimeCache.GetData(stockCode);
            if (realTimeData == null)
                return;

            // 检查实时数据是否有效（有价格数据）
            if (realTimeData.Open <= 0 || realTimeData.NewPrice <= 0)
                return;

            // 使用真实数据（不使用前复权价格）
            decimal realOpen = realTimeData.Open;
            decimal realHigh = realTimeData.High;
            decimal realLow = realTimeData.Low;
            decimal realClose = realTimeData.NewPrice;

            // 检查数据库中是否已有今日数据
            bool hasTodayData = result.Count > 0 && result[result.Count - 1].Date.Date == today;

            if (hasTodayData)
            {
                // 更新今日K线数据（用实时数据覆盖，使用真实价格）
                var todayCandle = result[result.Count - 1];
                todayCandle.High = Math.Max(todayCandle.High, (double)realHigh);
                todayCandle.Low = Math.Min(todayCandle.Low, (double)realLow);
                todayCandle.Close = (double)realClose;
                todayCandle.Volume = (double)realTimeData.Volume;
                // 如果实时数据的开盘价与数据库不同，也更新（可能是集合竞价后的价格）
                if (realTimeData.Open > 0)
                {
                    todayCandle.Open = (double)realOpen;
                }
            }
            else
            {
                // 仅在今日是工作日时添加实时数据（避免在周末/节假日添加上一交易日的重复数据）
                if (today.DayOfWeek == DayOfWeek.Saturday || today.DayOfWeek == DayOfWeek.Sunday)
                    return;

                // 检查最后一个交易日是否是昨天（如果不是，说明今天可能是节假日）
                if (result.Count > 0)
                {
                    var lastTradeDate = result[result.Count - 1].Date.Date;
                    var daysSinceLastTrade = (today - lastTradeDate).Days;

                    // 如果距离上一个交易日超过3天，很可能今天是长假期，不添加实时数据
                    if (daysSinceLastTrade > 3)
                        return;
                }

                // 添加新的今日K线（使用实时数据，真实价格）
                result.Add(new Models.CandleDataPoint
                {
                    Date = today,
                    Open = (double)realOpen,
                    High = (double)realHigh,
                    Low = (double)realLow,
                    Close = (double)realClose,
                    Volume = (double)realTimeData.Volume
                });
            }
        }

        /// <summary>
        /// 将实时数据添加为当日K线（仅当今日是交易日且数据库中没有当日数据）
        /// 优化：实时数据也需要计算复权价格，保持与历史数据一致
        /// </summary>
        private void AppendRealTimeCandle(List<Models.CandleDataPoint> result, string stockCode, DateTime today)
        {
            if (realTimeCache == null)
                return;

            var realTimeData = realTimeCache.GetData(stockCode);
            if (realTimeData == null)
                return;

            // 检查实时数据是否有效（有价格数据）
            if (realTimeData.Open <= 0 || realTimeData.NewPrice <= 0)
                return;

            // 计算实时数据的前复权价格（与历史K线数据保持一致）
            decimal adjOpen = exRightsCalculator.CalculateForwardAdjustedPrice(stockCode, today, realTimeData.Open);
            decimal adjHigh = exRightsCalculator.CalculateForwardAdjustedPrice(stockCode, today, realTimeData.High);
            decimal adjLow = exRightsCalculator.CalculateForwardAdjustedPrice(stockCode, today, realTimeData.Low);
            decimal adjClose = exRightsCalculator.CalculateForwardAdjustedPrice(stockCode, today, realTimeData.NewPrice);

            // 检查数据库中是否已有今日数据
            bool hasTodayData = result.Count > 0 && result[result.Count - 1].Date.Date == today;

            if (hasTodayData)
            {
                // 更新今日K线数据（用实时数据覆盖，使用前复权价格）
                var todayCandle = result[result.Count - 1];
                todayCandle.High = Math.Max(todayCandle.High, (double)adjHigh);
                todayCandle.Low = Math.Min(todayCandle.Low, (double)adjLow);
                todayCandle.Close = (double)adjClose;
                todayCandle.Volume = (double)realTimeData.Volume;
                // 如果实时数据的开盘价与数据库不同，也更新（可能是集合竞价后的价格）
                if (realTimeData.Open > 0)
                {
                    todayCandle.Open = (double)adjOpen;
                }
            }
            else
            {
                // 仅在今日是工作日时添加实时数据（避免在周末/节假日添加上一交易日的重复数据）
                // 如果今日是周末或数据库中没有任何近期数据，则不添加
                if (today.DayOfWeek == DayOfWeek.Saturday || today.DayOfWeek == DayOfWeek.Sunday)
                    return;

                // 检查最后一个交易日是否是昨天（如果不是，说明今天可能是节假日）
                if (result.Count > 0)
                {
                    var lastTradeDate = result[result.Count - 1].Date.Date;
                    var daysSinceLastTrade = (today - lastTradeDate).Days;

                    // 如果距离上一个交易日超过3天，很可能今天是长假期，不添加实时数据
                    if (daysSinceLastTrade > 3)
                        return;
                }

                // 添加新的今日K线（使用实时数据，前复权价格）
                result.Add(new Models.CandleDataPoint
                {
                    Date = today,
                    Open = (double)adjOpen,
                    High = (double)adjHigh,
                    Low = (double)adjLow,
                    Close = (double)adjClose,
                    Volume = (double)realTimeData.Volume
                });
            }
        }

        /// <summary>
        /// 加载KD指标序列
        /// </summary>
        private List<Models.KDDataPoint> LoadKDSequence(string stockCode, DateTime startDate, DateTime endDate, string cycleType, int maxPeriods)
        {
            var result = new List<Models.KDDataPoint>();

            try
            {
                // 使用KDCalculator获取历史序列
                List<KDResult> kdSequence = null;

                switch (cycleType.ToLower())
                {
                    case "week":
                        kdSequence = kdCalculator.GetWeeklyKDSequence(stockCode, endDate, maxPeriods);
                        break;
                    case "month":
                        kdSequence = kdCalculator.GetMonthlyKDSequence(stockCode, endDate, maxPeriods);
                        break;
                    case "quarter":
                        kdSequence = kdCalculator.GetQuarterlyKDSequence(stockCode, endDate, maxPeriods);
                        break;
                }

                if (kdSequence != null)
                {
                    foreach (var kd in kdSequence)
                    {
                        if (kd.Date >= startDate && kd.Date <= endDate)
                        {
                            result.Add(new Models.KDDataPoint
                            {
                                Date = kd.Date,
                                K = (double)kd.K,
                                D = (double)kd.D
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载{cycleType}KD序列失败: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 获取股票名称（从内存缓存获取，性能更好）
        /// </summary>
        private string GetStockName(string stockCode)
        {
            return StockInfoCache.Instance.GetStockName(stockCode);
        }
    }
}
