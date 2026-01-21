using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Npgsql;

/// <summary>
/// 查找所有非A股代码的工具
/// 包括：指数、债券、基金、B股、退市股等
/// </summary>
class FindAllNonAStockCodes
{
    private static string connectionString = "Host=localhost;Port=8532;Username=postgres;Password=123456;Database=stockdb;";
    
    // 关键词匹配规则
    private static readonly string[] IndexKeywords = { "指数", "指标" };
    private static readonly string[] BondKeywords = { "债", "债券" };
    private static readonly string[] FundKeywords = { "基金", "ETF", "LOF" };
    
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Clear();
        
        Console.WriteLine("╔════════════════════════════════════════════════╗");
        Console.WriteLine("║        查找所有非A股代码工具                   ║");
        Console.WriteLine("╚════════════════════════════════════════════════╝");
        Console.WriteLine();
        
        var suspiciousCodes = new List<SuspiciousCode>();
        
        try
        {
            // 第1步：从数据库加载所有活跃代码
            Console.WriteLine("[步骤 1/4] 从数据库加载活跃代码...");
            var allCodes = LoadActiveCodesFromDatabase();
            Console.WriteLine($"  加载了 {allCodes.Count} 个活跃代码");
            Console.WriteLine();
            
            // 第2步：通过模式匹配找出可疑代码
            Console.WriteLine("[步骤 2/4] 模式匹配分析...");
            AnalyzeByPattern(allCodes, suspiciousCodes);
            Console.WriteLine($"  发现 {suspiciousCodes.Count} 个可疑代码");
            Console.WriteLine();
            
            // 第3步：通过名称关键词过滤
            Console.WriteLine("[步骤 3/4] 关键词匹配分析...");
            AnalyzeByKeywords(allCodes, suspiciousCodes);
            Console.WriteLine($"  当前共 {suspiciousCodes.Count} 个可疑代码");
            Console.WriteLine();
            
            // 第4步：生成报告
            Console.WriteLine("[步骤 4/4] 生成报告...");
            GenerateReport(suspiciousCodes);
            
            // 生成黑名单代码
            GenerateBlacklist(suspiciousCodes);
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"错误: {ex.Message}");
        }
        
        Console.WriteLine();
        Console.WriteLine("按任意键退出...");
        Console.ReadKey();
    }
    
    static List<StockCode> LoadActiveCodesFromDatabase()
    {
        var codes = new List<StockCode>();
        
        using (var conn = new NpgsqlConnection(connectionString))
        {
            conn.Open();
            var cmd = new NpgsqlCommand(@"
                SELECT stock_code, stock_name, market_code, market_name
                FROM stock_info
                WHERE is_active = TRUE
                ORDER BY stock_code", conn);
            
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    codes.Add(new StockCode
                    {
                        Code = reader.GetString(0),
                        Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        MarketCode = reader.IsDBNull(2) ? -1 : reader.GetInt16(2),
                        MarketName = reader.IsDBNull(3) ? "" : reader.GetString(3)
                    });
                }
            }
        }
        
        return codes;
    }
    
    static void AnalyzeByPattern(List<StockCode> allCodes, List<SuspiciousCode> suspiciousCodes)
    {
        foreach (var code in allCodes)
        {
            var reasons = new List<string>();
            
            // 规则1：000000-000099 极高可能是指数
            if (code.Code.StartsWith("0000"))
            {
                int num = int.Parse(code.Code);
                if (num < 100)
                {
                    reasons.Add("代码范围000000-000099（通常是指数）");
                }
            }
            
            // 规则2：000100-000199 高可能性是特殊代码
            if (code.Code.StartsWith("0001"))
            {
                int num = int.Parse(code.Code);
                if (num >= 100 && num < 200)
                {
                    reasons.Add("代码范围000100-000199（可能是指数/债券）");
                }
            }
            
            // 规则3：B股代码
            if (code.Code.StartsWith("200") || code.Code.StartsWith("900"))
            {
                reasons.Add("B股代码");
            }
            
            if (reasons.Count > 0)
            {
                suspiciousCodes.Add(new SuspiciousCode
                {
                    Code = code.Code,
                    Name = code.Name,
                    Reasons = reasons,
                    RiskLevel = "高"
                });
            }
        }
    }
    
    static void AnalyzeByKeywords(List<StockCode> allCodes, List<SuspiciousCode> suspiciousCodes)
    {
        var existingCodes = new HashSet<string>(suspiciousCodes.Select(s => s.Code));
        
        foreach (var code in allCodes)
        {
            if (existingCodes.Contains(code.Code))
                continue;
            
            var reasons = new List<string>();
            
            // 检查名称关键词
            foreach (var keyword in IndexKeywords)
            {
                if (code.Name.Contains(keyword))
                {
                    reasons.Add($"名称包含'{keyword}'");
                    break;
                }
            }
            
            foreach (var keyword in BondKeywords)
            {
                if (code.Name.Contains(keyword))
                {
                    reasons.Add($"名称包含'{keyword}'");
                    break;
                }
            }
            
            foreach (var keyword in FundKeywords)
            {
                if (code.Name.Contains(keyword))
                {
                    reasons.Add($"名称包含'{keyword}'");
                    break;
                }
            }
            
            if (reasons.Count > 0)
            {
                suspiciousCodes.Add(new SuspiciousCode
                {
                    Code = code.Code,
                    Name = code.Name,
                    Reasons = reasons,
                    RiskLevel = "中"
                });
            }
        }
    }
    
    static void GenerateReport(List<SuspiciousCode> suspiciousCodes)
    {
        Console.WriteLine("╔════════════════════════════════════════════════╗");
        Console.WriteLine("║              发现的可疑代码                    ║");
        Console.WriteLine("╚════════════════════════════════════════════════╝");
        Console.WriteLine();
        
        if (suspiciousCodes.Count == 0)
        {
            Console.WriteLine("  ✓ 未发现可疑代码！");
            return;
        }
        
        // 按风险级别分组
        var highRisk = suspiciousCodes.Where(s => s.RiskLevel == "高").OrderBy(s => s.Code).ToList();
        var mediumRisk = suspiciousCodes.Where(s => s.RiskLevel == "中").OrderBy(s => s.Code).ToList();
        
        Console.WriteLine($"  总计发现 {suspiciousCodes.Count} 个可疑代码");
        Console.WriteLine($"  高风险: {highRisk.Count} 个");
        Console.WriteLine($"  中风险: {mediumRisk.Count} 个");
        Console.WriteLine();
        
        // 显示高风险代码
        if (highRisk.Count > 0)
        {
            Console.WriteLine("  【高风险代码】");
            Console.WriteLine("  " + new string('-', 70));
            foreach (var sus in highRisk.Take(20))
            {
                Console.WriteLine($"  {sus.Code} | {sus.Name}");
                foreach (var reason in sus.Reasons)
                {
                    Console.WriteLine($"    - {reason}");
                }
            }
            if (highRisk.Count > 20)
            {
                Console.WriteLine($"  ... 还有 {highRisk.Count - 20} 个高风险代码");
            }
            Console.WriteLine();
        }
        
        // 显示中风险代码（只显示前10个）
        if (mediumRisk.Count > 0)
        {
            Console.WriteLine("  【中风险代码（前10个）】");
            Console.WriteLine("  " + new string('-', 70));
            foreach (var sus in mediumRisk.Take(10))
            {
                Console.WriteLine($"  {sus.Code} | {sus.Name}");
                foreach (var reason in sus.Reasons)
                {
                    Console.WriteLine($"    - {reason}");
                }
            }
            if (mediumRisk.Count > 10)
            {
                Console.WriteLine($"  ... 还有 {mediumRisk.Count - 10} 个中风险代码");
            }
            Console.WriteLine();
        }
    }
    
    static void GenerateBlacklist(List<SuspiciousCode> suspiciousCodes)
    {
        string filePath = "非A股代码黑名单_自动生成.txt";
        
        using (var writer = new System.IO.StreamWriter(filePath, false, Encoding.UTF8))
        {
            writer.WriteLine("// 自动生成的非A股代码黑名单");
            writer.WriteLine("// 生成时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            writer.WriteLine("// 总计: " + suspiciousCodes.Count + " 个可疑代码");
            writer.WriteLine();
            
            writer.WriteLine("// ===== 高风险代码（极可能是指数/债券） =====");
            foreach (var sus in suspiciousCodes.Where(s => s.RiskLevel == "高").OrderBy(s => s.Code))
            {
                writer.WriteLine($"\"{sus.Code}\", // {sus.Name} - {string.Join(", ", sus.Reasons)}");
            }
            
            writer.WriteLine();
            writer.WriteLine("// ===== 中风险代码（名称关键词匹配） =====");
            foreach (var sus in suspiciousCodes.Where(s => s.RiskLevel == "中").OrderBy(s => s.Code))
            {
                writer.WriteLine($"\"{sus.Code}\", // {sus.Name} - {string.Join(", ", sus.Reasons)}");
            }
        }
        
        Console.WriteLine($"✓ 已生成黑名单文件: {filePath}");
    }
    
    class StockCode
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public int MarketCode { get; set; }
        public string MarketName { get; set; }
    }
    
    class SuspiciousCode
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public List<string> Reasons { get; set; }
        public string RiskLevel { get; set; }  // "高" 或 "中"
    }
}
