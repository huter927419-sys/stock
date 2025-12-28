using System;
using System.Collections.Generic;
using System.Linq;
using MQReceiver.Helpers;
using MQReceiver.Repositories;

namespace MQReceiver.Calculators
{
    /// <summary>
    /// 复权计算器 - 用于计算前复权和后复权价格
    ///
    /// 复权说明：
    /// 1. 前复权：以最新价格为基准，向前调整历史价格（推荐用于技术分析）
    /// 2. 后复权：以最早价格为基准，向后调整历史价格（用于查看历史真实价格）
    ///
    /// 除权除息计算公式：
    /// - 送股：复权价格 = 原价格 / (1 + 送股比例)
    /// - 配股：复权价格 = (原价格 + 配股价 * 配股比例) / (1 + 配股比例)
    /// - 分红：复权价格 = 原价格 - 每股红利
    /// - 组合：需要按顺序计算
    /// </summary>
    public class ExRightsAdjustmentCalculator
    {
        private readonly IExRightsDataRepository _exRightsRepository;
        private readonly IKlineDataRepository _klineRepository;

        // 除权数据缓存（股票代码 -> 除权数据列表）
        private readonly Dictionary<string, List<ExRightsDataRecord>> _exRightsCache = new Dictionary<string, List<ExRightsDataRecord>>();
        private readonly object _cacheLock = new object();

        /// <summary>
        /// 使用默认仓储初始化（向后兼容）
        /// </summary>
        public ExRightsAdjustmentCalculator()
        {
            string connectionString = DatabaseConnectionHelper.BuildConnectionString();
            _exRightsRepository = new PostgresExRightsDataRepository(connectionString);
            _klineRepository = new PostgresKlineDataRepository(connectionString);
        }

        /// <summary>
        /// 使用指定仓储初始化（依赖注入）
        /// </summary>
        public ExRightsAdjustmentCalculator(IExRightsDataRepository exRightsRepository, IKlineDataRepository klineRepository)
        {
            _exRightsRepository = exRightsRepository ?? throw new ArgumentNullException(nameof(exRightsRepository));
            _klineRepository = klineRepository ?? throw new ArgumentNullException(nameof(klineRepository));
        }

        /// <summary>
        /// 获取股票的除权数据（带缓存）
        /// </summary>
        private List<ExRightsDataRecord> GetExRightsDataCached(string stockCode)
        {
            lock (_cacheLock)
            {
                if (_exRightsCache.TryGetValue(stockCode, out var cachedData))
                {
                    return cachedData;
                }

                // 一次性加载该股票的所有除权数据
                var exRightsData = _exRightsRepository.GetExRightsDataAfterDate(stockCode, DateTime.MinValue);
                _exRightsCache[stockCode] = exRightsData;
                return exRightsData;
            }
        }

        /// <summary>
        /// 清除指定股票的除权数据缓存
        /// </summary>
        public void ClearCache(string stockCode = null)
        {
            lock (_cacheLock)
            {
                if (stockCode != null)
                {
                    _exRightsCache.Remove(stockCode);
                }
                else
                {
                    _exRightsCache.Clear();
                }
            }
        }

        /// <summary>
        /// 计算前复权价格（以最新价格为基准，向前调整历史价格）
        /// 适用于：技术分析、K线图显示
        /// </summary>
        /// <param name="stockCode">股票代码</param>
        /// <param name="targetDate">目标日期</param>
        /// <param name="originalPrice">原始价格</param>
        /// <returns>前复权价格</returns>
        public decimal CalculateForwardAdjustedPrice(string stockCode, DateTime targetDate, decimal originalPrice)
        {
            // 从缓存获取除权数据，筛选目标日期之后的
            var allExRights = GetExRightsDataCached(stockCode);
            var exRightsList = allExRights.Where(x => x.ExRightsDate > targetDate).ToList();

            decimal adjustedPrice = originalPrice;

            // 从最新日期向前计算
            foreach (var exRights in exRightsList.OrderByDescending(x => x.ExRightsDate))
            {
                adjustedPrice = AdjustPriceForward(adjustedPrice, exRights);
            }

            return adjustedPrice;
        }

        /// <summary>
        /// 计算后复权价格（以最早价格为基准，向后调整历史价格）
        /// 适用于：查看历史真实价格
        /// </summary>
        /// <param name="stockCode">股票代码</param>
        /// <param name="targetDate">目标日期</param>
        /// <param name="originalPrice">原始价格</param>
        /// <returns>后复权价格</returns>
        public decimal CalculateBackwardAdjustedPrice(string stockCode, DateTime targetDate, decimal originalPrice)
        {
            // 从缓存获取除权数据，筛选目标日期之前的
            var allExRights = GetExRightsDataCached(stockCode);
            var exRightsList = allExRights.Where(x => x.ExRightsDate <= targetDate).ToList();

            decimal adjustedPrice = originalPrice;

            // 从最早日期向后计算
            foreach (var exRights in exRightsList.OrderBy(x => x.ExRightsDate))
            {
                adjustedPrice = AdjustPriceBackward(adjustedPrice, exRights);
            }

            return adjustedPrice;
        }

        /// <summary>
        /// 前复权价格调整（向前调整）
        /// </summary>
        private decimal AdjustPriceForward(decimal price, ExRightsDataRecord exRights)
        {
            decimal adjustedPrice = price;

            // 计算送股比例（每10股送X股，比例 = X/10）
            decimal giveRatio = exRights.GivePer10Shares / 10m;

            // 计算配股比例（每10股配X股，比例 = X/10）
            decimal peiRatio = exRights.PeiPer10Shares / 10m;

            // 1. 处理送股：复权价格 = 原价格 / (1 + 送股比例)
            if (giveRatio > 0)
            {
                adjustedPrice = adjustedPrice / (1 + giveRatio);
            }

            // 2. 处理配股：复权价格 = (原价格 + 配股价 * 配股比例) / (1 + 配股比例)
            if (peiRatio > 0 && exRights.PeiPrice > 0)
            {
                adjustedPrice = (adjustedPrice + exRights.PeiPrice * peiRatio) / (1 + peiRatio);
            }

            // 3. 处理分红：复权价格 = 原价格 - 每股红利
            if (exRights.ProfitPerShare > 0)
            {
                adjustedPrice = adjustedPrice - exRights.ProfitPerShare;
            }

            return adjustedPrice;
        }

        /// <summary>
        /// 后复权价格调整（向后调整）
        /// </summary>
        private decimal AdjustPriceBackward(decimal price, ExRightsDataRecord exRights)
        {
            decimal adjustedPrice = price;

            // 计算送股比例
            decimal giveRatio = exRights.GivePer10Shares / 10m;

            // 计算配股比例
            decimal peiRatio = exRights.PeiPer10Shares / 10m;

            // 1. 处理分红：先加回红利
            if (exRights.ProfitPerShare > 0)
            {
                adjustedPrice = adjustedPrice + exRights.ProfitPerShare;
            }

            // 2. 处理配股：反向计算
            if (peiRatio > 0 && exRights.PeiPrice > 0)
            {
                adjustedPrice = adjustedPrice * (1 + peiRatio) - exRights.PeiPrice * peiRatio;
            }

            // 3. 处理送股：反向计算
            if (giveRatio > 0)
            {
                adjustedPrice = adjustedPrice * (1 + giveRatio);
            }

            return adjustedPrice;
        }

        /// <summary>
        /// 批量计算前复权价格（用于更新日线数据表）
        /// 注意：此方法会直接更新数据库，请谨慎使用
        /// </summary>
        public int BatchCalculateForwardAdjustedPrices(string stockCode)
        {
            int updatedCount = 0;

            try
            {
                // 获取所有日线数据
                var dailyDataList = _klineRepository.GetDailyData(stockCode, DateTime.MinValue, DateTime.Now);

                if (dailyDataList == null || dailyDataList.Count == 0)
                {
                    Console.WriteLine($"股票 {stockCode} 没有日线数据");
                    return 0;
                }

                // 计算每条数据的复权价格
                var adjustedDataList = new List<KlineData>();

                foreach (var dailyData in dailyDataList.OrderBy(x => x.TradeDate))
                {
                    adjustedDataList.Add(new KlineData
                    {
                        StockCode = stockCode,
                        TradeDate = dailyData.TradeDate,
                        Open = CalculateForwardAdjustedPrice(stockCode, dailyData.TradeDate, dailyData.Open),
                        High = CalculateForwardAdjustedPrice(stockCode, dailyData.TradeDate, dailyData.High),
                        Low = CalculateForwardAdjustedPrice(stockCode, dailyData.TradeDate, dailyData.Low),
                        Close = CalculateForwardAdjustedPrice(stockCode, dailyData.TradeDate, dailyData.Close),
                        Volume = dailyData.Volume
                    });
                }

                // 批量更新日线数据
                updatedCount = _klineRepository.UpdateDailyData(adjustedDataList);

                Console.WriteLine($"股票 {stockCode} 批量复权完成，更新 {updatedCount} 条记录");
            }
            catch (Exception ex)
            {
                Console.WriteLine("批量计算复权价格失败: " + ex.Message);
            }

            return updatedCount;
        }
    }
}
