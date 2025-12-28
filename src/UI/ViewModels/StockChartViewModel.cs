using System;
using System.Collections.Generic;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Wpf;
using LiveCharts.Defaults;
using MQReceiver.Models;

namespace MQReceiver.ViewModels
{
    /// <summary>
    /// 股票图表视图模型
    /// </summary>
    public class StockChartViewModel
    {
        private ChartData _chartData;

        public StockChartViewModel(ChartData chartData)
        {
            _chartData = chartData;
            InitializeCharts();
        }

        public string StockName => _chartData.StockName;
        public string StockCode => _chartData.StockCode;

        // 图表系列
        public SeriesCollection KlineSeries { get; set; }
        public SeriesCollection VolumeSeries { get; set; }
        public SeriesCollection KDValueSeries { get; set; }
        public SeriesCollection KDBandSeries { get; set; }

        // 标签
        public string[] KlineLabels { get; set; }
        public string[] KDLabels { get; set; }

        /// <summary>
        /// 初始化所有图表
        /// </summary>
        private void InitializeCharts()
        {
            InitializeKlineChart();
            InitializeKDValueChart();
            InitializeKDBandChart();
        }

        /// <summary>
        /// 初始化K线图
        /// </summary>
        private void InitializeKlineChart()
        {
            var klineSeries = new SeriesCollection();
            var volumeSeries = new SeriesCollection();
            var labels = new List<string>();

            // 准备K线数据
            var candlePoints = new ChartValues<ObservablePoint>();
            var shadowHighPoints = new ChartValues<ObservablePoint>();
            var shadowLowPoints = new ChartValues<ObservablePoint>();
            var volumePoints = new ChartValues<ObservablePoint>();

            foreach (var candle in _chartData.DailyKline)
            {
                labels.Add(candle.Date.ToString("MM/dd"));

                // 实体（开盘-收盘）
                candlePoints.Add(new ObservablePoint(
                    _chartData.DailyKline.IndexOf(candle),
                    candle.Close));

                // 影线（最高-最低）
                shadowHighPoints.Add(new ObservablePoint(
                    _chartData.DailyKline.IndexOf(candle),
                    candle.High));
                shadowLowPoints.Add(new ObservablePoint(
                    _chartData.DailyKline.IndexOf(candle),
                    candle.Low));

                // 成交量
                volumePoints.Add(new ObservablePoint(
                    _chartData.DailyKline.IndexOf(candle),
                    candle.Volume));
            }

            // K线图实现（中国股市习惯：红涨绿跌）
            // 为每个K线创建单独的系列，确保符合中国习惯
            for (int i = 0; i < _chartData.DailyKline.Count; i++)
            {
                var candle = _chartData.DailyKline[i];

                // 中国习惯：红涨绿跌
                // IsRising = Close >= Open，所以红色表示上涨，绿色表示下跌
                var color = candle.IsRising ? Brushes.Red : Brushes.Green;

                // 计算实体上下边界
                var bodyTop = Math.Max(candle.Open, candle.Close);    // 实体顶部
                var bodyBottom = Math.Min(candle.Open, candle.Close);  // 实体底部

                // 1. 绘制上影线（最高价到实体顶部）
                if (candle.High > bodyTop)
                {
                    var upperShadowPoints = new ChartValues<ObservablePoint>
                    {
                        new ObservablePoint(i, candle.High),
                        new ObservablePoint(i, bodyTop)
                    };

                    var upperShadowSeries = new LineSeries
                    {
                        Values = upperShadowPoints,
                        Stroke = color,
                        StrokeThickness = 1,
                        PointGeometry = null
                    };

                    klineSeries.Add(upperShadowSeries);
                }

                // 2. 绘制实体（开盘到收盘的矩形）
                // 使用较粗的LineSeries来模拟实体矩形
                var bodyPoints = new ChartValues<ObservablePoint>
                {
                    new ObservablePoint(i, bodyTop),
                    new ObservablePoint(i, bodyBottom)
                };

                var bodySeries = new LineSeries
                {
                    Values = bodyPoints,
                    Stroke = color,
                    Fill = color, // 填充颜色
                    StrokeThickness = 8, // 实体宽度（可根据需要调整）
                    PointGeometry = null
                };

                klineSeries.Add(bodySeries);

                // 3. 绘制下影线（实体底部到最低价）
                if (candle.Low < bodyBottom)
                {
                    var lowerShadowPoints = new ChartValues<ObservablePoint>
                    {
                        new ObservablePoint(i, bodyBottom),
                        new ObservablePoint(i, candle.Low)
                    };

                    var lowerShadowSeries = new LineSeries
                    {
                        Values = lowerShadowPoints,
                        Stroke = color,
                        StrokeThickness = 1,
                        PointGeometry = null
                    };

                    klineSeries.Add(lowerShadowSeries);
                }
            }

            // 成交量
            var volumeColumnSeries = new ColumnSeries
            {
                Title = "成交量",
                Values = volumePoints,
                Fill = Brushes.LightBlue,
                Stroke = Brushes.Transparent
            };

            volumeSeries.Add(volumeColumnSeries);

            KlineSeries = klineSeries;
            VolumeSeries = volumeSeries;
            KlineLabels = labels.ToArray();
        }

        /// <summary>
        /// 初始化KD指标K值叠加图
        /// </summary>
        private void InitializeKDValueChart()
        {
            var series = new SeriesCollection();
            var labels = new List<string>();

            // 周K值（蓝色）
            if (_chartData.WeeklyKD.Count > 0)
            {
                var weeklyKPoints = new ChartValues<ObservablePoint>();
                foreach (var kd in _chartData.WeeklyKD)
                {
                    weeklyKPoints.Add(new ObservablePoint(
                        _chartData.WeeklyKD.IndexOf(kd),
                        kd.K));
                }
                series.Add(new LineSeries
                {
                    Title = "周K",
                    Values = weeklyKPoints,
                    Stroke = Brushes.Blue,
                    Fill = Brushes.Transparent,
                    PointGeometry = DefaultGeometries.Circle,
                    PointGeometrySize = 3
                });
            }

            // 月K值（绿色）
            if (_chartData.MonthlyKD.Count > 0)
            {
                var monthlyKPoints = new ChartValues<ObservablePoint>();
                foreach (var kd in _chartData.MonthlyKD)
                {
                    monthlyKPoints.Add(new ObservablePoint(
                        _chartData.MonthlyKD.IndexOf(kd),
                        kd.K));
                }
                series.Add(new LineSeries
                {
                    Title = "月K",
                    Values = monthlyKPoints,
                    Stroke = Brushes.Green,
                    Fill = Brushes.Transparent,
                    PointGeometry = DefaultGeometries.Circle,
                    PointGeometrySize = 3
                });
            }

            // 季K值（红色）
            if (_chartData.QuarterlyKD.Count > 0)
            {
                var quarterlyKPoints = new ChartValues<ObservablePoint>();
                foreach (var kd in _chartData.QuarterlyKD)
                {
                    quarterlyKPoints.Add(new ObservablePoint(
                        _chartData.QuarterlyKD.IndexOf(kd),
                        kd.K));
                }
                series.Add(new LineSeries
                {
                    Title = "季K",
                    Values = quarterlyKPoints,
                    Stroke = Brushes.Red,
                    Fill = Brushes.Transparent,
                    PointGeometry = DefaultGeometries.Circle,
                    PointGeometrySize = 3
                });
            }

            // 标签（使用最长的序列）
            var maxCount = Math.Max(
                Math.Max(_chartData.WeeklyKD.Count, _chartData.MonthlyKD.Count),
                _chartData.QuarterlyKD.Count);

            for (int i = 0; i < maxCount; i++)
            {
                labels.Add($"P{i}");
            }

            KDValueSeries = series;
            KDLabels = labels.ToArray();
        }

        /// <summary>
        /// 初始化KD指标带状图叠加图
        /// </summary>
        private void InitializeKDBandChart()
        {
            var series = new SeriesCollection();

            // 周KD带状图（红色=K>D，蓝色=K<D）
            if (_chartData.WeeklyKD.Count > 0)
            {
                CreateBandSeries(_chartData.WeeklyKD, "周KD",
                    new SolidColorBrush(Color.FromArgb(128, 255, 0, 0)), // 半透明红色
                    new SolidColorBrush(Color.FromArgb(128, 0, 0, 255)), // 半透明蓝色
                    series);
            }

            // 月KD带状图
            if (_chartData.MonthlyKD.Count > 0)
            {
                CreateBandSeries(_chartData.MonthlyKD, "月KD",
                    new SolidColorBrush(Color.FromArgb(100, 255, 0, 0)), // 更透明以便叠加
                    new SolidColorBrush(Color.FromArgb(100, 0, 0, 255)),
                    series);
            }

            // 季KD带状图
            if (_chartData.QuarterlyKD.Count > 0)
            {
                CreateBandSeries(_chartData.QuarterlyKD, "季KD",
                    new SolidColorBrush(Color.FromArgb(80, 255, 0, 0)), // 最透明
                    new SolidColorBrush(Color.FromArgb(80, 0, 0, 255)),
                    series);
            }

            KDBandSeries = series;
        }

        /// <summary>
        /// 创建带状图系列（K和D之间的填充区域）
        /// 使用多个LineSeries来实现不同颜色的填充
        /// </summary>
        private void CreateBandSeries(
            List<KDDataPoint> kdData,
            string title,
            Brush risingColor,
            Brush fallingColor,
            SeriesCollection targetCollection)
        {
            if (kdData == null || kdData.Count == 0)
                return;

            // 将数据分段，根据K>D或K<D创建不同颜色的区域
            var segments = new List<BandSegment>();
            BandSegment currentSegment = null;

            for (int i = 0; i < kdData.Count; i++)
            {
                var kd = kdData[i];
                bool isRising = kd.IsGoldenCross; // K > D

                if (currentSegment == null || currentSegment.IsRising != isRising)
                {
                    // 开始新段
                    if (currentSegment != null)
                    {
                        segments.Add(currentSegment);
                    }
                    currentSegment = new BandSegment
                    {
                        IsRising = isRising,
                        StartIndex = i,
                        Points = new ChartValues<ObservablePoint>()
                    };
                }

                // 添加点：K值和D值
                currentSegment.Points.Add(new ObservablePoint(i, kd.K));
                currentSegment.Points.Add(new ObservablePoint(i, kd.D));
            }

            if (currentSegment != null)
            {
                segments.Add(currentSegment);
            }

            // 为每个段创建填充区域
            // 在LiveCharts 0.9.7中，使用LineSeries并设置Fill属性来创建填充区域
            // 注意：LiveCharts 0.9.7的Fill属性会填充到X轴（Y=0），而不是填充两条线之间的区域
            // 这里我们使用K和D中较大值作为填充区域的顶部，填充到X轴
            foreach (var segment in segments)
            {
                // 创建填充区域（使用K和D中的较大值作为填充区域的顶部）
                var fillPoints = new ChartValues<ObservablePoint>();

                for (int i = segment.StartIndex; i < segment.StartIndex + segment.Points.Count / 2; i++)
                {
                    if (i < kdData.Count)
                    {
                        var kd = kdData[i];
                        // 使用K和D中的较大值作为填充区域的顶部
                        fillPoints.Add(new ObservablePoint(i, Math.Max(kd.K, kd.D)));
                    }
                }

                // 使用LineSeries创建填充区域
                // Fill属性会填充到X轴（Y=0）
                var fillSeries = new LineSeries
                {
                    Title = segment.IsRising ? $"{title}(K>D)" : $"{title}(K<D)",
                    Values = fillPoints,
                    Fill = segment.IsRising ? risingColor : fallingColor,
                    Stroke = Brushes.Transparent,
                    PointGeometry = null, // 不显示点
                    LineSmoothness = 0 // 不使用平滑曲线
                };

                targetCollection.Add(fillSeries);
            }

            // 添加K线和D线（用于显示具体数值）
            var kPoints = new ChartValues<ObservablePoint>();
            var dPoints = new ChartValues<ObservablePoint>();

            for (int i = 0; i < kdData.Count; i++)
            {
                var kd = kdData[i];
                kPoints.Add(new ObservablePoint(i, kd.K));
                dPoints.Add(new ObservablePoint(i, kd.D));
            }

            targetCollection.Add(new LineSeries
            {
                Title = $"{title}-K",
                Values = kPoints,
                Stroke = Brushes.Black,
                Fill = Brushes.Transparent,
                PointGeometry = DefaultGeometries.Circle,
                PointGeometrySize = 2
            });

            targetCollection.Add(new LineSeries
            {
                Title = $"{title}-D",
                Values = dPoints,
                Stroke = Brushes.Gray,
                Fill = Brushes.Transparent,
                PointGeometry = DefaultGeometries.Circle,
                PointGeometrySize = 2
            });
        }

        /// <summary>
        /// 带状图段（用于分段绘制不同颜色）
        /// </summary>
        private class BandSegment
        {
            public bool IsRising { get; set; }
            public int StartIndex { get; set; }
            public ChartValues<ObservablePoint> Points { get; set; }
        }

        /// <summary>
        /// 清理图表资源
        /// </summary>
        public void Cleanup()
        {
            // 清理K线图系列
            if (KlineSeries != null)
            {
                foreach (var series in KlineSeries)
                {
                    series.Values?.Clear();
                }
                KlineSeries.Clear();
                KlineSeries = null;
            }

            // 清理成交量系列
            if (VolumeSeries != null)
            {
                foreach (var series in VolumeSeries)
                {
                    series.Values?.Clear();
                }
                VolumeSeries.Clear();
                VolumeSeries = null;
            }

            // 清理KD值系列
            if (KDValueSeries != null)
            {
                foreach (var series in KDValueSeries)
                {
                    series.Values?.Clear();
                }
                KDValueSeries.Clear();
                KDValueSeries = null;
            }

            // 清理KD带状图系列
            if (KDBandSeries != null)
            {
                foreach (var series in KDBandSeries)
                {
                    series.Values?.Clear();
                }
                KDBandSeries.Clear();
                KDBandSeries = null;
            }

            // 清理标签
            KlineLabels = null;
            KDLabels = null;

            // 清理数据引用
            _chartData = null;
        }
    }
}
