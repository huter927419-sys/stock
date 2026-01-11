using System;
using System.Collections.Generic;
using System.Linq;
using MQReceiver.Cache;
using MQReceiver.Calculators;
using MQReceiver.Helpers;
using MQReceiver.Models;
using Npgsql;

namespace MQReceiver.Services
{
    /// <summary>
    /// 图表数据服务
    /// 负责从数据库加载K线数据和KD指标数据
    /// 支持合并实时数据显示当日K线
    /// </summary>
    public class ChartService
    {
        private readonly string connectionString;
        private readonly KDCalculator kdCalculator;
        private readonly ExRightsAdjustmentCalculator exRightsCalculator;
        private readonly RealTimeDataCache realTimeCache;

        public ChartService() : this(null)
        {
        }

        public ChartService(RealTimeDataCache cache)
        {
            connectionString = DatabaseConnectionHelper.BuildConnectionString();
            kdCalculator = new KDCalculator();
            exRightsCalculator = new ExRightsAdjustmentCalculator();
            realTimeCache = cache;
        }

        /// <summary>
        /// 加载股票图表数据
        /// </summary>
        /// <param name="stockCode">股票代码</param>
        /// <param name="days">加载最近多少天的数据（默认180天，约6个月）</param>
        public Models.ChartData LoadChartData(string stockCode, int days = 180)
        {
            var chartData = new Models.ChartData
            {
                StockCode = stockCode,
                StockName = GetStockName(stockCode)
            };

            try
            {
                // 1. 加载日K线数据
                chartData.DailyKline = LoadDailyKlineData(stockCode, days);

                // 2. 为每个交易日计算对应的周/月/季KD值（与日K线完全对齐）
                if (chartData.DailyKline.Count > 0)
                {
                    chartData.WeeklyKD = CalculateKDForEachTradingDay(stockCode, chartData.DailyKline, "week");
                    chartData.MonthlyKD = CalculateKDForEachTradingDay(stockCode, chartData.DailyKline, "month");
                    chartData.QuarterlyKD = CalculateKDForEachTradingDay(stockCode, chartData.DailyKline, "quarter");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载图表数据失败: {ex.Message}");
            }

            return chartData;
        }

        /// <summary>
        /// 为每个交易日计算对应的周/月/季KD值
        /// 这样KD数据可以与日K线完全对齐
        /// </summary>
        private List<Models.KDDataPoint> CalculateKDForEachTradingDay(string stockCode, List<Models.CandleDataPoint> dailyKline, string cycleType)
        {
            var result = new List<Models.KDDataPoint>();

            // 为了优化性能，使用缓存避免重复计算同一周期内的KD
            // 同一周/月/季内的KD值是相同的（因为周期数据还没变）
            string lastCycleKey = "";
            double lastK = 0, lastD = 0;

            foreach (var candle in dailyKline)
            {
                // 获取当前日期所属的周期键
                string cycleKey = GetCycleKey(candle.Date, cycleType);

                // 如果是同一个周期，复用上次的KD值（优化）
                if (cycleKey == lastCycleKey && result.Count > 0)
                {
                    result.Add(new Models.KDDataPoint
                    {
                        Date = candle.Date,
                        K = lastK,
                        D = lastD
                    });
                    continue;
                }

                // 计算该日期的KD值
                KDResult kdResult = null;
                switch (cycleType.ToLower())
                {
                    case "week":
                        kdResult = kdCalculator.CalculateWeeklyKD(stockCode, candle.Date);
                        break;
                    case "month":
                        kdResult = kdCalculator.CalculateMonthlyKD(stockCode, candle.Date);
                        break;
                    case "quarter":
                        kdResult = kdCalculator.CalculateQuarterlyKD(stockCode, candle.Date);
                        break;
                }

                if (kdResult != null)
                {
                    lastK = (double)kdResult.K;
                    lastD = (double)kdResult.D;
                    lastCycleKey = cycleKey;

                    result.Add(new Models.KDDataPoint
                    {
                        Date = candle.Date,
                        K = lastK,
                        D = lastD
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// 获取日期所属的周期键（用于优化，判断是否在同一周期内）
        /// </summary>
        private string GetCycleKey(DateTime date, string cycleType)
        {
            switch (cycleType.ToLower())
            {
                case "week":
                    // 获取该周的周一日期作为键
                    int daysFromMonday = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
                    DateTime monday = date.AddDays(-daysFromMonday);
                    return $"W{monday:yyyyMMdd}";
                case "month":
                    return $"M{date:yyyyMM}";
                case "quarter":
                    int quarter = (date.Month - 1) / 3 + 1;
                    return $"Q{date.Year}{quarter}";
                default:
                    return date.ToString("yyyyMMdd");
            }
        }

        /// <summary>
        /// 加载日K线数据（使用前复权价格）
        /// 如果有实时数据且当日数据库无数据，则添加实时K线
        /// </summary>
        private List<Models.CandleDataPoint> LoadDailyKlineData(string stockCode, int days)
        {
            var result = new List<Models.CandleDataPoint>();
            DateTime today = DateTime.Now.Date;
            DateTime endDate = today;
            DateTime startDate = endDate.AddDays(-days);

            try
            {
                using (var conn = new NpgsqlConnection(connectionString))
                {
                    conn.Open();
                    string sql = @"
                        SELECT trade_date, open_price, high_price, low_price, close_price, volume
                        FROM stock_daily_data
                        WHERE stock_code = @stock_code
                          AND trade_date >= @start_date
                          AND trade_date <= @end_date
                        ORDER BY trade_date ASC";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@stock_code", stockCode);
                        cmd.Parameters.AddWithValue("@start_date", startDate);
                        cmd.Parameters.AddWithValue("@end_date", endDate);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                DateTime tradeDate = reader.GetDateTime(0);
                                decimal openPrice = reader.GetDecimal(1);
                                decimal highPrice = reader.GetDecimal(2);
                                decimal lowPrice = reader.GetDecimal(3);
                                decimal closePrice = reader.GetDecimal(4);
                                decimal volume = reader.GetDecimal(5);

                                // 计算前复权价格
                                decimal adjOpen = exRightsCalculator.CalculateForwardAdjustedPrice(stockCode, tradeDate, openPrice);
                                decimal adjHigh = exRightsCalculator.CalculateForwardAdjustedPrice(stockCode, tradeDate, highPrice);
                                decimal adjLow = exRightsCalculator.CalculateForwardAdjustedPrice(stockCode, tradeDate, lowPrice);
                                decimal adjClose = exRightsCalculator.CalculateForwardAdjustedPrice(stockCode, tradeDate, closePrice);

                                result.Add(new Models.CandleDataPoint
                                {
                                    Date = tradeDate,
                                    Open = (double)adjOpen,
                                    High = (double)adjHigh,
                                    Low = (double)adjLow,
                                    Close = (double)adjClose,
                                    Volume = (double)volume
                                });
                            }
                        }
                    }
                }

                // 尝试添加实时数据作为当日K线
                AppendRealTimeCandle(result, stockCode, today);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载日K线数据失败: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 将实时数据添加为当日K线（仅当今日是交易日且数据库中没有当日数据）
        /// </summary>
        private void AppendRealTimeCandle(List<Models.CandleDataPoint> result, string stockCode, DateTime today)
        {
            if (realTimeCache == null)
                return;

            var realTimeData = realTimeCache.GetData(stockCode);
            if (realTimeData == null)
                return;

            // 检查实时数据是否有效（有价格数据）
            if (realTimeData.Open <= 0 || realTimeData.NewPrice <= 0)
                return;

            // 检查数据库中是否已有今日数据
            bool hasTodayData = result.Count > 0 && result[result.Count - 1].Date.Date == today;

            if (hasTodayData)
            {
                // 更新今日K线数据（用实时数据覆盖）
                var todayCandle = result[result.Count - 1];
                todayCandle.High = Math.Max(todayCandle.High, (double)realTimeData.High);
                todayCandle.Low = Math.Min(todayCandle.Low, (double)realTimeData.Low);
                todayCandle.Close = (double)realTimeData.NewPrice;
                todayCandle.Volume = (double)realTimeData.Volume;
            }
            else
            {
                // 仅在今日是工作日时添加实时数据（避免在周末/节假日添加上一交易日的重复数据）
                // 如果今日是周末或数据库中没有任何近期数据，则不添加
                if (today.DayOfWeek == DayOfWeek.Saturday || today.DayOfWeek == DayOfWeek.Sunday)
                    return;

                // 检查最后一个交易日是否是昨天（如果不是，说明今天可能是节假日）
                if (result.Count > 0)
                {
                    var lastTradeDate = result[result.Count - 1].Date.Date;
                    var daysSinceLastTrade = (today - lastTradeDate).Days;

                    // 如果距离上一个交易日超过3天，很可能今天是长假期，不添加实时数据
                    if (daysSinceLastTrade > 3)
                        return;
                }

                // 添加新的今日K线（使用实时数据）
                result.Add(new Models.CandleDataPoint
                {
                    Date = today,
                    Open = (double)realTimeData.Open,
                    High = (double)realTimeData.High,
                    Low = (double)realTimeData.Low,
                    Close = (double)realTimeData.NewPrice,
                    Volume = (double)realTimeData.Volume
                });
            }
        }

        /// <summary>
        /// 加载KD指标序列
        /// </summary>
        private List<Models.KDDataPoint> LoadKDSequence(string stockCode, DateTime startDate, DateTime endDate, string cycleType, int maxPeriods)
        {
            var result = new List<Models.KDDataPoint>();

            try
            {
                // 使用KDCalculator获取历史序列
                List<KDResult> kdSequence = null;

                switch (cycleType.ToLower())
                {
                    case "week":
                        kdSequence = kdCalculator.GetWeeklyKDSequence(stockCode, endDate, maxPeriods);
                        break;
                    case "month":
                        kdSequence = kdCalculator.GetMonthlyKDSequence(stockCode, endDate, maxPeriods);
                        break;
                    case "quarter":
                        kdSequence = kdCalculator.GetQuarterlyKDSequence(stockCode, endDate, maxPeriods);
                        break;
                }

                if (kdSequence != null)
                {
                    foreach (var kd in kdSequence)
                    {
                        if (kd.Date >= startDate && kd.Date <= endDate)
                        {
                            result.Add(new Models.KDDataPoint
                            {
                                Date = kd.Date,
                                K = (double)kd.K,
                                D = (double)kd.D
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载{cycleType}KD序列失败: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 获取股票名称（从内存缓存获取，性能更好）
        /// </summary>
        private string GetStockName(string stockCode)
        {
            return StockInfoCache.Instance.GetStockName(stockCode);
        }
    }
}
