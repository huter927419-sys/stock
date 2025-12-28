using System;
using System.Windows;

namespace MQReceiver.Views
{
    /// <summary>
    /// 过滤条件设置对话框
    /// </summary>
    public partial class FilterSettingsDialog : Window
    {
        /// <summary>
        /// 周K最小值
        /// </summary>
        public decimal WeeklyKMin { get; private set; }

        /// <summary>
        /// 月K最小值
        /// </summary>
        public decimal MonthlyKMin { get; private set; }

        /// <summary>
        /// 季K最小值
        /// </summary>
        public decimal QuarterlyKMin { get; private set; }

        public FilterSettingsDialog(decimal weeklyK, decimal monthlyK, decimal quarterlyK)
        {
            InitializeComponent();

            WeeklyKMin = weeklyK;
            MonthlyKMin = monthlyK;
            QuarterlyKMin = quarterlyK;

            WeeklyKTextBox.Text = weeklyK.ToString();
            MonthlyKTextBox.Text = monthlyK.ToString();
            QuarterlyKTextBox.Text = quarterlyK.ToString();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // 验证并解析输入
            if (!decimal.TryParse(WeeklyKTextBox.Text, out decimal weeklyK) || weeklyK < 0 || weeklyK > 100)
            {
                MessageBox.Show("周K最小值必须是0-100之间的数字", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                WeeklyKTextBox.Focus();
                return;
            }

            if (!decimal.TryParse(MonthlyKTextBox.Text, out decimal monthlyK) || monthlyK < 0 || monthlyK > 100)
            {
                MessageBox.Show("月K最小值必须是0-100之间的数字", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                MonthlyKTextBox.Focus();
                return;
            }

            if (!decimal.TryParse(QuarterlyKTextBox.Text, out decimal quarterlyK) || quarterlyK < 0 || quarterlyK > 100)
            {
                MessageBox.Show("季K最小值必须是0-100之间的数字", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                QuarterlyKTextBox.Focus();
                return;
            }

            WeeklyKMin = weeklyK;
            MonthlyKMin = monthlyK;
            QuarterlyKMin = quarterlyK;

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
