using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MQReceiver.Repositories;

namespace MQReceiver.DataProcessing.Repositories
{
    /// <summary>
    /// RocksDB（文件系统模拟）实时数据仓储实现
    /// </summary>
    public class RocksDBRealTimeDataRepository : IRealTimeDataRepository
    {
        private readonly string _dbPath;
        private readonly object _fileLock = new object();
        private readonly Dictionary<string, RealTimeDataRecord> _memoryCache;

        public RocksDBRealTimeDataRepository(string dbPath = "data/rocksdb")
        {
            _dbPath = dbPath;
            _memoryCache = new Dictionary<string, RealTimeDataRecord>();
            Initialize();
        }

        private bool Initialize()
        {
            try
            {
                var realtimeDir = Path.Combine(_dbPath, "realtime");
                if (!Directory.Exists(realtimeDir))
                {
                    Directory.CreateDirectory(realtimeDir);
                    Console.WriteLine($"[RocksDB] 实时数据目录创建成功: {realtimeDir}");
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] 实时数据目录初始化失败: {ex.Message}");
                return false;
            }
        }

        public bool TestConnection()
        {
            try
            {
                var realtimeDir = Path.Combine(_dbPath, "realtime");
                return Directory.Exists(realtimeDir);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 保存实时数据。按股票代码整条覆盖，同一代码不会重复。
        /// </summary>
        public int SaveRealTimeData(List<RealTimeDataRecord> records)
        {
            if (records == null || records.Count == 0)
                return 0;

            int totalSaved = 0;

            try
            {
                foreach (var record in records)
                {
                    try
                    {
                        // 按股票代码覆盖，不重复
                        _memoryCache[record.StockCode] = record;

                        var fileName = GetRealTimeFileName(record.StockCode);
                        string jsonContent = JsonSerializer.Serialize(record);

                        lock (_fileLock)
                        {
                            File.WriteAllText(fileName, jsonContent);
                        }

                        totalSaved++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[RocksDB] 保存实时数据失败 {record.StockCode}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] 批量保存实时数据失败: {ex.Message}");
            }

            return totalSaved;
        }

        public RealTimeDataRecord GetRealTimeData(string stockCode)
        {
            // 先尝试从内存缓存获取
            if (_memoryCache.TryGetValue(stockCode, out var cachedRecord))
            {
                return cachedRecord;
            }

            // 从文件加载
            var fileName = GetRealTimeFileName(stockCode);
            if (!File.Exists(fileName))
            {
                return null;
            }

            try
            {
                string jsonContent;
                lock (_fileLock)
                {
                    jsonContent = File.ReadAllText(fileName);
                }

                var record = JsonSerializer.Deserialize<RealTimeDataRecord>(jsonContent);

                // 更新内存缓存
                if (record != null)
                {
                    _memoryCache[stockCode] = record;
                }

                return record;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] 读取实时数据失败 {stockCode}: {ex.Message}");
                return null;
            }
        }

        public List<RealTimeDataRecord> GetAllRealTimeData()
        {
            var result = new List<RealTimeDataRecord>();

            try
            {
                var realtimeDir = Path.Combine(_dbPath, "realtime");
                if (!Directory.Exists(realtimeDir))
                    return result;

                var files = Directory.GetFiles(realtimeDir, "*.json");
                foreach (var file in files)
                {
                    try
                    {
                        string jsonContent;
                        lock (_fileLock)
                        {
                            jsonContent = File.ReadAllText(file);
                        }

                        var record = JsonSerializer.Deserialize<RealTimeDataRecord>(jsonContent);
                        if (record != null)
                        {
                            result.Add(record);

                            // 更新内存缓存
                            _memoryCache[record.StockCode] = record;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[RocksDB] 读取实时数据文件失败 {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] 获取所有实时数据失败: {ex.Message}");
            }

            return result;
        }

        private string GetRealTimeFileName(string stockCode)
        {
            return Path.Combine(_dbPath, "realtime", $"{stockCode}.json");
        }
    }
}
