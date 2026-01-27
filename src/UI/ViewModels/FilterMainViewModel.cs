using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using MQReceiver.Cache;
using MQReceiver.Models;

namespace MQReceiver.ViewModels
{
    /// <summary>
    /// 主窗口视图模型
    /// </summary>
    public class FilterMainViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<StockResultItem> _table1Results;
        private ObservableCollection<StockResultItem> _table2Results;
        private ObservableCollection<StockResultItem> _table3Results;
        private ObservableCollection<StockResultItem> _table4Results;
        private ObservableCollection<StockResultItem> _table5Results;
        private ObservableCollection<StockResultItem> _table6Results;
        private ObservableCollection<StockResultItem> _table7Results;
        private ObservableCollection<StockResultItem> _table8Results;
        private DateTime _lastUpdateTime;
        private static readonly Configuration.IConfigurationProvider _configProvider = Configuration.AppConfigProvider.Instance;
        
        // 实时数据缓存（用于响应数据推送更新涨幅）
        private RealTimeDataCache _realTimeCache;
        
        // 节流机制：合并短时间内的多次更新，避免UI卡顿
        private readonly HashSet<string> _pendingUpdateStockCodes = new HashSet<string>();
        private readonly object _pendingUpdateLock = new object();
        private System.Threading.Timer _updateThrottleTimer;
        private const int THROTTLE_INTERVAL_MS = 100; // 100ms内合并所有更新

        // 表格1-6的默认最小值
        private decimal _table1WeeklyKDefaultMin = 0;
        private decimal _table1MonthlyKDefaultMin = 0;
        private decimal _table1QuarterlyKDefaultMin = 0;
        private bool _table1WeeklyKSelected = true;
        private bool _table1MonthlyKSelected = false;
        private bool _table1QuarterlyKSelected = false;

        private decimal _table2WeeklyKDefaultMin = 0;
        private decimal _table2MonthlyKDefaultMin = 0;
        private decimal _table2QuarterlyKDefaultMin = 0;
        private bool _table2WeeklyKSelected = true;
        private bool _table2MonthlyKSelected = false;
        private bool _table2QuarterlyKSelected = false;

        private decimal _table3WeeklyKDefaultMin = 0;
        private decimal _table3MonthlyKDefaultMin = 0;
        private decimal _table3QuarterlyKDefaultMin = 0;
        private bool _table3WeeklyKSelected = true;
        private bool _table3MonthlyKSelected = false;
        private bool _table3QuarterlyKSelected = false;

        private decimal _table4WeeklyKDefaultMin = 0;
        private decimal _table4MonthlyKDefaultMin = 0;
        private decimal _table4QuarterlyKDefaultMin = 0;
        private bool _table4WeeklyKSelected = true;
        private bool _table4MonthlyKSelected = false;
        private bool _table4QuarterlyKSelected = false;

        private decimal _table5WeeklyKDefaultMin = 0;
        private decimal _table5MonthlyKDefaultMin = 0;
        private decimal _table5QuarterlyKDefaultMin = 0;
        private bool _table5WeeklyKSelected = true;
        private bool _table5MonthlyKSelected = false;
        private bool _table5QuarterlyKSelected = false;

        private decimal _table6WeeklyKDefaultMin = 0;
        private decimal _table6MonthlyKDefaultMin = 0;
        private decimal _table6QuarterlyKDefaultMin = 0;
        private bool _table6WeeklyKSelected = true;
        private bool _table6MonthlyKSelected = false;
        private bool _table6QuarterlyKSelected = false;

        // 全局M1/M2/M3/M4/N阈值（所有6个表格共用）
        private decimal _globalM1 = 78;
        private decimal _globalM2 = 65;
        private decimal _globalM3 = 50;
        private decimal _globalM4 = 30;
        private int _globalN = 5;
        private decimal _priceChangeFilterThreshold = 7; // 涨幅计算阈值，默认7%

        public FilterMainViewModel()
        {
            _table1Results = new ObservableCollection<StockResultItem>();
            _table2Results = new ObservableCollection<StockResultItem>();
            _table3Results = new ObservableCollection<StockResultItem>();
            _table4Results = new ObservableCollection<StockResultItem>();
            _table5Results = new ObservableCollection<StockResultItem>();
            _table6Results = new ObservableCollection<StockResultItem>();
            _table7Results = new ObservableCollection<StockResultItem>();
            _table8Results = new ObservableCollection<StockResultItem>();
            _lastUpdateTime = DateTime.Now;

            // 从配置文件加载默认值
            LoadDefaultValuesFromConfig();
        }
        
        /// <summary>
        /// 设置实时数据缓存（用于响应数据推送更新涨幅）
        /// </summary>
        public void SetRealTimeCache(RealTimeDataCache cache)
        {
            // 取消之前的订阅
            if (_realTimeCache != null)
            {
                _realTimeCache.DataUpdated -= OnRealTimeDataUpdated;
            }
            
            _realTimeCache = cache;
            
            // 订阅新的数据更新事件
            if (_realTimeCache != null)
            {
                _realTimeCache.DataUpdated += OnRealTimeDataUpdated;
            }
        }
        
        /// <summary>
        /// 当实时数据更新时，更新对应股票的涨幅（带节流机制）
        /// </summary>
        private void OnRealTimeDataUpdated(object sender, List<string> updatedStockCodes)
        {
            if (updatedStockCodes == null || updatedStockCodes.Count == 0 || _realTimeCache == null)
                return;
            
            // 将需要更新的股票代码加入待更新集合（线程安全）
            lock (_pendingUpdateLock)
            {
                foreach (var code in updatedStockCodes)
                {
                    if (!string.IsNullOrEmpty(code))
                        _pendingUpdateStockCodes.Add(code);
                }
            }
            
            // 重置节流定时器：如果100ms内没有新的更新，才真正执行UI更新
            if (_updateThrottleTimer == null)
            {
                _updateThrottleTimer = new System.Threading.Timer(OnThrottleTimerElapsed, null, THROTTLE_INTERVAL_MS, Timeout.Infinite);
            }
            else
            {
                _updateThrottleTimer.Change(THROTTLE_INTERVAL_MS, Timeout.Infinite);
            }
        }
        
        /// <summary>
        /// 节流定时器回调：执行批量更新
        /// </summary>
        private void OnThrottleTimerElapsed(object state)
        {
            List<string> stockCodesToUpdate;
            
            // 取出所有待更新的股票代码并清空集合
            lock (_pendingUpdateLock)
            {
                if (_pendingUpdateStockCodes.Count == 0)
                    return;
                    
                stockCodesToUpdate = new List<string>(_pendingUpdateStockCodes);
                _pendingUpdateStockCodes.Clear();
            }
            
            // 在UI线程异步更新，避免阻塞MQ数据接收
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    // 批量更新所有表格中对应股票的涨幅
                    bool hasChanges1 = UpdatePriceChangeForStockCodes(_table1Results, stockCodesToUpdate);
                    bool hasChanges2 = UpdatePriceChangeForStockCodes(_table2Results, stockCodesToUpdate);
                    bool hasChanges3 = UpdatePriceChangeForStockCodes(_table3Results, stockCodesToUpdate);
                    bool hasChanges4 = UpdatePriceChangeForStockCodes(_table4Results, stockCodesToUpdate);
                    bool hasChanges5 = UpdatePriceChangeForStockCodes(_table5Results, stockCodesToUpdate);
                    bool hasChanges6 = UpdatePriceChangeForStockCodes(_table6Results, stockCodesToUpdate);
                    bool hasChanges7 = UpdatePriceChangeForStockCodes(_table7Results, stockCodesToUpdate);
                    bool hasChanges8 = UpdatePriceChangeForStockCodes(_table8Results, stockCodesToUpdate);
                    
                    // 如果有涨幅变化，重新排序（按涨幅从高到低）
                    if (hasChanges1) SortByPriceChange(_table1Results);
                    if (hasChanges2) SortByPriceChange(_table2Results);
                    if (hasChanges3) SortByPriceChange(_table3Results);
                    if (hasChanges4) SortByPriceChange(_table4Results);
                    if (hasChanges5) SortByPriceChange(_table5Results);
                    if (hasChanges6) SortByPriceChange(_table6Results);
                    if (hasChanges7) SortByPriceChange(_table7Results);
                    if (hasChanges8) SortByPriceChange(_table8Results);
                }
                catch (Exception ex)
                {
                    // 静默处理错误，避免影响主流程
                    System.Diagnostics.Debug.WriteLine($"[更新涨幅] 错误: {ex.Message}");
                }
            }), System.Windows.Threading.DispatcherPriority.Background); // 使用Background优先级，降低对用户交互的影响
        }
        
        /// <summary>
        /// 更新指定股票代码列表的涨幅数据（优化：使用字典索引快速查找）
        /// </summary>
        /// <returns>是否有涨幅变化</returns>
        private bool UpdatePriceChangeForStockCodes(ObservableCollection<StockResultItem> collection, List<string> stockCodes)
        {
            if (collection == null || stockCodes == null || stockCodes.Count == 0 || _realTimeCache == null)
                return false;
            
            // 使用HashSet提高查找效率
            var stockCodeSet = new HashSet<string>(stockCodes, StringComparer.Ordinal);
            
            // 批量获取实时数据，减少字典查找次数
            var realTimeDataDict = _realTimeCache.GetDataBatch(stockCodes);
            
            bool hasChanges = false;
            
            // 遍历集合，只更新在stockCodeSet中的股票
            foreach (var item in collection)
            {
                if (string.IsNullOrEmpty(item.StockCode) || !stockCodeSet.Contains(item.StockCode))
                    continue;
                
                // 从批量获取的字典中查找数据
                if (realTimeDataDict.TryGetValue(item.StockCode, out var realTimeData) && realTimeData != null)
                {
                    // 计算新的涨幅
                    decimal? newPriceChange = CalculatePriceChangePercent(realTimeData);
                    
                    // 如果涨幅有变化，更新并通知UI
                    if (newPriceChange != item.PriceChangePercent)
                    {
                        item.PriceChangePercent = newPriceChange;
                        item.PriceChangeColor = GetPriceChangeColor(newPriceChange);
                        hasChanges = true;
                        // PriceChangeDisplay 是计算属性，会自动更新
                    }
                }
            }
            
            return hasChanges;
        }
        
        /// <summary>
        /// 重新按涨幅排序集合（涨幅从高到低，涨幅缺失的排到末尾）
        /// </summary>
        private void SortByPriceChange(ObservableCollection<StockResultItem> collection)
        {
            if (collection == null || collection.Count <= 1)
                return;
            
            // 转换为List并排序
            var sortedList = collection.OrderByDescending(item => item.PriceChangePercent ?? decimal.MinValue).ToList();
            
            // 检查顺序是否真的改变了（避免不必要的UI刷新）
            bool orderChanged = false;
            for (int i = 0; i < sortedList.Count; i++)
            {
                if (!ReferenceEquals(collection[i], sortedList[i]))
                {
                    orderChanged = true;
                    break;
                }
            }
            
            if (!orderChanged)
                return;
            
            // 重新填充集合（保持ObservableCollection的变更通知）
            collection.Clear();
            foreach (var item in sortedList)
            {
                collection.Add(item);
            }
        }
        
        /// <summary>
        /// 计算涨幅百分比（与 UnifiedStockFilter 逻辑一致）
        /// </summary>
        private decimal? CalculatePriceChangePercent(RealTimeDataRecord realTimeData)
        {
            try
            {
                if (realTimeData == null)
                    return null;

                decimal newPrice = realTimeData.NewPrice;
                decimal lastClose = realTimeData.LastClose;

                // 检查数据有效性
                if (lastClose <= 0 || newPrice <= 0)
                    return null;

                // 计算涨幅百分比（保留2位小数）
                decimal priceChange = newPrice - lastClose;
                decimal priceChangePercent = (priceChange / lastClose) * 100;

                return Math.Round(priceChangePercent, 2);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 从配置文件加载默认值
        /// </summary>
        private void LoadDefaultValuesFromConfig()
        {
            try
            {
                // 表格1
                _table1WeeklyKDefaultMin = _configProvider.GetDecimal("Filter1_WeeklyKDefaultMin", 0);
                _table1MonthlyKDefaultMin = _configProvider.GetDecimal("Filter1_MonthlyKDefaultMin", 0);
                _table1QuarterlyKDefaultMin = _configProvider.GetDecimal("Filter1_QuarterlyKDefaultMin", 0);

                // 表格2
                _table2WeeklyKDefaultMin = _configProvider.GetDecimal("Filter2_WeeklyKDefaultMin", 0);
                _table2MonthlyKDefaultMin = _configProvider.GetDecimal("Filter2_MonthlyKDefaultMin", 0);
                _table2QuarterlyKDefaultMin = _configProvider.GetDecimal("Filter2_QuarterlyKDefaultMin", 0);

                // 表格3
                _table3WeeklyKDefaultMin = _configProvider.GetDecimal("Filter3_WeeklyKDefaultMin", 0);
                _table3MonthlyKDefaultMin = _configProvider.GetDecimal("Filter3_MonthlyKDefaultMin", 0);
                _table3QuarterlyKDefaultMin = _configProvider.GetDecimal("Filter3_QuarterlyKDefaultMin", 0);

                // 表格4
                _table4WeeklyKDefaultMin = _configProvider.GetDecimal("Filter4_WeeklyKDefaultMin", 0);
                _table4MonthlyKDefaultMin = _configProvider.GetDecimal("Filter4_MonthlyKDefaultMin", 0);
                _table4QuarterlyKDefaultMin = _configProvider.GetDecimal("Filter4_QuarterlyKDefaultMin", 0);

                // 表格5
                _table5WeeklyKDefaultMin = _configProvider.GetDecimal("Filter5_WeeklyKDefaultMin", 0);
                _table5MonthlyKDefaultMin = _configProvider.GetDecimal("Filter5_MonthlyKDefaultMin", 0);
                _table5QuarterlyKDefaultMin = _configProvider.GetDecimal("Filter5_QuarterlyKDefaultMin", 0);

                // 表格6
                _table6WeeklyKDefaultMin = _configProvider.GetDecimal("Filter6_WeeklyKDefaultMin", 0);
                _table6MonthlyKDefaultMin = _configProvider.GetDecimal("Filter6_MonthlyKDefaultMin", 0);
                _table6QuarterlyKDefaultMin = _configProvider.GetDecimal("Filter6_QuarterlyKDefaultMin", 0);

                // 加载全局M1/M2/M3阈值
                _globalM1 = _configProvider.GetDecimal("GlobalThreshold_M1", 78m);
                _globalM2 = _configProvider.GetDecimal("GlobalThreshold_M2", 65m);
                _globalM3 = _configProvider.GetDecimal("GlobalThreshold_M3", 50m);
                _globalM4 = _configProvider.GetDecimal("GlobalThreshold_M4", 30m);
                _globalN = _configProvider.GetInt("GlobalThreshold_N", 5);
                _priceChangeFilterThreshold = _configProvider.GetDecimal("PriceChangeFilterThreshold", 7m);
            }
            catch
            {
                // 使用字段初始化的默认值
            }
        }

        // 表格1-6结果属性
        public ObservableCollection<StockResultItem> Table1Results
        {
            get { return _table1Results; }
            set
            {
                _table1Results = value;
                OnPropertyChanged(nameof(Table1Results));
                OnPropertyChanged(nameof(Table1Count));
            }
        }

        public ObservableCollection<StockResultItem> Table2Results
        {
            get { return _table2Results; }
            set
            {
                _table2Results = value;
                OnPropertyChanged(nameof(Table2Results));
                OnPropertyChanged(nameof(Table2Count));
            }
        }

        public ObservableCollection<StockResultItem> Table3Results
        {
            get { return _table3Results; }
            set
            {
                _table3Results = value;
                OnPropertyChanged(nameof(Table3Results));
                OnPropertyChanged(nameof(Table3Count));
            }
        }

        public ObservableCollection<StockResultItem> Table4Results
        {
            get { return _table4Results; }
            set
            {
                _table4Results = value;
                OnPropertyChanged(nameof(Table4Results));
                OnPropertyChanged(nameof(Table4Count));
            }
        }

        public ObservableCollection<StockResultItem> Table5Results
        {
            get { return _table5Results; }
            set
            {
                _table5Results = value;
                OnPropertyChanged(nameof(Table5Results));
                OnPropertyChanged(nameof(Table5Count));
            }
        }

        public ObservableCollection<StockResultItem> Table6Results
        {
            get { return _table6Results; }
            set
            {
                _table6Results = value;
                OnPropertyChanged(nameof(Table6Results));
                OnPropertyChanged(nameof(Table6Count));
            }
        }

        public ObservableCollection<StockResultItem> Table7Results
        {
            get { return _table7Results; }
            set
            {
                _table7Results = value;
                OnPropertyChanged(nameof(Table7Results));
                OnPropertyChanged(nameof(Table7Count));
            }
        }

        public ObservableCollection<StockResultItem> Table8Results
        {
            get { return _table8Results; }
            set
            {
                _table8Results = value;
                OnPropertyChanged(nameof(Table8Results));
                OnPropertyChanged(nameof(Table8Count));
            }
        }

        public int Table1Count => _table1Results?.Count ?? 0;
        public int Table2Count => _table2Results?.Count ?? 0;
        public int Table3Count => _table3Results?.Count ?? 0;
        public int Table4Count => _table4Results?.Count ?? 0;
        public int Table5Count => _table5Results?.Count ?? 0;
        public int Table6Count => _table6Results?.Count ?? 0;
        public int Table7Count => _table7Results?.Count ?? 0;
        public int Table8Count => _table8Results?.Count ?? 0;

        public DateTime LastUpdateTime
        {
            get { return _lastUpdateTime; }
            set
            {
                _lastUpdateTime = value;
                OnPropertyChanged(nameof(LastUpdateTime));
            }
        }

        // 存储原始结果（用于计算）
        private List<FilterResultWithHistory> _originalTable1Results;
        private List<FilterResultWithHistory> _originalTable2Results;
        private List<FilterResultWithHistory> _originalTable3Results;
        private List<FilterResultWithHistory> _originalTable4Results;
        private List<FilterResultWithHistory> _originalTable5Results;
        private List<FilterResultWithHistory> _originalTable6Results;
        private List<FilterResultWithHistory> _originalTable7Results;
        private List<FilterResultWithHistory> _originalTable8Results;

        // 表格1的默认最小值属性
        public decimal Table1WeeklyKDefaultMin
        {
            get { return _table1WeeklyKDefaultMin; }
            set
            {
                if (_table1WeeklyKDefaultMin != value)
                {
                    _table1WeeklyKDefaultMin = value;
                    _configProvider.SetDecimal("Filter1_WeeklyKDefaultMin", value);
                    OnPropertyChanged(nameof(Table1WeeklyKDefaultMin));
                    OnPropertyChanged(nameof(Table1SettingsDisplay));
                    FilterResultsByDefaultMin(1);
                }
            }
        }

        public decimal Table1MonthlyKDefaultMin
        {
            get { return _table1MonthlyKDefaultMin; }
            set
            {
                if (_table1MonthlyKDefaultMin != value)
                {
                    _table1MonthlyKDefaultMin = value;
                    _configProvider.SetDecimal("Filter1_MonthlyKDefaultMin", value);
                    OnPropertyChanged(nameof(Table1MonthlyKDefaultMin));
                    OnPropertyChanged(nameof(Table1SettingsDisplay));
                    FilterResultsByDefaultMin(1);
                }
            }
        }

        public decimal Table1QuarterlyKDefaultMin
        {
            get { return _table1QuarterlyKDefaultMin; }
            set
            {
                if (_table1QuarterlyKDefaultMin != value)
                {
                    _table1QuarterlyKDefaultMin = value;
                    _configProvider.SetDecimal("Filter1_QuarterlyKDefaultMin", value);
                    OnPropertyChanged(nameof(Table1QuarterlyKDefaultMin));
                    OnPropertyChanged(nameof(Table1SettingsDisplay));
                    FilterResultsByDefaultMin(1);
                }
            }
        }

        public bool Table1WeeklyKSelected
        {
            get { return _table1WeeklyKSelected; }
            set { if (_table1WeeklyKSelected != value) { _table1WeeklyKSelected = value; OnPropertyChanged(nameof(Table1WeeklyKSelected)); } }
        }

        public bool Table1MonthlyKSelected
        {
            get { return _table1MonthlyKSelected; }
            set { if (_table1MonthlyKSelected != value) { _table1MonthlyKSelected = value; OnPropertyChanged(nameof(Table1MonthlyKSelected)); } }
        }

        public bool Table1QuarterlyKSelected
        {
            get { return _table1QuarterlyKSelected; }
            set { if (_table1QuarterlyKSelected != value) { _table1QuarterlyKSelected = value; OnPropertyChanged(nameof(Table1QuarterlyKSelected)); } }
        }

        // 表格2的默认最小值属性
        public decimal Table2WeeklyKDefaultMin
        {
            get { return _table2WeeklyKDefaultMin; }
            set
            {
                if (_table2WeeklyKDefaultMin != value)
                {
                    _table2WeeklyKDefaultMin = value;
                    _configProvider.SetDecimal("Filter2_WeeklyKDefaultMin", value);
                    OnPropertyChanged(nameof(Table2WeeklyKDefaultMin));
                    OnPropertyChanged(nameof(Table2SettingsDisplay));
                    FilterResultsByDefaultMin(2);
                }
            }
        }

        public decimal Table2MonthlyKDefaultMin
        {
            get { return _table2MonthlyKDefaultMin; }
            set
            {
                if (_table2MonthlyKDefaultMin != value)
                {
                    _table2MonthlyKDefaultMin = value;
                    _configProvider.SetDecimal("Filter2_MonthlyKDefaultMin", value);
                    OnPropertyChanged(nameof(Table2MonthlyKDefaultMin));
                    OnPropertyChanged(nameof(Table2SettingsDisplay));
                    FilterResultsByDefaultMin(2);
                }
            }
        }

        public decimal Table2QuarterlyKDefaultMin
        {
            get { return _table2QuarterlyKDefaultMin; }
            set
            {
                if (_table2QuarterlyKDefaultMin != value)
                {
                    _table2QuarterlyKDefaultMin = value;
                    _configProvider.SetDecimal("Filter2_QuarterlyKDefaultMin", value);
                    OnPropertyChanged(nameof(Table2QuarterlyKDefaultMin));
                    OnPropertyChanged(nameof(Table2SettingsDisplay));
                    FilterResultsByDefaultMin(2);
                }
            }
        }

        public bool Table2WeeklyKSelected
        {
            get { return _table2WeeklyKSelected; }
            set { if (_table2WeeklyKSelected != value) { _table2WeeklyKSelected = value; OnPropertyChanged(nameof(Table2WeeklyKSelected)); } }
        }

        public bool Table2MonthlyKSelected
        {
            get { return _table2MonthlyKSelected; }
            set { if (_table2MonthlyKSelected != value) { _table2MonthlyKSelected = value; OnPropertyChanged(nameof(Table2MonthlyKSelected)); } }
        }

        public bool Table2QuarterlyKSelected
        {
            get { return _table2QuarterlyKSelected; }
            set { if (_table2QuarterlyKSelected != value) { _table2QuarterlyKSelected = value; OnPropertyChanged(nameof(Table2QuarterlyKSelected)); } }
        }

        // 表格3的默认最小值属性
        public decimal Table3WeeklyKDefaultMin
        {
            get { return _table3WeeklyKDefaultMin; }
            set
            {
                if (_table3WeeklyKDefaultMin != value)
                {
                    _table3WeeklyKDefaultMin = value;
                    _configProvider.SetDecimal("Filter3_WeeklyKDefaultMin", value);
                    OnPropertyChanged(nameof(Table3WeeklyKDefaultMin));
                    OnPropertyChanged(nameof(Table3SettingsDisplay));
                    FilterResultsByDefaultMin(3);
                }
            }
        }

        public decimal Table3MonthlyKDefaultMin
        {
            get { return _table3MonthlyKDefaultMin; }
            set
            {
                if (_table3MonthlyKDefaultMin != value)
                {
                    _table3MonthlyKDefaultMin = value;
                    _configProvider.SetDecimal("Filter3_MonthlyKDefaultMin", value);
                    OnPropertyChanged(nameof(Table3MonthlyKDefaultMin));
                    OnPropertyChanged(nameof(Table3SettingsDisplay));
                    FilterResultsByDefaultMin(3);
                }
            }
        }

        public decimal Table3QuarterlyKDefaultMin
        {
            get { return _table3QuarterlyKDefaultMin; }
            set
            {
                if (_table3QuarterlyKDefaultMin != value)
                {
                    _table3QuarterlyKDefaultMin = value;
                    _configProvider.SetDecimal("Filter3_QuarterlyKDefaultMin", value);
                    OnPropertyChanged(nameof(Table3QuarterlyKDefaultMin));
                    OnPropertyChanged(nameof(Table3SettingsDisplay));
                    FilterResultsByDefaultMin(3);
                }
            }
        }

        public bool Table3WeeklyKSelected
        {
            get { return _table3WeeklyKSelected; }
            set { if (_table3WeeklyKSelected != value) { _table3WeeklyKSelected = value; OnPropertyChanged(nameof(Table3WeeklyKSelected)); } }
        }

        public bool Table3MonthlyKSelected
        {
            get { return _table3MonthlyKSelected; }
            set { if (_table3MonthlyKSelected != value) { _table3MonthlyKSelected = value; OnPropertyChanged(nameof(Table3MonthlyKSelected)); } }
        }

        public bool Table3QuarterlyKSelected
        {
            get { return _table3QuarterlyKSelected; }
            set { if (_table3QuarterlyKSelected != value) { _table3QuarterlyKSelected = value; OnPropertyChanged(nameof(Table3QuarterlyKSelected)); } }
        }

        // 表格4的默认最小值属性
        public decimal Table4WeeklyKDefaultMin
        {
            get { return _table4WeeklyKDefaultMin; }
            set
            {
                if (_table4WeeklyKDefaultMin != value)
                {
                    _table4WeeklyKDefaultMin = value;
                    _configProvider.SetDecimal("Filter4_WeeklyKDefaultMin", value);
                    OnPropertyChanged(nameof(Table4WeeklyKDefaultMin));
                    OnPropertyChanged(nameof(Table4SettingsDisplay));
                    FilterResultsByDefaultMin(4);
                }
            }
        }

        public decimal Table4MonthlyKDefaultMin
        {
            get { return _table4MonthlyKDefaultMin; }
            set
            {
                if (_table4MonthlyKDefaultMin != value)
                {
                    _table4MonthlyKDefaultMin = value;
                    _configProvider.SetDecimal("Filter4_MonthlyKDefaultMin", value);
                    OnPropertyChanged(nameof(Table4MonthlyKDefaultMin));
                    OnPropertyChanged(nameof(Table4SettingsDisplay));
                    FilterResultsByDefaultMin(4);
                }
            }
        }

        public decimal Table4QuarterlyKDefaultMin
        {
            get { return _table4QuarterlyKDefaultMin; }
            set
            {
                if (_table4QuarterlyKDefaultMin != value)
                {
                    _table4QuarterlyKDefaultMin = value;
                    _configProvider.SetDecimal("Filter4_QuarterlyKDefaultMin", value);
                    OnPropertyChanged(nameof(Table4QuarterlyKDefaultMin));
                    OnPropertyChanged(nameof(Table4SettingsDisplay));
                    FilterResultsByDefaultMin(4);
                }
            }
        }

        public bool Table4WeeklyKSelected
        {
            get { return _table4WeeklyKSelected; }
            set { if (_table4WeeklyKSelected != value) { _table4WeeklyKSelected = value; OnPropertyChanged(nameof(Table4WeeklyKSelected)); } }
        }

        public bool Table4MonthlyKSelected
        {
            get { return _table4MonthlyKSelected; }
            set { if (_table4MonthlyKSelected != value) { _table4MonthlyKSelected = value; OnPropertyChanged(nameof(Table4MonthlyKSelected)); } }
        }

        public bool Table4QuarterlyKSelected
        {
            get { return _table4QuarterlyKSelected; }
            set { if (_table4QuarterlyKSelected != value) { _table4QuarterlyKSelected = value; OnPropertyChanged(nameof(Table4QuarterlyKSelected)); } }
        }

        // 表格5的默认最小值属性
        public decimal Table5WeeklyKDefaultMin
        {
            get { return _table5WeeklyKDefaultMin; }
            set
            {
                if (_table5WeeklyKDefaultMin != value)
                {
                    _table5WeeklyKDefaultMin = value;
                    _configProvider.SetDecimal("Filter5_WeeklyKDefaultMin", value);
                    OnPropertyChanged(nameof(Table5WeeklyKDefaultMin));
                    OnPropertyChanged(nameof(Table5SettingsDisplay));
                    FilterResultsByDefaultMin(5);
                }
            }
        }

        public decimal Table5MonthlyKDefaultMin
        {
            get { return _table5MonthlyKDefaultMin; }
            set
            {
                if (_table5MonthlyKDefaultMin != value)
                {
                    _table5MonthlyKDefaultMin = value;
                    _configProvider.SetDecimal("Filter5_MonthlyKDefaultMin", value);
                    OnPropertyChanged(nameof(Table5MonthlyKDefaultMin));
                    OnPropertyChanged(nameof(Table5SettingsDisplay));
                    FilterResultsByDefaultMin(5);
                }
            }
        }

        public decimal Table5QuarterlyKDefaultMin
        {
            get { return _table5QuarterlyKDefaultMin; }
            set
            {
                if (_table5QuarterlyKDefaultMin != value)
                {
                    _table5QuarterlyKDefaultMin = value;
                    _configProvider.SetDecimal("Filter5_QuarterlyKDefaultMin", value);
                    OnPropertyChanged(nameof(Table5QuarterlyKDefaultMin));
                    OnPropertyChanged(nameof(Table5SettingsDisplay));
                    FilterResultsByDefaultMin(5);
                }
            }
        }

        public bool Table5WeeklyKSelected
        {
            get { return _table5WeeklyKSelected; }
            set { if (_table5WeeklyKSelected != value) { _table5WeeklyKSelected = value; OnPropertyChanged(nameof(Table5WeeklyKSelected)); } }
        }

        public bool Table5MonthlyKSelected
        {
            get { return _table5MonthlyKSelected; }
            set { if (_table5MonthlyKSelected != value) { _table5MonthlyKSelected = value; OnPropertyChanged(nameof(Table5MonthlyKSelected)); } }
        }

        public bool Table5QuarterlyKSelected
        {
            get { return _table5QuarterlyKSelected; }
            set { if (_table5QuarterlyKSelected != value) { _table5QuarterlyKSelected = value; OnPropertyChanged(nameof(Table5QuarterlyKSelected)); } }
        }

        // 表格6的默认最小值属性
        public decimal Table6WeeklyKDefaultMin
        {
            get { return _table6WeeklyKDefaultMin; }
            set
            {
                if (_table6WeeklyKDefaultMin != value)
                {
                    _table6WeeklyKDefaultMin = value;
                    _configProvider.SetDecimal("Filter6_WeeklyKDefaultMin", value);
                    OnPropertyChanged(nameof(Table6WeeklyKDefaultMin));
                    OnPropertyChanged(nameof(Table6SettingsDisplay));
                    FilterResultsByDefaultMin(6);
                }
            }
        }

        public decimal Table6MonthlyKDefaultMin
        {
            get { return _table6MonthlyKDefaultMin; }
            set
            {
                if (_table6MonthlyKDefaultMin != value)
                {
                    _table6MonthlyKDefaultMin = value;
                    _configProvider.SetDecimal("Filter6_MonthlyKDefaultMin", value);
                    OnPropertyChanged(nameof(Table6MonthlyKDefaultMin));
                    OnPropertyChanged(nameof(Table6SettingsDisplay));
                    FilterResultsByDefaultMin(6);
                }
            }
        }

        public decimal Table6QuarterlyKDefaultMin
        {
            get { return _table6QuarterlyKDefaultMin; }
            set
            {
                if (_table6QuarterlyKDefaultMin != value)
                {
                    _table6QuarterlyKDefaultMin = value;
                    _configProvider.SetDecimal("Filter6_QuarterlyKDefaultMin", value);
                    OnPropertyChanged(nameof(Table6QuarterlyKDefaultMin));
                    OnPropertyChanged(nameof(Table6SettingsDisplay));
                    FilterResultsByDefaultMin(6);
                }
            }
        }

        public bool Table6WeeklyKSelected
        {
            get { return _table6WeeklyKSelected; }
            set { if (_table6WeeklyKSelected != value) { _table6WeeklyKSelected = value; OnPropertyChanged(nameof(Table6WeeklyKSelected)); } }
        }

        public bool Table6MonthlyKSelected
        {
            get { return _table6MonthlyKSelected; }
            set { if (_table6MonthlyKSelected != value) { _table6MonthlyKSelected = value; OnPropertyChanged(nameof(Table6MonthlyKSelected)); } }
        }

        public bool Table6QuarterlyKSelected
        {
            get { return _table6QuarterlyKSelected; }
            set { if (_table6QuarterlyKSelected != value) { _table6QuarterlyKSelected = value; OnPropertyChanged(nameof(Table6QuarterlyKSelected)); } }
        }

        // 设置显示文本属性
        public string Table1SettingsDisplay => $"周K({_table1WeeklyKDefaultMin}), 月K({_table1MonthlyKDefaultMin}), 季K({_table1QuarterlyKDefaultMin})";
        public string Table2SettingsDisplay => $"周K({_table2WeeklyKDefaultMin}), 月K({_table2MonthlyKDefaultMin}), 季K({_table2QuarterlyKDefaultMin})";
        public string Table3SettingsDisplay => $"周K({_table3WeeklyKDefaultMin}), 月K({_table3MonthlyKDefaultMin}), 季K({_table3QuarterlyKDefaultMin})";
        public string Table4SettingsDisplay => $"周K({_table4WeeklyKDefaultMin}), 月K({_table4MonthlyKDefaultMin}), 季K({_table4QuarterlyKDefaultMin})";
        public string Table5SettingsDisplay => $"周K({_table5WeeklyKDefaultMin}), 月K({_table5MonthlyKDefaultMin}), 季K({_table5QuarterlyKDefaultMin})";
        public string Table6SettingsDisplay => $"周K({_table6WeeklyKDefaultMin}), 月K({_table6MonthlyKDefaultMin}), 季K({_table6QuarterlyKDefaultMin})";

        // 全局M1/M2/M3/M4阈值属性（所有6个表格共用）
        public decimal GlobalM1
        {
            get { return _globalM1; }
            set
            {
                if (_globalM1 != value)
                {
                    _globalM1 = value;
                    _configProvider.SetDecimal("GlobalThreshold_M1", value);
                    OnPropertyChanged(nameof(GlobalM1));
                    OnPropertyChanged(nameof(GlobalThresholdDisplay));
                }
            }
        }

        public decimal GlobalM2
        {
            get { return _globalM2; }
            set
            {
                if (_globalM2 != value)
                {
                    _globalM2 = value;
                    _configProvider.SetDecimal("GlobalThreshold_M2", value);
                    OnPropertyChanged(nameof(GlobalM2));
                    OnPropertyChanged(nameof(GlobalThresholdDisplay));
                }
            }
        }

        public decimal GlobalM3
        {
            get { return _globalM3; }
            set
            {
                if (_globalM3 != value)
                {
                    _globalM3 = value;
                    _configProvider.SetDecimal("GlobalThreshold_M3", value);
                    OnPropertyChanged(nameof(GlobalM3));
                    OnPropertyChanged(nameof(GlobalThresholdDisplay));
                }
            }
        }

        public decimal GlobalM4
        {
            get { return _globalM4; }
            set
            {
                if (_globalM4 != value)
                {
                    _globalM4 = value;
                    _configProvider.SetDecimal("GlobalThreshold_M4", value);
                    OnPropertyChanged(nameof(GlobalM4));
                    OnPropertyChanged(nameof(GlobalThresholdDisplay));
                }
            }
        }

        public int GlobalN
        {
            get { return _globalN; }
            set
            {
                if (_globalN != value)
                {
                    _globalN = value;
                    _configProvider.SetValue("GlobalThreshold_N", value.ToString());
                    OnPropertyChanged(nameof(GlobalN));
                    OnPropertyChanged(nameof(GlobalThresholdDisplay));
                }
            }
        }

        /// <summary>
        /// 涨幅计算阈值（涨幅大于此百分比时不显示）
        /// </summary>
        public decimal PriceChangeFilterThreshold
        {
            get { return _priceChangeFilterThreshold; }
            set
            {
                if (_priceChangeFilterThreshold != value)
                {
                    _priceChangeFilterThreshold = value;
                    _configProvider.SetDecimal("PriceChangeFilterThreshold", value);
                    OnPropertyChanged(nameof(PriceChangeFilterThreshold));
                    // 阈值更改后，立即重新应用涨幅计算到所有表格
                    ApplyPriceChangeFilterToAllTables();
                }
            }
        }

        // 全局阈值显示文本
        public string GlobalThresholdDisplay => $"M1({_globalM1}), M2({_globalM2}), M3({_globalM3}), M4({_globalM4}), N({_globalN})";

        /// <summary>
        /// 刷新计算结果（当默认最小值改变时调用）
        /// </summary>
        public void RefreshFilteredResults()
        {
            for (int i = 1; i <= 6; i++)
            {
                FilterResultsByDefaultMin(i);
            }
        }

        /// <summary>
        /// 应用涨幅计算到所有表格（当涨幅计算阈值改变时调用）
        /// </summary>
        private void ApplyPriceChangeFilterToAllTables()
        {
            // 重新应用默认最小值计算（已包含涨幅计算）
            RefreshFilteredResults();
        }

        /// <summary>
        /// 根据默认最小值计算结果
        /// </summary>
        private void FilterResultsByDefaultMin(int tableNumber)
        {
            List<FilterResultWithHistory> originalResults = null;
            ObservableCollection<StockResultItem> targetResults = null;
            decimal weeklyMin = 0, monthlyMin = 0, quarterlyMin = 0;

            switch (tableNumber)
            {
                case 1:
                    originalResults = _originalTable1Results;
                    targetResults = _table1Results;
                    weeklyMin = _table1WeeklyKDefaultMin;
                    monthlyMin = _table1MonthlyKDefaultMin;
                    quarterlyMin = _table1QuarterlyKDefaultMin;
                    break;
                case 2:
                    originalResults = _originalTable2Results;
                    targetResults = _table2Results;
                    weeklyMin = _table2WeeklyKDefaultMin;
                    monthlyMin = _table2MonthlyKDefaultMin;
                    quarterlyMin = _table2QuarterlyKDefaultMin;
                    break;
                case 3:
                    originalResults = _originalTable3Results;
                    targetResults = _table3Results;
                    weeklyMin = _table3WeeklyKDefaultMin;
                    monthlyMin = _table3MonthlyKDefaultMin;
                    quarterlyMin = _table3QuarterlyKDefaultMin;
                    break;
                case 4:
                    originalResults = _originalTable4Results;
                    targetResults = _table4Results;
                    weeklyMin = _table4WeeklyKDefaultMin;
                    monthlyMin = _table4MonthlyKDefaultMin;
                    quarterlyMin = _table4QuarterlyKDefaultMin;
                    break;
                case 5:
                    originalResults = _originalTable5Results;
                    targetResults = _table5Results;
                    weeklyMin = _table5WeeklyKDefaultMin;
                    monthlyMin = _table5MonthlyKDefaultMin;
                    quarterlyMin = _table5QuarterlyKDefaultMin;
                    break;
                case 6:
                    originalResults = _originalTable6Results;
                    targetResults = _table6Results;
                    weeklyMin = _table6WeeklyKDefaultMin;
                    monthlyMin = _table6MonthlyKDefaultMin;
                    quarterlyMin = _table6QuarterlyKDefaultMin;
                    break;
            }

            if (originalResults != null && targetResults != null)
            {
                var filtered = originalResults.Where(r =>
                    r.WeeklyK >= weeklyMin &&
                    r.MonthlyK >= monthlyMin &&
                    r.QuarterlyK >= quarterlyMin &&
                    // 应用涨幅计算：涨幅为空或涨幅<=阈值时保留
                    (!r.PriceChangePercent.HasValue || r.PriceChangePercent.Value <= _priceChangeFilterThreshold)).ToList();

                targetResults.Clear();
                foreach (var item in filtered)
                {
                    targetResults.Add(CreateStockResultItem(item));
                }
                OnPropertyChanged($"Table{tableNumber}Count");
            }
        }

        /// <summary>
        /// 更新所有结果
        /// </summary>
        public void UpdateResults(
            List<FilterResultWithHistory> results1,
            List<FilterResultWithHistory> results2,
            List<FilterResultWithHistory> results3,
            List<FilterResultWithHistory> results4,
            List<FilterResultWithHistory> results5,
            List<FilterResultWithHistory> results6,
            List<FilterResultWithHistory> results7,
            List<FilterResultWithHistory> results8)
        {
            // 保存原始结果
            _originalTable1Results = results1;
            _originalTable2Results = results2;
            _originalTable3Results = results3;
            _originalTable4Results = results4;
            _originalTable5Results = results5;
            _originalTable6Results = results6;
            _originalTable7Results = results7;
            _originalTable8Results = results8;

            // 更新各表格结果（新8个条件不需要默认最小值计算，直接显示）
            UpdateTableResults(_table1Results, results1, 0, 0, 0);
            UpdateTableResults(_table2Results, results2, 0, 0, 0);
            UpdateTableResults(_table3Results, results3, 0, 0, 0);
            UpdateTableResults(_table4Results, results4, 0, 0, 0);
            UpdateTableResults(_table5Results, results5, 0, 0, 0);
            UpdateTableResults(_table6Results, results6, 0, 0, 0);
            UpdateTableResults(_table7Results, results7, 0, 0, 0);
            UpdateTableResults(_table8Results, results8, 0, 0, 0);

            LastUpdateTime = DateTime.Now;

            OnPropertyChanged(nameof(Table1Count));
            OnPropertyChanged(nameof(Table2Count));
            OnPropertyChanged(nameof(Table3Count));
            OnPropertyChanged(nameof(Table4Count));
            OnPropertyChanged(nameof(Table5Count));
            OnPropertyChanged(nameof(Table6Count));
            OnPropertyChanged(nameof(Table7Count));
            OnPropertyChanged(nameof(Table8Count));
        }

        private void UpdateTableResults(
            ObservableCollection<StockResultItem> targetResults,
            List<FilterResultWithHistory> sourceResults,
            decimal weeklyMin,
            decimal monthlyMin,
            decimal quarterlyMin)
        {
            if (sourceResults == null)
            {
                targetResults.Clear();
                return;
            }
            
            // 性能优化：移除重复排序（数据在 UnifiedStockFilter 中已经按涨幅排序）
            // 应用涨幅计算：涨幅为空或涨幅<=阈值时保留
            var filtered = sourceResults.Where(r =>
                r.WeeklyK >= weeklyMin &&
                r.MonthlyK >= monthlyMin &&
                r.QuarterlyK >= quarterlyMin &&
                (!r.PriceChangePercent.HasValue || r.PriceChangePercent.Value <= _priceChangeFilterThreshold))
                .ToList();

            // 性能优化：批量更新，减少UI刷新次数
            // 先创建所有新项，然后一次性替换（避免多次触发UI更新）
            var newItems = filtered.Select(r => CreateStockResultItem(r)).ToList();
            
            // 批量替换：先清空，然后批量添加
            targetResults.Clear();
            foreach (var item in newItems)
            {
                targetResults.Add(item);
            }
        }

        /// <summary>
        /// 创建股票结果项
        /// </summary>
        private StockResultItem CreateStockResultItem(FilterResultWithHistory result)
        {
            return new StockResultItem
            {
                StockCode = result.StockCode,
                StockName = result.StockName ?? result.StockCode,
                PriceChangePercent = result.PriceChangePercent,
                WeeklyK = result.WeeklyK,
                MonthlyK = result.MonthlyK,
                QuarterlyK = result.QuarterlyK,
                WeeklyKColor = GetKValueColor(result.WeeklyK, result.YesterdayWeeklyK),
                MonthlyKColor = GetKValueColor(result.MonthlyK, result.YesterdayMonthlyK),
                QuarterlyKColor = GetKValueColor(result.QuarterlyK, result.YesterdayQuarterlyK),
                PriceChangeColor = GetPriceChangeColor(result.PriceChangePercent)
            };
        }

        /// <summary>
        /// 获取涨幅颜色（红涨绿跌）
        /// </summary>
        private Brush GetPriceChangeColor(decimal? priceChangePercent)
        {
            if (!priceChangePercent.HasValue)
            {
                return new SolidColorBrush(Color.FromRgb(110, 110, 110)); // 无数据，显示灰色
            }

            if (priceChangePercent.Value > 0)
            {
                return new SolidColorBrush(Color.FromRgb(255, 107, 107)); // 上涨，显示红色
            }
            else if (priceChangePercent.Value < 0)
            {
                return new SolidColorBrush(Color.FromRgb(78, 205, 196)); // 下跌，显示绿色
            }
            else
            {
                return new SolidColorBrush(Color.FromRgb(110, 110, 110)); // 平盘，显示灰色
            }
        }

        /// <summary>
        /// 获取K值颜色（红涨绿跌，深色主题优化）
        /// </summary>
        private Brush GetKValueColor(decimal currentK, decimal? yesterdayK)
        {
            if (!yesterdayK.HasValue)
            {
                return new SolidColorBrush(Color.FromRgb(110, 110, 110)); // 无历史数据，显示灰色（#6E6E6E）
            }

            if (currentK > yesterdayK.Value)
            {
                return new SolidColorBrush(Color.FromRgb(255, 107, 107)); // K值上升，显示红色（#FF6B6B，柔和红色）
            }
            else if (currentK < yesterdayK.Value)
            {
                return new SolidColorBrush(Color.FromRgb(78, 205, 196)); // K值下降，显示绿色（#4ECDC4，柔和青色）
            }
            else
            {
                return new SolidColorBrush(Color.FromRgb(110, 110, 110)); // K值不变，显示灰色（#6E6E6E）
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Cleanup()
        {
            // 清理集合
            _table1Results?.Clear();
            _table2Results?.Clear();
            _table3Results?.Clear();
            _table4Results?.Clear();
            _table5Results?.Clear();
            _table6Results?.Clear();
            _table7Results?.Clear();
            _table8Results?.Clear();

            // 清理原始结果引用
            _originalTable1Results = null;
            _originalTable2Results = null;
            _originalTable3Results = null;
            _originalTable4Results = null;
            _originalTable5Results = null;
            _originalTable6Results = null;
            _originalTable7Results = null;
            _originalTable8Results = null;

            // 清理事件订阅
            PropertyChanged = null;
            
            // 取消实时数据缓存订阅
            if (_realTimeCache != null)
            {
                _realTimeCache.DataUpdated -= OnRealTimeDataUpdated;
                _realTimeCache = null;
            }
            
            // 释放节流定时器
            if (_updateThrottleTimer != null)
            {
                _updateThrottleTimer.Dispose();
                _updateThrottleTimer = null;
            }
            
            // 清空待更新集合
            lock (_pendingUpdateLock)
            {
                _pendingUpdateStockCodes.Clear();
            }
        }
    }

    /// <summary>
    /// 股票结果显示项
    /// </summary>
    public class StockResultItem : INotifyPropertyChanged
    {
        private string _stockCode;
        private string _stockName;
        private decimal? _priceChangePercent;
        private decimal _weeklyK;
        private decimal _monthlyK;
        private decimal _quarterlyK;
        private Brush _weeklyKColor;
        private Brush _monthlyKColor;
        private Brush _quarterlyKColor;
        private Brush _priceChangeColor;

        public string StockCode
        {
            get => _stockCode;
            set { _stockCode = value; OnPropertyChanged(nameof(StockCode)); }
        }

        public string StockName
        {
            get => _stockName;
            set { _stockName = value; OnPropertyChanged(nameof(StockName)); }
        }

        public decimal? PriceChangePercent
        {
            get => _priceChangePercent;
            set
            {
                if (_priceChangePercent != value)
                {
                    _priceChangePercent = value;
                    OnPropertyChanged(nameof(PriceChangePercent));
                    OnPropertyChanged(nameof(PriceChangeDisplay)); // 同时通知显示文本更新
                }
            }
        }

        public decimal WeeklyK
        {
            get => _weeklyK;
            set { _weeklyK = value; OnPropertyChanged(nameof(WeeklyK)); }
        }

        public decimal MonthlyK
        {
            get => _monthlyK;
            set { _monthlyK = value; OnPropertyChanged(nameof(MonthlyK)); }
        }

        public decimal QuarterlyK
        {
            get => _quarterlyK;
            set { _quarterlyK = value; OnPropertyChanged(nameof(QuarterlyK)); }
        }

        public Brush WeeklyKColor
        {
            get => _weeklyKColor;
            set { _weeklyKColor = value; OnPropertyChanged(nameof(WeeklyKColor)); }
        }

        public Brush MonthlyKColor
        {
            get => _monthlyKColor;
            set { _monthlyKColor = value; OnPropertyChanged(nameof(MonthlyKColor)); }
        }

        public Brush QuarterlyKColor
        {
            get => _quarterlyKColor;
            set { _quarterlyKColor = value; OnPropertyChanged(nameof(QuarterlyKColor)); }
        }

        public Brush PriceChangeColor
        {
            get => _priceChangeColor;
            set
            {
                if (_priceChangeColor != value)
                {
                    _priceChangeColor = value;
                    OnPropertyChanged(nameof(PriceChangeColor));
                }
            }
        }

        public string PriceChangeDisplay => PriceChangePercent.HasValue ? $"{PriceChangePercent.Value:F2}%" : "--";  // 涨幅显示文本（2位小数）

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
