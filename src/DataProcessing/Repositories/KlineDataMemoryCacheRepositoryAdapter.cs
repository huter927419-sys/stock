using System;
using System.Collections.Generic;
using System.Linq;
using MQReceiver.Cache;
using MQReceiver.Repositories;

namespace MQReceiver.DataProcessing.Repositories
{
    /// <summary>
    /// 将 KlineDataMemoryCache 适配为 IKlineDataRepository，供批量 KD 计算从内存读数据，避免预加载后仍反复读库。
    /// </summary>
    public class KlineDataMemoryCacheRepositoryAdapter : IKlineDataRepository
    {
        private readonly KlineDataMemoryCache _cache;

        public KlineDataMemoryCacheRepositoryAdapter(KlineDataMemoryCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public List<DailyKlineData> GetDailyData(string stockCode, DateTime startDate, DateTime endDate)
        {
            var records = _cache.GetDailyData(stockCode, startDate, endDate);
            if (records == null || records.Count == 0)
                return new List<DailyKlineData>();
            return records.Select(r => new DailyKlineData
            {
                TradeDate = r.TradeDate,
                Open = r.OpenPrice,
                High = r.HighPrice,
                Low = r.LowPrice,
                Close = r.ClosePrice,
                Volume = r.Volume,
                Amount = r.Amount,
                TurnoverRate = r.TurnoverRate
            }).ToList();
        }

        public List<DailyKlineData> GetLatestDailyData(string stockCode, int count)
        {
            var range = GetDataDateRange(stockCode);
            if (!range.EndDate.HasValue) return new List<DailyKlineData>();
            var start = range.StartDate ?? range.EndDate.Value.AddYears(-2);
            var all = GetDailyData(stockCode, start, range.EndDate.Value);
            return all.OrderByDescending(d => d.TradeDate).Take(count).OrderBy(d => d.TradeDate).ToList();
        }

        public bool HasData(string stockCode)
        {
            var range = GetDataDateRange(stockCode);
            return range.StartDate.HasValue;
        }

        public (DateTime? StartDate, DateTime? EndDate) GetDataDateRange(string stockCode)
        {
            return _cache.GetDataDateRange(stockCode);
        }

        public int UpdateDailyData(List<KlineData> dataList) => 0;

        public (decimal? Amount, decimal? TurnoverRate) GetYesterdayAmountAndTurnoverRate(string stockCode, DateTime tradeDate)
        {
            var data = GetDailyData(stockCode, tradeDate.Date, tradeDate.Date);
            if (data == null)
                return (null, null);
            var r = data.FirstOrDefault();
            return r != null ? (r.Amount, r.TurnoverRate) : (null, null);
        }

        public Dictionary<string, (decimal? Amount, decimal? TurnoverRate)> GetYesterdayAmountAndTurnoverRateBatch(List<string> stockCodes, DateTime tradeDate)
        {
            var d = new Dictionary<string, (decimal? Amount, decimal? TurnoverRate)>();
            foreach (var code in stockCodes ?? new List<string>())
                d[code] = GetYesterdayAmountAndTurnoverRate(code, tradeDate);
            return d;
        }

        public Dictionary<string, decimal?> GetYesterdayAmountBatch(List<string> stockCodes, DateTime tradeDate)
        {
            var d = new Dictionary<string, decimal?>();
            foreach (var code in stockCodes ?? new List<string>())
            {
                var (amount, _) = GetYesterdayAmountAndTurnoverRate(code, tradeDate);
                d[code] = amount;
            }
            return d;
        }
    }
}
