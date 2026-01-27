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
                            int filteredByCode = 0;
                            int filteredByName = 0;
                            while (reader.Read())
                            {
                                string stockCode = reader.GetString(0);
                                string stockName = reader.IsDBNull(1) ? stockCode : reader.GetString(1);
                                int marketCode = reader.IsDBNull(2) ? 0 : reader.GetInt16(2);
                                
                                // 双重验证：代码 + 名称
                                if (!Helpers.StockDataParser.IsValidStockCode(stockCode))
                                {
                                    filteredByCode++;
                                    continue;
                                }
                                
                                // 额外检查：名称是否包含非A股关键词
                                if (Helpers.StockDataParser.IsNonAStockByName(stockName))
                                {
                                    filteredByName++;
                                    continue;
                                }

                                _stockNameCache[stockCode] = stockName;
                                _marketCodeCache[stockCode] = marketCode;
                                count++;
                            }
                            Console.WriteLine($"[StockInfoCache] 从数据库加载了 {count} 条股票信息");
                            int totalFiltered = filteredByCode + filteredByName;
                            if (totalFiltered > 0)
                            {
                                Console.WriteLine($"[StockInfoCache] 计算了 {totalFiltered} 条非A股代码");
                                Console.WriteLine($"  • 按代码规则计算: {filteredByCode} 条（指数/B股等）");
                                Console.WriteLine($"  • 按名称关键词计算: {filteredByName} 条（债券/基金等）");
                            }
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
        /// 改进：如果缓存为空，自动尝试同步和补充名称
        /// 增强：自动检查并修复常见问题
        /// </summary>
        public void EnsureLoaded()
        {
            if (!_isLoaded)
            {
                // 第一步：自动修复数据库中的常见问题
                AutoFixCommonIssues();
                
                // 第二步：加载数据
                LoadFromDatabase();
                
                // 如果加载后仍然为空，尝试从日线数据同步
                if (_stockNameCache.Count == 0)
                {
                    Console.WriteLine("[StockInfoCache] 缓存为空，尝试从日线数据同步...");
                    SyncFromDailyData();
                }
                
                // 补充内置字典中的名称到缓存
                UpdateFromBuiltinDictionary();
            }
        }
        
        /// <summary>
        /// 自动修复stock_info表的常见问题
        /// </summary>
        private void AutoFixCommonIssues()
        {
            try
            {
                Console.WriteLine("[数据库修复] 正在检查并修复常见问题...");
                
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            int totalFixed = 0;
                            
                            // 1. 修复市场代码错误
                            using (var cmd = new NpgsqlCommand(@"
                                UPDATE stock_info
                                SET market_code = CASE
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
                                    END,
                                    update_time = CURRENT_TIMESTAMP
                                WHERE (
                                    (stock_code ~ '^(600|601|603|605|688)' AND market_code != 1) OR
                                    (stock_code ~ '^(000|001|002|003|004|300|301)' AND market_code != 0) OR
                                    (stock_code ~ '^(43|83|87|88)' AND market_code != 2)
                                )", conn, transaction))
                            {
                                int affected = cmd.ExecuteNonQuery();
                                if (affected > 0)
                                {
                                    Console.WriteLine($"[数据库修复] 修复市场代码: {affected} 条");
                                    totalFixed += affected;
                                }
                            }
                            
                            // 2. 停用指数代码
                            using (var cmd = new NpgsqlCommand(@"
                                UPDATE stock_info
                                SET is_active = FALSE, update_time = CURRENT_TIMESTAMP
                                WHERE stock_code IN (
                                    '000001', '000002', '000003', '000004', '000006', 
                                    '000008', '000009', '000010', '000011', '000012', '000013',
                                    '000016', '000017', '000300', '000688', '000905', '000906',
                                    '399001', '399002', '399003', '399004', '399005', '399006'
                                ) AND is_active = TRUE", conn, transaction))
                            {
                                int affected = cmd.ExecuteNonQuery();
                                if (affected > 0)
                                {
                                    Console.WriteLine($"[数据库修复] 停用指数代码: {affected} 条");
                                    totalFixed += affected;
                                }
                            }
                            
                            // 3. 停用B股
                            using (var cmd = new NpgsqlCommand(@"
                                UPDATE stock_info
                                SET is_active = FALSE, update_time = CURRENT_TIMESTAMP
                                WHERE (stock_code ~ '^200' OR stock_code ~ '^900')
                                AND is_active = TRUE", conn, transaction))
                            {
                                int affected = cmd.ExecuteNonQuery();
                                if (affected > 0)
                                {
                                    Console.WriteLine($"[数据库修复] 停用B股代码: {affected} 条");
                                    totalFixed += affected;
                                }
                            }
                            
                            // 4. 停用已知的已退市股票
                            using (var cmd = new NpgsqlCommand(@"
                                UPDATE stock_info
                                SET is_active = FALSE, update_time = CURRENT_TIMESTAMP
                                WHERE stock_code IN (
                                    '000018', '000033', '000046', '000139', '000669', 
                                    '000760', '000816', '000914', '000981', '600656'
                                ) AND is_active = TRUE", conn, transaction))
                            {
                                int affected = cmd.ExecuteNonQuery();
                                if (affected > 0)
                                {
                                    Console.WriteLine($"[数据库修复] 停用已退市股票: {affected} 条");
                                    totalFixed += affected;
                                }
                            }
                            
                            transaction.Commit();
                            
                            if (totalFixed > 0)
                            {
                                Console.WriteLine($"[数据库修复] 总计修复 {totalFixed} 条记录");
                            }
                            else
                            {
                                Console.WriteLine("[数据库修复] ✓ 数据库状态良好，无需修复");
                            }
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            Console.WriteLine($"[数据库修复] 修复失败（已回滚）: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[数据库修复] 无法连接数据库: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取股票名称
        /// 改进：添加详细日志，方便排查名称缺失问题
        /// </summary>
        /// <param name="stockCode">股票代码</param>
        /// <returns>股票名称，如果没找到返回股票代码本身</returns>
        public string GetStockName(string stockCode)
        {
            EnsureLoaded();

            if (string.IsNullOrEmpty(stockCode))
                return stockCode;

            // 首先检查内置字典（优先级最高，确保ST股票等特殊名称正确）
            string builtinName = GetBuiltinStockName(stockCode);
            if (!string.IsNullOrEmpty(builtinName))
            {
                _stockNameCache[stockCode] = builtinName;
                return builtinName;
            }

            if (_stockNameCache.TryGetValue(stockCode, out string name))
            {
                // 如果缓存的名称不是代码本身，且不是不完整的ST名称，返回缓存值
                if (name != stockCode && !IsIncompleteName(name))
                {
                    return name;
                }
                
                // 如果名称不完整，记录日志但仍返回（总比显示代码好）
                if (IsIncompleteName(name))
                {
                    // 静默处理，不输出太多日志
                    return name;
                }
            }

            // 如果缓存中有值（即使不完整），但内置字典没有，返回缓存值
            if (!string.IsNullOrEmpty(name) && name != stockCode)
            {
                return name;
            }

            // 如果缓存中没有，尝试从数据库查询并缓存
            string dbName = QueryStockNameFromDB(stockCode);
            if (!string.IsNullOrEmpty(dbName) && dbName != stockCode)
            {
                _stockNameCache[stockCode] = dbName;
                return dbName;
            }

            // 最后兜底：返回股票代码（并记录到静态集合，避免重复输出）
            lock (_missingNamesLock)
            {
                if (!_missingNamesReported.Contains(stockCode))
                {
                    _missingNamesReported.Add(stockCode);
                    if (_missingNamesReported.Count <= 20) // 只输出前20个
                    {
                        Console.WriteLine($"[StockInfoCache] 警告: 未找到 {stockCode} 的股票名称，将显示代码");
                    }
                }
            }
            
            return stockCode;
        }
        
        // 用于跟踪已报告的缺失名称，避免重复日志
        private static readonly HashSet<string> _missingNamesReported = new HashSet<string>();
        private static readonly object _missingNamesLock = new object();

        /// <summary>
        /// 检查名称是否不完整（如截断的ST名称）
        /// </summary>
        private bool IsIncompleteName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return true;

            // 检查是否是不完整的ST名称（如"S*ST"、"*ST"、"ST"、"*ST步"等）
            // 完整的ST名称应该至少有ST前缀+2个汉字（4个字符以上）
            // 修正正则：只匹配长度<=3的ST名称，或者ST后面只有1个字的
            if (name.Length <= 3 && System.Text.RegularExpressions.Regex.IsMatch(name, @"^[S\*]*ST"))
            {
                return true; // ST前缀但名称太短
            }
            
            // 检查ST后面只有1个字符的情况（如"ST步"、"*ST龙"）
            if (System.Text.RegularExpressions.Regex.IsMatch(name, @"^[S\*]*ST.{1}$"))
            {
                return true; // ST + 1个字，视为不完整
            }

            return false;
        }

        /// <summary>
        /// 从内置字典获取股票名称
        /// </summary>
        private string GetBuiltinStockName(string stockCode)
        {
            var builtinNames = new Dictionary<string, string>
            {
                // 深圳主板股票 (000开头)
                {"000022", "深赤湾A"}, {"000092", "惠天热电"}, {"000094", "大名城"},
                // 移除 000101 - 这是债券指数，不是A股！
                {"000105", "永鼎股份"}, {"000106", "重庆路桥"}, {"000112", "浙江东日"}, {"000113", "浙江东方"},
                {"000116", "三峡水利"}, {"000117", "西宁特钢"}, {"000119", "瑞茂通"}, {"000122", "兰花科创"},
                {"000125", "铁龙物流"}, {"000130", "波导股份"}, {"000133", "东湖高新"},
                {"000135", "乐凯胶片"}, {"000141", "中国宝安"}, {"000145", "廊坊发展"}, {"000152", "维科技术"},
                {"000160", "巨化股份"}, {"000828", "东莞控股"}, {"000830", "鲁西化工"}, {"000851", "高鸿股份"},
                {"000853", "冀东装备"}, {"000891", "阳光城"}, {"000982", "宁波能源"},
                
                // 上海主板股票 (600开头)
                {"600057", "厦门象屿"}, {"600071", "凤凰光学"}, {"600073", "光明肉业"}, 
                {"600101", "明星电力"}, {"600132", "重庆啤酒"}, {"600854", "春兰股份"},
                
                // 创业板股票 (300开头)
                {"300033", "同花顺"},
                // ST股票（注意：ST股票名称可能会变化，以实时数据为准）
                {"603268", "*ST松发"}, {"000693", "*ST华泽"}, {"000699", "S*ST佳唐"}, {"000564", "ST供销大集"}, {"000498", "*ST莲花"},
                {"000662", "*ST天夏"}, {"000673", "*ST当代"}, {"000760", "*ST斯太"}, {"000816", "*ST慧业"},
                {"000981", "*ST银亿"}, {"001270", "*ST铖昌"}, {"002029", "七匹狼"}, {"002070", "*ST众和"}, {"002209", "达意隆"},
                {"002113", "*ST天润"}, {"002220", "*ST天宝"}, {"002427", "*ST尤夫"}, {"002450", "*ST康得"},
                {"002569", "*ST步森"}, {"002647", "*ST仁东"}, {"600086", "*ST龙力"}, {"600145", "*ST新亿"},
                {"600155", "*ST宝硕"}, {"600175", "*ST祥源"}, {"600185", "*ST华讯"}, {"600196", "*ST复星"},
                {"600217", "*ST秦岭"}, {"600220", "*ST南药"}, {"600242", "*ST中昌"}, {"600275", "*ST昌鱼"},
                {"600289", "*ST信通"}, {"600291", "*ST西水"}, {"600320", "*ST薇创"}, {"600385", "*ST金泰"},
                {"600396", "*ST金山"}, {"600421", "*ST仰帆"}, {"600462", "*ST九有"}, {"600481", "*ST双良"},
                {"600499", "*ST科林"}, {"600518", "*ST康美"}, {"600556", "*ST天下秀"}, {"600568", "*ST中珠"},
                {"600576", "*ST祥龙"}, {"600577", "*ST精伦"}, {"600595", "*ST中孚"}, {"600604", "*ST市北"},
                {"600614", "*ST鹏起"}, {"600634", "*ST富控"}, {"600652", "*ST游久"}, {"600654", "*ST中安"},
                {"600656", "*ST退市博元"}, {"600666", "*ST瑞德"}, {"600677", "*ST航通"}, {"600696", "*ST岩石"},
                {"600698", "*ST湖南天雁"}, {"600701", "*ST工新"}, {"600708", "*ST海越"}, {"600726", "*ST华源"},
                {"600747", "*ST大控"}, {"600769", "*ST祥龙"}, {"600793", "*ST宜宾纸业"}, {"600796", "*ST钱江"},
                {"600817", "*ST宏盛"}, {"600870", "*ST厦华"}, {"600891", "*ST秋林"}, {"600978", "*ST宜华"},
                {"601558", "*ST华锐"}, {"603813", "*ST原尚"}, {"603828", "ST柯利达"},
                // 补充的股票
                {"000129", "泰山石油"}, {"000908", "*ST景峰"}, {"002289", "*ST宇顺"}, {"002586", "ST围海"},
                {"603843", "*ST正邦"}
            };

            return builtinNames.TryGetValue(stockCode, out string name) ? name : null;
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
        /// 从长公司名称中提取简称
        /// 如："浙江世宝股份有限" -> "世宝股份"
        /// 注意：ST类股票保留完整前缀（ST/S*ST/*ST等）+ 简称
        /// </summary>
        private void TruncateLongNames(NpgsqlConnection conn)
        {
            try
            {
                // 智能提取简称：去掉省市前缀和公司后缀
                // ST类股票特殊处理：保留ST前缀 + 后面的简称（最多6字符总长度）
                using (var cmd = new NpgsqlCommand(@"
                    UPDATE stock_info
                    SET stock_name =
                        CASE
                            -- ST类股票：保留前缀，截取到合理长度（6-8字符）
                            WHEN stock_name ~ '^[S\*]*ST' THEN LEFT(stock_name, 8)
                            -- 普通股票：长度<=4直接保留
                            WHEN LENGTH(stock_name) <= 4 THEN stock_name
                            -- 普通股票：去掉省市前缀和公司后缀后截取4字符
                            ELSE LEFT(
                                REGEXP_REPLACE(
                                    REGEXP_REPLACE(
                                        stock_name,
                                        '^(浙江|江苏|上海|北京|广东|深圳|广州|天津|重庆|福建|山东|河南|河北|湖南|湖北|四川|安徽|陕西|辽宁|吉林|黑龙江|江西|云南|贵州|甘肃|海南|宁夏|青海|西藏|新疆|内蒙古|广西|山西|厦门|青岛|大连|宁波|苏州|无锁|南京|杭州|武汉|成都|西安|长沙|郑州|合肥|南昌|昆明|贵阳|兰州|太原|沈阳|哈尔滨|长春)省?市?',
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
                      AND stock_name <> stock_code
                      AND stock_name !~ '^[S\*]*ST.{2,4}$'", conn))
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

                    // 从实时数据表同步股票名称
                    SyncNamesFromRealtimeData(conn);
                }

                // 从内置字典更新缺失的股票名称
                UpdateFromBuiltinDictionary();

                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();

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
                Console.WriteLine($"[StockInfoCache] 错误详情: {ex.GetType().Name}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[StockInfoCache] 内部错误: {ex.InnerException.Message}");
                }
                Console.WriteLine($"[StockInfoCache] 堆栈跟踪: {ex.StackTrace}");
                
                // 即使同步失败，也尝试加载现有数据
                try
                {
                    Console.WriteLine($"[StockInfoCache] 尝试加载现有缓存数据...");
                    LoadFromDatabase();
                }
                catch (Exception loadEx)
                {
                    Console.WriteLine($"[StockInfoCache] 加载缓存失败: {loadEx.Message}");
                }
            }

            return newCount;
        }

        /// <summary>
        /// 从实时数据表同步股票名称到stock_info表
        /// </summary>
        private void SyncNamesFromRealtimeData(NpgsqlConnection conn)
        {
            try
            {
                // 从 stock_realtime_data 表获取股票名称，更新到 stock_info
                string updateSql = @"
                    UPDATE stock_info si
                    SET stock_name = rt.stock_name,
                        update_time = CURRENT_TIMESTAMP
                    FROM (
                        SELECT DISTINCT ON (stock_code) stock_code, stock_name
                        FROM stock_realtime_data
                        WHERE stock_name IS NOT NULL
                          AND stock_name <> ''
                          AND stock_name <> stock_code
                        ORDER BY stock_code, update_time DESC
                    ) rt
                    WHERE si.stock_code = rt.stock_code
                      AND (si.stock_name IS NULL OR si.stock_name = '' OR si.stock_name = si.stock_code)";

                using (var cmd = new NpgsqlCommand(updateSql, conn))
                {
                    int updated = cmd.ExecuteNonQuery();
                    if (updated > 0)
                    {
                        Console.WriteLine($"[StockInfoCache] 从实时数据同步了 {updated} 条股票名称");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StockInfoCache] 从实时数据同步名称失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从内置字典更新股票名称（用于补充数据库中缺失的名称）
        /// </summary>
        public void UpdateFromBuiltinDictionary()
        {
            // 内置的股票名称字典（深圳主板股票）
            var stockNames = new Dictionary<string, string>
            {
                // 000开头的深圳股票
                {"000001", "平安银行"}, {"000002", "万科A"}, {"000004", "国农科技"}, {"000005", "ST星源"},
                {"000006", "深振业A"}, {"000007", "全新好"}, {"000008", "神州高铁"}, {"000009", "中国宝安"},
                {"000010", "美丽生态"}, {"000011", "深物业A"}, {"000012", "南玻A"}, {"000014", "沙河股份"},
                {"000016", "深康佳A"}, {"000017", "深中华A"}, {"000018", "神城A退"}, {"000019", "深粮控股"},
                {"000020", "深华发A"}, {"000021", "深科技"}, {"000022", "深赤湾A"}, {"000023", "深天地A"},
                {"000025", "特力A"}, {"000026", "飞亚达A"}, {"000027", "深圳能源"}, {"000028", "国药一致"},
                {"000029", "深深房A"}, {"000030", "富奥股份"}, {"000031", "大悦城"}, {"000032", "深桑达A"},
                {"000034", "神州数码"}, {"000035", "中国天楹"}, {"000036", "华联控股"},
                {"000037", "深南电A"}, {"000038", "深大通"}, {"000039", "中集集团"}, {"000040", "东旭蓝天"},
                {"000042", "中洲控股"}, {"000043", "中航善达"}, {"000045", "深纺织A"}, {"000046", "泛海控股"},
                {"000048", "京基智农"}, {"000049", "德赛电池"}, {"000050", "深天马A"},                 {"000055", "方大集团"}, {"000056", "皇庭国际"}, {"000058", "深赛格"}, {"000059", "华锦股份"},
                {"000060", "中金岭南"}, {"000061", "农产品"}, {"000062", "深圳华强"}, {"000063", "中兴通讯"},
                {"000065", "北方国际"}, {"000066", "中国长城"}, {"000068", "华控赛格"}, {"000069", "华侨城A"},
                {"000070", "特发信息"}, {"000078", "海王生物"},
                {"000088", "盐田港"}, {"000089", "深圳机场"}, {"000090", "天健集团"}, {"000092", "惠天热电"},
                {"000093", "新大洲A"}, {"000094", "大名城"}, {"000096", "广聚能源"}, {"000099", "中信海直"},
                {"000100", "TCL科技"}, 
                // 移除 000101 - 这是"上证5年期信用债指数"（债券指数），不是A股！
                {"000105", "永鼎股份"}, {"000106", "重庆路桥"},
                {"000112", "浙江东日"}, {"000113", "浙江东方"}, {"000116", "三峡水利"}, {"000117", "西宁特钢"},
                {"000119", "瑞茂通"}, {"000122", "兰花科创"}, {"000125", "铁龙物流"}, {"000130", "波导股份"},
                {"000133", "东湖高新"}, {"000135", "乐凯胶片"}, {"000141", "中国宝安"},
                {"000145", "廊坊发展"}, {"000152", "维科技术"}, {"000153", "丰原药业"}, {"000155", "川能动力"},
                {"000156", "华数传媒"}, {"000157", "中联重科"}, {"000158", "常山北明"}, {"000159", "国际实业"},
                {"000160", "巨化股份"}, {"000166", "申万宏源"}, {"000400", "许继电气"}, {"000401", "冀东水泥"},
                {"000402", "金融街"}, {"000403", "派林生物"}, {"000404", "华意压缩"}, {"000407", "胜利股份"},
                {"000408", "金谷源"}, {"000409", "云鼎科技"}, {"000410", "沈阳机床"}, {"000411", "英特集团"},
                {"000413", "东旭光电"}, {"000415", "渤海租赁"}, {"000417", "合肥百货"}, {"000418", "小天鹅A"},
                {"000419", "通程控股"}, {"000420", "吉林化纤"}, {"000421", "南京公用"}, {"000422", "湖北宜化"},
                {"000423", "东阿阿胶"}, {"000425", "徐工机械"}, {"000426", "兴业矿业"}, {"000428", "华天酒店"},
                {"000429", "粤高速A"}, {"000430", "张家界"}, {"000501", "鄂武商A"}, {"000502", "绿景控股"},
                {"000503", "国新健康"}, {"000504", "南华生物"}, {"000505", "京粮控股"}, {"000506", "中润资源"},
                {"000507", "珠海港"}, {"000509", "华塑控股"}, {"000510", "金路集团"}, {"000513", "丽珠集团"},
                {"000514", "渝开发"}, {"000516", "国际医学"}, {"000517", "荣安地产"}, {"000518", "四环生物"},
                {"000519", "中兵红箭"}, {"000520", "长航凤凰"}, {"000521", "美菱电器"}, {"000523", "广州浪奇"},
                {"000524", "岭南控股"}, {"000525", "红太阳"}, {"000526", "紫光学大"}, {"000528", "柳工"},
                {"000529", "广弘控股"}, {"000530", "大冷股份"}, {"000531", "穗恒运A"}, {"000532", "华金资本"},
                {"000533", "万家乐"}, {"000534", "万泽股份"}, {"000536", "华映科技"}, {"000537", "广宇发展"},
                {"000538", "云南白药"}, {"000539", "粤电力A"}, {"000540", "中天金融"}, {"000541", "佛山照明"},
                {"000543", "皖能电力"}, {"000544", "中原环保"}, {"000545", "金浦钛业"}, {"000546", "金圆股份"},
                {"000547", "航天发展"}, {"000548", "湖南投资"}, {"000550", "江铃汽车"}, {"000551", "创元科技"},
                {"000552", "靖远煤电"}, {"000553", "安道麦A"}, {"000554", "泰山石油"}, {"000555", "神州信息"},
                {"000557", "西部创业"}, {"000558", "莱茵体育"}, {"000559", "万向钱潮"}, {"000560", "我爱我家"},
                {"000561", "烽火电子"}, {"000563", "陕国投A"}, {"000564", "供销大集"}, {"000565", "渝三峡A"},
                {"000566", "海南海药"}, {"000567", "海德股份"}, {"000568", "泸州老窖"}, {"000570", "苏常柴A"},
                {"000571", "新大洲A"}, {"000572", "海马汽车"}, {"000573", "粤宏远A"}, {"000576", "广东甘化"},
                {"000581", "威孚高科"}, {"000582", "北部湾港"}, {"000584", "哈工智能"}, {"000585", "东北电气"},
                {"000586", "汇源通信"}, {"000587", "金洲慈航"}, {"000589", "黔轮胎A"}, {"000590", "启迪古汉"},
                {"000591", "太阳能"}, {"000592", "平潭发展"}, {"000593", "大通燃气"}, {"000595", "宝塔实业"},
                {"000596", "古井贡酒"}, {"000597", "东北制药"}, {"000598", "兴蓉环境"}, {"000599", "青岛双星"},
                {"000600", "建投能源"}, {"000601", "韶能股份"}, {"000603", "盛达资源"}, {"000605", "渤海股份"},
                {"000606", "顺利办"}, {"000607", "华媒控股"}, {"000608", "阳光股份"}, {"000609", "中迪投资"},
                {"000610", "西安旅游"}, {"000611", "天首发展"}, {"000612", "焦作万方"}, {"000613", "大东海A"},
                {"000615", "奥园美谷"}, {"000616", "海航投资"}, {"000617", "中油资本"}, {"000619", "海螺新材"},
                {"000620", "新华联"}, {"000622", "恒立实业"}, {"000623", "吉林敖东"}, {"000625", "长安汽车"},
                {"000626", "远大控股"}, {"000627", "天茂集团"}, {"000628", "高新发展"}, {"000629", "攀钢钒钛"},
                {"000630", "铜陵有色"}, {"000631", "顺发恒业"}, {"000632", "三木集团"}, {"000633", "合金投资"},
                {"000635", "英力特"}, {"000636", "风华高科"}, {"000637", "茂化实华"}, {"000638", "万方发展"},
                {"000639", "西王食品"}, {"000650", "仁和药业"}, {"000651", "格力电器"}, {"000652", "泰达股份"},
                {"000655", "金岭矿业"}, {"000656", "金科股份"}, {"000657", "中钨高新"}, {"000659", "珠海中富"},
                {"000661", "长春高新"}, {"000662", "天夏智慧"}, {"000663", "永安林业"}, {"000665", "湖北广电"},
                {"000666", "经纬纺机"}, {"000667", "美好置业"}, {"000668", "荣丰控股"}, {"000669", "金鸿退"},
                {"000670", "盈方微"}, {"000671", "阳光城"}, {"000672", "上峰水泥"}, {"000673", "当代文体"},
                {"000676", "智度股份"}, {"000677", "恒天海龙"}, {"000678", "襄阳轴承"}, {"000679", "大连友谊"},
                {"000680", "山推股份"}, {"000681", "视觉中国"}, {"000682", "东方电子"}, {"000683", "远兴能源"},
                {"000685", "中山公用"}, {"000686", "东北证券"}, {"000687", "华讯方舟"}, {"000688", "国城矿业"},
                {"000690", "宝新能源"}, {"000691", "亚太实业"}, {"000692", "惠天热电"}, {"000695", "滨海能源"},
                {"000697", "炼石航空"}, {"000698", "沈阳化工"}, {"000700", "模塑科技"}, {"000701", "厦门信达"},
                {"000702", "正虹科技"}, {"000703", "恒逸石化"}, {"000705", "浙江震元"}, {"000707", "双环科技"},
                {"000708", "中信特钢"}, {"000709", "河钢股份"}, {"000710", "贝瑞基因"}, {"000711", "京蓝科技"},
                {"000712", "锦龙股份"}, {"000713", "丰乐种业"}, {"000715", "中兴商业"}, {"000716", "黑芝麻"},
                {"000717", "韶钢松山"}, {"000718", "苏宁环球"}, {"000719", "中原传媒"}, {"000720", "新能泰山"},
                {"000721", "西安饮食"}, {"000722", "湖南发展"}, {"000723", "美锦能源"}, {"000725", "京东方A"},
                {"000726", "鲁泰A"}, {"000727", "华东科技"}, {"000728", "国元证券"}, {"000729", "燕京啤酒"},
                {"000731", "四川美丰"}, {"000732", "泰禾集团"}, {"000733", "振华科技"}, {"000735", "罗牛山"},
                {"000736", "中交地产"}, {"000737", "南风化工"}, {"000738", "航发控制"}, {"000739", "普洛药业"},
                {"000750", "国海证券"}, {"000751", "锌业股份"}, {"000752", "西藏发展"}, {"000753", "漳州发展"},
                {"000755", "山西路桥"}, {"000756", "新华制药"}, {"000757", "浩物股份"}, {"000758", "中色股份"},
                {"000759", "中百集团"}, {"000760", "斯太尔"}, {"000761", "本钢板材"}, {"000762", "西藏矿业"},
                {"000765", "中航重机"}, {"000766", "通化金马"}, {"000767", "漳泽电力"}, {"000768", "中航飞机"},
                {"000776", "广发证券"}, {"000777", "中核科技"}, {"000778", "新兴铸管"}, {"000779", "三毛派神"},
                {"000780", "平庄能源"}, {"000782", "美达股份"}, {"000783", "长江证券"}, {"000785", "武汉中商"},
                {"000786", "北新建材"}, {"000788", "北大医药"}, {"000789", "万年青"}, {"000790", "华神集团"},
                {"000791", "甘肃电投"}, {"000792", "盐湖股份"}, {"000793", "华闻传媒"}, {"000795", "英洛华"},
                {"000796", "凯撒旅业"}, {"000797", "中国武夷"}, {"000798", "中水渔业"}, {"000799", "酒鬼酒"},
                {"000800", "一汽解放"}, {"000801", "四川九洲"}, {"000802", "北京文化"}, {"000803", "金宇车城"},
                {"000807", "云铝股份"}, {"000809", "铁岭新城"}, {"000810", "创维数字"}, {"000811", "冰轮环境"},
                {"000812", "陕西金叶"}, {"000813", "德展健康"}, {"000815", "美利云"}, {"000816", "慧业退"},
                {"000817", "辽河油田"}, {"000818", "航锦科技"}, {"000819", "岳阳兴长"}, {"000820", "神雾节能"},
                {"000821", "京山轻机"}, {"000822", "山东海化"}, {"000823", "超声电子"}, {"000825", "太钢不锈"},
                {"000826", "启迪环境"}, {"000828", "东莞控股"}, {"000829", "天音控股"}, {"000830", "鲁西化工"},
                {"000831", "五矿稀土"}, {"000833", "粤桂股份"}, {"000835", "长城动漫"}, {"000836", "鑫茂科技"},
                {"000837", "秦川机床"}, {"000838", "财信发展"}, {"000839", "中信国安"}, {"000848", "承德露露"},
                {"000850", "华茂股份"}, {"000851", "高鸿股份"}, {"000852", "石化机械"}, {"000853", "冀东装备"},
                {"000855", "皖维高新"}, {"000856", "云南锗业"}, {"000857", "酒钢宏兴"}, {"000858", "五粮液"},
                {"000859", "国风塑业"}, {"000860", "顺鑫农业"}, {"000861", "海印股份"}, {"000862", "银星能源"},
                {"000863", "三湘印象"}, {"000868", "安凯客车"}, {"000869", "张裕A"}, {"000875", "吉电股份"},
                {"000876", "新希望"}, {"000877", "天山股份"}, {"000878", "云南铜业"}, {"000880", "潍柴重机"},
                {"000881", "中广核技"}, {"000882", "华联股份"}, {"000883", "湖北能源"}, {"000885", "城发环境"},
                {"000886", "海南高速"}, {"000887", "中鼎股份"}, {"000888", "峨眉山A"}, {"000889", "茂业通信"},
                {"000890", "法尔胜"}, {"000891", "阳光城"}, {"000892", "欢瑞世纪"}, {"000893", "东凌国际"},
                {"000895", "双汇发展"}, {"000897", "津滨发展"}, {"000898", "鞍钢股份"}, {"000899", "赣能股份"},
                {"000900", "现代投资"}, {"000901", "航天科技"}, {"000902", "新洋丰"}, {"000903", "云内动力"},
                {"000905", "厦门港务"}, {"000906", "浙商中拓"}, {"000908", "景峰医药"}, {"000909", "数源科技"},
                {"000910", "大亚圣象"}, {"000911", "南宁糖业"}, {"000912", "泸天化"}, {"000913", "钱江摩托"},
                {"000915", "山大华特"}, {"000917", "电广传媒"}, {"000919", "金陵药业"}, {"000920", "南方汇通"},
                {"000921", "海信家电"}, {"000922", "佳电股份"}, {"000923", "河钢资源"}, {"000925", "众合科技"},
                {"000926", "福星股份"}, {"000927", "中国铁物"}, {"000928", "中钢国际"}, {"000929", "兰州黄河"},
                {"000930", "中粮生化"}, {"000931", "中关村"}, {"000932", "华菱钢铁"}, {"000933", "神火股份"},
                {"000935", "四川双马"}, {"000936", "华西股份"}, {"000937", "冀中能源"}, {"000938", "紫光股份"},
                {"000948", "南天信息"}, {"000949", "新乡化纤"}, {"000950", "重药控股"}, {"000951", "中国重汽"},
                {"000952", "广济药业"}, {"000953", "河池化工"}, {"000955", "欣龙控股"}, {"000957", "中通客车"},
                {"000958", "东方能源"}, {"000959", "首钢股份"}, {"000960", "锡业股份"}, {"000961", "中南建设"},
                {"000962", "东方钽业"}, {"000963", "华东医药"}, {"000965", "天保基建"}, {"000966", "长源电力"},
                {"000967", "盈峰环境"}, {"000968", "蓝焰控股"}, {"000969", "安泰科技"}, {"000970", "中科三环"},
                {"000971", "高升控股"}, {"000972", "新中基"}, {"000973", "佛塑科技"}, {"000975", "银泰黄金"},
                {"000976", "华铁股份"}, {"000977", "浪潮信息"}, {"000978", "桂林旅游"}, {"000980", "众泰汽车"},
                {"000981", "银亿股份"}, {"000982", "宁波能源"}, {"000983", "西山煤电"}, {"000985", "大庆华科"},
                {"000987", "越秀金控"}, {"000988", "华工科技"}, {"000989", "九芝堂"}, {"000990", "诚志股份"},
                {"000993", "闽东电力"}, {"000995", "皇台酒业"}, {"000996", "中国中期"}, {"000997", "新大陆"},
                {"000998", "隆平高科"}, {"000999", "华润三九"},
                
                // 深圳中小板股票 (002开头) - 补充重要的映射
                {"002209", "达意隆"},  // ✅ 重要：修正达意隆的完整名称
                
                // 上海主板股票 (600开头) - 补充重要的映射
                {"600101", "明星电力"},  // ✅ 重要：修正明星电力的正确代码
            };

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    int updated = 0;

                    foreach (var kvp in stockNames)
                    {
                        // 内置字典的名称总是覆盖数据库中的值（确保名称正确）
                        string updateSql = @"
                            UPDATE stock_info
                            SET stock_name = @name, update_time = CURRENT_TIMESTAMP
                            WHERE stock_code = @code
                              AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code OR stock_name <> @name)";

                        using (var cmd = new NpgsqlCommand(updateSql, conn))
                        {
                            cmd.Parameters.AddWithValue("code", kvp.Key);
                            cmd.Parameters.AddWithValue("name", kvp.Value);
                            int affected = cmd.ExecuteNonQuery();
                            if (affected > 0) updated++;
                        }

                        // 同时更新内存缓存（无论数据库是否更新，都确保缓存中有正确的名称）
                        _stockNameCache[kvp.Key] = kvp.Value;
                    }

                    if (updated > 0)
                    {
                        Console.WriteLine($"[StockInfoCache] 从内置字典更新了 {updated} 条股票名称");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StockInfoCache] 从内置字典更新失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 诊断指定股票代码在各表中的情况
        /// </summary>
        public void DiagnoseStockCodes(string[] stockCodes)
        {
            Console.WriteLine("\n========== 股票代码诊断 ==========");
            Console.WriteLine("代码     | stock_info名称 | realtime名称 | daily存在");
            Console.WriteLine(new string('-', 60));

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();

                    foreach (var code in stockCodes)
                    {
                        string trimmedCode = code.Trim();
                        string infoName = "-";
                        string rtName = "-";
                        bool hasDaily = false;

                        // 查 stock_info
                        using (var cmd = new NpgsqlCommand("SELECT stock_name FROM stock_info WHERE stock_code = @code", conn))
                        {
                            cmd.Parameters.AddWithValue("code", trimmedCode);
                            var result = cmd.ExecuteScalar();
                            if (result != null && result != DBNull.Value)
                                infoName = result.ToString();
                        }

                        // 查 stock_realtime_data
                        using (var cmd = new NpgsqlCommand("SELECT stock_name FROM stock_realtime_data WHERE stock_code = @code LIMIT 1", conn))
                        {
                            cmd.Parameters.AddWithValue("code", trimmedCode);
                            var result = cmd.ExecuteScalar();
                            if (result != null && result != DBNull.Value)
                                rtName = result.ToString();
                        }

                        // 查 stock_daily_data
                        using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM stock_daily_data WHERE stock_code = @code", conn))
                        {
                            cmd.Parameters.AddWithValue("code", trimmedCode);
                            hasDaily = Convert.ToInt64(cmd.ExecuteScalar()) > 0;
                        }

                        // 如果名称等于代码，标记为缺失
                        if (infoName == trimmedCode) infoName = $"{trimmedCode}(缺)";
                        if (rtName == trimmedCode) rtName = $"{trimmedCode}(缺)";

                        Console.WriteLine($"{trimmedCode,-8} | {infoName,-14} | {rtName,-12} | {(hasDaily ? "是" : "否")}");
                    }

                    // 统计realtime表中有多少不同的名称
                    Console.WriteLine("\n【realtime表名称统计】");
                    using (var cmd = new NpgsqlCommand(@"
                        SELECT
                            COUNT(DISTINCT stock_code) as total,
                            COUNT(DISTINCT CASE WHEN stock_name IS NOT NULL AND stock_name <> '' AND stock_name <> stock_code THEN stock_code END) as has_name
                        FROM stock_realtime_data", conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                Console.WriteLine($"  总股票数: {reader.GetInt64(0)}, 有名称: {reader.GetInt64(1)}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"诊断失败: {ex.Message}");
            }
        }
    }
}
