using System;
using System.Collections.Generic;
using MQReceiver.Cache;
using MQReceiver.Calculators;
using MQReceiver.Models;

namespace MQReceiver.Filters
{
    /// <summary>
    /// K值顺序过滤器
    /// 过滤条件2：周K>月K>季K（不含金叉）
    /// </summary>
    public class KValueOrderFilter : BaseStockFilter<KValueOrderCondition>
    {
        public KValueOrderFilter(KDCalculator kdCalculator, RealTimeDataCache realTimeCache)
            : base(kdCalculator, realTimeCache)
        {
        }

        /// <summary>
        /// 检查K值顺序特定条件
        /// </summary>
        protected override bool CheckSpecificConditions(
            KValueOrderCondition condition,
            RealTimeDataRecord realTimeData,
            DateTime targetDate,
            KDResult weeklyKD,
            KDResult monthlyKD,
            KDResult quarterlyKD)
        {
            // 检查条件：周K > 月K > 季K
            if (!(weeklyKD.K > monthlyKD.K && monthlyKD.K > quarterlyKD.K))
                return false;

            // 检查"不含金叉"条件
            return CheckNoGoldenCrossAfterBreak(realTimeData.StockCode, targetDate, weeklyKD, monthlyKD, quarterlyKD);
        }

        /// <summary>
        /// 检查"不含金叉"条件：从破了金叉后没有再次突破金叉
        /// </summary>
        private bool CheckNoGoldenCrossAfterBreak(
            string stockCode,
            DateTime targetDate,
            KDResult weeklyKD,
            KDResult monthlyKD,
            KDResult quarterlyKD)
        {
            try
            {
                // 检查当前是否不是金叉（周K、月K、季K都 <= D）
                bool currentNotGoldenCross = weeklyKD.K <= weeklyKD.D &&
                                            monthlyKD.K <= monthlyKD.D &&
                                            quarterlyKD.K <= quarterlyKD.D;

                if (!currentNotGoldenCross)
                    return false;

                // 获取历史KD序列（检查最近10个周期）
                var weeklyHistory = _kdCalculator.GetWeeklyKDSequence(stockCode, targetDate, 10);
                var monthlyHistory = _kdCalculator.GetMonthlyKDSequence(stockCode, targetDate, 10);
                var quarterlyHistory = _kdCalculator.GetQuarterlyKDSequence(stockCode, targetDate, 10);

                // 检查各周期是否满足"不含金叉"条件
                bool weeklyNoGoldenCross = CheckNoGoldenCrossInHistory(weeklyHistory, weeklyKD);
                bool monthlyNoGoldenCross = CheckNoGoldenCrossInHistory(monthlyHistory, monthlyKD);
                bool quarterlyNoGoldenCross = CheckNoGoldenCrossInHistory(quarterlyHistory, quarterlyKD);

                return weeklyNoGoldenCross && monthlyNoGoldenCross && quarterlyNoGoldenCross;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"检查不含金叉条件失败: {ex.Message}");
                return true;
            }
        }

        /// <summary>
        /// 检查历史KD序列中是否满足"不含金叉"条件
        /// </summary>
        private bool CheckNoGoldenCrossInHistory(List<KDResult> history, KDResult current)
        {
            if (history == null || history.Count == 0)
                return true;

            bool foundGoldenCross = false;
            bool foundBreak = false;

            for (int i = history.Count - 1; i >= 0; i--)
            {
                var kd = history[i];

                if (kd.K > kd.D)
                {
                    if (foundBreak)
                        return false;
                    foundGoldenCross = true;
                }
                else if (kd.K <= kd.D)
                {
                    if (foundGoldenCross)
                        foundBreak = true;
                }
            }

            if (current.K <= current.D)
            {
                if (foundGoldenCross && foundBreak)
                    return true;
            }

            return true;
        }
    }
}
