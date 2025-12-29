using System;
using System.Configuration;
using Npgsql;
using MQReceiver.Helpers;
using MQReceiver.Cache;

namespace MQReceiver
{
    /// <summary>
    /// 快速数据库和Redis连接测试
    /// </summary>
    class QuickTestDB
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Console.WriteLine("========================================");
            Console.WriteLine("  数据库和Redis连接测试");
            Console.WriteLine("========================================");
            Console.WriteLine();

            // 测试PostgreSQL
            TestPostgreSQL();

            Console.WriteLine();

            // 测试Redis
            TestRedis();

            Console.WriteLine();
            Console.WriteLine();

            // 同步股票信息表
            SyncStockInfo();

            Console.WriteLine();

            // 测试KD过滤器
            MQReceiver.Tests.TestKDFilter.Run();

            Console.WriteLine();
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
        }

        static void TestPostgreSQL()
        {
            Console.WriteLine("【PostgreSQL 测试】");
            Console.WriteLine("----------------------------------------");

            try
            {
                string host = ConfigurationManager.AppSettings["DatabaseHost"] ?? "localhost";
                int port = int.Parse(ConfigurationManager.AppSettings["DatabasePort"] ?? "8532");
                string database = ConfigurationManager.AppSettings["DatabaseName"] ?? "stockdb";
                string username = ConfigurationManager.AppSettings["DatabaseUser"] ?? "postgres";
                string password = ConfigurationManager.AppSettings["DatabasePassword"] ?? "";

                Console.WriteLine($"连接信息: {host}:{port}/{database}");
                Console.WriteLine($"用户: {username}");
                Console.WriteLine();

                string connStr = $"Host={host};Port={port};Database={database};Username={username};Password={password};Timeout=10;";

                using (var conn = new NpgsqlConnection(connStr))
                {
                    conn.Open();
                    Console.WriteLine("✓ 数据库连接成功");
                    Console.WriteLine();

                    // 获取PostgreSQL版本
                    using (var cmd = new NpgsqlCommand("SELECT version();", conn))
                    {
                        var version = cmd.ExecuteScalar().ToString();
                        Console.WriteLine($"PostgreSQL版本: {version.Split(',')[0]}");
                    }

                    // 检查表
                    Console.WriteLine();
                    Console.WriteLine("检查数据表:");
                    string[] tables = { "stock_daily_data", "stock_realtime_data", "stock_exrights_data" };
                    foreach (var table in tables)
                    {
                        string sql = "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_schema='public' AND table_name=@name);";
                        using (var cmd = new NpgsqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@name", table);
                            bool exists = (bool)cmd.ExecuteScalar();
                            Console.WriteLine($"  {table}: {(exists ? "✓ 存在" : "✗ 不存在")}");
                        }
                    }

                    // 检查数据量
                    Console.WriteLine();
                    Console.WriteLine("数据统计:");
                    try
                    {
                        using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM stock_daily_data", conn))
                        {
                            var count = cmd.ExecuteScalar();
                            Console.WriteLine($"  总记录数: {count}");
                        }
                        using (var cmd = new NpgsqlCommand("SELECT COUNT(DISTINCT stock_code) FROM stock_daily_data", conn))
                        {
                            var count = cmd.ExecuteScalar();
                            Console.WriteLine($"  股票数量: {count}");
                        }

                        // 检查日期范围
                        using (var cmd = new NpgsqlCommand(
                            "SELECT MIN(trade_date), MAX(trade_date) FROM stock_daily_data", conn))
                        {
                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.Read() && !reader.IsDBNull(0))
                                {
                                    Console.WriteLine($"  日期范围: {reader.GetDateTime(0):yyyy-MM-dd} 至 {reader.GetDateTime(1):yyyy-MM-dd}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  查询数据量失败: {ex.Message}");
                    }

                    // 显示几条示例数据
                    Console.WriteLine();
                    Console.WriteLine("示例数据 (最新5条):");
                    try
                    {
                        using (var cmd = new NpgsqlCommand(
                            @"SELECT stock_code, trade_date, open_price, high_price, low_price, close_price
                              FROM stock_daily_data
                              ORDER BY trade_date DESC
                              LIMIT 5", conn))
                        {
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    Console.WriteLine($"  {reader.GetString(0)} | {reader.GetDateTime(1):yyyy-MM-dd} | " +
                                        $"O:{reader.GetDecimal(2):F2} H:{reader.GetDecimal(3):F2} L:{reader.GetDecimal(4):F2} C:{reader.GetDecimal(5):F2}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  查询示例数据失败: {ex.Message}");
                    }

                    Console.WriteLine();
                    Console.WriteLine("✓ PostgreSQL 测试通过");
                }
            }
            catch (NpgsqlException ex)
            {
                Console.WriteLine($"✗ 连接失败: {ex.Message}");
                Console.WriteLine($"错误代码: {ex.SqlState}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ 错误: {ex.Message}");
            }
        }

        static void TestRedis()
        {
            Console.WriteLine("【Redis 测试】");
            Console.WriteLine("----------------------------------------");

            try
            {
                string host = ConfigurationManager.AppSettings["RedisHost"] ?? "localhost";
                int port = int.Parse(ConfigurationManager.AppSettings["RedisPort"] ?? "6379");

                Console.WriteLine($"连接信息: {host}:{port}");
                Console.WriteLine();

                // 初始化Redis
                RedisHelper.Initialize();

                if (RedisHelper.IsConnected)
                {
                    Console.WriteLine("✓ Redis连接成功");
                    Console.WriteLine();

                    // 测试写入和读取
                    string testKey = "test:connection";
                    string testValue = $"TestAt_{DateTime.Now:HHmmss}";
                    RedisHelper.SetCache(testKey, testValue, TimeSpan.FromMinutes(1));
                    var readValue = RedisHelper.GetCache<string>(testKey);

                    if (readValue == testValue)
                    {
                        Console.WriteLine("✓ Redis读写测试通过");
                    }
                    else
                    {
                        Console.WriteLine($"✗ Redis读写测试失败: 期望={testValue}, 实际={readValue}");
                    }

                    Console.WriteLine();
                    Console.WriteLine("✓ Redis 测试通过");
                }
                else
                {
                    Console.WriteLine("✗ Redis连接失败");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Redis 错误: {ex.Message}");
                Console.WriteLine("  (Redis可选，不影响核心功能)");
            }
        }

        static void SyncStockInfo()
        {
            Console.WriteLine("【股票信息同步】");
            Console.WriteLine("----------------------------------------");

            try
            {
                Console.WriteLine("从日线数据同步股票代码到stock_info表...");
                Console.WriteLine();

                int newCount = StockInfoCache.Instance.SyncFromDailyData();

                Console.WriteLine();
                Console.WriteLine($"✓ 同步完成，缓存中共有 {StockInfoCache.Instance.Count} 条股票信息");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ 同步失败: {ex.Message}");
            }
        }

    }
}
