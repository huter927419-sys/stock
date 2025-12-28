using System;
using System.Collections.Generic;
using System.Linq;
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

                // 2. 加载KD指标数据
                DateTime endDate = DateTime.Now.Date;
                DateTime startDate = endDate.AddDays(-days);

                // 加载周KD序列（最近20周）
                chartData.WeeklyKD = LoadKDSequence(stockCode, startDate, endDate, "week", 20);

                // 加载月KD序列（最近12个月）
                chartData.MonthlyKD = LoadKDSequence(stockCode, startDate, endDate, "month", 12);

                // 加载季KD序列（最近8个季度）
                chartData.QuarterlyKD = LoadKDSequence(stockCode, startDate, endDate, "quarter", 8);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载图表数据失败: {ex.Message}");
            }

            return chartData;
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
        /// 获取股票名称
        /// </summary>
        private string GetStockName(string stockCode)
        {
            try
            {
                using (var conn = new NpgsqlConnection(connectionString))
                {
                    conn.Open();
                    string sql = @"
                        SELECT stock_name
                        FROM stock_realtime_data
                        WHERE stock_code = @stock_code
                        LIMIT 1";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@stock_code", stockCode);
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            return result.ToString();
                        }
                    }
                }
            }
            catch
            {
                // 忽略错误，返回股票代码
            }

            return stockCode;
        }
    }
}
