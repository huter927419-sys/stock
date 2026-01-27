using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using MQReceiver.Cache;
using MQReceiver.Models;
using MQReceiver.Services;

namespace MQReceiver.Calculators
{
    /// <summary>
    /// 批量KD计算器
    /// 预先计算并缓存所有股票的KD指标，避免重复计算
    /// 使用ChartService的计算逻辑，确保与图表数据完全一致
    /// </summary>
    public class BatchKDCalculator
    {
        private readonly KlineDataMemoryCache _klineCache;
        private readonly ChartService _chartService; // 使用ChartService确保计算一致性
        
        // 三层缓存：股票代码 -> 周期类型 -> 日期 -> KD结果
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<DateTime, KDResult>>> _kdCache;
        
        private const int DEFAULT_PERIOD = 9; // RSV周期
        
        public BatchKDCalculator(KlineDataMemoryCache klineCache, RealTimeDataCache realTimeCache)
        {
            _klineCache = klineCache ?? throw new ArgumentNullException(nameof(klineCache));
            // 创建ChartService实例，使用与图表相同的计算逻辑
            _chartService = new ChartService(realTimeCache);
            _kdCache = new ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<DateTime, KDResult>>>();
        }
        
        /// <summary>
        /// 预计算所有股票的KD指标
        /// </summary>
        public void PreCalculateAllKD(List<string> stockCodes, DateTime targetDate)
        {
            var stopwatch = Stopwatch.StartNew();
            Console.WriteLine($"[批量KD计算] 开始预计算 {stockCodes.Count} 只股票的KD指标...");
            
            int calculatedCount = 0;
            int failedCount = 0;
            var lockObj = new object();
            
            // 性能优化：调整并行度，避免过度并行导致上下文切换开销
            int maxParallelism = Math.Max(1, Math.Min(Environment.ProcessorCount, 8));
            Parallel.ForEach(stockCodes, new ParallelOptions 
            { 
                MaxDegreeOfParallelism = maxParallelism
            }, stockCode =>
            {
                try
                {
                    // 为每只股票计算周、月、季KD
                    CalculateAndCacheKD(stockCode, targetDate, "week");
                    CalculateAndCacheKD(stockCode, targetDate, "month");
                    CalculateAndCacheKD(stockCode, targetDate, "quarter");
                    
                    // 计算昨天的KD（用于计算条件判断）
                    DateTime yesterday = GetYesterdayDate(targetDate);
                    CalculateAndCacheKD(stockCode, yesterday, "week");
                    CalculateAndCacheKD(stockCode, yesterday, "month");
                    CalculateAndCacheKD(stockCode, yesterday, "quarter");
                    
                    int current = System.Threading.Interlocked.Increment(ref calculatedCount);
                    if (current % 500 == 0)
                    {
                        lock (lockObj)
                        {
                            Console.WriteLine($"[批量KD计算] 已计算 {current}/{stockCodes.Count} 只股票...");
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (lockObj)
                    {
                        failedCount++;
                        if (failedCount <= 10)
                        {
                            Console.WriteLine($"[批量KD计算] 计算 {stockCode} 失败: {ex.Message}");
                        }
                    }
                }
            });
            
            stopwatch.Stop();
            Console.WriteLine($"[批量KD计算] 预计算完成！");
            Console.WriteLine($"  成功: {calculatedCount} 只");
            Console.WriteLine($"  失败: {failedCount} 只");
            Console.WriteLine($"  耗时: {stopwatch.Elapsed.TotalSeconds:F1} 秒");
            Console.WriteLine($"  速度: {calculatedCount / stopwatch.Elapsed.TotalSeconds:F0} 只/秒");
        }
        
        /// <summary>
        /// 计算并缓存单个股票的KD指标
        /// </summary>
        private void CalculateAndCacheKD(string stockCode, DateTime targetDate, string cycleType)
        {
            var kd = CalculateKD(stockCode, targetDate, DEFAULT_PERIOD, cycleType);
            if (kd != null)
            {
                // 缓存结果
                var stockCache = _kdCache.GetOrAdd(stockCode, _ => new ConcurrentDictionary<string, ConcurrentDictionary<DateTime, KDResult>>());
                var cycleCache = stockCache.GetOrAdd(cycleType, _ => new ConcurrentDictionary<DateTime, KDResult>());
                cycleCache[targetDate] = kd;
            }
        }
        
        /// <summary>
        /// 从缓存获取KD结果
        /// </summary>
        public KDResult GetKD(string stockCode, DateTime targetDate, string cycleType)
        {
            if (_kdCache.TryGetValue(stockCode, out var stockCache) &&
                stockCache.TryGetValue(cycleType, out var cycleCache) &&
                cycleCache.TryGetValue(targetDate, out var result))
            {
                return result;
            }
            
            // 缓存未命中，实时计算
            return CalculateKD(stockCode, targetDate, DEFAULT_PERIOD, cycleType);
        }
        
        /// <summary>
        /// 计算KD指标（使用ChartService确保与图表数据完全一致）
        /// </summary>
        private KDResult CalculateKD(string stockCode, DateTime targetDate, int period, string cycleType)
        {
            try
            {
                // 直接使用ChartService的计算逻辑，确保与图表数据完全一致
                return _chartService.GetKDValue(stockCode, targetDate, cycleType);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// SMA函数（加权移动平均）：SMA(value, period, weight)
        /// 当weight=1时，公式为：SMA(value, period, 1) = (period-1)/period * 前值 + 1/period * 当前值
        /// 对应标准KD公式：K = SMA(RSV, M1, 1), D = SMA(K, M2, 1)
        /// </summary>
        /// <param name="currentValue">当前值（RSV或K值）</param>
        /// <param name="period">平滑周期（M1或M2，默认3）</param>
        /// <param name="previousValue">前一个值（前一个K值或前一个D值）</param>
        /// <returns>SMA计算结果</returns>
        private decimal SMA(decimal currentValue, int period, decimal previousValue)
        {
            // SMA(value, period, 1) = (period-1)/period * 前值 + 1/period * 当前值
            return ((period - 1m) / period) * previousValue + (1m / period) * currentValue;
        }
        
        /// <summary>
        /// 获取昨天的日期（跳过周末）
        /// </summary>
        private DateTime GetYesterdayDate(DateTime date)
        {
            DateTime yesterday = date.AddDays(-1);
            
            // 跳过周末
            while (yesterday.DayOfWeek == DayOfWeek.Saturday || yesterday.DayOfWeek == DayOfWeek.Sunday)
            {
                yesterday = yesterday.AddDays(-1);
            }
            
            return yesterday;
        }
        
        /// <summary>
        /// 获取缓存统计
        /// </summary>
        public (int stockCount, int totalResults) GetCacheStats()
        {
            int stockCount = _kdCache.Count;
            int totalResults = _kdCache.Values.Sum(stock => 
                stock.Values.Sum(cycle => cycle.Count));
            
            return (stockCount, totalResults);
        }
        
        /// <summary>
        /// 清空缓存
        /// </summary>
        public void Clear()
        {
            _kdCache.Clear();
        }
    }
}
