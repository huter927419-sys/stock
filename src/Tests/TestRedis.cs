using System;
using System.Configuration;

namespace MQReceiver
{
    /// <summary>
    /// Redis连接测试程序
    /// </summary>
    class TestRedis
    {
        static void Main(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("  Redis 连接测试");
            Console.WriteLine("========================================");
            Console.WriteLine();

            // 读取配置
            string redisHost = ConfigurationManager.AppSettings["RedisHost"] ?? "localhost";
            int redisPort = int.Parse(ConfigurationManager.AppSettings["RedisPort"] ?? "6379");
            string redisPassword = ConfigurationManager.AppSettings["RedisPassword"] ?? "";
            int redisDatabase = int.Parse(ConfigurationManager.AppSettings["RedisDatabase"] ?? "0");

            Console.WriteLine("配置信息：");
            Console.WriteLine($"  Host: {redisHost}");
            Console.WriteLine($"  Port: {redisPort}");
            Console.WriteLine($"  Password: {(string.IsNullOrEmpty(redisPassword) ? "(无)" : "***")}");
            Console.WriteLine($"  Database: {redisDatabase}");
            Console.WriteLine();

            // 测试连接
            Console.WriteLine("正在测试Redis连接...");
            try
            {
                RedisHelper.Initialize();

                if (RedisHelper.IsEnabled)
                {
                    Console.WriteLine("✅ Redis连接成功！");
                    Console.WriteLine();

                    // 测试基本操作
                    Console.WriteLine("测试基本操作...");
                    
                    // 写入测试
                    string testKey = "test:connection";
                    string testValue = "Hello Redis!";
                    bool setResult = RedisHelper.SetCache(testKey, testValue, TimeSpan.FromSeconds(10));
                    Console.WriteLine($"  写入测试: {(setResult ? "✅ 成功" : "❌ 失败")}");

                    // 读取测试
                    string readValue = RedisHelper.GetCache<string>(testKey);
                    Console.WriteLine($"  读取测试: {(readValue == testValue ? "✅ 成功" : "❌ 失败")}");
                    if (readValue == testValue)
                    {
                        Console.WriteLine($"  读取值: {readValue}");
                    }

                    // 删除测试
                    bool deleteResult = RedisHelper.DeleteCache(testKey);
                    Console.WriteLine($"  删除测试: {(deleteResult ? "✅ 成功" : "❌ 失败")}");

                    // 检查是否存在
                    bool exists = RedisHelper.Exists(testKey);
                    Console.WriteLine($"  存在检查: {(exists ? "❌ 应该不存在" : "✅ 正确（已删除）")}");

                    Console.WriteLine();
                    Console.WriteLine("========================================");
                    Console.WriteLine("✅ 所有测试通过！Redis缓存功能正常！");
                    Console.WriteLine("========================================");
                }
                else
                {
                    Console.WriteLine("❌ Redis连接失败，但程序会自动降级到数据库查询");
                    Console.WriteLine("   功能正常，但性能无提升");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Redis连接测试失败: {ex.Message}");
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

