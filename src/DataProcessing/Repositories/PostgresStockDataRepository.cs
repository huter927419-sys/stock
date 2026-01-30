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
                    volume, amount, turnover_rate, advance_count, decline_count, data_source
                ) VALUES (
                    @stock_code, @market_code, @trade_date, @trade_datetime, @time_stamp,
                    @open_price, @high_price, @low_price, @close_price,
                    @volume, @amount, @turnover_rate, @advance_count, @decline_count, @data_source
                ) ON CONFLICT (stock_code, trade_date) DO UPDATE SET
                    open_price = EXCLUDED.open_price,
                    high_price = EXCLUDED.high_price,
                    low_price = EXCLUDED.low_price,
                    close_price = EXCLUDED.close_price,
                    volume = EXCLUDED.volume,
                    amount = EXCLUDED.amount,
                    turnover_rate = EXCLUDED.turnover_rate,
                    advance_count = EXCLUDED.advance_count,
                    decline_count = EXCLUDED.decline_count,
                    update_time = CURRENT_TIMESTAMP";

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
                                    // TurnoverRate: 可空decimal，换手率（%）
                                    var turnoverRateValue = record.TurnoverRate.HasValue ? (object)record.TurnoverRate.Value : DBNull.Value;
                                    command.Parameters.Add(new NpgsqlParameter("@turnover_rate", NpgsqlDbType.Numeric)
                                    {
                                        Value = turnoverRateValue
                                    });
                                    
                                    // 记录换手率保存情况（仅前20条记录）
                                    if (totalSaved <= 20)
                                    {
                                        Console.WriteLine($"[保存换手率] {record.StockCode} {record.TradeDate:yyyy-MM-dd}: " +
                                            $"TurnoverRate={(record.TurnoverRate.HasValue ? record.TurnoverRate.Value.ToString("F2") + "%" : "NULL")}, " +
                                            $"保存值={(turnoverRateValue == DBNull.Value ? "DBNull" : turnoverRateValue.ToString())}, " +
                                            $"成交量={record.Volume}");
                                    }
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
        /// 只获取上海和深圳交易所的A股股票代码，且满足以下条件：
        /// 1. 最近一年内有足够的数据密度（至少200个交易日）
        /// 2. 最新数据日期在最近30天内（排除已退市股票）
        /// 上海: 600*, 601*, 603*, 605*, 688* (科创板)
        /// 深圳: 000*, 001*, 002*, 003*, 300*, 301* (创业板)
        /// </summary>
        public List<string> GetAllStockCodes()
        {
            var result = new List<string>();

            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    // 只获取A股股票代码，且满足数据质量要求
                    // 在SQL层面就计算掉非A股、ST股票、退市股票等
                    // 1. 股票代码符合A股格式
                    // 2. 排除000000-000199范围（指数/债券指数）
                    // 3. 排除已知的无效代码（黑名单）
                    // 4. 通过stock_info表计算ST股票和无效股票
                    // 5. 最近一年内至少有200个交易日的数据
                    // 6. 最新数据日期在数据库最新日期的30天内（排除退市股票）
                    string sql = @"
                        WITH max_date AS (
                            SELECT MAX(trade_date) as latest_date FROM stock_daily_data
                        ),
                        recent_data AS (
                            SELECT 
                                d.stock_code,
                                COUNT(*) as data_count,
                                MAX(d.trade_date) as last_date,
                                MIN(d.trade_date) as first_date
                            FROM stock_daily_data d
                            -- 关联 stock_info 表，获取股票信息用于计算
                            LEFT JOIN stock_info i ON d.stock_code = i.stock_code
                            WHERE d.trade_date >= (SELECT latest_date FROM max_date) - INTERVAL '365 days'
                              -- 1. 股票代码格式计算（A股、创业板、科创板）
                              AND d.stock_code ~ '^(600|601|603|605|688|000|001|002|003|300|301)[0-9]{3}$'
                              -- 2. 排除000000-000199范围（几乎全是指数和债券指数）
                              AND NOT (d.stock_code ~ '^000[0-1][0-9]{2}$')
                              -- 3. 排除已知的无效代码（指数、基金、B股、已退市等）
                              AND d.stock_code NOT IN (
                                  '000001', '000016', '000300', '000038', '000091',
                                  '000101', '000102', '000103', '000104', '000105', '000106', '000107', '000108',
                                  '000110', '000132', '000137', '000146', '000807', '000905', '000906', '000914', '000985', '000991', '000992', '000993',
                                  '000071', '000076', '000139', '000974',
                                  '000018', '000033', '000046', '000052', '000053', '000073', '000077', '000816',
                                  '000161', '000847', '000854'
                              )
                              -- 4. 通过 stock_info 表计算（如果表中有数据）
                              AND (i.stock_code IS NULL OR (
                                  -- 只选择有效的股票（is_active = TRUE）
                                  (i.is_active IS NULL OR i.is_active = TRUE)
                                  -- 排除ST股票（名称中包含ST或*ST）
                                  AND (i.stock_name IS NULL OR (
                                      i.stock_name !~ '\*?ST' 
                                      AND i.stock_name NOT LIKE '%ST%'
                                  ))
                                  -- 排除指数、基金等（stock_type = '股票'）
                                  AND (i.stock_type IS NULL OR i.stock_type = '股票')
                              ))
                            GROUP BY d.stock_code
                            HAVING 
                                -- 数据量要求：最近一年至少200个交易日
                                COUNT(*) >= 200
                                -- 数据新鲜度：最新数据在30天内（排除退市股票）
                                AND MAX(d.trade_date) >= (SELECT latest_date FROM max_date) - INTERVAL '30 days'
                        )
                        SELECT stock_code FROM recent_data ORDER BY stock_code";

                    using (var command = new NpgsqlCommand(sql, connection))
                    {
                        command.CommandTimeout = 120; // 增加超时时间
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
        /// 获取数据库中最新的交易日期
        /// </summary>
        public DateTime? GetLatestTradeDate()
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    string sql = "SELECT MAX(trade_date) FROM stock_daily_data";

                    using (var command = new NpgsqlCommand(sql, connection))
                    {
                        var result = command.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            return (DateTime)result;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取最新交易日期失败: {ex.Message}");
            }

            return null;
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

        /// <summary>
        /// 获取所有股票的名称（从stock_info表）
        /// </summary>
        public Dictionary<string, string> GetAllStockNames()
        {
            var result = new Dictionary<string, string>();

            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    string sql = "SELECT stock_code, stock_name FROM stock_info WHERE stock_name IS NOT NULL AND stock_name <> ''";

                    using (var command = new NpgsqlCommand(sql, connection))
                    {
                        command.CommandTimeout = 60;
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string code = reader.GetString(0);
                                string name = reader.IsDBNull(1) ? "" : reader.GetString(1);
                                if (!result.ContainsKey(code))
                                {
                                    result[code] = name;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取股票名称失败: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 保存或更新股票基本信息（到stock_info表）
        /// </summary>
        public int SaveStockInfo(List<(string StockCode, string StockName, ushort MarketCode)> stockInfoList)
        {
            if (stockInfoList == null || stockInfoList.Count == 0)
                return 0;

            int savedCount = 0;

            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    string sql = @"
                        INSERT INTO stock_info (stock_code, stock_name, market_code, market_name, stock_type, update_time)
                        VALUES (@stock_code, @stock_name, @market_code, @market_name, '股票', CURRENT_TIMESTAMP)
                        ON CONFLICT (stock_code) DO UPDATE SET
                            stock_name = CASE WHEN EXCLUDED.stock_name <> '' THEN EXCLUDED.stock_name ELSE stock_info.stock_name END,
                            market_code = EXCLUDED.market_code,
                            market_name = EXCLUDED.market_name,
                            update_time = CURRENT_TIMESTAMP
                        WHERE stock_info.stock_name IS DISTINCT FROM EXCLUDED.stock_name
                           OR stock_info.market_code IS DISTINCT FROM EXCLUDED.market_code";

                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            foreach (var info in stockInfoList)
                            {
                                if (string.IsNullOrWhiteSpace(info.StockCode) || string.IsNullOrWhiteSpace(info.StockName))
                                    continue;

                                using (var command = new NpgsqlCommand(sql, connection, transaction))
                                {
                                    string marketName = info.MarketCode == 1 ? "上海" : "深圳";
                                    command.Parameters.AddWithValue("@stock_code", info.StockCode);
                                    command.Parameters.AddWithValue("@stock_name", info.StockName);
                                    command.Parameters.AddWithValue("@market_code", (short)info.MarketCode);
                                    command.Parameters.AddWithValue("@market_name", marketName);
                                    command.ExecuteNonQuery();
                                    savedCount++;
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
                Console.WriteLine($"保存股票信息失败: {ex.Message}");
            }

            return savedCount;
        }

        /// <summary>
        /// 从stock_daily_data表初始化stock_info表（仅填充stock_code，无名称）
        /// 用于在没有码表数据时初始化stock_info
        /// </summary>
        public int InitializeStockInfoFromDailyData()
        {
            int insertedCount = 0;

            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    // 插入stock_daily_data中存在但stock_info中不存在的股票代码
                    string sql = @"
                        INSERT INTO stock_info (stock_code, market_code, market_name, stock_type, update_time)
                        SELECT DISTINCT
                            stock_code,
                            market_code,
                            CASE WHEN market_code = 1 THEN '上海' ELSE '深圳' END as market_name,
                            '股票' as stock_type,
                            CURRENT_TIMESTAMP
                        FROM stock_daily_data
                        WHERE stock_code NOT IN (SELECT stock_code FROM stock_info)
                        ON CONFLICT (stock_code) DO NOTHING";

                    using (var command = new NpgsqlCommand(sql, connection))
                    {
                        command.CommandTimeout = 120;
                        insertedCount = command.ExecuteNonQuery();
                    }
                }

                Console.WriteLine($"从日线数据初始化了 {insertedCount} 条股票信息到stock_info表");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"初始化股票信息失败: {ex.Message}");
            }

            return insertedCount;
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

        /// <summary>
        /// 获取指定日期的成交金额（元）和换手率（%）
        /// </summary>
        public (decimal? Amount, decimal? TurnoverRate) GetYesterdayAmountAndTurnoverRate(string stockCode, DateTime tradeDate)
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    string sql = @"SELECT amount, turnover_rate FROM stock_daily_data WHERE stock_code = @stock_code AND trade_date = @trade_date";
                    using (var cmd = new NpgsqlCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@stock_code", stockCode);
                        cmd.Parameters.AddWithValue("@trade_date", tradeDate.Date);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                decimal amount = reader.GetDecimal(0);
                                decimal? turnover = reader.IsDBNull(1) ? (decimal?)null : reader.GetDecimal(1);
                                return (amount, turnover);
                            }
                        }
                    }
                }
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "42703")
            {
                try
                {
                    using (var connection = new NpgsqlConnection(_connectionString))
                    {
                        connection.Open();
                        string sql = @"SELECT amount FROM stock_daily_data WHERE stock_code = @stock_code AND trade_date = @trade_date";
                        using (var cmd = new NpgsqlCommand(sql, connection))
                        {
                            cmd.Parameters.AddWithValue("@stock_code", stockCode);
                            cmd.Parameters.AddWithValue("@trade_date", tradeDate.Date);
                            var v = cmd.ExecuteScalar();
                            return (v != null && v != DBNull.Value ? (decimal?)Convert.ToDecimal(v) : null, null);
                        }
                    }
                }
                catch { return (null, null); }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetYesterdayAmountAndTurnoverRate [{stockCode}]: {ex.Message}");
            }
            return (null, null);
        }

        /// <summary>
        /// 批量获取指定日期的成交金额（元）和换手率（%），用于性能优化
        /// </summary>
        public Dictionary<string, (decimal? Amount, decimal? TurnoverRate)> GetYesterdayAmountAndTurnoverRateBatch(List<string> stockCodes, DateTime tradeDate)
        {
            var result = new Dictionary<string, (decimal? Amount, decimal? TurnoverRate)>();
            
            if (stockCodes == null || stockCodes.Count == 0)
                return result;
            
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    
                    // 批量查询：使用 ANY 子句一次性查询所有股票
                    string sql = @"
                        SELECT stock_code, amount, turnover_rate
                        FROM stock_daily_data
                        WHERE stock_code = ANY(@stock_codes) AND trade_date = @trade_date";
                    
                    using (var cmd = new NpgsqlCommand(sql, connection))
                    {
                        // 使用数组参数（PostgreSQL数组类型）
                        var stockCodesParam = new NpgsqlParameter("@stock_codes", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text)
                        {
                            Value = stockCodes.ToArray()
                        };
                        cmd.Parameters.Add(stockCodesParam);
                        cmd.Parameters.AddWithValue("@trade_date", tradeDate.Date);
                        
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string code = reader.GetString(0);
                                decimal amount = reader.GetDecimal(1);
                                decimal? turnover = reader.IsDBNull(2) ? (decimal?)null : reader.GetDecimal(2);
                                result[code] = (amount, turnover);
                            }
                        }
                    }
                }
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "42703")
            {
                // turnover_rate 列不存在时，仅查 amount
                try
                {
                    using (var connection = new NpgsqlConnection(_connectionString))
                    {
                        connection.Open();
                        string sql = @"
                            SELECT stock_code, amount
                            FROM stock_daily_data
                            WHERE stock_code = ANY(@stock_codes) AND trade_date = @trade_date";
                        using (var cmd = new NpgsqlCommand(sql, connection))
                        {
                            var stockCodesParam = new NpgsqlParameter("@stock_codes", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text)
                            {
                                Value = stockCodes.ToArray()
                            };
                            cmd.Parameters.Add(stockCodesParam);
                            cmd.Parameters.AddWithValue("@trade_date", tradeDate.Date);
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    string code = reader.GetString(0);
                                    decimal amount = reader.GetDecimal(1);
                                    result[code] = (amount, null);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"GetYesterdayAmountAndTurnoverRateBatch (fallback): {ex2.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetYesterdayAmountAndTurnoverRateBatch: {ex.Message}");
            }
            
            return result;
        }

        /// <summary>
        /// 批量获取指定日期的成交金额（元），仅用于过滤条件（昨天成交金额>=N亿），不查换手率。
        /// </summary>
        public Dictionary<string, decimal?> GetYesterdayAmountBatch(List<string> stockCodes, DateTime tradeDate)
        {
            var result = new Dictionary<string, decimal?>();
            if (stockCodes == null || stockCodes.Count == 0)
                return result;
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    string sql = @"
                        SELECT stock_code, amount
                        FROM stock_daily_data
                        WHERE stock_code = ANY(@stock_codes) AND trade_date = @trade_date";
                    using (var cmd = new NpgsqlCommand(sql, connection))
                    {
                        var stockCodesParam = new NpgsqlParameter("@stock_codes", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text)
                        {
                            Value = stockCodes.ToArray()
                        };
                        cmd.Parameters.Add(stockCodesParam);
                        cmd.Parameters.AddWithValue("@trade_date", tradeDate.Date);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string code = reader.GetString(0);
                                decimal? amount = reader.IsDBNull(1) ? (decimal?)null : reader.GetDecimal(1);
                                result[code] = amount;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetYesterdayAmountBatch: {ex.Message}");
            }
            return result;
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

        #region 数据诊断方法

        /// <summary>
        /// 打印数据库数据诊断信息
        /// </summary>
        public void PrintDataDiagnostics()
        {
            Console.WriteLine("\n========== 数据库诊断报告 ==========\n");

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();

                    // 1. 各表数据量
                    Console.WriteLine("【表数据量统计】");
                    PrintTableCount(conn, "stock_info", "股票信息表");
                    PrintTableCount(conn, "stock_daily_data", "日线数据表");
                    PrintTableCount(conn, "stock_realtime_data", "实时数据表");
                    PrintTableCount(conn, "stock_exrights_data", "除权数据表");

                    // 2. 日线数据日期范围
                    Console.WriteLine("\n【日线数据日期范围】");
                    using (var cmd = new NpgsqlCommand(
                        "SELECT MIN(trade_date), MAX(trade_date) FROM stock_daily_data", conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read() && !reader.IsDBNull(0))
                            {
                                Console.WriteLine($"  最早日期: {reader.GetDateTime(0):yyyy-MM-dd}");
                                Console.WriteLine($"  最新日期: {reader.GetDateTime(1):yyyy-MM-dd}");
                            }
                            else
                            {
                                Console.WriteLine("  无数据");
                            }
                        }
                    }

                    // 3. 最近10个交易日数据量
                    Console.WriteLine("\n【最近10个交易日数据量】");
                    using (var cmd = new NpgsqlCommand(@"
                        SELECT trade_date, COUNT(DISTINCT stock_code) as stock_count
                        FROM stock_daily_data
                        WHERE trade_date >= CURRENT_DATE - INTERVAL '20 days'
                        GROUP BY trade_date
                        ORDER BY trade_date DESC
                        LIMIT 10", conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Console.WriteLine($"  {reader.GetDateTime(0):yyyy-MM-dd}: {reader.GetInt64(1)} 只股票");
                            }
                        }
                    }

                    // 4. 股票名称统计
                    Console.WriteLine("\n【股票名称统计】");
                    using (var cmd = new NpgsqlCommand(@"
                        SELECT
                            COUNT(*) as total,
                            SUM(CASE WHEN stock_name = stock_code OR stock_name IS NULL OR stock_name = '' THEN 1 ELSE 0 END) as missing,
                            SUM(CASE WHEN stock_name <> stock_code AND stock_name IS NOT NULL AND stock_name <> '' THEN 1 ELSE 0 END) as has_name
                        FROM stock_info", conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                Console.WriteLine($"  总数: {reader.GetInt64(0)}");
                                Console.WriteLine($"  缺失名称: {reader.GetInt64(1)}");
                                Console.WriteLine($"  有名称: {reader.GetInt64(2)}");
                            }
                        }
                    }

                    // 5. 抽样股票数据
                    Console.WriteLine("\n【抽样股票最近数据】");
                    string[] sampleStocks = { "000001", "600000", "300001" };
                    foreach (var code in sampleStocks)
                    {
                        Console.WriteLine($"\n  --- {code} ---");
                        using (var cmd = new NpgsqlCommand($@"
                            SELECT trade_date, open_price, high_price, low_price, close_price, volume
                            FROM stock_daily_data
                            WHERE stock_code = @code
                            ORDER BY trade_date DESC
                            LIMIT 3", conn))
                        {
                            cmd.Parameters.AddWithValue("@code", code);
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    Console.WriteLine($"  {reader.GetDateTime(0):yyyy-MM-dd} O:{reader.GetDecimal(1):F2} H:{reader.GetDecimal(2):F2} L:{reader.GetDecimal(3):F2} C:{reader.GetDecimal(4):F2} V:{reader.GetDecimal(5):F0}");
                                }
                            }
                        }
                    }
                }

                Console.WriteLine("\n========== 诊断完成 ==========\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"诊断失败: {ex.Message}");
            }
        }

        private void PrintTableCount(NpgsqlConnection conn, string table, string name)
        {
            using (var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {table}", conn))
            {
                var count = cmd.ExecuteScalar();
                Console.WriteLine($"  {name}: {count}");
            }
        }

        #endregion
    }
}
