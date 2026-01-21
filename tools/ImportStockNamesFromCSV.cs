using System;
using System.IO;
using System.Text;
using Npgsql;

/// <summary>
/// 从CSV文件导入股票名称到数据库
/// 用法: 编译后运行，或在程序中调用 ImportStockNamesFromCSV.Run()
/// </summary>
public class ImportStockNamesFromCSV
{
    private static readonly string ConnectionString =
        "Host=192.168.1.82;Port=5432;Database=aistock;Username=postgres;Password=123456";

    public static void Main(string[] args)
    {
        string csvPath = args.Length > 0 ? args[0] : @"F:\dsfr\StockList_20260106_232144.csv";
        Run(csvPath);
    }

    public static void Run(string csvPath)
    {
        if (!File.Exists(csvPath))
        {
            Console.WriteLine($"错误: 文件不存在 - {csvPath}");
            return;
        }

        Console.WriteLine($"开始从 {csvPath} 导入股票名称...");

        int total = 0;
        int updated = 0;
        int inserted = 0;
        int errors = 0;

        try
        {
            using (var conn = new NpgsqlConnection(ConnectionString))
            {
                conn.Open();
                Console.WriteLine("数据库连接成功");

                // 读取CSV文件
                var lines = File.ReadAllLines(csvPath, Encoding.UTF8);
                total = lines.Length - 1; // 减去表头

                Console.WriteLine($"读取到 {total} 条股票记录");

                // 使用事务批量更新
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // 准备更新语句
                        string updateSql = @"
                            INSERT INTO stock_info (stock_code, stock_name, market_code, market_name, stock_type, is_active)
                            VALUES (@code, @name, @market_code, @market_name, '股票', TRUE)
                            ON CONFLICT (stock_code) DO UPDATE SET
                                stock_name = @name,
                                update_time = CURRENT_TIMESTAMP";

                        for (int i = 1; i < lines.Length; i++) // 跳过表头
                        {
                            string line = lines[i].Trim();
                            if (string.IsNullOrEmpty(line)) continue;

                            // 解析CSV行: 股票代码,股票名称
                            var parts = line.Split(',');
                            if (parts.Length < 2) continue;

                            string stockCode = parts[0].Trim();
                            string stockName = parts[1].Trim();

                            // 验证股票代码格式
                            if (stockCode.Length != 6 || !IsDigitsOnly(stockCode))
                            {
                                errors++;
                                continue;
                            }

                            // 确定市场代码
                            int marketCode;
                            string marketName;
                            GetMarketInfo(stockCode, out marketCode, out marketName);

                            try
                            {
                                using (var cmd = new NpgsqlCommand(updateSql, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@code", stockCode);
                                    cmd.Parameters.AddWithValue("@name", stockName);
                                    cmd.Parameters.AddWithValue("@market_code", (short)marketCode);
                                    cmd.Parameters.AddWithValue("@market_name", marketName);

                                    int affected = cmd.ExecuteNonQuery();
                                    if (affected > 0) updated++;
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"更新 {stockCode} 失败: {ex.Message}");
                                errors++;
                            }

                            // 进度显示
                            if (i % 500 == 0)
                            {
                                Console.WriteLine($"进度: {i}/{total}");
                            }
                        }

                        transaction.Commit();
                        Console.WriteLine("事务提交成功");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Console.WriteLine($"事务回滚: {ex.Message}");
                        throw;
                    }
                }

                // 统计结果
                using (var cmd = new NpgsqlCommand(@"
                    SELECT
                        COUNT(*) AS total,
                        SUM(CASE WHEN stock_name <> stock_code AND stock_name IS NOT NULL THEN 1 ELSE 0 END) AS has_name
                    FROM stock_info", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        Console.WriteLine($"\n数据库统计: 总记录={reader.GetInt64(0)}, 有名称={reader.GetInt64(1)}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"导入失败: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }

        Console.WriteLine($"\n导入完成: 总计={total}, 更新={updated}, 错误={errors}");
    }

    private static bool IsDigitsOnly(string str)
    {
        foreach (char c in str)
        {
            if (c < '0' || c > '9') return false;
        }
        return true;
    }

    private static void GetMarketInfo(string stockCode, out int marketCode, out string marketName)
    {
        // 上海(1): 600/601/603/605(主板), 688(科创板)
        // 深圳(0): 000/001(主板), 002/003(中小板), 300/301(创业板)
        // 北京(2): 43/83/87/88开头
        if (stockCode.StartsWith("6"))
        {
            marketCode = 1;
            marketName = "上海";
        }
        else if (stockCode.StartsWith("43") || stockCode.StartsWith("83") ||
                 stockCode.StartsWith("87") || stockCode.StartsWith("88"))
        {
            marketCode = 2;
            marketName = "北京";
        }
        else
        {
            marketCode = 0;
            marketName = "深圳";
        }
    }
}
