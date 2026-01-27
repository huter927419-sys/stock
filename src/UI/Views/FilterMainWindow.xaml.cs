using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MQReceiver.Cache;
using MQReceiver.Configuration;
using MQReceiver.Models;
using MQReceiver.Services;
using MQReceiver.ViewModels;

namespace MQReceiver.Views
{
    /// <summary>
    /// FilterMainWindow.xaml 的交互逻辑
    /// 主窗口，显示三个计算条件的面板
    /// 管理共享缓存，协调数据服务和计算服务
    /// </summary>
    public partial class FilterMainWindow : Window
    {
        private FilterMainViewModel _viewModel;
        private MQServiceWrapper _mqService;
        private FilterService _filterService;
        private RealTimeDataCache _sharedCache;  // 共享缓存
        private bool _isMQRunning = false;
        private bool _isFilterRunning = false;
        private System.Threading.Timer _columnWidthSaveTimer;  // 列宽保存延迟定时器
        private readonly object _columnWidthSaveLock = new object();
        private List<DependencyPropertyDescriptor> _columnWidthDescriptors = new List<DependencyPropertyDescriptor>();  // 保存列宽监听器，用于清理

        public FilterMainWindow()
        {
            InitializeComponent();
            _viewModel = new FilterMainViewModel();
            this.DataContext = _viewModel;
            this.Closed += FilterMainWindow_Closed;

            // 创建共享缓存
            _sharedCache = new RealTimeDataCache();
            
            // 将实时缓存传递给ViewModel，用于响应数据推送更新涨幅
            _viewModel.SetRealTimeCache(_sharedCache);

            // 初始化股票信息缓存（从数据库加载或同步）
            InitializeStockInfoCache();

            // 更新缓存状态显示
            UpdateCacheStatus();
        }

        /// <summary>
        /// 初始化股票信息缓存
        /// </summary>
        private void InitializeStockInfoCache()
        {
            try
            {
                // 打印数据库诊断信息
                var repository = new Repositories.PostgresStockDataRepository();
                repository.PrintDataDiagnostics();

                // 先从日线数据同步股票代码到stock_info表
                Console.WriteLine("[FilterMainWindow] 正在同步股票代码...");
                int syncCount = StockInfoCache.Instance.SyncFromDailyData();
                if (syncCount > 0)
                {
                    Console.WriteLine($"[FilterMainWindow] 同步了 {syncCount} 条股票记录");
                }

                // 修复数据库中的股票信息（包括更新已知股票名称）
                StockInfoCache.Instance.FixStockInfoData();
                Console.WriteLine($"[FilterMainWindow] 股票信息缓存已加载: {StockInfoCache.Instance.Count} 条记录");

                // 诊断问题股票代码
                string[] problemCodes = { "000891", "000022", "000101", "000071", "000033", "000854", "000141", "000119", "000112", "000132", "000117", "000057", "000982", "000133", "000135", "000106", "000145", "000105", "000094", "000092", "000853", "000160", "000073", "000122", "000116", "000130", "000152", "000125", "000113" };
                StockInfoCache.Instance.DiagnoseStockCodes(problemCodes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FilterMainWindow] 初始化股票信息缓存失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 窗口关闭时清理资源
        /// </summary>
        private void FilterMainWindow_Closed(object sender, EventArgs e)
        {
            // 停止列宽保存定时器
            lock (_columnWidthSaveLock)
            {
                if (_columnWidthSaveTimer != null)
                {
                    _columnWidthSaveTimer.Dispose();
                    _columnWidthSaveTimer = null;
                }
            }

            // 移除列宽改变事件监听
            DetachColumnWidthChangeHandlers();

            // 保存列宽和顺序（在窗口关闭时也保存一次，确保不丢失）
            SavePanelTableColumns();

            // 停止计算服务
            StopFilterService();

            // 停止数据服务
            StopMQService();

            // 清理事件订阅
            RefreshRequested = null;

            // 清理ViewModel
            if (_viewModel != null)
            {
                _viewModel.Cleanup();
                _viewModel = null;
            }

            // 清理共享缓存
            if (_sharedCache != null)
            {
                _sharedCache.Dispose();
                _sharedCache = null;
            }

            // 清理DataContext
            this.DataContext = null;

            // 取消事件订阅
            this.Closed -= FilterMainWindow_Closed;
        }

        private void FilterMainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RestorePanelTableColumns();
            // 为所有DataGrid添加列宽改变事件监听
            AttachColumnWidthChangeHandlers();
        }

        /// <summary>
        /// 为所有DataGrid添加列宽改变事件监听
        /// </summary>
        private void AttachColumnWidthChangeHandlers()
        {
            var grids = new[] { DataGrid_Table1, DataGrid_Table2, DataGrid_Table3, DataGrid_Table4, DataGrid_Table5, DataGrid_Table6 };
            foreach (var dg in grids)
            {
                if (dg == null) continue;
                // 为每个列添加宽度属性变化监听
                foreach (var col in dg.Columns.Cast<DataGridColumn>())
                {
                    var descriptor = DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty, typeof(DataGridColumn));
                    if (descriptor != null)
                    {
                        descriptor.AddValueChanged(col, Column_WidthPropertyChanged);
                        _columnWidthDescriptors.Add(descriptor);
                    }
                }
            }
        }

        /// <summary>
        /// 移除所有列宽改变事件监听
        /// </summary>
        private void DetachColumnWidthChangeHandlers()
        {
            var grids = new[] { DataGrid_Table1, DataGrid_Table2, DataGrid_Table3, DataGrid_Table4, DataGrid_Table5, DataGrid_Table6 };
            foreach (var dg in grids)
            {
                if (dg == null) continue;
                foreach (var col in dg.Columns.Cast<DataGridColumn>())
                {
                    var descriptor = DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty, typeof(DataGridColumn));
                    if (descriptor != null)
                    {
                        descriptor.RemoveValueChanged(col, Column_WidthPropertyChanged);
                    }
                }
            }
            _columnWidthDescriptors.Clear();
        }

        /// <summary>
        /// 列宽属性改变事件处理（延迟保存，避免频繁写入）
        /// </summary>
        private void Column_WidthPropertyChanged(object sender, EventArgs e)
        {
            lock (_columnWidthSaveLock)
            {
                // 如果定时器已存在，先停止
                if (_columnWidthSaveTimer != null)
                {
                    _columnWidthSaveTimer.Dispose();
                    _columnWidthSaveTimer = null;
                }

                // 创建新的延迟定时器（500ms后保存）
                _columnWidthSaveTimer = new System.Threading.Timer(
                    _ => 
                    {
                        lock (_columnWidthSaveLock)
                        {
                            try
                            {
                                // 在UI线程上执行保存
                                Application.Current?.Dispatcher.Invoke(() =>
                                {
                                    SavePanelTableColumns();
                                });
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[Column_WidthPropertyChanged] 保存列宽失败: {ex.Message}");
                            }
                            finally
                            {
                                _columnWidthSaveTimer?.Dispose();
                                _columnWidthSaveTimer = null;
                            }
                        }
                    },
                    null,
                    500,  // 延迟500ms
                    Timeout.Infinite
                );
            }
        }

        private void FilterMainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 保存列宽和顺序
            SavePanelTableColumns();
            
            // 保存M1/M2/M3/M4/N阈值和涨幅计算阈值（确保不丢失）
            if (_viewModel != null)
            {
                var cfg = AppConfigProvider.Instance;
                cfg.SetDecimal("GlobalThreshold_M1", _viewModel.GlobalM1);
                cfg.SetDecimal("GlobalThreshold_M2", _viewModel.GlobalM2);
                cfg.SetDecimal("GlobalThreshold_M3", _viewModel.GlobalM3);
                cfg.SetDecimal("GlobalThreshold_M4", _viewModel.GlobalM4);
                cfg.SetValue("GlobalThreshold_N", _viewModel.GlobalN.ToString());
                cfg.SetDecimal("PriceChangeFilterThreshold", _viewModel.PriceChangeFilterThreshold);
            }
        }

        /// <summary>
        /// 从 App.config 恢复面板表格列顺序与列宽（名称、涨幅、周K、月K、季K）
        /// </summary>
        private void RestorePanelTableColumns()
        {
            try
            {
                var cfg = AppConfigProvider.Instance;
                string orderStr = cfg.GetString("PanelTable_ColumnOrder");
                if (string.IsNullOrEmpty(orderStr)) return;
                var ids = orderStr.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
                if (ids.Length == 0) return;

                var idToHeader = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Name", "名称" }, { "PriceChange", "涨幅" }, { "WeeklyK", "周K" }, { "MonthlyK", "月K" }, { "QuarterlyK", "季K" }
                };
                var defaults = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Name", 65 }, { "PriceChange", 48 }, { "WeeklyK", 42 }, { "MonthlyK", 42 }, { "QuarterlyK", 42 }
                };

                var grids = new[] { DataGrid_Table1, DataGrid_Table2, DataGrid_Table3, DataGrid_Table4, DataGrid_Table5, DataGrid_Table6 };
                foreach (var dg in grids)
                {
                    if (dg?.Columns == null || dg.Columns.Count == 0) continue;
                    for (int i = 0; i < ids.Length; i++)
                    {
                        string id = ids[i];
                        if (!idToHeader.TryGetValue(id, out string header)) continue;
                        var col = dg.Columns.Cast<DataGridColumn>().FirstOrDefault(c => object.Equals(c.Header?.ToString(), header));
                        if (col == null) continue;
                        col.DisplayIndex = i;
                        string wKey = "PanelTable_ColumnWidth_" + id;
                        string ws = cfg.GetString(wKey);
                        double w = defaults.TryGetValue(id, out double def) ? def : 60;
                        if (!string.IsNullOrEmpty(ws) && double.TryParse(ws, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) && parsed >= 30 && parsed <= 400)
                            w = parsed;
                        col.Width = new DataGridLength(w, DataGridLengthUnitType.Pixel);
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[RestorePanelTableColumns] {ex.Message}"); }
        }

        /// <summary>
        /// 将面板表格列顺序与列宽保存到 App.config
        /// </summary>
        private void SavePanelTableColumns()
        {
            try
            {
                var dg = DataGrid_Table1;
                if (dg?.Columns == null || dg.Columns.Count == 0) return;

                var headerToId = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "名称", "Name" }, { "涨幅", "PriceChange" }, { "周K", "WeeklyK" }, { "月K", "MonthlyK" }, { "季K", "QuarterlyK" }
                };
                var defaults = new Dictionary<string, double>(StringComparer.Ordinal)
                {
                    { "名称", 60 }, { "涨幅", 48 }, { "周K", 50 }, { "月K", 50 }, { "季K", 50 }
                };

                var ordered = dg.Columns.Cast<DataGridColumn>()
                    .OrderBy(c => c.DisplayIndex)
                    .Select(c => c.Header?.ToString())
                    .Where(h => !string.IsNullOrEmpty(h) && headerToId.ContainsKey(h))
                    .Select(h => headerToId[h])
                    .ToList();
                if (ordered.Count == 0) return;

                var cfg = AppConfigProvider.Instance;
                cfg.SetValue("PanelTable_ColumnOrder", string.Join(",", ordered));

                foreach (var col in dg.Columns.Cast<DataGridColumn>())
                {
                    string h = col.Header?.ToString();
                    if (string.IsNullOrEmpty(h) || !headerToId.TryGetValue(h, out string id)) continue;
                    
                    // 优先使用当前列宽（如果是Pixel类型且有效）
                    double val = defaults.TryGetValue(h, out double def) ? def : 60;
                    if (col.Width.UnitType == DataGridLengthUnitType.Pixel && !double.IsNaN(col.Width.Value) && col.Width.Value > 0)
                    {
                        val = col.Width.Value;
                    }
                    else if (col.Width.UnitType == DataGridLengthUnitType.Auto)
                    {
                        // 如果是Auto，尝试获取实际渲染宽度
                        if (col.ActualWidth > 0 && !double.IsNaN(col.ActualWidth))
                            val = col.ActualWidth;
                    }
                    
                    // 确保宽度在合理范围内
                    if (val < 30) val = 30;
                    if (val > 400) val = 400;
                    
                    cfg.SetValue("PanelTable_ColumnWidth_" + id, val.ToString(CultureInfo.InvariantCulture));
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[SavePanelTableColumns] {ex.Message}"); }
        }

        /// <summary>
        /// 更新计算结果
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
            _viewModel.UpdateResults(results1, results2, results3, results4, results5, results6, results7, results8);
        }

        /// <summary>
        /// 股票名称点击事件
        /// </summary>
        private void StockName_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var textBlock = sender as System.Windows.Controls.TextBlock;
            if (textBlock != null && textBlock.Tag != null)
            {
                string stockCode = textBlock.Tag.ToString();
                OpenStockChart(stockCode);
                e.Handled = true;
            }
        }

        /// <summary>
        /// 双击行打开图表
        /// </summary>
        private void DataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var dataGrid = sender as DataGrid;
            if (dataGrid != null && dataGrid.SelectedItem != null)
            {
                var item = dataGrid.SelectedItem as StockResultItem;
                if (item != null)
                {
                    OpenStockChart(item.StockCode);
                }
            }
        }

        // 保存当前打开的图表窗口（单例模式）
        private static WebChartWindow _currentChartWindow = null;

        /// <summary>
        /// 打开股票图表窗口（单窗口模式：新窗口覆盖旧窗口）
        /// </summary>
        private void OpenStockChart(string stockCode)
        {
            try
            {
                // 如果已有图表窗口打开，先关闭它
                if (_currentChartWindow != null && _currentChartWindow.IsLoaded)
                {
                    _currentChartWindow.Close();
                    _currentChartWindow = null;
                }

                // 创建新的图表窗口
                var chartWindow = new WebChartWindow(stockCode, _sharedCache);
                _currentChartWindow = chartWindow;
                
                // 窗口关闭时清空引用
                chartWindow.Closed += (s, e) =>
                {
                    if (_currentChartWindow == chartWindow)
                    {
                        _currentChartWindow = null;
                    }
                };
                
                chartWindow.Show();
                Console.WriteLine($"[单窗口模式] 已打开股票 {stockCode} 的图表窗口");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开图表失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 刷新按钮
        /// </summary>
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            // 触发刷新事件（由FilterService处理）
            if (RefreshRequested != null)
            {
                RefreshRequested(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 关闭按钮
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// 刷新请求事件
        /// </summary>
        public event EventHandler RefreshRequested;

        /// <summary>
        /// 表格1设置按钮点击
        /// </summary>
        private void Table1Settings_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;

            var dialog = new FilterSettingsDialog(
                _viewModel.Table1WeeklyKDefaultMin,
                _viewModel.Table1MonthlyKDefaultMin,
                _viewModel.Table1QuarterlyKDefaultMin);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                _viewModel.Table1WeeklyKDefaultMin = dialog.WeeklyKMin;
                _viewModel.Table1MonthlyKDefaultMin = dialog.MonthlyKMin;
                _viewModel.Table1QuarterlyKDefaultMin = dialog.QuarterlyKMin;
            }
        }

        /// <summary>
        /// 表格2设置按钮点击
        /// </summary>
        private void Table2Settings_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;

            var dialog = new FilterSettingsDialog(
                _viewModel.Table2WeeklyKDefaultMin,
                _viewModel.Table2MonthlyKDefaultMin,
                _viewModel.Table2QuarterlyKDefaultMin);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                _viewModel.Table2WeeklyKDefaultMin = dialog.WeeklyKMin;
                _viewModel.Table2MonthlyKDefaultMin = dialog.MonthlyKMin;
                _viewModel.Table2QuarterlyKDefaultMin = dialog.QuarterlyKMin;
            }
        }

        /// <summary>
        /// 表格3设置按钮点击
        /// </summary>
        private void Table3Settings_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;

            var dialog = new FilterSettingsDialog(
                _viewModel.Table3WeeklyKDefaultMin,
                _viewModel.Table3MonthlyKDefaultMin,
                _viewModel.Table3QuarterlyKDefaultMin);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                _viewModel.Table3WeeklyKDefaultMin = dialog.WeeklyKMin;
                _viewModel.Table3MonthlyKDefaultMin = dialog.MonthlyKMin;
                _viewModel.Table3QuarterlyKDefaultMin = dialog.QuarterlyKMin;
            }
        }

        /// <summary>
        /// 表格4设置按钮点击
        /// </summary>
        private void Table4Settings_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;

            var dialog = new FilterSettingsDialog(
                _viewModel.Table4WeeklyKDefaultMin,
                _viewModel.Table4MonthlyKDefaultMin,
                _viewModel.Table4QuarterlyKDefaultMin);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                _viewModel.Table4WeeklyKDefaultMin = dialog.WeeklyKMin;
                _viewModel.Table4MonthlyKDefaultMin = dialog.MonthlyKMin;
                _viewModel.Table4QuarterlyKDefaultMin = dialog.QuarterlyKMin;
            }
        }

        /// <summary>
        /// 表格5设置按钮点击
        /// </summary>
        private void Table5Settings_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;

            var dialog = new FilterSettingsDialog(
                _viewModel.Table5WeeklyKDefaultMin,
                _viewModel.Table5MonthlyKDefaultMin,
                _viewModel.Table5QuarterlyKDefaultMin);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                _viewModel.Table5WeeklyKDefaultMin = dialog.WeeklyKMin;
                _viewModel.Table5MonthlyKDefaultMin = dialog.MonthlyKMin;
                _viewModel.Table5QuarterlyKDefaultMin = dialog.QuarterlyKMin;
            }
        }

        /// <summary>
        /// 表格6设置按钮点击
        /// </summary>
        private void Table6Settings_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;

            var dialog = new FilterSettingsDialog(
                _viewModel.Table6WeeklyKDefaultMin,
                _viewModel.Table6MonthlyKDefaultMin,
                _viewModel.Table6QuarterlyKDefaultMin);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                _viewModel.Table6WeeklyKDefaultMin = dialog.WeeklyKMin;
                _viewModel.Table6MonthlyKDefaultMin = dialog.MonthlyKMin;
                _viewModel.Table6QuarterlyKDefaultMin = dialog.QuarterlyKMin;
            }
        }

        #region 数据服务控制

        /// <summary>
        /// 数据服务按钮点击
        /// </summary>
        private void MQServiceButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isMQRunning)
            {
                StopMQService();
            }
            else
            {
                StartMQService();
            }
        }

        /// <summary>
        /// 启动数据服务
        /// </summary>
        private void StartMQService()
        {
            try
            {
                // 显示日志面板
                LogPanel.Visibility = Visibility.Visible;
                ToggleLogButton.Content = "隐藏日志";  // 更新按钮文字
                AppendLog("正在启动数据服务...");

                // 创建数据服务包装器并设置共享缓存
                _mqService = new MQServiceWrapper();
                _mqService.SetExternalCache(_sharedCache);  // 使用共享缓存
                _mqService.LogMessage += OnMQLogMessage;
                _mqService.StatusChanged += OnMQStatusChanged;

                // 在后台线程启动
                Task.Run(() =>
                {
                    try
                    {
                        _mqService.Start();
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            AppendLog($"启动数据服务失败: {ex.Message}");
                            UpdateMQStatus(false);
                        });
                    }
                });

                _isMQRunning = true;
                UpdateMQStatus(true);
                MQServiceButton.Content = "停止数据服务";
                AppendLog("数据服务启动命令已发送");
                AppendLog("提示: 数据服务接收数据后，可启动计算服务进行分析");
            }
            catch (Exception ex)
            {
                AppendLog($"启动数据服务失败: {ex.Message}");
                MessageBox.Show($"启动数据服务失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 停止数据服务
        /// </summary>
        private void StopMQService()
        {
            try
            {
                if (_mqService != null)
                {
                    AppendLog("正在停止数据服务...");
                    _mqService.Stop();
                    _mqService.LogMessage -= OnMQLogMessage;
                    _mqService.StatusChanged -= OnMQStatusChanged;
                    _mqService.Dispose();
                    _mqService = null;
                    AppendLog("数据服务已停止");
                }

                _isMQRunning = false;
                UpdateMQStatus(false);
                MQServiceButton.Content = "接收数据服务";
            }
            catch (Exception ex)
            {
                AppendLog($"停止数据服务失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 数据服务日志消息回调
        /// </summary>
        private void OnMQLogMessage(object sender, string message)
        {
            Dispatcher.Invoke(() =>
            {
                AppendLog(message);
                // 当收到数据时更新缓存状态
                if (message.Contains("成功") && message.Contains("缓存"))
                {
                    UpdateCacheStatus();
                }
            });
        }

        /// <summary>
        /// 数据服务状态变化回调
        /// </summary>
        private void OnMQStatusChanged(object sender, bool isRunning)
        {
            Dispatcher.Invoke(() =>
            {
                _isMQRunning = isRunning;
                UpdateMQStatus(isRunning);
                MQServiceButton.Content = isRunning ? "停止数据服务" : "接收数据服务";
            });
        }

        /// <summary>
        /// 更新数据服务状态显示
        /// </summary>
        private void UpdateMQStatus(bool isRunning)
        {
            if (isRunning)
            {
                MQStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(0x4E, 0xCD, 0x4E)); // 绿色
                MQStatusText.Text = "数据服务运行中";
                MQStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x4E, 0xCD, 0x4E));
            }
            else
            {
                MQStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x6E)); // 灰色
                MQStatusText.Text = "数据服务未启动";
                MQStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x85, 0x85, 0x85));
            }
        }

        /// <summary>
        /// 添加日志
        /// </summary>
        private void AppendLog(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogTextBox.AppendText($"[{timestamp}] {message}\r\n");
            LogTextBox.ScrollToEnd();
        }

        /// <summary>
        /// 清空日志
        /// </summary>
        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
        }

        /// <summary>
        /// 隐藏日志面板
        /// </summary>
        private void HideLogButton_Click(object sender, RoutedEventArgs e)
        {
            LogPanel.Visibility = Visibility.Collapsed;
        }
        
        /// <summary>
        /// 切换日志面板显示/隐藏
        /// </summary>
        private void ToggleLogButton_Click(object sender, RoutedEventArgs e)
        {
            if (LogPanel.Visibility == Visibility.Visible)
            {
                LogPanel.Visibility = Visibility.Collapsed;
                ToggleLogButton.Content = "显示日志";
            }
            else
            {
                LogPanel.Visibility = Visibility.Visible;
                ToggleLogButton.Content = "隐藏日志";
            }
        }

        #endregion

        #region 计算服务控制

        /// <summary>
        /// 计算服务按钮点击
        /// </summary>
        private void FilterServiceButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isFilterRunning)
            {
                StopFilterService();
            }
            else
            {
                StartFilterService();
            }
        }

        /// <summary>
        /// 启动计算服务
        /// </summary>
        private void StartFilterService()
        {
            try
            {
                // 显示日志面板
                LogPanel.Visibility = Visibility.Visible;
                ToggleLogButton.Content = "隐藏日志";  // 更新按钮文字

                // 如果缓存为空，提示将从数据库加载
                if (_sharedCache == null || _sharedCache.Count == 0)
                {
                    AppendLog("缓存中没有实时数据，计算服务将尝试从数据库加载股票列表");
                }

                AppendLog("正在启动计算服务...");

                // 创建计算服务并设置共享缓存
                _filterService = new FilterService();
                _filterService.SetExternalCache(_sharedCache);
                
                // 订阅日志事件
                _filterService.LogMessage += (msg) => Dispatcher.Invoke(() => AppendLog(msg));

                // 订阅计算完成事件
                _filterService.FilterCompleted += OnFilterCompleted;

                // 在后台线程初始化和启动
                Task.Run(() =>
                {
                    try
                    {
                        if (_filterService.Initialize())
                        {
                            _filterService.StartTimer();

                            Dispatcher.Invoke(() =>
                            {
                                _isFilterRunning = true;
                                UpdateFilterStatus(true);
                                FilterServiceButton.Content = "停止计算";
                                AppendLog("计算服务已启动");
                            });

                            // 立即执行一次计算
                            _filterService.TriggerFilter();
                        }
                        else
                        {
                            Dispatcher.Invoke(() =>
                            {
                                AppendLog("计算服务初始化失败");
                                UpdateFilterStatus(false);
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            AppendLog($"启动计算服务失败: {ex.Message}");
                            UpdateFilterStatus(false);
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                AppendLog($"启动计算服务失败: {ex.Message}");
                MessageBox.Show($"启动计算服务失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 停止计算服务
        /// </summary>
        private void StopFilterService()
        {
            try
            {
                if (_filterService != null)
                {
                    AppendLog("正在停止计算服务...");
                    _filterService.FilterCompleted -= OnFilterCompleted;
                    _filterService.LogMessage -= null;  // 取消订阅日志事件
                    _filterService.Stop();
                    _filterService.Dispose();
                    _filterService = null;
                    AppendLog("计算服务已停止");
                }

                _isFilterRunning = false;
                UpdateFilterStatus(false);
                FilterServiceButton.Content = "开始计算";
            }
            catch (Exception ex)
            {
                AppendLog($"停止计算服务失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 计算完成回调
        /// </summary>
        private void OnFilterCompleted(object sender, MQReceiver.Events.FilterResultEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateResults(e.Table1Results, e.Table2Results, e.Table3Results, e.Table4Results, e.Table5Results, e.Table6Results, e.Table7Results, e.Table8Results);
                AppendLog($"计算完成: 表1={e.Table1Results?.Count ?? 0}, 表2={e.Table2Results?.Count ?? 0}, 表3={e.Table3Results?.Count ?? 0}, 表4={e.Table4Results?.Count ?? 0}, 表5={e.Table5Results?.Count ?? 0}, 表6={e.Table6Results?.Count ?? 0}, 表7={e.Table7Results?.Count ?? 0}, 表8={e.Table8Results?.Count ?? 0}");
                UpdateCacheStatus();
            });
        }

        /// <summary>
        /// 更新计算状态显示
        /// </summary>
        private void UpdateFilterStatus(bool isRunning)
        {
            if (isRunning)
            {
                FilterStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(0x4E, 0xCD, 0x4E)); // 绿色
                FilterStatusText.Text = "计算运行中";
                FilterStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x4E, 0xCD, 0x4E));
            }
            else
            {
                FilterStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x6E)); // 灰色
                FilterStatusText.Text = "计算未启动";
                FilterStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x85, 0x85, 0x85));
            }
        }

        /// <summary>
        /// 更新缓存状态显示
        /// </summary>
        private void UpdateCacheStatus()
        {
            if (_sharedCache != null)
            {
                CacheCountText.Text = $"缓存数据: {_sharedCache.Count} 只股票";
                if (_sharedCache.LastUpdateTime != DateTime.MinValue)
                {
                    CacheUpdateTimeText.Text = $"更新时间: {_sharedCache.LastUpdateTime:HH:mm:ss}";
                }
                else
                {
                    CacheUpdateTimeText.Text = "更新时间: 从未更新";
                }
            }

            // 更新数据状态显示
            UpdateDataStatusDisplay();
        }

        /// <summary>
        /// 更新数据状态显示
        /// </summary>
        private void UpdateDataStatusDisplay()
        {
            try
            {
                if (_filterService != null && _filterService.DataBoundaryManager != null)
                {
                    var dataStatus = _filterService.DataBoundaryManager.GetCurrentDataStatus();
                    string sessionDesc = DataBoundaryManager.GetSessionDescription(dataStatus.Session);
                    string strategyDesc = DataBoundaryManager.GetStrategyDescription(dataStatus.Strategy);

                    TradingSessionText.Text = sessionDesc;
                    DataStatusText.Text = $"数据: {strategyDesc}";

                    // 根据数据是否新鲜设置指示器颜色
                    if (dataStatus.IsDataFresh)
                    {
                        DataStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(0x4E, 0xCD, 0x4E)); // 绿色
                    }
                    else
                    {
                        DataStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0x9E, 0x4E)); // 橙色警告
                    }
                }
                else
                {
                    // 使用临时的数据边界管理器
                    var tempManager = new DataBoundaryManager(_sharedCache);
                    var dataStatus = tempManager.GetCurrentDataStatus();
                    string sessionDesc = DataBoundaryManager.GetSessionDescription(dataStatus.Session);

                    TradingSessionText.Text = sessionDesc;
                    DataStatusText.Text = "数据: 等待初始化";
                    DataStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x6E)); // 灰色
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新数据状态显示失败: {ex.Message}");
                DataStatusText.Text = "数据: 状态未知";
                TradingSessionText.Text = "";
                DataStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x6E)); // 灰色
            }
        }

        #endregion
    }
}
