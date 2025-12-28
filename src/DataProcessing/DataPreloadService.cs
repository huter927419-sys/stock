using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MQReceiver.Calculators;
using MQReceiver.Configuration;
using MQReceiver.Helpers;
using Npgsql;

namespace MQReceiver.Services
{
    /// <summary>
    /// 数据预加载服务
    /// 负责将历史KD数据预加载到Redis缓存中
    /// </summary>
    public class DataPreloadService
    {
        private readonly string connectionString;
        private readonly KDCalculator kdCalculator;
        private int batchSize;
        private int maxParallelism;
        private static readonly IConfigurationProvider _configProvider = AppConfigProvider.Instance;

        public DataPreloadService()
        {
            connectionString = DatabaseConnectionHelper.BuildConnectionString();
            kdCalculator = new KDCalculator();

            // 读取配置
            batchSize = _configProvider.GetInt("PreloadService_BatchSize", 100);
            maxParallelism = _configProvider.GetInt("PreloadService_MaxParallelism", 4);
        }

        /// <summary>
        /// 启动预加载服务
        /// </summary>
        public bool Start()
        {
            try
            {
                Console.WriteLine("========================================");
                Console.WriteLine("  启动数据预加载服务");
                Console.WriteLine("========================================");
                Console.WriteLine();

                // 检查Redis连接
                if (!RedisHelper.IsEnabled)
                {
                    Console.WriteLine("错误: Redis未启用，无法进行预加载");
                    Console.WriteLine("请先启动Redis服务或检查Redis配置");
                    return false;
                }

                // 检查数据库连接
                try
                {
                    using (var conn = new NpgsqlConnection(connectionString))
                    {
                        conn.Open();
                        using (var cmd = new NpgsqlCommand("SELECT 1", conn))
                        {
                            cmd.ExecuteScalar();
                        }
                    }
                    Console.WriteLine("数据库连接成功");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("错误: 数据库连接失败: " + ex.Message);
                    return false;
                }

                Console.WriteLine();
                Console.WriteLine("配置信息:");
                Console.WriteLine("  批次大小: {0}", batchSize);
                Console.WriteLine("  最大并行度: {0}", maxParallelism);
                Console.WriteLine();

                // 获取所有股票代码
                Console.WriteLine("正在获取股票列表...");
                List<string> stockCodes = GetAllStockCodes();
                Console.WriteLine("找到 {0} 只股票", stockCodes.Count);
                Console.WriteLine();

                if (stockCodes.Count == 0)
                {
                    Console.WriteLine("没有找到股票数据，退出预加载");
                    return false;
                }

                // 询问用户确认
                Console.WriteLine("准备预加载 {0} 只股票的KD数据到Redis", stockCodes.Count);
                Console.WriteLine("预计耗时: 约 {0:F1} 分钟", EstimateTime(stockCodes.Count));
                Console.WriteLine();
                Console.Write("是否继续？(Y/N): ");
                var key = Console.ReadKey();
                Console.WriteLine();
                Console.WriteLine();

                if (key.Key != ConsoleKey.Y)
                {
                    Console.WriteLine("用户取消预加载");
                    return false;
                }

                // 开始预加载
                Console.WriteLine("开始预加载...");
                Console.WriteLine();

                var stopwatch = Stopwatch.StartNew();
                int totalLoaded = PreloadKDData(stockCodes);
                stopwatch.Stop();

                Console.WriteLine();
                Console.WriteLine("========================================");
                Console.WriteLine("  预加载完成");
                Console.WriteLine("========================================");
                Console.WriteLine("总股票数: {0}", stockCodes.Count);
                Console.WriteLine("成功加载: {0}", totalLoaded);
                Console.WriteLine("失败数量: {0}", stockCodes.Count - totalLoaded);
                Console.WriteLine("总耗时: {0:F2} 秒 ({1:F1} 分钟)",
                    stopwatch.Elapsed.TotalSeconds, stopwatch.Elapsed.TotalMinutes);
                Console.WriteLine();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("错误: 预加载失败: " + ex.Message);
                Console.WriteLine("异常堆栈: " + ex.StackTrace);
                return false;
            }
        }

        /// <summary>
        /// 获取所有股票代码
        /// </summary>
        private List<string> GetAllStockCodes()
        {
            List<string> stockCodes = new List<string>();

            try
            {
                using (var conn = new NpgsqlConnection(connectionString))
                {
                    conn.Open();
                    string sql = @"
                        SELECT DISTINCT stock_code
                        FROM stock_daily_data
                        ORDER BY stock_code";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string stockCode = reader.GetString(0);
                                if (!string.IsNullOrEmpty(stockCode))
                                {
                                    stockCodes.Add(stockCode);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("获取股票列表失败: " + ex.Message);
            }

            return stockCodes;
        }

        /// <summary>
        /// 预加载KD数据
        /// </summary>
        private int PreloadKDData(List<string> stockCodes)
        {
            int totalLoaded = 0;
            int totalCount = stockCodes.Count;
            int processedCount = 0;
            DateTime lastUpdateTime = DateTime.Now;

            // 分批处理
            for (int i = 0; i < stockCodes.Count; i += batchSize)
            {
                var batch = stockCodes.Skip(i).Take(batchSize).ToList();

                // 并行处理批次
                Parallel.ForEach(batch, new ParallelOptions
                {
                    MaxDegreeOfParallelism = maxParallelism
                }, stockCode =>
                {
                    try
                    {
                        DateTime targetDate = DateTime.Now.Date;

                        // 计算周KD（缓存由KDCalculator内部处理，键格式：kd:{stockCode}:week:{yyyyMMdd}）
                        var weeklyKD = kdCalculator.CalculateWeeklyKD(stockCode, targetDate);

                        // 计算月KD
                        var monthlyKD = kdCalculator.CalculateMonthlyKD(stockCode, targetDate);

                        // 计算季KD
                        var quarterlyKD = kdCalculator.CalculateQuarterlyKD(stockCode, targetDate);

                        Interlocked.Increment(ref totalLoaded);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("预加载股票 {0} 失败: {1}", stockCode, ex.Message);
                    }
                });

                processedCount += batch.Count;

                // 显示进度
                if (DateTime.Now - lastUpdateTime >= TimeSpan.FromSeconds(1))
                {
                    double progress = (double)processedCount / totalCount * 100;
                    Console.Write("\r进度: {0}/{1} ({2:F1}%) - 已加载: {3}    ",
                        processedCount, totalCount, progress, totalLoaded);
                    lastUpdateTime = DateTime.Now;
                }
            }

            Console.WriteLine();
            return totalLoaded;
        }

        /// <summary>
        /// 估算预加载时间
        /// </summary>
        private double EstimateTime(int stockCount)
        {
            // 假设每只股票需要0.1秒（包括数据库查询和Redis写入）
            double secondsPerStock = 0.1;
            double totalSeconds = stockCount * secondsPerStock / maxParallelism;
            return totalSeconds / 60.0; // 转换为分钟
        }
    }
}
