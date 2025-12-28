using System;
using System.Collections.Generic;

namespace MQReceiver.Models
{
    /// <summary>
    /// K线数据点
    /// </summary>
    public class CandleDataPoint
    {
        public DateTime Date { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public double Volume { get; set; }

        /// <summary>
        /// 是否上涨（中国习惯：收盘价 >= 开盘价为上涨，显示红色）
        /// </summary>
        public bool IsRising => Close >= Open;
    }

    /// <summary>
    /// KD指标数据点
    /// </summary>
    public class KDDataPoint
    {
        public DateTime Date { get; set; }
        public double K { get; set; }
        public double D { get; set; }

        /// <summary>
        /// 是否金叉：K > D
        /// </summary>
        public bool IsGoldenCross => K > D;
    }

    /// <summary>
    /// 图表数据集合
    /// </summary>
    public class ChartData
    {
        public string StockCode { get; set; }
        public string StockName { get; set; }

        /// <summary>
        /// 日K线数据
        /// </summary>
        public List<CandleDataPoint> DailyKline { get; set; }

        /// <summary>
        /// 周KD指标序列
        /// </summary>
        public List<KDDataPoint> WeeklyKD { get; set; }

        /// <summary>
        /// 月KD指标序列
        /// </summary>
        public List<KDDataPoint> MonthlyKD { get; set; }

        /// <summary>
        /// 季KD指标序列
        /// </summary>
        public List<KDDataPoint> QuarterlyKD { get; set; }

        public ChartData()
        {
            DailyKline = new List<CandleDataPoint>();
            WeeklyKD = new List<KDDataPoint>();
            MonthlyKD = new List<KDDataPoint>();
            QuarterlyKD = new List<KDDataPoint>();
        }
    }
}
