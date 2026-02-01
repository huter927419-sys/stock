using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MQReceiver.Repositories;

namespace MQReceiver.DataProcessing.Repositories
{
    /// <summary>
    /// RocksDB（文件系统模拟）除权数据仓储实现
    /// </summary>
    public class RocksDBExRightsDataRepository : IExRightsDataRepository
    {
        private readonly string _dbPath;
        private readonly object _fileLock = new object();

        public RocksDBExRightsDataRepository(string dbPath = "data/rocksdb")
        {
            _dbPath = dbPath;
            Initialize();
        }

        private bool Initialize()
        {
            try
            {
                var exRightsDir = Path.Combine(_dbPath, "exrights");
                if (!Directory.Exists(exRightsDir))
                {
                    Directory.CreateDirectory(exRightsDir);
                    Console.WriteLine($"[RocksDB] 除权数据目录创建成功: {exRightsDir}");
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] 除权数据目录初始化失败: {ex.Message}");
                return false;
            }
        }

        public bool TestConnection()
        {
            try
            {
                var exRightsDir = Path.Combine(_dbPath, "exrights");
                return Directory.Exists(exRightsDir);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 保存除权数据。按 (股票代码, 除权日期) 与已有数据合并后覆盖写入，同一股票同一日期不会重复。
        /// </summary>
        public int SaveExRightsData(List<ExRightsDataRecord> records)
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
                    var existingData = GetExRightsData(stockCode);

                    // 按除权日期建字典，同日期覆盖，保证不重复
                    var dataDict = existingData.ToDictionary(d => d.ExRightsDate);

                    foreach (var record in group)
                    {
                        dataDict[record.ExRightsDate] = record;
                        totalSaved++;
                    }

                    // 按日期排序并保存
                    var sortedData = dataDict.Values.OrderBy(d => d.ExRightsDate).ToList();
                    SaveExRightsDataToFile(stockCode, sortedData);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RocksDB] 保存除权数据失败 {group.Key}: {ex.Message}");
                }
            }

            return totalSaved;
        }

        public List<ExRightsDataRecord> GetExRightsData(string stockCode)
        {
            var fileName = GetExRightsFileName(stockCode);
            if (!File.Exists(fileName))
            {
                return new List<ExRightsDataRecord>();
            }

            try
            {
                string jsonContent;
                lock (_fileLock)
                {
                    jsonContent = File.ReadAllText(fileName);
                }

                var data = JsonSerializer.Deserialize<List<ExRightsDataRecord>>(jsonContent);
                return data ?? new List<ExRightsDataRecord>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] 读取除权数据失败 {stockCode}: {ex.Message}");
                return new List<ExRightsDataRecord>();
            }
        }

        public List<ExRightsDataRecord> GetExRightsData(string stockCode, DateTime startDate, DateTime endDate)
        {
            var allData = GetExRightsData(stockCode);
            return allData.Where(d => d.ExRightsDate >= startDate && d.ExRightsDate <= endDate).ToList();
        }

        public bool HasExRightsData(string stockCode)
        {
            var fileName = GetExRightsFileName(stockCode);
            return File.Exists(fileName) && new FileInfo(fileName).Length > 0;
        }

        public bool DeleteExRightsData(string stockCode)
        {
            try
            {
                var fileName = GetExRightsFileName(stockCode);
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] 删除除权数据失败 {stockCode}: {ex.Message}");
                return false;
            }
        }

        public List<ExRightsDataRecord> GetExRightsDataAfterDate(string stockCode, DateTime targetDate)
        {
            var allData = GetExRightsData(stockCode);
            return allData.Where(d => d.ExRightsDate > targetDate).OrderBy(d => d.ExRightsDate).ToList();
        }

        public List<ExRightsDataRecord> GetExRightsDataBeforeDate(string stockCode, DateTime targetDate)
        {
            var allData = GetExRightsData(stockCode);
            return allData.Where(d => d.ExRightsDate < targetDate).OrderBy(d => d.ExRightsDate).ToList();
        }

        private void SaveExRightsDataToFile(string stockCode, List<ExRightsDataRecord> data)
        {
            var fileName = GetExRightsFileName(stockCode);
            try
            {
                string jsonContent = JsonSerializer.Serialize(data);
                lock (_fileLock)
                {
                    File.WriteAllText(fileName, jsonContent);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] 保存除权数据失败 {stockCode}: {ex.Message}");
                throw;
            }
        }

        private string GetExRightsFileName(string stockCode)
        {
            return Path.Combine(_dbPath, "exrights", $"{stockCode}.json");
        }
    }
}
