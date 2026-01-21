// 从东方财富API获取完整股票名称并更新到数据库
// 编译运行：在Visual Studio中添加此文件到项目，或使用 csc 编译

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Npgsql;

namespace MQReceiver.Tools
{
    /// <summary>
    /// 从东方财富获取股票名称并更新到数据库
    /// </summary>
    public class FetchStockNames
    {
        private static string connectionString = "Host=localhost;Port=8532;Database=stockdb;Username=postgres;Password=cd123321";

        public static void Run()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("========================================");
            Console.WriteLine("  从东方财富获取股票名称");
            Console.WriteLine("========================================");
            Console.WriteLine();

            try
            {
                // 获取沪深A股列表
                var stocks = FetchAllStocks();
                Console.WriteLine($"共获取到 {stocks.Count} 只股票");

                if (stocks.Count > 0)
                {
                    // 更新到数据库
                    int updated = UpdateDatabase(stocks);
                    Console.WriteLine($"成功更新 {updated} 条记录到数据库");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
            }

            Console.WriteLine();
            Console.WriteLine("完成！");
        }

        /// <summary>
        /// 从东方财富API获取所有股票
        /// </summary>
        private static List<(string Code, string Name)> FetchAllStocks()
        {
            var result = new List<(string Code, string Name)>();

            // 东方财富API - 获取沪深A股
            // m:0+t:6 深圳主板, m:0+t:80 深圳创业板, m:1+t:2 上海主板, m:1+t:23 上海科创板
            string[] markets = new string[]
            {
                "m:0+t:6,m:0+t:13,m:0+t:80",  // 深圳: 主板+中小板+创业板
                "m:1+t:2,m:1+t:23",            // 上海: 主板+科创板
                "m:0+t:81"                     // 北交所
            };

            foreach (var market in markets)
            {
                try
                {
                    string url = $"https://push2.eastmoney.com/api/qt/clist/get?pn=1&pz=6000&po=1&np=1&ut=bd1d9ddb04089700cf9c27f6f7426281&fltt=2&invt=2&fid=f12&fs={market}&fields=f12,f14";

                    using (var client = new WebClient())
                    {
                        client.Encoding = Encoding.UTF8;
                        client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                        client.Headers.Add("Referer", "https://quote.eastmoney.com/");

                        string json = client.DownloadString(url);

                        // 解析JSON - 提取 f12(代码) 和 f14(名称)
                        var matches = Regex.Matches(json, @"""f12"":""(\d{6})"",""f14"":""([^""]+)""");
                        foreach (Match match in matches)
                        {
                            if (match.Success)
                            {
                                string code = match.Groups[1].Value;
                                string name = match.Groups[2].Value;
                                result.Add((code, name));
                            }
                        }

                        Console.WriteLine($"  {market}: 获取 {matches.Count} 只股票");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  获取 {market} 失败: {ex.Message}");
                }
            }

            return result;
        }

        /// <summary>
        /// 更新股票名称到数据库
        /// </summary>
        private static int UpdateDatabase(List<(string Code, string Name)> stocks)
        {
            int updated = 0;

            using (var conn = new NpgsqlConnection(connectionString))
            {
                conn.Open();

                // 使用批量更新
                string sql = @"
                    UPDATE stock_info
                    SET stock_name = @name, update_time = CURRENT_TIMESTAMP
                    WHERE stock_code = @code
                      AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code)";

                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        foreach (var stock in stocks)
                        {
                            using (var cmd = new NpgsqlCommand(sql, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@code", stock.Code);
                                cmd.Parameters.AddWithValue("@name", stock.Name);
                                int affected = cmd.ExecuteNonQuery();
                                if (affected > 0) updated++;
                            }
                        }
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }

                // 显示更新后的统计
                string statsSql = @"
                    SELECT
                        COUNT(*) AS total_count,
                        SUM(CASE WHEN stock_name <> stock_code THEN 1 ELSE 0 END) AS has_name_count,
                        SUM(CASE WHEN stock_name = stock_code OR stock_name IS NULL THEN 1 ELSE 0 END) AS no_name_count
                    FROM stock_info";

                using (var cmd = new NpgsqlCommand(statsSql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        Console.WriteLine($"更新后统计: 总数={reader.GetInt64(0)}, 有名称={reader.GetInt64(1)}, 无名称={reader.GetInt64(2)}");
                    }
                }
            }

            return updated;
        }
    }
}
