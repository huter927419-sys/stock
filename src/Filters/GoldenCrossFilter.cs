using System;
using MQReceiver.Cache;
using MQReceiver.Calculators;
using MQReceiver.Models;

namespace MQReceiver.Filters
{
    /// <summary>
    /// 金叉过滤器
    /// 过滤条件1：周K、月K、季K 金叉过滤
    /// </summary>
    public class GoldenCrossFilter : BaseStockFilter<GoldenCrossCondition>
    {
        public GoldenCrossFilter(KDCalculator kdCalculator, RealTimeDataCache realTimeCache)
            : base(kdCalculator, realTimeCache)
        {
        }

        /// <summary>
        /// 检查金叉特定条件
        /// </summary>
        protected override bool CheckSpecificConditions(
            GoldenCrossCondition condition,
            RealTimeDataRecord realTimeData,
            DateTime targetDate,
            KDResult weeklyKD,
            KDResult monthlyKD,
            KDResult quarterlyKD)
        {
            // 检查金叉条件（K > D）
            bool weeklyGoldenCross = !condition.RequireWeeklyGoldenCross || (weeklyKD.K > weeklyKD.D);
            bool monthlyGoldenCross = !condition.RequireMonthlyGoldenCross || (monthlyKD.K > monthlyKD.D);
            bool quarterlyGoldenCross = !condition.RequireQuarterlyGoldenCross || (quarterlyKD.K > quarterlyKD.D);

            return weeklyGoldenCross && monthlyGoldenCross && quarterlyGoldenCross;
        }
    }
}
