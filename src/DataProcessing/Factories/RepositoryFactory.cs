using System;
using MQReceiver.Helpers;
using MQReceiver.Repositories;
using MQReceiver.DataProcessing.Repositories;

namespace MQReceiver.DataProcessing.Factories
{
    /// <summary>
    /// 数据仓储工厂
    /// 用于统一管理和创建不同类型的数据仓储（PostgreSQL、RocksDB等）
    /// </summary>
    public class RepositoryFactory
    {
        /// <summary>
        /// 存储后端类型
        /// </summary>
        public enum StorageBackend
        {
            PostgreSQL,   // PostgreSQL 数据库
            RocksDB,      // RocksDB 文件系统存储
            FileBased     // 纯文件系统存储（与RocksDB相同）
        }

        private static StorageBackend _currentBackend = StorageBackend.PostgreSQL;
        private static string _connectionString;
        private static string _dbPath = "data/rocksdb";
        private static readonly object _lock = new object();

        // 单例实例
        private static IStockDataRepository _stockDataRepository;
        private static IExRightsDataRepository _exRightsDataRepository;
        private static IRealTimeDataRepository _realTimeDataRepository;

        /// <summary>
        /// 配置存储后端
        /// </summary>
        /// <param name="backend">存储后端类型</param>
        /// <param name="connectionString">PostgreSQL连接字符串（仅PostgreSQL需要）</param>
        /// <param name="dbPath">数据库路径（仅RocksDB和FileBased需要）</param>
        public static void Configure(StorageBackend backend, string connectionString = null, string dbPath = "data/rocksdb")
        {
            lock (_lock)
            {
                _currentBackend = backend;
                _connectionString = connectionString;
                _dbPath = dbPath;

                // 清除现有实例
                _stockDataRepository = null;
                _exRightsDataRepository = null;
                _realTimeDataRepository = null;

                Console.WriteLine($"[RepositoryFactory] 已配置存储后端: {backend}");
            }
        }

        /// <summary>
        /// 获取股票数据仓储
        /// </summary>
        public static IStockDataRepository GetStockDataRepository()
        {
            if (_stockDataRepository == null)
            {
                lock (_lock)
                {
                    if (_stockDataRepository == null)
                    {
                        _stockDataRepository = CreateStockDataRepository();
                    }
                }
            }
            return _stockDataRepository;
        }

        /// <summary>
        /// 获取除权数据仓储
        /// </summary>
        public static IExRightsDataRepository GetExRightsDataRepository()
        {
            if (_exRightsDataRepository == null)
            {
                lock (_lock)
                {
                    if (_exRightsDataRepository == null)
                    {
                        _exRightsDataRepository = CreateExRightsDataRepository();
                    }
                }
            }
            return _exRightsDataRepository;
        }

        /// <summary>
        /// 获取实时数据仓储
        /// </summary>
        public static IRealTimeDataRepository GetRealTimeDataRepository()
        {
            if (_realTimeDataRepository == null)
            {
                lock (_lock)
                {
                    if (_realTimeDataRepository == null)
                    {
                        _realTimeDataRepository = CreateRealTimeDataRepository();
                    }
                }
            }
            return _realTimeDataRepository;
        }

        /// <summary>
        /// 获取 K 线数据仓储（与股票数据仓储同一实例，因 Postgres/RocksDB 均实现 IKlineDataRepository）
        /// </summary>
        public static IKlineDataRepository GetKlineDataRepository()
        {
            return (IKlineDataRepository)GetStockDataRepository();
        }

        /// <summary>
        /// 获取当前存储后端类型
        /// </summary>
        public static StorageBackend GetCurrentBackend()
        {
            return _currentBackend;
        }

        /// <summary>
        /// 重置所有仓储实例（用于切换后端）
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                _stockDataRepository = null;
                _exRightsDataRepository = null;
                _realTimeDataRepository = null;
            }
        }

        #region 私有方法

        private static IStockDataRepository CreateStockDataRepository()
        {
            switch (_currentBackend)
            {
                case StorageBackend.PostgreSQL:
                    Console.WriteLine("[RepositoryFactory] 创建 PostgreSQL StockDataRepository");
                    return new PostgresStockDataRepository(_connectionString ?? DatabaseConnectionHelper.BuildConnectionString());
                case StorageBackend.RocksDB:
                case StorageBackend.FileBased:
                default:
                    Console.WriteLine("[RepositoryFactory] 创建 RocksDB StockDataRepository: " + _dbPath);
                    return new RocksDBStockDataRepository(_dbPath);
            }
        }

        private static IExRightsDataRepository CreateExRightsDataRepository()
        {
            switch (_currentBackend)
            {
                case StorageBackend.PostgreSQL:
                    Console.WriteLine("[RepositoryFactory] 创建 PostgreSQL ExRightsDataRepository");
                    return new PostgresExRightsDataRepository(_connectionString ?? DatabaseConnectionHelper.BuildConnectionString());
                case StorageBackend.RocksDB:
                case StorageBackend.FileBased:
                default:
                    Console.WriteLine("[RepositoryFactory] 创建 RocksDB ExRightsDataRepository: " + _dbPath);
                    return new RocksDBExRightsDataRepository(_dbPath);
            }
        }

        private static IRealTimeDataRepository CreateRealTimeDataRepository()
        {
            switch (_currentBackend)
            {
                case StorageBackend.PostgreSQL:
                    Console.WriteLine("[RepositoryFactory] 创建 PostgreSQL RealTimeDataRepository");
                    return new PostgresRealTimeDataRepository(_connectionString ?? DatabaseConnectionHelper.BuildConnectionString());
                case StorageBackend.RocksDB:
                case StorageBackend.FileBased:
                default:
                    Console.WriteLine("[RepositoryFactory] 创建 RocksDB RealTimeDataRepository: " + _dbPath);
                    return new RocksDBRealTimeDataRepository(_dbPath);
            }
        }

        #endregion
    }
}
