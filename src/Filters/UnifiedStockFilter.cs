using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MQReceiver.Cache;
using MQReceiver.Calculators;
using MQReceiver.Configuration;
using MQReceiver.Helpers;
using MQReceiver.Models;
using MQReceiver.Repositories;
using MQReceiver.Services;

namespace MQReceiver.Filters
{
    /// <summary>
    /// 统一股票计算器 - 用于新的6个计算条件
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

        // 表格5/6 诊断计数（FilterDiagnose_56=true 时使用）
        private int _diagCandidates;
        private int _d5_a2geM1, _d5_k1ltM3, _d5_k1lta2, _d5_all;
        private int _d6_a2in, _d6_k1ltM4, _d6_k1lta2, _d6_all;

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

            // 提前读取涨幅计算阈值（避免在计算后重复读取配置）
            decimal priceChangeThreshold = AppConfigProvider.Instance.GetDecimal("PriceChangeFilterThreshold", 7m);

            int totalCount = realTimeDataList.Count;
            int processedCount = 0;
            int validCount = 0;
            var lockObj = new object();
            
            Console.WriteLine($"[计算开始] 总股票数: {totalCount}, 目标日期: {targetDate:yyyy-MM-dd}, 条件ID: {condition.FilterId}, 涨幅计算阈值: {priceChangeThreshold}%");

            // 计算昨天的日期
            DateTime yesterdayDate = GetYesterdayDate(targetDate);
            
            // 性能优化：批量查询所有股票的成交金额（仅用于昨天成交金额>=N亿，不查换手率）
            var validStockCodes = realTimeDataList
                .Where(d => StockDataParser.IsValidStockCode(d.StockCode))
                .Select(d => d.StockCode)
                .ToList();
            
            var amountDict = _klineRepository.GetYesterdayAmountBatch(validStockCodes, yesterdayDate);
            Console.WriteLine($"[性能优化] 批量查询成交金额: {validStockCodes.Count}只股票, 查询到: {amountDict.Count}条数据");

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

            bool diagnose56 = AppConfigProvider.Instance.GetBool("FilterDiagnose_56", false);
            // 条件5、6 始终做诊断计数，便于在结果=0时自动输出排查
            bool runDiagnose56 = (condition.FilterId == 5 || condition.FilterId == 6);
            if (runDiagnose56)
            {
                _diagCandidates = 0;
                _d5_a2geM1 = _d5_k1ltM3 = _d5_k1lta2 = _d5_all = 0;
                _d6_a2in = _d6_k1ltM4 = _d6_k1lta2 = _d6_all = 0;
            }

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

                    var result = ProcessStock(realTimeData, condition, targetDate, amountDict, runDiagnose56);
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
            Console.WriteLine($"[计算完成] 处理: {totalCount}只, 符合条件: {validCount}只, 耗时: {elapsed:F1}秒, 速度: {totalCount/elapsed:F0}只/秒");
            LogMessage?.Invoke($"[计算完成] 符合条件: {validCount}只, 耗时: {elapsed:F1}秒");

            // 条件5或6：当 FilterDiagnose_56 为 true，或 该条件结果=0 时，输出诊断便于排查
            bool shouldPrintDiag56 = runDiagnose56 && (diagnose56 || validCount == 0);
            if (shouldPrintDiag56)
            {
                Console.WriteLine("[表格5/6诊断] 在「有今日+昨日KD 且 昨天成交金额>=N亿」的股票中，各子条件满足数：");
                Console.WriteLine($"  候选数(有KD且金额达标): {_diagCandidates}");
                Console.WriteLine($"  条件5(强多反弹): A2>=M1:{_d5_a2geM1}  K1<M3:{_d5_k1ltM3}  K1<A2:{_d5_k1lta2}  全部:{_d5_all}");
                Console.WriteLine($"  条件6(中多反弹): 65<=A2<78:{_d6_a2in}  K1<M4:{_d6_k1ltM4}  K1<A2:{_d6_k1lta2}  全部:{_d6_all}");
                Console.WriteLine($"[条件5公式] K1<A2 AND A2>=M1({condition.M1}) AND K1<M3({condition.M3})");
                Console.WriteLine($"[条件5阈值] M1={condition.M1} M3={condition.M3} N={condition.N}");
                if (validCount == 0 && condition.FilterId == 5)
                {
                    Console.WriteLine($"[条件5排查] 结果=0，请检查：");
                    Console.WriteLine($"  1. 候选数={_diagCandidates}（若为0，检查KD数据或成交金额）");
                    Console.WriteLine($"  2. A2>=M1={_d5_a2geM1}（若为0，没有月K/季K同时>=78的股票）");
                    Console.WriteLine($"  3. K1<M3={_d5_k1ltM3}（若为0，没有周K<50的股票）");
                    Console.WriteLine($"  4. K1<A2={_d5_k1lta2}（若为0，没有周K<min(月K,季K)的反弹形态）");
                    Console.WriteLine($"  5. 全部满足={_d5_all}（若>0但结果=0，可能被其他条件计算）");
                }
                LogMessage?.Invoke($"[表格5/6诊断] 候选:{_diagCandidates} 条件5全满足:{_d5_all} 条件6全满足:{_d6_all}");
            }

            // 应用涨幅计算：计算掉涨幅大于阈值的股票（阈值已在方法开始处读取）
            var filteredResults = results.Where(r =>
            {
                // 如果涨幅为空，保留（不计算）
                if (!r.PriceChangePercent.HasValue)
                    return true;
                // 如果涨幅小于等于阈值，保留
                return r.PriceChangePercent.Value <= priceChangeThreshold;
            }).ToList();

            int filteredCount = results.Count - filteredResults.Count;
            if (filteredCount > 0)
            {
                Console.WriteLine($"[涨幅计算] 计算掉涨幅>{priceChangeThreshold}%的股票: {filteredCount}只");
                LogMessage?.Invoke($"[涨幅计算] 已计算涨幅>{priceChangeThreshold}%的股票: {filteredCount}只");
            }

            // 按涨幅从高到低排序，涨幅缺失的排到末尾
            return filteredResults.OrderByDescending(r => r.PriceChangePercent ?? decimal.MinValue).ToList();
        }

        /// <summary>
        /// 处理单个股票
        /// </summary>
        private FilterResultWithHistory ProcessStock(RealTimeDataRecord realTimeData, NewFilterCondition condition, DateTime targetDate, Dictionary<string, decimal?> amountDict = null, bool runDiagnose56 = false)
        {
            string stockCode = realTimeData.StockCode;

            // 计算昨天的日期（在方法开始处声明一次，避免重复声明）
            DateTime yesterdayDate = GetYesterdayDate(targetDate);

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

            // 统一条件：昨天成交金额 >= N*1亿（仅用金额，无换手率过滤）
            decimal? amount;
            if (amountDict != null && amountDict.TryGetValue(stockCode, out var cached))
                amount = cached;
            else
                amount = _klineRepository.GetYesterdayAmountAndTurnoverRate(stockCode, yesterdayDate).Amount;
            decimal minAmount = condition.N * 100_000_000m;  // N 亿

            // 表格5/6 诊断：在「有KD且金额达标」的候选中统计各子条件
            if (runDiagnose56 && (condition.FilterId == 5 || condition.FilterId == 6) && amount != null && amount >= minAmount)
            {
                decimal a2 = Math.Min(monthlyK, quarterlyK);
                Interlocked.Increment(ref _diagCandidates);
                if (a2 >= condition.M1) Interlocked.Increment(ref _d5_a2geM1);
                if (weeklyK < condition.M3) Interlocked.Increment(ref _d5_k1ltM3);
                if (weeklyK < a2) Interlocked.Increment(ref _d5_k1lta2);
                if (a2 >= condition.M1 && weeklyK < condition.M3 && weeklyK < a2) Interlocked.Increment(ref _d5_all);
                if (a2 >= condition.M2 && a2 < condition.M1) Interlocked.Increment(ref _d6_a2in);
                if (weeklyK < condition.M4) Interlocked.Increment(ref _d6_k1ltM4);
                if (weeklyK < a2) Interlocked.Increment(ref _d6_k1lta2);
                if (a2 >= condition.M2 && a2 < condition.M1 && weeklyK < condition.M4 && weeklyK < a2) Interlocked.Increment(ref _d6_all);
            }

            // 检查是否满足计算条件（新公式使用 K1,K2,K3 与 D1,D2,D3，不再使用昨日 K）
            bool conditionMet = condition.CheckCondition(weeklyK, monthlyK, quarterlyK, weeklyKD.D, monthlyKD.D, quarterlyKD.D);
            
            // 条件5详细诊断：当结果=0时输出前几个候选的详细值
            if (condition.FilterId == 5 && !conditionMet)
            {
                decimal a2 = Math.Min(monthlyK, quarterlyK);
                // 只在诊断模式下输出前几个失败的候选（避免日志过多）
                if (runDiagnose56 && _diagCandidates < 10 && amount != null && amount >= minAmount)
                {
                    Console.WriteLine($"[条件5详细] {stockCode}: K1={weeklyK:F2} A2={a2:F2} 检查: K1<A2={weeklyK < a2} A2>=M1={a2 >= condition.M1} K1<M3={weeklyK < condition.M3}");
                }
            }
            
            if (!conditionMet)
                return null;

            if (amount == null || amount < minAmount)
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
