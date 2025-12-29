using System;
using System.Configuration;
using Npgsql;
using StackExchange.Redis;

class TestConnection
{
    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Console.WriteLine("========================================");
        Console.WriteLine("  数据库和Redis连接测试");
        Console.WriteLine("========================================\n");

        // 测试PostgreSQL
        TestPostgreSQL();

        Console.WriteLine();

        // 测试Redis
        TestRedis();

        Console.WriteLine("\n按任意键退出...");
        Console.ReadKey();
    }

    static void TestPostgreSQL()
    {
        Console.WriteLine("【PostgreSQL 测试】");
        Console.WriteLine("----------------------------------------");

        try
        {
            string host = "localhost";
            int port = 8532;
            string database = "stockdb";
            string username = "postgres";
            string password = "cd123321";

            string connStr = $"Host={host};Port={port};Database={database};Username={username};Password={password};Timeout=10;";
            Console.WriteLine($"连接: {host}:{port}/{database}");

            using (var conn = new NpgsqlConnection(connStr))
            {
                conn.Open();
                Console.WriteLine("✓ 数据库连接成功\n");

                // 检查表是否存在
                Console.WriteLine("检查数据表:");
                using (var cmd = new NpgsqlCommand(
                    "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name='stock_daily_data')", conn))
                {
                    bool exists = (bool)cmd.ExecuteScalar();
                    Console.WriteLine($"  stock_daily_data: {(exists ? "存在" : "不存在")}");
                }

                // 检查数据量
                Console.WriteLine("\n数据统计:");
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

                // 显示几条示例数据
                Console.WriteLine("\n示例数据 (前5条):");
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
                                $"开:{reader.GetDecimal(2):F2} 高:{reader.GetDecimal(3):F2} 低:{reader.GetDecimal(4):F2} 收:{reader.GetDecimal(5):F2}");
                        }
                    }
                }

                Console.WriteLine("\n✓ PostgreSQL 测试通过");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ PostgreSQL 错误: {ex.Message}");
        }
    }

    static void TestRedis()
    {
        Console.WriteLine("【Redis 测试】");
        Console.WriteLine("----------------------------------------");

        try
        {
            string host = "localhost";
            int port = 6379;

            Console.WriteLine($"连接: {host}:{port}");

            var options = ConfigurationOptions.Parse($"{host}:{port}");
            options.ConnectTimeout = 5000;
            options.AbortOnConnectFail = false;

            using (var redis = ConnectionMultiplexer.Connect(options))
            {
                if (redis.IsConnected)
                {
                    Console.WriteLine("✓ Redis连接成功\n");

                    var db = redis.GetDatabase();
                    var server = redis.GetServer($"{host}:{port}");

                    // 获取数据库信息
                    var info = server.Info("keyspace");
                    Console.WriteLine("数据库信息:");
                    foreach (var group in info)
                    {
                        foreach (var pair in group)
                        {
                            Console.WriteLine($"  {pair.Key}: {pair.Value}");
                        }
                    }

                    // 检查KD缓存键
                    Console.WriteLine("\nKD缓存检查:");
                    var keys = server.Keys(pattern: "kd:*", pageSize: 10);
                    int keyCount = 0;
                    foreach (var key in keys)
                    {
                        keyCount++;
                        if (keyCount <= 5)
                        {
                            Console.WriteLine($"  {key}");
                        }
                    }
                    Console.WriteLine($"  共找到 {keyCount} 个KD缓存键");

                    Console.WriteLine("\n✓ Redis 测试通过");
                }
                else
                {
                    Console.WriteLine("✗ Redis连接失败");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Redis 错误: {ex.Message}");
        }
    }
}
