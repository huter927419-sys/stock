using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MQReceiver.Models;

namespace MQReceiver.Cache
{
    /// <summary>
    /// 实时数据内存缓存管理器
    /// 用于在内存中存储实时数据，不保存到数据库
    /// 使用ConcurrentDictionary实现无锁并发访问
    /// </summary>
    public class RealTimeDataCache : IDisposable
    {
        private readonly ConcurrentDictionary<string, RealTimeDataRecord> _cache;
        private long _lastUpdateTimeTicks;
        private bool _disposed = false;

        // 最大缓存容量（防止内存溢出）
        private const int MAX_CACHE_SIZE = 10000;

        /// <summary>
        /// 数据更新事件（当有数据推送过来时触发）
        /// 参数：更新的股票代码列表
        /// </summary>
        public event EventHandler<List<string>> DataUpdated;

        public RealTimeDataCache()
        {
            _cache = new ConcurrentDictionary<string, RealTimeDataRecord>();
            _lastUpdateTimeTicks = DateTime.MinValue.Ticks;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _cache.Clear();
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// 更新实时数据（批量）- 无锁实现
        /// </summary>
        public void UpdateData(List<RealTimeDataRecord> records)
        {
            if (records == null || records.Count == 0)
                return;

            var updatedStockCodes = new List<string>();
            foreach (var record in records)
            {
                if (!string.IsNullOrEmpty(record.StockCode))
                {
                    // 检查容量限制
                    if (_cache.Count >= MAX_CACHE_SIZE && !_cache.ContainsKey(record.StockCode))
                    {
                        continue; // 跳过新股票，保护内存
                    }
                    _cache[record.StockCode] = record;
                    updatedStockCodes.Add(record.StockCode);
                }
            }
            Interlocked.Exchange(ref _lastUpdateTimeTicks, DateTime.Now.Ticks);
            
            // 触发数据更新事件
            if (updatedStockCodes.Count > 0)
            {
                DataUpdated?.Invoke(this, updatedStockCodes);
            }
        }

        /// <summary>
        /// 更新单条实时数据 - 无锁实现
        /// </summary>
        public void UpdateData(RealTimeDataRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.StockCode))
                return;

            // 检查容量限制
            if (_cache.Count >= MAX_CACHE_SIZE && !_cache.ContainsKey(record.StockCode))
            {
                return;
            }

            _cache[record.StockCode] = record;
            Interlocked.Exchange(ref _lastUpdateTimeTicks, DateTime.Now.Ticks);
            
            // 触发数据更新事件
            DataUpdated?.Invoke(this, new List<string> { record.StockCode });
        }

        /// <summary>
        /// 获取指定股票的实时数据 - 无锁实现
        /// </summary>
        public RealTimeDataRecord GetData(string stockCode)
        {
            if (string.IsNullOrEmpty(stockCode))
                return null;

            _cache.TryGetValue(stockCode, out var record);
            return record;
        }

        /// <summary>
        /// 获取所有股票的实时数据
        /// 注意：返回的是快照，遍历时不会阻塞写入
        /// </summary>
        public List<RealTimeDataRecord> GetAllData()
        {
            return _cache.Values.ToList();
        }

        /// <summary>
        /// 获取所有股票代码列表
        /// </summary>
        public List<string> GetAllStockCodes()
        {
            return _cache.Keys.ToList();
        }

        /// <summary>
        /// 获取缓存中的股票数量 - 无锁实现
        /// </summary>
        public int Count => _cache.Count;

        /// <summary>
        /// 获取最后更新时间
        /// </summary>
        public DateTime LastUpdateTime => new DateTime(Interlocked.Read(ref _lastUpdateTimeTicks));

        /// <summary>
        /// 清空缓存
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
            Interlocked.Exchange(ref _lastUpdateTimeTicks, DateTime.MinValue.Ticks);
        }

        /// <summary>
        /// 检查数据是否过期（超过指定时间未更新）
        /// </summary>
        public bool IsExpired(TimeSpan maxAge)
        {
            var lastUpdate = new DateTime(Interlocked.Read(ref _lastUpdateTimeTicks));
            if (lastUpdate == DateTime.MinValue)
                return true;
            return DateTime.Now - lastUpdate > maxAge;
        }

        /// <summary>
        /// 尝试获取并更新股票数据（原子操作）
        /// </summary>
        public RealTimeDataRecord GetOrAdd(string stockCode, Func<string, RealTimeDataRecord> valueFactory)
        {
            if (string.IsNullOrEmpty(stockCode))
                return null;

            return _cache.GetOrAdd(stockCode, valueFactory);
        }

        /// <summary>
        /// 批量获取多只股票数据
        /// </summary>
        public Dictionary<string, RealTimeDataRecord> GetDataBatch(IEnumerable<string> stockCodes)
        {
            var result = new Dictionary<string, RealTimeDataRecord>();
            foreach (var code in stockCodes)
            {
                if (_cache.TryGetValue(code, out var record))
                {
                    result[code] = record;
                }
            }
            return result;
        }
    }
}
