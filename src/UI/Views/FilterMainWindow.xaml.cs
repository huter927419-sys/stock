using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MQReceiver.Cache;
using MQReceiver.Models;
using MQReceiver.Services;
using MQReceiver.ViewModels;

namespace MQReceiver.Views
{
    /// <summary>
    /// FilterMainWindow.xaml 的交互逻辑
    /// 主窗口，显示三个过滤条件的面板
    /// 管理共享缓存，协调MQ服务和过滤服务
    /// </summary>
    public partial class FilterMainWindow : Window
    {
        private FilterMainViewModel _viewModel;
        private MQServiceWrapper _mqService;
        private FilterService _filterService;
        private RealTimeDataCache _sharedCache;  // 共享缓存
        private bool _isMQRunning = false;
        private bool _isFilterRunning = false;

        public FilterMainWindow()
        {
            InitializeComponent();
            _viewModel = new FilterMainViewModel();
            this.DataContext = _viewModel;
            this.Closed += FilterMainWindow_Closed;

            // 创建共享缓存
            _sharedCache = new RealTimeDataCache();

            // 初始化股票信息缓存（从数据库加载或同步）
            InitializeStockInfoCache();

            // 更新缓存状态显示
            UpdateCacheStatus();
        }

        /// <summary>
        /// 初始化股票信息缓存
        /// 如果stock_info表为空或记录较少，会自动从日线数据同步
        /// 会自动从本地SQL文件导入股票名称
        /// </summary>
        private void InitializeStockInfoCache()
        {
            try
            {
                // 先修复可能存在的错误数据（stock_name为null或market_code错误，并从SQL文件导入名称）
                StockInfoCache.Instance.FixStockInfoData();

                // 加载缓存
                StockInfoCache.Instance.LoadFromDatabase();

                // 如果缓存为空，执行同步
                if (StockInfoCache.Instance.Count == 0)
                {
                    Console.WriteLine("[FilterMainWindow] 股票信息缓存为空，正在从日线数据同步...");
                    StockInfoCache.Instance.SyncFromDailyData();
                }
                else
                {
                    Console.WriteLine($"[FilterMainWindow] 股票信息缓存已加载: {StockInfoCache.Instance.Count} 条记录");
                }
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
            // 停止过滤服务
            StopFilterService();

            // 停止MQ服务
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

        /// <summary>
        /// 更新过滤结果
        /// </summary>
        public void UpdateResults(
            List<FilterResultWithHistory> results1,
            List<FilterResultWithHistory> results2,
            List<FilterResultWithHistory> results3)
        {
            _viewModel.UpdateResults(results1, results2, results3);
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

        /// <summary>
        /// 打开股票图表窗口
        /// </summary>
        private void OpenStockChart(string stockCode)
        {
            try
            {
                var chartWindow = new WebChartWindow(stockCode);
                chartWindow.Show();
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
        /// 条件1设置按钮点击
        /// </summary>
        private void Condition1Settings_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;

            var dialog = new FilterSettingsDialog(
                _viewModel.Condition1WeeklyKDefaultMin,
                _viewModel.Condition1MonthlyKDefaultMin,
                _viewModel.Condition1QuarterlyKDefaultMin);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                _viewModel.Condition1WeeklyKDefaultMin = dialog.WeeklyKMin;
                _viewModel.Condition1MonthlyKDefaultMin = dialog.MonthlyKMin;
                _viewModel.Condition1QuarterlyKDefaultMin = dialog.QuarterlyKMin;
            }
        }

        /// <summary>
        /// 条件2设置按钮点击
        /// </summary>
        private void Condition2Settings_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;

            var dialog = new FilterSettingsDialog(
                _viewModel.Condition2WeeklyKDefaultMin,
                _viewModel.Condition2MonthlyKDefaultMin,
                _viewModel.Condition2QuarterlyKDefaultMin);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                _viewModel.Condition2WeeklyKDefaultMin = dialog.WeeklyKMin;
                _viewModel.Condition2MonthlyKDefaultMin = dialog.MonthlyKMin;
                _viewModel.Condition2QuarterlyKDefaultMin = dialog.QuarterlyKMin;
            }
        }

        /// <summary>
        /// 条件3设置按钮点击
        /// </summary>
        private void Condition3Settings_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;

            var dialog = new FilterSettingsDialog(
                _viewModel.Condition3WeeklyKDefaultMin,
                _viewModel.Condition3MonthlyKDefaultMin,
                _viewModel.Condition3QuarterlyKDefaultMin);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                _viewModel.Condition3WeeklyKDefaultMin = dialog.WeeklyKMin;
                _viewModel.Condition3MonthlyKDefaultMin = dialog.MonthlyKMin;
                _viewModel.Condition3QuarterlyKDefaultMin = dialog.QuarterlyKMin;
            }
        }

        #region MQ服务控制

        /// <summary>
        /// MQ服务按钮点击
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
        /// 启动MQ服务
        /// </summary>
        private void StartMQService()
        {
            try
            {
                // 显示日志面板
                LogPanel.Visibility = Visibility.Visible;
                AppendLog("正在启动MQ服务...");

                // 创建MQ服务包装器并设置共享缓存
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
                            AppendLog($"启动MQ服务失败: {ex.Message}");
                            UpdateMQStatus(false);
                        });
                    }
                });

                _isMQRunning = true;
                UpdateMQStatus(true);
                MQServiceButton.Content = "停止MQ服务";
                AppendLog("MQ服务启动命令已发送");
                AppendLog("提示: MQ接收数据后，可启动过滤服务进行分析");
            }
            catch (Exception ex)
            {
                AppendLog($"启动MQ服务失败: {ex.Message}");
                MessageBox.Show($"启动MQ服务失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 停止MQ服务
        /// </summary>
        private void StopMQService()
        {
            try
            {
                if (_mqService != null)
                {
                    AppendLog("正在停止MQ服务...");
                    _mqService.Stop();
                    _mqService.LogMessage -= OnMQLogMessage;
                    _mqService.StatusChanged -= OnMQStatusChanged;
                    _mqService.Dispose();
                    _mqService = null;
                    AppendLog("MQ服务已停止");
                }

                _isMQRunning = false;
                UpdateMQStatus(false);
                MQServiceButton.Content = "启动MQ服务";
            }
            catch (Exception ex)
            {
                AppendLog($"停止MQ服务失败: {ex.Message}");
            }
        }

        /// <summary>
        /// MQ日志消息回调
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
        /// MQ状态变化回调
        /// </summary>
        private void OnMQStatusChanged(object sender, bool isRunning)
        {
            Dispatcher.Invoke(() =>
            {
                _isMQRunning = isRunning;
                UpdateMQStatus(isRunning);
                MQServiceButton.Content = isRunning ? "停止MQ服务" : "启动MQ服务";
            });
        }

        /// <summary>
        /// 更新MQ状态显示
        /// </summary>
        private void UpdateMQStatus(bool isRunning)
        {
            if (isRunning)
            {
                MQStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(0x4E, 0xCD, 0x4E)); // 绿色
                MQStatusText.Text = "MQ运行中";
                MQStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x4E, 0xCD, 0x4E));
            }
            else
            {
                MQStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x6E)); // 灰色
                MQStatusText.Text = "MQ未启动";
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

        #endregion

        #region 过滤服务控制

        /// <summary>
        /// 过滤服务按钮点击
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
        /// 启动过滤服务
        /// </summary>
        private void StartFilterService()
        {
            try
            {
                // 显示日志面板
                LogPanel.Visibility = Visibility.Visible;

                // 如果缓存为空，提示将从数据库加载
                if (_sharedCache == null || _sharedCache.Count == 0)
                {
                    AppendLog("缓存中没有实时数据，过滤服务将尝试从数据库加载股票列表");
                }

                AppendLog("正在启动过滤服务...");

                // 创建过滤服务并设置共享缓存
                _filterService = new FilterService();
                _filterService.SetExternalCache(_sharedCache);

                // 订阅过滤完成事件
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
                                FilterServiceButton.Content = "停止过滤";
                                AppendLog("过滤服务已启动");
                            });

                            // 立即执行一次过滤
                            _filterService.TriggerFilter();
                        }
                        else
                        {
                            Dispatcher.Invoke(() =>
                            {
                                AppendLog("过滤服务初始化失败");
                                UpdateFilterStatus(false);
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            AppendLog($"启动过滤服务失败: {ex.Message}");
                            UpdateFilterStatus(false);
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                AppendLog($"启动过滤服务失败: {ex.Message}");
                MessageBox.Show($"启动过滤服务失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 停止过滤服务
        /// </summary>
        private void StopFilterService()
        {
            try
            {
                if (_filterService != null)
                {
                    AppendLog("正在停止过滤服务...");
                    _filterService.FilterCompleted -= OnFilterCompleted;
                    _filterService.Stop();
                    _filterService.Dispose();
                    _filterService = null;
                    AppendLog("过滤服务已停止");
                }

                _isFilterRunning = false;
                UpdateFilterStatus(false);
                FilterServiceButton.Content = "启动过滤";
            }
            catch (Exception ex)
            {
                AppendLog($"停止过滤服务失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 过滤完成回调
        /// </summary>
        private void OnFilterCompleted(object sender, MQReceiver.Events.FilterResultEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateResults(e.Condition1Results, e.Condition2Results, e.Condition3Results);
                AppendLog($"过滤完成: 条件1={e.Condition1Results?.Count ?? 0}, 条件2={e.Condition2Results?.Count ?? 0}, 条件3={e.Condition3Results?.Count ?? 0}");
                UpdateCacheStatus();
            });
        }

        /// <summary>
        /// 更新过滤状态显示
        /// </summary>
        private void UpdateFilterStatus(bool isRunning)
        {
            if (isRunning)
            {
                FilterStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(0x4E, 0xCD, 0x4E)); // 绿色
                FilterStatusText.Text = "过滤运行中";
                FilterStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x4E, 0xCD, 0x4E));
            }
            else
            {
                FilterStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x6E)); // 灰色
                FilterStatusText.Text = "过滤未启动";
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
        }

        #endregion
    }
}
