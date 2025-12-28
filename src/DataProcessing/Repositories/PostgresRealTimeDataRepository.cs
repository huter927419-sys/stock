using System;
using System.Collections.Generic;
using System.Linq;
using MQReceiver.Helpers;
using Npgsql;
using NpgsqlTypes;

namespace MQReceiver.Repositories
{
    /// <summary>
    /// PostgreSQL 实时数据仓储实现
    /// </summary>
    public class PostgresRealTimeDataRepository : IRealTimeDataRepository
    {
        private readonly string _connectionString;

        public PostgresRealTimeDataRepository()
        {
            _connectionString = DatabaseConnectionHelper.BuildConnectionString();
        }

        public PostgresRealTimeDataRepository(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

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
                Console.WriteLine("实时数据数据库连接测试失败: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 保存实时数据到数据库
        /// </summary>
        public int SaveRealTimeData(List<RealTimeDataRecord> records)
        {
            if (records == null || records.Count == 0)
                return 0;

            string insertSql = @"
                INSERT INTO stock_realtime_data (
                    stock_code, stock_name, market_code, update_time, time_stamp,
                    last_close, open_price, high_price, low_price, new_price,
                    volume, amount,
                    buy_price_1, buy_price_2, buy_price_3, buy_price_4, buy_price_5,
                    buy_volume_1, buy_volume_2, buy_volume_3, buy_volume_4, buy_volume_5,
                    sell_price_1, sell_price_2, sell_price_3, sell_price_4, sell_price_5,
                    sell_volume_1, sell_volume_2, sell_volume_3, sell_volume_4, sell_volume_5,
                    data_source
                ) VALUES (
                    @stock_code, @stock_name, @market_code, @update_time, @time_stamp,
                    @last_close, @open_price, @high_price, @low_price, @new_price,
                    @volume, @amount,
                    @buy_price_1, @buy_price_2, @buy_price_3, @buy_price_4, @buy_price_5,
                    @buy_volume_1, @buy_volume_2, @buy_volume_3, @buy_volume_4, @buy_volume_5,
                    @sell_price_1, @sell_price_2, @sell_price_3, @sell_price_4, @sell_price_5,
                    @sell_volume_1, @sell_volume_2, @sell_volume_3, @sell_volume_4, @sell_volume_5,
                    @data_source
                ) ON CONFLICT (stock_code) DO UPDATE SET
                    stock_name = EXCLUDED.stock_name,
                    market_code = EXCLUDED.market_code,
                    update_time = EXCLUDED.update_time,
                    time_stamp = EXCLUDED.time_stamp,
                    last_close = EXCLUDED.last_close,
                    open_price = EXCLUDED.open_price,
                    high_price = EXCLUDED.high_price,
                    low_price = EXCLUDED.low_price,
                    new_price = EXCLUDED.new_price,
                    volume = EXCLUDED.volume,
                    amount = EXCLUDED.amount,
                    buy_price_1 = EXCLUDED.buy_price_1,
                    buy_price_2 = EXCLUDED.buy_price_2,
                    buy_price_3 = EXCLUDED.buy_price_3,
                    buy_price_4 = EXCLUDED.buy_price_4,
                    buy_price_5 = EXCLUDED.buy_price_5,
                    buy_volume_1 = EXCLUDED.buy_volume_1,
                    buy_volume_2 = EXCLUDED.buy_volume_2,
                    buy_volume_3 = EXCLUDED.buy_volume_3,
                    buy_volume_4 = EXCLUDED.buy_volume_4,
                    buy_volume_5 = EXCLUDED.buy_volume_5,
                    sell_price_1 = EXCLUDED.sell_price_1,
                    sell_price_2 = EXCLUDED.sell_price_2,
                    sell_price_3 = EXCLUDED.sell_price_3,
                    sell_price_4 = EXCLUDED.sell_price_4,
                    sell_price_5 = EXCLUDED.sell_price_5,
                    sell_volume_1 = EXCLUDED.sell_volume_1,
                    sell_volume_2 = EXCLUDED.sell_volume_2,
                    sell_volume_3 = EXCLUDED.sell_volume_3,
                    sell_volume_4 = EXCLUDED.sell_volume_4,
                    sell_volume_5 = EXCLUDED.sell_volume_5,
                    update_time_db = CURRENT_TIMESTAMP
                WHERE stock_realtime_data.new_price IS DISTINCT FROM EXCLUDED.new_price
                   OR stock_realtime_data.volume IS DISTINCT FROM EXCLUDED.volume
                   OR stock_realtime_data.amount IS DISTINCT FROM EXCLUDED.amount
                   OR stock_realtime_data.time_stamp IS DISTINCT FROM EXCLUDED.time_stamp
                   OR stock_realtime_data.high_price IS DISTINCT FROM EXCLUDED.high_price
                   OR stock_realtime_data.low_price IS DISTINCT FROM EXCLUDED.low_price";

            int totalSaved = 0;
            int batchSize = 200;

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
                                    AddParameters(command, record);
                                    command.ExecuteNonQuery();
                                    totalSaved++;
                                }
                            }

                            transaction.Commit();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            Console.WriteLine("批量保存实时数据失败: " + ex.Message);
                            throw;
                        }
                    }
                }
            }

            return totalSaved;
        }

        /// <summary>
        /// 获取指定股票的实时数据
        /// </summary>
        public RealTimeDataRecord GetRealTimeData(string stockCode)
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    string sql = @"
                        SELECT stock_code, stock_name, market_code, update_time, time_stamp,
                               last_close, open_price, high_price, low_price, new_price,
                               volume, amount
                        FROM stock_realtime_data
                        WHERE stock_code = @stock_code";

                    using (var command = new NpgsqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@stock_code", stockCode);

                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return ReadRecord(reader);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取实时数据失败: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 获取所有实时数据
        /// </summary>
        public List<RealTimeDataRecord> GetAllRealTimeData()
        {
            var result = new List<RealTimeDataRecord>();

            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    string sql = @"
                        SELECT stock_code, stock_name, market_code, update_time, time_stamp,
                               last_close, open_price, high_price, low_price, new_price,
                               volume, amount
                        FROM stock_realtime_data";

                    using (var command = new NpgsqlCommand(sql, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                result.Add(ReadRecord(reader));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取所有实时数据失败: {ex.Message}");
            }

            return result;
        }

        private void AddParameters(NpgsqlCommand command, RealTimeDataRecord record)
        {
            command.Parameters.AddWithValue("@stock_code", record.StockCode ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@stock_name", record.StockName ?? (object)DBNull.Value);
            // MarketCode: ushort -> short, market codes are 0 (深圳) or 1 (上海)
            command.Parameters.Add(new NpgsqlParameter("@market_code", NpgsqlDbType.Smallint)
            {
                Value = record.MarketCode <= 32767 ? (short)record.MarketCode : (short)0
            });
            command.Parameters.AddWithValue("@update_time", record.UpdateTime);
            command.Parameters.AddWithValue("@time_stamp", record.TimeStamp);
            command.Parameters.AddWithValue("@last_close", record.LastClose);
            command.Parameters.AddWithValue("@open_price", record.Open);
            command.Parameters.AddWithValue("@high_price", record.High);
            command.Parameters.AddWithValue("@low_price", record.Low);
            command.Parameters.AddWithValue("@new_price", record.NewPrice);
            command.Parameters.AddWithValue("@volume", record.Volume);
            command.Parameters.AddWithValue("@amount", record.Amount);

            for (int j = 0; j < 5; j++)
            {
                command.Parameters.AddWithValue("@buy_price_" + (j + 1),
                    record.BuyPrice != null && j < record.BuyPrice.Length && record.BuyPrice[j] > 0
                        ? (object)record.BuyPrice[j] : DBNull.Value);
                command.Parameters.AddWithValue("@buy_volume_" + (j + 1),
                    record.BuyVolume != null && j < record.BuyVolume.Length && record.BuyVolume[j] > 0
                        ? (object)record.BuyVolume[j] : DBNull.Value);
                command.Parameters.AddWithValue("@sell_price_" + (j + 1),
                    record.SellPrice != null && j < record.SellPrice.Length && record.SellPrice[j] > 0
                        ? (object)record.SellPrice[j] : DBNull.Value);
                command.Parameters.AddWithValue("@sell_volume_" + (j + 1),
                    record.SellVolume != null && j < record.SellVolume.Length && record.SellVolume[j] > 0
                        ? (object)record.SellVolume[j] : DBNull.Value);
            }

            command.Parameters.AddWithValue("@data_source", "龙卷风-MQ-实时");
        }

        private RealTimeDataRecord ReadRecord(NpgsqlDataReader reader)
        {
            return new RealTimeDataRecord
            {
                StockCode = reader.GetString(0),
                StockName = reader.IsDBNull(1) ? null : reader.GetString(1),
                MarketCode = (ushort)reader.GetInt16(2),
                UpdateTime = reader.GetDateTime(3),
                TimeStamp = reader.GetInt32(4),
                LastClose = reader.GetDecimal(5),
                Open = reader.GetDecimal(6),
                High = reader.GetDecimal(7),
                Low = reader.GetDecimal(8),
                NewPrice = reader.GetDecimal(9),
                Volume = reader.GetDecimal(10),
                Amount = reader.GetDecimal(11)
            };
        }
    }
}
