using System;
using System.Collections.Generic;
using System.Linq;
using Npgsql;

/// <summary>
/// 股票信息表诊断工具
/// 用于检查 stock_info 表的数据正确性
/// </summary>
class StockInfoDiagnostics
{
    private static string connectionString = "Host=localhost;Port=8532;Username=postgres;Password=123456;Database=stockdb;";
    
    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("========================================");
        Console.WriteLine("  股票信息表（stock_info）诊断工具");
        Console.WriteLine("========================================");
        Console.WriteLine();
        
        try
        {
            // 1. 基本统计
            Console.WriteLine("【1. 基本统计】");
            PrintBasicStats();
            Console.WriteLine();
            
            // 2. 市场分类统计
            Console.WriteLine("【2. 市场分类统计】");
            PrintMarketStats();
            Console.WriteLine();
            
            // 3. 检查可疑代码
            Console.WriteLine("【3. 可疑代码检查】");
            CheckSuspiciousCodes();
            Console.WriteLine();
            
            // 4. 检查市场代码错误
            Console.WriteLine("【4. 市场代码错误检查】");
            CheckMarketCodeErrors();
            Console.WriteLine();
            
            // 5. 检查名称异常
            Console.WriteLine("【5. 名称异常检查】");
            CheckNameIssues();
            Console.WriteLine();
            
            // 6. 检查重复代码
            Console.WriteLine("【6. 重复代码检查】");
            CheckDuplicateCodes();
            Console.WriteLine();
            
            // 7. 抽样检查
            Console.WriteLine("【7. 抽样检查（随机10只）】");
            PrintRandomSample();
            Console.WriteLine();
            
            // 8. 总结和建议
            Console.WriteLine("【8. 诊断总结和建议】");
            PrintSummary();
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"错误: {ex.Message}");
            Console.WriteLine($"详情: {ex.StackTrace}");
        }
        
        Console.WriteLine("========================================");
        Console.WriteLine("诊断完成！按任意键退出...");
        Console.ReadKey();
    }
    
    static void PrintBasicStats()
    {
        using (var conn = new NpgsqlConnection(connectionString))
        {
            conn.Open();
            
            // 总记录数
            var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM stock_info", conn);
            long total = (long)cmd.ExecuteScalar();
            Console.WriteLine($"  总记录数: {total}");
            
            // 激活状态
            cmd.CommandText = "SELECT COUNT(*) FROM stock_info WHERE is_active = TRUE";
            long active = (long)cmd.ExecuteScalar();
            Console.WriteLine($"  激活状态: {active} ({active * 100.0 / total:F1}%)");
            
            // 停用状态
            cmd.CommandText = "SELECT COUNT(*) FROM stock_info WHERE is_active = FALSE";
            long inactive = (long)cmd.ExecuteScalar();
            Console.WriteLine($"  停用状态: {inactive} ({inactive * 100.0 / total:F1}%)");
            
            // 有名称
            cmd.CommandText = "SELECT COUNT(*) FROM stock_info WHERE stock_name IS NOT NULL AND stock_name != '' AND stock_name != stock_code";
            long hasName = (long)cmd.ExecuteScalar();
            Console.WriteLine($"  有名称的股票: {hasName} ({hasName * 100.0 / total:F1}%)");
            
            // 无名称
            cmd.CommandText = "SELECT COUNT(*) FROM stock_info WHERE stock_name IS NULL OR stock_name = '' OR stock_name = stock_code";
            long noName = (long)cmd.ExecuteScalar();
            Console.WriteLine($"  无名称的股票: {noName} ({noName * 100.0 / total:F1}%)");
        }
    }
    
    static void PrintMarketStats()
    {
        using (var conn = new NpgsqlConnection(connectionString))
        {
            conn.Open();
            
            var cmd = new NpgsqlCommand(@"
                SELECT 
                    CASE 
                        WHEN stock_code ~ '^(600|601|603|605|688|900)' THEN '上海市场'
                        WHEN stock_code ~ '^(000|001|002|003|004|300|301|200)' THEN '深圳市场'
                        WHEN stock_code ~ '^(43|83|87|88)' THEN '北京市场'
                        ELSE '未知市场'
                    END as market,
                    COUNT(*) as count,
                    SUM(CASE WHEN stock_name != stock_code THEN 1 ELSE 0 END) as has_name_count
                FROM stock_info
                WHERE is_active = TRUE
                GROUP BY market
                ORDER BY count DESC", conn);
            
            using (var reader = cmd.ExecuteReader())
            {
                Console.WriteLine($"  {"市场",-12} {"股票数",-10} {"有名称",-10} {"覆盖率"}");
                Console.WriteLine($"  {new string('-', 50)}");
                
                while (reader.Read())
                {
                    string market = reader.GetString(0);
                    long count = reader.GetInt64(1);
                    long hasName = reader.GetInt64(2);
                    double coverage = hasName * 100.0 / count;
                    Console.WriteLine($"  {market,-12} {count,-10} {hasName,-10} {coverage:F1}%");
                }
            }
        }
    }
    
    static void CheckSuspiciousCodes()
    {
        using (var conn = new NpgsqlConnection(connectionString))
        {
            conn.Open();
            
            // 检查可能是指数的代码
            var cmd = new NpgsqlCommand(@"
                SELECT stock_code, stock_name
                FROM stock_info
                WHERE stock_code IN ('000001', '000300', '000905', '000016', '399001', '399006')
                  AND is_active = TRUE
                ORDER BY stock_code", conn);
            
            Console.WriteLine("  可能是指数的代码:");
            using (var reader = cmd.ExecuteReader())
            {
                if (!reader.HasRows)
                {
                    Console.WriteLine("    ✓ 未发现指数代码");
                }
                else
                {
                    while (reader.Read())
                    {
                        Console.WriteLine($"    ⚠ {reader.GetString(0)} - {reader.GetString(1)} (应该过滤)");
                    }
                }
            }
            
            // 检查B股
            cmd.CommandText = @"
                SELECT stock_code, stock_name
                FROM stock_info
                WHERE (stock_code ~ '^200' OR stock_code ~ '^900')
                  AND is_active = TRUE
                ORDER BY stock_code
                LIMIT 5";
            
            Console.WriteLine("  B股代码（应过滤）:");
            using (var reader = cmd.ExecuteReader())
            {
                if (!reader.HasRows)
                {
                    Console.WriteLine("    ✓ 未发现B股代码");
                }
                else
                {
                    int count = 0;
                    while (reader.Read())
                    {
                        Console.WriteLine($"    ⚠ {reader.GetString(0)} - {reader.GetString(1)}");
                        count++;
                    }
                    Console.WriteLine($"    (显示前5条，可能还有更多)");
                }
            }
        }
    }
    
    static void CheckMarketCodeErrors()
    {
        using (var conn = new NpgsqlConnection(connectionString))
        {
            conn.Open();
            
            var cmd = new NpgsqlCommand(@"
                SELECT stock_code, market_code,
                    CASE 
                        WHEN stock_code ~ '^(600|601|603|605|688|900)' THEN 1
                        WHEN stock_code ~ '^(000|001|002|003|004|300|301|200)' THEN 0
                        WHEN stock_code ~ '^(43|83|87|88)' THEN 2
                        ELSE -1
                    END as should_be
                FROM stock_info
                WHERE is_active = TRUE
                  AND (
                    (stock_code ~ '^(600|601|603|605|688)' AND market_code != 1) OR
                    (stock_code ~ '^(000|001|002|003|004|300|301)' AND market_code != 0) OR
                    (stock_code ~ '^(43|83|87|88)' AND market_code != 2)
                  )
                LIMIT 10", conn);
            
            using (var reader = cmd.ExecuteReader())
            {
                if (!reader.HasRows)
                {
                    Console.WriteLine("  ✓ 未发现市场代码错误");
                }
                else
                {
                    Console.WriteLine($"  {"代码",-10} {"当前市场",-10} {"应该是"}");
                    Console.WriteLine($"  {new string('-', 35)}");
                    
                    int count = 0;
                    while (reader.Read())
                    {
                        string code = reader.GetString(0);
                        int currentMarket = reader.GetInt16(1);
                        int shouldBe = reader.GetInt32(2);
                        
                        string currentStr = currentMarket == 0 ? "深圳(0)" : currentMarket == 1 ? "上海(1)" : "北京(2)";
                        string shouldStr = shouldBe == 0 ? "深圳(0)" : shouldBe == 1 ? "上海(1)" : "北京(2)";
                        
                        Console.WriteLine($"  {code,-10} {currentStr,-10} {shouldStr}");
                        count++;
                    }
                    Console.WriteLine($"  (显示前10条，可能还有更多)");
                }
            }
        }
    }
    
    static void CheckNameIssues()
    {
        using (var conn = new NpgsqlConnection(connectionString))
        {
            conn.Open();
            
            // 名称缺失
            var cmd = new NpgsqlCommand(@"
                SELECT stock_code, COALESCE(stock_name, '(NULL)')
                FROM stock_info
                WHERE is_active = TRUE
                  AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code)
                ORDER BY stock_code
                LIMIT 10", conn);
            
            Console.WriteLine("  名称缺失的股票:");
            using (var reader = cmd.ExecuteReader())
            {
                if (!reader.HasRows)
                {
                    Console.WriteLine("    ✓ 所有股票都有名称");
                }
                else
                {
                    int count = 0;
                    while (reader.Read())
                    {
                        Console.WriteLine($"    ⚠ {reader.GetString(0)} - {reader.GetString(1)}");
                        count++;
                    }
                    
                    // 统计总数
                    var countCmd = new NpgsqlCommand(@"
                        SELECT COUNT(*)
                        FROM stock_info
                        WHERE is_active = TRUE
                          AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code)", conn);
                    long totalMissing = (long)countCmd.ExecuteScalar();
                    Console.WriteLine($"    (显示前10条，共 {totalMissing} 只股票缺少名称)");
                }
            }
            
            // 名称过长
            cmd.CommandText = @"
                SELECT stock_code, stock_name, LENGTH(stock_name)
                FROM stock_info
                WHERE is_active = TRUE
                  AND stock_name IS NOT NULL
                  AND LENGTH(stock_name) > 8
                ORDER BY LENGTH(stock_name) DESC
                LIMIT 5";
            
            Console.WriteLine("  名称过长的股票（可能是公司全称）:");
            using (var reader = cmd.ExecuteReader())
            {
                if (!reader.HasRows)
                {
                    Console.WriteLine("    ✓ 未发现过长名称");
                }
                else
                {
                    while (reader.Read())
                    {
                        Console.WriteLine($"    ⚠ {reader.GetString(0)} - {reader.GetString(1)} ({reader.GetInt32(2)}字符)");
                    }
                }
            }
        }
    }
    
    static void CheckDuplicateCodes()
    {
        using (var conn = new NpgsqlConnection(connectionString))
        {
            conn.Open();
            
            var cmd = new NpgsqlCommand(@"
                SELECT stock_code, COUNT(*)
                FROM stock_info
                GROUP BY stock_code
                HAVING COUNT(*) > 1", conn);
            
            using (var reader = cmd.ExecuteReader())
            {
                if (!reader.HasRows)
                {
                    Console.WriteLine("  ✓ 未发现重复代码");
                }
                else
                {
                    Console.WriteLine($"  {"代码",-10} {"出现次数"}");
                    Console.WriteLine($"  {new string('-', 25)}");
                    
                    while (reader.Read())
                    {
                        Console.WriteLine($"  {reader.GetString(0),-10} {reader.GetInt64(1)}");
                    }
                }
            }
        }
    }
    
    static void PrintRandomSample()
    {
        using (var conn = new NpgsqlConnection(connectionString))
        {
            conn.Open();
            
            var cmd = new NpgsqlCommand(@"
                SELECT stock_code, stock_name, market_code, is_active
                FROM stock_info
                WHERE is_active = TRUE
                ORDER BY RANDOM()
                LIMIT 10", conn);
            
            Console.WriteLine($"  {"代码",-10} {"名称",-15} {"市场",-10} {"状态"}");
            Console.WriteLine($"  {new string('-', 50)}");
            
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string code = reader.GetString(0);
                    string name = reader.GetString(1);
                    int market = reader.GetInt16(2);
                    bool active = reader.GetBoolean(3);
                    
                    string marketStr = market == 0 ? "深圳" : market == 1 ? "上海" : "北京";
                    string activeStr = active ? "激活" : "停用";
                    
                    Console.WriteLine($"  {code,-10} {name,-15} {marketStr,-10} {activeStr}");
                }
            }
        }
    }
    
    static void PrintSummary()
    {
        using (var conn = new NpgsqlConnection(connectionString))
        {
            conn.Open();
            
            // 数据完整性
            var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM stock_info WHERE is_active = TRUE", conn);
            long activeCount = (long)cmd.ExecuteScalar();
            
            Console.WriteLine("  1. 数据完整性:");
            if (activeCount > 3000)
            {
                Console.WriteLine($"     ✓ 记录数量正常 ({activeCount} > 3000)");
            }
            else
            {
                Console.WriteLine($"     ⚠ 记录数量偏少 ({activeCount} < 3000)");
                Console.WriteLine($"     建议: 执行 SyncFromDailyData 从日线数据同步");
            }
            
            // 名称完整性
            cmd.CommandText = @"
                SELECT 
                    COUNT(*) as total,
                    SUM(CASE WHEN stock_name != stock_code THEN 1 ELSE 0 END) as has_name
                FROM stock_info
                WHERE is_active = TRUE";
            
            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    long total = reader.GetInt64(0);
                    long hasName = reader.GetInt64(1);
                    double coverage = hasName * 100.0 / total;
                    
                    Console.WriteLine("  2. 名称完整性:");
                    if (coverage > 90)
                    {
                        Console.WriteLine($"     ✓ 名称覆盖率良好 ({coverage:F1}% > 90%)");
                    }
                    else
                    {
                        Console.WriteLine($"     ⚠ 名称覆盖率不足 ({coverage:F1}% < 90%)");
                        Console.WriteLine($"     建议: 补充缺失的股票名称到 stock_info 表");
                    }
                }
            }
            
            // 市场代码准确性
            cmd.CommandText = @"
                SELECT COUNT(*)
                FROM stock_info
                WHERE is_active = TRUE
                  AND (
                    (stock_code ~ '^(600|601|603|605|688)' AND market_code != 1) OR
                    (stock_code ~ '^(000|001|002|003|004|300|301)' AND market_code != 0) OR
                    (stock_code ~ '^(43|83|87|88)' AND market_code != 2)
                  )";
            
            long marketErrors = (long)cmd.ExecuteScalar();
            
            Console.WriteLine("  3. 市场代码准确性:");
            if (marketErrors == 0)
            {
                Console.WriteLine("     ✓ 市场代码准确");
            }
            else
            {
                Console.WriteLine($"     ⚠ 发现 {marketErrors} 条市场代码错误");
                Console.WriteLine("     建议: 执行修复SQL更新market_code字段");
            }
            
            // 可疑代码
            cmd.CommandText = @"
                SELECT COUNT(*)
                FROM stock_info
                WHERE is_active = TRUE
                  AND (
                    stock_code IN ('000001', '000300', '000905', '000016', '399001', '399006') OR
                    stock_code ~ '^200' OR
                    stock_code ~ '^900'
                  )";
            
            long suspiciousCount = (long)cmd.ExecuteScalar();
            
            Console.WriteLine("  4. 可疑代码:");
            if (suspiciousCount == 0)
            {
                Console.WriteLine("     ✓ 未发现指数/B股等非A股代码");
            }
            else
            {
                Console.WriteLine($"     ⚠ 发现 {suspiciousCount} 条可疑代码（指数/B股等）");
                Console.WriteLine("     建议: 将这些代码标记为 is_active = FALSE");
            }
            
            Console.WriteLine();
            Console.WriteLine("  总体评估:");
            if (activeCount > 3000 && marketErrors == 0 && suspiciousCount == 0)
            {
                Console.WriteLine("     ✓ stock_info 表数据质量良好！");
            }
            else
            {
                Console.WriteLine("     ⚠ 发现一些问题，建议按上述建议进行修复");
            }
        }
    }
}
