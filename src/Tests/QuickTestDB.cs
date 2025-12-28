using System;
using System.Configuration;
using Npgsql;

namespace MQReceiver
{
    /// <summary>
    /// 快速数据库连接测试
    /// </summary>
    class QuickTestDB
    {
        static void Main(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("  数据库连接快速测试");
            Console.WriteLine("========================================");
            Console.WriteLine();

            string host = ConfigurationManager.AppSettings["DatabaseHost"] ?? "localhost";
            int port = int.Parse(ConfigurationManager.AppSettings["DatabasePort"] ?? "8532");
            string database = ConfigurationManager.AppSettings["DatabaseName"] ?? "stockdb";
            string username = ConfigurationManager.AppSettings["DatabaseUser"] ?? "postgres";
            string password = ConfigurationManager.AppSettings["DatabasePassword"] ?? "";

            Console.WriteLine($"连接信息: {host}:{port}/{database}");
            Console.WriteLine($"用户: {username}");
            Console.WriteLine();

            try
            {
                string connStr = $"Host={host};Port={port};Database={database};Username={username};Password={password};Timeout=10;";
                Console.WriteLine("正在连接...");
                
                using (var conn = new NpgsqlConnection(connStr))
                {
                    conn.Open();
                    Console.WriteLine("✅ 数据库连接成功！");
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
                            Console.WriteLine($"  {table}: {(exists ? "✅" : "❌")}");
                        }
                    }

                    Console.WriteLine();
                    Console.WriteLine("========================================");
                    Console.WriteLine("✅ 测试完成！数据库连接正常！");
                    Console.WriteLine("========================================");
                }
            }
            catch (NpgsqlException ex)
            {
                Console.WriteLine($"❌ 连接失败: {ex.Message}");
                Console.WriteLine();
                Console.WriteLine("错误代码: " + ex.SqlState);
                Console.WriteLine();
                Console.WriteLine("可能的原因:");
                if (ex.Message.Contains("timeout") || ex.Message.Contains("连接"))
                {
                    Console.WriteLine("  - PostgreSQL服务未启动");
                    Console.WriteLine("  - 端口" + port + "不正确或被占用");
                    Console.WriteLine("  - 防火墙阻止连接");
                }
                else if (ex.Message.Contains("password") || ex.Message.Contains("认证"))
                {
                    Console.WriteLine("  - 用户名或密码错误");
                }
                else if (ex.Message.Contains("database") || ex.Message.Contains("不存在"))
                {
                    Console.WriteLine("  - 数据库 " + database + " 不存在");
                    Console.WriteLine("  - 需要创建数据库: CREATE DATABASE " + database + ";");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 错误: {ex.Message}");
            }

            Console.WriteLine();
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
        }
    }
}

