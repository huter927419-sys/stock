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
    /// 表格一条件：月K > 季K AND 周K上穿月K（周K金叉月K）
    /// </summary>
    public class Table1Condition : FilterConditionBase
    {
        // 无额外属性，使用基类的通用属性
    }

    /// <summary>
    /// 表格二条件：月K > 季K AND 周K > 月K（不含上穿当天）
    /// </summary>
    public class Table2Condition : FilterConditionBase
    {
        // 无额外属性，使用基类的通用属性
    }

    /// <summary>
    /// 表格三条件：月K < 季K AND 周K上穿季K（周K金叉季K）
    /// </summary>
    public class Table3Condition : FilterConditionBase
    {
        // 无额外属性，使用基类的通用属性
    }

    /// <summary>
    /// 表格四条件：月K < 季K AND 周K > 季K（不含上穿当天）
    /// </summary>
    public class Table4Condition : FilterConditionBase
    {
        // 无额外属性，使用基类的通用属性
    }

    /// <summary>
    /// 表格五条件：月K > 季K AND 周K < 月K
    /// </summary>
    public class Table5Condition : FilterConditionBase
    {
        // 无额外属性，使用基类的通用属性
    }

    /// <summary>
    /// 表格六条件：月K < 季K AND 周K < 季K
    /// </summary>
    public class Table6Condition : FilterConditionBase
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
