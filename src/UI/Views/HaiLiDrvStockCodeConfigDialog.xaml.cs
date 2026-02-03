using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using MQReceiver.Configuration;
using MQReceiver.Services;

namespace MQReceiver.Views
{
    /// <summary>
    /// 可选股票项（用于“从数据列表选择”）
    /// </summary>
    public class StockCodeDisplayItem
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string DisplayText => $"{Code}  {Name}";
    }

    /// <summary>
    /// HaiLiDrvStockCodeConfigDialog.xaml 的交互逻辑
    /// 配置 HaiLiDrv 要显示的股票代码，支持“添加方式”和“删除选中”
    /// </summary>
    public partial class HaiLiDrvStockCodeConfigDialog : Window
    {
        private IConfigurationProvider _configProvider;
        private ObservableCollection<string> _selectedCodes; // 当前已选代码列表
        private List<StockCodeDisplayItem> _availableStockItems; // 可选股票（从主窗口传入）

        public HaiLiDrvStockCodeConfigDialog(IConfigurationProvider configProvider, 
            IList<Services.HaiLiDataItem> availableStocks = null)
        {
            InitializeComponent();
            _configProvider = configProvider;
            _selectedCodes = new ObservableCollection<string>();
            SelectedCodesListBox.ItemsSource = _selectedCodes;

            if (availableStocks != null && availableStocks.Count > 0)
            {
                _availableStockItems = availableStocks
                    .Select(x => new StockCodeDisplayItem { Code = x.StockCode ?? "", Name = x.StockName ?? "" })
                    .ToList();
                AvailableStocksListBox.ItemsSource = _availableStockItems;
                AvailableCountText.Text = $"{_availableStockItems.Count} 只可选";
            }
            else
            {
                _availableStockItems = new List<StockCodeDisplayItem>();
                AvailableStocksListBox.ItemsSource = _availableStockItems;
                AvailableCountText.Text = "无数据（请先在主界面加载数据）";
            }

            LoadConfiguration();
            AddMethodComboBox_SelectionChanged(null, null);
            UpdateSelectedCount();
        }

        /// <summary>
        /// 加载当前配置到已选列表
        /// </summary>
        private void LoadConfiguration()
        {
            try
            {
                string stockCodesConfig = _configProvider.GetString("HaiLiDrv_StockCodes", "");
                var codes = ParseStockCodes(stockCodesConfig);
                _selectedCodes.Clear();
                foreach (var c in codes.OrderBy(x => x))
                    _selectedCodes.Add(c);

                EnableFilterCheckBox.IsChecked = _configProvider.GetBool("HaiLiDrv_EnableStockCodeFilter", false);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载配置失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 添加方式切换
        /// </summary>
        private void AddMethodComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool isManual = (AddMethodComboBox?.SelectedIndex != 1);
            if (ManualInputPanel != null)
                ManualInputPanel.Visibility = isManual ? Visibility.Visible : Visibility.Collapsed;
            if (FromListPanel != null)
                FromListPanel.Visibility = isManual ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <summary>
        /// 手动输入 - 添加
        /// </summary>
        private void AddManualButton_Click(object sender, RoutedEventArgs e)
        {
            var codes = ParseStockCodes(ManualCodeTextBox?.Text ?? "");
            foreach (var c in codes)
            {
                if (!_selectedCodes.Any(x => string.Equals(x, c, StringComparison.OrdinalIgnoreCase)))
                    _selectedCodes.Add(c);
            }
            RefreshSelectedListOrder();
            if (ManualCodeTextBox != null && !string.IsNullOrEmpty(ManualCodeTextBox.Text))
                ManualCodeTextBox.Text = "";
            UpdateSelectedCount();
        }

        private void RefreshSelectedListOrder()
        {
            var list = _selectedCodes.ToList();
            _selectedCodes.Clear();
            foreach (var c in list.OrderBy(x => x).Distinct(StringComparer.OrdinalIgnoreCase))
                _selectedCodes.Add(c);
        }

        /// <summary>
        /// 从数据列表选择 - 添加选中
        /// </summary>
        private void AddSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = AvailableStocksListBox.SelectedItems.Cast<StockCodeDisplayItem>().ToList();
            foreach (var item in selected)
            {
                if (string.IsNullOrEmpty(item?.Code)) continue;
                if (!_selectedCodes.Any(x => string.Equals(x, item.Code, StringComparison.OrdinalIgnoreCase)))
                    _selectedCodes.Add(item.Code);
            }
            RefreshSelectedListOrder();
            UpdateSelectedCount();
        }

        /// <summary>
        /// 删除选中
        /// </summary>
        private void DeleteSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var toRemove = SelectedCodesListBox.SelectedItems.Cast<string>().ToList();
            foreach (var s in toRemove)
                _selectedCodes.Remove(s);
            UpdateSelectedCount();
        }

        /// <summary>
        /// 清空
        /// </summary>
        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedCodes.Clear();
            UpdateSelectedCount();
        }

        private void UpdateSelectedCount()
        {
            SelectedCountText.Text = $"{_selectedCodes.Count} 个";
        }

        /// <summary>
        /// 解析股票代码文本（支持换行、逗号、分号分隔）
        /// </summary>
        private HashSet<string> ParseStockCodes(string text)
        {
            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(text))
                return codes;

            var parts = text.Split(new[] { '\n', '\r', ',', ';', ' ', '\t' },
                StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                string trimmed = part.Trim();
                if (!string.IsNullOrEmpty(trimmed) && Regex.IsMatch(trimmed, @"^\d{6}$"))
                    codes.Add(trimmed);
            }
            return codes;
        }

        /// <summary>
        /// 保存
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var codes = _selectedCodes.ToList();
                string stockCodesValue = string.Join(",", codes);
                _configProvider.SetValue("HaiLiDrv_StockCodes", stockCodesValue);

                bool enableFilter = EnableFilterCheckBox.IsChecked ?? false;
                _configProvider.SetValue("HaiLiDrv_EnableStockCodeFilter", enableFilter.ToString());

                Console.WriteLine($"[HaiLiDrvStockCodeConfigDialog] 已保存配置：{codes.Count} 个股票代码，过滤启用={enableFilter}");

                MessageBox.Show($"配置已保存！\n股票代码数量：{codes.Count}\n过滤启用：{(enableFilter ? "是" : "否")}",
                    "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存配置失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
