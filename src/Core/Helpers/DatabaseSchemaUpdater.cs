using System;
using System.Collections.Generic;
using Npgsql;
using MQReceiver.Configuration;

namespace MQReceiver.Helpers
{
    /// <summary>
    /// 数据库架构更新器
    /// 负责自动检测并更新数据库表结构，确保字段与最新版本代码一致
    /// </summary>
    public static class DatabaseSchemaUpdater
    {
        /// <summary>
        /// 检查并更新所有表的字段结构
        /// </summary>
        public static void UpdateSchema()
        {
            try
            {
                Console.WriteLine("正在检查并更新数据库表结构...");

                string connectionString = DatabaseConnectionHelper.BuildConnectionString();

                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    // 更新日线数据表
                    UpdateDailyDataTable(connection);

                    // 更新实时数据表
                    UpdateRealTimeDataTable(connection);

                    // 更新除权数据表
                    UpdateExRightsDataTable(connection);

                    // 更新股票信息表
                    UpdateStockInfoTable(connection);

                    Console.WriteLine("数据库表结构更新完成");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"数据库架构更新失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 更新日线数据表结构
        /// </summary>
        private static void UpdateDailyDataTable(NpgsqlConnection connection)
        {
            var columnsToAdd = new Dictionary<string, string>
            {
                { "turnover_rate", "NUMERIC(8,4)" },  // 换手率（%）
            };

            foreach (var column in columnsToAdd)
            {
                EnsureColumnExists(connection, "stock_daily_data", column.Key, column.Value);
            }

            // 检查并更新字段类型（如果需要）
            // 例如：如果 volume 或 amount 的精度不够，可以在这里更新
        }

        /// <summary>
        /// 更新实时数据表结构
        /// </summary>
        private static void UpdateRealTimeDataTable(NpgsqlConnection connection)
        {
            // 实时数据表目前没有需要添加的字段
            // 如果需要，可以在这里添加
        }

        /// <summary>
        /// 更新除权数据表结构
        /// </summary>
        private static void UpdateExRightsDataTable(NpgsqlConnection connection)
        {
            // 除权数据表目前没有需要添加的字段
            // 如果需要，可以在这里添加
        }

        /// <summary>
        /// 更新股票信息表结构
        /// </summary>
        private static void UpdateStockInfoTable(NpgsqlConnection connection)
        {
            // 股票信息表目前没有需要添加的字段
            // 如果需要，可以在这里添加
        }

        /// <summary>
        /// 确保列存在，如果不存在则添加
        /// </summary>
        private static void EnsureColumnExists(NpgsqlConnection connection, string tableName, string columnName, string columnType)
        {
            try
            {
                // 检查列是否存在
                string checkSql = @"
                    SELECT EXISTS (
                        SELECT FROM information_schema.columns
                        WHERE table_schema = 'public'
                        AND table_name = @tableName
                        AND column_name = @columnName
                    );
                ";

                bool columnExists;
                using (var command = new NpgsqlCommand(checkSql, connection))
                {
                    command.Parameters.AddWithValue("@tableName", tableName);
                    command.Parameters.AddWithValue("@columnName", columnName);
                    columnExists = (bool)command.ExecuteScalar();
                }

                if (!columnExists)
                {
                    // 添加列
                    string addColumnSql = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnType};";
                    using (var command = new NpgsqlCommand(addColumnSql, connection))
                    {
                        command.ExecuteNonQuery();
                        Console.WriteLine($"  ✓ 已添加列: {tableName}.{columnName}");
                    }
                }
                else
                {
                    Console.WriteLine($"  ✓ 列已存在: {tableName}.{columnName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ 检查/添加列 {tableName}.{columnName} 失败: {ex.Message}");
                // 不抛出异常，继续处理其他列
            }
        }

        /// <summary>
        /// 检查列是否存在
        /// </summary>
        public static bool ColumnExists(string tableName, string columnName)
        {
            try
            {
                string connectionString = DatabaseConnectionHelper.BuildConnectionString();

                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    string sql = @"
                        SELECT EXISTS (
                            SELECT FROM information_schema.columns
                            WHERE table_schema = 'public'
                            AND table_name = @tableName
                            AND column_name = @columnName
                        );
                    ";

                    using (var command = new NpgsqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@tableName", tableName);
                        command.Parameters.AddWithValue("@columnName", columnName);
                        return (bool)command.ExecuteScalar();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"检查列是否存在时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取表的所有列信息
        /// </summary>
        public static List<ColumnInfo> GetTableColumns(string tableName)
        {
            var columns = new List<ColumnInfo>();

            try
            {
                string connectionString = DatabaseConnectionHelper.BuildConnectionString();

                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    string sql = @"
                        SELECT 
                            column_name,
                            data_type,
                            is_nullable,
                            column_default
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                        AND table_name = @tableName
                        ORDER BY ordinal_position;
                    ";

                    using (var command = new NpgsqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@tableName", tableName);
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                columns.Add(new ColumnInfo
                                {
                                    Name = reader.GetString(0),
                                    DataType = reader.GetString(1),
                                    IsNullable = reader.GetString(2) == "YES",
                                    DefaultValue = reader.IsDBNull(3) ? null : reader.GetString(3)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取表列信息时出错: {ex.Message}");
            }

            return columns;
        }

        /// <summary>
        /// 列信息
        /// </summary>
        public class ColumnInfo
        {
            public string Name { get; set; }
            public string DataType { get; set; }
            public bool IsNullable { get; set; }
            public string DefaultValue { get; set; }
        }
    }
}
