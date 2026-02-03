using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MQReceiver.Cache;
using MQReceiver.Configuration;
using MQReceiver.Helpers;
using MQReceiver.Models;
using MQReceiver.Repositories;
using MQReceiver.DataProcessing.Factories;
using Npgsql;

namespace MQReceiver.Services
{
    /// <summary>
    /// HaiLiDrv数据服务
    /// 根据交易时间自动选择数据源：实时数据（交易时间）或日线数据（收盘后）
    /// </summary>
    public class HaiLiDrvDataService
    {
        private RealTimeDataCache _realTimeCache;
        private IStockDataRepository _repository;
        private string _connectionString;
        private IConfigurationProvider _configProvider;
        private HashSet<string> _configuredStockCodes; // 配置的股票代码集合
        private bool _enableStockCodeFilter; // 是否启用股票代码过滤
        
        // 复用List对象，减少GC压力
        private readonly List<HaiLiDataItem> _reusableItemList = new List<HaiLiDataItem>(500);
        private readonly List<Models.DailyDataRecord> _reusableDailyDataList = new List<Models.DailyDataRecord>(1000);

        public HaiLiDrvDataService(RealTimeDataCache realTimeCache, IConfigurationProvider configProvider = null)
        {
            _realTimeCache = realTimeCache;
            _configProvider = configProvider ?? AppConfigProvider.Instance;
            
            // 确保使用 RocksDB 作为存储后端
            EnsureRocksDBBackend();
            
            _repository = RepositoryFactory.GetStockDataRepository();
            
            // 根据配置提供者构建连接字符串（独立模式使用HaiLiDrvConfigProvider的数据库配置）
            _connectionString = BuildConnectionStringFromConfig(_configProvider);
            
            // 加载配置的股票代码
            LoadConfiguredStockCodes();
        }
        
        /// <summary>
        /// 确保使用 RocksDB 作为存储后端
        /// </summary>
        private void EnsureRocksDBBackend()
        {
            // 检查当前后端，如果不是 RocksDB，则配置为 RocksDB
            var currentBackend = RepositoryFactory.GetCurrentBackend();
            if (currentBackend != RepositoryFactory.StorageBackend.RocksDB && 
                currentBackend != RepositoryFactory.StorageBackend.FileBased)
            {
                // 读取 RocksDB 路径配置
                string dbPath = _configProvider.GetString("RocksDBPath", "data/rocksdb");
                
                // 解析路径（相对路径转为绝对路径）
                if (!System.IO.Path.IsPathRooted(dbPath))
                {
                    string baseDir = System.AppDomain.CurrentDomain.BaseDirectory ?? ".";
                    dbPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, dbPath));
                }
                
                // 配置为 RocksDB
                RepositoryFactory.Configure(
                    RepositoryFactory.StorageBackend.RocksDB,
                    dbPath: dbPath
                );
                
                Console.WriteLine($"[HaiLiDrvDataService] 已配置使用 RocksDB 存储后端: {dbPath}");
            }
            else
            {
                Console.WriteLine($"[HaiLiDrvDataService] 当前存储后端: {currentBackend}");
            }
        }

        /// <summary>
        /// 从配置提供者构建数据库连接字符串
        /// </summary>
        private string BuildConnectionStringFromConfig(IConfigurationProvider configProvider)
        {
            // 使用ConfigurationHelper统一处理
            return Helpers.ConfigurationHelper.GetConnectionString(configProvider);
        }

        /// <summary>
        /// 加载配置的股票代码列表
        /// </summary>
        private void LoadConfiguredStockCodes()
        {
            _configuredStockCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _enableStockCodeFilter = _configProvider.GetBool("HaiLiDrv_EnableStockCodeFilter", false);
            
            if (_enableStockCodeFilter)
            {
                string stockCodesConfig = _configProvider.GetString("HaiLiDrv_StockCodes", "");
                if (!string.IsNullOrWhiteSpace(stockCodesConfig))
                {
                    var codes = stockCodesConfig.Split(new[] { ',', ';', ' ', '\t', '\n', '\r' }, 
                        StringSplitOptions.RemoveEmptyEntries);
                    foreach (var code in codes)
                    {
                        string trimmedCode = code.Trim();
                        if (!string.IsNullOrEmpty(trimmedCode))
                        {
                            _configuredStockCodes.Add(trimmedCode);
                        }
                    }
                    Console.WriteLine($"[HaiLiDrvDataService] 已加载 {_configuredStockCodes.Count} 个配置的股票代码");
                }
                else
                {
                    Console.WriteLine("[HaiLiDrvDataService] 股票代码过滤已启用，但未配置股票代码，将显示全部");
                }
            }
        }

        /// <summary>
        /// 检查股票代码是否应该显示
        /// </summary>
        private bool ShouldDisplayStock(string stockCode)
        {
            if (!_enableStockCodeFilter)
                return true; // 未启用过滤，显示全部
            
            if (_configuredStockCodes.Count == 0)
                return true; // 配置为空，显示全部
            
            return _configuredStockCodes.Contains(stockCode);
        }

        /// <summary>
        /// 判断当前是否为交易时间
        /// </summary>
        public bool IsTradingTime()
        {
            var now = DateTime.Now;
            var time = now.TimeOfDay;
            var dayOfWeek = now.DayOfWeek;

            // 周末不是交易时间
            if (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday)
                return false;

            // 交易时间：9:30-11:30, 13:00-15:00
            bool isMorning = time >= new TimeSpan(9, 30, 0) && time < new TimeSpan(11, 30, 0);
            bool isAfternoon = time >= new TimeSpan(13, 0, 0) && time < new TimeSpan(15, 0, 0);

            return isMorning || isAfternoon;
        }

        /// <summary>
        /// 获取所有股票数据（盘中用实时数据，盘后用日线数据）
        /// </summary>
        public List<HaiLiDataItem> GetAllStockData(int maxCount = 500)
        {
            bool isTradingTime = IsTradingTime();
            int cacheCount = _realTimeCache?.Count ?? 0;
            
            // 盘中（交易时间）：使用实时数据
            if (isTradingTime)
            {
                if (_realTimeCache != null && cacheCount > 0)
                {
                    var result = GetRealTimeData(maxCount);
                    
                    // 如果实时数据为空（可能被过滤掉了），回退到日线数据
                    if (result.Count == 0)
                    {
                        return GetDailyData(maxCount);
                    }
                    
                    return result;
                }
                else
                {
                    // 盘中但实时数据缓存为空，回退到日线数据
                    return GetDailyData(maxCount);
                }
            }
            else
            {
                // 盘后（非交易时间）：使用日线数据
                return GetDailyData(maxCount);
            }
        }

        /// <summary>
        /// 获取实时数据
        /// </summary>
        private List<HaiLiDataItem> GetRealTimeData(int maxCount)
        {
            try
            {
                if (_realTimeCache == null)
                {
                    return new List<HaiLiDataItem>();
                }
                
                var allData = _realTimeCache.GetAllData();
                
                if (allData == null || allData.Count == 0)
                {
                    return new List<HaiLiDataItem>();
                }
                
                // 清空并复用List，减少GC压力
                _reusableItemList.Clear();
                _reusableItemList.Capacity = Math.Max(_reusableItemList.Capacity, Math.Min(maxCount, allData.Count));
                
                // 性能优化：先收集需要查询前一日收盘价的股票代码（LastClose <= 0的）
                var needPrevCloseCodes = new List<string>();
                var recordsToProcess = new List<(RealTimeDataRecord record, decimal lastClose)>();
                
                foreach (var record in allData)
                {
                    // 应用股票代码过滤
                    if (!ShouldDisplayStock(record.StockCode))
                        continue;
                    
                    decimal lastClose = record.LastClose;
                    if (lastClose <= 0)
                    {
                        needPrevCloseCodes.Add(record.StockCode);
                        recordsToProcess.Add((record, 0m));
                    }
                    else
                    {
                        recordsToProcess.Add((record, lastClose));
                    }
                }
                
                // 批量获取前一日收盘价（性能优化：一次性查询所有需要的股票）
                Dictionary<string, decimal?> prevCloseDict = null;
                if (needPrevCloseCodes.Count > 0)
                {
                    prevCloseDict = BatchGetPreviousClosePrices(needPrevCloseCodes, DateTime.Today);
                }
                
                // 处理数据
                foreach (var (record, lastClose) in recordsToProcess)
                {
                    decimal finalLastClose = lastClose;
                    if (finalLastClose <= 0)
                    {
                        // 从批量查询结果获取前一日收盘价
                        if (prevCloseDict != null && prevCloseDict.TryGetValue(record.StockCode, out var prevClose) && prevClose.HasValue && prevClose.Value > 0)
                        {
                            finalLastClose = prevClose.Value;
                        }
                        else
                        {
                            // 如果还是找不到，使用当日开盘价（不理想，但比0好）
                            finalLastClose = record.Open > 0 ? record.Open : record.NewPrice;
                        }
                    }
                    
                    // 计算涨跌幅
                    double priceChange = 0;
                    if (finalLastClose > 0 && record.NewPrice > 0)
                    {
                        priceChange = (double)((record.NewPrice - finalLastClose) / finalLastClose * 100);
                    }
                    
                    _reusableItemList.Add(new HaiLiDataItem
                    {
                        Time = record.UpdateTime.ToString("HH:mm:ss"),
                        StockCode = record.StockCode,
                        StockName = record.StockName ?? record.StockCode,
                        NewPrice = (double)record.NewPrice,
                        LastClose = (double)finalLastClose,
                        Open = (double)record.Open,
                        PriceChange = priceChange,
                        Volume = (double)record.Volume,
                        Amount = (double)record.Amount
                    });
                }
                
                // 排序并限制数量（按涨跌幅降序排序，涨幅大的在前）
                _reusableItemList.Sort((a, b) => b.PriceChange.CompareTo(a.PriceChange));
                
                if (_reusableItemList.Count > maxCount)
                {
                    _reusableItemList.RemoveRange(maxCount, _reusableItemList.Count - maxCount);
                }
                
                // 返回新列表（因为调用者可能会修改），但复用内部缓冲区
                return new List<HaiLiDataItem>(_reusableItemList);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HaiLiDrvDataService] 获取实时数据失败: {ex.Message}");
                return new List<HaiLiDataItem>();
            }
        }

        /// <summary>
        /// 获取日线数据（最新交易日的数据）
        /// </summary>
        private List<HaiLiDataItem> GetDailyData(int maxCount)
        {
            try
            {
                // 获取最新交易日
                DateTime latestTradeDate = GetLatestTradeDate();
                if (latestTradeDate == DateTime.MinValue)
                {
                    return new List<HaiLiDataItem>();
                }

                // 从数据库获取最新交易日的所有股票数据
                var dailyData = GetDailyDataByDate(latestTradeDate);
                if (dailyData.Count == 0)
                {
                    return new List<HaiLiDataItem>();
                }
                
                // 性能优化：只对需要的数据批量获取前一日收盘价（限制数量，避免查询过多）
                var stockCodes = dailyData.Take(maxCount * 2).Select(d => d.StockCode).Distinct().ToList();
                var prevCloseDict = BatchGetPreviousClosePrices(stockCodes, latestTradeDate);
                
                // 清空并复用List，减少GC压力
                _reusableItemList.Clear();
                _reusableItemList.Capacity = Math.Max(_reusableItemList.Capacity, Math.Min(maxCount, dailyData.Count));
                
                // 批量处理数据
                foreach (var record in dailyData)
                {
                    // 应用股票代码过滤
                    if (!ShouldDisplayStock(record.StockCode))
                        continue;
                    
                    // 从批量查询结果获取前一日收盘价
                    decimal? prevClose = prevCloseDict.TryGetValue(record.StockCode, out var prev) ? prev : null;
                    decimal lastClose = prevClose ?? record.ClosePrice;
                    
                    // 计算涨跌幅：只有当有前一日收盘价时才计算，否则为0
                    double priceChange = 0;
                    if (prevClose.HasValue && prevClose.Value > 0)
                    {
                        priceChange = (double)((record.ClosePrice - prevClose.Value) / prevClose.Value * 100);
                    }
                    
                    _reusableItemList.Add(new HaiLiDataItem
                    {
                        Time = record.TradeDate.ToString("yyyy-MM-dd"),
                        StockCode = record.StockCode,
                        StockName = GetStockName(record.StockCode),
                        NewPrice = (double)record.ClosePrice,
                        LastClose = (double)lastClose,
                        Open = (double)record.OpenPrice,
                        PriceChange = priceChange,
                        Volume = (double)record.Volume,
                        Amount = (double)record.Amount
                    });
                }
                
                // 排序并限制数量（按涨跌幅降序排序，涨幅大的在前）
                _reusableItemList.Sort((a, b) => b.PriceChange.CompareTo(a.PriceChange));
                
                if (_reusableItemList.Count > maxCount)
                {
                    _reusableItemList.RemoveRange(maxCount, _reusableItemList.Count - maxCount);
                }
                
                // 返回新列表（因为调用者可能会修改），但复用内部缓冲区
                return new List<HaiLiDataItem>(_reusableItemList);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HaiLiDrvDataService] 获取日线数据失败: {ex.Message}");
                return new List<HaiLiDataItem>();
            }
        }

        /// <summary>
        /// 获取指定日期的所有股票日线数据
        /// </summary>
        /// <summary>
        /// 获取指定日期的全部日线数据（使用当前配置的存储后端）
        /// 性能优化：限制查询数量，只查询前N个股票（按代码排序，确保稳定性）
        /// </summary>
        private List<Models.DailyDataRecord> GetDailyDataByDate(DateTime tradeDate)
        {
            _reusableDailyDataList.Clear();
            try
            {
                var codes = _repository.GetAllStockCodes();
                if (codes == null || codes.Count == 0)
                {
                    return new List<Models.DailyDataRecord>(_reusableDailyDataList);
                }
                
                // 性能优化：限制查询的股票数量，避免查询所有股票（通常只需要前几千个）
                // 如果配置了股票代码过滤，只查询配置的股票
                List<string> codesToQuery;
                if (_enableStockCodeFilter && _configuredStockCodes.Count > 0)
                {
                    codesToQuery = codes.Where(c => _configuredStockCodes.Contains(c)).ToList();
                    if (codesToQuery.Count == 0)
                    {
                        codesToQuery = codes.Take(10000).ToList(); // 如果过滤后为空，回退到查询前10000个
                    }
                }
                else
                {
                    // 未启用过滤，只查询前10000个股票（按代码排序，确保稳定性）
                    codesToQuery = codes.OrderBy(c => c).Take(10000).ToList();
                }
                
                var day = tradeDate.Date;
                var lockObj = new object();
                
                // 性能优化：并行查询，但限制并行度避免过多连接
                System.Threading.Tasks.Parallel.ForEach(codesToQuery, new System.Threading.Tasks.ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 8) // 限制并行度
                }, stockCode =>
                {
                    try
                    {
                        var list = _repository.GetDailyData(stockCode, day, day);
                        if (list == null || list.Count == 0) return;
                        
                        lock (lockObj)
                        {
                            foreach (var k in list)
                            {
                                _reusableDailyDataList.Add(new Models.DailyDataRecord
                                {
                                    StockCode = stockCode,
                                    TradeDate = k.TradeDate,
                                    OpenPrice = k.Open,
                                    HighPrice = k.High,
                                    LowPrice = k.Low,
                                    ClosePrice = k.Close,
                                    Volume = k.Volume,
                                    Amount = k.Amount ?? 0m,
                                    MarketCode = 0,
                                    TradeDateTime = k.TradeDate,
                                    TimeStamp = 0
                                });
                            }
                        }
                    }
                    catch
                    {
                        // 忽略单个股票查询失败
                    }
                });
                
                // 按成交额排序
                _reusableDailyDataList.Sort((a, b) => b.Amount.CompareTo(a.Amount));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HaiLiDrvDataService] 获取指定日期日线数据失败: {ex.Message}");
            }
            return new List<Models.DailyDataRecord>(_reusableDailyDataList);
        }

        /// <summary>
        /// 获取最新交易日（使用当前配置的存储后端）
        /// </summary>
        private DateTime GetLatestTradeDate()
        {
            try
            {
                var dt = _repository.GetLatestTradeDate();
                return dt?.Date ?? DateTime.MinValue;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HaiLiDrvDataService] 获取最新交易日失败: {ex.Message}");
            }
            return DateTime.MinValue;
        }

        /// <summary>
        /// 批量获取前一日收盘价（性能优化：一次性查询所有股票）
        /// </summary>
        private Dictionary<string, decimal?> BatchGetPreviousClosePrices(List<string> stockCodes, DateTime currentDate)
        {
            var result = new Dictionary<string, decimal?>();
            if (stockCodes == null || stockCodes.Count == 0)
                return result;
            
            try
            {
                var prevDate = currentDate.AddDays(-1);
                var startDate = currentDate.AddMonths(-3);
                
                // 性能优化：使用并行查询，但限制并行度避免过多连接
                var lockObj = new object();
                System.Threading.Tasks.Parallel.ForEach(stockCodes, new System.Threading.Tasks.ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 8) // 限制并行度
                }, stockCode =>
                {
                    try
                    {
                        var data = _repository.GetDailyData(stockCode, startDate, prevDate);
                        if (data != null && data.Count > 0)
                        {
                            var last = data.OrderByDescending(d => d.TradeDate).FirstOrDefault();
                            if (last != null)
                            {
                                lock (lockObj)
                                {
                                    result[stockCode] = last.Close;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // 忽略单个股票查询失败
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HaiLiDrvDataService] 批量获取前一日收盘价失败: {ex.Message}");
            }
            
            return result;
        }

        /// <summary>
        /// 获取前一日收盘价（使用当前配置的存储后端）
        /// 注意：已优化为批量查询，此方法保留用于兼容性
        /// </summary>
        private decimal? GetPreviousClosePrice(string stockCode, DateTime currentDate)
        {
            try
            {
                var data = _repository.GetDailyData(stockCode, currentDate.AddMonths(-3), currentDate.AddDays(-1));
                var last = data?.OrderByDescending(d => d.TradeDate).FirstOrDefault();
                return last?.Close;
            }
            catch
            {
                // 减少日志输出，避免性能影响
            }
            return null;
        }

        /// <summary>
        /// 获取股票名称
        /// </summary>
        private string GetStockName(string stockCode)
        {
            try
            {
                return Cache.StockInfoCache.Instance.GetStockName(stockCode);
            }
            catch
            {
                return stockCode;
            }
        }
    }

    /// <summary>
    /// HaiLi数据项（用于显示）
    /// </summary>
    public class HaiLiDataItem
    {
        public string Time { get; set; }
        public string StockCode { get; set; }
        public string StockName { get; set; }
        public double NewPrice { get; set; }
        public double LastClose { get; set; }
        /// <summary>开盘价（用于日内涨幅筛选）</summary>
        public double Open { get; set; }
        public double PriceChange { get; set; }
        public double Volume { get; set; }
        public double Amount { get; set; }
    }
}
