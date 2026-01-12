using System;
using System.Collections.Generic;
using System.IO;
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
            LoadChartData(stockCode);
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

        private void LoadChartData(string stockCode)
        {
            try
            {
                var chartService = new ChartService(_realTimeCache);
                _chartData = chartService.LoadChartData(stockCode, 180);

                if (_chartData == null || _chartData.DailyKline.Count == 0)
                {
                    MessageBox.Show($"无法加载股票 {stockCode} 的图表数据", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    this.Close();
                    return;
                }

                this.Title = $"股票图表 - {_chartData.StockName} ({_chartData.StockCode})";
            }
            catch (Exception ex)
            {
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
            // 初始化WebView2，指定用户数据文件夹
            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MQReceiver", "WebView2");

            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await webView.EnsureCoreWebView2Async(env);

            _isWebViewInitialized = true;

            // 加载HTML文件
            string htmlPath = GetHtmlFilePath();
            if (File.Exists(htmlPath))
            {
                webView.Source = new Uri(htmlPath);
            }
            else
            {
                // 如果文件不存在，使用内嵌HTML
                webView.NavigateToString(GetEmbeddedHtml());
            }

            // 等待页面加载完成
            webView.NavigationCompleted += async (s, args) =>
            {
                if (args.IsSuccess)
                {
                    loadingText.Visibility = Visibility.Collapsed;
                    await SetChartData();
                }
            };
        }

        private string GetHtmlFilePath()
        {
            // 尝试查找HTML文件
            string exePath = Assembly.GetExecutingAssembly().Location;
            string exeDir = Path.GetDirectoryName(exePath);

            // 尝试多个可能的路径
            string[] possiblePaths = new[]
            {
                Path.Combine(exeDir, "src", "UI", "WebChart", "stock-chart.html"),  // 编译后相对路径
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "src", "UI", "WebChart", "stock-chart.html"),
                @"f:\dsfr\mqq\src\UI\WebChart\stock-chart.html"  // 开发时路径
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                    return path;
            }

            return possiblePaths[0];
        }

        private async Task SetChartData()
        {
            if (!_isWebViewInitialized || _chartData == null)
                return;

            try
            {
                var chartJson = ConvertToChartJson(_chartData);
                string script = $"setAllData({chartJson});";
                await webView.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"设置图表数据失败: {ex.Message}");
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

            // 输出前几条KD数据样例
            if (data.WeeklyKD?.Count > 0)
            {
                var sample = data.WeeklyKD[data.WeeklyKD.Count - 1];
                Console.WriteLine($"  周KD最后一条: Date={sample.Date:yyyy-MM-dd}, K={sample.K:F2}, D={sample.D:F2}");
            }

            var chartData = new
            {
                stockName = data.StockName,
                stockCode = data.StockCode,
                candles = ConvertCandleData(data.DailyKline),
                volumes = ConvertVolumeData(data.DailyKline),
                weeklyK = ConvertKDData(data.WeeklyKD, true),
                weeklyD = ConvertKDData(data.WeeklyKD, false),
                monthlyK = ConvertKDData(data.MonthlyKD, true),
                monthlyD = ConvertKDData(data.MonthlyKD, false),
                quarterlyK = ConvertKDData(data.QuarterlyKD, true),
                quarterlyD = ConvertKDData(data.QuarterlyKD, false),
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
