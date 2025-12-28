using System;
using System.Collections.Generic;

namespace MQReceiver.Filters
{
    /// <summary>
    /// 股票过滤器接口
    /// </summary>
    public interface IStockFilter<TCondition> where TCondition : FilterConditionBase
    {
        /// <summary>
        /// 执行过滤（串行版本）
        /// </summary>
        List<SimpleFilterResult> Filter(TCondition condition, DateTime targetDate);

        /// <summary>
        /// 执行过滤（并行版本）
        /// </summary>
        List<SimpleFilterResult> FilterParallel(TCondition condition, DateTime targetDate);
    }
}
