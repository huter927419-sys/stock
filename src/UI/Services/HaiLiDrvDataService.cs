using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MQReceiver.Cache;
using MQReceiver.Configuration;
using MQReceiver.Helpers;
using MQReceiver.Models;
using MQReceiver.Repositories;
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
        private PostgresStockDataRepository _repository;
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
            _repository = new PostgresStockDataRepository();
            _configProvider = configProvider ?? AppConfigProvider.Instance;
            
            // 根据配置提供者构建连接字符串（独立模式使用HaiLiDrvConfigProvider的数据库配置）
            _connectionString = BuildConnectionStringFromConfig(_configProvider);
            
            // 加载配置的股票代码
            LoadConfiguredStockCodes();
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
        /// 获取所有股票数据（自动选择实时数据或日线数据）
        /// </summary>
        public List<HaiLiDataItem> GetAllStockData(int maxCount = 500)
        {
            if (IsTradingTime() && _realTimeCache != null && _realTimeCache.Count > 0)
            {
                // 交易时间：使用实时数据
                return GetRealTimeData(maxCount);
            }
            else
            {
                // 收盘后：使用日线数据
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
                var allData = _realTimeCache.GetAllData();
                
                // 清空并复用List，减少GC压力
                _reusableItemList.Clear();
                _reusableItemList.Capacity = Math.Max(_reusableItemList.Capacity, Math.Min(maxCount, allData.Count));
                
                foreach (var record in allData)
                {
                    // 应用股票代码过滤
                    if (!ShouldDisplayStock(record.StockCode))
                        continue;
                    
                    _reusableItemList.Add(new HaiLiDataItem
                    {
                        Time = record.UpdateTime.ToString("HH:mm:ss"),
                        StockCode = record.StockCode,
                        StockName = record.StockName ?? record.StockCode,
                        NewPrice = (double)record.NewPrice,
                        LastClose = (double)record.LastClose,
                        PriceChange = record.LastClose != 0
                            ? (double)((record.NewPrice - record.LastClose) / record.LastClose * 100)
                            : 0,
                        Volume = (double)record.Volume,
                        Amount = (double)record.Amount
                    });
                }
                
                // 排序并限制数量
                _reusableItemList.Sort((a, b) => b.Amount.CompareTo(a.Amount));
                if (_reusableItemList.Count > maxCount)
                {
                    _reusableItemList.RemoveRange(maxCount, _reusableItemList.Count - maxCount);
                }
                
                if (_enableStockCodeFilter && _configuredStockCodes.Count > 0)
                {
                    Console.WriteLine($"[HaiLiDrvDataService] 实时数据：过滤后显示 {_reusableItemList.Count} 条（配置了 {_configuredStockCodes.Count} 个股票代码）");
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
                    Console.WriteLine("[HaiLiDrvDataService] 未找到交易日数据");
                    return new List<HaiLiDataItem>();
                }

                // 从数据库获取最新交易日的所有股票数据
                var dailyData = GetDailyDataByDate(latestTradeDate);
                
                // 清空并复用List，减少GC压力
                _reusableItemList.Clear();
                _reusableItemList.Capacity = Math.Max(_reusableItemList.Capacity, Math.Min(maxCount, dailyData.Count));
                
                // 需要获取前一日收盘价来计算涨跌幅
                foreach (var record in dailyData)
                {
                    // 应用股票代码过滤
                    if (!ShouldDisplayStock(record.StockCode))
                        continue;
                    
                    // 获取前一日收盘价
                    decimal? prevClose = GetPreviousClosePrice(record.StockCode, record.TradeDate);
                    decimal lastClose = prevClose ?? record.OpenPrice;
                    
                    _reusableItemList.Add(new HaiLiDataItem
                    {
                        Time = record.TradeDate.ToString("yyyy-MM-dd"),
                        StockCode = record.StockCode,
                        StockName = GetStockName(record.StockCode),
                        NewPrice = (double)record.ClosePrice,
                        LastClose = (double)lastClose,
                        PriceChange = lastClose != 0 
                            ? (double)((record.ClosePrice - lastClose) / lastClose * 100)
                            : 0,
                        Volume = (double)record.Volume,
                        Amount = (double)record.Amount
                    });
                }
                
                // 排序并限制数量
                _reusableItemList.Sort((a, b) => b.Amount.CompareTo(a.Amount));
                if (_reusableItemList.Count > maxCount)
                {
                    _reusableItemList.RemoveRange(maxCount, _reusableItemList.Count - maxCount);
                }
                
                // 返回新列表（因为调用者可能会修改），但复用内部缓冲区
                var result = new List<HaiLiDataItem>(_reusableItemList);
                
                if (_enableStockCodeFilter && _configuredStockCodes.Count > 0)
                {
                    Console.WriteLine($"[HaiLiDrvDataService] 日线数据：过滤后显示 {result.Count} 条（配置了 {_configuredStockCodes.Count} 个股票代码）");
                }
                
                return result;
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
        private List<Models.DailyDataRecord> GetDailyDataByDate(DateTime tradeDate)
        {
            // 清空并复用List，减少GC压力
            _reusableDailyDataList.Clear();
            
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    string sql = @"
                        SELECT stock_code, trade_date, open_price, high_price, low_price, close_price, volume, amount
                        FROM stock_daily_data
                        WHERE trade_date = @trade_date
                        ORDER BY amount DESC NULLS LAST";
                    
                    using (var command = new NpgsqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@trade_date", tradeDate.Date);
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                _reusableDailyDataList.Add(new Models.DailyDataRecord
                                {
                                    StockCode = reader.GetString(0),
                                    TradeDate = reader.GetDateTime(1),
                                    OpenPrice = reader.GetDecimal(2),
                                    HighPrice = reader.GetDecimal(3),
                                    LowPrice = reader.GetDecimal(4),
                                    ClosePrice = reader.GetDecimal(5),
                                    Volume = reader.GetDecimal(6),
                                    Amount = reader.IsDBNull(7) ? 0m : reader.GetDecimal(7),
                                    MarketCode = 0, // 默认值，可以从stock_code判断
                                    TradeDateTime = reader.GetDateTime(1),
                                    TimeStamp = 0
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HaiLiDrvDataService] 获取指定日期日线数据失败: {ex.Message}");
            }
            
            // 返回新列表（因为调用者可能会修改），但复用内部缓冲区
            return new List<Models.DailyDataRecord>(_reusableDailyDataList);
        }

        /// <summary>
        /// 获取最新交易日
        /// </summary>
        private DateTime GetLatestTradeDate()
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    string sql = @"
                        SELECT MAX(trade_date) 
                        FROM stock_daily_data 
                        WHERE trade_date <= CURRENT_DATE";
                    
                    using (var command = new NpgsqlCommand(sql, connection))
                    {
                        var result = command.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            return ((DateTime)result).Date;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HaiLiDrvDataService] 获取最新交易日失败: {ex.Message}");
            }
            return DateTime.MinValue;
        }

        /// <summary>
        /// 获取前一日收盘价
        /// </summary>
        private decimal? GetPreviousClosePrice(string stockCode, DateTime currentDate)
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    string sql = @"
                        SELECT close_price 
                        FROM stock_daily_data
                        WHERE stock_code = @stock_code 
                          AND trade_date < @current_date
                        ORDER BY trade_date DESC
                        LIMIT 1";
                    
                    using (var command = new NpgsqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@stock_code", stockCode);
                        command.Parameters.AddWithValue("@current_date", currentDate.Date);
                        var result = command.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            return (decimal)result;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HaiLiDrvDataService] 获取前一日收盘价失败 {stockCode}: {ex.Message}");
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
        public double PriceChange { get; set; }
        public double Volume { get; set; }
        public double Amount { get; set; }
    }
}
