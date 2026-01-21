using System;
using Npgsql;

class Program
{
    static void Main()
    {
        string connStr = "Host=localhost;Port=8532;Database=stockdb;Username=postgres;Password=123456";
        string[] codes = {"000891","000022","000101","000071","000033","000854","000141","000119","000112","000132","000117","000057","000982","000133","000135","000106","000145","000105","000094","000092","000853","000160","000073","000122","000116","000130","000152","000125","000113"};
        
        using (var conn = new NpgsqlConnection(connStr))
        {
            conn.Open();
            
            Console.WriteLine("股票代码 | stock_info名称 | realtime_data名称 | daily_data存在");
            Console.WriteLine(new string('-', 80));
            
            foreach (var code in codes)
            {
                string infoName = "-";
                string realtimeName = "-";
                bool hasDailyData = false;
                
                // 查 stock_info
                using (var cmd = new NpgsqlCommand("SELECT stock_name FROM stock_info WHERE stock_code = @code", conn))
                {
                    cmd.Parameters.AddWithValue("code", code.Trim());
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                        infoName = result.ToString();
                }
                
                // 查 stock_realtime_data
                using (var cmd = new NpgsqlCommand("SELECT stock_name FROM stock_realtime_data WHERE stock_code = @code LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("code", code.Trim());
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                        realtimeName = result.ToString();
                }
                
                // 查 stock_daily_data
                using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM stock_daily_data WHERE stock_code = @code", conn))
                {
                    cmd.Parameters.AddWithValue("code", code.Trim());
                    var result = cmd.ExecuteScalar();
                    hasDailyData = Convert.ToInt64(result) > 0;
                }
                
                Console.WriteLine($"{code.Trim().PadRight(8)} | {infoName.PadRight(14)} | {realtimeName.PadRight(17)} | {(hasDailyData ? "是" : "否")}");
            }
        }
    }
}
