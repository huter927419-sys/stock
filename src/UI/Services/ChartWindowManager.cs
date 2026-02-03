using System;
using System.Windows;
using System.Windows.Forms;
using MQReceiver.Cache;
using MQReceiver.Views;

namespace MQReceiver.Services
{
    /// <summary>
    /// K线图窗口管理器（单例模式）
    /// 确保同时只有一个K线图窗口打开
    /// 优化：使用独立的图表数据服务，预加载数据提升性能
    /// </summary>
    public class ChartWindowManager
    {
        private static ChartWindowManager _instance;
        private static readonly object _lock = new object();
        
        private WebChartWindow _currentChartWindow;
        private RealTimeDataCache _realTimeCache;
        private ChartDataService _chartDataService; // 独立的图表数据服务

        private ChartWindowManager()
        {
        }

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static ChartWindowManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new ChartWindowManager();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 设置实时数据缓存（必须在首次使用前调用）
        /// </summary>
        public void SetRealTimeCache(RealTimeDataCache cache)
        {
            _realTimeCache = cache;
            // 创建独立的图表数据服务
            _chartDataService = new ChartDataService(cache);
        }

        /// <summary>
        /// 打开股票K线图窗口（单窗口模式：如果窗口已打开，则更新内容；否则创建新窗口）
        /// </summary>
        /// <param name="stockCode">股票代码</param>
        /// <param name="targetScreen">目标屏幕（null表示使用保存的位置，或主屏）</param>
        public void OpenChartWindow(string stockCode, Screen targetScreen = null)
        {
            try
            {
                if (string.IsNullOrEmpty(stockCode))
                {
                    Console.WriteLine("[ChartWindowManager] 股票代码为空，无法打开图表");
                    return;
                }

                // 如果已有图表窗口打开，更新其内容而不是关闭重建
                if (_currentChartWindow != null && _currentChartWindow.IsLoaded)
                {
                    try
                    {
                        // 预加载数据（后台执行，不阻塞）
                        if (_chartDataService != null)
                        {
                            _ = _chartDataService.LoadChartDataAsync(stockCode);
                        }
                        
                        // 更新窗口显示的股票代码（复用窗口，保持位置和大小）
                        // 注意：UpdateStockCodeAsync 是异步方法，但不等待完成，让它在后台执行
                        _ = _currentChartWindow.UpdateStockCodeAsync(stockCode);
                        
                        // 如果窗口被最小化，恢复显示
                        if (_currentChartWindow.WindowState == WindowState.Minimized)
                        {
                            _currentChartWindow.WindowState = WindowState.Normal;
                        }
                        
                        // 激活窗口并置于前台
                        _currentChartWindow.Activate();
                        _currentChartWindow.Focus();
                        
                        Console.WriteLine($"[ChartWindowManager] 已更新图表窗口显示股票 {stockCode}");
                        return;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ChartWindowManager] 更新窗口内容时出错: {ex.Message}，将创建新窗口");
                        // 如果更新失败，关闭旧窗口并创建新窗口
                        try
                        {
                            _currentChartWindow.Close();
                        }
                        catch { }
                        _currentChartWindow = null;
                    }
                }

                // 预加载数据（后台执行，不阻塞窗口创建）
                // 注意：窗口会在 Window_Loaded 时自动加载数据，这里预加载可以提升性能
                if (_chartDataService != null)
                {
                    _ = _chartDataService.LoadChartDataAsync(stockCode);
                }

                // 创建新的图表窗口（如果缓存未设置，WebChartWindow会使用默认方式）
                var chartWindow = new WebChartWindow(stockCode, _realTimeCache);
                _currentChartWindow = chartWindow;
                
                // 如果数据服务可用，设置到窗口（可选优化）
                // chartWindow.SetChartDataService(_chartDataService);

                // 如果指定了目标屏幕，将窗口移动到该屏幕
                if (targetScreen != null)
                {
                    MoveWindowToScreen(chartWindow, targetScreen);
                }

                // 窗口关闭时清空引用
                chartWindow.Closed += (s, e) =>
                {
                    if (_currentChartWindow == chartWindow)
                    {
                        _currentChartWindow = null;
                    }
                };

                chartWindow.Show();
                Console.WriteLine($"[ChartWindowManager] 已打开股票 {stockCode} 的图表窗口");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"打开图表失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 将窗口移动到指定屏幕并最大化
        /// </summary>
        private void MoveWindowToScreen(Window window, Screen targetScreen)
        {
            try
            {
                var bounds = targetScreen.Bounds;
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = bounds.Left;
                window.Top = bounds.Top;
                window.Width = bounds.Width;
                window.Height = bounds.Height;
                window.WindowState = WindowState.Maximized;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChartWindowManager] 移动窗口到屏幕失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 关闭当前K线图窗口
        /// </summary>
        public void CloseChartWindow()
        {
            if (_currentChartWindow != null)
            {
                try
                {
                    if (_currentChartWindow.IsLoaded)
                    {
                        _currentChartWindow.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ChartWindowManager] 关闭窗口时出错: {ex.Message}");
                }
                finally
                {
                    _currentChartWindow = null;
                }
            }
        }

        /// <summary>
        /// 检查是否有K线图窗口打开
        /// </summary>
        public bool IsChartWindowOpen => _currentChartWindow != null && _currentChartWindow.IsLoaded;
    }
}
