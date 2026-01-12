using System;
using MQReceiver.Cache;
using MQReceiver.Calculators;
using MQReceiver.Models;

namespace MQReceiver.Filters
{
    /// <summary>
    /// 表格三过滤器
    /// 条件：月K < 季K AND 周K上穿季K（周K金叉季K）
    /// 说明：周K今天首次大于季K（昨天周K <= 季K）
    /// </summary>
    public class Table3Filter : BaseStockFilter<Table3Condition>
    {
        public Table3Filter(KDCalculator kdCalculator, RealTimeDataCache realTimeCache)
            : base(kdCalculator, realTimeCache)
        {
        }

        /// <summary>
        /// 检查特定条件：月K < 季K AND 周K上穿季K
        /// </summary>
        protected override bool CheckSpecificConditions(
            Table3Condition condition,
            RealTimeDataRecord realTimeData,
            DateTime targetDate,
            KDResult weeklyKD,
            KDResult monthlyKD,
            KDResult quarterlyKD)
        {
            // 条件1：月K < 季K
            if (monthlyKD.K >= quarterlyKD.K)
                return false;

            // 条件2：周K > 季K（今天）
            if (weeklyKD.K <= quarterlyKD.K)
                return false;

            // 条件3：周K上穿季K（昨天周K <= 季K，今天周K > 季K）
            return CheckWeeklyCrossQuarterly(realTimeData.StockCode, targetDate, weeklyKD, quarterlyKD);
        }

        /// <summary>
        /// 检查周K是否上穿季K
        /// </summary>
        private bool CheckWeeklyCrossQuarterly(string stockCode, DateTime targetDate, KDResult currentWeeklyKD, KDResult currentQuarterlyKD)
        {
            try
            {
                // 获取前一个交易日的周KD和季KD（跳过周末）
                DateTime previousTradingDay = GetPreviousTradingDay(targetDate);
                var previousWeeklyKD = _kdCalculator.CalculateWeeklyKD(stockCode, previousTradingDay);
                var previousQuarterlyKD = _kdCalculator.CalculateQuarterlyKD(stockCode, previousTradingDay);

                if (previousWeeklyKD == null || previousQuarterlyKD == null)
                    return false;

                // 上穿条件：前一交易日周K <= 季K，今天周K > 季K
                return previousWeeklyKD.K <= previousQuarterlyKD.K && currentWeeklyKD.K > currentQuarterlyKD.K;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
