using System;
using System.Collections.Generic;
using System.Linq;
using MQReceiver.Calculators;
using MQReceiver.Filters;
using MQReceiver.Models;

namespace MQReceiver.Helpers
{
    /// <summary>
    /// 计算结果显示辅助类
    /// 负责格式化、排序和颜色显示计算结果
    /// </summary>
    public static class FilterDisplayHelper
    {
        /// <summary>
        /// 获取昨天的K值并创建带历史的结果列表
        /// </summary>
        public static List<FilterResultWithHistory> EnrichWithHistory(
            List<SimpleFilterResult> results,
            KDCalculator kdCalculator,
            DateTime targetDate)
        {
            var enrichedResults = new List<FilterResultWithHistory>();
            DateTime yesterday = GetPreviousTradingDay(targetDate);

            foreach (var result in results)
            {
                var enriched = new FilterResultWithHistory
                {
                    StockCode = result.StockCode,
                    StockName = result.StockName ?? result.StockCode,
                    WeeklyK = result.WeeklyK,
                    MonthlyK = result.MonthlyK,
                    QuarterlyK = result.QuarterlyK
                };

                // 获取昨天的K值
                try
                {
                    var yesterdayWeekly = kdCalculator.CalculateWeeklyKD(result.StockCode, yesterday);
                    if (yesterdayWeekly != null)
                    {
                        enriched.YesterdayWeeklyK = yesterdayWeekly.K;
                    }

                    var yesterdayMonthly = kdCalculator.CalculateMonthlyKD(result.StockCode, yesterday);
                    if (yesterdayMonthly != null)
                    {
                        enriched.YesterdayMonthlyK = yesterdayMonthly.K;
                    }

                    var yesterdayQuarterly = kdCalculator.CalculateQuarterlyKD(result.StockCode, yesterday);
                    if (yesterdayQuarterly != null)
                    {
                        enriched.YesterdayQuarterlyK = yesterdayQuarterly.K;
                    }
                }
                catch
                {
                    // 如果获取失败，保持为null
                }

                enrichedResults.Add(enriched);
            }

            return enrichedResults;
        }

        /// <summary>
        /// 获取上一个交易日
        /// </summary>
        private static DateTime GetPreviousTradingDay(DateTime date)
        {
            DateTime previous = date.AddDays(-1);

            // 跳过周末（简单处理，实际应该考虑节假日）
            while (previous.DayOfWeek == DayOfWeek.Sunday || previous.DayOfWeek == DayOfWeek.Saturday)
            {
                previous = previous.AddDays(-1);
            }

            return previous;
        }

        /// <summary>
        /// 按季K值排序（降序）
        /// </summary>
        public static List<FilterResultWithHistory> SortByQuarterlyK(List<FilterResultWithHistory> results, bool descending = true)
        {
            if (descending)
            {
                return results.OrderByDescending(r => r.QuarterlyK).ToList();
            }
            else
            {
                return results.OrderBy(r => r.QuarterlyK).ToList();
            }
        }

        /// <summary>
        /// 获取控制台窗口宽度（如果无法获取，返回默认值）
        /// </summary>
        public static int GetConsoleWidth()
        {
            try
            {
                return Console.WindowWidth > 0 ? Console.WindowWidth : 120;
            }
            catch
            {
                return 120; // 默认宽度
            }
        }

        /// <summary>
        /// 格式化K值显示（带颜色）
        /// </summary>
        public static void WriteKValueWithColor(decimal currentK, decimal? yesterdayK, int width, string format = "F2")
        {
            if (yesterdayK.HasValue)
            {
                if (currentK > yesterdayK.Value)
                {
                    // 红色：K值上升
                    Console.ForegroundColor = ConsoleColor.Red;
                }
                else if (currentK < yesterdayK.Value)
                {
                    // 绿色：K值下降
                    Console.ForegroundColor = ConsoleColor.Green;
                }
                else
                {
                    // 默认颜色：K值不变
                    Console.ForegroundColor = ConsoleColor.Gray;
                }
            }
            else
            {
                // 没有历史数据，使用默认颜色
                Console.ForegroundColor = ConsoleColor.Gray;
            }

            string value = currentK.ToString(format);
            Console.Write(value.PadLeft(width));
            Console.ResetColor();
        }
    }
}
