using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MQReceiver.Repositories;

namespace MQReceiver.DataProcessing.Repositories
{
    /// <summary>
    /// RocksDB（文件系统模拟）股票数据仓储实现
    /// 提供与 PostgreSQL 相同的接口，但使用文件系统存储
    /// </summary>
    public class RocksDBStockDataRepository : IStockDataRepository, IKlineDataRepository
    {
        private readonly string _dbPath;
        private readonly ConcurrentDictionary<string, List<DailyKlineData>> _memoryCache;
        /// <summary>按股票代码加锁，不同股票可并行读，加快预加载与批量KD</summary>
        private readonly ConcurrentDictionary<string, object> _fileLocks = new ConcurrentDictionary<string, object>(StringComparer.Ordinal);
        /// <summary>metadata/stock_names.json 单文件写锁</summary>
        private static readonly object _metadataLock = new object();

        public RocksDBStockDataRepository(string dbPath = "data/rocksdb")
        {
            _dbPath = ResolvePath(string.IsNullOrWhiteSpace(dbPath) ? "data/rocksdb" : dbPath.Trim());
            _memoryCache = new ConcurrentDictionary<string, List<DailyKlineData>>();
            Initialize();
        }

        /// <summary>
        /// 将相对路径解析为基于程序基目录的绝对路径，避免工作目录不同导致 IOException
        /// </summary>
        private static string ResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;
            if (Path.IsPathRooted(path)) return path;
            string baseDir = AppDomain.CurrentDomain.BaseDirectory ?? ".";
            try
            {
                return Path.GetFullPath(Path.Combine(baseDir, path));
            }
            catch (IOException ex)
            {
                Console.WriteLine($"[RocksDB] 路径解析失败 BaseDirectory={baseDir} path={path}: {ex.Message}");
                throw;
            }
        }

        private bool Initialize()
        {
            try
            {
                if (!Directory.Exists(_dbPath))
                {
                    Directory.CreateDirectory(_dbPath);
                    Console.WriteLine($"[RocksDB] 数据库目录创建成功: {_dbPath}");
                }

                var subdirs = new[] { "kline", "stock_info", "metadata" };
                foreach (var subdir in subdirs)
                {
                    var path = Path.Combine(_dbPath, subdir);
                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }
                }

                return true;
            }
            catch (IOException ex)
            {
                Console.WriteLine($"[RocksDB] 初始化失败(IO): {ex.Message}");
                Console.WriteLine($"[RocksDB] 路径: {_dbPath}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] 初始化失败: {ex.Message}");
                return false;
            }
        }

        #region IStockDataRepository 实现

        public bool TestConnection()
        {
            try
            {
                return Directory.Exists(_dbPath);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 保存日线数据。按 (股票代码, 交易日期) 与已有数据合并后覆盖写入，同一股票同一日期不会重复。
        /// </summary>
        public int SaveDailyData(List<DailyDataRecord> records)
        {
            if (records == null || records.Count == 0)
                return 0;

            int totalSaved = 0;
            var stockGroups = records.GroupBy(r => r.StockCode);

            foreach (var group in stockGroups)
            {
                try
                {
                    var stockCode = group.Key;
                    var existingData = GetAllDailyDataForStock(stockCode);

                    // 按交易日期建字典，同日期覆盖，保证不重复
                    var dataDict = existingData.ToDictionary(d => d.TradeDate);

                    foreach (var record in group)
                    {
                        var klineData = new DailyKlineData
                        {
                            TradeDate = record.TradeDate,
                            Open = record.OpenPrice,
                            High = record.HighPrice,
                            Low = record.LowPrice,
                            Close = record.ClosePrice,
                            Volume = record.Volume,
                            Amount = record.Amount,
                            TurnoverRate = record.TurnoverRate
                        };

                        dataDict[record.TradeDate] = klineData;
                        totalSaved++;
                    }

                    // 保存到文件
                    var sortedData = dataDict.Values.OrderBy(d => d.TradeDate).ToList();
                    SaveStockData(stockCode, sortedData);

                    // 更新内存缓存
                    _memoryCache[stockCode] = new List<DailyKlineData>(sortedData);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RocksDB] 保存股票 {group.Key} 数据失败: {ex.Message}");
                }
            }

            return totalSaved;
        }

        public List<DailyKlineData> GetDailyData(string stockCode, DateTime startDate, DateTime endDate)
        {
            // 首先尝试从内存缓存获取
            if (_memoryCache.TryGetValue(stockCode, out var cachedData) && cachedData != null && cachedData.Count > 0)
            {
                return FilterDateRange(cachedData, startDate, endDate);
            }

            // 从文件加载
            var allData = GetAllDailyDataForStock(stockCode);

            // 更新内存缓存
            if (allData.Count > 0)
            {
                _memoryCache[stockCode] = new List<DailyKlineData>(allData);
            }

            return FilterDateRange(allData, startDate, endDate);
        }

        public List<DailyKlineData> GetLatestDailyData(string stockCode, int count)
        {
            if (_memoryCache.TryGetValue(stockCode, out var cached) && cached != null && cached.Count > 0)
            {
                var sorted = cached.OrderBy(d => d.TradeDate).ToList();
                return sorted.Skip(Math.Max(0, sorted.Count - count)).ToList();
            }
            var allData = GetAllDailyDataForStock(stockCode);
            return allData.OrderByDescending(d => d.TradeDate).Take(count).OrderBy(d => d.TradeDate).ToList();
        }

        /// <summary>
        /// 获取指定股票的最早 N 条日线数据（用于查看历史最长股票的最初数据）
        /// </summary>
        public List<DailyKlineData> GetEarliestDailyData(string stockCode, int count)
        {
            if (_memoryCache.TryGetValue(stockCode, out var cached) && cached != null && cached.Count > 0)
            {
                var sorted = cached.OrderBy(d => d.TradeDate).ToList();
                return sorted.Take(count).ToList();
            }
            var allData = GetAllDailyDataForStock(stockCode);
            return allData.OrderBy(d => d.TradeDate).Take(count).ToList();
        }

        public bool HasData(string stockCode)
        {
            if (_memoryCache.TryGetValue(stockCode, out var cached) && cached != null && cached.Count > 0)
                return true;
            var fileName = GetKlineFileName(stockCode);
            return File.Exists(fileName);
        }

        public (DateTime? StartDate, DateTime? EndDate) GetDataDateRange(string stockCode)
        {
            if (_memoryCache.TryGetValue(stockCode, out var cached) && cached != null && cached.Count > 0)
            {
                var sorted = cached.OrderBy(d => d.TradeDate).ToList();
                return (sorted[0].TradeDate, sorted[sorted.Count - 1].TradeDate);
            }
            var data = GetAllDailyDataForStock(stockCode);
            if (data.Count == 0)
                return (null, null);
            var sortedData = data.OrderBy(d => d.TradeDate).ToList();
            return (sortedData.First().TradeDate, sortedData.Last().TradeDate);
        }

        public List<string> GetAllStockCodes()
        {
            var result = new List<string>();

            try
            {
                var klineDir = Path.Combine(_dbPath, "kline");
                if (!Directory.Exists(klineDir))
                    return result;

                var files = Directory.GetFiles(klineDir, "*.json");
                foreach (var file in files)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        result.Add(fileName);
                    }
                }

                result.Sort();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] 获取股票代码列表失败: {ex.Message}");
            }

            return result;
        }

        public bool DeleteStockData(string stockCode)
        {
            try
            {
                var fileName = GetKlineFileName(stockCode);
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                    _memoryCache.TryRemove(stockCode, out _);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] 删除股票数据失败: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region IKlineDataRepository 实现

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
                    var existingData = GetAllDailyDataForStock(group.Key);
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

                    // 按日期排序并保存
                    updatedData = updatedData.OrderBy(d => d.TradeDate).ToList();
                    SaveStockData(group.Key, updatedData);

                    // 更新内存缓存
                    _memoryCache[group.Key] = new List<DailyKlineData>(updatedData);
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
            try
            {
                var data = GetAllDailyDataForStock(stockCode);
                if (data == null)
                    return (null, null);
                var record = data.FirstOrDefault(d => d.TradeDate.Date == tradeDate.Date);
                if (record != null)
                {
                    return (record.Amount, record.TurnoverRate);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] GetYesterdayAmountAndTurnoverRate [{stockCode}]: {ex.Message}");
            }
            return (null, null);
        }

        public Dictionary<string, (decimal? Amount, decimal? TurnoverRate)> GetYesterdayAmountAndTurnoverRateBatch(
            List<string> stockCodes, DateTime tradeDate)
        {
            var result = new Dictionary<string, (decimal? Amount, decimal? TurnoverRate)>();

            if (stockCodes == null || stockCodes.Count == 0)
                return result;

            foreach (var stockCode in stockCodes)
            {
                result[stockCode] = GetYesterdayAmountAndTurnoverRate(stockCode, tradeDate);
            }

            return result;
        }

        public Dictionary<string, decimal?> GetYesterdayAmountBatch(List<string> stockCodes, DateTime tradeDate)
        {
            var result = new Dictionary<string, decimal?>();

            if (stockCodes == null || stockCodes.Count == 0)
                return result;

            foreach (var stockCode in stockCodes)
            {
                var (amount, _) = GetYesterdayAmountAndTurnoverRate(stockCode, tradeDate);
                result[stockCode] = amount;
            }

            return result;
        }

        #endregion

        #region Stock Info 相关方法

        public DateTime? GetLatestTradeDate()
        {
            try
            {
                DateTime? latestDate = null;
                var stockCodes = GetAllStockCodes();

                foreach (var stockCode in stockCodes)
                {
                    var (_, endDate) = GetDataDateRange(stockCode);
                    if (endDate.HasValue && (!latestDate.HasValue || endDate.Value > latestDate.Value))
                    {
                        latestDate = endDate.Value;
                    }
                }

                return latestDate;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] GetLatestTradeDate 失败: {ex.Message}");
                return null;
            }
        }

        public Dictionary<string, string> GetAllStockNames()
        {
            var result = new Dictionary<string, string>();

            try
            {
                var fileName = Path.Combine(_dbPath, "metadata", "stock_names.json");
                if (File.Exists(fileName))
                {
                    string jsonContent;
                    lock (_metadataLock)
                    {
                        jsonContent = File.ReadAllText(fileName);
                    }

                    var names = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);
                    return names ?? new Dictionary<string, string>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] GetAllStockNames 失败: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 保存股票信息。按股票代码与已有名称合并覆盖，同一代码不会重复。
        /// </summary>
        public int SaveStockInfo(List<(string StockCode, string StockName, ushort MarketCode)> stockInfoList)
        {
            if (stockInfoList == null || stockInfoList.Count == 0)
                return 0;

            try
            {
                var stockNames = GetAllStockNames();

                foreach (var info in stockInfoList)
                {
                    if (!string.IsNullOrWhiteSpace(info.StockCode) && !string.IsNullOrWhiteSpace(info.StockName))
                    {
                        stockNames[info.StockCode] = info.StockName;
                    }
                }

                // 保存到文件
                var fileName = Path.Combine(_dbPath, "metadata", "stock_names.json");
                string jsonContent = JsonSerializer.Serialize(stockNames);

                lock (_metadataLock)
                {
                    File.WriteAllText(fileName, jsonContent);
                }

                return stockInfoList.Count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] SaveStockInfo 失败: {ex.Message}");
                return 0;
            }
        }

        public int InitializeStockInfoFromDailyData()
        {
            try
            {
                var stockCodes = GetAllStockCodes();
                var stockNames = GetAllStockNames();

                int added = 0;
                foreach (var stockCode in stockCodes)
                {
                    if (!stockNames.ContainsKey(stockCode))
                    {
                        stockNames[stockCode] = stockCode; // 默认使用股票代码作为名称
                        added++;
                    }
                }

                if (added > 0)
                {
                    var fileName = Path.Combine(_dbPath, "metadata", "stock_names.json");
                    string jsonContent = JsonSerializer.Serialize(stockNames);

                    lock (_metadataLock)
                    {
                        File.WriteAllText(fileName, jsonContent);
                    }
                }

                return added;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] InitializeStockInfoFromDailyData 失败: {ex.Message}");
                return 0;
            }
        }

        #endregion

        #region 私有方法

        private List<DailyKlineData> GetAllDailyDataForStock(string stockCode)
        {
            var fileName = GetKlineFileName(stockCode);
            if (!File.Exists(fileName))
                return new List<DailyKlineData>();

            object lockObj = _fileLocks.GetOrAdd(stockCode, _ => new object());
            try
            {
                string jsonContent;
                lock (lockObj)
                {
                    jsonContent = File.ReadAllText(fileName);
                }
                var data = JsonSerializer.Deserialize<List<DailyKlineData>>(jsonContent);
                return data ?? new List<DailyKlineData>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] 读取K线数据失败 {stockCode}: {ex.Message}");
                return new List<DailyKlineData>();
            }
        }

        private void SaveStockData(string stockCode, List<DailyKlineData> data)
        {
            var fileName = GetKlineFileName(stockCode);
            object lockObj = _fileLocks.GetOrAdd(stockCode, _ => new object());
            try
            {
                string jsonContent = JsonSerializer.Serialize(data);
                lock (lockObj)
                {
                    File.WriteAllText(fileName, jsonContent);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] 保存K线数据失败 {stockCode}: {ex.Message}");
                throw;
            }
        }

        private List<DailyKlineData> FilterDateRange(List<DailyKlineData> data, DateTime startDate, DateTime endDate)
        {
            return data.Where(d => d.TradeDate >= startDate && d.TradeDate <= endDate).ToList();
        }

        private string GetKlineFileName(string stockCode)
        {
            return Path.Combine(_dbPath, "kline", $"{stockCode}.json");
        }

        #endregion
    }
}
