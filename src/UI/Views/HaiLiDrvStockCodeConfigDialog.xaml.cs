using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using MQReceiver.Configuration;

namespace MQReceiver.Views
{
    /// <summary>
    /// HaiLiDrvStockCodeConfigDialog.xaml 的交互逻辑
    /// 配置HaiLiDrv要显示的股票代码
    /// </summary>
    public partial class HaiLiDrvStockCodeConfigDialog : Window
    {
        private IConfigurationProvider _configProvider;

        public HaiLiDrvStockCodeConfigDialog(IConfigurationProvider configProvider)
        {
            InitializeComponent();
            _configProvider = configProvider;
            
            // 加载当前配置
            LoadConfiguration();
            
            // 监听输入变化，更新股票代码计数
            StockCodesTextBox.TextChanged += StockCodesTextBox_TextChanged;
        }

        /// <summary>
        /// 加载当前配置
        /// </summary>
        private void LoadConfiguration()
        {
            try
            {
                // 加载股票代码列表
                string stockCodesConfig = _configProvider.GetString("HaiLiDrv_StockCodes", "");
                StockCodesTextBox.Text = stockCodesConfig;
                
                // 加载是否启用过滤
                bool enableFilter = _configProvider.GetBool("HaiLiDrv_EnableStockCodeFilter", false);
                EnableFilterCheckBox.IsChecked = enableFilter;
                
                // 更新计数
                UpdateStockCount();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载配置失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 股票代码输入变化时更新计数
        /// </summary>
        private void StockCodesTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateStockCount();
        }

        /// <summary>
        /// 更新股票代码计数显示
        /// </summary>
        private void UpdateStockCount()
        {
            try
            {
                var codes = ParseStockCodes(StockCodesTextBox.Text);
                StockCountText.Text = $"当前配置：{codes.Count} 个股票代码";
            }
            catch
            {
                StockCountText.Text = "当前配置：解析错误";
            }
        }

        /// <summary>
        /// 解析股票代码文本（支持换行、逗号、分号分隔）
        /// </summary>
        private HashSet<string> ParseStockCodes(string text)
        {
            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(text))
                return codes;

            // 按换行、逗号、分号分割
            var parts = text.Split(new[] { '\n', '\r', ',', ';', ' ', '\t' }, 
                StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var part in parts)
            {
                string trimmed = part.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    // 验证股票代码格式（6位数字）
                    if (Regex.IsMatch(trimmed, @"^\d{6}$"))
                    {
                        codes.Add(trimmed);
                    }
                }
            }
            
            return codes;
        }

        /// <summary>
        /// 保存按钮点击
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 解析并验证股票代码
                var codes = ParseStockCodes(StockCodesTextBox.Text);
                
                // 保存股票代码（逗号分隔）
                string stockCodesValue = string.Join(",", codes);
                _configProvider.SetValue("HaiLiDrv_StockCodes", stockCodesValue);
                
                // 保存是否启用过滤
                bool enableFilter = EnableFilterCheckBox.IsChecked ?? false;
                _configProvider.SetValue("HaiLiDrv_EnableStockCodeFilter", enableFilter.ToString());
                
                Console.WriteLine($"[HaiLiDrvStockCodeConfigDialog] 已保存配置：{codes.Count} 个股票代码，过滤启用={enableFilter}");
                
                MessageBox.Show($"配置已保存！\n股票代码数量：{codes.Count}\n过滤启用：{(enableFilter ? "是" : "否")}", 
                    "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
                
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存配置失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 取消按钮点击
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
