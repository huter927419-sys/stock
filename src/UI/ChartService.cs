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
    /// </summary>
    public class ChartService
    {
        private readonly string connectionString;
        private readonly KDCalculator kdCalculator;

        public ChartService()
        {
            connectionString = DatabaseConnectionHelper.BuildConnectionString();
            kdCalculator = new KDCalculator();
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
        /// 加载日K线数据
        /// </summary>
        private List<Models.CandleDataPoint> LoadDailyKlineData(string stockCode, int days)
        {
            var result = new List<Models.CandleDataPoint>();
            DateTime endDate = DateTime.Now.Date;
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
                                result.Add(new Models.CandleDataPoint
                                {
                                    Date = reader.GetDateTime(0),
                                    Open = (double)reader.GetDecimal(1),
                                    High = (double)reader.GetDecimal(2),
                                    Low = (double)reader.GetDecimal(3),
                                    Close = (double)reader.GetDecimal(4),
                                    Volume = (double)reader.GetDecimal(5)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载日K线数据失败: {ex.Message}");
            }

            return result;
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
