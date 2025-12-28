using System;
using System.Threading;
using MQReceiver.Configuration;

namespace MQReceiver.Helpers
{
    /// <summary>
    /// 数据库连接字符串辅助类
    /// 统一管理数据库连接配置，支持动态连接池大小
    /// 线程安全：使用Lazy和Interlocked实现无锁线程安全
    /// </summary>
    public static class DatabaseConnectionHelper
    {
        private static IConfigurationProvider _configProvider = AppConfigProvider.Instance;
        private static int _cachedMinPoolSize = -1;  // -1 表示未初始化
        private static int _cachedMaxPoolSize = -1;  // -1 表示未初始化
        private static readonly object _lockObject = new object();

        /// <summary>
        /// 设置配置提供者（用于依赖注入和单元测试）
        /// </summary>
        public static void SetConfigurationProvider(IConfigurationProvider provider)
        {
            lock (_lockObject)
            {
                _configProvider = provider ?? AppConfigProvider.Instance;
                // 重置缓存，以便使用新的配置提供者
                Interlocked.Exchange(ref _cachedMinPoolSize, -1);
                Interlocked.Exchange(ref _cachedMaxPoolSize, -1);
            }
        }

        /// <summary>
        /// 获取最小连接池大小（线程安全）
        /// </summary>
        public static int GetMinPoolSize()
        {
            // 使用Interlocked读取，确保可见性
            int cached = Interlocked.CompareExchange(ref _cachedMinPoolSize, 0, 0);
            if (cached >= 0)
                return cached;

            lock (_lockObject)
            {
                // 双重检查
                cached = Interlocked.CompareExchange(ref _cachedMinPoolSize, 0, 0);
                if (cached >= 0)
                    return cached;

                // 最小连接数 = CPU核心数（确保有足够的连接）
                int minSize = Environment.ProcessorCount;

                // 从配置读取（如果配置了）
                int configValue = _configProvider.GetInt("DatabaseMinPoolSize", 0);
                if (configValue > 0)
                {
                    minSize = configValue;
                }

                int result = Math.Max(1, minSize);
                Interlocked.Exchange(ref _cachedMinPoolSize, result);
                return result;
            }
        }

        /// <summary>
        /// 获取最大连接池大小（线程安全）
        /// </summary>
        public static int GetMaxPoolSize()
        {
            // 使用Interlocked读取，确保可见性
            int cached = Interlocked.CompareExchange(ref _cachedMaxPoolSize, 0, 0);
            if (cached >= 0)
                return cached;

            lock (_lockObject)
            {
                // 双重检查
                cached = Interlocked.CompareExchange(ref _cachedMaxPoolSize, 0, 0);
                if (cached >= 0)
                    return cached;

                // 计算最优并行度
                int optimalParallelism = GetOptimalParallelism();

                // 最大连接数 = 并行度 × 4 + 预留连接（为MQ接收预留）
                // 每个线程最多3个连接，加上缓冲，再为MQ接收预留20个连接
                int reservedForMQ = 20; // 为MQ接收预留的连接数
                int maxSize = Math.Max(optimalParallelism * 4 + reservedForMQ, GetMinPoolSize() * 2 + reservedForMQ);

                // 从配置读取（如果配置了）
                int configValue = _configProvider.GetInt("DatabaseMaxPoolSize", 0);
                if (configValue > 0)
                {
                    maxSize = configValue;
                }

                // 确保最大连接数不超过合理范围（1-500）
                int result = Math.Max(GetMinPoolSize(), Math.Min(maxSize, 500));
                Interlocked.Exchange(ref _cachedMaxPoolSize, result);
                return result;
            }
        }

        /// <summary>
        /// 获取最优并行度（用于并行处理）
        /// </summary>
        public static int GetOptimalParallelism()
        {
            int cpuCores = Environment.ProcessorCount;
            int baseMaxConnections = 100; // 基础最大连接数

                // 并行度 = min(CPU核心数, 基础连接池大小 / 3)
                // 降低并行度，减少连接占用，避免与MQ接收竞争
                // 每个线程可能需要3个连接（周/月/季KD查询）
                int optimal = Math.Min(cpuCores, baseMaxConnections / 3);

                // 确保至少为1，最多不超过CPU核心数的2倍（降低上限）
                return Math.Max(1, Math.Min(optimal, cpuCores * 2));
        }

        /// <summary>
        /// 生成数据库连接字符串
        /// </summary>
        public static string BuildConnectionString()
        {
            string host = _configProvider.GetString("DatabaseHost", "localhost");
            int port = _configProvider.GetInt("DatabasePort", 8532);
            string database = _configProvider.GetString("DatabaseName", "stockdb");
            string username = _configProvider.GetString("DatabaseUser", "postgres");
            string password = _configProvider.GetString("DatabasePassword", "");

            int minPoolSize = GetMinPoolSize();
            int maxPoolSize = GetMaxPoolSize();

            return string.Format(
                "Host={0};Port={1};Database={2};Username={3};Password={4};" +
                "Pooling=true;MinPoolSize={5};MaxPoolSize={6};Timeout=30;CommandTimeout=30;",
                host, port, database, username, password, minPoolSize, maxPoolSize);
        }

        /// <summary>
        /// 使用 DatabaseConfig 生成连接字符串
        /// </summary>
        public static string BuildConnectionString(DatabaseConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            return string.Format(
                "Host={0};Port={1};Database={2};Username={3};Password={4};" +
                "Pooling=true;MinPoolSize={5};MaxPoolSize={6};Timeout=30;CommandTimeout={7};",
                config.Host, config.Port, config.Database, config.Username, config.Password,
                config.MinPoolSize, config.MaxPoolSize, config.CommandTimeout);
        }

        /// <summary>
        /// 重置缓存（用于动态调整，线程安全）
        /// </summary>
        public static void ResetCache()
        {
            // 使用Interlocked确保原子性
            Interlocked.Exchange(ref _cachedMinPoolSize, -1);
            Interlocked.Exchange(ref _cachedMaxPoolSize, -1);
        }

        /// <summary>
        /// 获取连接池配置信息（用于日志）
        /// </summary>
        public static string GetPoolInfo()
        {
            return string.Format("连接池配置: Min={0}, Max={1}, 最优并行度={2}, CPU核心数={3}",
                GetMinPoolSize(), GetMaxPoolSize(), GetOptimalParallelism(), Environment.ProcessorCount);
        }
    }
}
