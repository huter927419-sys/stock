using System;
using MQReceiver.Cache;
using MQReceiver.Calculators;
using MQReceiver.Models;

namespace MQReceiver.Filters
{
    /// <summary>
    /// 表格五过滤器
    /// 条件：月K > 季K AND 周K < 月K
    /// </summary>
    public class Table5Filter : BaseStockFilter<Table5Condition>
    {
        public Table5Filter(KDCalculator kdCalculator, RealTimeDataCache realTimeCache)
            : base(kdCalculator, realTimeCache)
        {
        }

        /// <summary>
        /// 检查特定条件：月K > 季K AND 周K < 月K
        /// </summary>
        protected override bool CheckSpecificConditions(
            Table5Condition condition,
            RealTimeDataRecord realTimeData,
            DateTime targetDate,
            KDResult weeklyKD,
            KDResult monthlyKD,
            KDResult quarterlyKD)
        {
            // 条件1：月K > 季K
            if (monthlyKD.K <= quarterlyKD.K)
                return false;

            // 条件2：周K < 月K
            if (weeklyKD.K >= monthlyKD.K)
                return false;

            return true;
        }
    }
}
