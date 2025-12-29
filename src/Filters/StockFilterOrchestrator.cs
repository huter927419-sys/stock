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
        private readonly Table1Filter _table1Filter;
        private readonly Table2Filter _table2Filter;
        private readonly Table3Filter _table3Filter;
        private readonly Table4Filter _table4Filter;
        private readonly Table5Filter _table5Filter;
        private readonly Table6Filter _table6Filter;

        public StockFilterOrchestrator(KDCalculator kdCalculator, RealTimeDataCache realTimeCache)
        {
            _table1Filter = new Table1Filter(kdCalculator, realTimeCache);
            _table2Filter = new Table2Filter(kdCalculator, realTimeCache);
            _table3Filter = new Table3Filter(kdCalculator, realTimeCache);
            _table4Filter = new Table4Filter(kdCalculator, realTimeCache);
            _table5Filter = new Table5Filter(kdCalculator, realTimeCache);
            _table6Filter = new Table6Filter(kdCalculator, realTimeCache);
        }

        /// <summary>
        /// 执行表格一过滤（月K > 季K AND 周K上穿月K）
        /// </summary>
        public List<SimpleFilterResult> FilterByTable1(DateTime targetDate)
        {
            var condition = FilterConditionBuilder.BuildTable1Condition();
            return _table1Filter.Filter(condition, targetDate);
        }

        /// <summary>
        /// 执行表格一过滤（并行版本）
        /// </summary>
        public List<SimpleFilterResult> FilterByTable1Parallel(DateTime targetDate)
        {
            var condition = FilterConditionBuilder.BuildTable1Condition();
            return _table1Filter.FilterParallel(condition, targetDate);
        }

        /// <summary>
        /// 执行表格一过滤（使用自定义条件）
        /// </summary>
        public List<SimpleFilterResult> FilterByTable1Parallel(Table1Condition condition, DateTime targetDate)
        {
            return _table1Filter.FilterParallel(condition, targetDate);
        }

        /// <summary>
        /// 执行表格二过滤（月K > 季K AND 周K > 月K，不含上穿当天）
        /// </summary>
        public List<SimpleFilterResult> FilterByTable2(DateTime targetDate)
        {
            var condition = FilterConditionBuilder.BuildTable2Condition();
            return _table2Filter.Filter(condition, targetDate);
        }

        /// <summary>
        /// 执行表格二过滤（并行版本）
        /// </summary>
        public List<SimpleFilterResult> FilterByTable2Parallel(DateTime targetDate)
        {
            var condition = FilterConditionBuilder.BuildTable2Condition();
            return _table2Filter.FilterParallel(condition, targetDate);
        }

        /// <summary>
        /// 执行表格二过滤（使用自定义条件）
        /// </summary>
        public List<SimpleFilterResult> FilterByTable2Parallel(Table2Condition condition, DateTime targetDate)
        {
            return _table2Filter.FilterParallel(condition, targetDate);
        }

        /// <summary>
        /// 执行表格三过滤（月K 小于 季K AND 周K上穿季K）
        /// </summary>
        public List<SimpleFilterResult> FilterByTable3(DateTime targetDate)
        {
            var condition = FilterConditionBuilder.BuildTable3Condition();
            return _table3Filter.Filter(condition, targetDate);
        }

        /// <summary>
        /// 执行表格三过滤（并行版本）
        /// </summary>
        public List<SimpleFilterResult> FilterByTable3Parallel(DateTime targetDate)
        {
            var condition = FilterConditionBuilder.BuildTable3Condition();
            return _table3Filter.FilterParallel(condition, targetDate);
        }

        /// <summary>
        /// 执行表格三过滤（使用自定义条件）
        /// </summary>
        public List<SimpleFilterResult> FilterByTable3Parallel(Table3Condition condition, DateTime targetDate)
        {
            return _table3Filter.FilterParallel(condition, targetDate);
        }

        /// <summary>
        /// 执行表格四过滤（月K 小于 季K AND 周K > 季K，不含上穿当天）
        /// </summary>
        public List<SimpleFilterResult> FilterByTable4(DateTime targetDate)
        {
            var condition = FilterConditionBuilder.BuildTable4Condition();
            return _table4Filter.Filter(condition, targetDate);
        }

        /// <summary>
        /// 执行表格四过滤（并行版本）
        /// </summary>
        public List<SimpleFilterResult> FilterByTable4Parallel(DateTime targetDate)
        {
            var condition = FilterConditionBuilder.BuildTable4Condition();
            return _table4Filter.FilterParallel(condition, targetDate);
        }

        /// <summary>
        /// 执行表格四过滤（使用自定义条件）
        /// </summary>
        public List<SimpleFilterResult> FilterByTable4Parallel(Table4Condition condition, DateTime targetDate)
        {
            return _table4Filter.FilterParallel(condition, targetDate);
        }

        /// <summary>
        /// 执行表格五过滤（月K > 季K AND 周K 小于 月K）
        /// </summary>
        public List<SimpleFilterResult> FilterByTable5(DateTime targetDate)
        {
            var condition = FilterConditionBuilder.BuildTable5Condition();
            return _table5Filter.Filter(condition, targetDate);
        }

        /// <summary>
        /// 执行表格五过滤（并行版本）
        /// </summary>
        public List<SimpleFilterResult> FilterByTable5Parallel(DateTime targetDate)
        {
            var condition = FilterConditionBuilder.BuildTable5Condition();
            return _table5Filter.FilterParallel(condition, targetDate);
        }

        /// <summary>
        /// 执行表格五过滤（使用自定义条件）
        /// </summary>
        public List<SimpleFilterResult> FilterByTable5Parallel(Table5Condition condition, DateTime targetDate)
        {
            return _table5Filter.FilterParallel(condition, targetDate);
        }

        /// <summary>
        /// 执行表格六过滤（月K 小于 季K AND 周K 小于 季K）
        /// </summary>
        public List<SimpleFilterResult> FilterByTable6(DateTime targetDate)
        {
            var condition = FilterConditionBuilder.BuildTable6Condition();
            return _table6Filter.Filter(condition, targetDate);
        }

        /// <summary>
        /// 执行表格六过滤（并行版本）
        /// </summary>
        public List<SimpleFilterResult> FilterByTable6Parallel(DateTime targetDate)
        {
            var condition = FilterConditionBuilder.BuildTable6Condition();
            return _table6Filter.FilterParallel(condition, targetDate);
        }

        /// <summary>
        /// 执行表格六过滤（使用自定义条件）
        /// </summary>
        public List<SimpleFilterResult> FilterByTable6Parallel(Table6Condition condition, DateTime targetDate)
        {
            return _table6Filter.FilterParallel(condition, targetDate);
        }
    }
}
