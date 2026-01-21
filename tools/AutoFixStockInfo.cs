using System;
using Npgsql;

/// <summary>
/// 自动检查并修复 stock_info 表
/// </summary>
class AutoFixStockInfo
{
    private static string connectionString = "Host=localhost;Port=8532;Username=postgres;Password=123456;Database=stockdb;";
    
    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.Clear();
        
        Console.WriteLine("╔════════════════════════════════════════╗");
        Console.WriteLine("║   股票代码表自动检查和修复工具         ║");
        Console.WriteLine("╚════════════════════════════════════════╝");
        Console.WriteLine();
        
        try
        {
            // 步骤1：检查当前状态
            Console.WriteLine("[步骤 1/3] 正在检查当前状态...");
            Console.WriteLine("----------------------------------------");
            CheckCurrentStatus();
            Console.WriteLine();
            
            // 步骤2：执行修复
            Console.WriteLine("[步骤 2/3] 正在执行修复...");
            Console.WriteLine("----------------------------------------");
            ExecuteFixes();
            Console.WriteLine();
            
            // 步骤3：验证结果
            Console.WriteLine("[步骤 3/3] 验证修复结果...");
            Console.WriteLine("----------------------------------------");
            CheckCurrentStatus();
            Console.WriteLine();
            
            Console.WriteLine("╔════════════════════════════════════════╗");
            Console.WriteLine("║            修复完成！                  ║");
            Console.WriteLine("╚════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("建议：");
            Console.WriteLine("1. 重启应用程序以重新加载缓存");
            Console.WriteLine("2. 刷新过滤查看效果");
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"错误: {ex.Message}");
            Console.WriteLine($"详情: {ex.StackTrace}");
        }
        
        Console.WriteLine("按任意键退出...");
        Console.ReadKey();
    }
    
    static void CheckCurrentStatus()
    {
        using (var conn = new NpgsqlConnection(connectionString))
        {
            conn.Open();
            
            // 基本统计
            var cmd = new NpgsqlCommand(@"
                SELECT 
                    COUNT(*) as total,
                    SUM(CASE WHEN is_active = TRUE THEN 1 ELSE 0 END) as active,
                    SUM(CASE WHEN stock_name != stock_code AND stock_name IS NOT NULL THEN 1 ELSE 0 END) as has_name
                FROM stock_info", conn);
            
            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    long total = reader.GetInt64(0);
                    long active = reader.GetInt64(1);
                    long hasName = reader.GetInt64(2);
                    
                    Console.WriteLine($"  总记录数: {total}");
                    Console.WriteLine($"  激活状态: {active} ({active * 100.0 / total:F1}%)");
                    Console.WriteLine($"  有名称数: {hasName} ({hasName * 100.0 / total:F1}%)");
                    Console.WriteLine($"  无名称数: {total - hasName} ({(total - hasName) * 100.0 / total:F1}%)");
                }
            }
            
            // 检查可疑代码
            cmd.CommandText = @"
                SELECT stock_code, stock_name, is_active
                FROM stock_info
                WHERE stock_code IN ('000001', '000300', '000139', '000046', '000914')
                ORDER BY stock_code";
            
            Console.WriteLine();
            Console.WriteLine("可疑代码:");
            using (var reader = cmd.ExecuteReader())
            {
                if (!reader.HasRows)
                {
                    Console.WriteLine("  ✓ 未发现可疑代码");
                }
                else
                {
                    while (reader.Read())
                    {
                        string code = reader.GetString(0);
                        string name = reader.GetString(1);
                        bool active = reader.GetBoolean(2);
                        string status = active ? "激活" : "已停用";
                        Console.WriteLine($"  {code} - {name} ({status})");
                    }
                }
            }
        }
    }
    
    static void ExecuteFixes()
    {
        using (var conn = new NpgsqlConnection(connectionString))
        {
            conn.Open();
            
            using (var transaction = conn.BeginTransaction())
            {
                try
                {
                    int totalFixed = 0;
                    
                    // 1. 修复市场代码
                    var cmd = new NpgsqlCommand(@"
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
                            END,
                            update_time = CURRENT_TIMESTAMP
                        WHERE (
                            (stock_code ~ '^(600|601|603|605|688)' AND market_code != 1) OR
                            (stock_code ~ '^(000|001|002|003|004|300|301)' AND market_code != 0) OR
                            (stock_code ~ '^(43|83|87|88)' AND market_code != 2)
                        )", conn, transaction);
                    
                    int fixed = cmd.ExecuteNonQuery();
                    Console.WriteLine($"  ✓ 修复市场代码: {fixed} 条");
                    totalFixed += fixed;
                    
                    // 2. 停用指数代码
                    cmd.CommandText = @"
                        UPDATE stock_info
                        SET is_active = FALSE, update_time = CURRENT_TIMESTAMP
                        WHERE stock_code IN (
                            '000001', '000002', '000003', '000004', '000005', '000006', 
                            '000008', '000009', '000010', '000011', '000012', '000013',
                            '000016', '000017', '000300', '000688', '000905', '000906',
                            '399001', '399002', '399003', '399004', '399005', '399006',
                            '399007', '399008', '399100', '399101', '399106', '399107',
                            '399108', '399333', '399606'
                        ) AND is_active = TRUE";
                    
                    fixed = cmd.ExecuteNonQuery();
                    Console.WriteLine($"  ✓ 停用指数代码: {fixed} 条");
                    totalFixed += fixed;
                    
                    // 3. 停用B股
                    cmd.CommandText = @"
                        UPDATE stock_info
                        SET is_active = FALSE, update_time = CURRENT_TIMESTAMP
                        WHERE (stock_code ~ '^200' OR stock_code ~ '^900')
                        AND is_active = TRUE";
                    
                    fixed = cmd.ExecuteNonQuery();
                    Console.WriteLine($"  ✓ 停用B股代码: {fixed} 条");
                    totalFixed += fixed;
                    
                    // 4. 停用已退市股票
                    cmd.CommandText = @"
                        UPDATE stock_info
                        SET is_active = FALSE, update_time = CURRENT_TIMESTAMP
                        WHERE stock_code IN (
                            '000018', '000033', '000046', '000139', '000669', 
                            '000760', '000816', '000914', '000981', '600656'
                        ) AND is_active = TRUE";
                    
                    fixed = cmd.ExecuteNonQuery();
                    Console.WriteLine($"  ✓ 停用已退市股票: {fixed} 条");
                    totalFixed += fixed;
                    
                    // 5. 修复空名称
                    cmd.CommandText = @"
                        UPDATE stock_info
                        SET stock_name = stock_code, update_time = CURRENT_TIMESTAMP
                        WHERE (stock_name IS NULL OR stock_name = '')
                        AND is_active = TRUE";
                    
                    fixed = cmd.ExecuteNonQuery();
                    Console.WriteLine($"  ✓ 修复空名称: {fixed} 条");
                    totalFixed += fixed;
                    
                    transaction.Commit();
                    Console.WriteLine();
                    Console.WriteLine($"总计修复: {totalFixed} 条记录");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    Console.WriteLine($"修复失败，已回滚: {ex.Message}");
                    throw;
                }
            }
        }
    }
}
