using System;
using System.Collections.Generic;
using MQReceiver.Cache;

namespace MQReceiver.Helpers
{
    /// <summary>
    /// 缓存管理器
    /// 统一管理RealTimeDataCache的创建和生命周期
    /// 支持缓存复用和资源释放
    /// </summary>
    public static class CacheManager
    {
        // 全局共享缓存（用于集成模式）
        private static RealTimeDataCache _sharedCache;
        private static readonly object _sharedCacheLock = new object();

        // 独立缓存列表（用于跟踪独立模式创建的缓存，便于清理）
        private static readonly List<RealTimeDataCache> _standaloneCaches = new List<RealTimeDataCache>();
        private static readonly object _standaloneCachesLock = new object();

        /// <summary>
        /// 获取或创建共享缓存（单例模式）
        /// </summary>
        public static RealTimeDataCache GetOrCreateSharedCache()
        {
            if (_sharedCache == null)
            {
                lock (_sharedCacheLock)
                {
                    if (_sharedCache == null)
                    {
                        _sharedCache = new RealTimeDataCache();
                        System.Diagnostics.Debug.WriteLine("[CacheManager] 创建共享缓存");
                    }
                }
            }
            return _sharedCache;
        }

        /// <summary>
        /// 创建独立缓存（用于独立模式）
        /// </summary>
        public static RealTimeDataCache CreateStandaloneCache()
        {
            var cache = new RealTimeDataCache();
            lock (_standaloneCachesLock)
            {
                _standaloneCaches.Add(cache);
            }
            System.Diagnostics.Debug.WriteLine("[CacheManager] 创建独立缓存");
            return cache;
        }

        /// <summary>
        /// 释放独立缓存
        /// </summary>
        public static void ReleaseStandaloneCache(RealTimeDataCache cache)
        {
            if (cache == null) return;

            try
            {
                cache.Dispose();
                lock (_standaloneCachesLock)
                {
                    _standaloneCaches.Remove(cache);
                }
                System.Diagnostics.Debug.WriteLine("[CacheManager] 释放独立缓存");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CacheManager] 释放缓存失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 释放所有独立缓存
        /// </summary>
        public static void ReleaseAllStandaloneCaches()
        {
            lock (_standaloneCachesLock)
            {
                foreach (var cache in _standaloneCaches)
                {
                    try
                    {
                        cache?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CacheManager] 释放缓存失败: {ex.Message}");
                    }
                }
                _standaloneCaches.Clear();
            }
        }

        /// <summary>
        /// 释放共享缓存
        /// </summary>
        public static void ReleaseSharedCache()
        {
            lock (_sharedCacheLock)
            {
                if (_sharedCache != null)
                {
                    try
                    {
                        _sharedCache.Dispose();
                        System.Diagnostics.Debug.WriteLine("[CacheManager] 释放共享缓存");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CacheManager] 释放共享缓存失败: {ex.Message}");
                    }
                    finally
                    {
                        _sharedCache = null;
                    }
                }
            }
        }
    }
}
