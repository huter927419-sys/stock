using System;
using System.Data;
using Npgsql;

namespace MQReceiver.Tools
{
    /// <summary>
    /// 检查日线数据表中的换手率和成交量数据
    /// </summary>
    class CheckTurnoverRateAndVolume
    {
        static void Main(string[] args)
        {
            string connectionString = "Host=localhost;Port=8532;Database=stockdb;Username=postgres;Password=cd123321";

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    Console.WriteLine("数据库连接成功！\n");

                    // 1. 检查字段是否存在
                    Console.WriteLine("========== 1. 检查字段是否存在 ==========");
                    CheckColumns(connection);

                    // 2. 统计总记录数
                    Console.WriteLine("\n========== 2. 统计总记录数 ==========");
                    CountRecords(connection);

                    // 3. 查看最近7天的数据统计
                    Console.WriteLine("\n========== 3. 最近7天的数据统计 ==========");
                    CountRecentDays(connection);

                    // 4. 查看有换手率数据的示例记录
                    Console.WriteLine("\n========== 4. 有换手率数据的示例记录（最近10条） ==========");
                    ShowRecordsWithTurnoverRate(connection);

                    // 5. 查看没有换手率但有成交量的记录
                    Console.WriteLine("\n========== 5. 没有换手率但有成交量的记录（最近10条） ==========");
                    ShowRecordsWithoutTurnoverRate(connection);

                    // 6. 统计有换手率数据的股票数量
                    Console.WriteLine("\n========== 6. 有换手率数据的股票数量（最近30天） ==========");
                    CountStocksWithTurnoverRate(connection);

                    // 7. 查看换手率数据分布
                    Console.WriteLine("\n========== 7. 换手率数据分布（最近30天） ==========");
                    ShowTurnoverRateDistribution(connection);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
                Console.WriteLine($"堆栈: {ex.StackTrace}");
            }

            Console.WriteLine("\n按任意键退出...");
            Console.ReadKey();
        }

        static void CheckColumns(NpgsqlConnection connection)
        {
            string sql = @"
                SELECT 
                    column_name, 
                    data_type, 
                    is_nullable,
                    column_default
                FROM information_schema.columns 
                WHERE table_name = 'stock_daily_data' 
                  AND column_name IN ('turnover_rate', 'volume', 'amount')
                ORDER BY column_name";

            using (var command = new NpgsqlCommand(sql, connection))
            using (var reader = command.ExecuteReader())
            {
                Console.WriteLine($"{"字段名",-20} {"数据类型",-20} {"可空",-10} {"默认值"}");
                Console.WriteLine(new string('-', 80));
                while (reader.Read())
                {
                    Console.WriteLine($"{reader["column_name"],-20} {reader["data_type"],-20} {reader["is_nullable"],-10} {reader["column_default"] ?? "NULL"}");
                }
            }
        }

        static void CountRecords(NpgsqlConnection connection)
        {
            string sql = @"
                SELECT 
                    COUNT(*) as total_records,
                    COUNT(volume) as records_with_volume,
                    COUNT(amount) as records_with_amount,
                    COUNT(turnover_rate) as records_with_turnover_rate,
                    COUNT(CASE WHEN volume > 0 THEN 1 END) as records_with_positive_volume,
                    COUNT(CASE WHEN turnover_rate IS NOT NULL AND turnover_rate > 0 THEN 1 END) as records_with_positive_turnover_rate
                FROM stock_daily_data";

            using (var command = new NpgsqlCommand(sql, connection))
            using (var reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    Console.WriteLine($"总记录数: {reader["total_records"]}");
                    Console.WriteLine($"有成交量的记录数: {reader["records_with_volume"]}");
                    Console.WriteLine($"有成交额的记录数: {reader["records_with_amount"]}");
                    Console.WriteLine($"有换手率的记录数: {reader["records_with_turnover_rate"]}");
                    Console.WriteLine($"成交量>0的记录数: {reader["records_with_positive_volume"]}");
                    Console.WriteLine($"换手率>0的记录数: {reader["records_with_positive_turnover_rate"]}");
                }
            }
        }

        static void CountRecentDays(NpgsqlConnection connection)
        {
            string sql = @"
                SELECT 
                    trade_date,
                    COUNT(*) as total_records,
                    COUNT(volume) as records_with_volume,
                    COUNT(turnover_rate) as records_with_turnover_rate,
                    COUNT(CASE WHEN volume > 0 THEN 1 END) as records_with_positive_volume,
                    COUNT(CASE WHEN turnover_rate IS NOT NULL AND turnover_rate > 0 THEN 1 END) as records_with_positive_turnover_rate,
                    AVG(volume) as avg_volume,
                    AVG(turnover_rate) as avg_turnover_rate
                FROM stock_daily_data
                WHERE trade_date >= CURRENT_DATE - INTERVAL '7 days'
                GROUP BY trade_date
                ORDER BY trade_date DESC";

            using (var command = new NpgsqlCommand(sql, connection))
            using (var reader = command.ExecuteReader())
            {
                Console.WriteLine($"{"日期",-12} {"总记录",-10} {"有成交量",-10} {"有换手率",-10} {"成交量>0",-10} {"换手率>0",-10} {"平均成交量",-15} {"平均换手率"}");
                Console.WriteLine(new string('-', 100));
                while (reader.Read())
                {
                    var date = reader["trade_date"] is DateTime dt ? dt.ToString("yyyy-MM-dd") : reader["trade_date"].ToString();
                    var avgVolume = reader["avg_volume"] == DBNull.Value ? "NULL" : Convert.ToDecimal(reader["avg_volume"]).ToString("F2");
                    var avgTurnover = reader["avg_turnover_rate"] == DBNull.Value ? "NULL" : Convert.ToDecimal(reader["avg_turnover_rate"]).ToString("F2");
                    Console.WriteLine($"{date,-12} {reader["total_records"],-10} {reader["records_with_volume"],-10} {reader["records_with_turnover_rate"],-10} {reader["records_with_positive_volume"],-10} {reader["records_with_positive_turnover_rate"],-10} {avgVolume,-15} {avgTurnover}");
                }
            }
        }

        static void ShowRecordsWithTurnoverRate(NpgsqlConnection connection)
        {
            string sql = @"
                SELECT 
                    stock_code,
                    trade_date,
                    volume,
                    amount,
                    turnover_rate,
                    open_price,
                    close_price
                FROM stock_daily_data
                WHERE turnover_rate IS NOT NULL
                ORDER BY trade_date DESC, stock_code
                LIMIT 10";

            using (var command = new NpgsqlCommand(sql, connection))
            using (var reader = command.ExecuteReader())
            {
                Console.WriteLine($"{"股票代码",-10} {"日期",-12} {"成交量",-15} {"成交额",-15} {"换手率",-10} {"开盘价",-10} {"收盘价"}");
                Console.WriteLine(new string('-', 100));
                while (reader.Read())
                {
                    var date = reader["trade_date"] is DateTime dt ? dt.ToString("yyyy-MM-dd") : reader["trade_date"].ToString();
                    Console.WriteLine($"{reader["stock_code"],-10} {date,-12} {reader["volume"],-15} {reader["amount"],-15} {reader["turnover_rate"],-10} {reader["open_price"],-10} {reader["close_price"]}");
                }
            }
        }

        static void ShowRecordsWithoutTurnoverRate(NpgsqlConnection connection)
        {
            string sql = @"
                SELECT 
                    stock_code,
                    trade_date,
                    volume,
                    amount,
                    turnover_rate,
                    open_price,
                    close_price
                FROM stock_daily_data
                WHERE turnover_rate IS NULL 
                  AND volume > 0
                ORDER BY trade_date DESC, stock_code
                LIMIT 10";

            using (var command = new NpgsqlCommand(sql, connection))
            using (var reader = command.ExecuteReader())
            {
                Console.WriteLine($"{"股票代码",-10} {"日期",-12} {"成交量",-15} {"成交额",-15} {"换手率",-10} {"开盘价",-10} {"收盘价"}");
                Console.WriteLine(new string('-', 100));
                while (reader.Read())
                {
                    var date = reader["trade_date"] is DateTime dt ? dt.ToString("yyyy-MM-dd") : reader["trade_date"].ToString();
                    Console.WriteLine($"{reader["stock_code"],-10} {date,-12} {reader["volume"],-15} {reader["amount"],-15} {"NULL",-10} {reader["open_price"],-10} {reader["close_price"]}");
                }
            }
        }

        static void CountStocksWithTurnoverRate(NpgsqlConnection connection)
        {
            string sql = @"
                SELECT 
                    COUNT(DISTINCT stock_code) as stocks_with_turnover_rate
                FROM stock_daily_data
                WHERE trade_date >= CURRENT_DATE - INTERVAL '30 days'
                  AND turnover_rate IS NOT NULL
                  AND turnover_rate > 0";

            using (var command = new NpgsqlCommand(sql, connection))
            using (var reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    Console.WriteLine($"有换手率数据的股票数量（最近30天）: {reader["stocks_with_turnover_rate"]}");
                }
            }
        }

        static void ShowTurnoverRateDistribution(NpgsqlConnection connection)
        {
            string sql = @"
                SELECT 
                    CASE 
                        WHEN turnover_rate IS NULL THEN 'NULL'
                        WHEN turnover_rate = 0 THEN '0'
                        WHEN turnover_rate > 0 AND turnover_rate <= 1 THEN '0-1%'
                        WHEN turnover_rate > 1 AND turnover_rate <= 3 THEN '1-3%'
                        WHEN turnover_rate > 3 AND turnover_rate <= 5 THEN '3-5%'
                        WHEN turnover_rate > 5 AND turnover_rate <= 10 THEN '5-10%'
                        ELSE '>10%'
                    END as turnover_rate_range,
                    COUNT(*) as record_count
                FROM stock_daily_data
                WHERE trade_date >= CURRENT_DATE - INTERVAL '30 days'
                GROUP BY 
                    CASE 
                        WHEN turnover_rate IS NULL THEN 'NULL'
                        WHEN turnover_rate = 0 THEN '0'
                        WHEN turnover_rate > 0 AND turnover_rate <= 1 THEN '0-1%'
                        WHEN turnover_rate > 1 AND turnover_rate <= 3 THEN '1-3%'
                        WHEN turnover_rate > 3 AND turnover_rate <= 5 THEN '3-5%'
                        WHEN turnover_rate > 5 AND turnover_rate <= 10 THEN '5-10%'
                        ELSE '>10%'
                    END
                ORDER BY 
                    CASE 
                        WHEN turnover_rate IS NULL THEN 0
                        WHEN turnover_rate = 0 THEN 1
                        WHEN turnover_rate > 0 AND turnover_rate <= 1 THEN 2
                        WHEN turnover_rate > 1 AND turnover_rate <= 3 THEN 3
                        WHEN turnover_rate > 3 AND turnover_rate <= 5 THEN 4
                        WHEN turnover_rate > 5 AND turnover_rate <= 10 THEN 5
                        ELSE 6
                    END";

            using (var command = new NpgsqlCommand(sql, connection))
            using (var reader = command.ExecuteReader())
            {
                Console.WriteLine($"{"换手率范围",-15} {"记录数"}");
                Console.WriteLine(new string('-', 30));
                while (reader.Read())
                {
                    Console.WriteLine($"{reader["turnover_rate_range"],-15} {reader["record_count"]}");
                }
            }
        }
    }
}
