// 从本地Excel文件导入股票名称到数据库
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Npgsql;

namespace MQReceiver.Tools
{
    /// <summary>
    /// 从本地Excel文件导入股票名称
    /// 支持 A股列表.xlsx 和 GPLIST.xls
    /// </summary>
    public class ImportStockNamesFromExcel
    {
        private static string connectionString = "Host=localhost;Port=8532;Database=stockdb;Username=postgres;Password=cd123321";

        public static void Run()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("========================================");
            Console.WriteLine("  从本地Excel导入股票名称");
            Console.WriteLine("========================================");
            Console.WriteLine();

            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            // 向上查找到项目根目录
            string projectRoot = FindProjectRoot(basePath);

            if (string.IsNullOrEmpty(projectRoot))
            {
                projectRoot = @"f:\dsfr\mqq";
            }

            var allStocks = new Dictionary<string, string>(); // code -> name

            // 1. 读取 GPLIST.xls (优先，因为有简称)
            string gplistPath = Path.Combine(projectRoot, "GPLIST.xls");
            if (File.Exists(gplistPath))
            {
                Console.WriteLine($"读取: {gplistPath}");
                var stocks = ReadGPList(gplistPath);
                Console.WriteLine($"  获取到 {stocks.Count} 只股票");
                foreach (var kv in stocks)
                {
                    allStocks[kv.Key] = kv.Value;
                }
            }
            else
            {
                Console.WriteLine($"文件不存在: {gplistPath}");
            }

            // 2. 读取 A股列表.xlsx (补充)
            string aSharePath = Path.Combine(projectRoot, "A股列表.xlsx");
            if (File.Exists(aSharePath))
            {
                Console.WriteLine($"读取: {aSharePath}");
                var stocks = ReadAShareList(aSharePath);
                Console.WriteLine($"  获取到 {stocks.Count} 只股票");
                foreach (var kv in stocks)
                {
                    // 只添加GPLIST中没有的
                    if (!allStocks.ContainsKey(kv.Key))
                    {
                        allStocks[kv.Key] = kv.Value;
                    }
                }
            }
            else
            {
                Console.WriteLine($"文件不存在: {aSharePath}");
            }

            Console.WriteLine();
            Console.WriteLine($"合计获取到 {allStocks.Count} 只股票");

            if (allStocks.Count > 0)
            {
                int updated = UpdateDatabase(allStocks);
                Console.WriteLine($"成功更新 {updated} 条记录到数据库");
            }

            Console.WriteLine();
            Console.WriteLine("完成！");
        }

        /// <summary>
        /// 查找项目根目录
        /// </summary>
        private static string FindProjectRoot(string startPath)
        {
            string current = startPath;
            for (int i = 0; i < 5; i++)
            {
                if (File.Exists(Path.Combine(current, "MQReceiver.csproj")))
                {
                    return current;
                }
                string parent = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(parent) || parent == current)
                    break;
                current = parent;
            }
            return null;
        }

        /// <summary>
        /// 读取 GPLIST.xls
        /// 第1列: A股代码, 第3列: 证券简称
        /// </summary>
        private static Dictionary<string, string> ReadGPList(string filePath)
        {
            var result = new Dictionary<string, string>();
            dynamic excel = null;
            dynamic workbook = null;

            try
            {
                Type excelType = Type.GetTypeFromProgID("Excel.Application");
                if (excelType == null)
                {
                    Console.WriteLine("  错误: 未安装Excel");
                    return result;
                }

                excel = Activator.CreateInstance(excelType);
                excel.Visible = false;
                excel.DisplayAlerts = false;

                workbook = excel.Workbooks.Open(filePath);
                dynamic sheet = workbook.Sheets[1];

                int rowCount = sheet.UsedRange.Rows.Count;
                Console.WriteLine($"  总行数: {rowCount}");

                for (int row = 2; row <= rowCount; row++) // 跳过标题行
                {
                    string code = Convert.ToString(sheet.Cells[row, 1].Value);
                    string name = Convert.ToString(sheet.Cells[row, 3].Value);

                    if (!string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(name)
                        && code != "-" && name != "-")
                    {
                        // 确保是6位数字代码
                        code = code.Trim().PadLeft(6, '0');
                        if (code.Length == 6 && IsDigitsOnly(code))
                        {
                            result[code] = name.Trim();
                        }
                    }
                }

                workbook.Close(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  读取Excel失败: {ex.Message}");
            }
            finally
            {
                if (workbook != null)
                {
                    try { workbook.Close(false); } catch { }
                    Marshal.ReleaseComObject(workbook);
                }
                if (excel != null)
                {
                    try { excel.Quit(); } catch { }
                    Marshal.ReleaseComObject(excel);
                }
            }

            return result;
        }

        /// <summary>
        /// 读取 A股列表.xlsx
        /// 第5列: A股代码, 第2列: 公司全称
        /// </summary>
        private static Dictionary<string, string> ReadAShareList(string filePath)
        {
            var result = new Dictionary<string, string>();
            dynamic excel = null;
            dynamic workbook = null;

            try
            {
                Type excelType = Type.GetTypeFromProgID("Excel.Application");
                if (excelType == null)
                {
                    Console.WriteLine("  错误: 未安装Excel");
                    return result;
                }

                excel = Activator.CreateInstance(excelType);
                excel.Visible = false;
                excel.DisplayAlerts = false;

                workbook = excel.Workbooks.Open(filePath);
                dynamic sheet = workbook.Sheets[1];

                int rowCount = sheet.UsedRange.Rows.Count;
                Console.WriteLine($"  总行数: {rowCount}");

                for (int row = 2; row <= rowCount; row++) // 跳过标题行
                {
                    string code = Convert.ToString(sheet.Cells[row, 5].Value); // 第5列: A股代码
                    string fullName = Convert.ToString(sheet.Cells[row, 2].Value); // 第2列: 公司全称

                    if (!string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(fullName))
                    {
                        code = code.Trim().PadLeft(6, '0');
                        if (code.Length == 6 && IsDigitsOnly(code))
                        {
                            // 公司全称通常太长，尝试提取简称
                            string shortName = ExtractShortName(fullName);
                            result[code] = shortName;
                        }
                    }
                }

                workbook.Close(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  读取Excel失败: {ex.Message}");
            }
            finally
            {
                if (workbook != null)
                {
                    try { workbook.Close(false); } catch { }
                    Marshal.ReleaseComObject(workbook);
                }
                if (excel != null)
                {
                    try { excel.Quit(); } catch { }
                    Marshal.ReleaseComObject(excel);
                }
            }

            return result;
        }

        /// <summary>
        /// 从公司全称提取简称
        /// </summary>
        private static string ExtractShortName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
                return fullName;

            // 移除常见后缀
            string name = fullName
                .Replace("股份有限公司", "")
                .Replace("有限公司", "")
                .Replace("集团", "")
                .Replace("(集团)", "")
                .Replace("（集团）", "")
                .Replace("Co., Ltd.", "")
                .Replace("Co.,Ltd.", "")
                .Replace("Ltd.", "")
                .Trim();

            // 如果太长，截取前4个中文字符
            if (name.Length > 8)
            {
                int chineseCount = 0;
                int endIndex = 0;
                foreach (char c in name)
                {
                    endIndex++;
                    if (c >= 0x4e00 && c <= 0x9fff)
                    {
                        chineseCount++;
                        if (chineseCount >= 4)
                            break;
                    }
                }
                name = name.Substring(0, Math.Min(endIndex, name.Length));
            }

            return name;
        }

        private static bool IsDigitsOnly(string str)
        {
            foreach (char c in str)
            {
                if (c < '0' || c > '9')
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 更新数据库
        /// </summary>
        private static int UpdateDatabase(Dictionary<string, string> stocks)
        {
            int updated = 0;

            using (var conn = new NpgsqlConnection(connectionString))
            {
                conn.Open();

                string sql = @"
                    UPDATE stock_info
                    SET stock_name = @name, update_time = CURRENT_TIMESTAMP
                    WHERE stock_code = @code
                      AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code)";

                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        foreach (var kv in stocks)
                        {
                            using (var cmd = new NpgsqlCommand(sql, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@code", kv.Key);
                                cmd.Parameters.AddWithValue("@name", kv.Value);
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
