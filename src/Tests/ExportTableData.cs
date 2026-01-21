using System;
using System.Configuration;
using System.IO;
using System.Text;
using Npgsql;

namespace MQReceiver.Tests
{
    /// <summary>
    /// 导出数据库表数据
    /// </summary>
    class ExportTableData
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Console.WriteLine("========================================");
            Console.WriteLine("  数据库表导出工具");
            Console.WriteLine("========================================");
            Console.WriteLine();

            string host = ConfigurationManager.AppSettings["DatabaseHost"] ?? "localhost";
            int port = int.Parse(ConfigurationManager.AppSettings["DatabasePort"] ?? "8532");
            string database = ConfigurationManager.AppSettings["DatabaseName"] ?? "stockdb";
            string username = ConfigurationManager.AppSettings["DatabaseUser"] ?? "postgres";
            string password = ConfigurationManager.AppSettings["DatabasePassword"] ?? "cd123321";

            string connStr = $"Host={host};Port={port};Database={database};Username={username};Password={password};Timeout=30;";
            string outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "db");

            // 确保输出目录存在
            if (!Directory.Exists(outputDir))
            {
                outputDir = @"f:\dsfr\mqq\db";
            }

            try
            {
                using (var conn = new NpgsqlConnection(connStr))
                {
                    conn.Open();
                    Console.WriteLine($"数据库连接成功: {host}:{port}/{database}");
                    Console.WriteLine($"输出目录: {outputDir}");
                    Console.WriteLine();

                    // 导出 stock_info
                    ExportTable(conn, "stock_info", Path.Combine(outputDir, "stock_info_data.sql"));

                    // 导出 stock_exrights_data
                    ExportTable(conn, "stock_exrights_data", Path.Combine(outputDir, "stock_exrights_data.sql"));

                    Console.WriteLine();
                    Console.WriteLine("========================================");
                    Console.WriteLine("  导出完成！");
                    Console.WriteLine("========================================");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine();
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
        }

        static void ExportTable(NpgsqlConnection conn, string tableName, string outputFile)
        {
            Console.WriteLine($"正在导出 {tableName}...");

            using (var cmd = new NpgsqlCommand($"SELECT * FROM {tableName} ORDER BY 1", conn))
            using (var reader = cmd.ExecuteReader())
            {
                var sb = new StringBuilder();
                sb.AppendLine($"-- {tableName} 数据导出");
                sb.AppendLine($"-- 导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"-- 使用方法: psql -h localhost -p 8532 -U postgres -d stockdb -f {Path.GetFileName(outputFile)}");
                sb.AppendLine();
                sb.AppendLine($"-- 清空表（可选，取消注释使用）");
                sb.AppendLine($"-- TRUNCATE TABLE {tableName};");
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
                            var fieldType = reader.GetFieldType(i);
                            var value = reader.GetValue(i);

                            if (fieldType == typeof(string))
                            {
                                values.Append($"'{value.ToString().Replace("'", "''")}'");
                            }
                            else if (fieldType == typeof(DateTime))
                            {
                                values.Append($"'{((DateTime)value):yyyy-MM-dd HH:mm:ss}'");
                            }
                            else if (fieldType == typeof(bool))
                            {
                                values.Append((bool)value ? "TRUE" : "FALSE");
                            }
                            else if (fieldType == typeof(decimal) || fieldType == typeof(double) || fieldType == typeof(float))
                            {
                                values.Append(Convert.ToDecimal(value).ToString("G"));
                            }
                            else
                            {
                                values.Append(value);
                            }
                        }
                    }

                    sb.AppendLine($"INSERT INTO {tableName} ({columns}) VALUES ({values}) ON CONFLICT DO NOTHING;");
                    count++;
                }

                File.WriteAllText(outputFile, sb.ToString(), Encoding.UTF8);
                Console.WriteLine($"  ✓ 导出 {count} 条记录到 {outputFile}");
            }
        }
    }
}
