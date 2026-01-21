using System;
using System.IO;
using System.Text;
using Npgsql;

class ExportData
{
    static void Main()
    {
        string connectionString = "Host=localhost;Port=8532;Database=stockdb;Username=postgres;Password=cd123321";
        string outputDir = @"f:\dsfr\mqq\db";

        try
        {
            using (var conn = new NpgsqlConnection(connectionString))
            {
                conn.Open();
                Console.WriteLine("数据库连接成功！");

                // 导出 stock_info
                ExportTable(conn, "stock_info", Path.Combine(outputDir, "stock_info_data.sql"));

                // 导出 stock_exrights_data
                ExportTable(conn, "stock_exrights_data", Path.Combine(outputDir, "stock_exrights_data.sql"));

                Console.WriteLine("\n导出完成！");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"错误: {ex.Message}");
        }
    }

    static void ExportTable(NpgsqlConnection conn, string tableName, string outputFile)
    {
        Console.WriteLine($"\n正在导出 {tableName}...");

        using (var cmd = new NpgsqlCommand($"SELECT * FROM {tableName}", conn))
        using (var reader = cmd.ExecuteReader())
        {
            var sb = new StringBuilder();
            sb.AppendLine($"-- {tableName} 数据导出");
            sb.AppendLine($"-- 导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            int count = 0;
            while (reader.Read())
            {
                var columns = new StringBuilder();
                var values = new StringBuilder();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    if (i > 0)
                    {
                        columns.Append(", ");
                        values.Append(", ");
                    }

                    columns.Append(reader.GetName(i));

                    if (reader.IsDBNull(i))
                    {
                        values.Append("NULL");
                    }
                    else
                    {
                        var value = reader.GetValue(i);
                        if (value is string || value is DateTime)
                        {
                            values.Append($"'{value.ToString().Replace("'", "''")}'");
                        }
                        else if (value is bool b)
                        {
                            values.Append(b ? "TRUE" : "FALSE");
                        }
                        else
                        {
                            values.Append(value);
                        }
                    }
                }

                sb.AppendLine($"INSERT INTO {tableName} ({columns}) VALUES ({values});");
                count++;
            }

            File.WriteAllText(outputFile, sb.ToString(), Encoding.UTF8);
            Console.WriteLine($"  导出 {count} 条记录到 {outputFile}");
        }
    }
}
