using System;
using System.Collections.Generic;
using MQReceiver.Cache;
using MQReceiver.Calculators;
using MQReceiver.Models;

namespace MQReceiver.Filters
{
    /// <summary>
    /// 表格一过滤器
    /// 条件：月K > 季K AND 周K上穿月K（周K金叉月K）
    /// 说明：周K今天首次大于月K（昨天周K <= 月K）
    /// </summary>
    public class Table1Filter : BaseStockFilter<Table1Condition>
    {
        public Table1Filter(KDCalculator kdCalculator, RealTimeDataCache realTimeCache)
            : base(kdCalculator, realTimeCache)
        {
        }

        /// <summary>
        /// 检查特定条件：月K > 季K AND 周K上穿月K
        /// </summary>
        protected override bool CheckSpecificConditions(
            Table1Condition condition,
            RealTimeDataRecord realTimeData,
            DateTime targetDate,
            KDResult weeklyKD,
            KDResult monthlyKD,
            KDResult quarterlyKD)
        {
            // 条件1：月K > 季K
            if (monthlyKD.K <= quarterlyKD.K)
                return false;

            // 条件2：周K > 月K（今天）
            if (weeklyKD.K <= monthlyKD.K)
                return false;

            // 条件3：周K上穿月K（昨天周K <= 月K，今天周K > 月K）
            return CheckWeeklyCrossMonthly(realTimeData.StockCode, targetDate, weeklyKD, monthlyKD);
        }

        /// <summary>
        /// 检查周K是否上穿月K
        /// </summary>
        private bool CheckWeeklyCrossMonthly(string stockCode, DateTime targetDate, KDResult currentWeeklyKD, KDResult currentMonthlyKD)
        {
            try
            {
                // 获取昨天的周KD和月KD
                var yesterdayWeeklyKD = _kdCalculator.CalculateWeeklyKD(stockCode, targetDate.AddDays(-1));
                var yesterdayMonthlyKD = _kdCalculator.CalculateMonthlyKD(stockCode, targetDate.AddDays(-1));

                if (yesterdayWeeklyKD == null || yesterdayMonthlyKD == null)
                    return false;

                // 上穿条件：昨天周K <= 月K，今天周K > 月K
                return yesterdayWeeklyKD.K <= yesterdayMonthlyKD.K && currentWeeklyKD.K > currentMonthlyKD.K;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
