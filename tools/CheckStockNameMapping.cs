using System;
using System.Collections.Generic;
using Npgsql;

/// <summary>
/// 检查股票代码-名称映射工具
/// 帮助发现和修复错误的映射关系
/// </summary>
class CheckStockNameMapping
{
    private static string connectionString = "Host=localhost;Port=8532;Username=postgres;Password=123456;Database=stockdb;";
    
    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.Clear();
        
        Console.WriteLine("╔════════════════════════════════════════╗");
        Console.WriteLine("║   股票代码-名称映射检查工具            ║");
        Console.WriteLine("╚════════════════════════════════════════╝");
        Console.WriteLine();
        
        try
        {
            // 1. 检查"明星电力"相关的映射
            Console.WriteLine("[1] 检查「明星电力」相关映射");
            Console.WriteLine("----------------------------------------");
            CheckMingxingDianli();
            Console.WriteLine();
            
            // 2. 检查重复或冲突的名称
            Console.WriteLine("[2] 检查重复或冲突的名称");
            Console.WriteLine("----------------------------------------");
            CheckDuplicateNames();
            Console.WriteLine();
            
            // 3. 抽查一些常见股票的映射
            Console.WriteLine("[3] 抽查常见股票映射");
            Console.WriteLine("----------------------------------------");
            CheckCommonStocks();
            Console.WriteLine();
            
            // 4. 提供交互式查询
            Console.WriteLine("╔════════════════════════════════════════╗");
            Console.WriteLine("║          交互式查询                    ║");
            Console.WriteLine("╚════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("输入股票代码或名称（部分）查询（直接回车退出）：");
            
            while (true)
            {
                Console.Write("> ");
                string input = Console.ReadLine();
                
                if (string.IsNullOrWhiteSpace(input))
                    break;
                
                SearchStock(input.Trim());
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"错误: {ex.Message}");
            Console.WriteLine($"详情: {ex.StackTrace}");
        }
        
        Console.WriteLine("按任意键退出...");
        Console.ReadKey();
    }
    
    static void CheckMingxingDianli()
    {
        using (var conn = new NpgsqlConnection(connectionString))
        {
            conn.Open();
            
            // 查找所有包含"明星"的股票
            var cmd = new NpgsqlCommand(@"
                SELECT stock_code, stock_name, is_active, market_code
                FROM stock_info
                WHERE stock_name LIKE '%明星%'
                ORDER BY stock_code", conn);
            
            Console.WriteLine("stock_info表中包含「明星」的股票：");
            using (var reader = cmd.ExecuteReader())
            {
                if (!reader.HasRows)
                {
                    Console.WriteLine("  未找到");
                }
                else
                {
                    while (reader.Read())
                    {
                        string code = reader.GetString(0);
                        string name = reader.GetString(1);
                        bool active = reader.GetBoolean(2);
                        int market = reader.GetInt16(3);
                        
                        string status = active ? "激活" : "停用";
                        string marketName = market == 0 ? "深圳" : market == 1 ? "上海" : "北京";
                        
                        Console.WriteLine($"  {code} - {name} ({marketName}, {status})");
                    }
                }
            }
            
            // 检查000101的最近交易数据
            cmd.CommandText = @"
                SELECT trade_date, close
                FROM stock_daily_data
                WHERE stock_code = '000101'
                ORDER BY trade_date DESC
                LIMIT 5";
            
            Console.WriteLine();
            Console.WriteLine("000101的最近交易数据（验证实际股票）：");
            using (var reader = cmd.ExecuteReader())
            {
                if (!reader.HasRows)
                {
                    Console.WriteLine("  无交易数据");
                }
                else
                {
                    while (reader.Read())
                    {
                        DateTime date = reader.GetDateTime(0);
                        decimal close = reader.GetDecimal(1);
                        Console.WriteLine($"  {date:yyyy-MM-dd}  收盘价: {close:F2}");
                    }
                }
            }
        }
    }
    
    static void CheckDuplicateNames()
    {
        using (var conn = new NpgsqlConnection(connectionString))
        {
            conn.Open();
            
            var cmd = new NpgsqlCommand(@"
                SELECT stock_name, COUNT(DISTINCT stock_code) as code_count,
                       STRING_AGG(stock_code, ', ' ORDER BY stock_code) as codes
                FROM stock_info
                WHERE stock_name IS NOT NULL 
                  AND stock_name != '' 
                  AND stock_name != stock_code
                  AND is_active = TRUE
                GROUP BY stock_name
                HAVING COUNT(DISTINCT stock_code) > 1
                ORDER BY code_count DESC, stock_name
                LIMIT 10", conn);
            
            using (var reader = cmd.ExecuteReader())
            {
                if (!reader.HasRows)
                {
                    Console.WriteLine("  ✓ 未发现重复名称");
                }
                else
                {
                    Console.WriteLine("  股票名称           代码数    代码列表");
                    Console.WriteLine("  " + new string('-', 60));
                    
                    while (reader.Read())
                    {
                        string name = reader.GetString(0);
                        int count = reader.GetInt32(1);
                        string codes = reader.GetString(2);
                        
                        Console.WriteLine($"  {name,-18} {count,-9} {codes}");
                    }
                }
            }
        }
    }
    
    static void CheckCommonStocks()
    {
        var testCodes = new[] { "000001", "000002", "600036", "600519", "000651", "300750", "000101" };
        
        using (var conn = new NpgsqlConnection(connectionString))
        {
            conn.Open();
            
            Console.WriteLine("  代码       名称              市场    状态");
            Console.WriteLine("  " + new string('-', 50));
            
            foreach (var code in testCodes)
            {
                var cmd = new NpgsqlCommand(@"
                    SELECT stock_name, market_code, is_active
                    FROM stock_info
                    WHERE stock_code = @code", conn);
                cmd.Parameters.AddWithValue("code", code);
                
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        string name = reader.GetString(0);
                        int market = reader.GetInt16(1);
                        bool active = reader.GetBoolean(2);
                        
                        string marketName = market == 0 ? "深圳" : market == 1 ? "上海" : "北京";
                        string status = active ? "激活" : "停用";
                        
                        // 标记可能有问题的
                        string warning = "";
                        if (code == "000001" && name != "平安银行") warning = " ⚠";
                        if (code == "000002" && name != "万科A") warning = " ⚠";
                        if (code == "600036" && name != "招商银行") warning = " ⚠";
                        if (code == "000651" && name != "格力电器") warning = " ⚠";
                        
                        Console.WriteLine($"  {code}     {name,-16} {marketName,-6} {status}{warning}");
                    }
                    else
                    {
                        Console.WriteLine($"  {code}     (未找到)");
                    }
                }
            }
        }
    }
    
    static void SearchStock(string input)
    {
        using (var conn = new NpgsqlConnection(connectionString))
        {
            conn.Open();
            
            // 尝试作为代码搜索
            var cmd = new NpgsqlCommand(@"
                SELECT stock_code, stock_name, market_code, is_active
                FROM stock_info
                WHERE stock_code LIKE @input OR stock_name LIKE @pattern
                ORDER BY stock_code
                LIMIT 20", conn);
            
            cmd.Parameters.AddWithValue("input", input + "%");
            cmd.Parameters.AddWithValue("pattern", "%" + input + "%");
            
            using (var reader = cmd.ExecuteReader())
            {
                if (!reader.HasRows)
                {
                    Console.WriteLine($"  未找到匹配「{input}」的股票");
                }
                else
                {
                    Console.WriteLine($"  搜索「{input}」的结果：");
                    Console.WriteLine($"  {"代码",-10} {"名称",-15} {"市场",-8} {"状态"}");
                    Console.WriteLine("  " + new string('-', 45));
                    
                    while (reader.Read())
                    {
                        string code = reader.GetString(0);
                        string name = reader.GetString(1);
                        int market = reader.GetInt16(2);
                        bool active = reader.GetBoolean(3);
                        
                        string marketName = market == 0 ? "深圳" : market == 1 ? "上海" : "北京";
                        string status = active ? "激活" : "停用";
                        
                        Console.WriteLine($"  {code,-10} {name,-15} {marketName,-8} {status}");
                    }
                }
            }
        }
    }
}
