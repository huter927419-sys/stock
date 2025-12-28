using System;
using System.Collections.Generic;

namespace MQReceiver.Helpers
{
    /// <summary>
    /// 股票数据解析器
    /// 封装各类股票数据的JSON解析逻辑
    /// </summary>
    public static class StockDataParser
    {
        /// <summary>
        /// 解析日线数据JSON
        /// </summary>
        public static List<DailyDataRecord> ParseDailyData(string json)
        {
            return JsonParserHelper.ParseRecordsArray(json, ParseDailyRecord);
        }

        /// <summary>
        /// 解析实时数据JSON
        /// </summary>
        public static List<RealTimeDataRecord> ParseRealTimeData(string json)
        {
            return JsonParserHelper.ParseRecordsArray(json, ParseRealTimeRecord);
        }

        /// <summary>
        /// 解析除权数据JSON
        /// </summary>
        public static List<ExRightsDataRecord> ParseExRightsData(string json)
        {
            return JsonParserHelper.ParseRecordsArray(json, ParseExRightsRecord);
        }

        /// <summary>
        /// 解析码表数据JSON
        /// </summary>
        public static List<Models.MarketTableDataRecord> ParseMarketTableData(string json)
        {
            return JsonParserHelper.ParseRecordsArray(json, ParseMarketTableRecord);
        }

        /// <summary>
        /// 解析单条日线数据记录
        /// </summary>
        private static DailyDataRecord ParseDailyRecord(string recordJson)
        {
            try
            {
                var record = new DailyDataRecord
                {
                    StockCode = JsonParserHelper.ExtractStringValue(recordJson, "stock_code"),
                    MarketCode = JsonParserHelper.ExtractUShortValue(recordJson, "market_code"),
                    TradeDate = JsonParserHelper.ExtractDateValue(recordJson, "trade_date"),
                    TradeDateTime = JsonParserHelper.ExtractDateTimeValue(recordJson, "trade_datetime"),
                    TimeStamp = JsonParserHelper.ExtractIntValue(recordJson, "time_stamp"),
                    OpenPrice = JsonParserHelper.ExtractDecimalValue(recordJson, "open_price"),
                    HighPrice = JsonParserHelper.ExtractDecimalValue(recordJson, "high_price"),
                    LowPrice = JsonParserHelper.ExtractDecimalValue(recordJson, "low_price"),
                    ClosePrice = JsonParserHelper.ExtractDecimalValue(recordJson, "close_price"),
                    Volume = JsonParserHelper.ExtractDecimalValue(recordJson, "volume"),
                    Amount = JsonParserHelper.ExtractDecimalValue(recordJson, "amount")
                };

                var advanceCount = JsonParserHelper.ExtractNullableUShortValue(recordJson, "advance_count");
                if (advanceCount.HasValue)
                    record.AdvanceCount = advanceCount.Value;

                var declineCount = JsonParserHelper.ExtractNullableUShortValue(recordJson, "decline_count");
                if (declineCount.HasValue)
                    record.DeclineCount = declineCount.Value;

                return record;
            }
            catch (Exception ex)
            {
                Console.WriteLine("解析日线数据记录失败: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 解析单条实时数据记录
        /// </summary>
        private static RealTimeDataRecord ParseRealTimeRecord(string recordJson)
        {
            try
            {
                return new RealTimeDataRecord
                {
                    StockCode = JsonParserHelper.ExtractStringValue(recordJson, "stock_code"),
                    StockName = JsonParserHelper.ExtractStringValue(recordJson, "stock_name"),
                    MarketCode = JsonParserHelper.ExtractUShortValue(recordJson, "market_code"),
                    UpdateTime = JsonParserHelper.ExtractDateTimeValue(recordJson, "update_time"),
                    TimeStamp = JsonParserHelper.ExtractIntValue(recordJson, "time_stamp"),
                    LastClose = JsonParserHelper.ExtractDecimalValue(recordJson, "last_close"),
                    Open = JsonParserHelper.ExtractDecimalValue(recordJson, "open"),
                    High = JsonParserHelper.ExtractDecimalValue(recordJson, "high"),
                    Low = JsonParserHelper.ExtractDecimalValue(recordJson, "low"),
                    NewPrice = JsonParserHelper.ExtractDecimalValue(recordJson, "new_price"),
                    Volume = JsonParserHelper.ExtractDecimalValue(recordJson, "volume"),
                    Amount = JsonParserHelper.ExtractDecimalValue(recordJson, "amount"),
                    BuyPrice = JsonParserHelper.ExtractDecimalArray(recordJson, "buy_price"),
                    BuyVolume = JsonParserHelper.ExtractDecimalArray(recordJson, "buy_volume"),
                    SellPrice = JsonParserHelper.ExtractDecimalArray(recordJson, "sell_price"),
                    SellVolume = JsonParserHelper.ExtractDecimalArray(recordJson, "sell_volume")
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine("解析实时数据记录失败: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 解析单条除权数据记录
        /// </summary>
        private static ExRightsDataRecord ParseExRightsRecord(string recordJson)
        {
            try
            {
                return new ExRightsDataRecord
                {
                    StockCode = JsonParserHelper.ExtractStringValue(recordJson, "stock_code"),
                    MarketCode = JsonParserHelper.ExtractUShortValue(recordJson, "market_code"),
                    ExRightsDate = JsonParserHelper.ExtractDateValue(recordJson, "ex_rights_date"),
                    ExRightsDateTime = JsonParserHelper.ExtractDateTimeValue(recordJson, "ex_rights_datetime"),
                    TimeStamp = JsonParserHelper.ExtractIntValue(recordJson, "time_stamp"),
                    GivePer10Shares = JsonParserHelper.ExtractDecimalValue(recordJson, "give_per_10_shares"),
                    PeiPer10Shares = JsonParserHelper.ExtractDecimalValue(recordJson, "pei_per_10_shares"),
                    PeiPrice = JsonParserHelper.ExtractDecimalValue(recordJson, "pei_price"),
                    ProfitPerShare = JsonParserHelper.ExtractDecimalValue(recordJson, "profit_per_share")
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine("解析除权数据记录失败: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 解析单条码表数据记录
        /// </summary>
        private static Models.MarketTableDataRecord ParseMarketTableRecord(string recordJson)
        {
            try
            {
                return new Models.MarketTableDataRecord
                {
                    StockCode = JsonParserHelper.ExtractStringValue(recordJson, "stock_code"),
                    StockName = JsonParserHelper.ExtractStringValue(recordJson, "stock_name"),
                    MarketCode = JsonParserHelper.ExtractIntValue(recordJson, "market_code"),
                    UpdateTime = JsonParserHelper.ExtractDateTimeValue(recordJson, "update_time")
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine("解析码表数据记录失败: " + ex.Message);
                return null;
            }
        }
    }
}
