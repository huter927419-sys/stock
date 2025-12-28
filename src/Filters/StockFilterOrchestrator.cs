using System;
using System.Collections.Generic;
using MQReceiver.Cache;
using MQReceiver.Calculators;

namespace MQReceiver.Filters
{
    /// <summary>
    /// 股票过滤器协调器
    /// 统一管理和调用所有过滤器
    /// </summary>
    public class StockFilterOrchestrator
    {
        private readonly GoldenCrossFilter _goldenCrossFilter;
        private readonly KValueOrderFilter _kValueOrderFilter;
        private readonly KValueRelationFilter _kValueRelationFilter;

        public StockFilterOrchestrator(KDCalculator kdCalculator, RealTimeDataCache realTimeCache)
        {
            _goldenCrossFilter = new GoldenCrossFilter(kdCalculator, realTimeCache);
            _kValueOrderFilter = new KValueOrderFilter(kdCalculator, realTimeCache);
            _kValueRelationFilter = new KValueRelationFilter(kdCalculator, realTimeCache);
        }

        /// <summary>
        /// 执行金叉过滤（条件1）
        /// </summary>
        public List<SimpleFilterResult> FilterByGoldenCross(DateTime targetDate)
        {
            var condition = FilterConditionBuilder.BuildGoldenCrossCondition();
            return _goldenCrossFilter.Filter(condition, targetDate);
        }

        /// <summary>
        /// 执行金叉过滤（条件1，并行版本）
        /// </summary>
        public List<SimpleFilterResult> FilterByGoldenCrossParallel(DateTime targetDate)
        {
            var condition = FilterConditionBuilder.BuildGoldenCrossCondition();
            return _goldenCrossFilter.FilterParallel(condition, targetDate);
        }

        /// <summary>
        /// 执行金叉过滤（条件1，使用自定义条件）
        /// </summary>
        public List<SimpleFilterResult> FilterByGoldenCrossParallel(GoldenCrossCondition condition, DateTime targetDate)
        {
            return _goldenCrossFilter.FilterParallel(condition, targetDate);
        }

        /// <summary>
        /// 执行K值顺序过滤（条件2）
        /// </summary>
        public List<SimpleFilterResult> FilterByKValueOrder(DateTime targetDate)
        {
            var condition = FilterConditionBuilder.BuildKValueOrderCondition();
            return _kValueOrderFilter.Filter(condition, targetDate);
        }

        /// <summary>
        /// 执行K值顺序过滤（条件2，并行版本）
        /// </summary>
        public List<SimpleFilterResult> FilterByKValueOrderParallel(DateTime targetDate)
        {
            var condition = FilterConditionBuilder.BuildKValueOrderCondition();
            return _kValueOrderFilter.FilterParallel(condition, targetDate);
        }

        /// <summary>
        /// 执行K值顺序过滤（条件2，使用自定义条件）
        /// </summary>
        public List<SimpleFilterResult> FilterByKValueOrderParallel(KValueOrderCondition condition, DateTime targetDate)
        {
            return _kValueOrderFilter.FilterParallel(condition, targetDate);
        }

        /// <summary>
        /// 执行K值关系过滤（条件3）
        /// </summary>
        public List<SimpleFilterResult> FilterByKValueRelation(DateTime targetDate)
        {
            var condition = FilterConditionBuilder.BuildKValueRelationCondition();
            return _kValueRelationFilter.Filter(condition, targetDate);
        }

        /// <summary>
        /// 执行K值关系过滤（条件3，并行版本）
        /// </summary>
        public List<SimpleFilterResult> FilterByKValueRelationParallel(DateTime targetDate)
        {
            var condition = FilterConditionBuilder.BuildKValueRelationCondition();
            return _kValueRelationFilter.FilterParallel(condition, targetDate);
        }

        /// <summary>
        /// 执行K值关系过滤（条件3，使用自定义条件）
        /// </summary>
        public List<SimpleFilterResult> FilterByKValueRelationParallel(KValueRelationCondition condition, DateTime targetDate)
        {
            return _kValueRelationFilter.FilterParallel(condition, targetDate);
        }
    }
}
