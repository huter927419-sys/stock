using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MQReceiver.Services;

namespace MQReceiver.Views
{
    /// <summary>
    /// HaiLiDrvPanel.xaml 的交互逻辑
    /// 单个数据面板，显示指定的股票列表
    /// </summary>
    public partial class HaiLiDrvPanel : System.Windows.Controls.UserControl
    {
        private ObservableCollection<Services.HaiLiDataItem> _dataItems;
        private string _panelName;
        private List<string> _stockCodes; // 该面板显示的股票代码列表
        private HashSet<string> _stockCodeSet; // 缓存的股票代码集合（用于快速查找）
        private bool _stockCodeSetDirty = true; // 标记是否需要重建集合

        public HaiLiDrvPanel()
        {
            InitializeComponent();
            _dataItems = new ObservableCollection<Services.HaiLiDataItem>();
            DataListGrid.ItemsSource = _dataItems;
            _stockCodes = new List<string>();
            
            // 双击打开K线图
            DataListGrid.MouseDoubleClick += DataListGrid_MouseDoubleClick;
        }

        /// <summary>
        /// 面板名称
        /// </summary>
        public string PanelName
        {
            get { return _panelName; }
            set
            {
                _panelName = value;
                PanelTitleText.Text = value ?? "未命名面板";
            }
        }

        /// <summary>
        /// 设置该面板显示的股票代码列表
        /// </summary>
        public void SetStockCodes(List<string> stockCodes)
        {
            _stockCodes = stockCodes ?? new List<string>();
            _stockCodeSetDirty = true; // 标记需要重建集合
        }

        /// <summary>
        /// 获取该面板的股票代码列表
        /// </summary>
        public List<string> GetStockCodes()
        {
            return new List<string>(_stockCodes);
        }

        /// <summary>
        /// 规范化股票代码：移除市场前缀（SH/SZ），只保留6位数字
        /// </summary>
        private string NormalizeStockCode(string stockCode)
        {
            if (string.IsNullOrEmpty(stockCode))
                return stockCode;
            
            // 移除市场前缀（SH、SZ等）
            string normalized = stockCode.Trim().ToUpper();
            if (normalized.StartsWith("SH") || normalized.StartsWith("SZ"))
            {
                normalized = normalized.Substring(2);
            }
            
            // 如果已经是6位数字，直接返回
            if (normalized.Length == 6 && Regex.IsMatch(normalized, @"^\d{6}$"))
            {
                return normalized;
            }
            
            // 否则返回原值（可能是其他格式）
            return stockCode;
        }

        /// <summary>
        /// 更新面板数据
        /// </summary>
        public void UpdateData(List<Services.HaiLiDataItem> allData)
        {
            try
            {
                Console.WriteLine($"[HaiLiDrvPanel] {_panelName}: UpdateData 被调用，传入 {allData?.Count ?? 0} 条数据");
                if (allData != null && allData.Count > 0)
                {
                    Console.WriteLine($"[HaiLiDrvPanel] {_panelName}: 数据示例（前3条）:");
                    for (int i = 0; i < Math.Min(3, allData.Count); i++)
                    {
                        var item = allData[i];
                        Console.WriteLine($"  [{i}] 代码={item.StockCode}, 名称={item.StockName}, 规范化后={NormalizeStockCode(item.StockCode)}");
                    }
                }
                
                _dataItems.Clear();
                
                if (_stockCodes == null || _stockCodes.Count == 0)
                {
                    // 如果没有配置股票代码，显示全部数据
                    foreach (var item in allData)
                    {
                        _dataItems.Add(item);
                    }
                    Console.WriteLine($"[HaiLiDrvPanel] {_panelName}: 未配置股票代码，显示全部 {_dataItems.Count} 条数据");
                }
                else
                {
                    // 只显示配置的股票代码
                    // 使用缓存的HashSet，避免每次更新都创建新集合
                    if (_stockCodeSetDirty || _stockCodeSet == null)
                    {
                        // 规范化配置的股票代码（移除市场前缀）
                        _stockCodeSet = new HashSet<string>(
                            _stockCodes.Select(code => NormalizeStockCode(code)),
                            StringComparer.OrdinalIgnoreCase
                        );
                        _stockCodeSetDirty = false;
                        Console.WriteLine($"[HaiLiDrvPanel] {_panelName}: 配置了 {_stockCodes.Count} 个股票代码: {string.Join(", ", _stockCodes.Take(10))}{(_stockCodes.Count > 10 ? "..." : "")}");
                        Console.WriteLine($"[HaiLiDrvPanel] {_panelName}: 规范化后的代码集合包含 {_stockCodeSet.Count} 个唯一代码");
                    }
                    
                    int matchedCount = 0;
                    int sampleCount = 0;
                    var unmatchedConfigCodes = new HashSet<string>(_stockCodeSet); // 用于跟踪未匹配的配置代码
                    
                    foreach (var item in allData)
                    {
                        // 规范化数据中的股票代码，然后匹配
                        string normalizedDataCode = NormalizeStockCode(item.StockCode);
                        if (_stockCodeSet.Contains(normalizedDataCode))
                        {
                            _dataItems.Add(item);
                            matchedCount++;
                            unmatchedConfigCodes.Remove(normalizedDataCode); // 从未匹配集合中移除
                            // 记录前3个匹配的代码作为示例
                            if (sampleCount < 3)
                            {
                                Console.WriteLine($"[HaiLiDrvPanel] {_panelName}: 匹配示例 {sampleCount + 1}: 数据代码={item.StockCode}, 规范化后={normalizedDataCode}, 股票名称={item.StockName}");
                                sampleCount++;
                            }
                        }
                    }
                    Console.WriteLine($"[HaiLiDrvPanel] {_panelName}: 从 {allData.Count} 条数据中匹配到 {matchedCount} 条（配置了 {_stockCodes.Count} 个代码）");
                    
                    // 如果没有匹配到，输出详细的调试信息
                    if (matchedCount == 0 && _stockCodes.Count > 0)
                    {
                        Console.WriteLine($"[HaiLiDrvPanel] {_panelName}: ⚠️ 警告 - 没有匹配到任何数据！");
                        Console.WriteLine($"[HaiLiDrvPanel] {_panelName}: 配置的代码（前10个）: {string.Join(", ", _stockCodes.Take(10))}");
                        Console.WriteLine($"[HaiLiDrvPanel] {_panelName}: 规范化后的代码集合（前10个）: {string.Join(", ", _stockCodeSet.Take(10))}");
                        if (allData.Count > 0)
                        {
                            Console.WriteLine($"[HaiLiDrvPanel] {_panelName}: 数据中的代码示例（前10个）: {string.Join(", ", allData.Take(10).Select(d => $"{d.StockCode}→{NormalizeStockCode(d.StockCode)}"))}");
                            
                            // 检查是否有部分匹配（代码前缀匹配）
                            var dataCodesSet = new HashSet<string>(allData.Select(d => NormalizeStockCode(d.StockCode)));
                            var partialMatches = _stockCodeSet.Where(c => dataCodesSet.Contains(c)).Take(5).ToList();
                            if (partialMatches.Any())
                            {
                                Console.WriteLine($"[HaiLiDrvPanel] {_panelName}: 发现部分匹配的代码: {string.Join(", ", partialMatches)}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"[HaiLiDrvPanel] {_panelName}: 传入的数据列表为空！");
                        }
                    }
                    else if (matchedCount > 0 && unmatchedConfigCodes.Count > 0)
                    {
                        // 有匹配但还有未匹配的配置代码
                        Console.WriteLine($"[HaiLiDrvPanel] {_panelName}: 匹配了 {matchedCount} 条，但还有 {unmatchedConfigCodes.Count} 个配置的代码未找到数据");
                        Console.WriteLine($"[HaiLiDrvPanel] {_panelName}: 未匹配的配置代码（前10个）: {string.Join(", ", unmatchedConfigCodes.Take(10))}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HaiLiDrvPanel] {_panelName} 更新数据失败: {ex.Message}");
                Console.WriteLine($"[HaiLiDrvPanel] 异常堆栈: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 双击行打开K线图（保留兼容性）
        /// </summary>
        private void DataListGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var selectedItem = DataListGrid.SelectedItem as Services.HaiLiDataItem;
            if (selectedItem != null)
            {
                // 触发事件，让主窗口处理
                OnStockDoubleClick(selectedItem.StockCode);
            }
        }

        /// <summary>
        /// 点击股票名称打开K线图
        /// </summary>
        private void StockName_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBlock textBlock)
            {
                string stockCode = textBlock.Tag as string;
                if (!string.IsNullOrEmpty(stockCode))
                {
                    // 触发事件，让主窗口处理
                    OnStockDoubleClick(stockCode);
                    e.Handled = true; // 阻止事件继续传播
                }
            }
        }

        /// <summary>
        /// 股票双击事件
        /// </summary>
        public event EventHandler<string> StockDoubleClick;

        private void OnStockDoubleClick(string stockCode)
        {
            StockDoubleClick?.Invoke(this, stockCode);
        }

        /// <summary>
        /// 应用主题（与 Mairui 一致：深色/浅色）
        /// </summary>
        public void ApplyTheme(bool isDark)
        {
            if (isDark)
            {
                Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26));
                if (PanelTitleText != null) PanelTitleText.Foreground = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4));
                if (DataListGrid != null)
                {
                    DataListGrid.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
                    DataListGrid.Foreground = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4));
                }
            }
            else
            {
                Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
                if (PanelTitleText != null) PanelTitleText.Foreground = Brushes.Black;
                if (DataListGrid != null)
                {
                    DataListGrid.Background = Brushes.White;
                    DataListGrid.Foreground = Brushes.Black;
                }
            }
        }
    }
}
