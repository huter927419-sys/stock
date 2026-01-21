using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using MQReceiver.Cache;
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

        public WebChartWindow(string stockCode) : this(stockCode, null)
        {
        }

        public WebChartWindow(string stockCode, RealTimeDataCache realTimeCache)
        {
            InitializeComponent();
            _stockCode = stockCode;
            _realTimeCache = realTimeCache;
            
            // 允许多个图表窗口同时打开
            this.Owner = null;
            
            // 设置初始标题
            this.Title = $"加载中... - {stockCode}";
            
            // 同步加载数据（确保WebView初始化时数据已准备好）
            Console.WriteLine($"═══════════════════════════════════════════════════════");
            Console.WriteLine($"[图表数据加载] 开始同步加载股票: {stockCode}");
            var chartService = new ChartService(_realTimeCache);
            _chartData = chartService.LoadChartData(stockCode, 0);
            
            if (_chartData != null)
            {
                Console.WriteLine($"[图表数据加载] ✅ 数据加载成功");
                Console.WriteLine($"  - 日K线: {_chartData.DailyKline?.Count ?? 0} 条");
                Console.WriteLine($"  - 周KD: {_chartData.WeeklyKD?.Count ?? 0} 条");
                this.Title = $"股票图表 - {_chartData.StockName} ({_chartData.StockCode})";
            }
            else
            {
                Console.WriteLine($"[图表数据加载] ❌ 数据加载失败");
            }
            Console.WriteLine($"═══════════════════════════════════════════════════════");
        }

        public WebChartWindow(ChartData chartData)
        {
            InitializeComponent();
            _chartData = chartData;
            if (chartData != null)
            {
                this.Title = $"股票图表 - {chartData.StockName} ({chartData.StockCode})";
            }
        }

        /// <summary>
        /// 异步加载图表数据（不阻塞UI线程）
        /// </summary>
        private async Task LoadChartDataAsync(string stockCode)
        {
            try
            {
                Console.WriteLine($"═══════════════════════════════════════════════════════");
                Console.WriteLine($"[图表数据加载] 开始异步加载股票: {stockCode}");
                Console.WriteLine($"[图表数据加载] 时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                
                // 在后台线程加载数据
                await Task.Run(() =>
                {
                    var chartService = new ChartService(_realTimeCache);
                    Console.WriteLine($"[图表数据加载] ChartService 创建成功");
                    
                    _chartData = chartService.LoadChartData(stockCode, 0); // 0表示加载所有历史数据
                    Console.WriteLine($"[图表数据加载] LoadChartData 返回");
                });

                // 回到UI线程更新界面
                if (_chartData == null)
                {
                    Console.WriteLine($"[图表数据加载] ❌ _chartData 为 null");
                    MessageBox.Show($"无法加载股票 {stockCode} 的图表数据", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    this.Close();
                    return;
                }
                
                Console.WriteLine($"[图表数据加载] ✅ 数据加载成功");
                Console.WriteLine($"  - 股票代码: {_chartData.StockCode}");
                Console.WriteLine($"  - 股票名称: {_chartData.StockName}");
                Console.WriteLine($"  - 日K线数量: {_chartData.DailyKline?.Count ?? 0}");
                Console.WriteLine($"  - 周KD数量: {_chartData.WeeklyKD?.Count ?? 0}");
                Console.WriteLine($"  - 月KD数量: {_chartData.MonthlyKD?.Count ?? 0}");
                Console.WriteLine($"  - 季KD数量: {_chartData.QuarterlyKD?.Count ?? 0}");
                
                if (_chartData.DailyKline?.Count > 0)
                {
                    Console.WriteLine($"  - 日K线日期范围: {_chartData.DailyKline.First().Date:yyyy-MM-dd} ~ {_chartData.DailyKline.Last().Date:yyyy-MM-dd}");
                }
                
                if (_chartData.WeeklyKD?.Count > 0)
                {
                    Console.WriteLine($"  - 周KD日期范围: {_chartData.WeeklyKD.First().Date:yyyy-MM-dd} ~ {_chartData.WeeklyKD.Last().Date:yyyy-MM-dd}");
                    Console.WriteLine($"  - 周KD最新值: K={_chartData.WeeklyKD.Last().K:F2}, D={_chartData.WeeklyKD.Last().D:F2}, 差值={(_chartData.WeeklyKD.Last().K - _chartData.WeeklyKD.Last().D):F2}");
                }
                
                if (_chartData.DailyKline.Count == 0)
                {
                    Console.WriteLine($"[图表数据加载] ❌ 日K线数据为空");
                    MessageBox.Show($"无法加载股票 {stockCode} 的图表数据", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    this.Close();
                    return;
                }

                this.Title = $"股票图表 - {_chartData.StockName} ({_chartData.StockCode})";
                Console.WriteLine($"[图表数据加载] ✅ 加载完成");
                Console.WriteLine($"═══════════════════════════════════════════════════════");
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
                await InitializeWebView();
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
                if (args.IsSuccess)
                {
                    Console.WriteLine("[WebView初始化] ✅ 页面导航成功");
                    Console.WriteLine("[WebView初始化] 准备设置图表数据...");
                    loadingText.Visibility = Visibility.Collapsed;
                    await SetChartData();
                }
                else
                {
                    Console.WriteLine($"[WebView初始化] ❌ 页面导航失败: {args.WebErrorStatus}");
                }
            };
            
            Console.WriteLine($"[WebView初始化] 等待页面加载完成...");
            Console.WriteLine($"───────────────────────────────────────────────────────");
        }


        private async Task SetChartData()
        {
            Console.WriteLine($"───────────────────────────────────────────────────────");
            Console.WriteLine($"[设置图表数据] 开始设置图表数据");
            
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

            Console.WriteLine($"[设置图表数据] ✅ 前置条件检查通过");

            try
            {
                Console.WriteLine($"[设置图表数据] 步骤1: 转换为JSON...");
                var chartJson = ConvertToChartJson(_chartData);
                Console.WriteLine($"[设置图表数据] ✅ JSON转换成功，大小: {chartJson.Length / 1024:F1} KB");
                
                // 输出JSON片段（前200字符）
                string jsonPreview = chartJson.Length > 200 ? chartJson.Substring(0, 200) + "..." : chartJson;
                Console.WriteLine($"[设置图表数据] JSON预览: {jsonPreview}");
                
                Console.WriteLine($"[设置图表数据] 步骤2: 执行JavaScript脚本...");
                string script = $"setAllData({chartJson});";
                Console.WriteLine($"[设置图表数据] 脚本长度: {script.Length} 字节");
                
                var result = await webView.ExecuteScriptAsync(script);
                Console.WriteLine($"[设置图表数据] ✅ JavaScript执行完成");
                Console.WriteLine($"[设置图表数据] 执行结果: {result}");
                
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
            catch (Exception ex)
            {
                Console.WriteLine($"[设置图表数据] ❌ 设置失败: {ex.Message}");
                Console.WriteLine($"[设置图表数据] 堆栈: {ex.StackTrace}");
                Console.WriteLine($"───────────────────────────────────────────────────────");
            }
        }

        private string ConvertToChartJson(ChartData data)
        {
            // KD数据现在已经与日K线完全对齐（每个交易日都有对应的KD值）
            // 直接转换即可，不需要额外映射

            // 调试：输出KD数据量
            Console.WriteLine($"[WebChart调试] 股票: {data.StockCode}");
            Console.WriteLine($"  日K线数据量: {data.DailyKline?.Count ?? 0}");
            Console.WriteLine($"  周KD数据量: {data.WeeklyKD?.Count ?? 0}");
            Console.WriteLine($"  月KD数据量: {data.MonthlyKD?.Count ?? 0}");
            Console.WriteLine($"  季KD数据量: {data.QuarterlyKD?.Count ?? 0}");

            // 输出KD数据样例（前3条和后3条）
            if (data.WeeklyKD?.Count > 0)
            {
                Console.WriteLine($"  周KD数据样例:");
                for (int i = 0; i < Math.Min(3, data.WeeklyKD.Count); i++)
                {
                    var kd = data.WeeklyKD[i];
                    Console.WriteLine($"    [{i}] Date={kd.Date:yyyy-MM-dd}, K={kd.K:F2}, D={kd.D:F2}, K-D={kd.K - kd.D:F2}");
                }
                if (data.WeeklyKD.Count > 3)
                {
                    Console.WriteLine($"    ...");
                    for (int i = Math.Max(0, data.WeeklyKD.Count - 3); i < data.WeeklyKD.Count; i++)
                    {
                        var kd = data.WeeklyKD[i];
                        Console.WriteLine($"    [{i}] Date={kd.Date:yyyy-MM-dd}, K={kd.K:F2}, D={kd.D:F2}, K-D={kd.K - kd.D:F2}");
                    }
                }
            }
            else
            {
                Console.WriteLine($"  ⚠️ 警告: 周KD数据为空！");
            }

            if (data.MonthlyKD?.Count > 0)
            {
                var sample = data.MonthlyKD[data.MonthlyKD.Count - 1];
                Console.WriteLine($"  月KD最后一条: Date={sample.Date:yyyy-MM-dd}, K={sample.K:F2}, D={sample.D:F2}, K-D={sample.K - sample.D:F2}");
            }
            else
            {
                Console.WriteLine($"  ⚠️ 警告: 月KD数据为空！");
            }

            if (data.QuarterlyKD?.Count > 0)
            {
                var sample = data.QuarterlyKD[data.QuarterlyKD.Count - 1];
                Console.WriteLine($"  季KD最后一条: Date={sample.Date:yyyy-MM-dd}, K={sample.K:F2}, D={sample.D:F2}, K-D={sample.K - sample.D:F2}");
            }
            else
            {
                Console.WriteLine($"  ⚠️ 警告: 季KD数据为空！");
            }

            // 转换KD数据（已做平滑处理）
            Console.WriteLine("[WebChart调试] 开始转换KD数据...");
            var weeklyK = ConvertKDData(data.WeeklyKD, true);
            var weeklyD = ConvertKDData(data.WeeklyKD, false);
            var monthlyK = ConvertKDData(data.MonthlyKD, true);
            var monthlyD = ConvertKDData(data.MonthlyKD, false);
            var quarterlyK = ConvertKDData(data.QuarterlyKD, true);
            var quarterlyD = ConvertKDData(data.QuarterlyKD, false);
            
            // 验证K和D数据的时间点是否匹配
            Console.WriteLine("[WebChart调试] 验证K和D数据的时间点匹配...");
            if (weeklyK.Length > 0 && weeklyD.Length > 0)
            {
                Console.WriteLine($"  周KD: K数据={weeklyK.Length}条, D数据={weeklyD.Length}条");
                if (weeklyK.Length == weeklyD.Length)
                {
                    // 检查前5条和后5条的时间是否匹配，并计算K-D差值范围
                    Console.WriteLine("  前5条数据:");
                    decimal minDiff = decimal.MaxValue, maxDiff = decimal.MinValue;
                    for (int i = 0; i < Math.Min(5, weeklyK.Length); i++)
                    {
                        var kTime = ((dynamic)weeklyK[i]).time;
                        var dTime = ((dynamic)weeklyD[i]).time;
                        decimal kValue = Convert.ToDecimal(((dynamic)weeklyK[i]).value);
                        decimal dValue = Convert.ToDecimal(((dynamic)weeklyD[i]).value);
                        decimal diff = kValue - dValue;
                        var timeMatch = kTime.year == dTime.year && kTime.month == dTime.month && kTime.day == dTime.day;
                        Console.WriteLine($"    [{i}] 日期={kTime.year}-{kTime.month:D2}-{kTime.day:D2}, 匹配={timeMatch}, K={kValue:F2}, D={dValue:F2}, K-D={diff:F2}");
                        if (diff < minDiff) minDiff = diff;
                        if (diff > maxDiff) maxDiff = diff;
                    }
                    
                    Console.WriteLine("  后5条数据:");
                    for (int i = Math.Max(0, weeklyK.Length - 5); i < weeklyK.Length; i++)
                    {
                        var kTime = ((dynamic)weeklyK[i]).time;
                        var dTime = ((dynamic)weeklyD[i]).time;
                        decimal kValue = Convert.ToDecimal(((dynamic)weeklyK[i]).value);
                        decimal dValue = Convert.ToDecimal(((dynamic)weeklyD[i]).value);
                        decimal diff = kValue - dValue;
                        var timeMatch = kTime.year == dTime.year && kTime.month == dTime.month && kTime.day == dTime.day;
                        Console.WriteLine($"    [{i}] 日期={kTime.year}-{kTime.month:D2}-{kTime.day:D2}, 匹配={timeMatch}, K={kValue:F2}, D={dValue:F2}, K-D={diff:F2}");
                        if (diff < minDiff) minDiff = diff;
                        if (diff > maxDiff) maxDiff = diff;
                    }
                    
                    // 计算所有数据的K-D差值范围
                    for (int i = 5; i < Math.Max(0, weeklyK.Length - 5); i++)
                    {
                        decimal kValue = Convert.ToDecimal(((dynamic)weeklyK[i]).value);
                        decimal dValue = Convert.ToDecimal(((dynamic)weeklyD[i]).value);
                        decimal diff = kValue - dValue;
                        if (diff < minDiff) minDiff = diff;
                        if (diff > maxDiff) maxDiff = diff;
                    }
                    
                    Console.WriteLine($"  K-D差值范围: 最小={minDiff:F2}, 最大={maxDiff:F2}, 波动范围={maxDiff - minDiff:F2}");
                    
                    if (maxDiff - minDiff < 0.01m)
                    {
                        Console.WriteLine($"  ⚠️ 警告: K-D差值几乎没有波动！所有差值都接近 {minDiff:F2}");
                        Console.WriteLine($"  这会导致图表显示为直线！");
                    }
                }
                else
                {
                    Console.WriteLine($"  ⚠️ 警告: 周KD的K和D数据量不一致！");
                }
            }
            
            var chartData = new
            {
                stockName = data.StockName,
                stockCode = data.StockCode,
                candles = ConvertCandleData(data.DailyKline),
                volumes = ConvertVolumeData(data.DailyKline),
                weeklyK = weeklyK,
                weeklyD = weeklyD,
                monthlyK = monthlyK,
                monthlyD = monthlyD,
                quarterlyK = quarterlyK,
                quarterlyD = quarterlyD,
            };

            return JsonConvert.SerializeObject(chartData);
        }

        private object[] ConvertCandleData(System.Collections.Generic.List<CandleDataPoint> candles)
        {
            if (candles == null || candles.Count == 0)
                return new object[0];

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

        private object[] ConvertVolumeData(System.Collections.Generic.List<CandleDataPoint> candles)
        {
            if (candles == null || candles.Count == 0)
                return new object[0];

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
            {
                Console.WriteLine($"[ConvertKDData] {(isK ? "K" : "D")}值: 数据列表为空");
                return new object[0];
            }

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
            
            // 调试输出：显示前3条和后3条数据
            if (result.Length > 0)
            {
                Console.WriteLine($"[ConvertKDData] {(isK ? "K" : "D")}值: 共 {result.Length} 条数据");
                for (int i = 0; i < Math.Min(3, result.Length); i++)
                {
                    var kd = kdList[i];
                    var value = isK ? kd.K : kd.D;
                    Console.WriteLine($"  [{i}] Date={kd.Date:yyyy-MM-dd}, {(isK ? "K" : "D")}={value:F2}, time={kd.Date.Year}-{kd.Date.Month:D2}-{kd.Date.Day:D2}");
                }
                if (result.Length > 3)
                {
                    Console.WriteLine($"  ...");
                    for (int i = Math.Max(0, result.Length - 3); i < result.Length; i++)
                    {
                        var kd = kdList[i];
                        var value = isK ? kd.K : kd.D;
                        Console.WriteLine($"  [{i}] Date={kd.Date:yyyy-MM-dd}, {(isK ? "K" : "D")}={value:F2}, time={kd.Date.Year}-{kd.Date.Month:D2}-{kd.Date.Day:D2}");
                    }
                }
            }
            
            return result;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 清理WebView2资源
            if (webView != null)
            {
                webView.Dispose();
            }
        }

        private async void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 当WPF窗口大小改变时，通知WebView2内的图表重新调整大小
            if (_isWebViewInitialized && webView?.CoreWebView2 != null)
            {
                try
                {
                    // 触发JavaScript的resize事件处理
                    await webView.ExecuteScriptAsync("window.dispatchEvent(new Event('resize'));");
                }
                catch (Exception)
                {
                    // 忽略执行脚本时的异常（窗口正在关闭等情况）
                }
            }
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
