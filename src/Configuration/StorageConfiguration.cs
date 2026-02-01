using System;
using System.IO;
using MQReceiver.DataProcessing.Factories;
using MQReceiver.Configuration;

namespace MQReceiver
{
    /// <summary>
    /// 存储后端配置示例
    /// 演示如何在应用程序启动时配置使用 PostgreSQL 或 RocksDB
    /// </summary>
    public class StorageConfiguration
    {
        /// <summary>
        /// 初始化存储后端
        /// 根据配置文件决定使用哪种存储
        /// </summary>
        public static void Initialize()
        {
            try
            {
                var configProvider = AppConfigProvider.Instance;

                // 从配置文件读取存储后端类型
                // 支持的值: "PostgreSQL", "RocksDB", "FileBased"
                string backendType = configProvider.GetString("StorageBackend", "RocksDB");

                Console.WriteLine($"[存储配置] 正在初始化存储后端: {backendType}");

                switch (backendType.ToUpper())
                {
                    case "POSTGRESQL":
                    case "PG":
                        InitializePostgreSQL();
                        break;

                    case "ROCKSDB":
                    case "ROCKS":
                    case "FILEBASED":
                    default:
                        InitializeRocksDB();
                        break;
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine($"[存储配置] IO 异常: {ex.Message}");
                Console.WriteLine($"[存储配置] 堆栈: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// 初始化 PostgreSQL 存储
        /// </summary>
        private static void InitializePostgreSQL()
        {
            var configProvider = AppConfigProvider.Instance;

            // 读取 PostgreSQL 配置
            string host = configProvider.GetString("DatabaseHost", "localhost");
            int port = configProvider.GetInt("DatabasePort", 8532);
            string database = configProvider.GetString("DatabaseName", "stockdb");
            string username = configProvider.GetString("DatabaseUser", "postgres");
            string password = configProvider.GetString("DatabasePassword", "");

            // 构建连接字符串
            string connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password}";

            // 配置 RepositoryFactory
            RepositoryFactory.Configure(
                RepositoryFactory.StorageBackend.PostgreSQL,
                connectionString: connectionString
            );

            Console.WriteLine($"[存储配置] PostgreSQL 已配置: {host}:{port}/{database}");
        }

        /// <summary>
        /// 初始化 RocksDB 存储
        /// </summary>
        private static void InitializeRocksDB()
        {
            var configProvider = AppConfigProvider.Instance;

            // 读取 RocksDB 配置；相对路径按程序基目录解析，避免工作目录不同导致 IOException
            string dbPath = configProvider.GetString("RocksDBPath", "data/rocksdb");
            dbPath = ResolveRocksDBPath(dbPath);

            RepositoryFactory.Configure(
                RepositoryFactory.StorageBackend.RocksDB,
                dbPath: dbPath
            );

            Console.WriteLine($"[存储配置] RocksDB 已配置: {dbPath}");
        }

        /// <summary>
        /// 将相对路径解析为基于程序基目录的绝对路径，避免当前工作目录不可写导致 IOException
        /// </summary>
        private static string ResolveRocksDBPath(string dbPath)
        {
            if (string.IsNullOrWhiteSpace(dbPath))
                dbPath = "data/rocksdb";
            dbPath = dbPath.Trim();
            // 已是绝对路径（含盘符或以 / 开头）则不拼接
            if (Path.IsPathRooted(dbPath))
                return dbPath;
            string baseDir = AppDomain.CurrentDomain.BaseDirectory ?? ".";
            try
            {
                return Path.GetFullPath(Path.Combine(baseDir, dbPath));
            }
            catch (IOException ex)
            {
                Console.WriteLine($"[存储配置] 路径解析失败 BaseDirectory={baseDir} RocksDBPath={dbPath}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 切换存储后端（运行时切换）
        /// </summary>
        public static void SwitchBackend(string backendType)
        {
            Console.WriteLine($"[存储配置] 切换存储后端到: {backendType}");

            // 重置现有仓储
            RepositoryFactory.Reset();

            // 重新初始化
            Initialize();
        }

        /// <summary>
        /// 获取当前存储后端信息
        /// </summary>
        public static string GetCurrentBackendInfo()
        {
            var backend = RepositoryFactory.GetCurrentBackend();
            return $"当前存储后端: {backend}";
        }

        /// <summary>
        /// 测试当前存储后端连接
        /// </summary>
        public static bool TestConnection()
        {
            try
            {
                var stockRepo = RepositoryFactory.GetStockDataRepository();
                bool connected = stockRepo.TestConnection();

                if (connected)
                {
                    Console.WriteLine($"[存储配置] ✓ 存储后端连接成功: {RepositoryFactory.GetCurrentBackend()}");
                }
                else
                {
                    Console.WriteLine($"[存储配置] ❌ 存储后端连接失败: {RepositoryFactory.GetCurrentBackend()}");
                }

                return connected;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[存储配置] ❌ 连接测试异常: {ex.Message}");
                return false;
            }
        }
    }
}
