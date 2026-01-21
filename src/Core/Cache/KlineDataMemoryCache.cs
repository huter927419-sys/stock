using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using MQReceiver.Calculators;
using MQReceiver.Helpers;
using MQReceiver.Models;
using MQReceiver.Repositories;

namespace MQReceiver.Cache
{
    /// <summary>
    /// K线数据内存缓存
    /// 预加载所有股票的K线数据到内存，并预计算前复权价格，大幅提升过滤速度
    /// </summary>
    public class KlineDataMemoryCache
    {
        // 股票代码 -> K线数据列表（包含前复权价格）
        private readonly ConcurrentDictionary<string, List<AdjustedKlineData>> _klineCache;
        
        // 除权数据缓存（股票代码 -> 除权数据列表）
        private readonly ConcurrentDictionary<string, List<ExRightsDataRecord>> _exRightsCache;
        
        private readonly IKlineDataRepository _klineRepository;
        private readonly IExRightsDataRepository _exRightsRepository;
        private readonly ExRightsAdjustmentCalculator _exRightsCalculator;
        
        private DateTime _lastLoadTime = DateTime.MinValue;
        private bool _isLoaded = false;
        private readonly object _loadLock = new object();
        
        /// <summary>
        /// 调整后的K线数据（包含前复权OHLC）
        /// </summary>
        public class AdjustedKlineData
        {
            public DateTime TradeDate { get; set; }
            public decimal Open { get; set; }
            public decimal High { get; set; }
            public decimal Low { get; set; }
            public decimal Close { get; set; }
            public long Volume { get; set; }
            
            // 前复权价格
            public decimal AdjustedOpen { get; set; }
            public decimal AdjustedHigh { get; set; }
            public decimal AdjustedLow { get; set; }
            public decimal AdjustedClose { get; set; }
        }
        
        public KlineDataMemoryCache()
        {
            string connectionString = DatabaseConnectionHelper.BuildConnectionString();
            _klineRepository = new PostgresKlineDataRepository(connectionString);
            _exRightsRepository = new PostgresExRightsDataRepository(connectionString);
            _exRightsCalculator = new ExRightsAdjustmentCalculator(_exRightsRepository, _klineRepository);
            
            _klineCache = new ConcurrentDictionary<string, List<AdjustedKlineData>>();
            _exRightsCache = new ConcurrentDictionary<string, List<ExRightsDataRecord>>();
        }
        
        /// <summary>
        /// 是否已加载数据
        /// </summary>
        public bool IsLoaded => _isLoaded;
        
        /// <summary>
        /// 缓存的股票数量
        /// </summary>
        public int StockCount => _klineCache.Count;
        
        /// <summary>
        /// 预加载所有股票的K线数据到内存
        /// </summary>
        public void PreloadAllStockData(List<string> stockCodes)
        {
            if (_isLoaded) return;
            
            lock (_loadLock)
            {
                if (_isLoaded) return;
                
                var stopwatch = Stopwatch.StartNew();
                Console.WriteLine($"[K线缓存] 开始预加载 {stockCodes.Count} 只股票的K线数据...");
                
                // 并行加载K线数据和除权数据
                int loadedCount = 0;
                int failedCount = 0;
                var lockObj = new object();
                
                Parallel.ForEach(stockCodes, new ParallelOptions 
                { 
                    MaxDegreeOfParallelism = Environment.ProcessorCount 
                }, stockCode =>
                {
                    try
                    {
                        // 加载K线数据
                        var klineData = _klineRepository.GetDailyData(stockCode, DateTime.MinValue, DateTime.Now);
                        if (klineData == null || klineData.Count == 0)
                        {
                            lock (lockObj) { failedCount++; }
                            return;
                        }
                        
                        // 加载除权数据
                        var exRightsData = _exRightsRepository.GetExRightsDataAfterDate(stockCode, DateTime.MinValue);
                        _exRightsCache[stockCode] = exRightsData;
                        
                        // 批量计算前复权OHLC价格
                        var ohlcDict = klineData.ToDictionary(
                            k => k.TradeDate,
                            k => (k.Open, k.High, k.Low, k.Close)
                        );
                        
                        var adjustedOHLC = _exRightsCalculator.BatchCalculateOHLCAdjustedPrices(stockCode, ohlcDict);
                        
                        // 合并原始数据和前复权数据
                        var adjustedKlineList = new List<AdjustedKlineData>(klineData.Count);
                        foreach (var kline in klineData.OrderBy(k => k.TradeDate))
                        {
                            var adjusted = adjustedOHLC.ContainsKey(kline.TradeDate) 
                                ? adjustedOHLC[kline.TradeDate] 
                                : (kline.Open, kline.High, kline.Low, kline.Close);
                            
                            adjustedKlineList.Add(new AdjustedKlineData
                            {
                                TradeDate = kline.TradeDate,
                                Open = kline.Open,
                                High = kline.High,
                                Low = kline.Low,
                                Close = kline.Close,
                                Volume = kline.Volume,
                                AdjustedOpen = adjusted.Open,
                                AdjustedHigh = adjusted.High,
                                AdjustedLow = adjusted.Low,
                                AdjustedClose = adjusted.Close
                            });
                        }
                        
                        _klineCache[stockCode] = adjustedKlineList;
                        
                        int current = System.Threading.Interlocked.Increment(ref loadedCount);
                        if (current % 500 == 0)
                        {
                            lock (lockObj)
                            {
                                Console.WriteLine($"[K线缓存] 已加载 {current}/{stockCodes.Count} 只股票...");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (lockObj) 
                        { 
                            failedCount++; 
                            if (failedCount <= 10) // 只输出前10个错误
                            {
                                Console.WriteLine($"[K线缓存] 加载 {stockCode} 失败: {ex.Message}");
                            }
                        }
                    }
                });
                
                stopwatch.Stop();
                _isLoaded = true;
                _lastLoadTime = DateTime.Now;
                
                Console.WriteLine($"[K线缓存] 预加载完成！");
                Console.WriteLine($"  成功: {loadedCount} 只");
                Console.WriteLine($"  失败: {failedCount} 只");
                Console.WriteLine($"  耗时: {stopwatch.Elapsed.TotalSeconds:F1} 秒");
                Console.WriteLine($"  速度: {loadedCount / stopwatch.Elapsed.TotalSeconds:F0} 只/秒");
            }
        }
        
        /// <summary>
        /// 获取股票的前复权K线数据（直接从内存读取）
        /// </summary>
        public List<AdjustedKlineData> GetAdjustedKlineData(string stockCode, DateTime startDate, DateTime endDate)
        {
            if (!_klineCache.TryGetValue(stockCode, out var klineList))
            {
                return new List<AdjustedKlineData>();
            }
            
            return klineList.Where(k => k.TradeDate >= startDate && k.TradeDate <= endDate).ToList();
        }
        
        /// <summary>
        /// 获取股票的所有前复权K线数据
        /// </summary>
        public List<AdjustedKlineData> GetAllAdjustedKlineData(string stockCode)
        {
            if (!_klineCache.TryGetValue(stockCode, out var klineList))
            {
                return new List<AdjustedKlineData>();
            }
            
            return klineList;
        }
        
        /// <summary>
        /// 获取聚合K线数据（周/月/季）
        /// </summary>
        public List<AggregatedCandle> GetAggregatedData(string stockCode, DateTime targetDate, string cycleType)
        {
            var klineData = GetAllAdjustedKlineData(stockCode);
            if (klineData.Count == 0)
            {
                return new List<AggregatedCandle>();
            }
            
            // 过滤到目标日期
            var filteredData = klineData.Where(k => k.TradeDate <= targetDate).ToList();
            if (filteredData.Count == 0)
            {
                return new List<AggregatedCandle>();
            }
            
            // 根据周期类型聚合
            switch (cycleType.ToLower())
            {
                case "week":
                    return AggregateWeekly(filteredData);
                case "month":
                    return AggregateMonthly(filteredData);
                case "quarter":
                    return AggregateQuarterly(filteredData);
                default:
                    throw new ArgumentException($"不支持的周期类型: {cycleType}");
            }
        }
        
        private List<AggregatedCandle> AggregateWeekly(List<AdjustedKlineData> klineData)
        {
            var weeklyData = new List<AggregatedCandle>();
            var currentWeekData = new List<AdjustedKlineData>();
            int currentWeekOfYear = -1;
            int currentYear = -1;
            
            foreach (var kline in klineData.OrderBy(k => k.TradeDate))
            {
                var calendar = System.Globalization.CultureInfo.CurrentCulture.Calendar;
                int weekOfYear = calendar.GetWeekOfYear(kline.TradeDate, 
                    System.Globalization.CalendarWeekRule.FirstDay, 
                    DayOfWeek.Monday);
                int year = kline.TradeDate.Year;
                
                if (currentWeekOfYear != weekOfYear || currentYear != year)
                {
                    // 新的一周，保存上周数据
                    if (currentWeekData.Count > 0)
                    {
                        weeklyData.Add(CreateAggregatedCandle(currentWeekData));
                        currentWeekData.Clear();
                    }
                    currentWeekOfYear = weekOfYear;
                    currentYear = year;
                }
                
                currentWeekData.Add(kline);
            }
            
            // 保存最后一周
            if (currentWeekData.Count > 0)
            {
                weeklyData.Add(CreateAggregatedCandle(currentWeekData));
            }
            
            return weeklyData;
        }
        
        private List<AggregatedCandle> AggregateMonthly(List<AdjustedKlineData> klineData)
        {
            var monthlyData = klineData
                .OrderBy(k => k.TradeDate)
                .GroupBy(k => new { k.TradeDate.Year, k.TradeDate.Month })
                .Select(g => CreateAggregatedCandle(g.ToList()))
                .ToList();
            
            return monthlyData;
        }
        
        private List<AggregatedCandle> AggregateQuarterly(List<AdjustedKlineData> klineData)
        {
            var quarterlyData = klineData
                .OrderBy(k => k.TradeDate)
                .GroupBy(k => new { k.TradeDate.Year, Quarter = (k.TradeDate.Month - 1) / 3 })
                .Select(g => CreateAggregatedCandle(g.ToList()))
                .ToList();
            
            return quarterlyData;
        }
        
        private AggregatedCandle CreateAggregatedCandle(List<AdjustedKlineData> periodData)
        {
            return new AggregatedCandle
            {
                Date = periodData.Last().TradeDate,
                Open = periodData.First().AdjustedOpen,
                High = periodData.Max(k => k.AdjustedHigh),
                Low = periodData.Min(k => k.AdjustedLow),
                Close = periodData.Last().AdjustedClose,
                Volume = periodData.Sum(k => k.Volume)
            };
        }
        
        /// <summary>
        /// 清除缓存
        /// </summary>
        public void Clear()
        {
            _klineCache.Clear();
            _exRightsCache.Clear();
            _isLoaded = false;
        }
        
        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        public (int StockCount, long TotalDataPoints, long MemoryEstimateMB) GetCacheStats()
        {
            long totalDataPoints = _klineCache.Values.Sum(list => list.Count);
            // 估算内存占用：每个数据点约 80 字节（8个decimal + DateTime + long）
            long memoryBytes = totalDataPoints * 80;
            long memoryMB = memoryBytes / (1024 * 1024);
            
            return (_klineCache.Count, totalDataPoints, memoryMB);
        }
    }
    
    /// <summary>
    /// 聚合后的K线数据
    /// </summary>
    public class AggregatedCandle
    {
        public DateTime Date { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public long Volume { get; set; }
    }
}
