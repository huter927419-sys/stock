using System;
using System.Configuration;
using MQReceiver.Configuration;

namespace MQReceiver.Filters
{
    /// <summary>
    /// 过滤条件构建器
    /// 负责从配置文件读取过滤条件
    /// </summary>
    public static class FilterConditionBuilder
    {
        private static IConfigurationProvider _configProvider = AppConfigProvider.Instance;

        /// <summary>
        /// 设置配置提供者（用于依赖注入和单元测试）
        /// </summary>
        public static void SetConfigurationProvider(IConfigurationProvider provider)
        {
            _configProvider = provider ?? AppConfigProvider.Instance;
        }

        /// <summary>
        /// 从配置文件读取表格一条件：月K > 季K AND 周K上穿月K
        /// </summary>
        public static Table1Condition BuildTable1Condition()
        {
            return new Table1Condition
            {
                WeeklyKDefaultMin = _configProvider.GetDecimal("Filter1_WeeklyKDefaultMin", 0),
                MonthlyKDefaultMin = _configProvider.GetDecimal("Filter1_MonthlyKDefaultMin", 0),
                QuarterlyKDefaultMin = _configProvider.GetDecimal("Filter1_QuarterlyKDefaultMin", 0),
                WeeklyKMin = _configProvider.GetNullableDecimal("Filter1_WeeklyKMin"),
                WeeklyKMax = _configProvider.GetNullableDecimal("Filter1_WeeklyKMax"),
                MonthlyKMin = _configProvider.GetNullableDecimal("Filter1_MonthlyKMin"),
                MonthlyKMax = _configProvider.GetNullableDecimal("Filter1_MonthlyKMax"),
                QuarterlyKMin = _configProvider.GetNullableDecimal("Filter1_QuarterlyKMin"),
                QuarterlyKMax = _configProvider.GetNullableDecimal("Filter1_QuarterlyKMax"),
                PriceMin = _configProvider.GetNullableDecimal("Filter1_PriceMin"),
                VolumeMin = _configProvider.GetNullableDecimal("Filter1_VolumeMin")
            };
        }

        /// <summary>
        /// 从配置文件读取表格二条件：月K > 季K AND 周K > 月K（不含上穿当天）
        /// </summary>
        public static Table2Condition BuildTable2Condition()
        {
            return new Table2Condition
            {
                WeeklyKDefaultMin = _configProvider.GetDecimal("Filter2_WeeklyKDefaultMin", 0),
                MonthlyKDefaultMin = _configProvider.GetDecimal("Filter2_MonthlyKDefaultMin", 0),
                QuarterlyKDefaultMin = _configProvider.GetDecimal("Filter2_QuarterlyKDefaultMin", 0),
                WeeklyKMin = _configProvider.GetNullableDecimal("Filter2_WeeklyKMin"),
                WeeklyKMax = _configProvider.GetNullableDecimal("Filter2_WeeklyKMax"),
                MonthlyKMin = _configProvider.GetNullableDecimal("Filter2_MonthlyKMin"),
                MonthlyKMax = _configProvider.GetNullableDecimal("Filter2_MonthlyKMax"),
                QuarterlyKMin = _configProvider.GetNullableDecimal("Filter2_QuarterlyKMin"),
                QuarterlyKMax = _configProvider.GetNullableDecimal("Filter2_QuarterlyKMax"),
                PriceMin = _configProvider.GetNullableDecimal("Filter2_PriceMin"),
                VolumeMin = _configProvider.GetNullableDecimal("Filter2_VolumeMin")
            };
        }

        /// <summary>
        /// 从配置文件读取表格三条件：月K 小于 季K AND 周K上穿季K
        /// </summary>
        public static Table3Condition BuildTable3Condition()
        {
            return new Table3Condition
            {
                WeeklyKDefaultMin = _configProvider.GetDecimal("Filter3_WeeklyKDefaultMin", 0),
                MonthlyKDefaultMin = _configProvider.GetDecimal("Filter3_MonthlyKDefaultMin", 0),
                QuarterlyKDefaultMin = _configProvider.GetDecimal("Filter3_QuarterlyKDefaultMin", 0),
                WeeklyKMin = _configProvider.GetNullableDecimal("Filter3_WeeklyKMin"),
                WeeklyKMax = _configProvider.GetNullableDecimal("Filter3_WeeklyKMax"),
                MonthlyKMin = _configProvider.GetNullableDecimal("Filter3_MonthlyKMin"),
                MonthlyKMax = _configProvider.GetNullableDecimal("Filter3_MonthlyKMax"),
                QuarterlyKMin = _configProvider.GetNullableDecimal("Filter3_QuarterlyKMin"),
                QuarterlyKMax = _configProvider.GetNullableDecimal("Filter3_QuarterlyKMax"),
                PriceMin = _configProvider.GetNullableDecimal("Filter3_PriceMin"),
                VolumeMin = _configProvider.GetNullableDecimal("Filter3_VolumeMin")
            };
        }

        /// <summary>
        /// 从配置文件读取表格四条件：月K 小于 季K AND 周K > 季K（不含上穿当天）
        /// </summary>
        public static Table4Condition BuildTable4Condition()
        {
            return new Table4Condition
            {
                WeeklyKDefaultMin = _configProvider.GetDecimal("Filter4_WeeklyKDefaultMin", 0),
                MonthlyKDefaultMin = _configProvider.GetDecimal("Filter4_MonthlyKDefaultMin", 0),
                QuarterlyKDefaultMin = _configProvider.GetDecimal("Filter4_QuarterlyKDefaultMin", 0),
                WeeklyKMin = _configProvider.GetNullableDecimal("Filter4_WeeklyKMin"),
                WeeklyKMax = _configProvider.GetNullableDecimal("Filter4_WeeklyKMax"),
                MonthlyKMin = _configProvider.GetNullableDecimal("Filter4_MonthlyKMin"),
                MonthlyKMax = _configProvider.GetNullableDecimal("Filter4_MonthlyKMax"),
                QuarterlyKMin = _configProvider.GetNullableDecimal("Filter4_QuarterlyKMin"),
                QuarterlyKMax = _configProvider.GetNullableDecimal("Filter4_QuarterlyKMax"),
                PriceMin = _configProvider.GetNullableDecimal("Filter4_PriceMin"),
                VolumeMin = _configProvider.GetNullableDecimal("Filter4_VolumeMin")
            };
        }

        /// <summary>
        /// 从配置文件读取表格五条件：月K > 季K AND 周K 小于 月K
        /// </summary>
        public static Table5Condition BuildTable5Condition()
        {
            return new Table5Condition
            {
                WeeklyKDefaultMin = _configProvider.GetDecimal("Filter5_WeeklyKDefaultMin", 0),
                MonthlyKDefaultMin = _configProvider.GetDecimal("Filter5_MonthlyKDefaultMin", 0),
                QuarterlyKDefaultMin = _configProvider.GetDecimal("Filter5_QuarterlyKDefaultMin", 0),
                WeeklyKMin = _configProvider.GetNullableDecimal("Filter5_WeeklyKMin"),
                WeeklyKMax = _configProvider.GetNullableDecimal("Filter5_WeeklyKMax"),
                MonthlyKMin = _configProvider.GetNullableDecimal("Filter5_MonthlyKMin"),
                MonthlyKMax = _configProvider.GetNullableDecimal("Filter5_MonthlyKMax"),
                QuarterlyKMin = _configProvider.GetNullableDecimal("Filter5_QuarterlyKMin"),
                QuarterlyKMax = _configProvider.GetNullableDecimal("Filter5_QuarterlyKMax"),
                PriceMin = _configProvider.GetNullableDecimal("Filter5_PriceMin"),
                VolumeMin = _configProvider.GetNullableDecimal("Filter5_VolumeMin")
            };
        }

        /// <summary>
        /// 从配置文件读取表格六条件：月K 小于 季K AND 周K 小于 季K
        /// </summary>
        public static Table6Condition BuildTable6Condition()
        {
            return new Table6Condition
            {
                WeeklyKDefaultMin = _configProvider.GetDecimal("Filter6_WeeklyKDefaultMin", 0),
                MonthlyKDefaultMin = _configProvider.GetDecimal("Filter6_MonthlyKDefaultMin", 0),
                QuarterlyKDefaultMin = _configProvider.GetDecimal("Filter6_QuarterlyKDefaultMin", 0),
                WeeklyKMin = _configProvider.GetNullableDecimal("Filter6_WeeklyKMin"),
                WeeklyKMax = _configProvider.GetNullableDecimal("Filter6_WeeklyKMax"),
                MonthlyKMin = _configProvider.GetNullableDecimal("Filter6_MonthlyKMin"),
                MonthlyKMax = _configProvider.GetNullableDecimal("Filter6_MonthlyKMax"),
                QuarterlyKMin = _configProvider.GetNullableDecimal("Filter6_QuarterlyKMin"),
                QuarterlyKMax = _configProvider.GetNullableDecimal("Filter6_QuarterlyKMax"),
                PriceMin = _configProvider.GetNullableDecimal("Filter6_PriceMin"),
                VolumeMin = _configProvider.GetNullableDecimal("Filter6_VolumeMin")
            };
        }

    }
}
