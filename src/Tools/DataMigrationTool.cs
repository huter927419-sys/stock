using System;
using System.Collections.Generic;
using System.Linq;
using MQReceiver.DataProcessing.Repositories;
using MQReceiver.Repositories;
using Npgsql;

namespace MQReceiver.Tools
{
    /// <summary>
    /// 数据迁移工具：从 PostgreSQL 迁移数据到 RocksDB
    /// 支持所有表：stock_daily_data, stock_exrights_data, stock_realtime_data,
    /// stock_info, adjustment_task, data_receive_log
    /// </summary>
    public class DataMigrationTool
    {
        private readonly string _pgConnectionString;
        private readonly PostgresStockDataRepository _pgStockRepo;
        private readonly PostgresExRightsDataRepository _pgExRightsRepo;
        private readonly PostgresRealTimeDataRepository _pgRealTimeRepo;

        private readonly RocksDBStockDataRepository _rocksStockRepo;
        private readonly RocksDBExRightsDataRepository _rocksExRightsRepo;
        private readonly RocksDBRealTimeDataRepository _rocksRealTimeRepo;
        private readonly RocksDBAdjustmentTaskRepository _rocksAdjustmentTaskRepo;
        private readonly RocksDBDataReceiveLogRepository _rocksDataReceiveLogRepo;

        public DataMigrationTool(string pgConnectionString = null, string rocksDbPath = "data/rocksdb")
        {
            // 初始化 PostgreSQL 连接字符串
            _pgConnectionString = pgConnectionString ?? MQReceiver.Helpers.DatabaseConnectionHelper.BuildConnectionString();

            // 初始化 PostgreSQL 仓储
            _pgStockRepo = new PostgresStockDataRepository(_pgConnectionString);
            _pgExRightsRepo = new PostgresExRightsDataRepository(_pgConnectionString);
            _pgRealTimeRepo = new PostgresRealTimeDataRepository(_pgConnectionString);

            // 初始化 RocksDB 仓储
            _rocksStockRepo = new RocksDBStockDataRepository(rocksDbPath);
            _rocksExRightsRepo = new RocksDBExRightsDataRepository(rocksDbPath);
            _rocksRealTimeRepo = new RocksDBRealTimeDataRepository(rocksDbPath);
            _rocksAdjustmentTaskRepo = new RocksDBAdjustmentTaskRepository(rocksDbPath);
            _rocksDataReceiveLogRepo = new RocksDBDataReceiveLogRepository(rocksDbPath);
        }

        /// <summary>
        /// 执行完整迁移（所有表）
        /// </summary>
        public bool MigrateAll(bool skipRealTime = false, bool skipLogs = false)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("数据迁移工具：PostgreSQL -> RocksDB");
            Console.WriteLine("========================================");

            try
            {
                // 1. 测试连接
                Console.WriteLine("\n[步骤 1/7] 测试数据库连接...");
                if (!_pgStockRepo.TestConnection())
                {
                    Console.WriteLine("❌ PostgreSQL 连接失败");
                    return false;
                }
                Console.WriteLine("✓ PostgreSQL 连接成功");

                if (!_rocksStockRepo.TestConnection())
                {
                    Console.WriteLine("❌ RocksDB 初始化失败");
                    return false;
                }
                Console.WriteLine("✓ RocksDB 初始化成功");

                // 2. 迁移股票日线数据
                Console.WriteLine("\n[步骤 2/7] 迁移股票日线数据...");
                if (!MigrateStockData())
                {
                    Console.WriteLine("❌ 股票日线数据迁移失败");
                    return false;
                }

                // 3. 迁移股票基本信息
                Console.WriteLine("\n[步骤 3/7] 迁移股票基本信息...");
                if (!MigrateStockInfo())
                {
                    Console.WriteLine("⚠️ 股票基本信息迁移失败（非关键错误）");
                }

                // 4. 迁移除权数据
                Console.WriteLine("\n[步骤 4/7] 迁移除权数据...");
                if (!MigrateExRightsData())
                {
                    Console.WriteLine("❌ 除权数据迁移失败");
                    return false;
                }

                // 5. 迁移实时数据（可选）
                if (!skipRealTime)
                {
                    Console.WriteLine("\n[步骤 5/7] 迁移实时数据...");
                    if (!MigrateRealTimeData())
                    {
                        Console.WriteLine("⚠️ 实时数据迁移失败（非关键错误）");
                    }
                }
                else
                {
                    Console.WriteLine("\n[步骤 5/7] 跳过实时数据迁移");
                }

                // 6. 迁移复权任务
                Console.WriteLine("\n[步骤 6/7] 迁移复权计算任务...");
                if (!MigrateAdjustmentTasks())
                {
                    Console.WriteLine("⚠️ 复权任务迁移失败（非关键错误）");
                }

                // 7. 迁移日志数据（可选）
                if (!skipLogs)
                {
                    Console.WriteLine("\n[步骤 7/7] 迁移数据接收日志...");
                    if (!MigrateDataReceiveLogs())
                    {
                        Console.WriteLine("⚠️ 日志数据迁移失败（非关键错误）");
                    }
                }
                else
                {
                    Console.WriteLine("\n[步骤 7/7] 跳过日志数据迁移");
                }

                Console.WriteLine("\n========================================");
                Console.WriteLine("✓ 数据迁移完成！");
                Console.WriteLine("========================================");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ 迁移过程中发生错误: {ex.Message}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// 迁移股票日线数据
        /// </summary>
        public bool MigrateStockData()
        {
            try
            {
                // 获取所有股票代码
                var stockCodes = _pgStockRepo.GetAllStockCodes();
                Console.WriteLine($"  发现 {stockCodes.Count} 只股票");

                if (stockCodes.Count == 0)
                {
                    Console.WriteLine("  没有数据需要迁移");
                    return true;
                }

                int totalMigrated = 0;
                int successCount = 0;
                int failCount = 0;

                for (int i = 0; i < stockCodes.Count; i++)
                {
                    var stockCode = stockCodes[i];
                    try
                    {
                        // 获取该股票的所有数据
                        var data = _pgStockRepo.GetDailyData(stockCode, DateTime.MinValue, DateTime.MaxValue);

                        if (data.Count > 0)
                        {
                            // 转换为 DailyDataRecord 格式
                            var records = data.Select(d => new DailyDataRecord
                            {
                                StockCode = stockCode,
                                MarketCode = 0, // 默认值，可根据股票代码推断
                                TradeDate = d.TradeDate,
                                OpenPrice = d.Open,
                                HighPrice = d.High,
                                LowPrice = d.Low,
                                ClosePrice = d.Close,
                                Volume = d.Volume,
                                Amount = d.Amount ?? 0,
                                TurnoverRate = d.TurnoverRate
                            }).ToList();

                            // 保存到 RocksDB
                            int saved = _rocksStockRepo.SaveDailyData(records);
                            totalMigrated += saved;
                            successCount++;

                            if ((i + 1) % 100 == 0 || i == stockCodes.Count - 1)
                            {
                                Console.WriteLine($"  进度: {i + 1}/{stockCodes.Count} ({successCount} 成功, {failCount} 失败, {totalMigrated} 条记录)");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        Console.WriteLine($"  ⚠️ {stockCode} 迁移失败: {ex.Message}");
                    }
                }

                Console.WriteLine($"  ✓ 股票数据迁移完成: {successCount}/{stockCodes.Count} 只股票, 共 {totalMigrated} 条记录");
                return failCount == 0 || successCount > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 股票数据迁移失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 迁移除权数据
        /// </summary>
        public bool MigrateExRightsData()
        {
            try
            {
                // 获取所有股票代码
                var stockCodes = _pgStockRepo.GetAllStockCodes();
                Console.WriteLine($"  检查 {stockCodes.Count} 只股票的除权数据");

                int totalMigrated = 0;
                int successCount = 0;
                int skipCount = 0;

                for (int i = 0; i < stockCodes.Count; i++)
                {
                    var stockCode = stockCodes[i];
                    try
                    {
                        // 检查是否有除权数据
                        if (_pgExRightsRepo.HasExRightsData(stockCode))
                        {
                            var exRightsData = _pgExRightsRepo.GetExRightsData(stockCode);

                            if (exRightsData.Count > 0)
                            {
                                // 保存到 RocksDB
                                int saved = _rocksExRightsRepo.SaveExRightsData(exRightsData);
                                totalMigrated += saved;
                                successCount++;
                            }
                        }
                        else
                        {
                            skipCount++;
                        }

                        if ((i + 1) % 200 == 0 || i == stockCodes.Count - 1)
                        {
                            Console.WriteLine($"  进度: {i + 1}/{stockCodes.Count} ({successCount} 有除权数据, {skipCount} 无除权数据, {totalMigrated} 条记录)");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ⚠️ {stockCode} 除权数据迁移失败: {ex.Message}");
                    }
                }

                Console.WriteLine($"  ✓ 除权数据迁移完成: {successCount} 只股票有除权数据, 共 {totalMigrated} 条记录");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 除权数据迁移失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 迁移实时数据
        /// </summary>
        public bool MigrateRealTimeData()
        {
            try
            {
                var realtimeData = _pgRealTimeRepo.GetAllRealTimeData();
                Console.WriteLine($"  发现 {realtimeData.Count} 条实时数据");

                if (realtimeData.Count > 0)
                {
                    int saved = _rocksRealTimeRepo.SaveRealTimeData(realtimeData);
                    Console.WriteLine($"  ✓ 实时数据迁移完成: {saved} 条记录");
                    return true;
                }
                else
                {
                    Console.WriteLine("  没有实时数据需要迁移");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 实时数据迁移失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 迁移股票基本信息
        /// </summary>
        public bool MigrateStockInfo()
        {
            try
            {
                var stockNames = _pgStockRepo.GetAllStockNames();
                Console.WriteLine($"  发现 {stockNames.Count} 条股票名称");

                if (stockNames.Count > 0)
                {
                    var stockInfoList = stockNames.Select(kv =>
                        (StockCode: kv.Key, StockName: kv.Value, MarketCode: (ushort)0)
                    ).ToList();

                    int saved = _rocksStockRepo.SaveStockInfo(stockInfoList);
                    Console.WriteLine($"  ✓ 股票信息迁移完成: {saved} 条记录");
                    return true;
                }
                else
                {
                    Console.WriteLine("  没有股票信息需要迁移");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 股票信息迁移失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 迁移复权计算任务
        /// </summary>
        public bool MigrateAdjustmentTasks()
        {
            try
            {
                using (var connection = new NpgsqlConnection(_pgConnectionString))
                {
                    connection.Open();

                    string sql = @"
                        SELECT id, stock_code, task_type, trigger_date, status, priority,
                               error_message, retry_count, max_retries, create_time, start_time, complete_time
                        FROM adjustment_task
                        WHERE status != 'completed'
                        ORDER BY create_time DESC
                        LIMIT 1000";

                    using (var cmd = new NpgsqlCommand(sql, connection))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            var tasks = new List<AdjustmentTask>();

                            while (reader.Read())
                            {
                                tasks.Add(new AdjustmentTask
                                {
                                    Id = reader.GetInt64(0),
                                    StockCode = reader.GetString(1),
                                    TaskType = reader.GetString(2),
                                    TriggerDate = reader.GetDateTime(3),
                                    Status = reader.GetString(4),
                                    Priority = reader.GetInt32(5),
                                    ErrorMessage = reader.IsDBNull(6) ? null : reader.GetString(6),
                                    RetryCount = reader.GetInt32(7),
                                    MaxRetries = reader.GetInt32(8),
                                    CreateTime = reader.GetDateTime(9),
                                    StartTime = reader.IsDBNull(10) ? (DateTime?)null : reader.GetDateTime(10),
                                    CompleteTime = reader.IsDBNull(11) ? (DateTime?)null : reader.GetDateTime(11)
                                });
                            }

                            Console.WriteLine($"  发现 {tasks.Count} 个待处理任务");

                            if (tasks.Count > 0)
                            {
                                int saved = _rocksAdjustmentTaskRepo.AddTasks(tasks);
                                Console.WriteLine($"  ✓ 复权任务迁移完成: {saved} 条记录");
                            }
                            else
                            {
                                Console.WriteLine("  没有复权任务需要迁移");
                            }

                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 复权任务迁移失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 迁移数据接收日志
        /// </summary>
        public bool MigrateDataReceiveLogs()
        {
            try
            {
                using (var connection = new NpgsqlConnection(_pgConnectionString))
                {
                    connection.Open();

                    // 只迁移最近30天的日志
                    string sql = @"
                        SELECT id, receive_time, data_type, record_count, queue_name,
                               source_ip, status, error_message, processing_time_ms
                        FROM data_receive_log
                        WHERE receive_time >= NOW() - INTERVAL '30 days'
                        ORDER BY receive_time DESC
                        LIMIT 5000";

                    using (var cmd = new NpgsqlCommand(sql, connection))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            int saved = 0;

                            while (reader.Read())
                            {
                                var log = new DataReceiveLog
                                {
                                    Id = reader.GetInt64(0),
                                    ReceiveTime = reader.GetDateTime(1),
                                    DataType = reader.IsDBNull(2) ? null : reader.GetString(2),
                                    RecordCount = reader.GetInt32(3),
                                    QueueName = reader.IsDBNull(4) ? null : reader.GetString(4),
                                    SourceIp = reader.IsDBNull(5) ? null : reader.GetString(5),
                                    Status = reader.IsDBNull(6) ? "success" : reader.GetString(6),
                                    ErrorMessage = reader.IsDBNull(7) ? null : reader.GetString(7),
                                    ProcessingTimeMs = reader.IsDBNull(8) ? 0 : reader.GetInt32(8)
                                };

                                _rocksDataReceiveLogRepo.AddLog(log);
                                saved++;
                            }

                            Console.WriteLine($"  ✓ 日志数据迁移完成: {saved} 条记录（最近30天）");
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 日志数据迁移失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 验证迁移结果
        /// </summary>
        public void VerifyMigration()
        {
            Console.WriteLine("\n========================================");
            Console.WriteLine("验证迁移结果");
            Console.WriteLine("========================================");

            try
            {
                // 验证股票数据
                var pgStockCodes = _pgStockRepo.GetAllStockCodes();
                var rocksStockCodes = _rocksStockRepo.GetAllStockCodes();

                Console.WriteLine($"\nPostgreSQL 股票数量: {pgStockCodes.Count}");
                Console.WriteLine($"RocksDB 股票数量: {rocksStockCodes.Count}");

                // 验证股票信息
                var pgStockNames = _pgStockRepo.GetAllStockNames();
                var rocksStockNames = _rocksStockRepo.GetAllStockNames();

                Console.WriteLine($"\nPostgreSQL 股票名称数量: {pgStockNames.Count}");
                Console.WriteLine($"RocksDB 股票名称数量: {rocksStockNames.Count}");

                // 随机抽样验证
                if (pgStockCodes.Count > 0)
                {
                    var random = new Random();
                    var sampleCodes = pgStockCodes.OrderBy(x => random.Next()).Take(5).ToList();

                    Console.WriteLine("\n抽样验证 5 只股票:");
                    foreach (var stockCode in sampleCodes)
                    {
                        var pgData = _pgStockRepo.GetLatestDailyData(stockCode, 1);
                        var rocksData = _rocksStockRepo.GetLatestDailyData(stockCode, 1);

                        if (pgData.Count > 0 && rocksData.Count > 0)
                        {
                            var pgLatest = pgData[0];
                            var rocksLatest = rocksData[0];

                            bool match = pgLatest.TradeDate == rocksLatest.TradeDate &&
                                        pgLatest.Close == rocksLatest.Close;

                            string stockName = pgStockNames.ContainsKey(stockCode) ? pgStockNames[stockCode] : stockCode;

                            Console.WriteLine($"  {stockCode} ({stockName}): {(match ? "✓" : "❌")} " +
                                $"PG({pgLatest.TradeDate:yyyy-MM-dd}, {pgLatest.Close:F2}) " +
                                $"vs RocksDB({rocksLatest.TradeDate:yyyy-MM-dd}, {rocksLatest.Close:F2})");
                        }
                        else
                        {
                            Console.WriteLine($"  {stockCode}: ⚠️ 数据不完整");
                        }
                    }
                }

                Console.WriteLine("\n========================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"验证失败: {ex.Message}");
            }
        }
    }
}
