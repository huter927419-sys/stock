using System;
using MQReceiver.Cache;
using MQReceiver.Calculators;
using MQReceiver.Models;

namespace MQReceiver.Filters
{
    /// <summary>
    /// K值关系过滤器
    /// 过滤条件3：月K>季K or 周K 小于 月K
    /// </summary>
    public class KValueRelationFilter : BaseStockFilter<KValueRelationCondition>
    {
        public KValueRelationFilter(KDCalculator kdCalculator, RealTimeDataCache realTimeCache)
            : base(kdCalculator, realTimeCache)
        {
        }

        /// <summary>
        /// 检查K值关系特定条件
        /// </summary>
        protected override bool CheckSpecificConditions(
            KValueRelationCondition condition,
            RealTimeDataRecord realTimeData,
            DateTime targetDate,
            KDResult weeklyKD,
            KDResult monthlyKD,
            KDResult quarterlyKD)
        {
            // 检查条件：月K > 季K 或 周K < 月K
            return monthlyKD.K > quarterlyKD.K || weeklyKD.K < monthlyKD.K;
        }
    }
}
