using System;
using System.Collections.Generic;
using MQReceiver.Models;
using MQReceiver.Cache;
using MQReceiver.Repositories;

namespace MQReceiver.Services
{
    /// <summary>
    /// 交易时段枚举
    /// </summary>
    public enum TradingSession
    {
        /// <summary>盘前 (交易日9:30之前)</summary>
        PreMarket,

        /// <summary>盘中 (9:30-11:30, 13:00-15:00)</summary>
        MarketOpen,

        /// <summary>午休 (11:30-13:00)</summary>
        LunchBreak,

        /// <summary>盘后 (15:00之后)</summary>
        AfterMarket,

        /// <summary>非交易日 (周末/节假日)</summary>
        NonTradingDay
    }

    /// <summary>
    /// 数据来源策略
    /// </summary>
    public enum DataSourceStrategy
    {
        /// <summary>仅使用数据库历史数据</summary>
        DatabaseOnly,

        /// <summary>数据库历史 + 实时数据合成当日K线</summary>
        DatabasePlusRealTime,

        /// <summary>优先使用实时数据，回退到数据库</summary>
        RealTimePreferred
    }

    /// <summary>
    /// 数据状态信息
    /// </summary>
    public class DataStatus
    {
        /// <summary>当前交易时段</summary>
        public TradingSession Session { get; set; }

        /// <summary>数据来源策略</summary>
        public DataSourceStrategy Strategy { get; set; }

        /// <summary>数据库最新交易日期</summary>
        public DateTime? LatestDbDate { get; set; }

        /// <summary>实时缓存数据时间</summary>
        public DateTime? RealTimeCacheTime { get; set; }

        /// <summary>实时缓存股票数量</summary>
        public int RealTimeCacheCount { get; set; }

        /// <summary>当日日线是否已入库</summary>
        public bool TodayDataInDb { get; set; }

        /// <summary>数据是否新鲜（可信赖）</summary>
        public bool IsDataFresh { get; set; }

        /// <summary>数据状态描述</summary>
        public string StatusDescription { get; set; }

        /// <summary>建议的目标日期</summary>
        public DateTime RecommendedTargetDate { get; set; }
    }

    /// <summary>
    /// 数据边界管理器
    /// 负责判断交易时段、决定数据来源策略、协调实时数据和历史数据
    /// </summary>
    public class DataBoundaryManager
    {
        private readonly TradingCalendar _tradingCalendar;
        private readonly RealTimeDataCache _realTimeCache;
        private DateTime? _cachedLatestDbDate;
        private DateTime _lastDbDateCheck = DateTime.MinValue;

        // 交易时间常量
        private static readonly TimeSpan MorningOpen = new TimeSpan(9, 30, 0);
        private static readonly TimeSpan MorningClose = new TimeSpan(11, 30, 0);
        private static readonly TimeSpan AfternoonOpen = new TimeSpan(13, 0, 0);
        private static readonly TimeSpan AfternoonClose = new TimeSpan(15, 0, 0);

        public DataBoundaryManager(RealTimeDataCache realTimeCache = null)
        {
            _tradingCalendar = new TradingCalendar();
            _realTimeCache = realTimeCache;
        }

        /// <summary>
        /// 获取当前交易时段
        /// </summary>
        public TradingSession GetCurrentSession()
        {
            return GetSession(DateTime.Now);
        }

        /// <summary>
        /// 获取指定时间的交易时段
        /// </summary>
        public TradingSession GetSession(DateTime dateTime)
        {
            // 先检查是否是交易日
            if (!_tradingCalendar.IsTradingDay(dateTime.Date))
            {
                return TradingSession.NonTradingDay;
            }

            TimeSpan currentTime = dateTime.TimeOfDay;

            if (currentTime < MorningOpen)
            {
                return TradingSession.PreMarket;
            }
            else if (currentTime >= MorningOpen && currentTime < MorningClose)
            {
                return TradingSession.MarketOpen;
            }
            else if (currentTime >= MorningClose && currentTime < AfternoonOpen)
            {
                return TradingSession.LunchBreak;
            }
            else if (currentTime >= AfternoonOpen && currentTime < AfternoonClose)
            {
                return TradingSession.MarketOpen;
            }
            else
            {
                return TradingSession.AfterMarket;
            }
        }

        /// <summary>
        /// 获取当前数据状态
        /// </summary>
        public DataStatus GetCurrentDataStatus(DateTime? latestDbDate = null)
        {
            DateTime now = DateTime.Now;
            TradingSession session = GetSession(now);

            // 更新数据库最新日期缓存
            if (latestDbDate.HasValue)
            {
                _cachedLatestDbDate = latestDbDate;
                _lastDbDateCheck = now;
            }

            var status = new DataStatus
            {
                Session = session,
                LatestDbDate = _cachedLatestDbDate,
                RealTimeCacheTime = _realTimeCache?.LastUpdateTime,
                RealTimeCacheCount = _realTimeCache?.Count ?? 0
            };

            // 判断当日数据是否已入库
            DateTime today = now.Date;
            status.TodayDataInDb = _cachedLatestDbDate.HasValue &&
                                   _cachedLatestDbDate.Value.Date >= today;

            // 根据时段决定数据来源策略
            switch (session)
            {
                case TradingSession.PreMarket:
                    // 盘前：使用昨天的数据
                    status.Strategy = DataSourceStrategy.DatabaseOnly;
                    status.RecommendedTargetDate = GetPreviousTradingDay(today);
                    status.IsDataFresh = true;
                    status.StatusDescription = "盘前时段，使用上一交易日数据";
                    break;

                case TradingSession.MarketOpen:
                case TradingSession.LunchBreak:
                    // 盘中/午休：合成当日数据
                    if (_realTimeCache != null && _realTimeCache.Count > 0)
                    {
                        status.Strategy = DataSourceStrategy.DatabasePlusRealTime;
                        status.RecommendedTargetDate = today;
                        status.IsDataFresh = !_realTimeCache.IsExpired(TimeSpan.FromMinutes(5));
                        status.StatusDescription = status.IsDataFresh
                            ? "盘中交易，使用实时数据合成当日K线"
                            : "盘中交易，实时数据可能延迟";
                    }
                    else
                    {
                        status.Strategy = DataSourceStrategy.DatabaseOnly;
                        status.RecommendedTargetDate = GetPreviousTradingDay(today);
                        status.IsDataFresh = false;
                        status.StatusDescription = "盘中交易，但无实时数据，使用上一交易日数据";
                    }
                    break;

                case TradingSession.AfterMarket:
                    // 盘后：检查当日数据是否已入库
                    if (status.TodayDataInDb)
                    {
                        status.Strategy = DataSourceStrategy.DatabaseOnly;
                        status.RecommendedTargetDate = today;
                        status.IsDataFresh = true;
                        status.StatusDescription = "盘后时段，当日日线已入库";
                    }
                    else if (_realTimeCache != null && _realTimeCache.Count > 0)
                    {
                        // 当日数据未入库，但有实时缓存
                        status.Strategy = DataSourceStrategy.DatabasePlusRealTime;
                        status.RecommendedTargetDate = today;
                        status.IsDataFresh = true;
                        status.StatusDescription = "盘后时段，当日日线未入库，使用实时数据补充";
                    }
                    else
                    {
                        status.Strategy = DataSourceStrategy.DatabaseOnly;
                        status.RecommendedTargetDate = GetPreviousTradingDay(today);
                        status.IsDataFresh = false;
                        status.StatusDescription = "盘后时段，当日数据不可用，使用上一交易日数据";
                    }
                    break;

                case TradingSession.NonTradingDay:
                default:
                    // 非交易日：使用最近的交易日数据
                    status.Strategy = DataSourceStrategy.DatabaseOnly;
                    status.RecommendedTargetDate = GetPreviousTradingDay(today);
                    status.IsDataFresh = true;
                    status.StatusDescription = "非交易日，使用最近交易日数据";
                    break;
            }

            return status;
        }

        /// <summary>
        /// 获取上一个交易日
        /// </summary>
        public DateTime GetPreviousTradingDay(DateTime date)
        {
            return _tradingCalendar.GetPreviousTradingDay(date);
        }

        /// <summary>
        /// 获取下一个交易日
        /// </summary>
        public DateTime GetNextTradingDay(DateTime date)
        {
            return _tradingCalendar.GetNextTradingDay(date);
        }

        /// <summary>
        /// 判断指定日期是否是交易日
        /// </summary>
        public bool IsTradingDay(DateTime date)
        {
            return _tradingCalendar.IsTradingDay(date);
        }

        /// <summary>
        /// 构造当日虚拟K线（用于盘中计算）
        /// 从实时数据构造一根日K线
        /// </summary>
        public DailyKlineData BuildTodayKlineFromRealTime(RealTimeDataRecord realTimeData)
        {
            if (realTimeData == null)
                return null;

            return new DailyKlineData
            {
                TradeDate = DateTime.Now.Date,
                Open = realTimeData.Open,
                High = realTimeData.High,
                Low = realTimeData.Low,
                Close = realTimeData.NewPrice,  // 使用最新价作为收盘价
                Volume = realTimeData.Volume
            };
        }

        /// <summary>
        /// 合并历史数据和当日实时数据
        /// </summary>
        public List<DailyKlineData> MergeHistoryAndRealTime(
            List<DailyKlineData> historyData,
            RealTimeDataRecord realTimeData,
            DateTime targetDate)
        {
            if (historyData == null)
                historyData = new List<DailyKlineData>();

            // 如果目标日期不是今天，直接返回历史数据
            if (targetDate.Date != DateTime.Now.Date)
                return historyData;

            // 如果没有实时数据，返回历史数据
            if (realTimeData == null)
                return historyData;

            // 检查历史数据中是否已有今天的数据
            bool hasTodayData = false;
            for (int i = 0; i < historyData.Count; i++)
            {
                if (historyData[i].TradeDate.Date == targetDate.Date)
                {
                    hasTodayData = true;
                    // 用实时数据更新今天的K线
                    historyData[i] = BuildTodayKlineFromRealTime(realTimeData);
                    break;
                }
            }

            // 如果历史数据中没有今天的数据，添加虚拟K线
            if (!hasTodayData)
            {
                var todayKline = BuildTodayKlineFromRealTime(realTimeData);
                if (todayKline != null)
                {
                    historyData.Add(todayKline);
                    // 按日期排序
                    historyData.Sort((a, b) => a.TradeDate.CompareTo(b.TradeDate));
                }
            }

            return historyData;
        }

        /// <summary>
        /// 获取时段的中文描述
        /// </summary>
        public static string GetSessionDescription(TradingSession session)
        {
            switch (session)
            {
                case TradingSession.PreMarket: return "盘前";
                case TradingSession.MarketOpen: return "交易中";
                case TradingSession.LunchBreak: return "午休";
                case TradingSession.AfterMarket: return "盘后";
                case TradingSession.NonTradingDay: return "休市";
                default: return "未知";
            }
        }

        /// <summary>
        /// 获取数据策略的中文描述
        /// </summary>
        public static string GetStrategyDescription(DataSourceStrategy strategy)
        {
            switch (strategy)
            {
                case DataSourceStrategy.DatabaseOnly: return "历史数据";
                case DataSourceStrategy.DatabasePlusRealTime: return "历史+实时";
                case DataSourceStrategy.RealTimePreferred: return "实时优先";
                default: return "未知";
            }
        }
    }
}
