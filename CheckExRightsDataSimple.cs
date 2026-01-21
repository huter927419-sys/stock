using System;
using MQReceiver.Repositories;
using MQReceiver.Helpers;

namespace MQReceiver
{
    class CheckExRightsDataSimple
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("========================================");
            Console.WriteLine("  除权数据检查工具");
            Console.WriteLine("========================================");
            Console.WriteLine();

            try
            {
                // 创建除权数据仓储
                var exRightsRepo = new PostgresExRightsDataRepository();

                // 测试连接
                Console.Write("测试数据库连接... ");
                if (!exRightsRepo.TestConnection())
                {
                    Console.WriteLine("失败！");
                    Console.WriteLine("请检查数据库连接配置");
                    return;
                }
                Console.WriteLine("成功！");
                Console.WriteLine();

                // 检查一个测试股票的除权数据
                string testStockCode = "000001";
                Console.WriteLine(string.Format("检查股票 {0} 的除权数据:", testStockCode));

                bool hasData = exRightsRepo.HasExRightsData(testStockCode);
                Console.WriteLine(string.Format("  是否有除权数据: {0}", hasData ? "是" : "否"));

                if (hasData)
                {
                    var exRightsData = exRightsRepo.GetExRightsData(testStockCode);
                    Console.WriteLine(string.Format("  除权记录数: {0}", exRightsData.Count));

                    if (exRightsData.Count > 0)
                    {
                        Console.WriteLine();
                        Console.WriteLine("  最近3条除权记录:");
                        int displayCount = Math.Min(3, exRightsData.Count);
                        for (int i = exRightsData.Count - displayCount; i < exRightsData.Count; i++)
                        {
                            var record = exRightsData[i];
                            Console.WriteLine(string.Format("  {0} - 送股:{1} 配股:{2} 分红:{3}",
                                record.ExRightsDate.ToString("yyyy-MM-dd"),
                                record.GivePer10Shares,
                                record.PeiPer10Shares,
                                record.ProfitPerShare));
                        }
                    }
                }

                Console.WriteLine();
                Console.WriteLine("========================================");
                Console.WriteLine("结论:");
                if (hasData)
                {
                    Console.WriteLine("✓ 除权数据表存在且有数据");
                    Console.WriteLine("  建议：可以运行复权价格计算程序");
                }
                else
                {
                    Console.WriteLine("✗ 除权数据表为空或不存在");
                    Console.WriteLine("  建议：需要先导入除权数据");
                }
                Console.WriteLine("========================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("错误: {0}", ex.Message));
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine();
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
        }
    }
}
