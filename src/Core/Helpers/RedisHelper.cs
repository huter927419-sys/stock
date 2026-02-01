using System;
using MQReceiver.Configuration;

namespace MQReceiver.Helpers
{
    /// <summary>
    /// Redis 已移除：此类为占位桩，所有方法均为空操作，不再使用 Redis。
    /// 保留相同 API 以避免调用方报错，实际缓存请使用数据库或内存。
    /// </summary>
    public static class RedisHelper
    {
        /// <summary>始终为 false，表示未使用 Redis。</summary>
        public static bool IsEnabled => false;

        /// <summary>始终为 false（兼容旧测试代码）。</summary>
        public static bool IsConnected => false;

        public static void SetConfigurationProvider(IConfigurationProvider provider) { }

        /// <summary>空操作，不再连接 Redis。</summary>
        public static void Initialize() { }

        /// <summary>空操作。</summary>
        public static void Initialize(RedisConfig config) { }

        /// <summary>始终返回 null。</summary>
        public static object GetDatabase() => null;

        /// <summary>空操作，返回 false。</summary>
        public static bool SetCache<T>(string key, T value, TimeSpan? expiry = null) => false;

        /// <summary>始终返回 null（未命中）。</summary>
        public static T GetCache<T>(string key) where T : class => null;

        /// <summary>空操作，返回 false。</summary>
        public static bool DeleteCache(string key) => false;

        /// <summary>空操作。</summary>
        public static void DeleteCacheByPattern(string pattern) { }

        /// <summary>始终返回 false。</summary>
        public static bool Exists(string key) => false;

        /// <summary>始终返回 null。</summary>
        public static TimeSpan? GetExpiry(string key) => null;

        /// <summary>空操作。</summary>
        public static void Close() { }

        /// <summary>空操作。</summary>
        public static void Reset() { }
    }
}
