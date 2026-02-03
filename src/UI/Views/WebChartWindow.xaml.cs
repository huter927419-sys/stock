using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using MQReceiver.Cache;
using MQReceiver.Configuration;
using MQReceiver.Models;
using MQReceiver.Services;

namespace MQReceiver.Views
{
    public partial class WebChartWindow : Window
    {
        private ChartData _chartData;
        private bool _isWebViewInitialized = false;
        private string _stockCode;
        private RealTimeDataCache _realTimeCache;
        private ChartDataService _chartDataService; // 独立的图表数据服务（可选，提升性能）
        private DispatcherTimer _saveBoundsDebounce;
        private DispatcherTimer _resizeDebounce;  // 窗口尺寸变化时防抖，减轻拖动卡顿
        private EventHandler _onLeftOrTopChangedHandler;

        // 盘中图表卡顿优化：同股票短时间内复用图表数据 + 预序列化 JSON，避免重复拉库与重复序列化
        private static readonly Dictionary<string, (ChartData data, DateTime cachedAt, string chartJson)> _chartDataCache =
            new Dictionary<string, (ChartData, DateTime, string)>();
        private static readonly object _chartDataCacheLock = new object();
        private const int ChartCacheTTLSecondsTrading = 20;  // 盘中 20 秒
        private const int ChartCacheTTLSecondsNonTrading = 60; // 盘外 60 秒

        public WebChartWindow(string stockCode) : this(stockCode, null)
        {
        }

        public WebChartWindow(string stockCode, RealTimeDataCache realTimeCache)
        {
            InitializeComponent();
            ApplyInitialPlacement();
            _stockCode = stockCode;
            _realTimeCache = realTimeCache;
            // 创建独立的图表数据服务（带缓存，提升性能）
            if (realTimeCache != null)
            {
                _chartDataService = new ChartDataService(realTimeCache);
            }
            this.Owner = null;
            this.Title = $"加载中... - {stockCode}";
            InitializeBoundsPersistence();
        }

        public WebChartWindow(ChartData chartData)
        {
            InitializeComponent();
            ApplyInitialPlacement();
            _chartData = chartData;
            if (chartData != null)
                this.Title = $"股票图表 - {chartData.StockName} ({chartData.StockCode})";
            InitializeBoundsPersistence();
        }

        /// <summary>
        /// 统一初始放置：双屏=副屏最大化，单屏=主屏中间，否则恢复上次位置。
        /// </summary>
        private void ApplyInitialPlacement()
        {
            bool useFixed = AppConfigProvider.Instance.GetBool("ChartWindow_OpenOnSecondaryScreenAndMaximize", true);
            var secondary = GetSecondaryScreen();
            if (useFixed && secondary != null)
                ApplySecondaryScreenAndMaximize();
            else if (secondary == null)
                ApplyPrimaryScreenPlacement(1400, 950);
            else
                RestoreWindowBounds();
        }

        /// <summary>
        /// 是否为固定位置模式（双屏副屏最大化）：此模式下不保存窗口位置。
        /// </summary>
        private static bool IsFixedPlacementMode()
        {
            return AppConfigProvider.Instance.GetBool("ChartWindow_OpenOnSecondaryScreenAndMaximize", true)
                && GetSecondaryScreen() != null;
        }

        /// <summary>
        /// 从配置恢复K线图窗口位置和大小，支持多屏。
        /// 严格校验：仅当保存的 (Left,Top) 落在当前某块显示器的 Bounds 内才恢复，否则使用默认居中。
        /// </summary>
        private void RestoreWindowBounds()
        {
            try
            {
                var cfg = AppConfigProvider.Instance;
                string sl = cfg.GetString("ChartWindow_Left");
                string st = cfg.GetString("ChartWindow_Top");
                string sw = cfg.GetString("ChartWindow_Width");
                string sh = cfg.GetString("ChartWindow_Height");
                if (string.IsNullOrEmpty(sl) || string.IsNullOrEmpty(st) || string.IsNullOrEmpty(sw) || string.IsNullOrEmpty(sh))
                    return;
                if (!double.TryParse(sl, NumberStyles.Float, CultureInfo.InvariantCulture, out double left) ||
                    !double.TryParse(st, NumberStyles.Float, CultureInfo.InvariantCulture, out double top) ||
                    !double.TryParse(sw, NumberStyles.Float, CultureInfo.InvariantCulture, out double width) ||
                    !double.TryParse(sh, NumberStyles.Float, CultureInfo.InvariantCulture, out double height))
                    return;
                if (width < 300 || width > 4000 || height < 300 || height > 4000)
                    return;
                // 严格多屏：仅当 (left, top) 落在当前某块屏幕内才恢复；否则退回到主屏居中
                if (!IsPointOnAnyScreen(left, top))
                {
                    ApplyPrimaryScreenPlacement(width, height);
                    return;
                }
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = left;
                Top = top;
                Width = width;
                Height = height;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebChartWindow] RestoreWindowBounds: {ex.Message}");
            }
        }

        /// <summary>
        /// 判断点 (x,y) 是否落在当前任意一块显示器的 Bounds 内（支持多屏、负坐标及副屏）。
        /// 使用 System.Windows.Forms.Screen，与 WPF 窗口坐标同属系统虚拟屏坐标系。
        /// </summary>
        private static bool IsPointOnAnyScreen(double x, double y)
        {
            try
            {
                foreach (var s in System.Windows.Forms.Screen.AllScreens)
                {
                    var b = s.Bounds;
                    var r = new Rect(b.X, b.Y, b.Width, b.Height);
                    if (r.Contains(x, y))
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
        /// 当保存的位置已不在任意屏幕时（如断开了该显示器），将窗口放到主屏工作区居中。
        /// 宽高限制在主屏 WorkingArea 内，避免超出可见范围。
        /// </summary>
        private void ApplyPrimaryScreenPlacement(double width, double height)
        {
            try
            {
                var primary = System.Windows.Forms.Screen.PrimaryScreen;
                if (primary == null) return;
                var wa = primary.WorkingArea;
                double w = Math.Min(Math.Max(300, width), wa.Width);
                double h = Math.Min(Math.Max(300, height), wa.Height);
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = wa.X + (wa.Width - w) / 2;
                Top = wa.Y + (wa.Height - h) / 2;
                Width = w;
                Height = h;
            }
            catch { }
        }

        /// <summary>
        /// 获取副屏（非主屏）：多屏时返回第一个非主屏，单屏返回 null。
        /// 用于：面板在主屏，K 线图在副屏打开并最大化。
        /// </summary>
        private static System.Windows.Forms.Screen GetSecondaryScreen()
        {
            try
            {
                var all = System.Windows.Forms.Screen.AllScreens;
                if (all == null || all.Length < 2) return null;
                var primary = System.Windows.Forms.Screen.PrimaryScreen;
                foreach (var s in all)
                {
                    if (s != primary) return s;
                }
                return null;
            }
            catch { return null; }
        }

        /// <summary>
        /// 将 K 线图放到副屏并最大化（面板保留在主屏，图表在另一块屏上最大化）。
        /// </summary>
        private void ApplySecondaryScreenAndMaximize()
        {
            try
            {
                var secondary = GetSecondaryScreen();
                if (secondary == null) return;
                var wa = secondary.WorkingArea;
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = wa.X;
                Top = wa.Y;
                Width = wa.Width;
                Height = wa.Height;
                WindowState = WindowState.Maximized;
            }
            catch { }
        }

        /// <summary>
        /// 初始化窗口位置/大小的持久化：防抖保存到配置。移动或 resize 后不等关闭即写入，
        /// 新开的 K 线图会出现在上次放置的屏幕（含附加屏）。
        /// </summary>
        private void InitializeBoundsPersistence()
        {
            int debounceMs = AppConfigProvider.Instance.GetInt("ChartWindow_SaveBoundsDebounceMs", 600);
            _saveBoundsDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(200, debounceMs)) };
            _saveBoundsDebounce.Tick += (s, ev) =>
            {
                _saveBoundsDebounce.Stop();
                SaveWindowBoundsToConfig();
            };
            _resizeDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
            _resizeDebounce.Tick += async (s, ev) =>
            {
                _resizeDebounce.Stop();
                if (_isWebViewInitialized && webView?.CoreWebView2 != null)
                {
                    try { await webView.ExecuteScriptAsync("window.dispatchEvent(new Event('resize'));"); }
                    catch { }
                }
            };
            _onLeftOrTopChangedHandler = (s, ev) => ScheduleSaveWindowBounds();
            DependencyPropertyDescriptor.FromProperty(Window.LeftProperty, typeof(Window)).AddValueChanged(this, _onLeftOrTopChangedHandler);
            DependencyPropertyDescriptor.FromProperty(Window.TopProperty, typeof(Window)).AddValueChanged(this, _onLeftOrTopChangedHandler);
        }

        private void ScheduleSaveWindowBounds()
        {
            _saveBoundsDebounce?.Stop();
            _saveBoundsDebounce?.Start();
        }

        /// <summary>
        /// 将当前窗口位置和大小写入 ChartWindow_Left/Top/Width/Height。最大化时使用 RestoreBounds。
        /// </summary>
        private void SaveWindowBoundsToConfig()
        {
            try
            {
                if (IsFixedPlacementMode())
                    return;
                var r = WindowState == WindowState.Maximized ? RestoreBounds : new Rect(Left, Top, Width, Height);
                var cfg = AppConfigProvider.Instance;
                cfg.SetValue("ChartWindow_Left", r.Left.ToString(CultureInfo.InvariantCulture));
                cfg.SetValue("ChartWindow_Top", r.Top.ToString(CultureInfo.InvariantCulture));
                cfg.SetValue("ChartWindow_Width", r.Width.ToString(CultureInfo.InvariantCulture));
                cfg.SetValue("ChartWindow_Height", r.Height.ToString(CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebChartWindow] SaveWindowBoundsToConfig: {ex.Message}");
            }
        }

        /// <summary>
        /// 是否处于 A 股交易时段（用于图表缓存 TTL）
        /// </summary>
        private static bool IsInTradingHours()
        {
            var now = DateTime.Now;
            if (now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday)
                return false;
            var time = now.TimeOfDay;
            return (time >= new TimeSpan(9, 30, 0) && time < new TimeSpan(11, 30, 0))
                || (time >= new TimeSpan(13, 0, 0) && time < new TimeSpan(15, 0, 0));
        }

        /// <summary>
        /// 更新窗口显示的股票代码（用于复用窗口，只更新内容）
        /// </summary>
        public async Task UpdateStockCodeAsync(string newStockCode)
        {
            if (string.IsNullOrEmpty(newStockCode))
                return;
            
            // 检查窗口是否已关闭或正在关闭
            if (!this.IsLoaded || this.IsClosing)
                return;
            
            try
            {
                _stockCode = newStockCode;
                this.Title = $"加载中... - {newStockCode}";
                
                // 清空当前数据
                _chartData = null;
                
                // 如果WebView还未初始化，等待初始化完成
                if (!_isWebViewInitialized)
                {
                    await InitializeWebView();
                }
                
                // 再次检查窗口状态（初始化可能耗时）
                if (!this.IsLoaded || this.IsClosing)
                    return;
                
                // 检查 WebView 是否可用
                if (webView == null || webView.CoreWebView2 == null)
                {
                    Console.WriteLine("[WebChartWindow] WebView 不可用，重新初始化");
                    await InitializeWebView();
                    if (webView == null || webView.CoreWebView2 == null)
                    {
                        Console.WriteLine("[WebChartWindow] WebView 初始化失败");
                        return;
                    }
                }
                
                // 重新加载新股票的数据
                await LoadChartDataAsync(newStockCode);
                
                // 再次检查窗口状态（数据加载可能耗时）
                if (!this.IsLoaded || this.IsClosing || _chartData == null)
                    return;
                
                // 如果数据加载成功，更新图表显示
                if (_chartData != null && _isWebViewInitialized && webView?.CoreWebView2 != null)
                {
                    var task = Dispatcher.InvokeAsync(async () =>
                    {
                        if (this.IsLoaded && !this.IsClosing && webView?.CoreWebView2 != null)
                        {
                            await SetChartData();
                        }
                    }, DispatcherPriority.Background);
                    await task;
                }
            }
            catch (ObjectDisposedException)
            {
                Console.WriteLine("[WebChartWindow] 对象已释放，无法更新股票代码");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebChartWindow] 更新股票代码失败: {ex.Message}");
            }
        }
        
        private bool IsClosing { get; set; } = false;

        /// <summary>
        /// 异步加载图表数据（不阻塞UI线程）；盘中同股票短时间复用缓存，减轻卡顿。
        /// </summary>
        private async Task LoadChartDataAsync(string stockCode)
        {
            try
            {
                int ttlSec = IsInTradingHours() ? ChartCacheTTLSecondsTrading : ChartCacheTTLSecondsNonTrading;
                lock (_chartDataCacheLock)
                {
                    if (_chartDataCache.TryGetValue(stockCode, out var cached) && (DateTime.Now - cached.cachedAt).TotalSeconds < ttlSec)
                    {
                        _chartData = cached.data;
                        Console.WriteLine($"[图表数据加载] 使用缓存: {stockCode}（{((DateTime.Now - cached.cachedAt).TotalSeconds):F0}秒前）");
                        return;
                    }
                }
                // 优先使用独立的数据服务（带缓存和预加载优化）
                if (_chartDataService != null)
                {
                    _chartData = await _chartDataService.LoadChartDataAsync(stockCode);
                }
                else
                {
                    // 回退：直接使用 ChartService（如果没有数据服务）
                    await Task.Run(() =>
                    {
                        var chartService = new ChartService(_realTimeCache);
                        _chartData = chartService.LoadChartData(stockCode, 500); // 限制数据量提升性能
                    });
                }
                
                if (_chartData == null) return;
                
                // 预运算：在后台一次性序列化为图表 JSON 并写入缓存，后续 SetChartData 直接复用
                string chartJson = await Task.Run(() => ConvertToChartJson(_chartData));
                lock (_chartDataCacheLock)
                {
                    _chartDataCache[stockCode] = (_chartData, DateTime.Now, chartJson);
                    if (_chartDataCache.Count > 50)
                    {
                        var expired = _chartDataCache.Where(kv => (DateTime.Now - kv.Value.cachedAt).TotalSeconds > ttlSec * 2).Select(kv => kv.Key).ToList();
                        foreach (var k in expired) _chartDataCache.Remove(k);
                    }
                }

                // 回到UI线程更新界面
                if (_chartData == null)
                {
                    Console.WriteLine($"[图表数据加载] ❌ _chartData 为 null");
                    MessageBox.Show($"无法加载股票 {stockCode} 的图表数据", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    this.Close();
                    return;
                }
                
                // 性能优化：减少日志输出，仅在调试模式下输出详细信息
#if DEBUG
                Console.WriteLine($"[图表数据加载] ✅ 数据加载成功");
                Console.WriteLine($"  - 股票代码: {_chartData.StockCode}");
                Console.WriteLine($"  - 股票名称: {_chartData.StockName}");
                Console.WriteLine($"  - 日K线数量: {_chartData.DailyKline?.Count ?? 0}");
                Console.WriteLine($"  - 周KD数量: {_chartData.WeeklyKD?.Count ?? 0}");
                Console.WriteLine($"  - 月KD数量: {_chartData.MonthlyKD?.Count ?? 0}");
                Console.WriteLine($"  - 季KD数量: {_chartData.QuarterlyKD?.Count ?? 0}");
#endif
                
                if (_chartData.DailyKline.Count == 0)
                {
                    Console.WriteLine($"[图表数据加载] ❌ 日K线数据为空");
                    MessageBox.Show($"无法加载股票 {stockCode} 的图表数据", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    this.Close();
                    return;
                }

                this.Title = $"股票图表 - {_chartData.StockName} ({_chartData.StockCode})";
                
                // 如果 WebView 已初始化，立即设置图表数据
                if (_isWebViewInitialized && webView != null && webView.CoreWebView2 != null && !IsClosing && this.IsLoaded)
                {
                    await Dispatcher.InvokeAsync(async () =>
                    {
                        if (!IsClosing && this.IsLoaded && webView != null && webView.CoreWebView2 != null)
                        {
                            await SetChartData();
                        }
                    }, DispatcherPriority.Background);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[图表数据加载] ❌ 异常: {ex.Message}");
                Console.WriteLine($"[图表数据加载] 堆栈: {ex.StackTrace}");
                MessageBox.Show($"加载图表数据失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 确保窗口在正确的屏幕上最大化
                // 窗口位置已在 RestoreWindowBounds() 中设置，现在最大化到该屏幕
                if (WindowState != WindowState.Maximized)
                {
                    Console.WriteLine($"[WebChartWindow] Window_Loaded: 窗口位置 Left={Left}, Top={Top}, Width={Width}, Height={Height}");
                    var screen = System.Windows.Forms.Screen.FromPoint(
                        new System.Drawing.Point((int)(Left + Width / 2), (int)(Top + Height / 2)));
                    if (screen != null)
                    {
                        Console.WriteLine($"[WebChartWindow] Window_Loaded: 窗口中心位于屏幕 {screen.DeviceName}, Bounds={screen.Bounds}");
                        Console.WriteLine($"[WebChartWindow] Window_Loaded: 设置窗口为最大化状态");
                        WindowState = WindowState.Maximized;
                    }
                    else
                    {
                        Console.WriteLine($"[WebChartWindow] Window_Loaded: 无法确定屏幕，使用默认最大化");
                        WindowState = WindowState.Maximized;
                    }
                }
                
                // 性能优化：先初始化WebView，然后异步加载数据
                await InitializeWebView();
                
                // 如果数据还未加载，则异步加载
                if (_chartData == null && !string.IsNullOrEmpty(_stockCode))
                {
                    await LoadChartDataAsync(_stockCode);

                    // 检查窗口状态（数据加载可能耗时）
                    if (IsClosing || !this.IsLoaded || webView == null || webView.CoreWebView2 == null)
                    {
                        Console.WriteLine("[WebChartWindow] Window_Loaded: 窗口已关闭或WebView不可用，跳过设置数据");
                        return;
                    }

                    if (_chartData != null)
                    {
                        this.Title = $"股票图表 - {_chartData.StockName} ({_chartData.StockCode})";
                        // 用 Background 优先级渲染图表，保证窗口先显示、拖动不卡顿
                        await Dispatcher.InvokeAsync(async () =>
                        {
                            if (!IsClosing && this.IsLoaded && webView != null && webView.CoreWebView2 != null)
                            {
                                await SetChartData();
                            }
                        }, DispatcherPriority.Background);
                    }
                }
                else if (_chartData != null)
                {
                    // 检查窗口状态
                    if (IsClosing || !this.IsLoaded || webView == null || webView.CoreWebView2 == null)
                    {
                        Console.WriteLine("[WebChartWindow] Window_Loaded: 窗口已关闭或WebView不可用，跳过设置数据");
                        return;
                    }
                    
                    await Dispatcher.InvokeAsync(async () =>
                    {
                        if (!IsClosing && this.IsLoaded && webView != null && webView.CoreWebView2 != null)
                        {
                            await SetChartData();
                        }
                    }, DispatcherPriority.Background);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化WebView2失败: {ex.Message}\n\n请确保已安装 WebView2 Runtime。",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private async Task InitializeWebView()
        {
            Console.WriteLine($"───────────────────────────────────────────────────────");
            Console.WriteLine($"[WebView初始化] 开始初始化 WebView2");
            
            // 初始化WebView2，指定用户数据文件夹
            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MQReceiver", "WebView2");
            Console.WriteLine($"[WebView初始化] 用户数据文件夹: {userDataFolder}");

            Console.WriteLine($"[WebView初始化] 创建 CoreWebView2Environment...");
            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            Console.WriteLine($"[WebView初始化] ✅ Environment 创建成功");
            
            Console.WriteLine($"[WebView初始化] 确保 CoreWebView2 初始化...");
            await webView.EnsureCoreWebView2Async(env);
            Console.WriteLine($"[WebView初始化] ✅ CoreWebView2 初始化成功");

            _isWebViewInitialized = true;

            Console.WriteLine($"[WebView初始化] 使用内嵌式资源加载图表，完全自包含，不依赖外部文件");
            
            try
            {
                Console.WriteLine($"[WebView初始化] 步骤1: 读取嵌入式JS库资源...");
                // 读取嵌入式JS库资源
                string jsContent = GetEmbeddedResource("lib.lightweight-charts.js");
                
                if (string.IsNullOrEmpty(jsContent))
                {
                    Console.WriteLine($"[WebView初始化] ⚠️ 警告: 无法读取嵌入式JS资源，将使用CDN版本");
                    jsContent = ""; // 使用CDN作为后备
                }
                else
                {
                    Console.WriteLine($"[WebView初始化] ✅ 成功读取嵌入式JS库");
                    Console.WriteLine($"[WebView初始化]    大小: {jsContent.Length / 1024:F1} KB ({jsContent.Length} 字节)");
                }
                
                Console.WriteLine($"[WebView初始化] 步骤2: 读取嵌入式HTML模板...");
                // 读取嵌入式HTML模板
                string htmlTemplate = GetEmbeddedResource("stock-chart.html");
                
                if (string.IsNullOrEmpty(htmlTemplate))
                {
                    Console.WriteLine($"[WebView初始化] ⚠️ 警告: 无法读取嵌入式HTML，使用备用HTML");
                    htmlTemplate = GenerateFullHtml(jsContent);
                }
                else
                {
                    Console.WriteLine($"[WebView初始化] ✅ 成功读取嵌入式HTML模板");
                    Console.WriteLine($"[WebView初始化]    大小: {htmlTemplate.Length / 1024:F1} KB");
                    
                    // 将JS库内容嵌入到HTML中（替换占位符）
                    if (!string.IsNullOrEmpty(jsContent))
                    {
                        // 替换占位符为嵌入式JS
                        if (htmlTemplate.Contains("PLACEHOLDER_FOR_JS_LIBRARY"))
                        {
                            htmlTemplate = htmlTemplate.Replace(
                                "<!-- PLACEHOLDER_FOR_JS_LIBRARY -->",
                                $"<script>{jsContent}</script>");
                            Console.WriteLine($"[WebView初始化] ✅ 已将JS库嵌入到HTML");
                        }
                        else if (htmlTemplate.Contains("unpkg.com/lightweight-charts"))
                        {
                            htmlTemplate = htmlTemplate.Replace(
                                "<script src=\"https://unpkg.com/lightweight-charts@4.2.0/dist/lightweight-charts.standalone.production.js\"></script>",
                                $"<script>{jsContent}</script>");
                            Console.WriteLine($"[WebView初始化] ✅ 已将CDN引用替换为嵌入式JS");
                        }
                        else
                        {
                            // 如果都没有找到，追加到head结束标签之前
                            htmlTemplate = htmlTemplate.Replace("</head>", $"<script>{jsContent}</script></head>");
                            Console.WriteLine($"[WebView初始化] ✅ 已将JS库追加到head");
                        }
                    }
                }
                
                Console.WriteLine($"[WebView初始化] 步骤3: 使用NavigateToString加载HTML...");
                Console.WriteLine($"[WebView初始化]    最终HTML大小: {htmlTemplate.Length / 1024:F1} KB");
                // 使用NavigateToString直接加载HTML字符串
                webView.NavigateToString(htmlTemplate);
                Console.WriteLine($"[WebView初始化] ✅ NavigateToString 调用成功");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebView初始化] ❌ 加载资源失败: {ex.Message}");
                Console.WriteLine($"[WebView初始化] 堆栈: {ex.StackTrace}");
                // 如果失败，使用简单的后备HTML（使用CDN）
                Console.WriteLine($"[WebView初始化] 使用后备方案（CDN版本）");
                webView.NavigateToString(GetEmbeddedHtml());
            }

            // 等待页面加载完成
            webView.NavigationCompleted += async (s, args) =>
            {
                try
                {
                    // 检查窗口状态和 WebView 是否可用
                    if (IsClosing || !this.IsLoaded || webView == null || webView.CoreWebView2 == null)
                    {
                        Console.WriteLine("[WebView初始化] ⚠️ 页面导航完成，但窗口已关闭或WebView不可用，跳过设置数据");
                        return;
                    }
                    
                    if (args.IsSuccess)
                    {
                    Console.WriteLine("[WebView初始化] ✅ 页面导航成功");
                    Console.WriteLine("[WebView初始化] 准备设置图表数据...");
                    // 页面就绪后：如果数据已加载，立即渲染；否则保持“加载中”，等待数据加载完成后再渲染
                    if (_chartData != null)
                    {
                        // 再次检查窗口状态（可能在检查后发生了变化）
                        if (IsClosing || !this.IsLoaded || webView == null || webView.CoreWebView2 == null)
                        {
                            Console.WriteLine("[WebView初始化] ⚠️ 窗口状态已变化，跳过设置数据");
                            return;
                        }
                        
                        loadingText.Visibility = Visibility.Collapsed;
                        var task = Dispatcher.InvokeAsync(async () =>
                        {
                            // 第三次检查（在UI线程中）
                            if (!IsClosing && this.IsLoaded && webView != null && webView.CoreWebView2 != null)
                            {
                                await SetChartData();
                            }
                        }, DispatcherPriority.Background);
                        await task; // 等待任务完成
                    }
                    else
                    {
                        Console.WriteLine("[WebView初始化] ⚠️ 图表数据尚未加载，等待数据加载完成...");
                    }
                    }
                    else
                    {
                        Console.WriteLine($"[WebView初始化] ❌ 页面导航失败: {args.WebErrorStatus}");
                    }
                }
                catch (ObjectDisposedException)
                {
                    Console.WriteLine("[WebView初始化] ⚠️ WebView对象已释放，无法设置数据");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WebView初始化] ⚠️ 设置图表数据时出错: {ex.Message}");
                }
            };
            
            Console.WriteLine($"[WebView初始化] 等待页面加载完成...");
            Console.WriteLine($"───────────────────────────────────────────────────────");
        }


        private async Task SetChartData()
        {
            Console.WriteLine($"───────────────────────────────────────────────────────");
            Console.WriteLine($"[设置图表数据] 开始设置图表数据");
            
            // 检查窗口状态
            if (IsClosing || !this.IsLoaded)
            {
                Console.WriteLine($"[设置图表数据] ❌ 窗口已关闭或正在关闭");
                return;
            }
            
            if (!_isWebViewInitialized)
            {
                Console.WriteLine($"[设置图表数据] ❌ WebView未初始化");
                return;
            }
            
            if (_chartData == null)
            {
                Console.WriteLine($"[设置图表数据] ❌ 图表数据为null");
                return;
            }
            
            // 检查 WebView 是否可用
            if (webView == null || webView.CoreWebView2 == null)
            {
                Console.WriteLine($"[设置图表数据] ❌ WebView不可用");
                return;
            }

            Console.WriteLine($"[设置图表数据] ✅ 前置条件检查通过");

            try
            {
                // 优先使用缓存的预序列化 JSON，避免重复大对象序列化导致卡顿
                string chartJson;
                int ttlSec = IsInTradingHours() ? ChartCacheTTLSecondsTrading : ChartCacheTTLSecondsNonTrading;
                lock (_chartDataCacheLock)
                {
                    if (_chartDataCache.TryGetValue(_chartData.StockCode, out var cached) &&
                        (DateTime.Now - cached.cachedAt).TotalSeconds < ttlSec &&
                        cached.data == _chartData &&
                        !string.IsNullOrEmpty(cached.chartJson))
                    {
                        chartJson = cached.chartJson;
                    }
                    else
                    {
                        chartJson = null;
                    }
                }
                if (string.IsNullOrEmpty(chartJson))
                {
                    // 在后台线程序列化JSON，避免阻塞UI
                    chartJson = await Task.Run(() => ConvertToChartJson(_chartData));
                    // 计算后写回缓存，供同股票再次 SetChartData（如页面重载）时复用
                    lock (_chartDataCacheLock)
                    {
                        if (_chartDataCache.TryGetValue(_chartData.StockCode, out var existing))
                            _chartDataCache[_chartData.StockCode] = (existing.data, existing.cachedAt, chartJson);
                        else
                            _chartDataCache[_chartData.StockCode] = (_chartData, DateTime.Now, chartJson);
                    }
                }
                string script = $"setAllData({chartJson});";
                
                // 再次检查窗口状态和 WebView 可用性
                if (IsClosing || !this.IsLoaded || webView == null || webView.CoreWebView2 == null)
                {
                    Console.WriteLine($"[设置图表数据] ❌ 执行脚本前检查失败：窗口状态异常或WebView不可用");
                    return;
                }
                
                try
                {
                    var result = await webView.ExecuteScriptAsync(script);
                    
                    if (result != null && result.Contains("success"))
                    {
                        Console.WriteLine($"[设置图表数据] ✅✅✅ 图表数据设置成功！");
                    }
                    else if (result != null && result.Contains("error"))
                    {
                        Console.WriteLine($"[设置图表数据] ⚠️ JavaScript返回错误: {result}");
                    }
                    
                    Console.WriteLine($"───────────────────────────────────────────────────────");
                }
                catch (ObjectDisposedException)
                {
                    Console.WriteLine($"[设置图表数据] ❌ WebView对象已释放，无法设置数据");
                    Console.WriteLine($"───────────────────────────────────────────────────────");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[设置图表数据] ❌ 执行脚本失败: {ex.Message}");
                    Console.WriteLine($"───────────────────────────────────────────────────────");
                }
            }
            catch (ObjectDisposedException)
            {
                Console.WriteLine($"[设置图表数据] ❌ WebView对象已释放");
                Console.WriteLine($"───────────────────────────────────────────────────────");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[设置图表数据] ❌ 设置失败: {ex.Message}");
                Console.WriteLine($"[设置图表数据] 堆栈: {ex.StackTrace}");
                Console.WriteLine($"───────────────────────────────────────────────────────");
            }
        }

        private string ConvertToChartJson(ChartData data)
        {
            // 性能优化：并行转换KD数据
            var weeklyKTask = Task.Run(() => ConvertKDData(data.WeeklyKD, true));
            var weeklyDTask = Task.Run(() => ConvertKDData(data.WeeklyKD, false));
            var monthlyKTask = Task.Run(() => ConvertKDData(data.MonthlyKD, true));
            var monthlyDTask = Task.Run(() => ConvertKDData(data.MonthlyKD, false));
            var quarterlyKTask = Task.Run(() => ConvertKDData(data.QuarterlyKD, true));
            var quarterlyDTask = Task.Run(() => ConvertKDData(data.QuarterlyKD, false));
            
            // 并行转换K线和成交量数据
            var candlesTask = Task.Run(() => ConvertCandleData(data.DailyKline));
            var volumesTask = Task.Run(() => ConvertVolumeData(data.DailyKline));
            
            // 等待所有转换完成
            Task.WaitAll(weeklyKTask, weeklyDTask, monthlyKTask, monthlyDTask, quarterlyKTask, quarterlyDTask, candlesTask, volumesTask);

            var chartData = new
            {
                stockName = data.StockName,
                stockCode = data.StockCode,
                candles = candlesTask.Result,
                volumes = volumesTask.Result,
                weeklyK = weeklyKTask.Result,
                weeklyD = weeklyDTask.Result,
                monthlyK = monthlyKTask.Result,
                monthlyD = monthlyDTask.Result,
                quarterlyK = quarterlyKTask.Result,
                quarterlyD = quarterlyDTask.Result,
            };

            // 使用更快的序列化设置
            return JsonConvert.SerializeObject(chartData, new JsonSerializerSettings
            {
                Formatting = Formatting.None, // 不格式化，减少字符串大小
                NullValueHandling = NullValueHandling.Ignore // 忽略null值
            });
        }

        private object[] ConvertCandleData(System.Collections.Generic.IList<CandleDataPoint> candles)
        {
            if (candles == null || candles.Count == 0)
                return Array.Empty<object>();

            var result = new object[candles.Count];
            for (int i = 0; i < candles.Count; i++)
            {
                var c = candles[i];
                result[i] = new
                {
                    time = new { year = c.Date.Year, month = c.Date.Month, day = c.Date.Day },
                    open = c.Open,
                    high = c.High,
                    low = c.Low,
                    close = c.Close
                };
            }
            return result;
        }

        private object[] ConvertVolumeData(System.Collections.Generic.IList<CandleDataPoint> candles)
        {
            if (candles == null || candles.Count == 0)
                return Array.Empty<object>();

            var result = new object[candles.Count];
            for (int i = 0; i < candles.Count; i++)
            {
                var c = candles[i];
                result[i] = new
                {
                    time = new { year = c.Date.Year, month = c.Date.Month, day = c.Date.Day },
                    value = c.Volume,
                    color = c.IsRising ? "rgba(239, 83, 80, 0.5)" : "rgba(38, 166, 154, 0.5)"
                };
            }
            return result;
        }

        private object[] ConvertKDData(System.Collections.Generic.List<KDDataPoint> kdList, bool isK)
        {
            if (kdList == null || kdList.Count == 0)
                return new object[0];

            var result = new object[kdList.Count];
            for (int i = 0; i < kdList.Count; i++)
            {
                var kd = kdList[i];
                result[i] = new
                {
                    time = new { year = kd.Date.Year, month = kd.Date.Month, day = kd.Date.Day },
                    value = isK ? kd.K : kd.D
                };
            }
            return result;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            IsClosing = true;
            _saveBoundsDebounce?.Stop();
            try
            {
                DependencyPropertyDescriptor.FromProperty(Window.LeftProperty, typeof(Window)).RemoveValueChanged(this, _onLeftOrTopChangedHandler);
                DependencyPropertyDescriptor.FromProperty(Window.TopProperty, typeof(Window)).RemoveValueChanged(this, _onLeftOrTopChangedHandler);
            }
            catch { }
            SaveWindowBoundsToConfig();
            if (webView != null)
            {
                try
                {
                    webView.Dispose();
                }
                catch { }
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ScheduleSaveWindowBounds();
            // 防抖：拖动/缩放时不在每帧调用 JS resize，减轻卡顿
            _resizeDebounce?.Stop();
            _resizeDebounce?.Start();
        }

        /// <summary>
        /// 从嵌入式资源读取文本内容
        /// </summary>
        private string GetEmbeddedResource(string resourceName)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var fullResourceName = $"MQReceiver.src.UI.WebChart.{resourceName}";
                
                Console.WriteLine($"[资源加载] 尝试加载资源: {fullResourceName}");
                
                using (Stream stream = assembly.GetManifestResourceStream(fullResourceName))
                {
                    if (stream == null)
                    {
                        // 列出所有可用资源以便调试
                        var availableResources = assembly.GetManifestResourceNames();
                        Console.WriteLine($"[资源加载] 可用的嵌入式资源:");
                        foreach (var res in availableResources)
                        {
                            Console.WriteLine($"  - {res}");
                        }
                        return null;
                    }
                    
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        var content = reader.ReadToEnd();
                        Console.WriteLine($"[资源加载] 成功读取资源，大小: {content.Length} 字节");
                        return content;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[资源加载] 加载资源失败: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 生成完整的HTML，包含内嵌的JS库
        /// </summary>
        private string GenerateFullHtml(string jsLibContent)
        {
            // 如果JS库内容为空，使用CDN
            string scriptTag = string.IsNullOrEmpty(jsLibContent)
                ? @"<script src=""https://unpkg.com/lightweight-charts@4.2.0/dist/lightweight-charts.standalone.production.js""></script>"
                : $@"<script>{jsLibContent}</script>";
            
            return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <title>股票图表</title>
    {scriptTag}
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ font-family: 'Microsoft YaHei', Arial, sans-serif; background-color: #1a1a2e; color: #eee; overflow: hidden; }}
        .header {{ background-color: #2C3E50; padding: 10px 15px; }}
        .stock-name {{ font-size: 18px; font-weight: bold; color: white; }}
        .chart-container {{ width: 100%; height: calc(100vh - 50px); display: flex; flex-direction: column; }}
        .main-chart {{ flex: 3; position: relative; }}
        .sub-chart {{ flex: 1; border-top: 1px solid #333; position: relative; }}
        .chart-label {{ position: absolute; top: 5px; left: 10px; font-size: 12px; color: #888; z-index: 10; }}
    </style>
</head>
<body>
    <div class=""header"">
        <span class=""stock-name"" id=""stockName"">--</span>
    </div>
    <div class=""chart-container"">
        <div class=""main-chart"" id=""mainChart""><div class=""chart-label"">K线图</div></div>
        <div class=""sub-chart"" id=""weeklyBandChart""><div class=""chart-label"">周KD</div></div>
        <div class=""sub-chart"" id=""monthlyBandChart""><div class=""chart-label"">月KD</div></div>
        <div class=""sub-chart"" id=""quarterlyBandChart""><div class=""chart-label"">季KD</div></div>
    </div>
    <script>
        console.log('[Chart] LightweightCharts:', typeof LightweightCharts !== 'undefined' ? 'OK' : 'FAIL');
        let mainChart, weeklyBandChart, monthlyBandChart, quarterlyBandChart;
        let candleSeries, volumeSeries, weeklyBandSeries, monthlyBandSeries, quarterlyBandSeries;
        
        function initCharts() {{
            const opts = {{ layout: {{ background: {{ type: 'solid', color: '#1a1a2e' }}, textColor: '#DDD' }}, grid: {{ vertLines: {{ color: '#2B2B43' }}, horzLines: {{ color: '#2B2B43' }} }} }};
            mainChart = LightweightCharts.createChart(document.getElementById('mainChart'), {{ ...opts, height: document.getElementById('mainChart').clientHeight }});
            candleSeries = mainChart.addCandlestickSeries({{ upColor: '#ef5350', downColor: '#26a69a' }});
            volumeSeries = mainChart.addHistogramSeries({{ priceFormat: {{ type: 'volume' }}, priceScaleId: 'volume' }});
            mainChart.priceScale('volume').applyOptions({{ scaleMargins: {{ top: 0.8, bottom: 0 }} }});
            
            weeklyBandChart = LightweightCharts.createChart(document.getElementById('weeklyBandChart'), {{ ...opts, height: document.getElementById('weeklyBandChart').clientHeight }});
            weeklyBandSeries = weeklyBandChart.addBaselineSeries({{ baseValue: {{ type: 'price', price: 0 }}, topLineColor: '#00FF00', topFillColor1: 'rgba(0,255,0,0.3)', bottomLineColor: '#00FF00', bottomFillColor1: 'rgba(0,255,0,0.3)' }});
            
            monthlyBandChart = LightweightCharts.createChart(document.getElementById('monthlyBandChart'), {{ ...opts, height: document.getElementById('monthlyBandChart').clientHeight }});
            monthlyBandSeries = monthlyBandChart.addBaselineSeries({{ baseValue: {{ type: 'price', price: 0 }}, topLineColor: '#00FF00', topFillColor1: 'rgba(0,255,0,0.3)', bottomLineColor: '#00FF00', bottomFillColor1: 'rgba(0,255,0,0.3)' }});
            
            quarterlyBandChart = LightweightCharts.createChart(document.getElementById('quarterlyBandChart'), {{ ...opts, height: document.getElementById('quarterlyBandChart').clientHeight }});
            quarterlyBandSeries = quarterlyBandChart.addBaselineSeries({{ baseValue: {{ type: 'price', price: 0 }}, topLineColor: '#00FF00', topFillColor1: 'rgba(0,255,0,0.3)', bottomLineColor: '#00FF00', bottomFillColor1: 'rgba(0,255,0,0.3)' }});
            
            console.log('[Chart] Init OK');
        }}
        
        function setStockInfo(name, code) {{ document.getElementById('stockName').textContent = name + ' (' + code + ')'; }}
        
        function calculateKDDiff(kData, dData) {{
            if (!kData || !dData) return [];
            const result = [];
            for (let i = 0; i < Math.min(kData.length, dData.length); i++) {{
                if (kData[i] && dData[i]) result.push({{ time: kData[i].time, value: kData[i].value - dData[i].value }});
            }}
            return result;
        }}
        
        function setAllData(jsonData) {{
            try {{
                const data = typeof jsonData === 'string' ? JSON.parse(jsonData) : jsonData;
                if (data.stockName) setStockInfo(data.stockName, data.stockCode);
                if (data.candles) candleSeries.setData(data.candles);
                if (data.volumes) volumeSeries.setData(data.volumes);
                weeklyBandSeries.setData(calculateKDDiff(data.weeklyK, data.weeklyD));
                monthlyBandSeries.setData(calculateKDDiff(data.monthlyK, data.monthlyD));
                quarterlyBandSeries.setData(calculateKDDiff(data.quarterlyK, data.quarterlyD));
                mainChart.timeScale().fitContent();
                console.log('[Chart] Data loaded');
                return 'success';
            }} catch (e) {{ console.error('[Chart] Error:', e); return 'error: ' + e.message; }}
        }}
        
        document.addEventListener('DOMContentLoaded', initCharts);
        if (document.readyState === 'complete') initCharts();
    </script>
</body>
</html>";
        }

        private string GetEmbeddedHtml()
        {
            // 内嵌的HTML作为后备方案
            return @"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
    <meta charset=""UTF-8"">
    <title>股票图表</title>
    <script src=""https://unpkg.com/lightweight-charts@4.2.0/dist/lightweight-charts.standalone.production.js""></script>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { font-family: 'Microsoft YaHei', Arial, sans-serif; background-color: #1a1a2e; color: #eee; }
        .header { background-color: #2C3E50; padding: 10px 15px; display: flex; align-items: center; gap: 15px; }
        .stock-name { font-size: 18px; font-weight: bold; color: white; }
        .stock-code { font-size: 14px; color: #ECF0F1; }
        .chart-container { width: 100%; height: calc(100vh - 50px); display: flex; flex-direction: column; }
        .main-chart { flex: 3; min-height: 0; position: relative; }
        .sub-chart { flex: 1; min-height: 0; border-top: 1px solid #333; position: relative; }
        .chart-label { position: absolute; top: 5px; left: 10px; font-size: 12px; color: #888; z-index: 10; }
    </style>
</head>
<body>
    <div class=""header"">
        <span class=""stock-name"" id=""stockName"">--</span>
        <span class=""stock-code"" id=""stockCode"">--</span>
    </div>
    <div class=""chart-container"">
        <div class=""main-chart"" id=""mainChart""><div class=""chart-label"">日K线 + 成交量</div></div>
        <div class=""sub-chart"" id=""weeklyKDChart""><div class=""chart-label"">周KD (K-白 D-黄)</div></div>
        <div class=""sub-chart"" id=""monthlyKDChart""><div class=""chart-label"">月KD (K-白 D-黄)</div></div>
        <div class=""sub-chart"" id=""quarterlyKDChart""><div class=""chart-label"">季KD (K-白 D-黄)</div></div>
    </div>
    <script>
        let mainChart, weeklyKDChart, monthlyKDChart, quarterlyKDChart;
        let candleSeries, volumeSeries, weeklyKSeries, weeklyDSeries, monthlyKSeries, monthlyDSeries, quarterlyKSeries, quarterlyDSeries;
        const chartOptions = { layout: { background: { type: 'solid', color: '#1a1a2e' }, textColor: '#DDD' }, grid: { vertLines: { color: '#2B2B43' }, horzLines: { color: '#2B2B43' } }, crosshair: { mode: LightweightCharts.CrosshairMode.Normal }, rightPriceScale: { borderColor: '#2B2B43' }, timeScale: { borderColor: '#2B2B43', timeVisible: true } };
        function initCharts() {
            mainChart = LightweightCharts.createChart(document.getElementById('mainChart'), { ...chartOptions, height: document.getElementById('mainChart').clientHeight });
            candleSeries = mainChart.addCandlestickSeries({ upColor: '#ef5350', downColor: '#26a69a', borderUpColor: '#ef5350', borderDownColor: '#26a69a', wickUpColor: '#ef5350', wickDownColor: '#26a69a' });
            volumeSeries = mainChart.addHistogramSeries({ priceFormat: { type: 'volume' }, priceScaleId: 'volume' });
            mainChart.priceScale('volume').applyOptions({ scaleMargins: { top: 0.8, bottom: 0 } });
            weeklyKDChart = LightweightCharts.createChart(document.getElementById('weeklyKDChart'), { ...chartOptions, height: document.getElementById('weeklyKDChart').clientHeight });
            weeklyKSeries = weeklyKDChart.addLineSeries({ color: '#FFFFFF', lineWidth: 1 });
            weeklyDSeries = weeklyKDChart.addLineSeries({ color: '#FFD700', lineWidth: 1 });
            monthlyKDChart = LightweightCharts.createChart(document.getElementById('monthlyKDChart'), { ...chartOptions, height: document.getElementById('monthlyKDChart').clientHeight });
            monthlyKSeries = monthlyKDChart.addLineSeries({ color: '#FFFFFF', lineWidth: 1 });
            monthlyDSeries = monthlyKDChart.addLineSeries({ color: '#FFD700', lineWidth: 1 });
            quarterlyKDChart = LightweightCharts.createChart(document.getElementById('quarterlyKDChart'), { ...chartOptions, height: document.getElementById('quarterlyKDChart').clientHeight });
            quarterlyKSeries = quarterlyKDChart.addLineSeries({ color: '#FFFFFF', lineWidth: 1 });
            quarterlyDSeries = quarterlyKDChart.addLineSeries({ color: '#FFD700', lineWidth: 1 });
            syncTimeScales();
        }
        function syncTimeScales() {
            const charts = [mainChart, weeklyKDChart, monthlyKDChart, quarterlyKDChart];
            charts.forEach((chart, i) => { chart.timeScale().subscribeVisibleLogicalRangeChange((range) => { if (range) charts.forEach((c, j) => { if (i !== j) c.timeScale().setVisibleLogicalRange(range); }); }); });
        }
        function setStockInfo(name, code) { document.getElementById('stockName').textContent = name || '--'; document.getElementById('stockCode').textContent = code || '--'; }
        function setAllData(jsonData) {
            try {
                const data = typeof jsonData === 'string' ? JSON.parse(jsonData) : jsonData;
                if (data.stockName) setStockInfo(data.stockName, data.stockCode);
                if (data.candles) candleSeries.setData(data.candles);
                if (data.volumes) volumeSeries.setData(data.volumes);
                if (data.weeklyK) weeklyKSeries.setData(data.weeklyK);
                if (data.weeklyD) weeklyDSeries.setData(data.weeklyD);
                if (data.monthlyK) monthlyKSeries.setData(data.monthlyK);
                if (data.monthlyD) monthlyDSeries.setData(data.monthlyD);
                if (data.quarterlyK) quarterlyKSeries.setData(data.quarterlyK);
                if (data.quarterlyD) quarterlyDSeries.setData(data.quarterlyD);
                mainChart.timeScale().fitContent();
                return 'success';
            } catch (e) { return 'error: ' + e.message; }
        }
        window.addEventListener('resize', () => {
            if (mainChart) mainChart.resize(document.getElementById('mainChart').clientWidth, document.getElementById('mainChart').clientHeight);
            if (weeklyKDChart) weeklyKDChart.resize(document.getElementById('weeklyKDChart').clientWidth, document.getElementById('weeklyKDChart').clientHeight);
            if (monthlyKDChart) monthlyKDChart.resize(document.getElementById('monthlyKDChart').clientWidth, document.getElementById('monthlyKDChart').clientHeight);
            if (quarterlyKDChart) quarterlyKDChart.resize(document.getElementById('quarterlyKDChart').clientWidth, document.getElementById('quarterlyKDChart').clientHeight);
        });
        document.addEventListener('DOMContentLoaded', initCharts);
    </script>
</body>
</html>";
        }
    }
}
