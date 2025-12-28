using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using StackExchange.Redis;
using MQReceiver.Configuration;

namespace MQReceiver.Helpers
{
    /// <summary>
    /// Redis缓存辅助类
    /// 用于缓存KD计算结果，减少数据库查询
    /// 线程安全：所有公共方法都是线程安全的
    /// </summary>
    public static class RedisHelper
    {
        private static ConnectionMultiplexer _redis;
        private static IDatabase _database;
        private static readonly object _lockObject = new object();
        private static volatile bool _isEnabled = true;
        private static volatile bool _isInitialized = false;
        private static volatile bool _disposed = false;
        private static IConfigurationProvider _configProvider = AppConfigProvider.Instance;

        /// <summary>
        /// Redis是否可用
        /// </summary>
        public static bool IsEnabled
        {
            get
            {
                // 获取本地引用，避免在检查过程中被其他线程修改
                var db = _database;
                return _isEnabled && _isInitialized && db != null;
            }
        }

        /// <summary>
        /// 设置配置提供者（用于依赖注入和单元测试）
        /// </summary>
        public static void SetConfigurationProvider(IConfigurationProvider provider)
        {
            _configProvider = provider ?? AppConfigProvider.Instance;
        }

        /// <summary>
        /// 初始化Redis连接（线程安全）
        /// </summary>
        public static void Initialize()
        {
            // 快速检查：如果已初始化，直接返回
            if (_isInitialized)
                return;

            lock (_lockObject)
            {
                // 双重检查：在锁内再次检查
                if (_isInitialized)
                    return;

                try
                {
                    var config = _configProvider.GetRedisConfig();

                    var configurationOptions = new ConfigurationOptions
                    {
                        EndPoints = { { config.Host, config.Port } },
                        AbortOnConnectFail = false,
                        ConnectRetry = 3,
                        ConnectTimeout = config.ConnectTimeout,
                        SyncTimeout = 5000,
                        DefaultDatabase = config.Database
                    };

                    if (!string.IsNullOrEmpty(config.Password))
                    {
                        configurationOptions.Password = config.Password;
                    }

                    var redis = ConnectionMultiplexer.Connect(configurationOptions);
                    var database = redis.GetDatabase();

                    // 测试连接
                    database.StringSet("test", "1", TimeSpan.FromSeconds(1));
                    database.StringGet("test");

                    // 先设置字段，再标记初始化完成（顺序很重要）
                    _redis = redis;
                    _database = database;
                    _isEnabled = true;
                    Thread.MemoryBarrier(); // 确保写入顺序
                    _isInitialized = true;

                    Console.WriteLine($"Redis连接成功: {config.Host}:{config.Port}, Database={config.Database}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Redis连接失败: {ex.Message}，将使用数据库直接查询");
                    _isEnabled = false;
                    _database = null;
                    _isInitialized = true; // 标记已尝试初始化，避免重复尝试
                }
            }
        }

        /// <summary>
        /// 使用指定配置初始化Redis连接（线程安全）
        /// </summary>
        public static void Initialize(RedisConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            // 快速检查：如果已初始化，直接返回
            if (_isInitialized)
                return;

            lock (_lockObject)
            {
                // 双重检查：在锁内再次检查
                if (_isInitialized)
                    return;

                try
                {
                    var configurationOptions = new ConfigurationOptions
                    {
                        EndPoints = { { config.Host, config.Port } },
                        AbortOnConnectFail = false,
                        ConnectRetry = 3,
                        ConnectTimeout = config.ConnectTimeout,
                        SyncTimeout = 5000,
                        DefaultDatabase = config.Database
                    };

                    if (!string.IsNullOrEmpty(config.Password))
                    {
                        configurationOptions.Password = config.Password;
                    }

                    var redis = ConnectionMultiplexer.Connect(configurationOptions);
                    var database = redis.GetDatabase();

                    // 测试连接
                    database.StringSet("test", "1", TimeSpan.FromSeconds(1));
                    database.StringGet("test");

                    // 先设置字段，再标记初始化完成
                    _redis = redis;
                    _database = database;
                    _isEnabled = config.Enabled;
                    Thread.MemoryBarrier();
                    _isInitialized = true;

                    Console.WriteLine($"Redis连接成功: {config.Host}:{config.Port}, Database={config.Database}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Redis连接失败: {ex.Message}，将使用数据库直接查询");
                    _isEnabled = false;
                    _database = null;
                    _isInitialized = true;
                }
            }
        }

        /// <summary>
        /// 获取Redis数据库实例（线程安全）
        /// </summary>
        public static IDatabase GetDatabase()
        {
            // 获取本地引用
            var db = _database;
            if (!_isEnabled || !_isInitialized || db == null)
                return null;
            return db;
        }

        /// <summary>
        /// 设置缓存（带过期时间，线程安全）
        /// </summary>
        public static bool SetCache<T>(string key, T value, TimeSpan? expiry = null)
        {
            // 获取本地引用，避免在操作过程中被Close()方法清空
            var db = _database;
            if (!_isEnabled || !_isInitialized || db == null)
                return false;

            try
            {
                string json = JsonConvert.SerializeObject(value);
                return db.StringSet(key, json, expiry);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Redis设置缓存失败: {key}, {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取缓存（线程安全）
        /// </summary>
        public static T GetCache<T>(string key) where T : class
        {
            // 获取本地引用
            var db = _database;
            if (!_isEnabled || !_isInitialized || db == null)
                return null;

            try
            {
                string json = db.StringGet(key);
                if (string.IsNullOrEmpty(json))
                    return null;

                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Redis获取缓存失败: {key}, {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 删除缓存（线程安全）
        /// </summary>
        public static bool DeleteCache(string key)
        {
            // 获取本地引用
            var db = _database;
            if (!_isEnabled || !_isInitialized || db == null)
                return false;

            try
            {
                return db.KeyDelete(key);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Redis删除缓存失败: {key}, {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 批量删除缓存（使用模式匹配，线程安全）
        /// </summary>
        public static void DeleteCacheByPattern(string pattern)
        {
            // 获取本地引用
            var redis = _redis;
            var db = _database;
            if (!_isEnabled || !_isInitialized || redis == null || db == null)
                return;

            try
            {
                var server = redis.GetServer(redis.GetEndPoints()[0]);
                var keys = server.Keys(pattern: pattern);

                foreach (var key in keys)
                {
                    db.KeyDelete(key);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Redis批量删除缓存失败: {pattern}, {ex.Message}");
            }
        }

        /// <summary>
        /// 检查缓存是否存在（线程安全）
        /// </summary>
        public static bool Exists(string key)
        {
            // 获取本地引用
            var db = _database;
            if (!_isEnabled || !_isInitialized || db == null)
                return false;

            try
            {
                return db.KeyExists(key);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Redis检查缓存失败: {key}, {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取缓存过期时间（线程安全）
        /// </summary>
        public static TimeSpan? GetExpiry(string key)
        {
            // 获取本地引用
            var db = _database;
            if (!_isEnabled || !_isInitialized || db == null)
                return null;

            try
            {
                return db.KeyTimeToLive(key);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Redis获取过期时间失败: {key}, {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 关闭并释放Redis连接（线程安全）
        /// </summary>
        public static void Close()
        {
            lock (_lockObject)
            {
                CloseInternal();
            }
        }

        /// <summary>
        /// 重置Redis连接（用于重新初始化，线程安全）
        /// </summary>
        public static void Reset()
        {
            lock (_lockObject)
            {
                // 先关闭现有连接
                CloseInternal();
                // 重置状态，允许重新初始化
                _disposed = false;
                _isInitialized = false;
            }
        }

        /// <summary>
        /// 内部关闭方法（调用者需持有锁）
        /// </summary>
        private static void CloseInternal()
        {
            if (_disposed)
                return;

            try
            {
                var redis = _redis;
                if (redis != null)
                {
                    if (redis.IsConnected)
                    {
                        redis.Close();
                    }
                    redis.Dispose();
                }
                _redis = null;
                _database = null;
                _isEnabled = false;
                _disposed = true;
                Console.WriteLine("Redis连接已关闭并释放资源");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"关闭Redis连接时出错: {ex.Message}");
            }
        }
    }
}
