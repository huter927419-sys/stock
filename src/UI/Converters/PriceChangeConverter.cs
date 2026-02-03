using System;
using System.Globalization;
using System.Windows.Data;

namespace MQReceiver.Converters
{
    /// <summary>
    /// 涨跌幅转换器：用于判断涨跌幅是否大于0（涨）或小于0（跌）
    /// </summary>
    public class GreaterThanZeroConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double priceChange)
            {
                return priceChange > 0;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 涨跌幅转换器：用于判断涨跌幅是否小于0（跌）
    /// </summary>
    public class LessThanZeroConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double priceChange)
            {
                return priceChange < 0;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
