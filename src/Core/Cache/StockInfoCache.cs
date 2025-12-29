using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using MQReceiver.Helpers;
using Npgsql;

namespace MQReceiver.Cache
{
    /// <summary>
    /// 股票信息缓存
    /// 单例模式，在内存中缓存股票代码和名称的映射
    /// </summary>
    public class StockInfoCache
    {
        private static readonly Lazy<StockInfoCache> _instance =
            new Lazy<StockInfoCache>(() => new StockInfoCache());

        public static StockInfoCache Instance => _instance.Value;

        // 股票代码 -> 股票名称 的映射
        private readonly ConcurrentDictionary<string, string> _stockNameCache;

        // 股票代码 -> 市场代码 的映射
        private readonly ConcurrentDictionary<string, int> _marketCodeCache;

        private readonly string _connectionString;
        private bool _isLoaded = false;
        private readonly object _loadLock = new object();

        private StockInfoCache()
        {
            _stockNameCache = new ConcurrentDictionary<string, string>();
            _marketCodeCache = new ConcurrentDictionary<string, int>();
            _connectionString = DatabaseConnectionHelper.BuildConnectionString();
        }

        /// <summary>
        /// 从数据库加载所有股票信息到内存
        /// </summary>
        public void LoadFromDatabase()
        {
            if (_isLoaded) return;

            lock (_loadLock)
            {
                if (_isLoaded) return;

                try
                {
                    using (var conn = new NpgsqlConnection(_connectionString))
                    {
                        conn.Open();
                        string sql = @"
                            SELECT stock_code, stock_name, market_code
                            FROM stock_info
                            WHERE is_active = TRUE";

                        using (var cmd = new NpgsqlCommand(sql, conn))
                        using (var reader = cmd.ExecuteReader())
                        {
                            int count = 0;
                            while (reader.Read())
                            {
                                string stockCode = reader.GetString(0);
                                string stockName = reader.IsDBNull(1) ? stockCode : reader.GetString(1);
                                int marketCode = reader.IsDBNull(2) ? 0 : reader.GetInt16(2);

                                _stockNameCache[stockCode] = stockName;
                                _marketCodeCache[stockCode] = marketCode;
                                count++;
                            }
                            Console.WriteLine($"[StockInfoCache] 从数据库加载了 {count} 条股票信息");
                        }
                    }
                    _isLoaded = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[StockInfoCache] 加载股票信息失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 确保缓存已加载
        /// </summary>
        public void EnsureLoaded()
        {
            if (!_isLoaded)
            {
                LoadFromDatabase();
            }
        }

        /// <summary>
        /// 获取股票名称
        /// </summary>
        /// <param name="stockCode">股票代码</param>
        /// <returns>股票名称，如果没找到返回股票代码本身</returns>
        public string GetStockName(string stockCode)
        {
            EnsureLoaded();

            if (string.IsNullOrEmpty(stockCode))
                return stockCode;

            if (_stockNameCache.TryGetValue(stockCode, out string name))
            {
                return name;
            }

            // 如果缓存中没有，尝试从数据库查询并缓存
            string dbName = QueryStockNameFromDB(stockCode);
            if (!string.IsNullOrEmpty(dbName))
            {
                _stockNameCache[stockCode] = dbName;
                return dbName;
            }

            return stockCode;
        }

        /// <summary>
        /// 获取市场代码
        /// </summary>
        public int GetMarketCode(string stockCode)
        {
            EnsureLoaded();

            if (_marketCodeCache.TryGetValue(stockCode, out int marketCode))
            {
                return marketCode;
            }

            return 0;
        }

        /// <summary>
        /// 更新股票信息（MQ推送时调用）
        /// </summary>
        public void UpdateStockInfo(string stockCode, string stockName, int marketCode = -1)
        {
            if (string.IsNullOrEmpty(stockCode))
                return;

            // 只有当名称不为空且不等于代码时才更新
            if (!string.IsNullOrEmpty(stockName) && stockName != stockCode)
            {
                _stockNameCache[stockCode] = stockName;
            }

            if (marketCode >= 0)
            {
                _marketCodeCache[stockCode] = marketCode;
            }
        }

        /// <summary>
        /// 批量更新股票信息
        /// </summary>
        public void UpdateStockInfoBatch(IEnumerable<(string StockCode, string StockName, int MarketCode)> stockInfoList)
        {
            foreach (var info in stockInfoList)
            {
                UpdateStockInfo(info.StockCode, info.StockName, info.MarketCode);
            }
        }

        /// <summary>
        /// 从数据库查询单个股票名称
        /// </summary>
        private string QueryStockNameFromDB(string stockCode)
        {
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();

                    // 先从 stock_info 表查
                    string sql = @"
                        SELECT stock_name FROM stock_info
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

                    // 如果 stock_info 没有，再从 stock_realtime_data 查
                    sql = @"
                        SELECT stock_name FROM stock_realtime_data
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
            catch (Exception ex)
            {
                Console.WriteLine($"[StockInfoCache] 查询股票名称失败: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 重新加载缓存
        /// </summary>
        public void Reload()
        {
            _isLoaded = false;
            _stockNameCache.Clear();
            _marketCodeCache.Clear();
            LoadFromDatabase();
        }

        /// <summary>
        /// 获取缓存的股票数量
        /// </summary>
        public int Count => _stockNameCache.Count;

        /// <summary>
        /// 检查缓存是否已加载
        /// </summary>
        public bool IsLoaded => _isLoaded;

        /// <summary>
        /// 获取缺失名称的股票数量（名称等于代码的视为缺失）
        /// </summary>
        public int GetMissingNameCount()
        {
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    string sql = @"
                        SELECT COUNT(*)
                        FROM stock_info
                        WHERE stock_name = stock_code OR stock_name IS NULL OR stock_name = ''";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        var result = cmd.ExecuteScalar();
                        return Convert.ToInt32(result);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StockInfoCache] 查询缺失名称数量失败: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 修复 stock_info 表中的错误数据
        /// 1. 修复 stock_name 为 null 的记录
        /// 2. 修复 market_code 错误的记录
        /// 3. 从Excel导出的SQL文件更新股票名称
        /// </summary>
        public void FixStockInfoData()
        {
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();

                    // 修复 stock_name 为 null 的记录
                    using (var cmd = new NpgsqlCommand(@"
                        UPDATE stock_info
                        SET stock_name = stock_code
                        WHERE stock_name IS NULL", conn))
                    {
                        int affected = cmd.ExecuteNonQuery();
                        if (affected > 0)
                            Console.WriteLine($"[StockInfoCache] 修复了 {affected} 条 stock_name 为空的记录");
                    }

                    // 修复 market_code 错误的记录
                    // 上海(1): 600/601/603/605(主板), 688(科创板), 900(B股)
                    // 深圳(0): 000/001(主板), 002/003/004(中小板), 300/301(创业板), 200(B股)
                    // 北京(2): 43/83/87/88开头
                    using (var cmd = new NpgsqlCommand(@"
                        UPDATE stock_info
                        SET
                            market_code = CASE
                                WHEN stock_code ~ '^(600|601|603|605|688|900)' THEN 1
                                WHEN stock_code ~ '^(000|001|002|003|004|300|301|200)' THEN 0
                                WHEN stock_code ~ '^(43|83|87|88)' THEN 2
                                ELSE 0
                            END,
                            market_name = CASE
                                WHEN stock_code ~ '^(600|601|603|605|688|900)' THEN '上海'
                                WHEN stock_code ~ '^(000|001|002|003|004|300|301|200)' THEN '深圳'
                                WHEN stock_code ~ '^(43|83|87|88)' THEN '北京'
                                ELSE '深圳'
                            END
                        WHERE market_code NOT IN (0, 1, 2) OR market_code IS NULL", conn))
                    {
                        int affected = cmd.ExecuteNonQuery();
                        if (affected > 0)
                            Console.WriteLine($"[StockInfoCache] 修复了 {affected} 条 market_code 错误的记录");
                    }

                    // 从Excel导出的SQL文件更新股票名称
                    ExecuteStockNamesSqlFile(conn);
                }

                // 修复后重新加载缓存
                Reload();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StockInfoCache] 修复数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 执行Excel导出的股票名称SQL文件
        /// </summary>
        private void ExecuteStockNamesSqlFile(NpgsqlConnection conn)
        {
            try
            {
                // 查找SQL文件
                string sqlFilePath = FindStockNamesSqlFile();
                if (string.IsNullOrEmpty(sqlFilePath) || !System.IO.File.Exists(sqlFilePath))
                {
                    Console.WriteLine("[StockInfoCache] 未找到股票名称SQL文件 (db/update_stock_names.sql)");
                    return;
                }

                // 检查是否需要执行：缺失名称或名称过长（超过4个中文字符）
                int needUpdateCount = 0;
                using (var cmd = new NpgsqlCommand(@"
                    SELECT COUNT(*) FROM stock_info
                    WHERE stock_name = stock_code
                       OR stock_name IS NULL
                       OR stock_name = ''
                       OR LENGTH(stock_name) > 4", conn))
                {
                    needUpdateCount = Convert.ToInt32(cmd.ExecuteScalar());
                }

                if (needUpdateCount < 100)
                {
                    Console.WriteLine($"[StockInfoCache] 股票名称基本完整（仅需更新 {needUpdateCount} 个），跳过SQL文件执行");
                    return;
                }

                Console.WriteLine($"[StockInfoCache] 检测到 {needUpdateCount} 只股票需要更新名称，正在从SQL文件导入...");

                // 读取并执行SQL文件
                string sqlContent = System.IO.File.ReadAllText(sqlFilePath, System.Text.Encoding.UTF8);

                // 分割成单条SQL语句执行（跳过注释和空行）
                int updated = 0;
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        foreach (string line in sqlContent.Split('\n'))
                        {
                            string sql = line.Trim();
                            if (string.IsNullOrEmpty(sql) || sql.StartsWith("--") ||
                                sql.StartsWith("BEGIN") || sql.StartsWith("COMMIT") ||
                                sql.StartsWith("SELECT"))
                                continue;

                            if (sql.StartsWith("UPDATE stock_info"))
                            {
                                using (var cmd = new NpgsqlCommand(sql, conn, transaction))
                                {
                                    int affected = cmd.ExecuteNonQuery();
                                    if (affected > 0) updated++;
                                }
                            }
                        }
                        transaction.Commit();
                        Console.WriteLine($"[StockInfoCache] 从SQL文件成功更新 {updated} 条股票名称");

                        // 截断所有超过4个字的名称
                        TruncateLongNames(conn);
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StockInfoCache] 执行股票名称SQL文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 智能提取股票简称
        /// 从长公司名称中提取4字简称
        /// 如："浙江世宝股份有限" -> "世宝股份"
        /// </summary>
        private void TruncateLongNames(NpgsqlConnection conn)
        {
            try
            {
                // 智能提取简称：去掉省市前缀和公司后缀
                using (var cmd = new NpgsqlCommand(@"
                    UPDATE stock_info
                    SET stock_name =
                        CASE
                            WHEN LENGTH(stock_name) <= 4 THEN stock_name
                            ELSE LEFT(
                                REGEXP_REPLACE(
                                    REGEXP_REPLACE(
                                        stock_name,
                                        '^(浙江|江苏|上海|北京|广东|深圳|广州|天津|重庆|福建|山东|河南|河北|湖南|湖北|四川|安徽|陕西|辽宁|吉林|黑龙江|江西|云南|贵州|甘肃|海南|宁夏|青海|西藏|新疆|内蒙古|广西|山西|厦门|青岛|大连|宁波|苏州|无锡|南京|杭州|武汉|成都|西安|长沙|郑州|合肥|南昌|昆明|贵阳|兰州|太原|沈阳|哈尔滨|长春)省?市?',
                                        ''
                                    ),
                                    '(股份有限公司|有限责任公司|有限公司|股份有限|集团股份|控股股份|科技股份|实业股份|电子股份|医药股份|工业股份|股份).*$',
                                    ''
                                ),
                                4
                            )
                        END,
                        update_time = CURRENT_TIMESTAMP
                    WHERE LENGTH(stock_name) > 4
                      AND stock_name <> stock_code", conn))
                {
                    int affected = cmd.ExecuteNonQuery();
                    if (affected > 0)
                        Console.WriteLine($"[StockInfoCache] 智能提取了 {affected} 条股票简称");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StockInfoCache] 提取股票简称失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 查找股票名称SQL文件
        /// </summary>
        private string FindStockNamesSqlFile()
        {
            // 尝试多个可能的路径
            string[] possiblePaths = new string[]
            {
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "db", "update_stock_names.sql"),
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "db", "update_stock_names.sql"),
                @"f:\dsfr\mqq\db\update_stock_names.sql"
            };

            foreach (string path in possiblePaths)
            {
                string fullPath = System.IO.Path.GetFullPath(path);
                if (System.IO.File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return null;
        }

        /// <summary>
        /// 从日线数据同步股票代码到stock_info表
        /// 确保所有在日线数据中出现的股票都有记录
        /// </summary>
        /// <returns>新增的记录数</returns>
        public int SyncFromDailyData()
        {
            int newCount = 0;
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();

                    // 查看日线数据中有多少股票
                    using (var cmd = new NpgsqlCommand("SELECT COUNT(DISTINCT stock_code) FROM stock_daily_data", conn))
                    {
                        var count = cmd.ExecuteScalar();
                        Console.WriteLine($"[StockInfoCache] 日线数据中股票数量: {count}");
                    }

                    // 执行同步SQL
                    // 上海(1): 600/601/603/605(主板), 688(科创板), 900(B股)
                    // 深圳(0): 000/001(主板), 002/003/004(中小板), 300/301(创业板), 200(B股)
                    // 北京(2): 43/83/87/88开头
                    string syncSql = @"
                        INSERT INTO stock_info (stock_code, stock_name, market_code, market_name, stock_type, is_active)
                        SELECT DISTINCT ON (stock_code)
                            stock_code,
                            stock_code AS stock_name,
                            CASE
                                WHEN stock_code ~ '^(600|601|603|605|688|900)' THEN 1
                                WHEN stock_code ~ '^(000|001|002|003|004|300|301|200)' THEN 0
                                WHEN stock_code ~ '^(43|83|87|88)' THEN 2
                                ELSE 0
                            END AS market_code,
                            CASE
                                WHEN stock_code ~ '^(600|601|603|605|688|900)' THEN '上海'
                                WHEN stock_code ~ '^(000|001|002|003|004|300|301|200)' THEN '深圳'
                                WHEN stock_code ~ '^(43|83|87|88)' THEN '北京'
                                ELSE '深圳'
                            END AS market_name,
                            '股票' AS stock_type,
                            TRUE AS is_active
                        FROM stock_daily_data
                        WHERE stock_code IS NOT NULL
                          AND stock_code ~ '^[0-9]{6}$'
                        ON CONFLICT (stock_code) DO UPDATE SET
                            stock_name = CASE
                                WHEN stock_info.stock_name IS NULL OR stock_info.stock_name = '' OR stock_info.stock_name = stock_info.stock_code
                                THEN EXCLUDED.stock_name
                                ELSE stock_info.stock_name
                            END,
                            market_code = EXCLUDED.market_code,
                            market_name = EXCLUDED.market_name";

                    using (var cmd = new NpgsqlCommand(syncSql, conn))
                    {
                        newCount = cmd.ExecuteNonQuery();
                        Console.WriteLine($"[StockInfoCache] 从日线数据同步完成，新增记录数: {newCount}");
                    }

                    // 查看同步后的统计
                    string statsSql = @"
                        SELECT
                            COUNT(*) AS total_count,
                            SUM(CASE WHEN stock_name = stock_code THEN 1 ELSE 0 END) AS no_name_count,
                            SUM(CASE WHEN stock_name <> stock_code THEN 1 ELSE 0 END) AS has_name_count
                        FROM stock_info";

                    using (var cmd = new NpgsqlCommand(statsSql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            Console.WriteLine($"[StockInfoCache] 同步后统计: 总数={reader.GetInt64(0)}, 无名称={reader.GetInt64(1)}, 有名称={reader.GetInt64(2)}");
                        }
                    }
                }

                // 同步后重新加载缓存
                Reload();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StockInfoCache] 从日线数据同步失败: {ex.Message}");
            }

            return newCount;
        }
    }
}
