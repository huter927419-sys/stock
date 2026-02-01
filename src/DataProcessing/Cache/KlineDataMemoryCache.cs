using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MQReceiver.Calculators;
using MQReceiver.Configuration;
using MQReceiver.Models;
using MQReceiver.Repositories;
using MQReceiver.Helpers;
using MQReceiver.DataProcessing.Factories;

namespace MQReceiver.Cache
{
    /// <summary>
    /// K线数据内存缓存
    /// 将所有K线数据加载到内存中，避免频繁的数据库查询
    /// </summary>
    public class KlineDataMemoryCache
    {
        // 股票代码 -> 日期 -> K线数据
        private readonly ConcurrentDictionary<string, SortedDictionary<DateTime, DailyDataRecord>> _dailyKlineCache;
        
        // 前复权计算器
        private readonly ExRightsAdjustmentCalculator _exRightsCalculator;
        
        // 数据仓库（当前配置的存储后端）
        private readonly IKlineDataRepository _klineRepository;
        private readonly IExRightsDataRepository _exRightsRepository;
        
        public KlineDataMemoryCache()
        {
            _dailyKlineCache = new ConcurrentDictionary<string, SortedDictionary<DateTime, DailyDataRecord>>();
            _klineRepository = RepositoryFactory.GetKlineDataRepository();
            _exRightsRepository = RepositoryFactory.GetExRightsDataRepository();
            _exRightsCalculator = new ExRightsAdjustmentCalculator(_exRightsRepository, _klineRepository);
        }
        
        /// <summary>
        /// 预加载所有K线数据到内存
        /// </summary>
        public void PreloadAllData(List<string> stockCodes)
        {
            var stopwatch = Stopwatch.StartNew();
            int preloadYearsConfig = AppConfigProvider.Instance.GetInt("FilterService_PreloadYears", 0);
            string scopeDesc = preloadYearsConfig <= 0 ? "全部历史数据" : $"最近 {preloadYearsConfig} 年";
            Console.WriteLine($"[内存缓存] 开始预加载 {stockCodes.Count} 只股票的K线数据（{scopeDesc}）...");
            
            int loadedCount = 0;
            int failedCount = 0;
            var lockObj = new object();
            var config = AppConfigProvider.Instance;
            int parallelism = config.GetInt("FilterService_PreloadMaxParallelism", 6);
            if (parallelism <= 0)
                parallelism = Math.Min(Environment.ProcessorCount, 8);
            parallelism = Math.Max(2, Math.Min(parallelism, Environment.ProcessorCount));

            Parallel.ForEach(stockCodes, new ParallelOptions { MaxDegreeOfParallelism = parallelism }, stockCode =>
            {
                try
                {
                    Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
                    // 0=全部历史数据，>0=最近 N 年；全部数据时季K 最完整、筛选结果最全
                    int preloadYears = AppConfigProvider.Instance.GetInt("FilterService_PreloadYears", 0);
                    DateTime startDate;
                    if (preloadYears <= 0)
                        startDate = DateTime.MinValue;
                    else
                    {
                        if (preloadYears > 20) preloadYears = 20;
                        startDate = DateTime.Today.AddYears(-preloadYears);
                    }
                    var dailyData = _klineRepository.GetDailyData(stockCode, startDate, DateTime.Today);
                    
                    if (dailyData != null && dailyData.Count > 0)
                    {
                        var sortedData = new SortedDictionary<DateTime, DailyDataRecord>();
                        foreach (var klineData in dailyData)
                        {
                            // 将 DailyKlineData 转换为 DailyDataRecord
                            var record = new DailyDataRecord
                            {
                                StockCode = stockCode,
                                TradeDate = klineData.TradeDate,
                                OpenPrice = klineData.Open,
                                HighPrice = klineData.High,
                                LowPrice = klineData.Low,
                                ClosePrice = klineData.Close,
                                Volume = klineData.Volume
                            };
                            sortedData[record.TradeDate] = record;
                        }
                        
                        _dailyKlineCache[stockCode] = sortedData;
                        
                        int current = Interlocked.Increment(ref loadedCount);
                        if (current % 500 == 0)
                        {
                            lock (lockObj)
                            {
                                Console.WriteLine($"[内存缓存] 已加载 {current}/{stockCodes.Count} 只股票...");
                            }
                        }
                        if (current % 200 == 0)
                            Thread.Sleep(0);
                    }
                }
                catch (Exception ex)
                {
                    lock (lockObj)
                    {
                        failedCount++;
                        if (failedCount <= 10)
                        {
                            Console.WriteLine($"[内存缓存] 加载 {stockCode} 失败: {ex.Message}");
                        }
                    }
                }
                finally
                {
                    try { Thread.CurrentThread.Priority = ThreadPriority.Normal; } catch { }
                }
            });
            
            stopwatch.Stop();
            Console.WriteLine($"[内存缓存] 预加载完成！");
            Console.WriteLine($"  成功: {loadedCount} 只");
            Console.WriteLine($"  失败: {failedCount} 只");
            Console.WriteLine($"  耗时: {stopwatch.Elapsed.TotalSeconds:F1} 秒");
            Console.WriteLine($"  内存占用: ~{(loadedCount * 500 * 50) / 1024 / 1024}MB");
        }
        
        /// <summary>
        /// 获取指定股票在缓存中的日期范围（供适配器/批量KD使用）
        /// </summary>
        public (DateTime? StartDate, DateTime? EndDate) GetDataDateRange(string stockCode)
        {
            if (!_dailyKlineCache.TryGetValue(stockCode, out var data) || data == null || data.Count == 0)
                return (null, null);
            return (data.Keys.Min(), data.Keys.Max());
        }

        /// <summary>
        /// 获取指定股票的日线数据
        /// </summary>
        public List<DailyDataRecord> GetDailyData(string stockCode, DateTime startDate, DateTime endDate)
        {
            if (!_dailyKlineCache.TryGetValue(stockCode, out var data))
            {
                // 缓存未命中，从数据库读取并转换
                var dailyData = _klineRepository.GetDailyData(stockCode, startDate, endDate);
                if (dailyData == null || dailyData.Count == 0)
                    return new List<DailyDataRecord>();
                
                return dailyData.Select(klineData => new DailyDataRecord
                {
                    StockCode = stockCode,
                    TradeDate = klineData.TradeDate,
                    OpenPrice = klineData.Open,
                    HighPrice = klineData.High,
                    LowPrice = klineData.Low,
                    ClosePrice = klineData.Close,
                    Volume = klineData.Volume
                }).ToList();
            }
            
            // 从缓存中计算日期范围
            return data.Where(kv => kv.Key >= startDate && kv.Key <= endDate)
                      .Select(kv => kv.Value)
                      .ToList();
        }
        
        /// <summary>
        /// 获取聚合数据（周/月/季）
        /// </summary>
        public List<AggregatedCandle> GetAggregatedData(string stockCode, DateTime targetDate, string cycleType)
        {
            // 获取日线数据
            DateTime startDate = GetStartDate(targetDate, cycleType);
            var dailyData = GetDailyData(stockCode, startDate, targetDate);
            
            if (dailyData == null || dailyData.Count == 0)
                return new List<AggregatedCandle>();
            
            // 聚合数据
            return AggregateData(dailyData, cycleType);
        }
        
        /// <summary>
        /// 根据周期类型获取起始日期
        /// </summary>
        private DateTime GetStartDate(DateTime targetDate, string cycleType)
        {
            switch (cycleType.ToLower())
            {
                case "week":
                    return targetDate.AddDays(-365); // 1年周线数据
                case "month":
                    return targetDate.AddDays(-730); // 2年月线数据
                case "quarter":
                    return targetDate.AddDays(-1095); // 3年季线数据
                default:
                    return targetDate.AddDays(-365);
            }
        }
        
        /// <summary>
        /// 聚合日线数据为周/月/季线
        /// </summary>
        private List<AggregatedCandle> AggregateData(List<DailyDataRecord> dailyData, string cycleType)
        {
            var result = new List<AggregatedCandle>();
            
            if (dailyData.Count == 0)
                return result;
            
            // 按周期分组
            var grouped = GroupByCycle(dailyData, cycleType);
            
            // 聚合每个周期的数据
            foreach (var group in grouped)
            {
                if (group.Value.Count == 0)
                    continue;
                
                var candle = new AggregatedCandle
                {
                    Date = group.Value.Last().TradeDate, // 使用周期内最后一天的日期
                    Open = group.Value.First().OpenPrice,
                    High = group.Value.Max(d => d.HighPrice),
                    Low = group.Value.Min(d => d.LowPrice),
                    Close = group.Value.Last().ClosePrice,
                    Volume = group.Value.Sum(d => d.Volume)
                };
                
                result.Add(candle);
            }
            
            return result.OrderBy(c => c.Date).ToList();
        }
        
        /// <summary>
        /// 按周期分组
        /// </summary>
        private Dictionary<string, List<DailyDataRecord>> GroupByCycle(List<DailyDataRecord> dailyData, string cycleType)
        {
            var groups = new Dictionary<string, List<DailyDataRecord>>();
            
            foreach (var record in dailyData.OrderBy(r => r.TradeDate))
            {
                string key = GetCycleKey(record.TradeDate, cycleType);
                
                if (!groups.ContainsKey(key))
                {
                    groups[key] = new List<DailyDataRecord>();
                }
                
                groups[key].Add(record);
            }
            
            return groups;
        }
        
        /// <summary>
        /// 获取周期键
        /// </summary>
        private string GetCycleKey(DateTime date, string cycleType)
        {
            switch (cycleType.ToLower())
            {
                case "week":
                    // 使用ISO周（周一为一周开始）
                    var weekStart = date.AddDays(-(int)date.DayOfWeek + (date.DayOfWeek == DayOfWeek.Sunday ? -6 : 1));
                    return $"{weekStart:yyyyMMdd}";
                    
                case "month":
                    return $"{date:yyyyMM}";
                    
                case "quarter":
                    int quarter = (date.Month - 1) / 3 + 1;
                    return $"{date.Year}Q{quarter}";
                    
                default:
                    return date.ToString("yyyyMMdd");
            }
        }
        
        /// <summary>
        /// 获取缓存的股票数量
        /// </summary>
        public int GetCachedStockCount()
        {
            return _dailyKlineCache.Count;
        }
        
        /// <summary>
        /// 清空缓存
        /// </summary>
        public void Clear()
        {
            _dailyKlineCache.Clear();
        }
    }
}
