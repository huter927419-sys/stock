using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Wpf;
using LiveCharts.Defaults;
using MQReceiver.Models;

namespace MQReceiver.ViewModels
{
    /// <summary>
    /// 股票图表视图模型
    /// 按照专业股票软件的标准设计：
    /// - 主图：K线图（蜡烛图）+ 成交量
    /// - 副图：周KD、月KD、季KD分开显示，各含K线和D线
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

        // 主图系列（K线+成交量合并）
        public SeriesCollection CombinedSeries { get; set; }
        public SeriesCollection KlineSeries { get; set; }
        public SeriesCollection VolumeSeries { get; set; }

        // KD副图系列（分开显示）
        public SeriesCollection WeeklyKDSeries { get; set; }
        public SeriesCollection MonthlyKDSeries { get; set; }
        public SeriesCollection QuarterlyKDSeries { get; set; }

        // KD带状图叠加系列
        public SeriesCollection KDBandSeries { get; set; }

        // K线图价格范围（用于紧凑显示）
        public double KlinePriceMin { get; set; }
        public double KlinePriceMax { get; set; }

        // 成交量最大值（用于Y轴范围）
        public double VolumeMax { get; set; }

        // 标签
        public string[] KlineLabels { get; set; }
        public string[] KDLabels { get; set; }

        /// <summary>
        /// 初始化所有图表
        /// </summary>
        private void InitializeCharts()
        {
            InitializeKlineChart();
            InitializeKDCharts();
            InitializeKDBandChart();
        }

        /// <summary>
        /// 初始化K线图（蜡烛图）和成交量图（合并到同一个图表）
        /// </summary>
        private void InitializeKlineChart()
        {
            var combinedSeries = new SeriesCollection();
            var labels = new List<string>();

            if (_chartData.DailyKline == null || _chartData.DailyKline.Count == 0)
            {
                CombinedSeries = combinedSeries;
                KlineSeries = new SeriesCollection();
                VolumeSeries = new SeriesCollection();
                KlineLabels = labels.ToArray();
                return;
            }

            // 计算价格范围
            double minPrice = _chartData.DailyKline.Min(c => c.Low);
            double maxPrice = _chartData.DailyKline.Max(c => c.High);
            double priceRange = maxPrice - minPrice;

            // 扩展Y轴下方25%用于显示成交量，上方5%边距
            // 实际价格显示在整个Y轴的25%-95%区间
            double extendedMin = minPrice - priceRange * 0.35;  // 下方留35%给成交量
            double extendedMax = maxPrice + priceRange * 0.05;  // 上方留5%边距
            KlinePriceMin = Math.Floor(extendedMin * 100) / 100;
            KlinePriceMax = Math.Ceiling(extendedMax * 100) / 100;

            // 计算成交量缩放因子：将成交量映射到价格Y轴的底部25%区域
            double maxVolume = _chartData.DailyKline.Max(c => c.Volume);
            double volumeAreaHeight = priceRange * 0.30;  // 成交量区域高度（价格范围的30%）
            double volumeScaleFactor = volumeAreaHeight / maxVolume;
            double volumeBaseY = KlinePriceMin;  // 成交量从Y轴最底部开始
            VolumeMax = maxVolume;

            // 准备成交量数据
            var risingVolumeValues = new ChartValues<ObservablePoint>();
            var fallingVolumeValues = new ChartValues<ObservablePoint>();

            // K线图实现（中国股市习惯：红涨绿跌）
            for (int i = 0; i < _chartData.DailyKline.Count; i++)
            {
                var candle = _chartData.DailyKline[i];
                labels.Add(candle.Date.ToString("MM/dd"));

                // 中国习惯：红涨绿跌
                var color = candle.IsRising ? Brushes.Red : Brushes.Green;

                // 计算实体上下边界
                var bodyTop = Math.Max(candle.Open, candle.Close);
                var bodyBottom = Math.Min(candle.Open, candle.Close);

                // 绘制影线（从最高到最低）- 使用Y轴0（价格轴）
                var shadowPoints = new ChartValues<ObservablePoint>
                {
                    new ObservablePoint(i, candle.High),
                    new ObservablePoint(i, candle.Low)
                };

                combinedSeries.Add(new LineSeries
                {
                    Values = shadowPoints,
                    Stroke = color,
                    StrokeThickness = 1,
                    PointGeometry = null,
                    Fill = Brushes.Transparent,
                    ScalesYAt = 0
                });

                // 绘制实体（较粗的线）
                if (Math.Abs(bodyTop - bodyBottom) > 0.001)
                {
                    var bodyPoints = new ChartValues<ObservablePoint>
                    {
                        new ObservablePoint(i, bodyTop),
                        new ObservablePoint(i, bodyBottom)
                    };

                    combinedSeries.Add(new LineSeries
                    {
                        Values = bodyPoints,
                        Stroke = color,
                        Fill = color,
                        StrokeThickness = 6,
                        PointGeometry = null,
                        ScalesYAt = 0
                    });
                }

                // 成交量 - 转换为价格坐标系（缩放到底部区域）
                double scaledVolume = volumeBaseY + candle.Volume * volumeScaleFactor;
                if (candle.IsRising)
                {
                    risingVolumeValues.Add(new ObservablePoint(i, scaledVolume));
                }
                else
                {
                    fallingVolumeValues.Add(new ObservablePoint(i, scaledVolume));
                }
            }

            // 成交量柱状图 - 使用与K线相同的Y轴（Y轴0），已缩放到底部区域
            if (risingVolumeValues.Count > 0)
            {
                combinedSeries.Add(new ColumnSeries
                {
                    Title = "成交量(涨)",
                    Values = risingVolumeValues,
                    Fill = new SolidColorBrush(Color.FromArgb(150, 255, 0, 0)),
                    Stroke = Brushes.Transparent,
                    MaxColumnWidth = 5,
                    ColumnPadding = 0,
                    ScalesYAt = 0  // 与K线使用同一个Y轴
                });
            }

            if (fallingVolumeValues.Count > 0)
            {
                combinedSeries.Add(new ColumnSeries
                {
                    Title = "成交量(跌)",
                    Values = fallingVolumeValues,
                    Fill = new SolidColorBrush(Color.FromArgb(150, 0, 128, 0)),
                    Stroke = Brushes.Transparent,
                    MaxColumnWidth = 5,
                    ColumnPadding = 0,
                    ScalesYAt = 0  // 与K线使用同一个Y轴
                });
            }

            CombinedSeries = combinedSeries;
            KlineSeries = new SeriesCollection();
            VolumeSeries = new SeriesCollection();
            KlineLabels = labels.ToArray();
        }

        /// <summary>
        /// 初始化KD指标图（周、月、季分开）
        /// 按照专业软件标准：K线白色，D线黄色
        /// KD数据按日期对齐到日K线的X轴
        /// </summary>
        private void InitializeKDCharts()
        {
            // KD图使用与日K线相同的标签（日期）
            KDLabels = KlineLabels;

            // 构建日期到索引的映射
            var dateToIndex = new Dictionary<DateTime, int>();
            if (_chartData.DailyKline != null)
            {
                for (int i = 0; i < _chartData.DailyKline.Count; i++)
                {
                    var date = _chartData.DailyKline[i].Date.Date;
                    if (!dateToIndex.ContainsKey(date))
                    {
                        dateToIndex[date] = i;
                    }
                }
            }

            int totalPoints = _chartData.DailyKline?.Count ?? 0;

            // 初始化各周期KD图，按日期对齐
            WeeklyKDSeries = CreateAlignedKDSeries(_chartData.WeeklyKD, "周", dateToIndex, totalPoints);
            MonthlyKDSeries = CreateAlignedKDSeries(_chartData.MonthlyKD, "月", dateToIndex, totalPoints);
            QuarterlyKDSeries = CreateAlignedKDSeries(_chartData.QuarterlyKD, "季", dateToIndex, totalPoints);
        }

        /// <summary>
        /// 创建对齐的KD指标系列（按日期映射到日K线X轴）
        /// </summary>
        private SeriesCollection CreateAlignedKDSeries(List<KDDataPoint> kdData, string period,
            Dictionary<DateTime, int> dateToIndex, int totalPoints)
        {
            var series = new SeriesCollection();

            if (kdData == null || kdData.Count == 0 || totalPoints == 0)
                return series;

            // 按日期排序
            var sortedKD = kdData.OrderBy(kd => kd.Date).ToList();

            // 使用ObservablePoint来指定X坐标（日期对应的索引）
            var kPoints = new ChartValues<ObservablePoint>();
            var dPoints = new ChartValues<ObservablePoint>();

            foreach (var kd in sortedKD)
            {
                // 查找最接近的日期索引
                int index = FindClosestDateIndex(kd.Date, dateToIndex, totalPoints);
                if (index >= 0)
                {
                    kPoints.Add(new ObservablePoint(index, kd.K));
                    dPoints.Add(new ObservablePoint(index, kd.D));
                }
            }

            if (kPoints.Count == 0)
                return series;

            // K线：白色实线
            series.Add(new LineSeries
            {
                Title = $"{period}K",
                Values = kPoints,
                Stroke = Brushes.White,
                Fill = Brushes.Transparent,
                PointGeometry = null,
                StrokeThickness = 1.5,
                LineSmoothness = 0
            });

            // D线：黄色实线
            series.Add(new LineSeries
            {
                Title = $"{period}D",
                Values = dPoints,
                Stroke = Brushes.Yellow,
                Fill = Brushes.Transparent,
                PointGeometry = null,
                StrokeThickness = 1.5,
                LineSmoothness = 0
            });

            return series;
        }

        /// <summary>
        /// 初始化KD带状图叠加图
        /// 层级顺序：蓝色(季)在下，绿色(月)在中，红色(周)在上
        /// K>D使用深色，K<D使用浅色
        /// </summary>
        private void InitializeKDBandChart()
        {
            var bandSeries = new SeriesCollection();

            // 构建日期到索引的映射
            var dateToIndex = new Dictionary<DateTime, int>();
            if (_chartData.DailyKline != null)
            {
                for (int i = 0; i < _chartData.DailyKline.Count; i++)
                {
                    var date = _chartData.DailyKline[i].Date.Date;
                    if (!dateToIndex.ContainsKey(date))
                    {
                        dateToIndex[date] = i;
                    }
                }
            }

            int totalPoints = _chartData.DailyKline?.Count ?? 0;

            // 添加顺序：先添加的在底层
            // 1. 季KD（蓝色）- 底层
            if (_chartData.QuarterlyKD != null && _chartData.QuarterlyKD.Count > 0)
            {
                AddKDBandSeries(bandSeries, _chartData.QuarterlyKD, "季",
                    Color.FromRgb(0, 80, 180),      // 深蓝色 K>D
                    Color.FromRgb(135, 180, 230),   // 浅蓝色 K<D
                    dateToIndex, totalPoints);
            }

            // 2. 月KD（绿色）- 中层
            if (_chartData.MonthlyKD != null && _chartData.MonthlyKD.Count > 0)
            {
                AddKDBandSeries(bandSeries, _chartData.MonthlyKD, "月",
                    Color.FromRgb(0, 128, 0),       // 深绿色 K>D
                    Color.FromRgb(144, 238, 144),   // 浅绿色 K<D
                    dateToIndex, totalPoints);
            }

            // 3. 周KD（红色）- 顶层
            if (_chartData.WeeklyKD != null && _chartData.WeeklyKD.Count > 0)
            {
                AddKDBandSeries(bandSeries, _chartData.WeeklyKD, "周",
                    Color.FromRgb(200, 0, 0),       // 大红色 K>D
                    Color.FromRgb(255, 182, 193),   // 粉红色 K<D
                    dateToIndex, totalPoints);
            }

            KDBandSeries = bandSeries;
        }

        /// <summary>
        /// 添加KD带状填充系列
        /// 使用上下两条线实现K和D之间的带状填充效果
        /// </summary>
        private void AddKDBandSeries(SeriesCollection targetCollection, List<KDDataPoint> kdData,
            string period, Color colorKGreaterD, Color colorKLessD,
            Dictionary<DateTime, int> dateToIndex, int totalPoints)
        {
            if (kdData == null || kdData.Count == 0)
                return;

            var sortedKD = kdData.OrderBy(kd => kd.Date).ToList();

            // 准备数据点，按日期索引排列
            var alignedData = new List<(int Index, double K, double D)>();

            foreach (var kd in sortedKD)
            {
                int index = FindClosestDateIndex(kd.Date, dateToIndex, totalPoints);
                if (index >= 0)
                {
                    alignedData.Add((index, kd.K, kd.D));
                }
            }

            if (alignedData.Count < 2)
                return;

            // 按索引排序确保顺序正确
            alignedData = alignedData.OrderBy(d => d.Index).ToList();

            // 绘制上边界（max(K,D)）并填充颜色
            var upperLine = new ChartValues<ObservablePoint>();
            var lowerLine = new ChartValues<ObservablePoint>();

            foreach (var point in alignedData)
            {
                upperLine.Add(new ObservablePoint(point.Index, Math.Max(point.K, point.D)));
                lowerLine.Add(new ObservablePoint(point.Index, Math.Min(point.K, point.D)));
            }

            // 判断当前主要是K>D还是K<D，选择对应颜色
            int kGreaterCount = alignedData.Count(p => p.K >= p.D);
            var mainColor = kGreaterCount > alignedData.Count / 2 ? colorKGreaterD : colorKLessD;

            // 绘制上边界线并填充到Y=0
            targetCollection.Add(new LineSeries
            {
                Title = $"{period}上",
                Values = upperLine,
                Stroke = new SolidColorBrush(mainColor),
                Fill = new SolidColorBrush(Color.FromArgb(140, mainColor.R, mainColor.G, mainColor.B)),
                PointGeometry = null,
                StrokeThickness = 1.5,
                LineSmoothness = 0
            });

            // 绘制下边界线，用背景色填充到Y=0，覆盖掉下方不需要的部分
            targetCollection.Add(new LineSeries
            {
                Title = $"{period}下",
                Values = lowerLine,
                Stroke = new SolidColorBrush(Color.FromArgb(180, mainColor.R, mainColor.G, mainColor.B)),
                Fill = new SolidColorBrush(Color.FromRgb(26, 26, 26)), // 背景色 #1A1A1A
                PointGeometry = null,
                StrokeThickness = 1,
                LineSmoothness = 0
            });
        }

        /// <summary>
        /// 查找最接近的日期索引
        /// </summary>
        private int FindClosestDateIndex(DateTime targetDate, Dictionary<DateTime, int> dateToIndex, int totalPoints)
        {
            var date = targetDate.Date;

            // 精确匹配
            if (dateToIndex.TryGetValue(date, out int exactIndex))
            {
                return exactIndex;
            }

            // 查找最接近的日期（向前查找，找到该周期结束时的位置）
            int closestIndex = -1;
            int minDays = int.MaxValue;

            foreach (var kvp in dateToIndex)
            {
                int days = Math.Abs((kvp.Key - date).Days);
                if (days < minDays)
                {
                    minDays = days;
                    closestIndex = kvp.Value;
                }
                // 如果找到的日期在目标日期之后且在7天内，优先使用
                if (kvp.Key >= date && (kvp.Key - date).Days <= 7)
                {
                    return kvp.Value;
                }
            }

            return closestIndex;
        }

        /// <summary>
        /// 清理图表资源
        /// </summary>
        public void Cleanup()
        {
            ClearSeriesCollection(CombinedSeries);
            ClearSeriesCollection(KlineSeries);
            ClearSeriesCollection(VolumeSeries);
            ClearSeriesCollection(WeeklyKDSeries);
            ClearSeriesCollection(MonthlyKDSeries);
            ClearSeriesCollection(QuarterlyKDSeries);
            ClearSeriesCollection(KDBandSeries);

            CombinedSeries = null;
            KlineSeries = null;
            VolumeSeries = null;
            WeeklyKDSeries = null;
            MonthlyKDSeries = null;
            QuarterlyKDSeries = null;
            KDBandSeries = null;

            KlineLabels = null;
            KDLabels = null;
            _chartData = null;
        }

        private void ClearSeriesCollection(SeriesCollection collection)
        {
            if (collection == null) return;

            foreach (var series in collection)
            {
                series.Values?.Clear();
            }
            collection.Clear();
        }
    }
}
