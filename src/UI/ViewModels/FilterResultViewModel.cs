using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;
using MQReceiver.Models;

namespace MQReceiver.ViewModels
{
    /// <summary>
    /// 计算结果视图模型
    /// </summary>
    public class FilterResultViewModel : INotifyPropertyChanged
    {
        private string _conditionName;
        private ObservableCollection<StockResultItem> _results;

        public FilterResultViewModel(List<FilterResultWithHistory> results, string conditionName)
        {
            _conditionName = conditionName;
            _results = new ObservableCollection<StockResultItem>();

            // 转换为显示项
            foreach (var result in results)
            {
                _results.Add(new StockResultItem
                {
                    StockCode = result.StockCode,
                    StockName = result.StockName ?? result.StockCode,
                    WeeklyK = result.WeeklyK,
                    MonthlyK = result.MonthlyK,
                    QuarterlyK = result.QuarterlyK,
                    WeeklyKColor = GetKValueColor(result.WeeklyK, result.YesterdayWeeklyK),
                    MonthlyKColor = GetKValueColor(result.MonthlyK, result.YesterdayMonthlyK),
                    QuarterlyKColor = GetKValueColor(result.QuarterlyK, result.YesterdayQuarterlyK)
                });
            }
        }

        public string ConditionName
        {
            get { return _conditionName; }
            set
            {
                _conditionName = value;
                OnPropertyChanged(nameof(ConditionName));
            }
        }

        public int ResultCount => _results.Count;

        public ObservableCollection<StockResultItem> Results
        {
            get { return _results; }
            set
            {
                _results = value;
                OnPropertyChanged(nameof(Results));
                OnPropertyChanged(nameof(ResultCount));
            }
        }

        /// <summary>
        /// 获取K值颜色（红涨绿跌）
        /// </summary>
        private Brush GetKValueColor(decimal currentK, decimal? yesterdayK)
        {
            if (!yesterdayK.HasValue)
            {
                return Brushes.Gray; // 无历史数据，显示灰色
            }

            if (currentK > yesterdayK.Value)
            {
                return Brushes.Red; // K值上升，显示红色
            }
            else if (currentK < yesterdayK.Value)
            {
                return Brushes.Green; // K值下降，显示绿色
            }
            else
            {
                return Brushes.Gray; // K值不变，显示灰色
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
