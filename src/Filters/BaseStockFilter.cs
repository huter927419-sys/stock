using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MQReceiver.Cache;
using MQReceiver.Calculators;
using MQReceiver.Helpers;
using MQReceiver.Models;

namespace MQReceiver.Filters
{
    /// <summary>
    /// 股票过滤器基类
    /// 提供通用的过滤逻辑和并行处理能力
    /// </summary>
    public abstract class BaseStockFilter<TCondition> : IStockFilter<TCondition>
        where TCondition : FilterConditionBase
    {
        protected readonly KDCalculator _kdCalculator;
        protected readonly RealTimeDataCache _realTimeCache;
        protected readonly DataValidator _validator;

        protected BaseStockFilter(KDCalculator kdCalculator, RealTimeDataCache realTimeCache)
        {
            _kdCalculator = kdCalculator ?? throw new ArgumentNullException(nameof(kdCalculator));
            _realTimeCache = realTimeCache ?? throw new ArgumentNullException(nameof(realTimeCache));
            _validator = new DataValidator();
        }

        /// <summary>
        /// 获取前一个交易日
        /// 跳过周末（周六、周日），返回实际的交易日
        /// </summary>
        protected DateTime GetPreviousTradingDay(DateTime date)
        {
            DateTime previousDay = date.AddDays(-1);

            // 跳过周末
            while (previousDay.DayOfWeek == DayOfWeek.Saturday || previousDay.DayOfWeek == DayOfWeek.Sunday)
            {
                previousDay = previousDay.AddDays(-1);
            }

            return previousDay;
        }

        /// <summary>
        /// 执行过滤（串行版本）
        /// </summary>
        public List<SimpleFilterResult> Filter(TCondition condition, DateTime targetDate)
        {
            var results = new List<SimpleFilterResult>();
            var realTimeDataList = _realTimeCache.GetAllData();

            if (realTimeDataList == null || realTimeDataList.Count == 0)
                return results;

            foreach (var realTimeData in realTimeDataList)
            {
                var result = ProcessStock(realTimeData, condition, targetDate);
                if (result != null)
                {
                    results.Add(result);
                }
            }

            return results;
        }

        /// <summary>
        /// 执行过滤（并行版本）
        /// </summary>
        public List<SimpleFilterResult> FilterParallel(TCondition condition, DateTime targetDate)
        {
            var results = new ConcurrentBag<SimpleFilterResult>();
            var realTimeDataList = _realTimeCache.GetAllData();

            if (realTimeDataList == null || realTimeDataList.Count == 0)
                return new List<SimpleFilterResult>();

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = GetOptimalParallelism()
            };

            int processedCount = 0;
            int skippedCount = 0;
            int errorCount = 0;

            Parallel.ForEach(realTimeDataList, parallelOptions, realTimeData =>
            {
                try
                {
                    var result = ProcessStock(realTimeData, condition, targetDate);
                    if (result != null)
                    {
                        results.Add(result);
                        Interlocked.Increment(ref processedCount);
                    }
                    else
                    {
                        Interlocked.Increment(ref skippedCount);
                    }
                }
                catch (Exception)
                {
                    Interlocked.Increment(ref errorCount);
                }
            });

            if (processedCount > 0 || skippedCount > 0 || errorCount > 0)
            {
                Console.WriteLine($"并行处理统计: 通过={processedCount}, 跳过={skippedCount}, 错误={errorCount}, 并行度={GetOptimalParallelism()}");
            }

            // 重置调试标志
            ResetSkipLog();

            return new List<SimpleFilterResult>(results);
        }

        // 调试开关：设为 true 时输出详细的跳过原因（仅用于开发调试）
        private static readonly bool _enableDebugLog = false;
        private static int _skipLogCount = 0;
        private const int MAX_SKIP_LOG = 5;
        private static readonly object _logLock = new object();

        private void LogFirstSkip(string message)
        {
            if (!_enableDebugLog) return;

            lock (_logLock)
            {
                if (_skipLogCount < MAX_SKIP_LOG)
                {
                    Console.WriteLine($"[过滤调试] #{_skipLogCount + 1}: {message}");
                    _skipLogCount++;
                }
            }
        }

        private static void ResetSkipLog()
        {
            if (!_enableDebugLog) return;

            lock (_logLock)
            {
                _skipLogCount = 0;
            }
        }

        /// <summary>
        /// 处理单只股票
        /// </summary>
        protected virtual SimpleFilterResult ProcessStock(RealTimeDataRecord realTimeData, TCondition condition, DateTime targetDate)
        {
            // 市场过滤：只保留深圳、上海、北京市场的股票
            if (!IsMainMarketStock(realTimeData.StockCode))
            {
                return null;  // 静默过滤，不记录日志
            }

            // 检查实时数据完整性
            var integrityCheck = _validator.CheckRealTimeDataIntegrity(realTimeData);
            if (!integrityCheck.IsValid)
            {
                LogFirstSkip($"实时数据验证失败 [{realTimeData.StockCode}]: {string.Join(", ", integrityCheck.Issues)}");
                return null;
            }

            // 计算KD指标
            var weeklyKD = _kdCalculator.CalculateWeeklyKD(realTimeData.StockCode, targetDate);
            var monthlyKD = _kdCalculator.CalculateMonthlyKD(realTimeData.StockCode, targetDate);
            var quarterlyKD = _kdCalculator.CalculateQuarterlyKD(realTimeData.StockCode, targetDate);

            // 检查KD数据完整性，输出更详细的日志
            var kdCheck = _validator.CheckKDDataIntegrity(realTimeData.StockCode, weeklyKD, monthlyKD, quarterlyKD);
            if (!kdCheck.IsValid)
            {
                string kdDetails = $"周KD={(weeklyKD != null ? $"K={weeklyKD.K:F2},D={weeklyKD.D:F2}" : "null")}, " +
                                   $"月KD={(monthlyKD != null ? $"K={monthlyKD.K:F2},D={monthlyKD.D:F2}" : "null")}, " +
                                   $"季KD={(quarterlyKD != null ? $"K={quarterlyKD.K:F2},D={quarterlyKD.D:F2}" : "null")}";
                LogFirstSkip($"KD验证失败 [{realTimeData.StockCode}]: {string.Join(", ", kdCheck.Issues)}. {kdDetails}");
                return null;
            }

            // 检查通用条件（默认最小值、价格、成交量等）
            if (!CheckBaseConditions(condition, realTimeData, weeklyKD, monthlyKD, quarterlyKD))
            {
                LogFirstSkip($"基础条件检查失败 [{realTimeData.StockCode}]: 价格={realTimeData.NewPrice}, 成交量={realTimeData.Volume}");
                return null;
            }

            // 检查特定过滤条件（由子类实现）
            if (!CheckSpecificConditions(condition, realTimeData, targetDate, weeklyKD, monthlyKD, quarterlyKD))
            {
                LogFirstSkip($"特定条件检查失败 [{realTimeData.StockCode}]: 周K={weeklyKD.K:F2}(D={weeklyKD.D:F2}), 月K={monthlyKD.K:F2}(D={monthlyKD.D:F2}), 季K={quarterlyKD.K:F2}(D={quarterlyKD.D:F2})");
                return null;
            }

            return new SimpleFilterResult
            {
                StockCode = realTimeData.StockCode,
                StockName = realTimeData.StockName ?? realTimeData.StockCode,
                WeeklyK = Math.Round(weeklyKD.K, 2),
                MonthlyK = Math.Round(monthlyKD.K, 2),
                QuarterlyK = Math.Round(quarterlyKD.K, 2)
            };
        }

        /// <summary>
        /// 检查通用基础条件
        /// </summary>
        protected virtual bool CheckBaseConditions(
            TCondition condition,
            RealTimeDataRecord realTimeData,
            KDResult weeklyKD,
            KDResult monthlyKD,
            KDResult quarterlyKD)
        {
            // 注意：默认最小值过滤移至UI层（FilterMainViewModel.UpdateResults）
            // 这样用户可以在UI上调整阈值并实时看到结果变化，无需重新计算KD

            // 检查实时数据条件
            // 注意：当没有实时数据（价格为0）时，跳过价格和成交量检查
            // 这种情况发生在只有日线历史数据，没有实时数据时
            bool hasRealTimeData = realTimeData.NewPrice > 0;
            if (hasRealTimeData)
            {
                if (condition.PriceMin.HasValue && realTimeData.NewPrice < condition.PriceMin.Value)
                    return false;
                if (condition.VolumeMin.HasValue && realTimeData.Volume < condition.VolumeMin.Value)
                    return false;
            }

            // 检查K值范围
            if (condition.WeeklyKMin.HasValue && weeklyKD.K < condition.WeeklyKMin.Value)
                return false;
            if (condition.WeeklyKMax.HasValue && weeklyKD.K > condition.WeeklyKMax.Value)
                return false;
            if (condition.MonthlyKMin.HasValue && monthlyKD.K < condition.MonthlyKMin.Value)
                return false;
            if (condition.MonthlyKMax.HasValue && monthlyKD.K > condition.MonthlyKMax.Value)
                return false;
            if (condition.QuarterlyKMin.HasValue && quarterlyKD.K < condition.QuarterlyKMin.Value)
                return false;
            if (condition.QuarterlyKMax.HasValue && quarterlyKD.K > condition.QuarterlyKMax.Value)
                return false;

            return true;
        }

        /// <summary>
        /// 检查特定过滤条件（由子类实现）
        /// </summary>
        protected abstract bool CheckSpecificConditions(
            TCondition condition,
            RealTimeDataRecord realTimeData,
            DateTime targetDate,
            KDResult weeklyKD,
            KDResult monthlyKD,
            KDResult quarterlyKD);

        /// <summary>
        /// 判断是否为主板市场股票（深圳、上海、北京）
        /// </summary>
        /// <param name="stockCode">股票代码</param>
        /// <returns>是否为主板市场股票</returns>
        protected bool IsMainMarketStock(string stockCode)
        {
            if (string.IsNullOrEmpty(stockCode) || stockCode.Length < 1)
                return false;

            char firstChar = stockCode[0];

            // 上海市场：6开头
            // - 600xxx, 601xxx, 603xxx 主板
            // - 688xxx 科创板
            if (firstChar == '6')
                return true;

            // 深圳市场：0开头或3开头
            // - 000xxx, 001xxx 主板
            // - 002xxx 中小板
            // - 300xxx, 301xxx 创业板
            if (firstChar == '0' || firstChar == '3')
                return true;

            // 北京市场（北交所）：4开头或8开头
            // - 430xxx, 830xxx, 831xxx 等
            if (firstChar == '4' || firstChar == '8')
                return true;

            // 其他：指数、基金、债券等，不包含
            return false;
        }

        /// <summary>
        /// 获取最优并行度
        /// </summary>
        protected int GetOptimalParallelism()
        {
            return DatabaseConnectionHelper.GetOptimalParallelism();
        }
    }
}
