using System;

namespace MQReceiver.Models
{
    /// <summary>
    /// KD指标计算结果
    /// </summary>
    public class KDResult
    {
        public string StockCode { get; set; }
        public DateTime Date { get; set; }
        public decimal K { get; set; }
        public decimal D { get; set; }
        public decimal RSV { get; set; }

        /// <summary>
        /// 是否金叉：K > D
        /// </summary>
        public bool IsGoldenCross => K > D;

        /// <summary>
        /// 是否死叉：K < D
        /// </summary>
        public bool IsDeadCross => K < D;
    }

    /// <summary>
    /// 聚合K线数据（用于周、月、季线计算）
    /// </summary>
    public class AggregatedCandle
    {
        public DateTime Date { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }
    }
}
