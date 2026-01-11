namespace MQReceiver.Models
{
    /// <summary>
    /// 带历史K值的过滤结果
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
    }
}
