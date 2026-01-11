using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MQReceiver.Cache;
using MQReceiver.Calculators;
using MQReceiver.Helpers;
using MQReceiver.Models;
using MQReceiver.Repositories;

namespace MQReceiver.Filters
{
    /// <summary>
    /// 统一股票过滤器 - 用于新的8个过滤条件
    /// 包含涨幅计算功能
    /// </summary>
    public class UnifiedStockFilter
    {
        private readonly KDCalculator _kdCalculator;
        private readonly RealTimeDataCache _realTimeCache;
        private readonly PostgresKlineDataRepository _klineRepository;

        public UnifiedStockFilter(KDCalculator kdCalculator, RealTimeDataCache realTimeCache)
        {
            _kdCalculator = kdCalculator ?? throw new ArgumentNullException(nameof(kdCalculator));
            _realTimeCache = realTimeCache ?? throw new ArgumentNullException(nameof(realTimeCache));
            _klineRepository = new PostgresKlineDataRepository(DatabaseConnectionHelper.BuildConnectionString());
        }

        /// <summary>
        /// 执行过滤（并行版本）
        /// </summary>
        public List<FilterResultWithHistory> FilterParallel(NewFilterCondition condition, DateTime targetDate)
        {
            var results = new ConcurrentBag<FilterResultWithHistory>();
            var realTimeDataList = _realTimeCache.GetAllData();

            if (realTimeDataList == null || realTimeDataList.Count == 0)
                return new List<FilterResultWithHistory>();

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            Parallel.ForEach(realTimeDataList, parallelOptions, realTimeData =>
            {
                try
                {
                    // 跳过无效的股票代码（非A股、创业板、北交所，以及上证指数）
                    if (!StockDataParser.IsValidStockCode(realTimeData.StockCode))
                        return;

                    var result = ProcessStock(realTimeData, condition, targetDate);
                    if (result != null)
                    {
                        results.Add(result);
                    }
                }
                catch (Exception)
                {
                    // 忽略单个股票的处理错误
                }
            });

            return results.OrderByDescending(r => r.QuarterlyK).ToList();
        }

        /// <summary>
        /// 处理单个股票
        /// </summary>
        private FilterResultWithHistory ProcessStock(RealTimeDataRecord realTimeData, NewFilterCondition condition, DateTime targetDate)
        {
            string stockCode = realTimeData.StockCode;

            // 计算当前K值
            var weeklyKD = _kdCalculator.CalculateWeeklyKD(stockCode, targetDate);
            var monthlyKD = _kdCalculator.CalculateMonthlyKD(stockCode, targetDate);
            var quarterlyKD = _kdCalculator.CalculateQuarterlyKD(stockCode, targetDate);

            if (weeklyKD == null || monthlyKD == null || quarterlyKD == null)
                return null;

            decimal weeklyK = weeklyKD.K;
            decimal monthlyK = monthlyKD.K;
            decimal quarterlyK = quarterlyKD.K;

            // 计算昨天的K值（用于REF(K1,1)和REF(K2,1)判断）
            DateTime yesterdayDate = GetYesterdayDate(targetDate);
            var yesterdayWeeklyKD = _kdCalculator.CalculateWeeklyKD(stockCode, yesterdayDate);
            var yesterdayMonthlyKD = _kdCalculator.CalculateMonthlyKD(stockCode, yesterdayDate);
            var yesterdayQuarterlyKD = _kdCalculator.CalculateQuarterlyKD(stockCode, yesterdayDate);

            if (yesterdayWeeklyKD == null || yesterdayMonthlyKD == null || yesterdayQuarterlyKD == null)
                return null;

            decimal yesterdayWeeklyK = yesterdayWeeklyKD.K;
            decimal yesterdayMonthlyK = yesterdayMonthlyKD.K;
            decimal yesterdayQuarterlyK = yesterdayQuarterlyKD.K;

            // 检查是否满足过滤条件
            if (!condition.CheckCondition(weeklyK, monthlyK, quarterlyK, yesterdayWeeklyK, yesterdayMonthlyK))
                return null;

            // 计算涨幅
            decimal? priceChangePercent = CalculatePriceChangePercent(stockCode, targetDate);

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
        /// 计算涨幅
        /// 今日有交易：今日收盘价相对昨日收盘价的涨幅
        /// 今日无交易：最近交易日的涨幅
        /// </summary>
        private decimal? CalculatePriceChangePercent(string stockCode, DateTime targetDate)
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

                // 计算涨幅百分比
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
