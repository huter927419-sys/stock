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

            return new List<SimpleFilterResult>(results);
        }

        /// <summary>
        /// 处理单只股票
        /// </summary>
        protected virtual SimpleFilterResult ProcessStock(RealTimeDataRecord realTimeData, TCondition condition, DateTime targetDate)
        {
            // 检查实时数据完整性
            var integrityCheck = _validator.CheckRealTimeDataIntegrity(realTimeData);
            if (!integrityCheck.IsValid)
                return null;

            // 计算KD指标
            var weeklyKD = _kdCalculator.CalculateWeeklyKD(realTimeData.StockCode, targetDate);
            var monthlyKD = _kdCalculator.CalculateMonthlyKD(realTimeData.StockCode, targetDate);
            var quarterlyKD = _kdCalculator.CalculateQuarterlyKD(realTimeData.StockCode, targetDate);

            // 检查KD数据完整性
            var kdCheck = _validator.CheckKDDataIntegrity(realTimeData.StockCode, weeklyKD, monthlyKD, quarterlyKD);
            if (!kdCheck.IsValid)
                return null;

            // 检查通用条件（默认最小值、价格、成交量等）
            if (!CheckBaseConditions(condition, realTimeData, weeklyKD, monthlyKD, quarterlyKD))
                return null;

            // 检查特定过滤条件（由子类实现）
            if (!CheckSpecificConditions(condition, realTimeData, targetDate, weeklyKD, monthlyKD, quarterlyKD))
                return null;

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
            // 检查默认最小值
            if (weeklyKD.K <= condition.WeeklyKDefaultMin)
                return false;
            if (monthlyKD.K <= condition.MonthlyKDefaultMin)
                return false;
            if (quarterlyKD.K <= condition.QuarterlyKDefaultMin)
                return false;

            // 检查实时数据条件
            if (condition.PriceMin.HasValue && realTimeData.NewPrice < condition.PriceMin.Value)
                return false;
            if (condition.VolumeMin.HasValue && realTimeData.Volume < condition.VolumeMin.Value)
                return false;

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
        /// 获取最优并行度
        /// </summary>
        protected int GetOptimalParallelism()
        {
            return DatabaseConnectionHelper.GetOptimalParallelism();
        }
    }
}
