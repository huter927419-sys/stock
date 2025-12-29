using System;
using MQReceiver.Cache;
using MQReceiver.Calculators;
using MQReceiver.Models;

namespace MQReceiver.Filters
{
    /// <summary>
    /// 表格二过滤器
    /// 条件：月K > 季K AND 周K > 月K（不含上穿当天）
    /// 说明：周K已经在月K之上，且不是今天刚上穿的
    /// </summary>
    public class Table2Filter : BaseStockFilter<Table2Condition>
    {
        public Table2Filter(KDCalculator kdCalculator, RealTimeDataCache realTimeCache)
            : base(kdCalculator, realTimeCache)
        {
        }

        /// <summary>
        /// 检查特定条件：月K > 季K AND 周K > 月K（不含上穿当天）
        /// </summary>
        protected override bool CheckSpecificConditions(
            Table2Condition condition,
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

            // 条件3：不含上穿当天（昨天周K也要大于月K）
            return CheckNotCrossToday(realTimeData.StockCode, targetDate);
        }

        /// <summary>
        /// 检查是否不是今天上穿的（昨天周K > 月K）
        /// </summary>
        private bool CheckNotCrossToday(string stockCode, DateTime targetDate)
        {
            try
            {
                // 获取昨天的周KD和月KD
                var yesterdayWeeklyKD = _kdCalculator.CalculateWeeklyKD(stockCode, targetDate.AddDays(-1));
                var yesterdayMonthlyKD = _kdCalculator.CalculateMonthlyKD(stockCode, targetDate.AddDays(-1));

                if (yesterdayWeeklyKD == null || yesterdayMonthlyKD == null)
                    return false;

                // 不含上穿当天：昨天周K > 月K
                return yesterdayWeeklyKD.K > yesterdayMonthlyKD.K;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
