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
        /// 批量计算前复权价格（性能优化版本）
        /// 一次性计算整个股票的所有日期的前复权价格
        /// </summary>
        /// <param name="stockCode">股票代码</param>
        /// <param name="priceData">日期 -> 原始价格的字典</param>
        /// <returns>日期 -> 前复权价格的字典</returns>
        public Dictionary<DateTime, decimal> BatchCalculateForwardAdjustedPrices(string stockCode, Dictionary<DateTime, decimal> priceData)
        {
            var result = new Dictionary<DateTime, decimal>();
            if (priceData == null || priceData.Count == 0)
                return result;

            // 获取除权数据
            var allExRights = GetExRightsDataCached(stockCode);
            
            // 如果没有除权数据，直接返回原始价格
            if (allExRights.Count == 0)
            {
                return new Dictionary<DateTime, decimal>(priceData);
            }

            // 为每个日期计算前复权价格
            foreach (var kvp in priceData)
            {
                DateTime targetDate = kvp.Key;
                decimal originalPrice = kvp.Value;
                
                // 筛选该日期之后的除权事件
                var exRightsList = allExRights.Where(x => x.ExRightsDate > targetDate).ToList();
                
                decimal adjustedPrice = originalPrice;
                
                // 从最新日期向前计算
                foreach (var exRights in exRightsList.OrderByDescending(x => x.ExRightsDate))
                {
                    adjustedPrice = AdjustPriceForward(adjustedPrice, exRights);
                }
                
                result[targetDate] = adjustedPrice;
            }

            return result;
        }

        /// <summary>
        /// 批量计算OHLC前复权价格（超级优化版本）
        /// 一次性计算所有Open/High/Low/Close的前复权价格
        /// </summary>
        public Dictionary<DateTime, (decimal Open, decimal High, decimal Low, decimal Close)> BatchCalculateOHLCAdjustedPrices(
            string stockCode, 
            Dictionary<DateTime, (decimal Open, decimal High, decimal Low, decimal Close)> ohlcData)
        {
            var result = new Dictionary<DateTime, (decimal, decimal, decimal, decimal)>();
            if (ohlcData == null || ohlcData.Count == 0)
                return result;

            // 获取除权数据
            var allExRights = GetExRightsDataCached(stockCode);
            
            // 如果没有除权数据，直接返回原始价格（快速路径）
            if (allExRights.Count == 0)
            {
                // 性能优化：移除调试日志
                return new Dictionary<DateTime, (decimal, decimal, decimal, decimal)>(ohlcData);
            }
            
            // 性能优化：移除调试日志（仅在需要时启用）
            // 如需调试，可以取消下面的注释
            /*
            var exRightsInRange = allExRights.Where(x => 
                ohlcData.Keys.Any(d => d <= x.ExRightsDate && x.ExRightsDate <= ohlcData.Keys.Max())
            ).ToList();
            if (exRightsInRange.Count > 0)
            {
                Console.WriteLine($"[前复权调试] {stockCode}: 找到 {allExRights.Count} 条除权数据，其中 {exRightsInRange.Count} 条在数据范围内");
            }
            */

            // 性能优化：预先按日期降序排序除权数据（只排序一次）
            var sortedExRights = allExRights.OrderByDescending(x => x.ExRightsDate).ToList();
            
            // 性能优化：按日期排序K线数据，从旧到新处理
            var sortedOhlc = ohlcData.OrderBy(x => x.Key).ToList();
            
            // 为每个日期计算前复权OHLC
            foreach (var kvp in sortedOhlc)
            {
                DateTime targetDate = kvp.Key;
                var ohlc = kvp.Value;
                
                // 快速路径：如果最早的除权日期还在目标日期之前，说明没有除权影响
                if (sortedExRights.Count > 0 && sortedExRights[sortedExRights.Count - 1].ExRightsDate <= targetDate)
                {
                    result[targetDate] = ohlc;
                    continue;
                }
                
                // 一次性计算4个价格的复权值
                decimal adjOpen = ohlc.Open;
                decimal adjHigh = ohlc.High;
                decimal adjLow = ohlc.Low;
                decimal adjClose = ohlc.Close;
                
                // 从最新日期向前计算（只遍历该日期之后的除权事件）
                foreach (var exRights in sortedExRights)
                {
                    if (exRights.ExRightsDate <= targetDate)
                        break; // 已经到了该日期之前的除权，停止遍历
                    
                    adjOpen = AdjustPriceForward(adjOpen, exRights);
                    adjHigh = AdjustPriceForward(adjHigh, exRights);
                    adjLow = AdjustPriceForward(adjLow, exRights);
                    adjClose = AdjustPriceForward(adjClose, exRights);
                }
                
                result[targetDate] = (adjOpen, adjHigh, adjLow, adjClose);
            }

            return result;
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
