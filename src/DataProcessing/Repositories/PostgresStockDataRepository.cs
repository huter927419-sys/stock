using System;
using System.Collections.Generic;
using System.Linq;
using MQReceiver.Helpers;
using Npgsql;
using NpgsqlTypes;

namespace MQReceiver.Repositories
{
    /// <summary>
    /// PostgreSQL 股票数据仓储实现
    /// 统一的数据访问层，合并读写功能
    /// </summary>
    public class PostgresStockDataRepository : IStockDataRepository, IKlineDataRepository
    {
        private readonly string _connectionString;

        public PostgresStockDataRepository()
        {
            _connectionString = DatabaseConnectionHelper.BuildConnectionString();
        }

        public PostgresStockDataRepository(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        #region IStockDataRepository 实现

        /// <summary>
        /// 测试数据库连接
        /// </summary>
        public bool TestConnection()
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("SELECT 1", connection))
                    {
                        command.ExecuteScalar();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("数据库连接测试失败: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 保存日线数据到数据库
        /// </summary>
        public int SaveDailyData(List<DailyDataRecord> records)
        {
            if (records == null || records.Count == 0)
                return 0;

            string insertSql = @"
                INSERT INTO stock_daily_data (
                    stock_code, market_code, trade_date, trade_datetime, time_stamp,
                    open_price, high_price, low_price, close_price,
                    volume, amount, advance_count, decline_count, data_source
                ) VALUES (
                    @stock_code, @market_code, @trade_date, @trade_datetime, @time_stamp,
                    @open_price, @high_price, @low_price, @close_price,
                    @volume, @amount, @advance_count, @decline_count, @data_source
                ) ON CONFLICT (stock_code, trade_date) DO UPDATE SET
                    open_price = EXCLUDED.open_price,
                    high_price = EXCLUDED.high_price,
                    low_price = EXCLUDED.low_price,
                    close_price = EXCLUDED.close_price,
                    volume = EXCLUDED.volume,
                    amount = EXCLUDED.amount,
                    advance_count = EXCLUDED.advance_count,
                    decline_count = EXCLUDED.decline_count,
                    update_time = CURRENT_TIMESTAMP
                WHERE stock_daily_data.open_price IS DISTINCT FROM EXCLUDED.open_price
                   OR stock_daily_data.high_price IS DISTINCT FROM EXCLUDED.high_price
                   OR stock_daily_data.low_price IS DISTINCT FROM EXCLUDED.low_price
                   OR stock_daily_data.close_price IS DISTINCT FROM EXCLUDED.close_price
                   OR stock_daily_data.volume IS DISTINCT FROM EXCLUDED.volume
                   OR stock_daily_data.amount IS DISTINCT FROM EXCLUDED.amount
                   OR COALESCE(stock_daily_data.advance_count, 0) IS DISTINCT FROM COALESCE(EXCLUDED.advance_count, 0)
                   OR COALESCE(stock_daily_data.decline_count, 0) IS DISTINCT FROM COALESCE(EXCLUDED.decline_count, 0)";

            int totalSaved = 0;
            int batchSize = 500;

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                for (int i = 0; i < records.Count; i += batchSize)
                {
                    var batch = records.Skip(i).Take(batchSize).ToList();

                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            foreach (var record in batch)
                            {
                                using (var command = new NpgsqlCommand(insertSql, connection, transaction))
                                {
                                    command.Parameters.AddWithValue("@stock_code", record.StockCode ?? (object)DBNull.Value);
                                    // MarketCode: ushort -> short, market codes are 0 (深圳) or 1 (上海)
                                    command.Parameters.Add(new NpgsqlParameter("@market_code", NpgsqlDbType.Smallint)
                                    {
                                        Value = record.MarketCode <= 32767 ? (short)record.MarketCode : (short)0
                                    });
                                    command.Parameters.AddWithValue("@trade_date", record.TradeDate);
                                    command.Parameters.AddWithValue("@trade_datetime",
                                        record.TradeDateTime != default(DateTime) ? (object)record.TradeDateTime : DBNull.Value);
                                    command.Parameters.AddWithValue("@time_stamp", record.TimeStamp);
                                    command.Parameters.AddWithValue("@open_price", record.OpenPrice);
                                    command.Parameters.AddWithValue("@high_price", record.HighPrice);
                                    command.Parameters.AddWithValue("@low_price", record.LowPrice);
                                    command.Parameters.AddWithValue("@close_price", record.ClosePrice);
                                    command.Parameters.AddWithValue("@volume", record.Volume);
                                    command.Parameters.AddWithValue("@amount", record.Amount);
                                    // AdvanceCount/DeclineCount: ushort -> short, cap at short.MaxValue to prevent overflow
                                    command.Parameters.Add(new NpgsqlParameter("@advance_count", NpgsqlDbType.Smallint)
                                    {
                                        Value = record.AdvanceCount > 0
                                            ? (object)(record.AdvanceCount <= 32767 ? (short)record.AdvanceCount : short.MaxValue)
                                            : DBNull.Value
                                    });
                                    command.Parameters.Add(new NpgsqlParameter("@decline_count", NpgsqlDbType.Smallint)
                                    {
                                        Value = record.DeclineCount > 0
                                            ? (object)(record.DeclineCount <= 32767 ? (short)record.DeclineCount : short.MaxValue)
                                            : DBNull.Value
                                    });
                                    command.Parameters.AddWithValue("@data_source", "龙卷风-MQ");

                                    command.ExecuteNonQuery();
                                    totalSaved++;
                                }
                            }

                            transaction.Commit();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            Console.WriteLine("批量保存失败: " + ex.Message);
                            throw;
                        }
                    }
                }
            }

            // 清除相关Redis缓存
            ClearKDCache(records);

            return totalSaved;
        }

        /// <summary>
        /// 获取所有股票代码列表
        /// </summary>
        public List<string> GetAllStockCodes()
        {
            var result = new List<string>();

            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    string sql = "SELECT DISTINCT stock_code FROM stock_daily_data ORDER BY stock_code";

                    using (var command = new NpgsqlCommand(sql, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                result.Add(reader.GetString(0));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取股票代码列表失败: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 删除指定股票的所有数据
        /// </summary>
        public bool DeleteStockData(string stockCode)
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    string sql = "DELETE FROM stock_daily_data WHERE stock_code = @stock_code";

                    using (var command = new NpgsqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@stock_code", stockCode);
                        command.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"删除股票数据失败: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region IKlineDataRepository 实现

        /// <summary>
        /// 获取指定时间范围内的日线数据
        /// </summary>
        public List<DailyKlineData> GetDailyData(string stockCode, DateTime startDate, DateTime endDate)
        {
            var result = new List<DailyKlineData>();

            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    string sql = @"
                        SELECT trade_date, open_price, high_price, low_price, close_price, volume
                        FROM stock_daily_data
                        WHERE stock_code = @stock_code
                          AND trade_date >= @start_date
                          AND trade_date <= @end_date
                        ORDER BY trade_date ASC";

                    using (var command = new NpgsqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@stock_code", stockCode);
                        command.Parameters.AddWithValue("@start_date", startDate.Date);
                        command.Parameters.AddWithValue("@end_date", endDate.Date);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                result.Add(new DailyKlineData
                                {
                                    TradeDate = reader.GetDateTime(0),
                                    Open = reader.GetDecimal(1),
                                    High = reader.GetDecimal(2),
                                    Low = reader.GetDecimal(3),
                                    Close = reader.GetDecimal(4),
                                    Volume = reader.GetDecimal(5)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取日线数据失败: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 获取最新的日线数据
        /// </summary>
        public List<DailyKlineData> GetLatestDailyData(string stockCode, int count)
        {
            var result = new List<DailyKlineData>();

            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    string sql = @"
                        SELECT trade_date, open_price, high_price, low_price, close_price, volume
                        FROM stock_daily_data
                        WHERE stock_code = @stock_code
                        ORDER BY trade_date DESC
                        LIMIT @count";

                    using (var command = new NpgsqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@stock_code", stockCode);
                        command.Parameters.AddWithValue("@count", count);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                result.Add(new DailyKlineData
                                {
                                    TradeDate = reader.GetDateTime(0),
                                    Open = reader.GetDecimal(1),
                                    High = reader.GetDecimal(2),
                                    Low = reader.GetDecimal(3),
                                    Close = reader.GetDecimal(4),
                                    Volume = reader.GetDecimal(5)
                                });
                            }
                        }
                    }
                }

                result.Reverse();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取最新日线数据失败: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 检查股票是否存在数据
        /// </summary>
        public bool HasData(string stockCode)
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    string sql = @"
                        SELECT EXISTS(
                            SELECT 1 FROM stock_daily_data
                            WHERE stock_code = @stock_code
                        )";

                    using (var command = new NpgsqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@stock_code", stockCode);
                        return (bool)command.ExecuteScalar();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"检查股票数据失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取数据的日期范围
        /// </summary>
        public (DateTime? StartDate, DateTime? EndDate) GetDataDateRange(string stockCode)
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    string sql = @"
                        SELECT MIN(trade_date), MAX(trade_date)
                        FROM stock_daily_data
                        WHERE stock_code = @stock_code";

                    using (var command = new NpgsqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@stock_code", stockCode);

                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read() && !reader.IsDBNull(0) && !reader.IsDBNull(1))
                            {
                                return (reader.GetDateTime(0), reader.GetDateTime(1));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取数据日期范围失败: {ex.Message}");
            }

            return (null, null);
        }

        /// <summary>
        /// 批量更新日线数据（用于复权计算）
        /// </summary>
        public int UpdateDailyData(List<KlineData> dataList)
        {
            if (dataList == null || dataList.Count == 0)
                return 0;

            int totalUpdated = 0;

            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    string updateSql = @"
                        UPDATE stock_daily_data SET
                            open_price = @open_price,
                            high_price = @high_price,
                            low_price = @low_price,
                            close_price = @close_price,
                            volume = @volume,
                            update_time = CURRENT_TIMESTAMP
                        WHERE stock_code = @stock_code AND trade_date = @trade_date";

                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            foreach (var data in dataList)
                            {
                                using (var command = new NpgsqlCommand(updateSql, connection, transaction))
                                {
                                    command.Parameters.AddWithValue("@stock_code", data.StockCode);
                                    command.Parameters.AddWithValue("@trade_date", data.TradeDate.Date);
                                    command.Parameters.AddWithValue("@open_price", data.Open);
                                    command.Parameters.AddWithValue("@high_price", data.High);
                                    command.Parameters.AddWithValue("@low_price", data.Low);
                                    command.Parameters.AddWithValue("@close_price", data.Close);
                                    command.Parameters.AddWithValue("@volume", data.Volume);

                                    int affected = command.ExecuteNonQuery();
                                    totalUpdated += affected;
                                }
                            }

                            transaction.Commit();
                        }
                        catch (Exception)
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"批量更新日线数据失败: {ex.Message}");
            }

            return totalUpdated;
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 清除相关Redis缓存
        /// </summary>
        private void ClearKDCache(List<DailyDataRecord> records)
        {
            if (records == null || records.Count == 0)
                return;

            if (!RedisHelper.IsEnabled)
                return;

            try
            {
                var stockCodes = records
                    .Select(r => r.StockCode)
                    .Where(c => !string.IsNullOrEmpty(c))
                    .Distinct()
                    .ToList();

                foreach (var stockCode in stockCodes)
                {
                    RedisHelper.DeleteCacheByPattern($"kd:{stockCode}:*");
                    RedisHelper.DeleteCacheByPattern($"kd_history:{stockCode}:*");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"清除Redis缓存失败: {ex.Message}");
            }
        }

        #endregion
    }
}
