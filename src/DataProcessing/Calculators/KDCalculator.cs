using System;
using System.Collections.Generic;
using System.Linq;
using MQReceiver.Helpers;
using MQReceiver.Models;
using MQReceiver.Repositories;

namespace MQReceiver.Calculators
{
    /// <summary>
    /// KD指标计算器
    /// 支持周、月、季周期的KD指标计算
    ///
    /// KD指标计算公式：
    /// 1. RSV = (收盘价 - 最低价) / (最高价 - 最低价) * 100
    /// 2. K值 = (2/3) * 前一日K值 + (1/3) * 当日RSV
    /// 3. D值 = (2/3) * 前一日D值 + (1/3) * 当日K值
    /// </summary>
    public class KDCalculator
    {
        private readonly string connectionString;
        private readonly IKlineDataRepository _klineRepository;
        private const int DEFAULT_PERIOD = 9; // 默认周期为9

        // 调试用变量 - 按周期类型分别记录
        private static bool _debugWeekLogged = false;
        private static bool _debugMonthLogged = false;
        private static bool _debugQuarterLogged = false;
        private static readonly object _debugLock = new object();

        /// <summary>
        /// 使用默认仓储初始化（向后兼容）
        /// </summary>
        public KDCalculator()
        {
            // 使用统一的连接字符串生成器
            connectionString = DatabaseConnectionHelper.BuildConnectionString();
            _klineRepository = new PostgresKlineDataRepository(connectionString);
        }

        /// <summary>
        /// 使用指定仓储初始化（依赖注入）
        /// </summary>
        public KDCalculator(IKlineDataRepository klineRepository)
        {
            _klineRepository = klineRepository ?? throw new ArgumentNullException(nameof(klineRepository));
            connectionString = DatabaseConnectionHelper.BuildConnectionString();
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

                if (aggregatedData.Count < period)
                {
                    return result; // 数据不足
                }

                // 计算每个周期的KD值
                var rsvList = new List<decimal>();
                for (int i = period - 1; i < aggregatedData.Count; i++)
                {
                    var periodData = aggregatedData.Skip(i - period + 1).Take(period).ToList();
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
                    k = (2m / 3m) * k + (1m / 3m) * rsvList[i];
                    d = (2m / 3m) * d + (1m / 3m) * k;
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
                        Date = aggregatedData[period - 1 + i].Date,
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
                            Console.WriteLine($"  聚合数据首条: {aggregatedData[0].Date:yyyy-MM-dd}");
                            Console.WriteLine($"  聚合数据末条: {aggregatedData[aggregatedData.Count-1].Date:yyyy-MM-dd}");
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

                // 计算RSV值
                var rsvList = new List<decimal>();
                for (int i = actualPeriod - 1; i < aggregatedData.Count; i++)
                {
                    var periodData = aggregatedData.Skip(i - actualPeriod + 1).Take(actualPeriod).ToList();
                    decimal highest = periodData.Max(data => data.High);
                    decimal lowest = periodData.Min(data => data.Low);
                    decimal close = aggregatedData[i].Close;

                    if (highest == lowest)
                    {
                        rsvList.Add(50); // 避免除零
                    }
                    else
                    {
                        decimal rsv = (close - lowest) / (highest - lowest) * 100;
                        rsvList.Add(rsv);
                    }
                }

                // 计算K值和D值
                decimal k = 50; // 初始K值
                decimal d = 50; // 初始D值

                foreach (var rsv in rsvList)
                {
                    k = (2m / 3m) * k + (1m / 3m) * rsv;
                    d = (2m / 3m) * d + (1m / 3m) * k;
                }

                var result = new KDResult
                {
                    StockCode = stockCode,
                    Date = targetDate,
                    K = Math.Round(k, 2),
                    D = Math.Round(d, 2),
                    RSV = rsvList.Count > 0 ? Math.Round(rsvList[rsvList.Count - 1], 2) : 0
                };

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
        /// </summary>
        private List<AggregatedCandle> GetAggregatedData(string stockCode, DateTime targetDate, int period, string cycleType)
        {
            var result = new List<AggregatedCandle>();

            try
            {
                // 检查股票的数据范围，如果targetDate超出范围，使用股票的最新可用日期
                var dateRange = _klineRepository.GetDataDateRange(stockCode);
                DateTime actualTargetDate = targetDate;

                if (dateRange.EndDate.HasValue && targetDate > dateRange.EndDate.Value)
                {
                    // 目标日期超出股票数据范围，使用股票的最新数据日期
                    actualTargetDate = dateRange.EndDate.Value;
                }

                // 根据周期类型确定需要获取的数据范围
                DateTime startDate;
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

                // 通过仓储接口获取数据
                var dailyData = _klineRepository.GetDailyData(stockCode, startDate, actualTargetDate);

                // 转换为内部格式
                foreach (var data in dailyData)
                {
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

                // 按周期聚合数据
                return AggregateByCycle(result, cycleType);
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
                    // 按周分组（周一到周日为一周）
                    var weekGroups = dailyData
                        .GroupBy(d => GetWeekKey(d.Date))
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
                    // 按季度分组
                    var quarterGroups = dailyData
                        .GroupBy(d => new { d.Date.Year, Quarter = (d.Date.Month - 1) / 3 + 1 })
                        .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Quarter);
                    foreach (var group in quarterGroups)
                    {
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
