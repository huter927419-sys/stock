using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;
using MQReceiver.Models;

namespace MQReceiver.ViewModels
{
    /// <summary>
    /// 主窗口视图模型
    /// </summary>
    public class FilterMainViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<StockResultItem> _condition1Results;
        private ObservableCollection<StockResultItem> _condition2Results;
        private ObservableCollection<StockResultItem> _condition3Results;
        private DateTime _lastUpdateTime;
        private static readonly Configuration.IConfigurationProvider _configProvider = Configuration.AppConfigProvider.Instance;

        // 条件1的默认最小值 (金叉条件，建议较低阈值)
        private decimal _condition1WeeklyKDefaultMin = 30;
        private decimal _condition1MonthlyKDefaultMin = 25;
        private decimal _condition1QuarterlyKDefaultMin = 20;
        private bool _condition1WeeklyKSelected = true;
        private bool _condition1MonthlyKSelected = false;
        private bool _condition1QuarterlyKSelected = false;

        // 条件2的默认最小值 (周K>月K>季K，趋势向上)
        private decimal _condition2WeeklyKDefaultMin = 35;
        private decimal _condition2MonthlyKDefaultMin = 30;
        private decimal _condition2QuarterlyKDefaultMin = 25;
        private bool _condition2WeeklyKSelected = true;
        private bool _condition2MonthlyKSelected = false;
        private bool _condition2QuarterlyKSelected = false;

        // 条件3的默认最小值 (月K>季K或周K<月K)
        private decimal _condition3WeeklyKDefaultMin = 30;
        private decimal _condition3MonthlyKDefaultMin = 25;
        private decimal _condition3QuarterlyKDefaultMin = 20;
        private bool _condition3WeeklyKSelected = true;
        private bool _condition3MonthlyKSelected = false;
        private bool _condition3QuarterlyKSelected = false;

        public FilterMainViewModel()
        {
            _condition1Results = new ObservableCollection<StockResultItem>();
            _condition2Results = new ObservableCollection<StockResultItem>();
            _condition3Results = new ObservableCollection<StockResultItem>();
            _lastUpdateTime = DateTime.Now;

            // 从配置文件加载默认值
            LoadDefaultValuesFromConfig();
        }

        /// <summary>
        /// 从配置文件加载默认值
        /// </summary>
        private void LoadDefaultValuesFromConfig()
        {
            try
            {
                // 条件1 (金叉条件，建议较低阈值：周K>=30, 月K>=25, 季K>=20)
                _condition1WeeklyKDefaultMin = _configProvider.GetDecimal("Filter1_WeeklyKDefaultMin", 30);
                _condition1MonthlyKDefaultMin = _configProvider.GetDecimal("Filter1_MonthlyKDefaultMin", 25);
                _condition1QuarterlyKDefaultMin = _configProvider.GetDecimal("Filter1_QuarterlyKDefaultMin", 20);

                // 条件2 (周K>月K>季K，趋势向上：周K>=35, 月K>=30, 季K>=25)
                _condition2WeeklyKDefaultMin = _configProvider.GetDecimal("Filter2_WeeklyKDefaultMin", 35);
                _condition2MonthlyKDefaultMin = _configProvider.GetDecimal("Filter2_MonthlyKDefaultMin", 30);
                _condition2QuarterlyKDefaultMin = _configProvider.GetDecimal("Filter2_QuarterlyKDefaultMin", 25);

                // 条件3 (月K>季K或周K<月K：周K>=30, 月K>=25, 季K>=20)
                _condition3WeeklyKDefaultMin = _configProvider.GetDecimal("Filter3_WeeklyKDefaultMin", 30);
                _condition3MonthlyKDefaultMin = _configProvider.GetDecimal("Filter3_MonthlyKDefaultMin", 25);
                _condition3QuarterlyKDefaultMin = _configProvider.GetDecimal("Filter3_QuarterlyKDefaultMin", 20);
            }
            catch
            {
                // 使用字段初始化的默认值
            }
        }

        public ObservableCollection<StockResultItem> Condition1Results
        {
            get { return _condition1Results; }
            set
            {
                _condition1Results = value;
                OnPropertyChanged(nameof(Condition1Results));
                OnPropertyChanged(nameof(Condition1Count));
            }
        }

        public ObservableCollection<StockResultItem> Condition2Results
        {
            get { return _condition2Results; }
            set
            {
                _condition2Results = value;
                OnPropertyChanged(nameof(Condition2Results));
                OnPropertyChanged(nameof(Condition2Count));
            }
        }

        public ObservableCollection<StockResultItem> Condition3Results
        {
            get { return _condition3Results; }
            set
            {
                _condition3Results = value;
                OnPropertyChanged(nameof(Condition3Results));
                OnPropertyChanged(nameof(Condition3Count));
            }
        }

        public int Condition1Count => _condition1Results?.Count ?? 0;
        public int Condition2Count => _condition2Results?.Count ?? 0;
        public int Condition3Count => _condition3Results?.Count ?? 0;

        public DateTime LastUpdateTime
        {
            get { return _lastUpdateTime; }
            set
            {
                _lastUpdateTime = value;
                OnPropertyChanged(nameof(LastUpdateTime));
            }
        }

        // 存储原始结果（用于过滤）
        private List<FilterResultWithHistory> _originalCondition1Results;
        private List<FilterResultWithHistory> _originalCondition2Results;
        private List<FilterResultWithHistory> _originalCondition3Results;

        // 条件1的默认最小值属性
        public decimal Condition1WeeklyKDefaultMin
        {
            get { return _condition1WeeklyKDefaultMin; }
            set
            {
                if (_condition1WeeklyKDefaultMin != value)
                {
                    _condition1WeeklyKDefaultMin = value;
                    _configProvider.SetDecimal("Filter1_WeeklyKDefaultMin", value);
                    OnPropertyChanged(nameof(Condition1WeeklyKDefaultMin));
                    OnPropertyChanged(nameof(Condition1SettingsDisplay));
                    FilterResultsByDefaultMin(1);
                }
            }
        }

        public decimal Condition1MonthlyKDefaultMin
        {
            get { return _condition1MonthlyKDefaultMin; }
            set
            {
                if (_condition1MonthlyKDefaultMin != value)
                {
                    _condition1MonthlyKDefaultMin = value;
                    _configProvider.SetDecimal("Filter1_MonthlyKDefaultMin", value);
                    OnPropertyChanged(nameof(Condition1MonthlyKDefaultMin));
                    OnPropertyChanged(nameof(Condition1SettingsDisplay));
                    FilterResultsByDefaultMin(1);
                }
            }
        }

        public decimal Condition1QuarterlyKDefaultMin
        {
            get { return _condition1QuarterlyKDefaultMin; }
            set
            {
                if (_condition1QuarterlyKDefaultMin != value)
                {
                    _condition1QuarterlyKDefaultMin = value;
                    _configProvider.SetDecimal("Filter1_QuarterlyKDefaultMin", value);
                    OnPropertyChanged(nameof(Condition1QuarterlyKDefaultMin));
                    OnPropertyChanged(nameof(Condition1SettingsDisplay));
                    FilterResultsByDefaultMin(1);
                }
            }
        }

        public bool Condition1WeeklyKSelected
        {
            get { return _condition1WeeklyKSelected; }
            set
            {
                if (_condition1WeeklyKSelected != value)
                {
                    _condition1WeeklyKSelected = value;
                    OnPropertyChanged(nameof(Condition1WeeklyKSelected));
                }
            }
        }

        public bool Condition1MonthlyKSelected
        {
            get { return _condition1MonthlyKSelected; }
            set
            {
                if (_condition1MonthlyKSelected != value)
                {
                    _condition1MonthlyKSelected = value;
                    OnPropertyChanged(nameof(Condition1MonthlyKSelected));
                }
            }
        }

        public bool Condition1QuarterlyKSelected
        {
            get { return _condition1QuarterlyKSelected; }
            set
            {
                if (_condition1QuarterlyKSelected != value)
                {
                    _condition1QuarterlyKSelected = value;
                    OnPropertyChanged(nameof(Condition1QuarterlyKSelected));
                }
            }
        }

        // 条件2的默认最小值属性
        public decimal Condition2WeeklyKDefaultMin
        {
            get { return _condition2WeeklyKDefaultMin; }
            set
            {
                if (_condition2WeeklyKDefaultMin != value)
                {
                    _condition2WeeklyKDefaultMin = value;
                    _configProvider.SetDecimal("Filter2_WeeklyKDefaultMin", value);
                    OnPropertyChanged(nameof(Condition2WeeklyKDefaultMin));
                    OnPropertyChanged(nameof(Condition2SettingsDisplay));
                    FilterResultsByDefaultMin(2);
                }
            }
        }

        public decimal Condition2MonthlyKDefaultMin
        {
            get { return _condition2MonthlyKDefaultMin; }
            set
            {
                if (_condition2MonthlyKDefaultMin != value)
                {
                    _condition2MonthlyKDefaultMin = value;
                    _configProvider.SetDecimal("Filter2_MonthlyKDefaultMin", value);
                    OnPropertyChanged(nameof(Condition2MonthlyKDefaultMin));
                    OnPropertyChanged(nameof(Condition2SettingsDisplay));
                    FilterResultsByDefaultMin(2);
                }
            }
        }

        public decimal Condition2QuarterlyKDefaultMin
        {
            get { return _condition2QuarterlyKDefaultMin; }
            set
            {
                if (_condition2QuarterlyKDefaultMin != value)
                {
                    _condition2QuarterlyKDefaultMin = value;
                    _configProvider.SetDecimal("Filter2_QuarterlyKDefaultMin", value);
                    OnPropertyChanged(nameof(Condition2QuarterlyKDefaultMin));
                    OnPropertyChanged(nameof(Condition2SettingsDisplay));
                    FilterResultsByDefaultMin(2);
                }
            }
        }

        public bool Condition2WeeklyKSelected
        {
            get { return _condition2WeeklyKSelected; }
            set
            {
                if (_condition2WeeklyKSelected != value)
                {
                    _condition2WeeklyKSelected = value;
                    OnPropertyChanged(nameof(Condition2WeeklyKSelected));
                }
            }
        }

        public bool Condition2MonthlyKSelected
        {
            get { return _condition2MonthlyKSelected; }
            set
            {
                if (_condition2MonthlyKSelected != value)
                {
                    _condition2MonthlyKSelected = value;
                    OnPropertyChanged(nameof(Condition2MonthlyKSelected));
                }
            }
        }

        public bool Condition2QuarterlyKSelected
        {
            get { return _condition2QuarterlyKSelected; }
            set
            {
                if (_condition2QuarterlyKSelected != value)
                {
                    _condition2QuarterlyKSelected = value;
                    OnPropertyChanged(nameof(Condition2QuarterlyKSelected));
                }
            }
        }

        // 条件3的默认最小值属性
        public decimal Condition3WeeklyKDefaultMin
        {
            get { return _condition3WeeklyKDefaultMin; }
            set
            {
                if (_condition3WeeklyKDefaultMin != value)
                {
                    _condition3WeeklyKDefaultMin = value;
                    _configProvider.SetDecimal("Filter3_WeeklyKDefaultMin", value);
                    OnPropertyChanged(nameof(Condition3WeeklyKDefaultMin));
                    OnPropertyChanged(nameof(Condition3SettingsDisplay));
                    FilterResultsByDefaultMin(3);
                }
            }
        }

        public decimal Condition3MonthlyKDefaultMin
        {
            get { return _condition3MonthlyKDefaultMin; }
            set
            {
                if (_condition3MonthlyKDefaultMin != value)
                {
                    _condition3MonthlyKDefaultMin = value;
                    _configProvider.SetDecimal("Filter3_MonthlyKDefaultMin", value);
                    OnPropertyChanged(nameof(Condition3MonthlyKDefaultMin));
                    OnPropertyChanged(nameof(Condition3SettingsDisplay));
                    FilterResultsByDefaultMin(3);
                }
            }
        }

        public decimal Condition3QuarterlyKDefaultMin
        {
            get { return _condition3QuarterlyKDefaultMin; }
            set
            {
                if (_condition3QuarterlyKDefaultMin != value)
                {
                    _condition3QuarterlyKDefaultMin = value;
                    _configProvider.SetDecimal("Filter3_QuarterlyKDefaultMin", value);
                    OnPropertyChanged(nameof(Condition3QuarterlyKDefaultMin));
                    OnPropertyChanged(nameof(Condition3SettingsDisplay));
                    FilterResultsByDefaultMin(3);
                }
            }
        }

        public bool Condition3WeeklyKSelected
        {
            get { return _condition3WeeklyKSelected; }
            set
            {
                if (_condition3WeeklyKSelected != value)
                {
                    _condition3WeeklyKSelected = value;
                    OnPropertyChanged(nameof(Condition3WeeklyKSelected));
                }
            }
        }

        public bool Condition3MonthlyKSelected
        {
            get { return _condition3MonthlyKSelected; }
            set
            {
                if (_condition3MonthlyKSelected != value)
                {
                    _condition3MonthlyKSelected = value;
                    OnPropertyChanged(nameof(Condition3MonthlyKSelected));
                }
            }
        }

        public bool Condition3QuarterlyKSelected
        {
            get { return _condition3QuarterlyKSelected; }
            set
            {
                if (_condition3QuarterlyKSelected != value)
                {
                    _condition3QuarterlyKSelected = value;
                    OnPropertyChanged(nameof(Condition3QuarterlyKSelected));
                }
            }
        }

        // 设置显示文本属性
        public string Condition1SettingsDisplay => $"周K({_condition1WeeklyKDefaultMin}), 月K({_condition1MonthlyKDefaultMin}), 季K({_condition1QuarterlyKDefaultMin})";
        public string Condition2SettingsDisplay => $"周K({_condition2WeeklyKDefaultMin}), 月K({_condition2MonthlyKDefaultMin}), 季K({_condition2QuarterlyKDefaultMin})";
        public string Condition3SettingsDisplay => $"周K({_condition3WeeklyKDefaultMin}), 月K({_condition3MonthlyKDefaultMin}), 季K({_condition3QuarterlyKDefaultMin})";

        /// <summary>
        /// 刷新过滤结果（当默认最小值改变时调用）
        /// </summary>
        public void RefreshFilteredResults()
        {
            FilterResultsByDefaultMin(1);
            FilterResultsByDefaultMin(2);
            FilterResultsByDefaultMin(3);
        }

        /// <summary>
        /// 根据默认最小值过滤结果
        /// </summary>
        private void FilterResultsByDefaultMin(int conditionNumber)
        {
            if (conditionNumber == 1 && _originalCondition1Results != null)
            {
                var filtered = _originalCondition1Results.Where(r =>
                    r.WeeklyK >= _condition1WeeklyKDefaultMin &&
                    r.MonthlyK >= _condition1MonthlyKDefaultMin &&
                    r.QuarterlyK >= _condition1QuarterlyKDefaultMin).ToList();

                _condition1Results.Clear();
                foreach (var item in filtered)
                {
                    _condition1Results.Add(CreateStockResultItem(item));
                }
                OnPropertyChanged(nameof(Condition1Count));
            }
            else if (conditionNumber == 2 && _originalCondition2Results != null)
            {
                var filtered = _originalCondition2Results.Where(r =>
                    r.WeeklyK >= _condition2WeeklyKDefaultMin &&
                    r.MonthlyK >= _condition2MonthlyKDefaultMin &&
                    r.QuarterlyK >= _condition2QuarterlyKDefaultMin).ToList();

                _condition2Results.Clear();
                foreach (var item in filtered)
                {
                    _condition2Results.Add(CreateStockResultItem(item));
                }
                OnPropertyChanged(nameof(Condition2Count));
            }
            else if (conditionNumber == 3 && _originalCondition3Results != null)
            {
                var filtered = _originalCondition3Results.Where(r =>
                    r.WeeklyK >= _condition3WeeklyKDefaultMin &&
                    r.MonthlyK >= _condition3MonthlyKDefaultMin &&
                    r.QuarterlyK >= _condition3QuarterlyKDefaultMin).ToList();

                _condition3Results.Clear();
                foreach (var item in filtered)
                {
                    _condition3Results.Add(CreateStockResultItem(item));
                }
                OnPropertyChanged(nameof(Condition3Count));
            }
        }

        /// <summary>
        /// 更新所有结果
        /// </summary>
        public void UpdateResults(
            List<FilterResultWithHistory> results1,
            List<FilterResultWithHistory> results2,
            List<FilterResultWithHistory> results3)
        {
            // 保存原始结果
            _originalCondition1Results = results1;
            _originalCondition2Results = results2;
            _originalCondition3Results = results3;

            // 根据默认最小值过滤并更新条件1结果
            _condition1Results.Clear();
            if (results1 != null)
            {
                var filtered = results1.Where(r =>
                    r.WeeklyK >= _condition1WeeklyKDefaultMin &&
                    r.MonthlyK >= _condition1MonthlyKDefaultMin &&
                    r.QuarterlyK >= _condition1QuarterlyKDefaultMin).ToList();

                foreach (var result in filtered)
                {
                    _condition1Results.Add(CreateStockResultItem(result));
                }
            }

            // 根据默认最小值过滤并更新条件2结果
            _condition2Results.Clear();
            if (results2 != null)
            {
                var filtered = results2.Where(r =>
                    r.WeeklyK >= _condition2WeeklyKDefaultMin &&
                    r.MonthlyK >= _condition2MonthlyKDefaultMin &&
                    r.QuarterlyK >= _condition2QuarterlyKDefaultMin).ToList();

                foreach (var result in filtered)
                {
                    _condition2Results.Add(CreateStockResultItem(result));
                }
            }

            // 根据默认最小值过滤并更新条件3结果
            _condition3Results.Clear();
            if (results3 != null)
            {
                var filtered = results3.Where(r =>
                    r.WeeklyK >= _condition3WeeklyKDefaultMin &&
                    r.MonthlyK >= _condition3MonthlyKDefaultMin &&
                    r.QuarterlyK >= _condition3QuarterlyKDefaultMin).ToList();

                foreach (var result in filtered)
                {
                    _condition3Results.Add(CreateStockResultItem(result));
                }
            }

            LastUpdateTime = DateTime.Now;

            OnPropertyChanged(nameof(Condition1Count));
            OnPropertyChanged(nameof(Condition2Count));
            OnPropertyChanged(nameof(Condition3Count));
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
                WeeklyK = result.WeeklyK,
                MonthlyK = result.MonthlyK,
                QuarterlyK = result.QuarterlyK,
                WeeklyKColor = GetKValueColor(result.WeeklyK, result.YesterdayWeeklyK),
                MonthlyKColor = GetKValueColor(result.MonthlyK, result.YesterdayMonthlyK),
                QuarterlyKColor = GetKValueColor(result.QuarterlyK, result.YesterdayQuarterlyK)
            };
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
            _condition1Results?.Clear();
            _condition2Results?.Clear();
            _condition3Results?.Clear();

            // 清理原始结果引用
            _originalCondition1Results = null;
            _originalCondition2Results = null;
            _originalCondition3Results = null;

            // 清理事件订阅
            PropertyChanged = null;
        }
    }

    /// <summary>
    /// 股票结果显示项
    /// </summary>
    public class StockResultItem
    {
        public string StockCode { get; set; }
        public string StockName { get; set; }
        public decimal WeeklyK { get; set; }
        public decimal MonthlyK { get; set; }
        public decimal QuarterlyK { get; set; }
        public Brush WeeklyKColor { get; set; }
        public Brush MonthlyKColor { get; set; }
        public Brush QuarterlyKColor { get; set; }
    }
}
