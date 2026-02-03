using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MQReceiver.Cache;
using MQReceiver.Models;
using MQReceiver.Services;

namespace MQReceiver.Services
{
    /// <summary>
    /// 图表数据服务（独立模块）
    /// 负责预加载和缓存图表数据，与窗口显示分离
    /// </summary>
    public class ChartDataService
    {
        private readonly ChartService _chartService;
        
        // 图表数据缓存（股票代码 -> ChartData）
        private static readonly Dictionary<string, (ChartData data, DateTime cachedAt)> _dataCache = 
            new Dictionary<string, (ChartData, DateTime)>();
        private static readonly object _cacheLock = new object();
        private const int CacheTTLSeconds = 30; // 缓存30秒
        
        // 正在加载的股票代码集合（避免重复加载）
        private static readonly HashSet<string> _loadingSet = new HashSet<string>();
        private static readonly object _loadingLock = new object();
        
        public ChartDataService(RealTimeDataCache realTimeCache)
        {
            _chartService = new ChartService(realTimeCache);
        }
        
        /// <summary>
        /// 异步预加载图表数据（后台执行，不阻塞UI）
        /// </summary>
        public async Task<ChartData> LoadChartDataAsync(string stockCode)
        {
            if (string.IsNullOrEmpty(stockCode))
                return null;
            
            // 检查缓存
            lock (_cacheLock)
            {
                if (_dataCache.TryGetValue(stockCode, out var cached))
                {
                    var age = (DateTime.Now - cached.cachedAt).TotalSeconds;
                    if (age < CacheTTLSeconds)
                    {
                        Console.WriteLine($"[ChartDataService] 使用缓存数据: {stockCode}（{age:F0}秒前）");
                        return cached.data;
                    }
                }
            }
            
            // 检查是否正在加载
            lock (_loadingLock)
            {
                if (_loadingSet.Contains(stockCode))
                {
                    Console.WriteLine($"[ChartDataService] {stockCode} 正在加载中，等待完成...");
                    // 等待加载完成（最多等待5秒）
                    int waitCount = 0;
                    while (_loadingSet.Contains(stockCode) && waitCount < 50)
                    {
                        System.Threading.Thread.Sleep(100);
                        waitCount++;
                        
                        // 再次检查缓存
                        lock (_cacheLock)
                        {
                            if (_dataCache.TryGetValue(stockCode, out var cached))
                            {
                                return cached.data;
                            }
                        }
                    }
                }
                _loadingSet.Add(stockCode);
            }
            
            try
            {
                // 在后台线程加载数据
                var chartData = await Task.Run(() =>
                {
                    Console.WriteLine($"[ChartDataService] 开始加载图表数据: {stockCode}");
                    var data = _chartService.LoadChartData(stockCode, 0);
                    Console.WriteLine($"[ChartDataService] 图表数据加载完成: {stockCode}");
                    return data;
                });
                
                // 更新缓存
                if (chartData != null)
                {
                    lock (_cacheLock)
                    {
                        _dataCache[stockCode] = (chartData, DateTime.Now);
                        
                        // 清理过期缓存（保留最近50个）
                        if (_dataCache.Count > 50)
                        {
                            var expired = _dataCache
                                .Where(kv => (DateTime.Now - kv.Value.cachedAt).TotalSeconds > CacheTTLSeconds * 2)
                                .Select(kv => kv.Key)
                                .ToList();
                            foreach (var key in expired)
                            {
                                _dataCache.Remove(key);
                            }
                        }
                    }
                }
                
                return chartData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChartDataService] 加载图表数据失败 {stockCode}: {ex.Message}");
                return null;
            }
            finally
            {
                lock (_loadingLock)
                {
                    _loadingSet.Remove(stockCode);
                }
            }
        }
        
        /// <summary>
        /// 预加载多个股票的图表数据（并行）
        /// </summary>
        public async Task PreloadChartDataAsync(List<string> stockCodes)
        {
            if (stockCodes == null || stockCodes.Count == 0)
                return;
            
            Console.WriteLine($"[ChartDataService] 开始预加载 {stockCodes.Count} 个股票的图表数据");
            
            var tasks = stockCodes.Select(code => LoadChartDataAsync(code)).ToArray();
            await Task.WhenAll(tasks);
            
            Console.WriteLine($"[ChartDataService] 预加载完成");
        }
        
        /// <summary>
        /// 清除缓存
        /// </summary>
        public static void ClearCache()
        {
            lock (_cacheLock)
            {
                _dataCache.Clear();
            }
        }
        
        /// <summary>
        /// 清除指定股票的缓存
        /// </summary>
        public static void ClearCache(string stockCode)
        {
            lock (_cacheLock)
            {
                _dataCache.Remove(stockCode);
            }
        }
    }
}
