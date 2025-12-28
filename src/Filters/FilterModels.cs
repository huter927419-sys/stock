using System;
using System.Collections.Generic;

namespace MQReceiver.Filters
{
    /// <summary>
    /// 过滤条件配置基类
    /// </summary>
    public abstract class FilterConditionBase
    {
        // 默认最小值（显示在表头，列表中的值必须大于此值）
        public decimal WeeklyKDefaultMin { get; set; } = 0;
        public decimal MonthlyKDefaultMin { get; set; } = 0;
        public decimal QuarterlyKDefaultMin { get; set; } = 0;

        // K值范围条件
        public decimal? WeeklyKMin { get; set; }
        public decimal? WeeklyKMax { get; set; }
        public decimal? MonthlyKMin { get; set; }
        public decimal? MonthlyKMax { get; set; }
        public decimal? QuarterlyKMin { get; set; }
        public decimal? QuarterlyKMax { get; set; }

        // 实时数据条件
        public decimal? PriceMin { get; set; }
        public decimal? VolumeMin { get; set; }
    }

    /// <summary>
    /// 过滤条件1的配置：金叉过滤
    /// </summary>
    public class GoldenCrossCondition : FilterConditionBase
    {
        // 周KD金叉条件（K > D）
        public bool RequireWeeklyGoldenCross { get; set; } = true;

        // 月KD金叉条件（K > D）
        public bool RequireMonthlyGoldenCross { get; set; } = true;

        // 季KD金叉条件（K > D）
        public bool RequireQuarterlyGoldenCross { get; set; } = true;
    }

    /// <summary>
    /// 过滤条件2的配置：周K>月K>季K
    /// </summary>
    public class KValueOrderCondition : FilterConditionBase
    {
        // 无额外属性，使用基类的通用属性
    }

    /// <summary>
    /// 过滤条件3的配置：月K>季K or 周K 小于 月K
    /// </summary>
    public class KValueRelationCondition : FilterConditionBase
    {
        // 无额外属性，使用基类的通用属性
    }

    /// <summary>
    /// 简化的过滤结果（只包含名称和K值）
    /// </summary>
    public class SimpleFilterResult
    {
        public string StockCode { get; set; }
        public string StockName { get; set; }
        public decimal WeeklyK { get; set; }
        public decimal MonthlyK { get; set; }
        public decimal QuarterlyK { get; set; }
    }

    /// <summary>
    /// 数据完整性检查结果
    /// </summary>
    public class DataIntegrityCheckResult
    {
        public bool IsValid { get; set; }
        public string StockCode { get; set; }
        public List<string> Issues { get; set; }

        public DataIntegrityCheckResult()
        {
            Issues = new List<string>();
        }
    }

    /// <summary>
    /// 完整的过滤结果（包含所有KD信息）
    /// </summary>
    public class FilterResult
    {
        public string StockCode { get; set; }
        public string StockName { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal ChangePercent { get; set; }
        public decimal WeeklyK { get; set; }
        public decimal WeeklyD { get; set; }
        public decimal MonthlyK { get; set; }
        public decimal MonthlyD { get; set; }
        public decimal QuarterlyK { get; set; }
        public decimal QuarterlyD { get; set; }
        public string Reason { get; set; }
    }
}
