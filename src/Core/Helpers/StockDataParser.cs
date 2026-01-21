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
        /// 非A股代码黑名单（指数、基金、B股等）
        /// 这些代码即使符合A股格式，也不应该被处理
        /// </summary>
        private static readonly HashSet<string> InvalidStockCodes = new HashSet<string>
        {
            // ===== 指数代码（000000-000099，极高风险）=====
            "000001", // 上证指数
            "000016", // 上证50
            "000300", // 沪深300
            "000038", // 上证金融指数
            "000091", // 沪财中小指数
            
            // ===== 债券指数（000100-000199）=====
            "000101", // 上证5年期信用债指数 ⚠️ 已确认
            "000102", // 上证投资品指数
            "000103", // 上证国债指数
            "000104", // 上证金融债指数
            "000105", // 上证企业债指数
            "000106", // 上证短期企业债指数
            "000107", // 上证长期企业债指数
            "000108", // 上证公司债指数
            
            // ===== 其他指数 =====
            "000110", // 380金融指数
            "000132", // 上证100指数
            "000137", // 380高贝指数
            "000146", // 优势制造指数
            "000807", // 食品饮料指数
            "000905", // 中证500
            "000906", // 中证800
            "000914", // 300金融指数
            "000985", // 中证全指
            "000991", // 全指医药
            "000992", // 全指金融
            "000993", // 全指能源
            
            // ===== 基金/ETF代码 =====
            "000071", // 华夏恒生ETF联接A
            "000076", // 华夏恒生ETF联接现钞
            "000139", // 富国国企债基金
            "000974", // 安信消费医药股票A
            
            // ===== 已退市股票 =====
            "000018", // 神城A退
            "000033", // 已退市
            "000046", // *ST泛海（已退市2024-02-07）
            "000052", // 已退市
            "000053", // 已退市
            "000073", // 已退市
            "000077", // 已退市
            "000816", // 慧业退
            
            // ===== 其他特殊代码 =====
            "000161", // 特殊代码
            "000847", // 特殊代码
            "000854", // 特殊代码
        };

        /// <summary>
        /// 验证股票代码是否为有效的A股代码
        /// 排除指数、B股、北交所等
        /// 采用智能规则过滤，减少对黑名单的依赖
        /// </summary>
        public static bool IsValidStockCode(string stockCode)
        {
            if (string.IsNullOrEmpty(stockCode) || stockCode.Length != 6)
                return false;

            // 规则1：000000-000199范围，极高概率是指数/债券指数
            if (stockCode.StartsWith("000"))
            {
                if (int.TryParse(stockCode, out int code))
                {
                    if (code < 200)
                    {
                        // 000000-000199 几乎全是指数和债券指数
                        return false;
                    }
                }
            }

            // 规则2：检查黑名单（已知的特殊情况）
            if (InvalidStockCodes.Contains(stockCode))
            {
                return false;
            }

            // 规则3：B股代码（200xxx, 900xxx）
            if (stockCode.StartsWith("200") || stockCode.StartsWith("900"))
            {
                return false;
            }

            // 规则4：上海A股：600xxx, 601xxx, 603xxx, 605xxx
            if (stockCode.StartsWith("60"))
                return true;

            // 规则5：深圳A股：000xxx, 001xxx, 002xxx, 003xxx
            // 注意：000000-000199已被规则1过滤
            if (stockCode.StartsWith("00") || stockCode.StartsWith("001") ||
                stockCode.StartsWith("002") || stockCode.StartsWith("003"))
                return true;

            // 规则6：创业板：300xxx, 301xxx
            if (stockCode.StartsWith("30"))
                return true;

            // 规则7：科创板：688xxx, 689xxx
            if (stockCode.StartsWith("68"))
                return true;

            return false;
        }

        /// <summary>
        /// 检查是否为ST股票
        /// ST股票包括：*ST、ST、S*ST等
        /// </summary>
        public static bool IsSTStock(string stockName)
        {
            if (string.IsNullOrEmpty(stockName))
                return false;
            
            // 检查是否包含ST标记（不区分大小写）
            string upperName = stockName.ToUpper();
            return upperName.Contains("ST") || upperName.Contains("*ST") || 
                   upperName.StartsWith("ST") || upperName.StartsWith("*ST");
        }

        /// <summary>
        /// 检查股票名称是否包含非A股关键词
        /// 用于辅助判断是否为指数、债券、基金等
        /// </summary>
        public static bool IsNonAStockByName(string stockName)
        {
            if (string.IsNullOrEmpty(stockName))
                return false;
            
            // 指数类关键词
            if (stockName.Contains("指数") || stockName.Contains("指标"))
                return true;
            
            // 债券类关键词
            if (stockName.Contains("债") || stockName.Contains("债券"))
                return true;
            
            // 基金类关键词
            if (stockName.Contains("基金") || stockName.Contains("ETF") || 
                stockName.Contains("LOF") || stockName.Contains("QDII"))
                return true;
            
            // 退市股票
            if (stockName.Contains("退"))
                return true;
            
            return false;
        }
        
        /// <summary>
        /// 综合判断：根据代码和名称判断是否为有效A股
        /// 这是最严格的验证方法
        /// </summary>
        public static bool IsValidAStock(string stockCode, string stockName)
        {
            // 先检查代码
            if (!IsValidStockCode(stockCode))
                return false;
            
            // 再检查名称
            if (IsNonAStockByName(stockName))
                return false;
            
            return true;
        }

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
