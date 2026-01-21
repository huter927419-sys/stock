using System;
using System.Collections.Generic;
using Npgsql;

/// <summary>
/// 更新股票名称的工具类
/// 用法: 编译后运行，或使用 dotnet script 执行
/// </summary>
class UpdateStockNames
{
    // 本地数据库连接字符串 (与 App.config 一致)
    private static string connectionString = "Host=localhost;Port=8532;Database=stockdb;Username=postgres;Password=cd123321";

    static void Main()
    {
        // 需要更新的股票名称
        var stockNames = new Dictionary<string, string>
        {
            {"000022", "深赤湾A"},
            {"000033", "同花顺"},
            {"000057", "厦门象屿"},
            {"000071", "凤凰光学"},
            {"000073", "光明肉业"},
            {"000092", "惠天热电"},
            {"000094", "大名城"},
            {"000101", "明星电力"},
            {"000105", "永鼎股份"},
            {"000106", "重庆路桥"},
            {"000112", "浙江东日"},
            {"000113", "浙江东方"},
            {"000116", "三峡水利"},
            {"000117", "西宁特钢"},
            {"000119", "瑞茂通"},
            {"000122", "兰花科创"},
            {"000125", "铁龙物流"},
            {"000130", "波导股份"},
            {"000132", "重庆啤酒"},
            {"000133", "东湖高新"},
            {"000135", "乐凯胶片"},
            {"000141", "中国宝安"},
            {"000145", "廊坊发展"},
            {"000152", "维科技术"},
            {"000160", "巨化股份"},
            {"000853", "冀东装备"},
            {"000854", "春兰股份"},
            {"000891", "阳光城"},
            {"000982", "宁波能源"}
        };

        try
        {
            using (var conn = new NpgsqlConnection(connectionString))
            {
                conn.Open();
                Console.WriteLine("数据库连接成功");
                Console.WriteLine($"连接: {connectionString.Replace("cd123321", "***")}");
                Console.WriteLine();

                int updatedCount = 0;
                int insertedCount = 0;

                foreach (var kvp in stockNames)
                {
                    string stockCode = kvp.Key;
                    string stockName = kvp.Value;

                    // 先检查记录是否存在
                    string checkSql = "SELECT COUNT(*) FROM stock_info WHERE stock_code = @code";
                    using (var checkCmd = new NpgsqlCommand(checkSql, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@code", stockCode);
                        long count = (long)checkCmd.ExecuteScalar();

                        if (count > 0)
                        {
                            // 更新现有记录 (强制更新，覆盖任何现有名称)
                            string updateSql = @"
                                UPDATE stock_info
                                SET stock_name = @name, update_time = @time
                                WHERE stock_code = @code";

                            using (var updateCmd = new NpgsqlCommand(updateSql, conn))
                            {
                                updateCmd.Parameters.AddWithValue("@code", stockCode);
                                updateCmd.Parameters.AddWithValue("@name", stockName);
                                updateCmd.Parameters.AddWithValue("@time", DateTime.Now);

                                int affected = updateCmd.ExecuteNonQuery();
                                if (affected > 0)
                                {
                                    Console.WriteLine($"已更新: {stockCode} -> {stockName}");
                                    updatedCount++;
                                }
                                else
                                {
                                    // 检查当前值
                                    using (var queryCmd = new NpgsqlCommand("SELECT stock_name FROM stock_info WHERE stock_code = @code", conn))
                                    {
                                        queryCmd.Parameters.AddWithValue("@code", stockCode);
                                        var currentName = queryCmd.ExecuteScalar();
                                        Console.WriteLine($"跳过: {stockCode} (当前名称: {currentName})");
                                    }
                                }
                            }
                        }
                        else
                        {
                            // 插入新记录
                            string marketCode = stockCode.StartsWith("6") ? "1" : "0";
                            string marketName = stockCode.StartsWith("6") ? "上海" : "深圳";

                            string insertSql = @"
                                INSERT INTO stock_info (stock_code, stock_name, market_code, market_name, is_active, update_time)
                                VALUES (@code, @name, @marketCode, @marketName, true, @time)";

                            using (var insertCmd = new NpgsqlCommand(insertSql, conn))
                            {
                                insertCmd.Parameters.AddWithValue("@code", stockCode);
                                insertCmd.Parameters.AddWithValue("@name", stockName);
                                insertCmd.Parameters.AddWithValue("@marketCode", marketCode);
                                insertCmd.Parameters.AddWithValue("@marketName", marketName);
                                insertCmd.Parameters.AddWithValue("@time", DateTime.Now);

                                insertCmd.ExecuteNonQuery();
                                Console.WriteLine($"已插入: {stockCode} -> {stockName} ({marketName})");
                                insertedCount++;
                            }
                        }
                    }
                }

                Console.WriteLine();
                Console.WriteLine($"操作完成: 更新 {updatedCount} 条, 插入 {insertedCount} 条");

                // 验证结果
                Console.WriteLine();
                Console.WriteLine("=== 验证结果 ===");
                string verifySql = @"
                    SELECT stock_code, stock_name, market_name, update_time
                    FROM stock_info
                    WHERE stock_code = ANY(@codes)
                    ORDER BY stock_code";

                using (var verifyCmd = new NpgsqlCommand(verifySql, conn))
                {
                    verifyCmd.Parameters.AddWithValue("@codes", new List<string>(stockNames.Keys).ToArray());
                    using (var reader = verifyCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string code = reader.GetString(0);
                            string name = reader.IsDBNull(1) ? "NULL" : reader.GetString(1);
                            string market = reader.IsDBNull(2) ? "" : reader.GetString(2);
                            DateTime? updateTime = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3);
                            Console.WriteLine($"{code} | {name,-12} | {market} | {updateTime:yyyy-MM-dd HH:mm:ss}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"错误: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}
