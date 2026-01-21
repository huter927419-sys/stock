using System;
using System.Collections.Generic;
using MQReceiver.Helpers;
using MQReceiver.Repositories;
using Npgsql;

namespace MQReceiver
{
    /// <summary>
    /// 检查除权数据和复权价格的工具程序
    /// </summary>
    class CheckExRightsData
    {
        static void Main(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("  除权数据和复权价格检查工具");
            Console.WriteLine("========================================");
            Console.WriteLine();

            var connectionString = DatabaseConnectionHelper.BuildConnectionString();

            try
            {
                using (var conn = new NpgsqlConnection(connectionString))
                {
                    conn.Open();
                    Console.WriteLine("✓ 数据库连接成功");
                    Console.WriteLine();

                    // 1. 检查除权数据表
                    CheckExRightsTable(conn);

                    // 2. 检查复权价格字段
                    CheckAdjustedPriceFields(conn);

                    // 3. 检查是否需要计算复权价格
                    CheckNeedCalculateAdjustment(conn);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ 错误: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine();
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
        }

        static void CheckExRightsTable(NpgsqlConnection conn)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("1. 检查除权数据表 (stock_exrights_data)");
            Console.WriteLine("========================================");

            try
            {
                // 检查表是否存在
                string checkTableSql = @"
                    SELECT EXISTS (
                        SELECT FROM information_schema.tables
                        WHERE table_name = 'stock_exrights_data'
                    )";

                using (var cmd = new NpgsqlCommand(checkTableSql, conn))
                {
                    bool tableExists = (bool)cmd.ExecuteScalar();
                    if (!tableExists)
                    {
                        Console.WriteLine("✗ 除权数据表不存在");
                        Console.WriteLine("  请运行: db/create_exrights_data_table_postgresql.sql");
                        Console.WriteLine();
                        return;
                    }
                }

                Console.WriteLine("✓ 除权数据表存在");

                // 统计除权数据
                string statsSql = @"
                    SELECT
                        COUNT(*) as total_count,
                        COUNT(DISTINCT stock_code) as stock_count,
                        MIN(ex_rights_date) as earliest_date,
                        MAX(ex_rights_date) as latest_date
                    FROM stock_exrights_data";

                using (var cmd = new NpgsqlCommand(statsSql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        long totalCount = reader.GetInt64(0);
                        long stockCount = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);

                        Console.WriteLine($"  总记录数: {totalCount:N0}");
                        Console.WriteLine($"  股票数量: {stockCount:N0}");

                        if (!reader.IsDBNull(2))
                        {
                            DateTime earliest = reader.GetDateTime(2);
                            DateTime latest = reader.GetDateTime(3);
                            Console.WriteLine($"  日期范围: {earliest:yyyy-MM-dd} ~ {latest:yyyy-MM-dd}");
                        }

                        if (totalCount == 0)
                        {
                            Console.WriteLine();
                            Console.WriteLine("⚠ 警告: 除权数据表为空！");
                            Console.WriteLine("  需要先导入除权数据才能计算复权价格。");
                        }
                        else
                        {
                            Console.WriteLine($"✓ 除权数据已填充 ({totalCount:N0} 条记录)");
                        }
                    }
                }

                // 显示最近的除权数据示例
                if (true)
                {
                    Console.WriteLine();
                    Console.WriteLine("最近10条除权数据:");
                    string sampleSql = @"
                        SELECT
                            stock_code, ex_rights_date,
                            give_per_10_shares, pei_per_10_shares,
                            pei_price, profit_per_share
                        FROM stock_exrights_data
                        ORDER BY ex_rights_date DESC
                        LIMIT 10";

                    using (var cmd = new NpgsqlCommand(sampleSql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        int count = 0;
                        while (reader.Read())
                        {
                            count++;
                            string stockCode = reader.GetString(0);
                            DateTime exDate = reader.GetDateTime(1);
                            decimal give = reader.GetDecimal(2);
                            decimal pei = reader.GetDecimal(3);
                            decimal peiPrice = reader.GetDecimal(4);
                            decimal profit = reader.GetDecimal(5);

                            string scheme = "";
                            if (give > 0) scheme += $"10送{give}";
                            if (pei > 0) scheme += $" 10配{pei}@{peiPrice}";
                            if (profit > 0) scheme += $" 派{profit}";

                            Console.WriteLine($"  {stockCode} {exDate:yyyy-MM-dd} {scheme}");
                        }
                        if (count == 0)
                        {
                            Console.WriteLine("  (无数据)");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ 检查除权数据表失败: {ex.Message}");
            }

            Console.WriteLine();
        }

        static void CheckAdjustedPriceFields(NpgsqlConnection conn)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("2. 检查复权价格字段 (stock_daily_data)");
            Console.WriteLine("========================================");

            try
            {
                // 检查字段是否存在
                string checkColumnSql = @"
                    SELECT
                        EXISTS (
                            SELECT FROM information_schema.columns
                            WHERE table_name = 'stock_daily_data'
                            AND column_name = 'adjusted_close_price'
                        ) as has_column";

                using (var cmd = new NpgsqlCommand(checkColumnSql, conn))
                {
                    bool hasColumn = (bool)cmd.ExecuteScalar();
                    if (!hasColumn)
                    {
                        Console.WriteLine("✗ 复权价格字段不存在");
                        Console.WriteLine("  请运行: db/add_adjusted_price_columns.sql");
                        Console.WriteLine();
                        return;
                    }
                }

                Console.WriteLine("✓ 复权价格字段存在");

                // 统计复权价格数据
                string statsSql = @"
                    SELECT
                        COUNT(*) as total_count,
                        COUNT(adjusted_close_price) as adjusted_count,
                        COUNT(*) - COUNT(adjusted_close_price) as unadjusted_count
                    FROM stock_daily_data";

                using (var cmd = new NpgsqlCommand(statsSql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        long totalCount = reader.GetInt64(0);
                        long adjustedCount = reader.GetInt64(1);
                        long unadjustedCount = reader.GetInt64(2);
                        double coverage = totalCount > 0 ? (adjustedCount * 100.0 / totalCount) : 0;

                        Console.WriteLine($"  总记录数: {totalCount:N0}");
                        Console.WriteLine($"  有复权价: {adjustedCount:N0}");
                        Console.WriteLine($"  无复权价: {unadjustedCount:N0}");
                        Console.WriteLine($"  覆盖率: {coverage:F2}%");

                        if (adjustedCount == 0)
                        {
                            Console.WriteLine();
                            Console.WriteLine("⚠ 警告: 尚未计算复权价格！");
                            Console.WriteLine("  需要运行复权价格计算程序。");
                        }
                        else if (coverage < 100)
                        {
                            Console.WriteLine();
                            Console.WriteLine($"⚠ 提示: 复权价格未完全覆盖 ({coverage:F2}%)");
                        }
                        else
                        {
                            Console.WriteLine("✓ 复权价格已完全计算");
                        }
                    }
                }

                // 显示复权价格示例
                Console.WriteLine();
                Console.WriteLine("复权价格示例（前5条）:");
                string sampleSql = @"
                    SELECT
                        stock_code, trade_date,
                        close_price, adjusted_close_price
                    FROM stock_daily_data
                    WHERE adjusted_close_price IS NOT NULL
                    ORDER BY trade_date DESC
                    LIMIT 5";

                using (var cmd = new NpgsqlCommand(sampleSql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    int count = 0;
                    while (reader.Read())
                    {
                        count++;
                        string stockCode = reader.GetString(0);
                        DateTime tradeDate = reader.GetDateTime(1);
                        decimal closePrice = reader.GetDecimal(2);
                        decimal adjPrice = reader.GetDecimal(3);
                        double diff = (double)((adjPrice - closePrice) / closePrice * 100);

                        Console.WriteLine($"  {stockCode} {tradeDate:yyyy-MM-dd} 原价:{closePrice:F2} 复权:{adjPrice:F2} 差异:{diff:+0.00;-0.00}%");
                    }
                    if (count == 0)
                    {
                        Console.WriteLine("  (无复权数据)");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ 检查复权价格字段失败: {ex.Message}");
            }

            Console.WriteLine();
        }

        static void CheckNeedCalculateAdjustment(NpgsqlConnection conn)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("3. 建议");
            Console.WriteLine("========================================");

            try
            {
                // 检查除权数据和复权价格状态
                string checkSql = @"
                    SELECT
                        (SELECT COUNT(*) FROM stock_exrights_data) as exrights_count,
                        (SELECT COUNT(adjusted_close_price) FROM stock_daily_data) as adjusted_count";

                using (var cmd = new NpgsqlCommand(checkSql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        long exRightsCount = reader.GetInt64(0);
                        long adjustedCount = reader.GetInt64(1);

                        if (exRightsCount > 0 && adjustedCount == 0)
                        {
                            Console.WriteLine("📋 下一步操作:");
                            Console.WriteLine("  1. 除权数据已有，但复权价格未计算");
                            Console.WriteLine("  2. 建议运行复权价格计算程序");
                            Console.WriteLine("  3. 计算完成后，修改 PostgresKlineDataRepository.cs");
                            Console.WriteLine("     将 open_price 改为 COALESCE(adjusted_open_price, open_price)");
                        }
                        else if (exRightsCount == 0)
                        {
                            Console.WriteLine("📋 下一步操作:");
                            Console.WriteLine("  1. 需要先导入除权数据到 stock_exrights_data 表");
                            Console.WriteLine("  2. 然后运行复权价格计算程序");
                            Console.WriteLine("  3. 最后修改代码启用复权数据");
                        }
                        else if (adjustedCount > 0)
                        {
                            Console.WriteLine("✓ 复权数据完备，可以启用复权价格用于KD计算");
                            Console.WriteLine();
                            Console.WriteLine("📋 启用方法:");
                            Console.WriteLine("  修改 PostgresKlineDataRepository.cs 第42行和第94行:");
                            Console.WriteLine("  FROM:");
                            Console.WriteLine("    SELECT trade_date, open_price, high_price, ...");
                            Console.WriteLine("  TO:");
                            Console.WriteLine("    SELECT trade_date,");
                            Console.WriteLine("           COALESCE(adjusted_open_price, open_price) as open_price,");
                            Console.WriteLine("           COALESCE(adjusted_high_price, high_price) as high_price,");
                            Console.WriteLine("           ...");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ 检查失败: {ex.Message}");
            }

            Console.WriteLine();
        }
    }
}
