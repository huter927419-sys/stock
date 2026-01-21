using System;
using System.Linq;
using MQReceiver.Calculators;
using MQReceiver.Repositories;
using MQReceiver.Services;
using MQReceiver.Helpers;

namespace MQReceiver.Tools
{
    /// <summary>
    /// 验证KD计算和数据加载
    /// </summary>
    class VerifyKDData
    {
        static void Main(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("  KD 数据加载和计算验证工具");
            Console.WriteLine("========================================\n");

            string stockCode = "000001"; // 平安银行
            if (args.Length > 0)
            {
                stockCode = args[0];
            }

            Console.WriteLine($"测试股票: {stockCode}\n");

            try
            {
                // 1. 验证数据库中的数据量
                Console.WriteLine("【1】检查数据库中的数据量...");
                var repository = new PostgresKlineDataRepository();
                var dateRange = repository.GetDataDateRange(stockCode);
                
                if (!dateRange.StartDate.HasValue || !dateRange.EndDate.HasValue)
                {
                    Console.WriteLine($"❌ 股票 {stockCode} 没有数据！");
                    Console.WriteLine("\n按任意键退出...");
                    Console.ReadKey();
                    return;
                }
                
                Console.WriteLine($"  最早日期: {dateRange.StartDate:yyyy-MM-dd}");
                Console.WriteLine($"  最晚日期: {dateRange.EndDate:yyyy-MM-dd}");
                
                var allData = repository.GetDailyData(stockCode, dateRange.StartDate.Value, dateRange.EndDate.Value);
                Console.WriteLine($"  数据记录数: {allData.Count()} 条");
                
                if (allData.Count() > 0)
                {
                    var first = allData.First();
                    var last = allData.Last();
                    Console.WriteLine($"  第一条: {first.TradeDate:yyyy-MM-dd} 收盘={first.Close:F2}");
                    Console.WriteLine($"  最后一条: {last.TradeDate:yyyy-MM-dd} 收盘={last.Close:F2}");
                }
                
                // 2. 验证ChartService加载的数据量
                Console.WriteLine("\n【2】检查ChartService加载的数据量...");
                var chartService = new ChartService();
                var chartData = chartService.LoadChartData(stockCode, 0); // 0 = 加载全部
                
                Console.WriteLine($"  K线数据量: {chartData.DailyKline?.Count ?? 0} 条");
                
                if (chartData.DailyKline != null && chartData.DailyKline.Count > 0)
                {
                    var firstCandle = chartData.DailyKline.First();
                    var lastCandle = chartData.DailyKline.Last();
                    Console.WriteLine($"  第一根K线: {firstCandle.Date:yyyy-MM-dd} 收盘={firstCandle.Close:F2}");
                    Console.WriteLine($"  最后一根K线: {lastCandle.Date:yyyy-MM-dd} 收盘={lastCandle.Close:F2}");
                    
                    // 验证数据量是否一致
                    if (chartData.DailyKline.Count == allData.Count())
                    {
                        Console.WriteLine($"  ✅ 数据量匹配！ChartService加载了全量数据");
                    }
                    else
                    {
                        Console.WriteLine($"  ⚠️ 数据量不匹配！");
                        Console.WriteLine($"     数据库: {allData.Count()} 条");
                        Console.WriteLine($"     ChartService: {chartData.DailyKline.Count} 条");
                        Console.WriteLine($"     差异: {allData.Count() - chartData.DailyKline.Count} 条");
                    }
                }
                else
                {
                    Console.WriteLine($"  ❌ K线数据为空！");
                }
                
                // 3. 验证KD数据量
                Console.WriteLine("\n【3】检查KD指标数据量...");
                Console.WriteLine($"  周KD: {chartData.WeeklyKD?.Count ?? 0} 条");
                Console.WriteLine($"  月KD: {chartData.MonthlyKD?.Count ?? 0} 条");
                Console.WriteLine($"  季KD: {chartData.QuarterlyKD?.Count ?? 0} 条");
                
                if (chartData.WeeklyKD != null && chartData.WeeklyKD.Count > 0)
                {
                    var firstKD = chartData.WeeklyKD.First();
                    var lastKD = chartData.WeeklyKD.Last();
                    Console.WriteLine($"  周KD第一条: {firstKD.Date:yyyy-MM-dd} K={firstKD.K:F2} D={firstKD.D:F2}");
                    Console.WriteLine($"  周KD最后一条: {lastKD.Date:yyyy-MM-dd} K={lastKD.K:F2} D={lastKD.D:F2}");
                    
                    // 验证周KD数据量是否与日K线数据量一致
                    if (chartData.WeeklyKD.Count == chartData.DailyKline.Count)
                    {
                        Console.WriteLine($"  ✅ 周KD数据与日K线完全对齐（每个交易日都有对应的周KD值）");
                    }
                    else
                    {
                        Console.WriteLine($"  ⚠️ 周KD数据量与日K线不一致");
                        Console.WriteLine($"     日K线: {chartData.DailyKline.Count} 条");
                        Console.WriteLine($"     周KD: {chartData.WeeklyKD.Count} 条");
                        
                        double ratio = (double)chartData.WeeklyKD.Count / chartData.DailyKline.Count * 100;
                        Console.WriteLine($"     覆盖率: {ratio:F1}%");
                    }
                }
                else
                {
                    Console.WriteLine($"  ❌ 周KD数据为空！");
                }
                
                // 4. 测试KD计算器直接计算
                Console.WriteLine("\n【4】测试KD计算器直接计算...");
                var kdCalc = new KDCalculator();
                var targetDate = DateTime.Now.Date;
                
                var weeklyKD = kdCalc.CalculateWeeklyKD(stockCode, targetDate);
                if (weeklyKD != null)
                {
                    Console.WriteLine($"  ✅ 周KD计算成功");
                    Console.WriteLine($"     日期: {weeklyKD.Date:yyyy-MM-dd}");
                    Console.WriteLine($"     K值: {weeklyKD.K:F2}");
                    Console.WriteLine($"     D值: {weeklyKD.D:F2}");
                    Console.WriteLine($"     RSV: {weeklyKD.RSV:F2}");
                    Console.WriteLine($"     K-D: {(weeklyKD.K - weeklyKD.D):F2}");
                }
                else
                {
                    Console.WriteLine($"  ❌ 周KD计算失败！");
                }
                
                var monthlyKD = kdCalc.CalculateMonthlyKD(stockCode, targetDate);
                if (monthlyKD != null)
                {
                    Console.WriteLine($"  ✅ 月KD计算成功");
                    Console.WriteLine($"     K值: {monthlyKD.K:F2}, D值: {monthlyKD.D:F2}, K-D: {(monthlyKD.K - monthlyKD.D):F2}");
                }
                
                var quarterlyKD = kdCalc.CalculateQuarterlyKD(stockCode, targetDate);
                if (quarterlyKD != null)
                {
                    Console.WriteLine($"  ✅ 季KD计算成功");
                    Console.WriteLine($"     K值: {quarterlyKD.K:F2}, D值: {quarterlyKD.D:F2}, K-D: {(quarterlyKD.K - quarterlyKD.D):F2}");
                }
                
                // 5. 验证历史KD序列
                Console.WriteLine("\n【5】测试历史KD序列...");
                var kdSequence = kdCalc.GetWeeklyKDSequence(stockCode, targetDate, 10);
                
                if (kdSequence != null && kdSequence.Count > 0)
                {
                    Console.WriteLine($"  ✅ 历史周KD序列: {kdSequence.Count} 个周期");
                    Console.WriteLine($"  前5个周期:");
                    foreach (var kd in kdSequence.Take(5))
                    {
                        Console.WriteLine($"    {kd.Date:yyyy-MM-dd}: K={kd.K:F2}, D={kd.D:F2}, K-D={kd.K - kd.D:F2}");
                    }
                }
                else
                {
                    Console.WriteLine($"  ❌ 历史KD序列为空！");
                }
                
                // 6. 总结
                Console.WriteLine("\n========================================");
                Console.WriteLine("  验证总结");
                Console.WriteLine("========================================");
                Console.WriteLine($"✅ 数据库数据: {allData.Count()} 条");
                Console.WriteLine($"✅ 图表加载数据: {chartData.DailyKline?.Count ?? 0} 条");
                Console.WriteLine($"✅ 周KD数据: {chartData.WeeklyKD?.Count ?? 0} 条");
                Console.WriteLine($"✅ 月KD数据: {chartData.MonthlyKD?.Count ?? 0} 条");
                Console.WriteLine($"✅ 季KD数据: {chartData.QuarterlyKD?.Count ?? 0} 条");
                
                bool allDataLoaded = chartData.DailyKline?.Count == allData.Count();
                bool kdDataPresent = (chartData.WeeklyKD?.Count ?? 0) > 0;
                
                if (allDataLoaded && kdDataPresent)
                {
                    Console.WriteLine("\n✅✅✅ 验证通过！所有数据加载正常！");
                }
                else if (allDataLoaded && !kdDataPresent)
                {
                    Console.WriteLine("\n⚠️ K线数据正常，但KD数据为空，请检查KD计算逻辑");
                }
                else if (!allDataLoaded)
                {
                    Console.WriteLine("\n❌ K线数据加载不完整，请检查数据加载逻辑");
                }
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ 验证过程出错:");
                Console.WriteLine($"   {ex.Message}");
                Console.WriteLine($"\n堆栈跟踪:");
                Console.WriteLine(ex.StackTrace);
            }
            
            Console.WriteLine("\n========================================");
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
        }
    }
}
