using System;
using System.Collections.Generic;
using System.Linq;
using MQReceiver.Configuration;
using MQReceiver.Helpers;
using MQReceiver.Models;
using MQReceiver.Repositories;
using MQReceiver.Services;
using MQReceiver.Cache;
using MQReceiver.DataProcessing.Factories;

namespace MQReceiver.Calculators
{
    /// <summary>
    /// KD指标计算器
    /// 支持周、月、季周期的KD指标计算
    /// 支持盘中实时数据合并计算
    ///
    /// KD指标计算公式：
    /// 1. RSV = (收盘价 - 最低价) / (最高价 - 最低价) * 100
    /// 2. K值 = (2/3) * 前一日K值 + (1/3) * 当日RSV
    /// 3. D值 = (2/3) * 前一日D值 + (1/3) * 当日K值
    /// 
    /// 重要：使用前复权价格计算KD指标，确保除权除息不影响技术指标的连续性
    /// </summary>
    public class KDCalculator
    {
        private readonly string connectionString;
        private readonly IKlineDataRepository _klineRepository;
        private readonly RealTimeDataCache _realTimeCache;
        private readonly DataBoundaryManager _dataBoundaryManager;
        private readonly ExRightsAdjustmentCalculator _exRightsCalculator;
        private const int DEFAULT_PERIOD = 9; // 默认周期为9

        /// <summary>
        /// 是否启用实时数据合并（盘中计算时使用）
        /// </summary>
        public bool EnableRealTimeDataMerge { get; set; } = true;

        /// <summary>
        /// 是否启用前复权价格计算（性能开关）
        /// true: 使用前复权价格（准确，但较慢）
        /// false: 使用原始价格（快速，但除权时会有跳空）
        /// </summary>
        public bool EnableForwardAdjustment { get; set; } = false;

        // 调试用变量 - 按周期类型分别记录
        private static bool _debugWeekLogged = false;
        private static bool _debugMonthLogged = false;
        private static bool _debugQuarterLogged = false;
        private static readonly object _debugLock = new object();

        /// <summary>「数据不足」诊断：抽样条数，避免刷屏</summary>
        private static int _dataInsufficientDiagnoseCount = 0;
        private const int MaxDataInsufficientDiagnoseLogs = 8;

        /// <summary>
        /// 使用默认仓储初始化（向后兼容）
        /// </summary>
        public KDCalculator()
        {
            connectionString = DatabaseConnectionHelper.BuildConnectionString();
            _klineRepository = RepositoryFactory.GetKlineDataRepository();
            _exRightsCalculator = new ExRightsAdjustmentCalculator();
        }

        /// <summary>
        /// 使用指定仓储初始化（依赖注入）
        /// </summary>
        public KDCalculator(IKlineDataRepository klineRepository)
        {
            _klineRepository = klineRepository ?? throw new ArgumentNullException(nameof(klineRepository));
            connectionString = DatabaseConnectionHelper.BuildConnectionString();
            _exRightsCalculator = new ExRightsAdjustmentCalculator();
        }

        /// <summary>
        /// 使用仓储和实时数据缓存初始化（完整依赖注入）
        /// </summary>
        public KDCalculator(IKlineDataRepository klineRepository, RealTimeDataCache realTimeCache, DataBoundaryManager dataBoundaryManager)
        {
            _klineRepository = klineRepository ?? throw new ArgumentNullException(nameof(klineRepository));
            _realTimeCache = realTimeCache;
            _dataBoundaryManager = dataBoundaryManager ?? new DataBoundaryManager(realTimeCache);
            connectionString = DatabaseConnectionHelper.BuildConnectionString();
            _exRightsCalculator = new ExRightsAdjustmentCalculator();
        }

        /// <summary>
        /// 计算周KD指标
        /// </summary>
        public KDResult CalculateWeeklyKD(string stockCode, DateTime targetDate, int period = DEFAULT_PERIOD)
        {
            return CalculateKD(stockCode, targetDate, period, "week");
        }

        /// <summary>
        /// 计算月KD指标
        /// </summary>
        public KDResult CalculateMonthlyKD(string stockCode, DateTime targetDate, int period = DEFAULT_PERIOD)
        {
            return CalculateKD(stockCode, targetDate, period, "month");
        }

        /// <summary>
        /// 计算季KD指标
        /// </summary>
        public KDResult CalculateQuarterlyKD(string stockCode, DateTime targetDate, int period = DEFAULT_PERIOD)
        {
            return CalculateKD(stockCode, targetDate, period, "quarter");
        }

        /// <summary>
        /// 获取历史KD序列（用于判断"不含金叉"条件）
        /// 返回指定周期数内的KD值序列
        /// </summary>
        public List<KDResult> GetHistoricalKDSequence(string stockCode, DateTime targetDate, int lookbackPeriods, string cycleType, int period = DEFAULT_PERIOD)
        {
            var result = new List<KDResult>();

            try
            {
                // 尝试从Redis缓存获取历史序列
                string cacheKey = $"kd:seq:{stockCode}:{cycleType}:{targetDate:yyyyMMdd}:{lookbackPeriods}";
                var cachedResult = RedisHelper.GetCache<List<KDResult>>(cacheKey);
                if (cachedResult != null && cachedResult.Count > 0)
                {
                    return cachedResult; // 缓存命中
                }

                // 获取聚合后的K线数据
                var aggregatedData = GetAggregatedData(stockCode, targetDate, period, cycleType);

                // 如果数据不足，尝试使用较短周期（最少需要2个周期的数据）
                int actualPeriod = period;
                if (aggregatedData.Count < period)
                {
                    if (aggregatedData.Count >= 2)
                    {
                        actualPeriod = aggregatedData.Count;
                        Console.WriteLine($"[KD序列] {stockCode} {cycleType}周期数据不足{period}个，使用实际周期数: {actualPeriod}");
                    }
                    else
                    {
                        Console.WriteLine($"[KD序列] {stockCode} {cycleType}周期数据不足，仅有{aggregatedData.Count}个周期");
                        return result; // 数据确实不足
                    }
                }

                // 计算每个周期的KD值
                var rsvList = new List<decimal>();
                for (int i = actualPeriod - 1; i < aggregatedData.Count; i++)
                {
                    var periodData = aggregatedData.Skip(i - actualPeriod + 1).Take(actualPeriod).ToList();
                    decimal highest = periodData.Max(data => data.High);
                    decimal lowest = periodData.Min(data => data.Low);
                    decimal close = aggregatedData[i].Close;

                    if (highest == lowest)
                    {
                        rsvList.Add(50);
                    }
                    else
                    {
                        decimal rsv = (close - lowest) / (highest - lowest) * 100;
                        rsvList.Add(rsv);
                    }
                }

                // 计算K值和D值序列 - O(n)优化版本
                // 顺序计算一次，保存所有K和D值
                var kValues = new List<decimal>(rsvList.Count);
                var dValues = new List<decimal>(rsvList.Count);
                decimal k = 50;
                decimal d = 50;

                for (int i = 0; i < rsvList.Count; i++)
                {
                    // 使用SMA函数计算K和D值（与标准公式一致）
                    const int m1 = 3; // K值平滑周期（M1）
                    const int m2 = 3; // D值平滑周期（M2）
                    k = SMA(rsvList[i], m1, k);
                    d = SMA(k, m2, d);
                    kValues.Add(k);
                    dValues.Add(d);
                }

                // 从后往前取指定数量的周期
                int startIndex = Math.Max(0, rsvList.Count - lookbackPeriods);

                for (int i = startIndex; i < rsvList.Count; i++)
                {
                    result.Add(new KDResult
                    {
                        StockCode = stockCode,
                        Date = aggregatedData[actualPeriod - 1 + i].Date,
                        K = Math.Round(kValues[i], 2),
                        D = Math.Round(dValues[i], 2),
                        RSV = rsvList[i]
                    });
                }

                // 写入Redis缓存（TTL：1天）
                if (result.Count > 0)
                {
                    RedisHelper.SetCache(cacheKey, result, TimeSpan.FromDays(1));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取历史KD序列失败: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 获取周KD历史序列
        /// </summary>
        public List<KDResult> GetWeeklyKDSequence(string stockCode, DateTime targetDate, int lookbackPeriods = 10)
        {
            return GetHistoricalKDSequence(stockCode, targetDate, lookbackPeriods, "week");
        }

        /// <summary>
        /// 获取月KD历史序列
        /// </summary>
        public List<KDResult> GetMonthlyKDSequence(string stockCode, DateTime targetDate, int lookbackPeriods = 10)
        {
            return GetHistoricalKDSequence(stockCode, targetDate, lookbackPeriods, "month");
        }

        /// <summary>
        /// 获取季KD历史序列
        /// </summary>
        public List<KDResult> GetQuarterlyKDSequence(string stockCode, DateTime targetDate, int lookbackPeriods = 10)
        {
            return GetHistoricalKDSequence(stockCode, targetDate, lookbackPeriods, "quarter");
        }

        /// <summary>
        /// 计算KD指标（通用方法，带Redis缓存）
        /// </summary>
        private KDResult CalculateKD(string stockCode, DateTime targetDate, int period, string cycleType)
        {
            try
            {
                // 尝试从Redis缓存获取
                string cacheKey = $"kd:{stockCode}:{cycleType}:{targetDate:yyyyMMdd}";
                var cachedResult = RedisHelper.GetCache<KDResult>(cacheKey);
                if (cachedResult != null)
                {
                    return cachedResult; // 缓存命中
                }

                // 缓存未命中，计算KD值
                var aggregatedData = GetAggregatedData(stockCode, targetDate, period, cycleType);

                // 调试：输出第一只股票的KD计算详情（每个周期类型输出一次）
                bool shouldLog = stockCode == "000001" && (
                    (cycleType == "week" && !_debugWeekLogged) ||
                    (cycleType == "month" && !_debugMonthLogged) ||
                    (cycleType == "quarter" && !_debugQuarterLogged)
                );
                if (shouldLog)
                {
                    lock (_debugLock)
                    {
                        Console.WriteLine($"[KD调试] {stockCode} {cycleType}周期 (period={period}):");
                        Console.WriteLine($"  目标日期: {targetDate:yyyy-MM-dd}");
                        Console.WriteLine($"  聚合后数据量: {aggregatedData.Count}");
                        if (aggregatedData.Count > 0)
                        {
                            Console.WriteLine($"  聚合数据首条: {aggregatedData[0].Date:yyyy-MM-dd} (开={aggregatedData[0].Open:F2}, 高={aggregatedData[0].High:F2}, 低={aggregatedData[0].Low:F2}, 收={aggregatedData[0].Close:F2})");
                            Console.WriteLine($"  聚合数据末条: {aggregatedData[aggregatedData.Count-1].Date:yyyy-MM-dd} (开={aggregatedData[aggregatedData.Count-1].Open:F2}, 高={aggregatedData[aggregatedData.Count-1].High:F2}, 低={aggregatedData[aggregatedData.Count-1].Low:F2}, 收={aggregatedData[aggregatedData.Count-1].Close:F2})");
                            
                            // 对于季K，输出所有季度的聚合数据
                            if (cycleType == "quarter" && aggregatedData.Count > 0)
                            {
                                Console.WriteLine($"  所有季度聚合数据（共{aggregatedData.Count}个季度）:");
                                for (int idx = 0; idx < Math.Min(aggregatedData.Count, 15); idx++)
                                {
                                    var q = aggregatedData[idx];
                                    Console.WriteLine($"    季度{idx+1}: {q.Date:yyyy-MM-dd} 开={q.Open:F2} 高={q.High:F2} 低={q.Low:F2} 收={q.Close:F2}");
                                }
                                if (aggregatedData.Count > 15)
                                {
                                    Console.WriteLine($"    ... (还有{aggregatedData.Count - 15}个季度)");
                                }
                            }
                        }
                        if (cycleType == "week") _debugWeekLogged = true;
                        else if (cycleType == "month") _debugMonthLogged = true;
                        else if (cycleType == "quarter") _debugQuarterLogged = true;
                    }
                }

                // 如果数据不足，尝试使用较短周期（最少需要2个周期的数据）
                int actualPeriod = period;
                if (aggregatedData.Count < period)
                {
                    if (aggregatedData.Count >= 2)
                    {
                        actualPeriod = aggregatedData.Count;
                    }
                    else
                    {
                        return null; // 数据确实不足
                    }
                }

                // 计算RSV值（中国股市标准算法）
                // 重要：RSV计算必须使用聚合后的周期K线数据，而不是日线数据
                // 对于周K：使用最近N周的周K线最高/最低/收盘
                // 对于月K：使用最近N月的月K线最高/最低/收盘
                // 对于季K：使用最近N季的季K线最高/最低/收盘
                var rsvList = new List<decimal>();
                for (int i = actualPeriod - 1; i < aggregatedData.Count; i++)
                {
                    // 获取过去N个周期的聚合K线数据（这是关键：必须使用聚合后的周期K线）
                    var periodData = aggregatedData.Skip(i - actualPeriod + 1).Take(actualPeriod).ToList();
                    
                    // 从聚合后的周期K线中找最高价和最低价（不是从日线中找）
                    decimal highest = periodData.Max(data => data.High);  // 过去N个周期的最高价
                    decimal lowest = periodData.Min(data => data.Low);    // 过去N个周期的最低价
                    decimal close = aggregatedData[i].Close;              // 当前周期的收盘价（聚合后的周期K线收盘价）

                    if (highest == lowest)
                    {
                        rsvList.Add(50); // 避免除零
                    }
                    else
                    {
                        // RSV = (收盘价 - 最低价) / (最高价 - 最低价) * 100
                        // 中国股市标准公式
                        decimal rsv = (close - lowest) / (highest - lowest) * 100;
                        rsvList.Add(rsv);
                    }
                    
                    // 调试输出：对于季K，输出最后几个RSV的计算过程
                    if (shouldLog && cycleType == "quarter" && i >= aggregatedData.Count - 3)
                    {
                        Console.WriteLine($"  RSV计算[{i}]: 日期={aggregatedData[i].Date:yyyy-MM-dd}, 收盘={close:F2}, 过去{actualPeriod}个季度最高={highest:F2}, 最低={lowest:F2}, RSV={rsvList[rsvList.Count-1]:F2}");
                    }
                }

                // 调试输出：显示前几个RSV值
                if (shouldLog && rsvList.Count > 0)
                {
                    Console.WriteLine($"  RSV计算完成，共 {rsvList.Count} 个值");
                    int sampleCount = Math.Min(5, rsvList.Count);
                    for (int i = 0; i < sampleCount; i++)
                    {
                        int dataIndex = actualPeriod - 1 + i;
                        var periodData = aggregatedData.Skip(dataIndex - actualPeriod + 1).Take(actualPeriod).ToList();
                        decimal highest = periodData.Max(data => data.High);
                        decimal lowest = periodData.Min(data => data.Low);
                        decimal close = aggregatedData[dataIndex].Close;
                        Console.WriteLine($"    RSV[{i}]: 日期={aggregatedData[dataIndex].Date:yyyy-MM-dd}, " +
                            $"收盘={close:F2}, 最高={highest:F2}, 最低={lowest:F2}, RSV={rsvList[i]:F2}");
                    }
                }

                // 计算K值和D值（使用SMA函数，与标准公式一致）
                // 标准公式：K = SMA(RSV, M1, 1), D = SMA(K, M2, 1)
                // 其中 M1=3, M2=3（默认值）
                // SMA(value, period, 1) = (period-1)/period * 前值 + 1/period * 当前值
                // 当period=3时：SMA(value, 3, 1) = 2/3 * 前值 + 1/3 * 当前值
                const int m1 = 3; // K值平滑周期（M1）
                const int m2 = 3; // D值平滑周期（M2）
                
                decimal k = 50m; // 初始K值（对应位置n-1，与标准算法一致）
                decimal d = 50m; // 初始D值（对应位置n-1，与标准算法一致）

                // 调试输出：显示K和D的计算过程
                if (shouldLog && rsvList.Count > 0)
                {
                    Console.WriteLine($"  K和D值计算过程（前5个周期，使用SMA函数）:");
                    Console.WriteLine($"    初始值: K={k:F2}, D={d:F2} (位置n-1，对应第一个RSV)");
                    Console.WriteLine($"    参数: M1={m1}, M2={m2} (对应SMA周期)");
                    for (int i = 0; i < Math.Min(5, rsvList.Count); i++)
                    {
                        decimal prevK = k;
                        decimal prevD = d;
                        // 使用SMA函数：K = SMA(RSV, M1, 1)
                        k = SMA(rsvList[i], m1, k);
                        // 使用SMA函数：D = SMA(K, M2, 1)
                        d = SMA(k, m2, d);
                        Console.WriteLine($"    周期[{i}]: RSV={rsvList[i]:F2}, " +
                            $"K={prevK:F2} -> {k:F2} (SMA(RSV,{m1},1)), " +
                            $"D={prevD:F2} -> {d:F2} (SMA(K,{m2},1))");
                    }
                    // 继续计算剩余的RSV
                    for (int i = 5; i < rsvList.Count; i++)
                    {
                        k = SMA(rsvList[i], m1, k);
                        d = SMA(k, m2, d);
                    }
                }
                else
                {
                    // 正常计算流程（使用SMA函数）
                    foreach (var rsv in rsvList)
                    {
                        k = SMA(rsv, m1, k);
                        d = SMA(k, m2, d);
                    }
                }

                var result = new KDResult
                {
                    StockCode = stockCode,
                    Date = targetDate,
                    K = Math.Round(k, 2),
                    D = Math.Round(d, 2),
                    RSV = rsvList.Count > 0 ? Math.Round(rsvList[rsvList.Count - 1], 2) : 0
                };

                // 调试输出：显示最终结果
                if (shouldLog)
                {
                    Console.WriteLine($"  最终结果: K={result.K:F2}, D={result.D:F2}, RSV={result.RSV:F2}, K-D={result.K - result.D:F2}");
                }

                // 写入Redis缓存（TTL：1天）
                RedisHelper.SetCache(cacheKey, result, TimeSpan.FromDays(1));

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"计算{cycleType}KD指标失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取聚合后的K线数据
        /// 支持盘中实时数据合并
        /// </summary>
        private List<AggregatedCandle> GetAggregatedData(string stockCode, DateTime targetDate, int period, string cycleType)
        {
            var result = new List<AggregatedCandle>();

            try
            {
                // 获取数据边界状态
                DataStatus dataStatus = null;
                if (_dataBoundaryManager != null)
                {
                    var dateRange = _klineRepository.GetDataDateRange(stockCode);
                    dataStatus = _dataBoundaryManager.GetCurrentDataStatus(dateRange.EndDate);
                }

                // 检查股票的数据范围，如果targetDate超出范围，使用股票的最新可用日期
                var stockDateRange = _klineRepository.GetDataDateRange(stockCode);
                DateTime actualTargetDate = targetDate;

                // 根据数据状态调整目标日期
                if (dataStatus != null && EnableRealTimeDataMerge)
                {
                    // 使用数据边界管理器推荐的目标日期
                    actualTargetDate = dataStatus.RecommendedTargetDate;
                }
                else if (stockDateRange.EndDate.HasValue && targetDate > stockDateRange.EndDate.Value)
                {
                    // 目标日期超出股票数据范围，使用股票的最新数据日期
                    actualTargetDate = stockDateRange.EndDate.Value;
                }

                // 从股票的最早数据开始获取，而不是只获取计算所需的最小数据量
                // 这样可以确保KD计算的准确性和一致性
                DateTime startDate;
                if (stockDateRange.StartDate.HasValue)
                {
                    // 从股票的最早数据开始
                    startDate = stockDateRange.StartDate.Value;
                }
                else
                {
                    // 如果没有找到最早日期，使用原来的逻辑作为后备
                    switch (cycleType.ToLower())
                    {
                        case "week":
                            startDate = actualTargetDate.AddDays(-(period * 7 + 30)); // 周线需要更多数据
                            break;
                        case "month":
                            startDate = actualTargetDate.AddMonths(-(period + 3)); // 月线需要更多数据
                            break;
                        case "quarter":
                            startDate = actualTargetDate.AddMonths(-((period + 2) * 3)); // 季线需要更多数据
                            break;
                        default:
                            startDate = actualTargetDate.AddDays(-(period * 2));
                            break;
                    }
                }

                // 通过仓储接口获取数据库历史数据（从最早数据开始）
                var dailyData = _klineRepository.GetDailyData(stockCode, startDate, actualTargetDate);

                // 盘中实时数据合并
                if (EnableRealTimeDataMerge && _dataBoundaryManager != null && _realTimeCache != null &&
                    dataStatus != null && dataStatus.Strategy == DataSourceStrategy.DatabasePlusRealTime)
                {
                    // 获取实时数据
                    var realTimeData = _realTimeCache.GetData(stockCode);
                    if (realTimeData != null)
                    {
                        // 合并实时数据到历史数据
                        dailyData = _dataBoundaryManager.MergeHistoryAndRealTime(
                            dailyData.ToList(),
                            realTimeData,
                            DateTime.Now.Date);
                    }
                }

                // 转换为内部格式（根据配置决定是否使用前复权价格）
                if (EnableForwardAdjustment)
                {
                    // 性能优化：批量计算OHLC前复权值（一次性处理，避免重复计算）
                    var ohlcData = dailyData.ToDictionary(
                        d => d.TradeDate,
                        d => (d.Open, d.High, d.Low, d.Close)
                    );

                    var adjOhlcData = _exRightsCalculator.BatchCalculateOHLCAdjustedPrices(stockCode, ohlcData);

                    // 应用复权价格到所有数据
                    foreach (var data in dailyData)
                    {
                        if (adjOhlcData.TryGetValue(data.TradeDate, out var adj))
                        {
                            result.Add(new AggregatedCandle
                            {
                                Date = data.TradeDate,
                                Open = adj.Open,      // 使用前复权开盘价
                                High = adj.High,      // 使用前复权最高价
                                Low = adj.Low,        // 使用前复权最低价
                                Close = adj.Close,    // 使用前复权收盘价
                                Volume = data.Volume
                            });
                        }
                        else
                        {
                            // 如果没有复权数据，使用原始价格
                            result.Add(new AggregatedCandle
                            {
                                Date = data.TradeDate,
                                Open = data.Open,
                                High = data.High,
                                Low = data.Low,
                                Close = data.Close,
                                Volume = data.Volume
                            });
                        }
                    }
                }
                else
                {
                    // 快速模式：直接使用原始价格（不计算复权）
                    foreach (var data in dailyData)
                    {
                        result.Add(new AggregatedCandle
                        {
                            Date = data.TradeDate,
                            Open = data.Open,      // 使用原始开盘价
                            High = data.High,      // 使用原始最高价
                            Low = data.Low,        // 使用原始最低价
                            Close = data.Close,    // 使用原始收盘价
                            Volume = data.Volume
                        });
                    }
                }

                // 按周期聚合数据
                var aggregated = AggregateByCycle(result, cycleType);

                // 「数据不足」诊断：抽样输出，区分「真实数据少」与「日线多但聚合少（程序/周期问题）」
                if (aggregated.Count < period)
                {
                    bool diagnose = AppConfigProvider.Instance.GetBool("FilterDiagnose_DataInsufficient", false);
                    if (diagnose)
                    {
                        lock (_debugLock)
                        {
                            if (_dataInsufficientDiagnoseCount < MaxDataInsufficientDiagnoseLogs)
                            {
                                _dataInsufficientDiagnoseCount++;
                                string rangeStr = stockDateRange.StartDate.HasValue && stockDateRange.EndDate.HasValue
                                    ? $"{stockDateRange.StartDate.Value:yyyy-MM-dd} ~ {stockDateRange.EndDate.Value:yyyy-MM-dd}"
                                    : "无";
                                string conclusion = result.Count >= 100 && aggregated.Count < period
                                    ? "→ 日线充足但聚合少，疑似周期/程序逻辑问题"
                                    : "→ 日线少，数据确实不足（新股或历史短）";
                                Console.WriteLine($"[数据不足诊断] {stockCode} {cycleType} 日线数={result.Count} 聚合周期数={aggregated.Count} 范围={rangeStr} {conclusion}");
                            }
                        }
                    }
                }

                return aggregated;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取聚合数据失败: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// 按周期聚合K线数据
        /// </summary>
        private List<AggregatedCandle> AggregateByCycle(List<AggregatedCandle> dailyData, string cycleType)
        {
            if (dailyData.Count == 0)
                return new List<AggregatedCandle>();

            var aggregated = new List<AggregatedCandle>();
            var grouped = new List<List<AggregatedCandle>>();

            switch (cycleType.ToLower())
            {
                case "week":
                    // 按周分组（周一到周日为一周，与ChartService保持一致）
                    // 使用与ChartService相同的分组逻辑：按周一的日期分组
                    var weekGroups = dailyData
                        .GroupBy(d => {
                            // 获取该周的周一日期作为键（与ChartService.GetCycleKey逻辑一致）
                            int daysFromMonday = ((int)d.Date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
                            DateTime monday = d.Date.AddDays(-daysFromMonday);
                            return monday; // 使用DateTime作为分组键，确保与ChartService一致
                        })
                        .OrderBy(g => g.Key);
                    foreach (var group in weekGroups)
                    {
                        grouped.Add(group.OrderBy(x => x.Date).ToList());
                    }
                    break;

                case "month":
                    // 按月分组
                    var monthGroups = dailyData
                        .GroupBy(d => new { d.Date.Year, d.Date.Month })
                        .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month);
                    foreach (var group in monthGroups)
                    {
                        grouped.Add(group.OrderBy(x => x.Date).ToList());
                    }
                    break;

                case "quarter":
                    // 按季度分组（中国股市标准：Q1=1-3月，Q2=4-6月，Q3=7-9月，Q4=10-12月）
                    // 确保按照自然季度分组，而不是按交易日计算
                    var quarterGroups = dailyData
                        .GroupBy(d => new { 
                            Year = d.Date.Year, 
                            Quarter = (d.Date.Month - 1) / 3 + 1  // 1-3月=Q1, 4-6月=Q2, 7-9月=Q3, 10-12月=Q4
                        })
                        .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Quarter);
                    foreach (var group in quarterGroups)
                    {
                        // 确保每个季度内的数据按日期排序
                        grouped.Add(group.OrderBy(x => x.Date).ToList());
                    }
                    break;

                default:
                    grouped.Add(dailyData);
                    break;
            }

            // 聚合每组数据
            foreach (var group in grouped)
            {
                if (group.Count > 0)
                {
                    aggregated.Add(new AggregatedCandle
                    {
                        Date = group.Last().Date, // 使用最后一天的日期
                        Open = group.First().Open, // 开盘价取第一天的
                        High = group.Max(d => d.High), // 最高价取最大值
                        Low = group.Min(d => d.Low), // 最低价取最小值
                        Close = group.Last().Close, // 收盘价取最后一天的
                        Volume = group.Sum(d => d.Volume) // 成交量求和
                    });
                }
            }

            return aggregated;
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
        /// 获取周的关键字（用于分组）
        /// </summary>
        private string GetWeekKey(DateTime date)
        {
            // 获取该周的第一天（周一）
            int daysFromMonday = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            DateTime monday = date.AddDays(-daysFromMonday);
            return $"{monday:yyyy-MM-dd}";
        }
    }
}
