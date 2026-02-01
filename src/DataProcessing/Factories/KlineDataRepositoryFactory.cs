using System;
using MQReceiver.DataProcessing.Cache;
using MQReceiver.DataProcessing.Repositories;

namespace MQReceiver.DataProcessing.Factories
{
    /// <summary>
    /// K线数据仓储工厂
    /// 用于管理不同的仓储实现（PostgreSQL、RocksDB、文件系统等）
    /// </summary>
    public class KlineDataRepositoryFactory
    {
        /// <summary>
        /// 仓库类型枚举
        /// </summary>
        public enum RepositoryType
        {
            PostgreSQL,   // PostgreSQL数据库（默认）
            FileBased,    // 文件系统存储（替代RocksDB）
            RocksDB       // RocksDB（需要RocksDBSharp包）
        }

        private static IKlineDataRepository _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// 获取K线数据仓储实例（单例模式）
        /// </summary>
        /// <param name="type">仓库类型</param>
        /// <param name="connectionString">连接字符串（仅PostgreSQL需要）</param>
        /// <param name="dbPath">数据库路径（仅FileBased和RocksDB需要）</param>
        /// <returns>IKlineDataRepository实例</returns>
        public static IKlineDataRepository GetInstance(
            RepositoryType type = RepositoryType.FileBased,
            string connectionString = null,
            string dbPath = "data/rocksdb")
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        switch (type)
                        {
                            case RepositoryType.PostgreSQL:
                                _instance = new PostgresKlineDataRepository(connectionString);
                                Console.WriteLine("[仓储工厂] 使用PostgreSQL数据库");
                                break;
                            case RepositoryType.RocksDB:
                                try
                                {
                                    _instance = new FileBasedKlineDataRepository(dbPath);
                                    Console.WriteLine("[仓储工厂] 使用RocksDB（模拟实现）");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"[仓储工厂] RocksDB初始化失败，降级到文件系统存储: {ex.Message}");
                                    _instance = new FileBasedKlineDataRepository(dbPath);
                                }
                                break;
                            case RepositoryType.FileBased:
                            default:
                                _instance = new FileBasedKlineDataRepository(dbPath);
                                Console.WriteLine($"[仓储工厂] 使用文件系统存储: {dbPath}");
                                break;
                        }

                        // 确保仓库已初始化
                        if (_instance is IRocksDBKlineDataRepository rocksDBRepo)
                        {
                            rocksDBRepo.Initialize();
                        }
                    }
                }
            }

            return _instance;
        }

        /// <summary>
        /// 重新创建实例（用于切换仓库类型）
        /// </summary>
        /// <param name="type">新的仓库类型</param>
        /// <param name="connectionString">连接字符串</param>
        /// <param name="dbPath">数据库路径</param>
        public static void ResetInstance(
            RepositoryType type = RepositoryType.FileBased,
            string connectionString = null,
            string dbPath = "data/rocksdb")
        {
            lock (_lock)
            {
                if (_instance != null)
                {
                    if (_instance is IRocksDBKlineDataRepository rocksDBRepo)
                    {
                        rocksDBRepo.Close();
                    }
                    _instance = null;
                }

                // 重新创建实例
                _instance = GetInstance(type, connectionString, dbPath);
            }
        }

        /// <summary>
        /// 获取当前使用的仓库类型
        /// </summary>
        public static RepositoryType GetCurrentType()
        {
            if (_instance == null)
                return RepositoryType.FileBased;

            if (_instance is PostgresKlineDataRepository)
                return RepositoryType.PostgreSQL;
            else if (_instance is FileBasedKlineDataRepository)
                return RepositoryType.FileBased;
            else
                return RepositoryType.RocksDB;
        }

        /// <summary>
        /// 检查是否支持特定仓库类型
        /// </summary>
        public static bool IsTypeSupported(RepositoryType type)
        {
            switch (type)
            {
                case RepositoryType.PostgreSQL:
                    return true; // 始终支持PostgreSQL
                case RepositoryType.FileBased:
                    return true; // 始终支持文件系统
                case RepositoryType.RocksDB:
                    try
                    {
                        // 尝试加载RocksDBSharp程序集
                        var rocksDBType = Type.GetType("RocksDbSharp.RocksDB, RocksDbSharp");
                        return rocksDBType != null;
                    }
                    catch
                    {
                        return false;
                    }
                default:
                    return false;
            }
        }

        /// <summary>
        /// 获取支持的仓库类型列表
        /// </summary>
        public static RepositoryType[] GetSupportedTypes()
        {
            var supportedTypes = new System.Collections.Generic.List<RepositoryType>
            {
                RepositoryType.PostgreSQL,
                RepositoryType.FileBased
            };

            if (IsTypeSupported(RepositoryType.RocksDB))
            {
                supportedTypes.Add(RepositoryType.RocksDB);
            }

            return supportedTypes.ToArray();
        }
    }
}
