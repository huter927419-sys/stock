using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Npgsql;

namespace MQReceiver.Tools
{
    /// <summary>
    /// 股票名称验证工具
    /// 从在线API获取准确的股票代码和名称,并更新数据库
    /// </summary>
    public class StockNameValidator
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly string _connectionString;

        public StockNameValidator(string connectionString)
        {
            _connectionString = connectionString;
            _httpClient.Timeout = TimeSpan.FromSeconds(5);
        }

        /// <summary>
        /// 从新浪财经API获取股票名称
        /// </summary>
        public async Task<(bool Success, string Name)> GetStockNameFromSina(string stockCode)
        {
            try
            {
                // 判断市场: 6开头=上海, 0/3开头=深圳
                string market = stockCode.StartsWith("6") ? "sh" : "sz";
                string url = $"http://hq.sinajs.cn/list={market}{stockCode}";

                var response = await _httpClient.GetStringAsync(url);
                
                // 返回格式: var hq_str_sh600000="浦发银行,11.05,11.02,..."
                if (string.IsNullOrEmpty(response) || response.Contains("\"\""))
                {
                    return (false, null);
                }

                // 提取股票名称 (第一个逗号前的内容)
                int start = response.IndexOf("\"") + 1;
                int end = response.IndexOf(",", start);
                if (start > 0 && end > start)
                {
                    string name = response.Substring(start, end - start);
                    return (true, name);
                }

                return (false, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[验证] 获取 {stockCode} 名称失败: {ex.Message}");
                return (false, null);
            }
        }

        /// <summary>
        /// 从腾讯财经API获取股票名称
        /// </summary>
        public async Task<(bool Success, string Name)> GetStockNameFromTencent(string stockCode)
        {
            try
            {
                // 判断市场: 6开头=sh, 0/3开头=sz
                string market = stockCode.StartsWith("6") ? "sh" : "sz";
                string url = $"http://qt.gtimg.cn/q={market}{stockCode}";

                var response = await _httpClient.GetStringAsync(url);
                
                // 返回格式: v_sz000001="51~平安银行~000001~12.23~..."
                if (string.IsNullOrEmpty(response) || !response.Contains("~"))
                {
                    return (false, null);
                }

                var parts = response.Split('~');
                if (parts.Length > 1)
                {
                    return (true, parts[1]);
                }

                return (false, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[验证] 获取 {stockCode} 名称失败: {ex.Message}");
                return (false, null);
            }
        }

        /// <summary>
        /// 验证单个股票代码并更新数据库
        /// </summary>
        public async Task<(bool IsValid, string ActualName)> ValidateAndUpdate(string stockCode)
        {
            // 先尝试新浪
            var (success, name) = await GetStockNameFromSina(stockCode);
            
            // 如果失败,尝试腾讯
            if (!success)
            {
                (success, name) = await GetStockNameFromTencent(stockCode);
            }

            if (success && !string.IsNullOrEmpty(name))
            {
                // 更新数据库
                UpdateStockName(stockCode, name);
                return (true, name);
            }

            return (false, null);
        }

        /// <summary>
        /// 批量验证数据库中的所有股票
        /// </summary>
        public async Task ValidateAllStocks()
        {
            var stockCodes = GetAllStockCodes();
            Console.WriteLine($"\n[验证] 开始验证 {stockCodes.Count} 只股票...\n");

            int validCount = 0;
            int invalidCount = 0;
            int updatedCount = 0;

            foreach (var (code, dbName) in stockCodes)
            {
                var (isValid, actualName) = await ValidateAndUpdate(code);
                
                if (isValid)
                {
                    validCount++;
                    if (dbName != actualName)
                    {
                        updatedCount++;
                        Console.WriteLine($"[更新] {code}: \"{dbName}\" -> \"{actualName}\"");
                    }
                }
                else
                {
                    invalidCount++;
                    Console.WriteLine($"[无效] {code}: {dbName} (无法验证,可能已退市或非A股)");
                }

                // 避免请求过快
                await Task.Delay(100);
            }

            Console.WriteLine($"\n[统计] 有效: {validCount}, 无效: {invalidCount}, 更新: {updatedCount}");
        }

        /// <summary>
        /// 从数据库获取所有股票代码
        /// </summary>
        private List<(string Code, string Name)> GetAllStockCodes()
        {
            var result = new List<(string, string)>();

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    string sql = @"
                        SELECT stock_code, stock_name 
                        FROM stock_info 
                        WHERE stock_code ~ '^[0-9]{6}$'
                        AND is_active = TRUE
                        ORDER BY stock_code";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string code = reader.GetString(0);
                            string name = reader.IsDBNull(1) ? code : reader.GetString(1);
                            result.Add((code, name));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[错误] 读取数据库失败: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 更新数据库中的股票名称
        /// </summary>
        private void UpdateStockName(string stockCode, string stockName)
        {
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    string sql = @"
                        UPDATE stock_info 
                        SET stock_name = @name, update_time = CURRENT_TIMESTAMP 
                        WHERE stock_code = @code";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("code", stockCode);
                        cmd.Parameters.AddWithValue("name", stockName);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[错误] 更新 {stockCode} 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 标记无效的股票代码
        /// </summary>
        private void MarkAsInactive(string stockCode)
        {
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    string sql = @"
                        UPDATE stock_info 
                        SET is_active = FALSE, update_time = CURRENT_TIMESTAMP 
                        WHERE stock_code = @code";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("code", stockCode);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[错误] 标记 {stockCode} 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 验证并标记无效代码
        /// </summary>
        public async Task ValidateAndMarkInvalid()
        {
            var stockCodes = GetAllStockCodes();
            Console.WriteLine($"\n[验证] 开始验证并标记无效股票...\n");

            int marked = 0;

            foreach (var (code, dbName) in stockCodes)
            {
                var (isValid, actualName) = await ValidateAndUpdate(code);
                
                if (!isValid)
                {
                    MarkAsInactive(code);
                    marked++;
                    Console.WriteLine($"[标记为无效] {code}: {dbName}");
                }

                await Task.Delay(100);
            }

            Console.WriteLine($"\n[完成] 标记了 {marked} 个无效代码");
        }
    }

    /// <summary>
    /// 运行验证工具的主程序
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            string connStr = "Host=localhost;Port=8532;Username=postgres;Password=123456;Database=stockdb";
            var validator = new StockNameValidator(connStr);

            Console.WriteLine("=".PadRight(60, '='));
            Console.WriteLine("股票代码名称验证工具");
            Console.WriteLine("=".PadRight(60, '='));
            Console.WriteLine();
            Console.WriteLine("1. 验证所有股票并更新名称");
            Console.WriteLine("2. 验证并标记无效股票");
            Console.WriteLine();
            Console.Write("请选择 (1/2): ");

            string choice = Console.ReadLine();

            if (choice == "1")
            {
                await validator.ValidateAllStocks();
            }
            else if (choice == "2")
            {
                await validator.ValidateAndMarkInvalid();
            }
            else
            {
                Console.WriteLine("无效选择");
            }

            Console.WriteLine("\n按任意键退出...");
            Console.ReadKey();
        }
    }
}
