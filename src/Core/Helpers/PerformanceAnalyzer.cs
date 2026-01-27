using System;
using MQReceiver.Models;

namespace MQReceiver.Helpers
{
    /// <summary>
    /// 性能分析器 - 用于估算处理时间
    /// </summary>
    public static class PerformanceAnalyzer
    {
        /// <summary>
        /// 估算处理指定数量股票所需时间
        /// </summary>
        public static PerformanceEstimate EstimateProcessingTime(int stockCount)
        {
            var estimate = new PerformanceEstimate
            {
                StockCount = stockCount
            };

            // 时间估算（基于典型场景）
            // 每个股票的耗时分解（考虑除权复权计算）：
            double timePerStockMs = 5526.0; // 毫秒（考虑除权复权计算）
            double totalTimeMs = stockCount * timePerStockMs;
            estimate.EstimatedTime = TimeSpan.FromMilliseconds(totalTimeMs);

            // 优化后的估算（预计算复权价格方案）
            double preCalculatedTimePerStockMs = 129.0;
            double preCalculatedTotalTimeMs = stockCount * preCalculatedTimePerStockMs;
            estimate.EstimatedTimeWithOptimization = TimeSpan.FromMilliseconds(preCalculatedTotalTimeMs);

            // 详细分解
            estimate.Breakdown = string.Format(
                "处理 {0} 只股票的估算时间（考虑除权复权计算）：\n" +
                "----------------------------------------\n" +
                "【当前实现 - 实时计算复权价格】\n" +
                "每只股票耗时：\n" +
                "  - 实时数据完整性检查：0.5ms\n" +
                "  - 周KD计算：\n" +
                "    * 查询日线数据（100条）：30ms\n" +
                "    * 复权计算（100条×18ms）：1800ms\n" +
                "    * 数据聚合：10ms\n" +
                "    * KD计算：2ms\n" +
                "    * 小计：1842ms\n" +
                "  - 月KD计算：1842ms\n" +
                "  - 季KD计算：1842ms\n" +
                "  - 计算条件检查：0.5ms\n" +
                "  - 小计：约5526ms/股票（约5.5秒）\n" +
                "\n" +
                "【预计算方案 - 入库时计算复权价格】\n" +
                "每只股票耗时：\n" +
                "  - 实时数据完整性检查：0.5ms\n" +
                "  - 周KD计算（直接查询复权价格）：43ms\n" +
                "  - 月KD计算（直接查询复权价格）：43ms\n" +
                "  - 季KD计算（直接查询复权价格）：43ms\n" +
                "  - 计算条件检查：0.5ms\n" +
                "  - 小计：约129ms/股票（减少97.7%）\n" +
                "\n" +
                "总耗时估算：\n" +
                "  - 当前实现：{1}\n" +
                "  - 预计算方案：{2}\n" +
                "  - 性能提升：{3:F1}%\n" +
                "\n" +
                "假设条件：\n" +
                "  - 数据库查询延迟：30ms/次（平均）\n" +
                "  - 每条日线数据复权计算：18ms（查询除权数据10ms + 计算8ms）\n" +
                "  - 平均每条KD计算需要100条日线数据\n" +
                "  - 数据聚合计算：10ms\n" +
                "  - KD计算：2ms\n" +
                "  - 数据库有适当索引\n" +
                "  - 数据库连接池已配置",
                stockCount,
                FormatTimeSpan(estimate.EstimatedTime),
                FormatTimeSpan(estimate.EstimatedTimeWithOptimization),
                (1 - estimate.EstimatedTimeWithOptimization.TotalMilliseconds / estimate.EstimatedTime.TotalMilliseconds) * 100
            );

            // 优化建议
            estimate.Recommendations.Add("【强烈推荐】入库时预计算复权价格：可以节省97.7%的计算时间！");
            estimate.Recommendations.Add("1. 在DailyDataDBWriter中，保存日线数据后立即计算并保存复权价格");
            estimate.Recommendations.Add("2. 在stock_daily_data表中添加adjusted_*_price字段存储复权价格");
            estimate.Recommendations.Add("3. KD计算时直接使用复权价格，无需查询除权数据并计算");
            estimate.Recommendations.Add("4. 当有新的除权数据时，触发重新计算该股票的历史复权价格");
            estimate.Recommendations.Add("5. 使用数据库连接池（已配置，MaxPoolSize=100）");
            estimate.Recommendations.Add("6. 数据库索引优化：确保stock_code和trade_date有索引");
            estimate.Recommendations.Add("7. 并行处理：使用Parallel.ForEach并行处理股票（预计算方案下效果更明显）");

            return estimate;
        }

        /// <summary>
        /// 格式化时间跨度
        /// </summary>
        private static string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalMinutes < 1)
            {
                return string.Format("{0:F1} 秒", timeSpan.TotalSeconds);
            }
            else if (timeSpan.TotalHours < 1)
            {
                return string.Format("{0:F1} 分钟 ({1:F0} 秒)", timeSpan.TotalMinutes, timeSpan.TotalSeconds);
            }
            else
            {
                return string.Format("{0:F1} 小时 ({1:F0} 分钟)", timeSpan.TotalHours, timeSpan.TotalMinutes);
            }
        }

        /// <summary>
        /// 打印性能估算报告
        /// </summary>
        public static void PrintEstimateReport(int stockCount)
        {
            var estimate = EstimateProcessingTime(stockCount);

            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine("  性能估算报告");
            Console.WriteLine("========================================");
            Console.WriteLine();
            Console.WriteLine(estimate.Breakdown);
            Console.WriteLine();
            Console.WriteLine("优化建议：");
            foreach (var recommendation in estimate.Recommendations)
            {
                Console.WriteLine("  " + recommendation);
            }
            Console.WriteLine();
            Console.WriteLine("========================================");
        }
    }
}
