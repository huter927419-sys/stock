using System;
using System.Configuration;
using Npgsql;

namespace MQReceiver
{
    /// <summary>
    /// 数据库连接测试程序
    /// </summary>
    class TestDatabase
    {
        static void Main(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("  数据库连接测试");
            Console.WriteLine("========================================");
            Console.WriteLine();

            // 读取配置
            string dbHost = ConfigurationManager.AppSettings["DatabaseHost"] ?? "localhost";
            int dbPort = int.Parse(ConfigurationManager.AppSettings["DatabasePort"] ?? "5432");
            string dbName = ConfigurationManager.AppSettings["DatabaseName"] ?? "stockdb";
            string dbUser = ConfigurationManager.AppSettings["DatabaseUser"] ?? "postgres";
            string dbPassword = ConfigurationManager.AppSettings["DatabasePassword"] ?? "";

            Console.WriteLine("配置信息：");
            Console.WriteLine($"  Host: {dbHost}");
            Console.WriteLine($"  Port: {dbPort}");
            Console.WriteLine($"  Database: {dbName}");
            Console.WriteLine($"  User: {dbUser}");
            Console.WriteLine($"  Password: {(string.IsNullOrEmpty(dbPassword) ? "(空)" : "***")}");
            Console.WriteLine();

            // 测试连接
            Console.WriteLine("正在测试数据库连接...");
            try
            {
                string connectionString = DatabaseConnectionHelper.BuildConnectionString();
                Console.WriteLine($"连接字符串: {connectionString.Replace(dbPassword, "***")}");
                Console.WriteLine();

                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    Console.WriteLine("✅ 数据库连接成功！");
                    Console.WriteLine();

                    // 测试基本查询
                    Console.WriteLine("测试基本查询...");
                    using (var command = new NpgsqlCommand("SELECT version();", connection))
                    {
                        var version = command.ExecuteScalar();
                        Console.WriteLine($"  PostgreSQL版本: {version}");
                    }

                    // 检查表是否存在
                    Console.WriteLine();
                    Console.WriteLine("检查数据表...");
                    string[] tables = { "stock_daily_data", "stock_realtime_data", "stock_exrights_data" };
                    foreach (var table in tables)
                    {
                        string checkTableSql = @"
                            SELECT EXISTS (
                                SELECT FROM information_schema.tables 
                                WHERE table_schema = 'public' 
                                AND table_name = @table_name
                            );";
                        using (var command = new NpgsqlCommand(checkTableSql, connection))
                        {
                            command.Parameters.AddWithValue("@table_name", table);
                            bool exists = (bool)command.ExecuteScalar();
                            Console.WriteLine($"  {table}: {(exists ? "✅ 存在" : "❌ 不存在")}");
                        }
                    }

                    // 检查连接池信息
                    Console.WriteLine();
                    Console.WriteLine("连接池信息：");
                    Console.WriteLine(DatabaseConnectionHelper.GetPoolInfo());

                    Console.WriteLine();
                    Console.WriteLine("========================================");
                    Console.WriteLine("✅ 所有测试通过！数据库连接正常！");
                    Console.WriteLine("========================================");
                }
            }
            catch (Npgsql.NpgsqlException ex)
            {
                Console.WriteLine($"❌ 数据库连接失败: {ex.Message}");
                Console.WriteLine();
                Console.WriteLine("可能的原因：");
                Console.WriteLine("  1. PostgreSQL服务未启动");
                Console.WriteLine("  2. 数据库配置错误（Host、Port、Database、User、Password）");
                Console.WriteLine("  3. 数据库不存在");
                Console.WriteLine("  4. 用户权限不足");
                Console.WriteLine();
                Console.WriteLine("详细错误信息：");
                Console.WriteLine(ex.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 测试失败: {ex.Message}");
                Console.WriteLine();
                Console.WriteLine("详细错误信息：");
                Console.WriteLine(ex.ToString());
            }

            Console.WriteLine();
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
        }
    }
}

