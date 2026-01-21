using System;
using Npgsql;

class CheckStockNames
{
    static void Main()
    {
        string connStr = "Host=192.168.1.82;Port=5432;Database=aistock;Username=postgres;Password=123456";

        using (var conn = new NpgsqlConnection(connStr))
        {
            conn.Open();
            Console.WriteLine("数据库连接成功\n");

            // 1. 总体统计
            Console.WriteLine("=== 股票信息统计 ===");
            using (var cmd = new NpgsqlCommand(@"
                SELECT
                    COUNT(*) as total,
                    SUM(CASE WHEN stock_name <> stock_code AND stock_name IS NOT NULL AND stock_name <> '' THEN 1 ELSE 0 END) as has_name,
                    SUM(CASE WHEN stock_name = stock_code OR stock_name IS NULL OR stock_name = '' THEN 1 ELSE 0 END) as missing_name
                FROM stock_info", conn))
            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    Console.WriteLine($"总记录数: {reader.GetInt64(0)}");
                    Console.WriteLine($"有名称: {reader.GetInt64(1)}");
                    Console.WriteLine($"缺少名称: {reader.GetInt64(2)}");
                }
            }

            // 2. 列出缺少名称的股票（前50个）
            Console.WriteLine("\n=== 缺少名称的股票 (前50个) ===");
            using (var cmd = new NpgsqlCommand(@"
                SELECT stock_code, stock_name, market_name
                FROM stock_info
                WHERE stock_name = stock_code OR stock_name IS NULL OR stock_name = ''
                ORDER BY stock_code
                LIMIT 50", conn))
            using (var reader = cmd.ExecuteReader())
            {
                int count = 0;
                while (reader.Read())
                {
                    string code = reader.GetString(0);
                    string name = reader.IsDBNull(1) ? "NULL" : reader.GetString(1);
                    string market = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    Console.WriteLine($"{code} | {name} | {market}");
                    count++;
                }
                Console.WriteLine($"显示了 {count} 条记录");
            }

            // 3. 检查截图中标红的股票
            Console.WriteLine("\n=== 检查截图中的股票 ===");
            string[] checkCodes = { "000849", "000891", "000170", "000687", "000982", "000139", "000135", "000120", "000847", "000865", "000137", "000145", "000105", "000855", "000125", "000300" };
            using (var cmd = new NpgsqlCommand(@"
                SELECT stock_code, stock_name, market_name, is_active
                FROM stock_info
                WHERE stock_code = ANY(@codes)", conn))
            {
                cmd.Parameters.AddWithValue("@codes", checkCodes);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string code = reader.GetString(0);
                        string name = reader.IsDBNull(1) ? "NULL" : reader.GetString(1);
                        string market = reader.IsDBNull(2) ? "" : reader.GetString(2);
                        bool active = reader.IsDBNull(3) ? false : reader.GetBoolean(3);
                        Console.WriteLine($"{code} | {name,-10} | {market} | active={active}");
                    }
                }
            }

            // 4. 检查CSV导入的数据是否存在
            Console.WriteLine("\n=== 按市场统计 ===");
            using (var cmd = new NpgsqlCommand(@"
                SELECT market_name, COUNT(*) as cnt,
                    SUM(CASE WHEN stock_name <> stock_code THEN 1 ELSE 0 END) as has_name
                FROM stock_info
                GROUP BY market_name
                ORDER BY market_name", conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string market = reader.IsDBNull(0) ? "NULL" : reader.GetString(0);
                    long cnt = reader.GetInt64(1);
                    long hasName = reader.GetInt64(2);
                    Console.WriteLine($"{market}: 总数={cnt}, 有名称={hasName}");
                }
            }
        }
    }
}
