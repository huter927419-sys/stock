namespace MQReceiver.Models
{
    /// <summary>
    /// 带历史K值的过滤结果
    /// </summary>
    public class FilterResultWithHistory
    {
        public string StockCode { get; set; }
        public string StockName { get; set; }
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
