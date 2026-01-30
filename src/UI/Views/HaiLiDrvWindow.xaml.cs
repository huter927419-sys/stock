using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using MQReceiver.Cache;
using MQReceiver.Configuration;
using MQReceiver.Helpers;
using MQReceiver.Models;
using MQReceiver.Services;

namespace MQReceiver.Views
{
    /// <summary>
    /// HaiLiDrvWindow.xaml 的交互逻辑
    /// 海利驱动数据窗口（WPF版本）
    /// 从内存缓存获取数据，替代原有的DLL调用方式
    /// </summary>
    public partial class HaiLiDrvWindow : Window
    {
        private RealTimeDataCache _realTimeCache;
        private ChartWindowManager _chartWindowManager;
        private DispatcherTimer _refreshTimer;
        private bool _isStandaloneMode;
        private IConfigurationProvider _configProvider;
        private HaiLiDrvDataService _dataService;
        private List<Services.HaiLiDataItem> _allDataItems; // 存储所有数据（未过滤）
        private List<Services.HaiLiDataItem> _filteredDataItems; // 存储过滤后的数据（复用）
        private string _currentSearchText = ""; // 当前搜索文本
        private List<HaiLiDrvPanel> _panels = new List<HaiLiDrvPanel>(); // 面板列表

        /// <summary>
        /// 构造函数（集成模式：从主窗口调用）
        /// </summary>
        public HaiLiDrvWindow(RealTimeDataCache realTimeCache) : this(realTimeCache, false)
        {
        }

        /// <summary>
        /// 构造函数（支持独立模式和集成模式）
        /// </summary>
        /// <param name="realTimeCache">实时数据缓存（独立模式时可为null，会创建新的）</param>
        /// <param name="isStandaloneMode">是否为独立模式</param>
        public HaiLiDrvWindow(RealTimeDataCache realTimeCache, bool isStandaloneMode = false)
        {
            InitializeComponent();
            _isStandaloneMode = isStandaloneMode;

            // 根据模式选择配置提供者（使用ConfigurationHelper统一处理）
            _configProvider = Helpers.ConfigurationHelper.GetConfigProvider(_isStandaloneMode);
            Console.WriteLine($"[HaiLiDrvWindow] {(_isStandaloneMode ? "独立" : "集成")}模式：使用{(_isStandaloneMode ? "HaiLiDrv.config" : "App.config")}");

            // 处理缓存（优先使用外部提供的缓存，否则根据模式创建）
            if (realTimeCache != null)
            {
                _realTimeCache = realTimeCache;
                Console.WriteLine("[HaiLiDrvWindow] 使用外部提供的缓存");
            }
            else
            {
                // 根据模式选择：独立模式创建独立缓存，集成模式使用共享缓存
                if (_isStandaloneMode)
                {
                    _realTimeCache = Helpers.CacheManager.CreateStandaloneCache();
                    Console.WriteLine("[HaiLiDrvWindow] 创建独立缓存实例");
                }
                else
                {
                    _realTimeCache = Helpers.CacheManager.GetOrCreateSharedCache();
                    Console.WriteLine("[HaiLiDrvWindow] 使用共享缓存");
                }
            }

            _chartWindowManager = ChartWindowManager.Instance;
            _chartWindowManager.SetRealTimeCache(_realTimeCache);

            // 创建数据服务（自动选择实时数据或日线数据，使用当前配置提供者）
            _dataService = new HaiLiDrvDataService(_realTimeCache, _configProvider);

            _allDataItems = new List<Services.HaiLiDataItem>(500);
            _filteredDataItems = new List<Services.HaiLiDataItem>(500);
            
            // 设置搜索框提示文本
            SearchTextBox.Text = "";
            SearchTextBox.GotFocus += SearchTextBox_GotFocus;
            SearchTextBox.LostFocus += SearchTextBox_LostFocus;
            
            // 初始化面板
            InitializePanels();

            // 设置窗口在附加屏（第二个屏幕）上显示
            SetupSecondaryScreen();

            // 启动定时刷新
            StartAutoRefresh();

            // 窗口关闭时清理
            this.Closed += HaiLiDrvWindow_Closed;

            // 初始加载数据
            RefreshData();
        }

        /// <summary>
        /// 初始化面板
        /// </summary>
        private void InitializePanels()
        {
            try
            {
                // 从配置读取面板数量（默认1个，最多200个）
                int panelCount = _configProvider.GetInt("HaiLiDrv_PanelCount", 1);
                if (panelCount < 1) panelCount = 1;
                if (panelCount > 200) panelCount = 200;
                
                Console.WriteLine($"[HaiLiDrvWindow] 初始化 {panelCount} 个面板");
                
                // 确保 PanelsContainer 已初始化
                if (PanelsContainer == null)
                {
                    Console.WriteLine("[HaiLiDrvWindow] 错误: PanelsContainer 未初始化");
                    return;
                }
                
                // 清除现有面板
                foreach (var panel in _panels)
                {
                    if (panel != null)
                    {
                        PanelsContainer.Children.Remove(panel);
                        panel.StockDoubleClick -= Panel_StockDoubleClick;
                    }
                }
                _panels.Clear();
                
                // 创建新面板
                for (int i = 0; i < panelCount; i++)
                {
                    var panel = new HaiLiDrvPanel();
                    panel.PanelName = $"面板{i + 1}";
                    panel.StockDoubleClick += Panel_StockDoubleClick;
                    
                    // 从配置读取该面板的股票代码列表
                    string panelStockCodesKey = $"HaiLiDrv_Panel{i + 1}_StockCodes";
                    string stockCodesConfig = _configProvider.GetString(panelStockCodesKey, "");
                    if (!string.IsNullOrWhiteSpace(stockCodesConfig))
                    {
                        var codes = stockCodesConfig.Split(new[] { ',', ';', ' ', '\t', '\n', '\r' }, 
                            StringSplitOptions.RemoveEmptyEntries)
                            .Select(c => c.Trim())
                            .Where(c => !string.IsNullOrEmpty(c))
                            .ToList();
                        panel.SetStockCodes(codes);
                        Console.WriteLine($"[HaiLiDrvWindow] 面板{i + 1} 配置了 {codes.Count} 个股票代码");
                    }
                    
                    // 设置面板大小
                    panel.Width = _configProvider.GetInt($"HaiLiDrv_Panel{i + 1}_Width", 400);
                    panel.Height = _configProvider.GetInt($"HaiLiDrv_Panel{i + 1}_Height", 300);
                    
                    PanelsContainer.Children.Add(panel);
                    _panels.Add(panel);
                }
                
                Console.WriteLine($"[HaiLiDrvWindow] 已创建 {_panels.Count} 个面板");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HaiLiDrvWindow] 初始化面板失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 面板中股票双击事件处理
        /// </summary>
        private void Panel_StockDoubleClick(object sender, string stockCode)
        {
            _chartWindowManager.OpenChartWindow(stockCode);
        }

        /// <summary>
        /// 设置窗口在附加屏（第二个屏幕）上显示
        /// 独立模式：从HaiLiDrv.config读取位置
        /// 集成模式：从App.config读取位置
        /// </summary>
        private void SetupSecondaryScreen()
        {
            try
            {
                // 尝试从配置文件恢复窗口位置
                string sl = _configProvider.GetString("HaiLiDrvWindow_Left");
                string st = _configProvider.GetString("HaiLiDrvWindow_Top");
                string sw = _configProvider.GetString("HaiLiDrvWindow_Width");
                string sh = _configProvider.GetString("HaiLiDrvWindow_Height");

                if (!string.IsNullOrEmpty(sl) && !string.IsNullOrEmpty(st) && 
                    !string.IsNullOrEmpty(sw) && !string.IsNullOrEmpty(sh))
                {
                    if (double.TryParse(sl, out double left) &&
                        double.TryParse(st, out double top) &&
                        double.TryParse(sw, out double width) &&
                        double.TryParse(sh, out double height))
                    {
                        // 检查保存的位置是否在某个屏幕上
                        if (IsPointOnAnyScreen(left, top))
                        {
                            this.WindowStartupLocation = WindowStartupLocation.Manual;
                            this.Left = left;
                            this.Top = top;
                            this.Width = width;
                            this.Height = height;
                            this.WindowState = WindowState.Maximized;
                            Console.WriteLine($"[HaiLiDrvWindow] 从配置文件恢复窗口位置: ({left}, {top}, {width}, {height})");
                            return;
                        }
                    }
                }

                // 如果没有保存的位置或位置无效，自动检测屏幕
                var screens = Screen.AllScreens;
                if (screens.Length > 1)
                {
                    // 使用第二个屏幕（索引1）
                    var secondaryScreen = screens[1];
                    var bounds = secondaryScreen.Bounds;
                    
                    this.WindowStartupLocation = WindowStartupLocation.Manual;
                    this.Left = bounds.Left;
                    this.Top = bounds.Top;
                    this.Width = bounds.Width;
                    this.Height = bounds.Height;
                    this.WindowState = WindowState.Maximized;
                    
                    Console.WriteLine($"[HaiLiDrvWindow] 窗口已设置到附加屏: {bounds}");
                }
                else
                {
                    // 只有一个屏幕，使用主屏
                    var primaryScreen = Screen.PrimaryScreen;
                    var bounds = primaryScreen.Bounds;
                    
                    this.WindowStartupLocation = WindowStartupLocation.Manual;
                    this.Left = bounds.Left + bounds.Width / 4;
                    this.Top = bounds.Top + bounds.Height / 4;
                    this.Width = bounds.Width / 2;
                    this.Height = bounds.Height / 2;
                    
                    Console.WriteLine($"[HaiLiDrvWindow] 只有一个屏幕，窗口设置在主屏: {bounds}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HaiLiDrvWindow] 设置屏幕位置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 判断点是否在任意屏幕上
        /// </summary>
        private bool IsPointOnAnyScreen(double x, double y)
        {
            try
            {
                foreach (var s in Screen.AllScreens)
                {
                    var b = s.Bounds;
                    if (x >= b.Left && x <= b.Right && y >= b.Top && y <= b.Bottom)
                        return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 启动自动刷新定时器
        /// </summary>
        private void StartAutoRefresh()
        {
            _refreshTimer = new DispatcherTimer();
            // 从配置文件读取刷新间隔
            int intervalSeconds = _configProvider.GetInt("HaiLiDrv_RefreshIntervalSeconds", 3);
            _refreshTimer.Interval = TimeSpan.FromSeconds(intervalSeconds);
            _refreshTimer.Tick += (s, e) => RefreshData();
            _refreshTimer.Start();
            Console.WriteLine($"[HaiLiDrvWindow] 自动刷新间隔: {intervalSeconds}秒");
        }

        /// <summary>
        /// 刷新数据（自动选择实时数据或日线数据）
        /// </summary>
        private void RefreshData()
        {
            try
            {
                // 从配置读取最大显示条数
                int maxDisplayCount = _configProvider.GetInt("HaiLiDrv_MaxDisplayCount", 500);

                // 使用数据服务获取数据（自动判断是实时数据还是日线数据）
                var items = _dataService.GetAllStockData(maxDisplayCount);

                // 更新UI（在UI线程）
                Dispatcher.Invoke(() =>
                {
                    // 保存所有数据（未过滤）- 复用List，减少GC压力
                    _allDataItems.Clear();
                    _allDataItems.Capacity = Math.Max(_allDataItems.Capacity, items.Count);
                    _allDataItems.AddRange(items);
                    
                    // 应用搜索过滤（如果有搜索条件）- 复用List
                    List<Services.HaiLiDataItem> filteredData;
                    if (!string.IsNullOrWhiteSpace(_currentSearchText))
                    {
                        filteredData = ApplySearchFilterToList(_allDataItems, _currentSearchText);
                    }
                    else
                    {
                        filteredData = _allDataItems;
                    }
                    
                    // 更新所有面板
                    foreach (var panel in _panels)
                    {
                        panel.UpdateData(filteredData);
                    }
                    
                    string dataSource = _dataService.IsTradingTime() ? "实时数据" : "日线数据";
                    string filterInfo = GetFilterInfo();
                    int totalCount = _allDataItems.Count;
                    int displayCount = filteredData.Count;
                    string searchInfo = string.IsNullOrEmpty(_currentSearchText) ? "" : $" | 搜索: {displayCount}/{totalCount}";
                    string panelInfo = $" | 面板: {_panels.Count}";
                    StatusText.Text = $"已加载 {totalCount} 条数据 ({dataSource}){filterInfo}{searchInfo}{panelInfo}";
                    InfoText.Text = $"最后更新: {DateTime.Now:HH:mm:ss} | 共 {displayCount} 条记录 | 数据源: {dataSource}{filterInfo}{searchInfo}{panelInfo}";
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HaiLiDrvWindow] 刷新数据失败: {ex.Message}");
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = $"刷新失败: {ex.Message}";
                });
            }
        }

        /// <summary>
        /// 刷新按钮点击
        /// </summary>
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshData();
        }

        /// <summary>
        /// 获取过滤信息文本
        /// </summary>
        private string GetFilterInfo()
        {
            try
            {
                bool enableFilter = _configProvider.GetBool("HaiLiDrv_EnableStockCodeFilter", false);
                if (enableFilter)
                {
                    string stockCodesConfig = _configProvider.GetString("HaiLiDrv_StockCodes", "");
                    if (!string.IsNullOrWhiteSpace(stockCodesConfig))
                    {
                        var codes = stockCodesConfig.Split(new[] { ',', ';', ' ', '\t', '\n', '\r' }, 
                            StringSplitOptions.RemoveEmptyEntries);
                        return $" | 已过滤: {codes.Length} 个股票代码";
                    }
                }
            }
            catch { }
            return "";
        }

        /// <summary>
        /// 应用搜索过滤到列表（返回过滤后的列表）
        /// </summary>
        private List<Services.HaiLiDataItem> ApplySearchFilterToList(List<Services.HaiLiDataItem> sourceList, string searchText)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    return sourceList; // 直接返回，不创建新列表
                }
                
                // 复用List，减少GC压力
                _filteredDataItems.Clear();
                _filteredDataItems.Capacity = Math.Max(_filteredDataItems.Capacity, sourceList.Count);
                
                // 应用搜索过滤（支持股票代码和股票名称）
                string searchLower = searchText.Trim().ToLower();
                
                foreach (var item in sourceList)
                {
                    bool matches = false;
                    
                    // 匹配股票代码
                    if (!string.IsNullOrEmpty(item.StockCode) && 
                        item.StockCode.ToLower().Contains(searchLower))
                    {
                        matches = true;
                    }
                    
                    // 匹配股票名称
                    if (!matches && !string.IsNullOrEmpty(item.StockName) && 
                        item.StockName.ToLower().Contains(searchLower))
                    {
                        matches = true;
                    }
                    
                    if (matches)
                    {
                        _filteredDataItems.Add(item);
                    }
                }
                
                // 返回新列表（因为调用者可能会修改），但复用内部缓冲区
                return new List<Services.HaiLiDataItem>(_filteredDataItems);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HaiLiDrvWindow] 应用搜索过滤失败: {ex.Message}");
                return new List<Services.HaiLiDataItem>(sourceList);
            }
        }

        /// <summary>
        /// 搜索框文本变化
        /// </summary>
        private void SearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            _currentSearchText = SearchTextBox.Text;
            
            // 显示/隐藏清除按钮
            if (string.IsNullOrWhiteSpace(_currentSearchText))
            {
                ClearSearchButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                ClearSearchButton.Visibility = Visibility.Visible;
            }
            
            // 应用搜索过滤并更新所有面板
            if (_allDataItems.Count > 0)
            {
                var filteredData = ApplySearchFilterToList(_allDataItems, _currentSearchText);
                
                // 更新所有面板
                foreach (var panel in _panels)
                {
                    panel.UpdateData(filteredData);
                }
                
                // 更新状态栏
                int totalCount = _allDataItems.Count;
                int displayCount = filteredData.Count;
                string dataSource = _dataService.IsTradingTime() ? "实时数据" : "日线数据";
                string filterInfo = GetFilterInfo();
                string panelInfo = $" | 面板: {_panels.Count}";
                
                if (!string.IsNullOrWhiteSpace(_currentSearchText))
                {
                    StatusText.Text = $"搜索: {displayCount}/{totalCount} 条 ({dataSource}){filterInfo}{panelInfo}";
                    InfoText.Text = $"搜索: \"{_currentSearchText}\" | 显示 {displayCount}/{totalCount} 条记录 | 数据源: {dataSource}{filterInfo}{panelInfo}";
                }
                else
                {
                    StatusText.Text = $"已加载 {totalCount} 条数据 ({dataSource}){filterInfo}{panelInfo}";
                    InfoText.Text = $"最后更新: {DateTime.Now:HH:mm:ss} | 共 {displayCount} 条记录 | 数据源: {dataSource}{filterInfo}{panelInfo}";
                }
            }
        }

        /// <summary>
        /// 搜索框获得焦点
        /// </summary>
        private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // 获得焦点时，如果搜索框为空，可以显示提示
            if (string.IsNullOrWhiteSpace(SearchTextBox.Text))
            {
                SearchTextBox.Foreground = System.Windows.Media.Brushes.White;
            }
        }

        /// <summary>
        /// 搜索框失去焦点
        /// </summary>
        private void SearchTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // 失去焦点时不需要特殊处理
        }

        /// <summary>
        /// 搜索框按键事件（支持ESC清除）
        /// </summary>
        private void SearchTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                ClearSearch();
            }
        }

        /// <summary>
        /// 清除搜索按钮点击
        /// </summary>
        private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
        {
            ClearSearch();
        }

        /// <summary>
        /// 清除搜索
        /// </summary>
        private void ClearSearch()
        {
            SearchTextBox.Text = "";
            _currentSearchText = "";
            ClearSearchButton.Visibility = Visibility.Collapsed;
            
            // 更新所有面板（显示全部数据）
            if (_allDataItems.Count > 0)
            {
                foreach (var panel in _panels)
                {
                    panel.UpdateData(_allDataItems);
                }
            }
            
            // 恢复状态栏
            if (_allDataItems.Count > 0)
            {
                string dataSource = _dataService.IsTradingTime() ? "实时数据" : "日线数据";
                string filterInfo = GetFilterInfo();
                string panelInfo = $" | 面板: {_panels.Count}";
                StatusText.Text = $"已加载 {_allDataItems.Count} 条数据 ({dataSource}){filterInfo}{panelInfo}";
                InfoText.Text = $"最后更新: {DateTime.Now:HH:mm:ss} | 共 {_allDataItems.Count} 条记录 | 数据源: {dataSource}{filterInfo}{panelInfo}";
            }
        }

        /// <summary>
        /// 配置股票代码按钮点击
        /// </summary>
        private void ConfigStockCodesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var configDialog = new HaiLiDrvStockCodeConfigDialog(_configProvider);
                configDialog.Owner = this;
                
                if (configDialog.ShowDialog() == true)
                {
                    // 配置已保存，重新加载数据服务以应用新配置
                    _dataService = new HaiLiDrvDataService(_realTimeCache, _configProvider);
                    Console.WriteLine("[HaiLiDrvWindow] 股票代码配置已更新，重新加载数据");
                    
                    // 立即刷新数据
                    RefreshData();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"打开配置对话框失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        /// <summary>
        /// 窗口关闭时清理资源并保存配置
        /// </summary>
        private void HaiLiDrvWindow_Closed(object sender, EventArgs e)
        {
            if (_refreshTimer != null)
            {
                _refreshTimer.Stop();
                _refreshTimer = null;
            }

            // 如果是独立模式且缓存是独立创建的，释放缓存
            if (_isStandaloneMode && _realTimeCache != null)
            {
                // 检查是否是独立缓存（通过CacheManager创建的）
                // 注意：如果是从外部传入的缓存，不应该释放
                // 这里简化处理：独立模式下总是释放（因为独立模式通常使用独立缓存）
                try
                {
                    Helpers.CacheManager.ReleaseStandaloneCache(_realTimeCache);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[HaiLiDrvWindow] 释放缓存失败: {ex.Message}");
                }
            }

            // 保存窗口位置和大小到配置文件
            try
            {
                var bounds = this.RestoreBounds;
                if (bounds.Width > 0 && bounds.Height > 0)
                {
                    _configProvider.SetValue("HaiLiDrvWindow_Left", bounds.Left.ToString());
                    _configProvider.SetValue("HaiLiDrvWindow_Top", bounds.Top.ToString());
                    _configProvider.SetValue("HaiLiDrvWindow_Width", bounds.Width.ToString());
                    _configProvider.SetValue("HaiLiDrvWindow_Height", bounds.Height.ToString());
                    Console.WriteLine($"[HaiLiDrvWindow] 已保存窗口位置到配置文件");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HaiLiDrvWindow] 保存窗口位置失败: {ex.Message}");
            }
        }
    }

}
