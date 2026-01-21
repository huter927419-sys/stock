using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using MQReceiver.Models;
using MQReceiver.ViewModels;

namespace MQReceiver.Views
{
    /// <summary>
    /// FilterResultWindow.xaml 的交互逻辑
    /// </summary>
    public partial class FilterResultWindow : Window
    {
        private List<FilterResultWithHistory> _results;

        public FilterResultWindow(List<FilterResultWithHistory> results, string conditionName)
        {
            InitializeComponent();

            _results = results ?? new List<FilterResultWithHistory>();

            // 设置数据上下文
            this.DataContext = new FilterResultViewModel(_results, conditionName);
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
        /// Hyperlink点击事件（备用方案）
        /// </summary>
        private void StockName_Click(object sender, RoutedEventArgs e)
        {
            var textBlock = sender as System.Windows.Controls.TextBlock;
            if (textBlock != null && textBlock.Tag != null)
            {
                string stockCode = textBlock.Tag.ToString();
                OpenStockChart(stockCode);
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
                var chartWindow = new WebChartWindow(stockCode);
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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
