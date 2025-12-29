using System;
using MQReceiver.Cache;
using MQReceiver.Calculators;
using MQReceiver.Models;

namespace MQReceiver.Filters
{
    /// <summary>
    /// 表格六过滤器
    /// 条件：月K < 季K AND 周K < 季K
    /// </summary>
    public class Table6Filter : BaseStockFilter<Table6Condition>
    {
        public Table6Filter(KDCalculator kdCalculator, RealTimeDataCache realTimeCache)
            : base(kdCalculator, realTimeCache)
        {
        }

        /// <summary>
        /// 检查特定条件：月K < 季K AND 周K < 季K
        /// </summary>
        protected override bool CheckSpecificConditions(
            Table6Condition condition,
            RealTimeDataRecord realTimeData,
            DateTime targetDate,
            KDResult weeklyKD,
            KDResult monthlyKD,
            KDResult quarterlyKD)
        {
            // 条件1：月K < 季K
            if (monthlyKD.K >= quarterlyKD.K)
                return false;

            // 条件2：周K < 季K
            if (weeklyKD.K >= quarterlyKD.K)
                return false;

            return true;
        }
    }
}
