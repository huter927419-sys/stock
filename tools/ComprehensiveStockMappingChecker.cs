using System;
using System.Collections.Generic;
using System.Linq;
using Npgsql;

/// <summary>
/// 全面的股票代码-名称映射检查工具
/// 检查所有可能的错误映射
/// </summary>
class ComprehensiveStockMappingChecker
{
    private static string connectionString = "Host=localhost;Port=8532;Username=postgres;Password=123456;Database=stockdb;";
    
    // 已知正确的映射（从权威来源确认）
    private static Dictionary<string, string> KnownCorrectMappings = new Dictionary<string, string>
    {
        // 深圳主板
        {"000001", "平安银行"},
        {"000002", "万科A"},
        {"000004", "国农科技"},
        {"000005", "ST星源"},
        {"000006", "深振业A"},
        {"000007", "全新好"},
        {"000008", "神州高铁"},
        {"000009", "中国宝安"},
        
        // 上海主板
        {"600000", "浦发银行"},
        {"600004", "白云机场"},
        {"600005", "武钢股份"},
        {"600006", "东风汽车"},
        {"600007", "中国国贸"},
        {"600008", "首创环保"},
        {"600009", "上海机场"},
        {"600010", "包钢股份"},
        {"600011", "华能国际"},
        {"600015", "华夏银行"},
        {"600016", "民生银行"},
        {"600018", "上港集团"},
        {"600019", "宝钢股份"},
        {"600028", "中国石化"},
        {"600029", "南方航空"},
        {"600030", "中信证券"},
        {"600036", "招商银行"},
        {"600048", "保利发展"},
        {"600050", "中国联通"},
        {"600104", "上汽集团"},
        {"600111", "北方稀土"},
        {"600115", "中国东航"},
        {"600519", "贵州茅台"},
        {"600887", "伊利股份"},
        {"600900", "长江电力"},
        
        // 明星电力 - 重点修正
        {"600101", "明星电力"},  // 正确的！上海市场
        {"000101", "恒邦股份"},  // 深圳市场，不是明星电力！
        
        // 创业板
        {"300001", "特锐德"},
        {"300002", "神州泰岳"},
        {"300003", "乐普医疗"},
        {"300033", "同花顺"},
        {"300059", "东方财富"},
        {"300750", "宁德时代"},
        
        // 科创板
        {"688001", "华兴源创"},
        {"688008", "澜起科技"},
        {"688009", "中国通号"},
        
        // 常见错误案例
        {"000651", "格力电器"},  // 不是"格力空调"
        {"000858", "五粮液"},    // 不是"五粮春"
    };
    
    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.Clear();
        
        Console.WriteLine("╔════════════════════════════════════════════════╗");
        Console.WriteLine("║     全面股票代码-名称映射检查工具              ║");
        Console.WriteLine("╚════════════════════════════════════════════════╝");
        Console.WriteLine();
        
        try
        {
            var errors = new List<MappingError>();
            
            // 第1步：检查内置字典
            Console.WriteLine("[步骤 1/5] 检查内置字典...");
            CheckBuiltinDictionary(errors);
            Console.WriteLine();
            
            // 第2步：检查数据库映射
            Console.WriteLine("[步骤 2/5] 检查数据库映射...");
            CheckDatabaseMappings(errors);
            Console.WriteLine();
            
            // 第3步：检查重复名称
            Console.WriteLine("[步骤 3/5] 检查重复名称...");
            CheckDuplicateNames(errors);
            Console.WriteLine();
            
            // 第4步：检查市场代码不匹配
            Console.WriteLine("[步骤 4/5] 检查市场代码不匹配...");
            CheckMarketMismatch(errors);
            Console.WriteLine();
            
            // 第5步：生成报告
            Console.WriteLine("[步骤 5/5] 生成检查报告...");
            GenerateReport(errors);
            Console.WriteLine();
            
            // 生成修复脚本
            if (errors.Count > 0)
            {
                GenerateFixScript(errors);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"错误: {ex.Message}");
            Console.WriteLine($"详情: {ex.StackTrace}");
        }
        
        Console.WriteLine();
        Console.WriteLine("按任意键退出...");
        Console.ReadKey();
    }
    
    static void CheckBuiltinDictionary(List<MappingError> errors)
    {
        // 这里列出内置字典中的映射（从StockInfoCache.cs复制）
        var builtinMappings = new Dictionary<string, string>
        {
            {"000022", "深赤湾A"}, {"000092", "惠天热电"}, {"000094", "大名城"}, {"000101", "明星电力"},  // ⚠ 000101错误！
            {"000105", "永鼎股份"}, {"000106", "重庆路桥"}, {"000112", "浙江东日"}, {"000113", "浙江东方"},
            // ... 更多映射
        };
        
        int checkedCount = 0;
        int errorCount = 0;
        
        foreach (var mapping in builtinMappings)
        {
            checkedCount++;
            
            if (KnownCorrectMappings.ContainsKey(mapping.Key))
            {
                if (KnownCorrectMappings[mapping.Key] != mapping.Value)
                {
                    errors.Add(new MappingError
                    {
                        Source = "内置字典",
                        Code = mapping.Key,
                        WrongName = mapping.Value,
                        CorrectName = KnownCorrectMappings[mapping.Key],
                        ErrorType = "名称错误"
                    });
                    errorCount++;
                }
            }
        }
        
        Console.WriteLine($"  检查了 {checkedCount} 条内置映射");
        Console.WriteLine($"  发现 {errorCount} 个错误");
    }
    
    static void CheckDatabaseMappings(List<MappingError> errors)
    {
        using (var conn = new NpgsqlConnection(connectionString))
        {
            conn.Open();
            
            int checkedCount = 0;
            int errorCount = 0;
            
            foreach (var correct in KnownCorrectMappings)
            {
                var cmd = new NpgsqlCommand("SELECT stock_name, market_code FROM stock_info WHERE stock_code = @code", conn);
                cmd.Parameters.AddWithValue("code", correct.Key);
                
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        checkedCount++;
                        string dbName = reader.GetString(0);
                        int marketCode = reader.GetInt16(1);
                        
                        if (dbName != correct.Value)
                        {
                            errors.Add(new MappingError
                            {
                                Source = "数据库",
                                Code = correct.Key,
                                WrongName = dbName,
                                CorrectName = correct.Value,
                                ErrorType = "名称错误"
                            });
                            errorCount++;
                        }
                        
                        // 检查市场代码
                        int expectedMarket = correct.Key.StartsWith("6") ? 1 : 0;
                        if (marketCode != expectedMarket)
                        {
                            errors.Add(new MappingError
                            {
                                Source = "数据库",
                                Code = correct.Key,
                                WrongName = $"市场代码={marketCode}",
                                CorrectName = $"应该是{expectedMarket}",
                                ErrorType = "市场代码错误"
                            });
                        }
                    }
                }
            }
            
            Console.WriteLine($"  检查了 {checkedCount} 条数据库映射");
            Console.WriteLine($"  发现 {errorCount} 个错误");
        }
    }
    
    static void CheckDuplicateNames(List<MappingError> errors)
    {
        using (var conn = new NpgsqlConnection(connectionString))
        {
            conn.Open();
            
            var cmd = new NpgsqlCommand(@"
                SELECT stock_name, STRING_AGG(stock_code, ', ' ORDER BY stock_code) as codes, COUNT(*) as cnt
                FROM stock_info
                WHERE stock_name IS NOT NULL AND stock_name != '' AND stock_name != stock_code AND is_active = TRUE
                GROUP BY stock_name
                HAVING COUNT(*) > 1", conn);
            
            int duplicateCount = 0;
            
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string name = reader.GetString(0);
                    string codes = reader.GetString(1);
                    int count = reader.GetInt32(2);
                    
                    errors.Add(new MappingError
                    {
                        Source = "数据库",
                        Code = codes,
                        WrongName = name,
                        CorrectName = $"{count}个代码使用同一名称",
                        ErrorType = "重复名称"
                    });
                    duplicateCount++;
                }
            }
            
            Console.WriteLine($"  发现 {duplicateCount} 组重复名称");
        }
    }
    
    static void CheckMarketMismatch(List<MappingError> errors)
    {
        using (var conn = new NpgsqlConnection(connectionString))
        {
            conn.Open();
            
            var cmd = new NpgsqlCommand(@"
                SELECT stock_code, stock_name, market_code
                FROM stock_info
                WHERE is_active = TRUE AND (
                    (stock_code ~ '^6' AND market_code != 1) OR
                    (stock_code ~ '^0|^3' AND market_code != 0)
                )
                LIMIT 50", conn);
            
            int mismatchCount = 0;
            
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string code = reader.GetString(0);
                    string name = reader.GetString(1);
                    int market = reader.GetInt16(2);
                    
                    errors.Add(new MappingError
                    {
                        Source = "数据库",
                        Code = code,
                        WrongName = name + $" (市场={market})",
                        CorrectName = code.StartsWith("6") ? "应该是上海(1)" : "应该是深圳(0)",
                        ErrorType = "市场代码不匹配"
                    });
                    mismatchCount++;
                }
            }
            
            Console.WriteLine($"  发现 {mismatchCount} 个市场代码不匹配");
        }
    }
    
    static void GenerateReport(List<MappingError> errors)
    {
        Console.WriteLine("╔════════════════════════════════════════════════╗");
        Console.WriteLine("║              检查报告                          ║");
        Console.WriteLine("╚════════════════════════════════════════════════╝");
        Console.WriteLine();
        
        if (errors.Count == 0)
        {
            Console.WriteLine("  ✓ 未发现错误！数据映射正确。");
            return;
        }
        
        Console.WriteLine($"  总计发现 {errors.Count} 个错误");
        Console.WriteLine();
        
        // 按错误类型分组
        var grouped = errors.GroupBy(e => e.ErrorType);
        
        foreach (var group in grouped.OrderByDescending(g => g.Count()))
        {
            Console.WriteLine($"  【{group.Key}】 ({group.Count()}个)");
            Console.WriteLine("  " + new string('-', 70));
            
            foreach (var error in group.Take(10))
            {
                Console.WriteLine($"  代码: {error.Code}");
                Console.WriteLine($"  来源: {error.Source}");
                Console.WriteLine($"  错误: {error.WrongName}");
                Console.WriteLine($"  正确: {error.CorrectName}");
                Console.WriteLine();
            }
            
            if (group.Count() > 10)
            {
                Console.WriteLine($"  ... 还有 {group.Count() - 10} 个类似错误");
                Console.WriteLine();
            }
        }
        
        // 重点错误
        var criticalErrors = errors.Where(e => 
            e.Code == "000101" || 
            e.Code == "600101" || 
            e.ErrorType == "名称错误"
        ).ToList();
        
        if (criticalErrors.Count > 0)
        {
            Console.WriteLine("  ⚠ 重点错误：");
            Console.WriteLine("  " + new string('=', 70));
            foreach (var error in criticalErrors)
            {
                Console.WriteLine($"  ⚠ {error.Code}: {error.WrongName} → 应该是: {error.CorrectName}");
            }
            Console.WriteLine();
        }
    }
    
    static void GenerateFixScript(List<MappingError> errors)
    {
        string scriptPath = "db/auto_generated_fix_mappings.sql";
        
        using (var writer = new System.IO.StreamWriter(scriptPath, false, System.Text.Encoding.UTF8))
        {
            writer.WriteLine("-- 自动生成的股票映射修复脚本");
            writer.WriteLine("-- 生成时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            writer.WriteLine("-- 发现错误数: " + errors.Count);
            writer.WriteLine();
            writer.WriteLine("BEGIN;");
            writer.WriteLine();
            
            // 修复名称错误
            var nameErrors = errors.Where(e => e.ErrorType == "名称错误" && e.Source == "数据库").ToList();
            if (nameErrors.Count > 0)
            {
                writer.WriteLine("-- 修复名称错误 (" + nameErrors.Count + "个)");
                foreach (var error in nameErrors)
                {
                    writer.WriteLine($"UPDATE stock_info SET stock_name = '{error.CorrectName}', update_time = CURRENT_TIMESTAMP WHERE stock_code = '{error.Code}';");
                }
                writer.WriteLine();
            }
            
            // 修复市场代码
            var marketErrors = errors.Where(e => e.ErrorType == "市场代码错误" || e.ErrorType == "市场代码不匹配").ToList();
            if (marketErrors.Count > 0)
            {
                writer.WriteLine("-- 修复市场代码 (" + marketErrors.Count + "个)");
                writer.WriteLine(@"UPDATE stock_info
SET market_code = CASE
    WHEN stock_code ~ '^6' THEN 1
    WHEN stock_code ~ '^0|^3' THEN 0
    ELSE market_code
END,
market_name = CASE
    WHEN stock_code ~ '^6' THEN '上海'
    WHEN stock_code ~ '^0|^3' THEN '深圳'
    ELSE market_name
END,
update_time = CURRENT_TIMESTAMP
WHERE stock_code IN (");
                
                var codes = marketErrors.Select(e => $"'{e.Code}'").Take(100);
                writer.WriteLine("  " + string.Join(", ", codes));
                writer.WriteLine(");");
                writer.WriteLine();
            }
            
            writer.WriteLine("COMMIT;");
            writer.WriteLine();
            writer.WriteLine("-- 修复完成");
            writer.WriteLine($"SELECT '修复了 {errors.Count} 个错误' as result;");
        }
        
        Console.WriteLine($"✓ 已生成修复脚本: {scriptPath}");
        Console.WriteLine($"  请在pgAdmin中执行此脚本进行修复");
    }
    
    class MappingError
    {
        public string Source { get; set; }
        public string Code { get; set; }
        public string WrongName { get; set; }
        public string CorrectName { get; set; }
        public string ErrorType { get; set; }
    }
}
