using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MQReceiver.Cache;
using MQReceiver.Calculators;
using MQReceiver.Helpers;
using MQReceiver.Models;
using MQReceiver.Repositories;
using MQReceiver.Services;

namespace MQReceiver.Filters
{
    /// <summary>
    /// 统一股票过滤器 - 用于新的6个过滤条件
    /// 包含涨幅计算功能
    /// 使用ChartService的KD计算逻辑，确保与图表数据一致
    /// </summary>
    public class UnifiedStockFilter
    {
        private readonly KDCalculator _kdCalculator; // 保留作为备用
        private readonly ChartService _chartService; // 使用ChartService计算KD，确保与图表一致
        private readonly RealTimeDataCache _realTimeCache;
        private readonly PostgresKlineDataRepository _klineRepository;
        private readonly BatchKDCalculator _batchKDCalculator; // 批量KD计算器（性能优化）
        
        /// <summary>
        /// 日志消息事件
        /// </summary>
        public event Action<string> LogMessage;

        public UnifiedStockFilter(KDCalculator kdCalculator, RealTimeDataCache realTimeCache)
        {
            _kdCalculator = kdCalculator ?? throw new ArgumentNullException(nameof(kdCalculator));
            _realTimeCache = realTimeCache ?? throw new ArgumentNullException(nameof(realTimeCache));
            _klineRepository = new PostgresKlineDataRepository(DatabaseConnectionHelper.BuildConnectionString());
            // 创建ChartService实例，使用与图表相同的KD计算逻辑
            _chartService = new ChartService(realTimeCache);
            // _batchKDCalculator = null; // 默认不使用批量计算器
        }
        
        /// <summary>
        /// 构造函数（支持批量KD计算）
        /// </summary>
        public UnifiedStockFilter(KDCalculator kdCalculator, RealTimeDataCache realTimeCache, BatchKDCalculator batchKDCalculator)
        {
            _kdCalculator = kdCalculator ?? throw new ArgumentNullException(nameof(kdCalculator));
            _realTimeCache = realTimeCache ?? throw new ArgumentNullException(nameof(realTimeCache));
            _klineRepository = new PostgresKlineDataRepository(DatabaseConnectionHelper.BuildConnectionString());
            // 创建ChartService实例，使用与图表相同的KD计算逻辑
            _chartService = new ChartService(realTimeCache);
            _batchKDCalculator = batchKDCalculator; // 使用批量计算器（性能优化）
        }

        /// <summary>
        /// 执行过滤（并行版本）
        /// 优化：添加进度统计和性能优化
        /// </summary>
        public List<FilterResultWithHistory> FilterParallel(NewFilterCondition condition, DateTime targetDate)
        {
            var startTime = DateTime.Now;
            var results = new ConcurrentBag<FilterResultWithHistory>();
            var realTimeDataList = _realTimeCache.GetAllData();

            if (realTimeDataList == null || realTimeDataList.Count == 0)
                return new List<FilterResultWithHistory>();

            int totalCount = realTimeDataList.Count;
            int processedCount = 0;
            int validCount = 0;
            var lockObj = new object();
            
            Console.WriteLine($"[过滤开始] 总股票数: {totalCount}, 目标日期: {targetDate:yyyy-MM-dd}");

            // 优化并行度：使用内存缓存后，可以使用更高的并行度
            // 批量计算器模式：使用全部CPU核心（内存操作，无IO瓶颈）
            // 原始模式：使用一半CPU核心（有IO操作）
            int optimalParallelism = _batchKDCalculator != null 
                ? Environment.ProcessorCount  // 内存模式：全核心
                : Math.Max(Environment.ProcessorCount / 2, 4); // IO模式：半核心
            
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = optimalParallelism
            };

            Parallel.ForEach(realTimeDataList, parallelOptions, realTimeData =>
            {
                try
                {
                    // 跳过无效的股票代码（非A股、创业板、北交所，以及上证指数）
                    if (!StockDataParser.IsValidStockCode(realTimeData.StockCode))
                    {
                        Interlocked.Increment(ref processedCount);
                        return;
                    }

                    var result = ProcessStock(realTimeData, condition, targetDate);
                    if (result != null)
                    {
                        results.Add(result);
                        Interlocked.Increment(ref validCount);
                    }
                    
                    Interlocked.Increment(ref processedCount);
                    
                    // 每处理100只股票输出一次进度（减少输出频率，提升性能）
                    if (processedCount % 100 == 0)
                    {
                        lock (lockObj)
                        {
                            if (processedCount % 500 == 0)  // 每500只输出一次详细进度
                            {
                                double progress = (double)processedCount / totalCount * 100;
                                string logMsg = $"  进度: {processedCount}/{totalCount} ({progress:F1}%), 已找到: {validCount} 只";
                                Console.WriteLine(logMsg);
                                LogMessage?.Invoke(logMsg);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // 忽略单个股票的处理错误
                    Interlocked.Increment(ref processedCount);
                }
            });

            var elapsed = (DateTime.Now - startTime).TotalSeconds;
            Console.WriteLine($"[过滤完成] 处理: {totalCount}只, 符合条件: {validCount}只, 耗时: {elapsed:F1}秒, 速度: {totalCount/elapsed:F0}只/秒");
            LogMessage?.Invoke($"[过滤完成] 符合条件: {validCount}只, 耗时: {elapsed:F1}秒");
            
            return results.OrderByDescending(r => r.QuarterlyK).ToList();
        }

        /// <summary>
        /// 处理单个股票
        /// </summary>
        private FilterResultWithHistory ProcessStock(RealTimeDataRecord realTimeData, NewFilterCondition condition, DateTime targetDate)
        {
            string stockCode = realTimeData.StockCode;

            // 使用ChartService计算KD值，确保与图表数据完全一致
            KDResult weeklyKD, monthlyKD, quarterlyKD;
            KDResult yesterdayWeeklyKD, yesterdayMonthlyKD, yesterdayQuarterlyKD;
            
            // 优先使用批量计算器（性能优化），但如果没有则使用ChartService
            if (_batchKDCalculator != null)
            {
                // 使用批量计算器（从内存缓存读取）
                weeklyKD = _batchKDCalculator.GetKD(stockCode, targetDate, "week");
                monthlyKD = _batchKDCalculator.GetKD(stockCode, targetDate, "month");
                quarterlyKD = _batchKDCalculator.GetKD(stockCode, targetDate, "quarter");
                
                DateTime yesterdayDate = GetYesterdayDate(targetDate);
                yesterdayWeeklyKD = _batchKDCalculator.GetKD(stockCode, yesterdayDate, "week");
                yesterdayMonthlyKD = _batchKDCalculator.GetKD(stockCode, yesterdayDate, "month");
                yesterdayQuarterlyKD = _batchKDCalculator.GetKD(stockCode, yesterdayDate, "quarter");
            }
            else
            {
                // 使用ChartService计算KD值（与图表使用相同的计算逻辑）
                weeklyKD = _chartService.GetKDValue(stockCode, targetDate, "week");
                monthlyKD = _chartService.GetKDValue(stockCode, targetDate, "month");
                quarterlyKD = _chartService.GetKDValue(stockCode, targetDate, "quarter");

                DateTime yesterdayDate = GetYesterdayDate(targetDate);
                yesterdayWeeklyKD = _chartService.GetKDValue(stockCode, yesterdayDate, "week");
                yesterdayMonthlyKD = _chartService.GetKDValue(stockCode, yesterdayDate, "month");
                yesterdayQuarterlyKD = _chartService.GetKDValue(stockCode, yesterdayDate, "quarter");
            }

            if (weeklyKD == null || monthlyKD == null || quarterlyKD == null)
                return null;
            if (yesterdayWeeklyKD == null || yesterdayMonthlyKD == null || yesterdayQuarterlyKD == null)
                return null;

            decimal weeklyK = weeklyKD.K;
            decimal monthlyK = monthlyKD.K;
            decimal quarterlyK = quarterlyKD.K;
            decimal yesterdayWeeklyK = yesterdayWeeklyKD.K;
            decimal yesterdayMonthlyK = yesterdayMonthlyKD.K;
            decimal yesterdayQuarterlyK = yesterdayQuarterlyKD.K;

            // 检查是否满足过滤条件
            if (!condition.CheckCondition(weeklyK, monthlyK, quarterlyK, yesterdayWeeklyK, yesterdayMonthlyK))
                return null;

            // 计算涨幅 - 优先使用实时数据，无效时从日线数据获取
            decimal? priceChangePercent = CalculatePriceChangePercentFromRealTime(realTimeData);
            if (priceChangePercent == null)
            {
                priceChangePercent = CalculatePriceChangePercentFromDaily(stockCode, targetDate);
            }

            // 获取股票名称
            string stockName = StockInfoCache.Instance.GetStockName(stockCode);

            return new FilterResultWithHistory
            {
                StockCode = stockCode,
                StockName = stockName,
                PriceChangePercent = priceChangePercent,
                WeeklyK = weeklyK,
                MonthlyK = monthlyK,
                QuarterlyK = quarterlyK,
                YesterdayWeeklyK = yesterdayWeeklyK,
                YesterdayMonthlyK = yesterdayMonthlyK,
                YesterdayQuarterlyK = yesterdayQuarterlyK
            };
        }

        /// <summary>
        /// 从实时数据计算涨幅（优先使用）
        /// 使用实时的最新价格和昨日收盘价计算
        /// </summary>
        private decimal? CalculatePriceChangePercentFromRealTime(RealTimeDataRecord realTimeData)
        {
            try
            {
                if (realTimeData == null)
                    return null;

                decimal newPrice = realTimeData.NewPrice;
                decimal lastClose = realTimeData.LastClose;

                // 检查数据有效性
                if (lastClose <= 0 || newPrice <= 0)
                    return null;

                // 计算涨幅百分比（保留2位小数）
                decimal priceChange = newPrice - lastClose;
                decimal priceChangePercent = (priceChange / lastClose) * 100;

                return Math.Round(priceChangePercent, 2);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 从日线数据计算涨幅（备用方案）
        /// 今日有交易：今日收盘价相对昨日收盘价的涨幅
        /// 今日无交易：最近交易日的涨幅
        /// </summary>
        private decimal? CalculatePriceChangePercentFromDaily(string stockCode, DateTime targetDate)
        {
            try
            {
                // 获取最近2个交易日的K线数据
                var recentKlines = _klineRepository.GetDailyData(stockCode, targetDate.AddDays(-10), targetDate);
                if (recentKlines == null || recentKlines.Count < 2)
                    return null;

                // 按日期排序
                var sortedKlines = recentKlines.OrderByDescending(k => k.TradeDate).ToList();

                // 取最近的两个交易日
                var todayKline = sortedKlines[0];
                var previousKline = sortedKlines[1];

                if (previousKline.Close == 0)
                    return null;

                // 计算涨幅百分比（保留2位小数）
                decimal priceChange = todayKline.Close - previousKline.Close;
                decimal priceChangePercent = (priceChange / previousKline.Close) * 100;

                return Math.Round(priceChangePercent, 2);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 获取前一个交易日日期（简化版本，实际可能需要考虑节假日）
        /// </summary>
        private DateTime GetYesterdayDate(DateTime targetDate)
        {
            DateTime yesterday = targetDate.AddDays(-1);
            // 如果是周末，往前推到周五
            while (yesterday.DayOfWeek == DayOfWeek.Saturday || yesterday.DayOfWeek == DayOfWeek.Sunday)
            {
                yesterday = yesterday.AddDays(-1);
            }
            return yesterday;
        }
    }
}
