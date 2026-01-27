namespace MQReceiver.Models
{
    /// <summary>
    /// 带历史K值的计算结果
    /// </summary>
    public class FilterResultWithHistory
    {
        public string StockCode { get; set; }
        public string StockName { get; set; }

        /// <summary>
        /// 涨幅（百分比，如 5.23 表示 5.23%）
        /// 今日有交易：今日收盘价相对昨日收盘价的涨幅
        /// 今日无交易：最近交易日的涨幅
        /// </summary>
        public decimal? PriceChangePercent { get; set; }

        /// <summary>
        /// 涨幅显示文本（带%符号）
        /// </summary>
        public string PriceChangeDisplay
        {
            get
            {
                if (PriceChangePercent.HasValue)
                    return $"{PriceChangePercent.Value:F2}%";  // 显示2位小数
                return "--";
            }
        }

        /// <summary>
        /// 涨幅颜色（红涨绿跌）
        /// </summary>
        public string PriceChangeColor
        {
            get
            {
                if (PriceChangePercent.HasValue)
                {
                    if (PriceChangePercent.Value > 0)
                        return "#FF4444";  // 红色 - 上涨
                    else if (PriceChangePercent.Value < 0)
                        return "#00CC66";  // 绿色 - 下跌
                    return "#CCCCCC";  // 灰色 - 平盘
                }
                return "#CCCCCC";  // 灰色 - 无数据
            }
        }

        public decimal WeeklyK { get; set; }
        public decimal MonthlyK { get; set; }
        public decimal QuarterlyK { get; set; }

        /// <summary>
        /// 昨天的周K值（用于颜色显示）
        /// </summary>
        public decimal? YesterdayWeeklyK { get; set; }

        /// <summary>
        /// 昨天的月K值（用于颜色显示）
        /// </summary>
        public decimal? YesterdayMonthlyK { get; set; }

        /// <summary>
        /// 昨天的季K值（用于颜色显示）
        /// </summary>
        public decimal? YesterdayQuarterlyK { get; set; }

        /// <summary>
        /// 周K值颜色（红涨绿跌）
        /// </summary>
        public string WeeklyKColor
        {
            get
            {
                if (YesterdayWeeklyK.HasValue)
                {
                    if (WeeklyK > YesterdayWeeklyK.Value)
                        return "#FF4444";  // 红色 - K值上涨
                    else if (WeeklyK < YesterdayWeeklyK.Value)
                        return "#00CC66";  // 绿色 - K值下跌
                }
                return "#CCCCCC";  // 灰色 - 无法比较
            }
        }

        /// <summary>
        /// 月K值颜色（红涨绿跌）
        /// </summary>
        public string MonthlyKColor
        {
            get
            {
                if (YesterdayMonthlyK.HasValue)
                {
                    if (MonthlyK > YesterdayMonthlyK.Value)
                        return "#FF4444";  // 红色 - K值上涨
                    else if (MonthlyK < YesterdayMonthlyK.Value)
                        return "#00CC66";  // 绿色 - K值下跌
                }
                return "#CCCCCC";  // 灰色 - 无法比较
            }
        }

        /// <summary>
        /// 季K值颜色（红涨绿跌）
        /// </summary>
        public string QuarterlyKColor
        {
            get
            {
                if (YesterdayQuarterlyK.HasValue)
                {
                    if (QuarterlyK > YesterdayQuarterlyK.Value)
                        return "#FF4444";  // 红色 - K值上涨
                    else if (QuarterlyK < YesterdayQuarterlyK.Value)
                        return "#00CC66";  // 绿色 - K值下跌
                }
                return "#CCCCCC";  // 灰色 - 无法比较
            }
        }
    }
}
