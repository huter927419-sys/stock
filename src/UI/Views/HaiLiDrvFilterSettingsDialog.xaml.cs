using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using MQReceiver.UI.Configuration;

namespace MQReceiver.Views
{
    /// <summary>
    /// HaiLiDrv 全局筛选设置对话框（与 MairuiStockMonitor 一致）
    /// </summary>
    public partial class HaiLiDrvFilterSettingsDialog : Window
    {
        public HaiLiDrvFilterSettings FilterSettings { get; private set; }

        public HaiLiDrvFilterSettingsDialog(HaiLiDrvFilterSettings settings)
        {
            InitializeComponent();
            FilterSettings = settings?.Clone() ?? new HaiLiDrvFilterSettings();
            LoadSettings();
        }

        private void LoadSettings()
        {
            ChkEnableChangePercent.IsChecked = FilterSettings.EnableChangePercentFilter;
            TxtMinChangePercent.Text = FilterSettings.MinChangePercent.ToString("F2", CultureInfo.InvariantCulture);
            ChkEnableIntradayChange.IsChecked = FilterSettings.EnableIntradayChangeFilter;
            TxtMinIntradayChange.Text = FilterSettings.MinIntradayChangePercent.ToString("F2", CultureInfo.InvariantCulture);
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            FilterSettings = new HaiLiDrvFilterSettings();
            LoadSettings();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(TxtMinChangePercent.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var minChange))
                minChange = 3.0m;
            if (!decimal.TryParse(TxtMinIntradayChange.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var minIntraday))
                minIntraday = 5.0m;

            FilterSettings.EnableChangePercentFilter = ChkEnableChangePercent.IsChecked == true;
            FilterSettings.MinChangePercent = Math.Max(-100, Math.Min(1000, minChange));
            FilterSettings.EnableIntradayChangeFilter = ChkEnableIntradayChange.IsChecked == true;
            FilterSettings.MinIntradayChangePercent = Math.Max(-100, Math.Min(1000, minIntraday));

            DialogResult = true;
            Close();
        }
    }
}
