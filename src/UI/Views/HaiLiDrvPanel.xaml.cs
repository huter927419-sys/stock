using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
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
        /// 更新面板数据
        /// </summary>
        public void UpdateData(List<Services.HaiLiDataItem> allData)
        {
            try
            {
                _dataItems.Clear();
                
                if (_stockCodes == null || _stockCodes.Count == 0)
                {
                    // 如果没有配置股票代码，显示全部数据
                    foreach (var item in allData)
                    {
                        _dataItems.Add(item);
                    }
                }
                else
                {
                    // 只显示配置的股票代码
                    // 使用缓存的HashSet，避免每次更新都创建新集合
                    if (_stockCodeSetDirty || _stockCodeSet == null)
                    {
                        _stockCodeSet = new HashSet<string>(_stockCodes, StringComparer.OrdinalIgnoreCase);
                        _stockCodeSetDirty = false;
                    }
                    
                    foreach (var item in allData)
                    {
                        if (_stockCodeSet.Contains(item.StockCode))
                        {
                            _dataItems.Add(item);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HaiLiDrvPanel] 更新数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 双击行打开K线图
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
        /// 股票双击事件
        /// </summary>
        public event EventHandler<string> StockDoubleClick;

        private void OnStockDoubleClick(string stockCode)
        {
            StockDoubleClick?.Invoke(this, stockCode);
        }
    }
}
