using System;
using System.Collections.Generic;

namespace MQReceiver.Services
{
    /// <summary>
    /// 交易日历
    /// 用于判断交易日、节假日等
    /// </summary>
    public class TradingCalendar
    {
        // 节假日缓存（可以从配置或数据库加载）
        private readonly HashSet<DateTime> _holidays;

        // 补班日缓存（周末但需要交易的日子）
        private readonly HashSet<DateTime> _workingWeekends;

        public TradingCalendar()
        {
            _holidays = new HashSet<DateTime>();
            _workingWeekends = new HashSet<DateTime>();

            // 初始化2024-2025年的主要节假日
            InitializeHolidays();
        }

        /// <summary>
        /// 初始化节假日（可以后续从配置文件或数据库加载）
        /// </summary>
        private void InitializeHolidays()
        {
            // 2024年节假日
            // 元旦
            _holidays.Add(new DateTime(2024, 1, 1));

            // 春节 (2024年2月10日-17日)
            for (int i = 10; i <= 17; i++)
                _holidays.Add(new DateTime(2024, 2, i));

            // 清明节
            _holidays.Add(new DateTime(2024, 4, 4));
            _holidays.Add(new DateTime(2024, 4, 5));
            _holidays.Add(new DateTime(2024, 4, 6));

            // 劳动节
            for (int i = 1; i <= 5; i++)
                _holidays.Add(new DateTime(2024, 5, i));

            // 端午节
            _holidays.Add(new DateTime(2024, 6, 8));
            _holidays.Add(new DateTime(2024, 6, 9));
            _holidays.Add(new DateTime(2024, 6, 10));

            // 中秋节
            _holidays.Add(new DateTime(2024, 9, 15));
            _holidays.Add(new DateTime(2024, 9, 16));
            _holidays.Add(new DateTime(2024, 9, 17));

            // 国庆节
            for (int i = 1; i <= 7; i++)
                _holidays.Add(new DateTime(2024, 10, i));

            // 2025年节假日
            // 元旦
            _holidays.Add(new DateTime(2025, 1, 1));

            // 春节 (2025年1月28日-2月4日)
            for (int i = 28; i <= 31; i++)
                _holidays.Add(new DateTime(2025, 1, i));
            for (int i = 1; i <= 4; i++)
                _holidays.Add(new DateTime(2025, 2, i));

            // 清明节 (2025年4月4日-6日)
            for (int i = 4; i <= 6; i++)
                _holidays.Add(new DateTime(2025, 4, i));

            // 劳动节 (2025年5月1日-5日)
            for (int i = 1; i <= 5; i++)
                _holidays.Add(new DateTime(2025, 5, i));

            // 端午节 (2025年5月31日-6月2日)
            _holidays.Add(new DateTime(2025, 5, 31));
            _holidays.Add(new DateTime(2025, 6, 1));
            _holidays.Add(new DateTime(2025, 6, 2));

            // 中秋节+国庆节 (2025年10月1日-8日)
            for (int i = 1; i <= 8; i++)
                _holidays.Add(new DateTime(2025, 10, i));
        }

        /// <summary>
        /// 判断指定日期是否是交易日
        /// </summary>
        public bool IsTradingDay(DateTime date)
        {
            date = date.Date;

            // 如果是节假日，不是交易日
            if (_holidays.Contains(date))
                return false;

            // 如果是补班日（周末但交易），是交易日
            if (_workingWeekends.Contains(date))
                return true;

            // 周末不是交易日
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                return false;

            // 其他情况是交易日
            return true;
        }

        /// <summary>
        /// 获取上一个交易日
        /// </summary>
        public DateTime GetPreviousTradingDay(DateTime date)
        {
            date = date.Date.AddDays(-1);

            // 最多回溯30天（防止无限循环）
            for (int i = 0; i < 30; i++)
            {
                if (IsTradingDay(date))
                    return date;
                date = date.AddDays(-1);
            }

            // 如果30天内没找到交易日，返回原日期减1
            return date;
        }

        /// <summary>
        /// 获取下一个交易日
        /// </summary>
        public DateTime GetNextTradingDay(DateTime date)
        {
            date = date.Date.AddDays(1);

            // 最多前溯30天（防止无限循环）
            for (int i = 0; i < 30; i++)
            {
                if (IsTradingDay(date))
                    return date;
                date = date.AddDays(1);
            }

            return date;
        }

        /// <summary>
        /// 获取指定日期范围内的所有交易日
        /// </summary>
        public List<DateTime> GetTradingDays(DateTime startDate, DateTime endDate)
        {
            var tradingDays = new List<DateTime>();
            DateTime current = startDate.Date;

            while (current <= endDate.Date)
            {
                if (IsTradingDay(current))
                    tradingDays.Add(current);
                current = current.AddDays(1);
            }

            return tradingDays;
        }

        /// <summary>
        /// 获取距离指定日期最近的交易日（包含当天）
        /// </summary>
        public DateTime GetNearestTradingDay(DateTime date)
        {
            if (IsTradingDay(date.Date))
                return date.Date;
            return GetPreviousTradingDay(date);
        }

        /// <summary>
        /// 添加节假日
        /// </summary>
        public void AddHoliday(DateTime date)
        {
            _holidays.Add(date.Date);
        }

        /// <summary>
        /// 添加补班日（周末但交易）
        /// </summary>
        public void AddWorkingWeekend(DateTime date)
        {
            _workingWeekends.Add(date.Date);
        }

        /// <summary>
        /// 批量添加节假日
        /// </summary>
        public void AddHolidays(IEnumerable<DateTime> dates)
        {
            foreach (var date in dates)
                _holidays.Add(date.Date);
        }

        /// <summary>
        /// 清除所有节假日缓存
        /// </summary>
        public void ClearHolidays()
        {
            _holidays.Clear();
            _workingWeekends.Clear();
        }

        /// <summary>
        /// 从数据库加载节假日（预留接口）
        /// </summary>
        public void LoadHolidaysFromDatabase()
        {
            // TODO: 从数据库加载节假日配置
            // 可以创建一个 trading_calendar 表存储节假日信息
        }

        /// <summary>
        /// 判断当前是否在交易时段
        /// </summary>
        public bool IsMarketOpen()
        {
            DateTime now = DateTime.Now;

            if (!IsTradingDay(now.Date))
                return false;

            TimeSpan currentTime = now.TimeOfDay;

            // 上午交易时段 9:30 - 11:30
            bool morningSession = currentTime >= new TimeSpan(9, 30, 0) &&
                                  currentTime < new TimeSpan(11, 30, 0);

            // 下午交易时段 13:00 - 15:00
            bool afternoonSession = currentTime >= new TimeSpan(13, 0, 0) &&
                                    currentTime < new TimeSpan(15, 0, 0);

            return morningSession || afternoonSession;
        }

        /// <summary>
        /// 获取今天剩余交易时间（分钟）
        /// </summary>
        public int GetRemainingTradingMinutes()
        {
            if (!IsMarketOpen())
                return 0;

            DateTime now = DateTime.Now;
            TimeSpan currentTime = now.TimeOfDay;

            int remainingMinutes = 0;

            // 上午剩余时间
            if (currentTime < new TimeSpan(11, 30, 0))
            {
                TimeSpan morningEnd = new TimeSpan(11, 30, 0);
                remainingMinutes += (int)(morningEnd - currentTime).TotalMinutes;

                // 加上下午的时间
                remainingMinutes += 120; // 13:00-15:00 = 120分钟
            }
            else if (currentTime >= new TimeSpan(13, 0, 0) && currentTime < new TimeSpan(15, 0, 0))
            {
                // 下午剩余时间
                TimeSpan afternoonEnd = new TimeSpan(15, 0, 0);
                remainingMinutes = (int)(afternoonEnd - currentTime).TotalMinutes;
            }

            return Math.Max(0, remainingMinutes);
        }
    }
}
