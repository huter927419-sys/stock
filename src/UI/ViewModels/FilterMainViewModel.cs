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

        // 全局M1/M2/M3阈值（所有8个表格共用）
        private decimal _globalM1 = 78;
        private decimal _globalM2 = 65;
        private decimal _globalM3 = 50;

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

        // 存储原始结果（用于过滤）
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

        // 全局M1/M2/M3阈值属性（所有8个表格共用）
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

        // 全局阈值显示文本
        public string GlobalThresholdDisplay => $"M1({_globalM1}), M2({_globalM2}), M3({_globalM3})";

        /// <summary>
        /// 刷新过滤结果（当默认最小值改变时调用）
        /// </summary>
        public void RefreshFilteredResults()
        {
            for (int i = 1; i <= 6; i++)
            {
                FilterResultsByDefaultMin(i);
            }
        }

        /// <summary>
        /// 根据默认最小值过滤结果
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
                    r.QuarterlyK >= quarterlyMin).ToList();

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

            // 更新各表格结果（新8个条件不需要默认最小值过滤，直接显示）
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
            targetResults.Clear();
            if (sourceResults != null)
            {
                var filtered = sourceResults.Where(r =>
                    r.WeeklyK >= weeklyMin &&
                    r.MonthlyK >= monthlyMin &&
                    r.QuarterlyK >= quarterlyMin).ToList();

                foreach (var result in filtered)
                {
                    targetResults.Add(CreateStockResultItem(result));
                }
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
        }
    }

    /// <summary>
    /// 股票结果显示项
    /// </summary>
    public class StockResultItem
    {
        public string StockCode { get; set; }
        public string StockName { get; set; }
        public decimal? PriceChangePercent { get; set; }  // 涨幅百分比
        public decimal WeeklyK { get; set; }
        public decimal MonthlyK { get; set; }
        public decimal QuarterlyK { get; set; }
        public Brush WeeklyKColor { get; set; }
        public Brush MonthlyKColor { get; set; }
        public Brush QuarterlyKColor { get; set; }
        public Brush PriceChangeColor { get; set; }  // 涨幅颜色
        public string PriceChangeDisplay => PriceChangePercent.HasValue ? $"{PriceChangePercent.Value:F2}%" : "--";  // 涨幅显示文本
    }
}
