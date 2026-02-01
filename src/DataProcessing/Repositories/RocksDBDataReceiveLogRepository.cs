using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MQReceiver.Repositories;

namespace MQReceiver.DataProcessing.Repositories
{
    /// <summary>
    /// RocksDB 数据接收日志仓储实现
    /// </summary>
    public class RocksDBDataReceiveLogRepository : IDataReceiveLogRepository
    {
        private readonly string _dbPath;
        private readonly object _fileLock = new object();
        private long _nextId = 1;

        public RocksDBDataReceiveLogRepository(string dbPath = "data/rocksdb")
        {
            _dbPath = dbPath;
            Initialize();
        }

        private bool Initialize()
        {
            try
            {
                var logsDir = Path.Combine(_dbPath, "logs");
                if (!Directory.Exists(logsDir))
                {
                    Directory.CreateDirectory(logsDir);
                }

                // 加载最大 ID
                var logs = LoadAllLogs();
                if (logs.Count > 0)
                {
                    _nextId = logs.Max(l => l.Id) + 1;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] DataReceiveLog 初始化失败: {ex.Message}");
                return false;
            }
        }

        public int AddLog(DataReceiveLog log)
        {
            if (log == null)
                return 0;

            try
            {
                log.Id = _nextId++;
                if (log.ReceiveTime == default(DateTime))
                {
                    log.ReceiveTime = DateTime.Now;
                }

                var logs = LoadAllLogs();
                logs.Add(log);

                // 只保留最近的 10000 条日志
                if (logs.Count > 10000)
                {
                    logs = logs.OrderByDescending(l => l.ReceiveTime).Take(10000).ToList();
                }

                SaveAllLogs(logs);
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] AddLog 失败: {ex.Message}");
                return 0;
            }
        }

        public List<DataReceiveLog> GetRecentLogs(int limit = 100)
        {
            try
            {
                var logs = LoadAllLogs();
                return logs
                    .OrderByDescending(l => l.ReceiveTime)
                    .Take(limit)
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] GetRecentLogs 失败: {ex.Message}");
                return new List<DataReceiveLog>();
            }
        }

        public List<DataReceiveLog> GetLogsByTimeRange(DateTime startTime, DateTime endTime)
        {
            try
            {
                var logs = LoadAllLogs();
                return logs
                    .Where(l => l.ReceiveTime >= startTime && l.ReceiveTime <= endTime)
                    .OrderByDescending(l => l.ReceiveTime)
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] GetLogsByTimeRange 失败: {ex.Message}");
                return new List<DataReceiveLog>();
            }
        }

        public List<DataReceiveLog> GetFailedLogs(int limit = 100)
        {
            try
            {
                var logs = LoadAllLogs();
                return logs
                    .Where(l => l.Status == "failed")
                    .OrderByDescending(l => l.ReceiveTime)
                    .Take(limit)
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] GetFailedLogs 失败: {ex.Message}");
                return new List<DataReceiveLog>();
            }
        }

        public int DeleteOldLogs(DateTime beforeDate)
        {
            try
            {
                var logs = LoadAllLogs();
                var toDelete = logs.Where(l => l.ReceiveTime < beforeDate).ToList();

                foreach (var log in toDelete)
                {
                    logs.Remove(log);
                }

                if (toDelete.Count > 0)
                {
                    SaveAllLogs(logs);
                }

                return toDelete.Count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] DeleteOldLogs 失败: {ex.Message}");
                return 0;
            }
        }

        private List<DataReceiveLog> LoadAllLogs()
        {
            var fileName = Path.Combine(_dbPath, "logs", "receive_logs.json");
            if (!File.Exists(fileName))
            {
                return new List<DataReceiveLog>();
            }

            try
            {
                string jsonContent;
                lock (_fileLock)
                {
                    jsonContent = File.ReadAllText(fileName);
                }

                var logs = JsonSerializer.Deserialize<List<DataReceiveLog>>(jsonContent);
                return logs ?? new List<DataReceiveLog>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] LoadAllLogs 失败: {ex.Message}");
                return new List<DataReceiveLog>();
            }
        }

        private void SaveAllLogs(List<DataReceiveLog> logs)
        {
            var fileName = Path.Combine(_dbPath, "logs", "receive_logs.json");

            try
            {
                string jsonContent = JsonSerializer.Serialize(logs);
                lock (_fileLock)
                {
                    File.WriteAllText(fileName, jsonContent);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] SaveAllLogs 失败: {ex.Message}");
                throw;
            }
        }
    }
}
