using System;

namespace MQReceiver.Models
{
    /// <summary>
    /// 码表数据记录
    /// </summary>
    public class MarketTableDataRecord
    {
        /// <summary>
        /// 股票代码（标准化格式，如 "SH600000"）
        /// </summary>
        public string StockCode { get; set; }

        /// <summary>
        /// 股票名称
        /// </summary>
        public string StockName { get; set; }

        /// <summary>
        /// 市场代码（0=深圳, 1=上海）
        /// </summary>
        public int MarketCode { get; set; }

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdateTime { get; set; }
    }

    /// <summary>
    /// 日线数据记录
    /// </summary>
    public class DailyDataRecord
    {
        public string StockCode { get; set; }
        public ushort MarketCode { get; set; }
        public DateTime TradeDate { get; set; }
        public DateTime TradeDateTime { get; set; }
        public int TimeStamp { get; set; }
        public decimal OpenPrice { get; set; }
        public decimal HighPrice { get; set; }
        public decimal LowPrice { get; set; }
        public decimal ClosePrice { get; set; }
        public decimal Volume { get; set; }
        public decimal Amount { get; set; }
        public ushort AdvanceCount { get; set; }
        public ushort DeclineCount { get; set; }
    }

    /// <summary>
    /// 除权数据记录
    /// </summary>
    public class ExRightsDataRecord
    {
        public string StockCode { get; set; }
        public ushort MarketCode { get; set; }
        public DateTime ExRightsDate { get; set; }
        public DateTime ExRightsDateTime { get; set; }
        public int TimeStamp { get; set; }
        public decimal GivePer10Shares { get; set; }
        public decimal PeiPer10Shares { get; set; }
        public decimal PeiPrice { get; set; }
        public decimal ProfitPerShare { get; set; }
    }

    /// <summary>
    /// 实时数据记录
    /// </summary>
    public class RealTimeDataRecord
    {
        public string StockCode { get; set; }
        public string StockName { get; set; }
        public ushort MarketCode { get; set; }
        public DateTime UpdateTime { get; set; }
        public int TimeStamp { get; set; }

        // 价格数据
        public decimal LastClose { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal NewPrice { get; set; }

        // 成交数据
        public decimal Volume { get; set; }
        public decimal Amount { get; set; }

        // 买卖盘数据
        public decimal[] BuyPrice { get; set; }
        public decimal[] BuyVolume { get; set; }
        public decimal[] SellPrice { get; set; }
        public decimal[] SellVolume { get; set; }

        public RealTimeDataRecord()
        {
            BuyPrice = new decimal[5];
            BuyVolume = new decimal[5];
            SellPrice = new decimal[5];
            SellVolume = new decimal[5];
        }
    }
}
