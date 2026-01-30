using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using MQReceiver.Helpers;
using Npgsql;

namespace MQReceiver.Repositories
{
    /// <summary>
    /// PostgreSQL K线数据仓储实现
    /// 带内存缓存优化，减少数据库查询
    /// </summary>
    public class PostgresKlineDataRepository : IKlineDataRepository
    {
        private readonly string _connectionString;
        
        // 内存缓存：股票代码 -> 完整的K线数据列表（按日期排序）
        private static readonly ConcurrentDictionary<string, CachedKlineData> _klineCache = new ConcurrentDictionary<string, CachedKlineData>();
        private static readonly object _cacheLock = new object();
        
        // 缓存配置
        private const int MAX_CACHE_SIZE = 5000; // 最大缓存股票数量
        private static readonly TimeSpan CACHE_EXPIRY = TimeSpan.FromHours(1); // 缓存过期时间
        
        /// <summary>
        /// 缓存的K线数据
        /// </summary>
        private class CachedKlineData
        {
            public List<DailyKlineData> Data { get; set; }
            public DateTime CacheTime { get; set; }
            public DateTime? LastAccessTime { get; set; }
        }

        public PostgresKlineDataRepository()
        {
            _connectionString = DatabaseConnectionHelper.BuildConnectionString();
        }

        public PostgresKlineDataRepository(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        /// <summary>
        /// 获取指定时间范围内的日线数据（带内存缓存优化）
        /// 注意：当前使用原始价格（未复权），待复权数据填充后可切换为复权价格
        /// </summary>
        public List<DailyKlineData> GetDailyData(string stockCode, DateTime startDate, DateTime endDate)
        {
            // 尝试从缓存获取
            if (_klineCache.TryGetValue(stockCode, out var cachedData))
            {
                // 检查缓存是否过期
                if (DateTime.Now - cachedData.CacheTime < CACHE_EXPIRY)
                {
                    // 更新最后访问时间
                    cachedData.LastAccessTime = DateTime.Now;
                    
                    // 从缓存中筛选指定日期范围的数据
                    var filteredData = cachedData.Data
                        .Where(d => d.TradeDate >= startDate.Date && d.TradeDate <= endDate.Date)
                        .ToList();
                    
                    if (filteredData.Count > 0 || cachedData.Data.Count > 0)
                    {
                        return filteredData;
                    }
                }
                else
                {
                    // 缓存过期，移除
                    _klineCache.TryRemove(stockCode, out _);
                }
            }

            // 缓存未命中或过期，从数据库加载完整数据
            var allData = LoadAllKlineDataFromDatabase(stockCode);
            
            if (allData.Count > 0)
            {
                // 缓存完整数据
                _klineCache[stockCode] = new CachedKlineData
                {
                    Data = allData,
                    CacheTime = DateTime.Now,
                    LastAccessTime = DateTime.Now
                };
                
                // 检查缓存大小，如果超过限制，移除最久未访问的缓存
                CleanupCacheIfNeeded();
                
                // 从缓存中筛选指定日期范围的数据
                return allData
                    .Where(d => d.TradeDate >= startDate.Date && d.TradeDate <= endDate.Date)
                    .ToList();
            }

            return new List<DailyKlineData>();
        }
        
        /// <summary>
        /// 从数据库加载股票的所有K线数据
        /// </summary>
        private List<DailyKlineData> LoadAllKlineDataFromDatabase(string stockCode)
        {
            var result = new List<DailyKlineData>();

            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    // 加载所有历史数据（不限制日期范围）
                    string sql = @"
                        SELECT trade_date, open_price, high_price, low_price, close_price, volume
                        FROM stock_daily_data
                        WHERE stock_code = @stock_code
                        ORDER BY trade_date ASC";

                    using (var command = new NpgsqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@stock_code", stockCode);

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
                Console.WriteLine($"从数据库加载K线数据失败 [{stockCode}]: {ex.Message}");
            }

            return result;
        }
        
        /// <summary>
        /// 清理缓存（如果超过最大限制）
        /// </summary>
        private void CleanupCacheIfNeeded()
        {
            if (_klineCache.Count <= MAX_CACHE_SIZE)
                return;

            lock (_cacheLock)
            {
                if (_klineCache.Count <= MAX_CACHE_SIZE)
                    return;

                // 移除最久未访问的缓存项（LRU策略）
                var itemsToRemove = _klineCache
                    .OrderBy(kvp => kvp.Value.LastAccessTime ?? kvp.Value.CacheTime)
                    .Take(_klineCache.Count - MAX_CACHE_SIZE + 100) // 多移除一些，避免频繁清理
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in itemsToRemove)
                {
                    _klineCache.TryRemove(key, out _);
                }

                if (itemsToRemove.Count > 0)
                {
                    Console.WriteLine($"[K线缓存] 清理了 {itemsToRemove.Count} 个缓存项，当前缓存数量: {_klineCache.Count}");
                }
            }
        }

        /// <summary>
        /// 获取最新的日线数据
        /// 注意：当前使用原始价格（未复权），待复权数据填充后可切换为复权价格
        /// </summary>
        public List<DailyKlineData> GetLatestDailyData(string stockCode, int count)
        {
            var result = new List<DailyKlineData>();

            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    // 暂时使用原始价格，等复权数据填充后再切换
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

                // 反转列表，使其按日期升序排列
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
        /// 获取数据的日期范围（带缓存优化）
        /// </summary>
        public (DateTime? StartDate, DateTime? EndDate) GetDataDateRange(string stockCode)
        {
            // 尝试从缓存获取
            if (_klineCache.TryGetValue(stockCode, out var cachedData) && cachedData.Data != null && cachedData.Data.Count > 0)
            {
                var startDate = cachedData.Data[0].TradeDate;
                var endDate = cachedData.Data[cachedData.Data.Count - 1].TradeDate;
                return (startDate, endDate);
            }

            // 缓存未命中，从数据库查询
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
        /// 更新后会自动清除相关股票的缓存
        /// </summary>
        public int UpdateDailyData(List<KlineData> dataList)
        {
            if (dataList == null || dataList.Count == 0)
                return 0;

            int updatedCount = 0;
            var affectedStockCodes = new HashSet<string>();

            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    string updateSql = @"
                        UPDATE stock_daily_data
                        SET open_price = @open_price,
                            high_price = @high_price,
                            low_price = @low_price,
                            close_price = @close_price
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

                                    int affected = command.ExecuteNonQuery();
                                    if (affected > 0)
                                    {
                                        updatedCount++;
                                        affectedStockCodes.Add(data.StockCode);
                                    }
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
                
                // 清除受影响股票的缓存
                foreach (var stockCode in affectedStockCodes)
                {
                    ClearCache(stockCode);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"批量更新日线数据失败: {ex.Message}");
            }

            return updatedCount;
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
                    string sql = @"
                        SELECT amount, turnover_rate
                        FROM stock_daily_data
                        WHERE stock_code = @stock_code AND trade_date = @trade_date";
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
                // turnover_rate 列不存在时，仅查 amount
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
                    
                    // 批量查询：使用 IN 子句一次性查询所有股票
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

        /// <summary>
        /// 清除指定股票的缓存
        /// </summary>
        public void ClearCache(string stockCode)
        {
            if (string.IsNullOrEmpty(stockCode))
                return;
                
            _klineCache.TryRemove(stockCode, out _);
        }
        
        /// <summary>
        /// 清除所有缓存
        /// </summary>
        public void ClearAllCache()
        {
            _klineCache.Clear();
            Console.WriteLine("[K线缓存] 已清除所有缓存");
        }
        
        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        public (int Count, long TotalDataPoints) GetCacheStats()
        {
            int count = _klineCache.Count;
            long totalDataPoints = _klineCache.Values.Sum(v => v.Data?.Count ?? 0);
            return (count, totalDataPoints);
        }
    }
}
