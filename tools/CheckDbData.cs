using System;
using Npgsql;

class CheckDbData
{
    static void Main()
    {
        string connStr = "Host=localhost;Port=8532;Database=stockdb;Username=postgres;Password=cd123321";

        using (var conn = new NpgsqlConnection(connStr))
        {
            conn.Open();
            Console.WriteLine("=== 数据库连接成功 ===\n");

            // 1. 检查各表数据量
            Console.WriteLine("=== 各表数据量 ===");
            CheckCount(conn, "stock_info", "股票信息表");
            CheckCount(conn, "stock_daily_data", "日线数据表");
            CheckCount(conn, "stock_realtime_data", "实时数据表");
            CheckCount(conn, "stock_exrights_data", "除权数据表");

            // 2. 检查日线数据日期范围
            Console.WriteLine("\n=== 日线数据日期范围 ===");
            using (var cmd = new NpgsqlCommand(
                "SELECT MIN(trade_date), MAX(trade_date) FROM stock_daily_data", conn))
            {
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        Console.WriteLine($"最早日期: {reader.GetDateTime(0):yyyy-MM-dd}");
                        Console.WriteLine($"最新日期: {reader.GetDateTime(1):yyyy-MM-dd}");
                    }
                }
            }

            // 3. 检查最近10个交易日的数据量
            Console.WriteLine("\n=== 最近10个交易日数据量 ===");
            using (var cmd = new NpgsqlCommand(@"
                SELECT trade_date, COUNT(DISTINCT stock_code) as stock_count
                FROM stock_daily_data
                WHERE trade_date >= CURRENT_DATE - INTERVAL '20 days'
                GROUP BY trade_date
                ORDER BY trade_date DESC
                LIMIT 10", conn))
            {
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Console.WriteLine($"{reader.GetDateTime(0):yyyy-MM-dd}: {reader.GetInt64(1)} 只股票");
                    }
                }
            }

            // 4. 检查实时数据更新时间
            Console.WriteLine("\n=== 实时数据更新时间 ===");
            using (var cmd = new NpgsqlCommand(
                "SELECT MIN(update_time), MAX(update_time) FROM stock_realtime_data", conn))
            {
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        if (!reader.IsDBNull(0))
                            Console.WriteLine($"最早更新: {reader.GetDateTime(0):yyyy-MM-dd HH:mm:ss}");
                        if (!reader.IsDBNull(1))
                            Console.WriteLine($"最新更新: {reader.GetDateTime(1):yyyy-MM-dd HH:mm:ss}");
                    }
                }
            }

            // 5. 检查股票名称统计
            Console.WriteLine("\n=== 股票名称统计 ===");
            using (var cmd = new NpgsqlCommand(@"
                SELECT
                    COUNT(*) as total,
                    SUM(CASE WHEN stock_name = stock_code OR stock_name IS NULL OR stock_name = '' THEN 1 ELSE 0 END) as missing_name,
                    SUM(CASE WHEN stock_name <> stock_code AND stock_name IS NOT NULL AND stock_name <> '' THEN 1 ELSE 0 END) as has_name
                FROM stock_info", conn))
            {
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        Console.WriteLine($"总数: {reader.GetInt64(0)}");
                        Console.WriteLine($"缺失名称: {reader.GetInt64(1)}");
                        Console.WriteLine($"有名称: {reader.GetInt64(2)}");
                    }
                }
            }

            // 6. 抽样检查几只股票的日线数据
            Console.WriteLine("\n=== 抽样股票最近数据 ===");
            string[] sampleStocks = { "000001", "600000", "300001" };
            foreach (var code in sampleStocks)
            {
                Console.WriteLine($"\n--- {code} ---");
                using (var cmd = new NpgsqlCommand($@"
                    SELECT trade_date, open_price, high_price, low_price, close_price, volume
                    FROM stock_daily_data
                    WHERE stock_code = '{code}'
                    ORDER BY trade_date DESC
                    LIMIT 5", conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Console.WriteLine($"{reader.GetDateTime(0):yyyy-MM-dd} O:{reader.GetDecimal(1):F2} H:{reader.GetDecimal(2):F2} L:{reader.GetDecimal(3):F2} C:{reader.GetDecimal(4):F2} V:{reader.GetDecimal(5):F0}");
                        }
                    }
                }
            }

            // 7. 检查KD计算需要的数据量
            Console.WriteLine("\n=== KD计算数据检查 ===");
            using (var cmd = new NpgsqlCommand(@"
                SELECT stock_code, COUNT(*) as cnt
                FROM stock_daily_data
                GROUP BY stock_code
                HAVING COUNT(*) < 50
                ORDER BY cnt
                LIMIT 10", conn))
            {
                using (var reader = cmd.ExecuteReader())
                {
                    Console.WriteLine("数据量不足50条的股票（前10个）:");
                    while (reader.Read())
                    {
                        Console.WriteLine($"  {reader.GetString(0)}: {reader.GetInt64(1)} 条");
                    }
                }
            }

            Console.WriteLine("\n=== 检查完成 ===");
        }
    }

    static void CheckCount(NpgsqlConnection conn, string table, string name)
    {
        using (var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {table}", conn))
        {
            var count = cmd.ExecuteScalar();
            Console.WriteLine($"{name}: {count}");
        }
    }
}
