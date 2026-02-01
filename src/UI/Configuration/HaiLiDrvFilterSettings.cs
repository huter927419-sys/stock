using System;

namespace MQReceiver.UI.Configuration
{
    /// <summary>
    /// HaiLiDrv 数据筛选条件（与 MairuiStockMonitor/龙卷风版 完全一致）
    /// 涨幅筛选、日内涨幅筛选，多条件为 OR 关系。
    /// </summary>
    [Serializable]
    public class HaiLiDrvFilterSettings
    {
        public bool EnableChangePercentFilter { get; set; }
        public decimal MinChangePercent { get; set; }
        public bool EnableIntradayChangeFilter { get; set; }
        public decimal MinIntradayChangePercent { get; set; }

        public HaiLiDrvFilterSettings()
        {
            EnableChangePercentFilter = false;
            MinChangePercent = 3.0m;
            EnableIntradayChangeFilter = false;
            MinIntradayChangePercent = 5.0m;
        }

        /// <summary>
        /// 检查是否通过筛选（OR 关系：满足其一即通过）
        /// </summary>
        public bool PassFilter(double changePercent, double newPrice, double open)
        {
            if (!EnableChangePercentFilter && !EnableIntradayChangeFilter)
                return true;

            bool passChangePercent = false;
            bool passIntradayChange = false;

            if (EnableChangePercentFilter)
                passChangePercent = (decimal)changePercent >= MinChangePercent;

            if (EnableIntradayChangeFilter && open > 0)
            {
                double intradayChange = ((newPrice - open) / open) * 100;
                passIntradayChange = (decimal)intradayChange >= MinIntradayChangePercent;
            }

            if (EnableChangePercentFilter && EnableIntradayChangeFilter)
                return passChangePercent || passIntradayChange;
            if (EnableChangePercentFilter)
                return passChangePercent;
            if (EnableIntradayChangeFilter)
                return passIntradayChange;
            return true;
        }

        public HaiLiDrvFilterSettings Clone()
        {
            return new HaiLiDrvFilterSettings
            {
                EnableChangePercentFilter = EnableChangePercentFilter,
                MinChangePercent = MinChangePercent,
                EnableIntradayChangeFilter = EnableIntradayChangeFilter,
                MinIntradayChangePercent = MinIntradayChangePercent
            };
        }
    }
}
