using System;
using System.Collections.Generic;
using System.Linq;
using MQReceiver.Helpers;
using Npgsql;
using NpgsqlTypes;

namespace MQReceiver.Repositories
{
    /// <summary>
    /// 除权数据PostgreSQL仓储实现
    /// </summary>
    public class PostgresExRightsDataRepository : IExRightsDataRepository
    {
        private readonly string _connectionString;

        public PostgresExRightsDataRepository()
        {
            _connectionString = DatabaseConnectionHelper.BuildConnectionString();
        }

        public PostgresExRightsDataRepository(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

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
                Console.WriteLine("除权数据数据库连接测试失败: " + ex.Message);
                return false;
            }
        }

        public int SaveExRightsData(List<ExRightsDataRecord> records)
        {
            if (records == null || records.Count == 0)
                return 0;

            string insertSql = @"
                INSERT INTO stock_exrights_data (
                    stock_code, market_code, ex_rights_date, ex_rights_datetime, time_stamp,
                    give_per_10_shares, pei_per_10_shares, pei_price, profit_per_share, data_source
                ) VALUES (
                    @stock_code, @market_code, @ex_rights_date, @ex_rights_datetime, @time_stamp,
                    @give_per_10_shares, @pei_per_10_shares, @pei_price, @profit_per_share, @data_source
                ) ON CONFLICT (stock_code, ex_rights_date) DO UPDATE SET
                    give_per_10_shares = EXCLUDED.give_per_10_shares,
                    pei_per_10_shares = EXCLUDED.pei_per_10_shares,
                    pei_price = EXCLUDED.pei_price,
                    profit_per_share = EXCLUDED.profit_per_share,
                    update_time = CURRENT_TIMESTAMP
                WHERE stock_exrights_data.give_per_10_shares IS DISTINCT FROM EXCLUDED.give_per_10_shares
                   OR stock_exrights_data.pei_per_10_shares IS DISTINCT FROM EXCLUDED.pei_per_10_shares
                   OR stock_exrights_data.pei_price IS DISTINCT FROM EXCLUDED.pei_price
                   OR stock_exrights_data.profit_per_share IS DISTINCT FROM EXCLUDED.profit_per_share";

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
                                    command.Parameters.AddWithValue("@ex_rights_date", record.ExRightsDate);
                                    command.Parameters.AddWithValue("@ex_rights_datetime",
                                        record.ExRightsDateTime != default(DateTime) ? (object)record.ExRightsDateTime : DBNull.Value);
                                    command.Parameters.AddWithValue("@time_stamp", record.TimeStamp);
                                    command.Parameters.AddWithValue("@give_per_10_shares", record.GivePer10Shares);
                                    command.Parameters.AddWithValue("@pei_per_10_shares", record.PeiPer10Shares);
                                    command.Parameters.AddWithValue("@pei_price", record.PeiPrice);
                                    command.Parameters.AddWithValue("@profit_per_share", record.ProfitPerShare);
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
                            Console.WriteLine("批量保存除权数据失败: " + ex.Message);
                            throw;
                        }
                    }
                }
            }

            // 清除相关Redis缓存（除权数据更新后，KD值需要重新计算）
            if (totalSaved > 0 && RedisHelper.IsEnabled)
            {
                try
                {
                    var stockCodes = records.Select(r => r.StockCode).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();
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

            return totalSaved;
        }

        public List<ExRightsDataRecord> GetExRightsData(string stockCode)
        {
            var result = new List<ExRightsDataRecord>();

            string sql = @"
                SELECT stock_code, market_code, ex_rights_date, ex_rights_datetime, time_stamp,
                       give_per_10_shares, pei_per_10_shares, pei_price, profit_per_share
                FROM stock_exrights_data
                WHERE stock_code = @stock_code
                ORDER BY ex_rights_date";

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@stock_code", stockCode);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Add(MapToExRightsDataRecord(reader));
                        }
                    }
                }
            }

            return result;
        }

        public List<ExRightsDataRecord> GetExRightsData(string stockCode, DateTime startDate, DateTime endDate)
        {
            var result = new List<ExRightsDataRecord>();

            string sql = @"
                SELECT stock_code, market_code, ex_rights_date, ex_rights_datetime, time_stamp,
                       give_per_10_shares, pei_per_10_shares, pei_price, profit_per_share
                FROM stock_exrights_data
                WHERE stock_code = @stock_code
                  AND ex_rights_date >= @start_date
                  AND ex_rights_date <= @end_date
                ORDER BY ex_rights_date";

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@stock_code", stockCode);
                    command.Parameters.AddWithValue("@start_date", startDate.Date);
                    command.Parameters.AddWithValue("@end_date", endDate.Date);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Add(MapToExRightsDataRecord(reader));
                        }
                    }
                }
            }

            return result;
        }

        public bool HasExRightsData(string stockCode)
        {
            string sql = "SELECT COUNT(1) FROM stock_exrights_data WHERE stock_code = @stock_code";

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@stock_code", stockCode);
                    var count = Convert.ToInt64(command.ExecuteScalar());
                    return count > 0;
                }
            }
        }

        public bool DeleteExRightsData(string stockCode)
        {
            string sql = "DELETE FROM stock_exrights_data WHERE stock_code = @stock_code";

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@stock_code", stockCode);
                    int affected = command.ExecuteNonQuery();
                    return affected > 0;
                }
            }
        }

        public List<ExRightsDataRecord> GetExRightsDataAfterDate(string stockCode, DateTime targetDate)
        {
            var result = new List<ExRightsDataRecord>();

            string sql = @"
                SELECT stock_code, market_code, ex_rights_date, ex_rights_datetime, time_stamp,
                       give_per_10_shares, pei_per_10_shares, pei_price, profit_per_share
                FROM stock_exrights_data
                WHERE stock_code = @stock_code AND ex_rights_date > @target_date
                ORDER BY ex_rights_date ASC";

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@stock_code", stockCode);
                    command.Parameters.AddWithValue("@target_date", targetDate.Date);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Add(MapToExRightsDataRecord(reader));
                        }
                    }
                }
            }

            return result;
        }

        public List<ExRightsDataRecord> GetExRightsDataBeforeDate(string stockCode, DateTime targetDate)
        {
            var result = new List<ExRightsDataRecord>();

            string sql = @"
                SELECT stock_code, market_code, ex_rights_date, ex_rights_datetime, time_stamp,
                       give_per_10_shares, pei_per_10_shares, pei_price, profit_per_share
                FROM stock_exrights_data
                WHERE stock_code = @stock_code AND ex_rights_date < @target_date
                ORDER BY ex_rights_date ASC";

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@stock_code", stockCode);
                    command.Parameters.AddWithValue("@target_date", targetDate.Date);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Add(MapToExRightsDataRecord(reader));
                        }
                    }
                }
            }

            return result;
        }

        private ExRightsDataRecord MapToExRightsDataRecord(NpgsqlDataReader reader)
        {
            return new ExRightsDataRecord
            {
                StockCode = reader.GetString(0),
                MarketCode = (ushort)reader.GetInt16(1),
                ExRightsDate = reader.GetDateTime(2),
                ExRightsDateTime = reader.IsDBNull(3) ? default(DateTime) : reader.GetDateTime(3),
                TimeStamp = reader.GetInt32(4),
                GivePer10Shares = reader.GetDecimal(5),
                PeiPer10Shares = reader.GetDecimal(6),
                PeiPrice = reader.GetDecimal(7),
                ProfitPerShare = reader.GetDecimal(8)
            };
        }
    }
}
