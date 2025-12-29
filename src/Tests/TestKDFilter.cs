using System;
using System.Collections.Generic;
using MQReceiver.Cache;
using MQReceiver.Calculators;
using MQReceiver.Filters;
using MQReceiver.Helpers;
using MQReceiver.Models;
using MQReceiver.Repositories;

namespace MQReceiver.Tests
{
    /// <summary>
    /// KD过滤器测试程序
    /// </summary>
    public class TestKDFilter
    {
        public static void Run()
        {
            Console.WriteLine("========================================");
            Console.WriteLine("  KD过滤器测试");
            Console.WriteLine("========================================");
            Console.WriteLine();

            try
            {
                // 初始化Redis（可选）
                try
                {
                    RedisHelper.Initialize();
                    Console.WriteLine("Redis初始化成功");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Redis初始化失败（可继续）: {ex.Message}");
                }

                // 测试数据库连接
                var repository = new PostgresStockDataRepository();
                if (!repository.TestConnection())
                {
                    Console.WriteLine("数据库连接失败！");
                    return;
                }
                Console.WriteLine("数据库连接成功");

                // 获取最新交易日期
                DateTime? latestDate = repository.GetLatestTradeDate();
                if (!latestDate.HasValue)
                {
                    Console.WriteLine("无法获取最新交易日期！");
                    return;
                }
                Console.WriteLine($"最新交易日期: {latestDate.Value:yyyy-MM-dd}");

                // 创建KD计算器
                var kdCalculator = new KDCalculator();

                // 测试单只股票的KD计算
                Console.WriteLine();
                Console.WriteLine("========== 测试000001的KD计算 ==========");
                TestSingleStock(kdCalculator, "000001", latestDate.Value);

                // 测试几只股票的过滤条件
                Console.WriteLine();
                Console.WriteLine("========== 测试过滤条件 ==========");
                TestFilterConditions(kdCalculator, repository, latestDate.Value);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"测试失败: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        private static void TestSingleStock(KDCalculator kdCalculator, string stockCode, DateTime targetDate)
        {
            Console.WriteLine($"股票代码: {stockCode}");
            Console.WriteLine($"目标日期: {targetDate:yyyy-MM-dd}");
            Console.WriteLine();

            // 计算周KD
            var weeklyKD = kdCalculator.CalculateWeeklyKD(stockCode, targetDate);
            if (weeklyKD != null)
            {
                Console.WriteLine($"周KD: K={weeklyKD.K:F2}, D={weeklyKD.D:F2}, RSV={weeklyKD.RSV:F2}");
                Console.WriteLine($"  金叉状态: {(weeklyKD.K > weeklyKD.D ? "是" : "否")} (K > D)");
            }
            else
            {
                Console.WriteLine("周KD: 计算失败（null）");
            }

            // 计算月KD
            var monthlyKD = kdCalculator.CalculateMonthlyKD(stockCode, targetDate);
            if (monthlyKD != null)
            {
                Console.WriteLine($"月KD: K={monthlyKD.K:F2}, D={monthlyKD.D:F2}, RSV={monthlyKD.RSV:F2}");
                Console.WriteLine($"  金叉状态: {(monthlyKD.K > monthlyKD.D ? "是" : "否")} (K > D)");
            }
            else
            {
                Console.WriteLine("月KD: 计算失败（null）");
            }

            // 计算季KD
            var quarterlyKD = kdCalculator.CalculateQuarterlyKD(stockCode, targetDate);
            if (quarterlyKD != null)
            {
                Console.WriteLine($"季KD: K={quarterlyKD.K:F2}, D={quarterlyKD.D:F2}, RSV={quarterlyKD.RSV:F2}");
                Console.WriteLine($"  金叉状态: {(quarterlyKD.K > quarterlyKD.D ? "是" : "否")} (K > D)");
            }
            else
            {
                Console.WriteLine("季KD: 计算失败（null）");
            }

            // 检查各条件
            Console.WriteLine();
            if (weeklyKD != null && monthlyKD != null && quarterlyKD != null)
            {
                Console.WriteLine("过滤条件检查：");

                // 条件1：金叉
                bool cond1 = weeklyKD.K > weeklyKD.D && monthlyKD.K > monthlyKD.D && quarterlyKD.K > quarterlyKD.D;
                Console.WriteLine($"  条件1（全部金叉）: {(cond1 ? "满足" : "不满足")}");

                // 条件2：周K>月K>季K
                bool cond2Order = weeklyKD.K > monthlyKD.K && monthlyKD.K > quarterlyKD.K;
                bool cond2NoGolden = weeklyKD.K <= weeklyKD.D && monthlyKD.K <= monthlyKD.D && quarterlyKD.K <= quarterlyKD.D;
                Console.WriteLine($"  条件2-顺序（周K>月K>季K）: {(cond2Order ? "满足" : "不满足")} ({weeklyKD.K:F2} > {monthlyKD.K:F2} > {quarterlyKD.K:F2})");
                Console.WriteLine($"  条件2-非金叉: {(cond2NoGolden ? "满足" : "不满足")}");

                // 条件3：月K>季K 或 周K<月K
                bool cond3 = monthlyKD.K > quarterlyKD.K || weeklyKD.K < monthlyKD.K;
                Console.WriteLine($"  条件3（月K>季K 或 周K<月K）: {(cond3 ? "满足" : "不满足")}");
            }
        }

        private static void TestFilterConditions(KDCalculator kdCalculator, PostgresStockDataRepository repository, DateTime targetDate)
        {
            // 获取前20只股票进行测试
            var stockCodes = repository.GetAllStockCodes();
            if (stockCodes.Count == 0)
            {
                Console.WriteLine("没有股票代码！");
                return;
            }

            Console.WriteLine($"总共 {stockCodes.Count} 只股票，测试前20只...");
            Console.WriteLine();

            int passedCond1 = 0, passedCond2 = 0, passedCond3 = 0;
            int failed = 0;

            var testCodes = stockCodes.Count > 20 ? stockCodes.GetRange(0, 20) : stockCodes;

            foreach (var stockCode in testCodes)
            {
                var weeklyKD = kdCalculator.CalculateWeeklyKD(stockCode, targetDate);
                var monthlyKD = kdCalculator.CalculateMonthlyKD(stockCode, targetDate);
                var quarterlyKD = kdCalculator.CalculateQuarterlyKD(stockCode, targetDate);

                if (weeklyKD == null || monthlyKD == null || quarterlyKD == null)
                {
                    Console.WriteLine($"{stockCode}: KD计算失败 (周={weeklyKD != null}, 月={monthlyKD != null}, 季={quarterlyKD != null})");
                    failed++;
                    continue;
                }

                // 检查条件
                bool cond1 = weeklyKD.K > weeklyKD.D && monthlyKD.K > monthlyKD.D && quarterlyKD.K > quarterlyKD.D;
                bool cond2Order = weeklyKD.K > monthlyKD.K && monthlyKD.K > quarterlyKD.K;
                bool cond2NoGolden = weeklyKD.K <= weeklyKD.D && monthlyKD.K <= monthlyKD.D && quarterlyKD.K <= quarterlyKD.D;
                bool cond3 = monthlyKD.K > quarterlyKD.K || weeklyKD.K < monthlyKD.K;

                string status = "";
                if (cond1) { passedCond1++; status += "[1]"; }
                if (cond2Order && cond2NoGolden) { passedCond2++; status += "[2]"; }
                if (cond3) { passedCond3++; status += "[3]"; }

                Console.WriteLine($"{stockCode}: 周K={weeklyKD.K:F1}({(weeklyKD.K > weeklyKD.D ? "金" : " ")}) " +
                    $"月K={monthlyKD.K:F1}({(monthlyKD.K > monthlyKD.D ? "金" : " ")}) " +
                    $"季K={quarterlyKD.K:F1}({(quarterlyKD.K > quarterlyKD.D ? "金" : " ")}) " +
                    $"{(string.IsNullOrEmpty(status) ? "无匹配" : status)}");
            }

            Console.WriteLine();
            Console.WriteLine($"统计（共{testCodes.Count}只股票）：");
            Console.WriteLine($"  KD计算失败: {failed}");
            Console.WriteLine($"  条件1（金叉）通过: {passedCond1}");
            Console.WriteLine($"  条件2（周>月>季且非金叉）通过: {passedCond2}");
            Console.WriteLine($"  条件3（月>季或周<月）通过: {passedCond3}");
        }
    }
}
