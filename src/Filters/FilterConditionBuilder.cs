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
        /// 从配置文件读取过滤条件1：周K、月K、季K 金叉过滤
        /// </summary>
        public static GoldenCrossCondition BuildGoldenCrossCondition()
        {
            return new GoldenCrossCondition
            {
                RequireWeeklyGoldenCross = _configProvider.GetBool("Filter1_RequireWeeklyGoldenCross", true),
                RequireMonthlyGoldenCross = _configProvider.GetBool("Filter1_RequireMonthlyGoldenCross", true),
                RequireQuarterlyGoldenCross = _configProvider.GetBool("Filter1_RequireQuarterlyGoldenCross", true),
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
        /// 从配置文件读取过滤条件2：周K>月K>季K（不含金叉）
        /// </summary>
        public static KValueOrderCondition BuildKValueOrderCondition()
        {
            return new KValueOrderCondition
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
        /// 从配置文件读取过滤条件3：月K>季K or 周K 小于 月K
        /// </summary>
        public static KValueRelationCondition BuildKValueRelationCondition()
        {
            return new KValueRelationCondition
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
    }
}
