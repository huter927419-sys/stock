using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MQReceiver.Core.Cache;
using MQReceiver.DataProcessing.Cache;
using MQReceiver.Repositories;

namespace MQReceiver.DataProcessing.Repositories
{
    /// <summary>
    /// 基于文件系统的高性能键值存储（替代 RocksDB）
    /// 提供类似 RocksDB 的接口，但使用本地文件系统实现
    /// </summary>
    public class FileBasedKlineDataRepository : IRocksDBKlineDataRepository
    {
        private readonly string _dbPath;
        private readonly KlineDataMemoryCache _memoryCache;
        private readonly object _fileLock = new object();

        public FileBasedKlineDataRepository(string dbPath = "data/rocksdb")
        {
            _dbPath = dbPath;
            _memoryCache = new KlineDataMemoryCache();
            Initialize();
        }

        public bool Initialize()
        {
            try
            {
                if (!Directory.Exists(_dbPath))
                {
                    Directory.CreateDirectory(_dbPath);
                    Console.WriteLine($"[RocksDB] 数据库目录创建成功: {_dbPath}");
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] 初始化失败: {ex.Message}");
                return false;
            }
        }

        public void Close()
        {
            _memoryCache.Clear();
        }

        public bool Backup(string backupPath)
        {
            try
            {
                if (!Directory.Exists(_dbPath))
                    return false;

                if (!Directory.Exists(backupPath))
                    Directory.CreateDirectory(backupPath);

                foreach (var file in Directory.GetFiles(_dbPath))
                {
                    string destFile = Path.Combine(backupPath, Path.GetFileName(file));
                    File.Copy(file, destFile, true);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] 备份失败: {ex.Message}");
                return false;
            }
        }

        public bool Restore(string backupPath)
        {
            try
            {
                if (!Directory.Exists(backupPath))
                    return false;

                if (!Directory.Exists(_dbPath))
                    Directory.CreateDirectory(_dbPath);

                foreach (var file in Directory.GetFiles(backupPath))
                {
                    string destFile = Path.Combine(_dbPath, Path.GetFileName(file));
                    File.Copy(file, destFile, true);
                }

                _memoryCache.Clear();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] 恢复失败: {ex.Message}");
                return false;
            }
        }

        public bool Compact()
        {
            try
            {
                // 清理空文件和过期数据
                var files = Directory.GetFiles(_dbPath);
                foreach (var file in files)
                {
                    try
                    {
                        if (new FileInfo(file).Length == 0)
                        {
                            File.Delete(file);
                        }
                    }
                    catch
                    {
                        // 忽略无法删除的文件
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] 压缩失败: {ex.Message}");
                return false;
            }
        }

        public Dictionary<string, string> GetStatistics()
        {
            var stats = new Dictionary<string, string>();
            try
            {
                if (Directory.Exists(_dbPath))
                {
                    var files = Directory.GetFiles(_dbPath);
                    stats["TotalFiles"] = files.Length.ToString();
                    stats["TotalSize"] = files.Sum(f => new FileInfo(f).Length).ToString();
                    stats["MemoryCacheSize"] = _memoryCache.GetCacheSize().ToString();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] 统计信息获取失败: {ex.Message}");
            }
            return stats;
        }

        public List<DailyKlineData> GetDailyData(string stockCode, DateTime startDate, DateTime endDate)
        {
            // 首先尝试从内存缓存获取
            var cachedData = _memoryCache.GetKlineData(stockCode);
            if (cachedData != null && cachedData.Count > 0)
            {
                return FilterDateRange(cachedData, startDate, endDate);
            }

            // 从文件加载
            var fileName = GetFileName(stockCode);
            if (!File.Exists(fileName))
            {
                return new List<DailyKlineData>();
            }

            try
            {
                string jsonContent;
                lock (_fileLock)
                {
                    jsonContent = File.ReadAllText(fileName);
                }

                var allData = JsonSerializer.Deserialize<List<DailyKlineData>>(jsonContent);
                if (allData == null)
                {
                    return new List<DailyKlineData>();
                }

                // 更新内存缓存
                _memoryCache.UpdateKlineData(stockCode, allData);

                return FilterDateRange(allData, startDate, endDate);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] 读取K线数据失败 {stockCode}: {ex.Message}");
                return new List<DailyKlineData>();
            }
        }

        public List<DailyKlineData> GetLatestDailyData(string stockCode, int count)
        {
            var allData = GetDailyData(stockCode, DateTime.MinValue, DateTime.MaxValue);
            return allData.OrderByDescending(d => d.TradeDate).Take(count).OrderBy(d => d.TradeDate).ToList();
        }

        public bool HasData(string stockCode)
        {
            var fileName = GetFileName(stockCode);
            return File.Exists(fileName);
        }

        public (DateTime? StartDate, DateTime? EndDate) GetDataDateRange(string stockCode)
        {
            var data = GetDailyData(stockCode, DateTime.MinValue, DateTime.MaxValue);
            if (data.Count == 0)
            {
                return (null, null);
            }

            var sortedData = data.OrderBy(d => d.TradeDate).ToList();
            return (sortedData.First().TradeDate, sortedData.Last().TradeDate);
        }

        public int UpdateDailyData(List<KlineData> dataList)
        {
            if (dataList == null || dataList.Count == 0)
                return 0;

            var stockGroups = dataList.GroupBy(d => d.StockCode);
            int totalUpdated = 0;

            foreach (var group in stockGroups)
            {
                try
                {
                    var existingData = GetDailyData(group.Key, DateTime.MinValue, DateTime.MaxValue);
                    var updatedData = new List<DailyKlineData>(existingData);

                    foreach (var newItem in group)
                    {
                        var existingIndex = updatedData.FindIndex(d => d.TradeDate == newItem.TradeDate);
                        if (existingIndex >= 0)
                        {
                            updatedData[existingIndex] = new DailyKlineData
                            {
                                TradeDate = newItem.TradeDate,
                                Open = newItem.Open,
                                High = newItem.High,
                                Low = newItem.Low,
                                Close = newItem.Close,
                                Volume = newItem.Volume
                            };
                        }
                        else
                        {
                            updatedData.Add(new DailyKlineData
                            {
                                TradeDate = newItem.TradeDate,
                                Open = newItem.Open,
                                High = newItem.High,
                                Low = newItem.Low,
                                Close = newItem.Close,
                                Volume = newItem.Volume
                            });
                        }
                        totalUpdated++;
                    }

                    // 按日期排序
                    updatedData = updatedData.OrderBy(d => d.TradeDate).ToList();

                    // 保存到文件
                    var fileName = GetFileName(group.Key);
                    string jsonContent = JsonSerializer.Serialize(updatedData);

                    lock (_fileLock)
                    {
                        File.WriteAllText(fileName, jsonContent);
                    }

                    // 更新内存缓存
                    _memoryCache.UpdateKlineData(group.Key, updatedData);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RocksDB] 更新K线数据失败 {group.Key}: {ex.Message}");
                }
            }

            return totalUpdated;
        }

        public (decimal? Amount, decimal? TurnoverRate) GetYesterdayAmountAndTurnoverRate(string stockCode, DateTime tradeDate)
        {
            // 文件系统存储目前不支持获取成交量和换手率
            // 在实际应用中，这些数据应该在存储时一起保存
            return (null, null);
        }

        public Dictionary<string, (decimal? Amount, decimal? TurnoverRate)> GetYesterdayAmountAndTurnoverRateBatch(
            List<string> stockCodes, DateTime tradeDate)
        {
            var result = new Dictionary<string, (decimal? Amount, decimal? TurnoverRate)>();
            foreach (var stockCode in stockCodes)
            {
                result[stockCode] = (null, null);
            }
            return result;
        }

        public Dictionary<string, decimal?> GetYesterdayAmountBatch(List<string> stockCodes, DateTime tradeDate)
        {
            var result = new Dictionary<string, decimal?>();
            foreach (var stockCode in stockCodes)
            {
                result[stockCode] = null;
            }
            return result;
        }


        private List<DailyKlineData> FilterDateRange(List<DailyKlineData> data, DateTime startDate, DateTime endDate)
        {
            return data.Where(d => d.TradeDate >= startDate && d.TradeDate <= endDate).ToList();
        }

        private string GetFileName(string stockCode)
        {
            return Path.Combine(_dbPath, $"{stockCode}.json");
        }
    }
}
