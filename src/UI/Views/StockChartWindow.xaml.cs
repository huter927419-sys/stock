using System;
using System.Windows;
using MQReceiver.Models;
using MQReceiver.Services;
using MQReceiver.ViewModels;

namespace MQReceiver.Views
{
    /// <summary>
    /// StockChartWindow.xaml 的交互逻辑
    /// </summary>
    public partial class StockChartWindow : Window
    {
        private ChartData chartData;
        private StockChartViewModel _viewModel;

        public StockChartWindow(string stockCode)
        {
            InitializeComponent();
            this.Closed += StockChartWindow_Closed;
            LoadChartData(stockCode);
        }

        /// <summary>
        /// 窗口关闭时清理资源
        /// </summary>
        private void StockChartWindow_Closed(object sender, EventArgs e)
        {
            // 清理图表资源
            if (_viewModel != null)
            {
                _viewModel.Cleanup();
                _viewModel = null;
            }

            // 清理数据引用
            chartData = null;
            this.DataContext = null;

            // 取消事件订阅
            this.Closed -= StockChartWindow_Closed;
        }

        /// <summary>
        /// 加载图表数据
        /// </summary>
        private void LoadChartData(string stockCode)
        {
            try
            {
                var chartService = new ChartService();
                chartData = chartService.LoadChartData(stockCode, 0); // 0表示加载所有历史数据

                if (chartData == null || chartData.DailyKline.Count == 0)
                {
                    MessageBox.Show($"无法加载股票 {stockCode} 的图表数据", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    this.Close();
                    return;
                }

                // 设置窗口标题
                this.Title = $"股票图表 - {chartData.StockName} ({chartData.StockCode})";
                _viewModel = new StockChartViewModel(chartData);
                this.DataContext = _viewModel;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载图表数据失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
