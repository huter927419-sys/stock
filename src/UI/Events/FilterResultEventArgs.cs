using System;
using System.Collections.Generic;
using MQReceiver.Models;

namespace MQReceiver.Events
{
    /// <summary>
    /// 过滤结果事件参数
    /// 用于解耦 FilterService 与 UI 层
    /// </summary>
    public class FilterResultEventArgs : EventArgs
    {
        /// <summary>
        /// 表格一结果：月K > 季K AND 周K上穿月K
        /// </summary>
        public List<FilterResultWithHistory> Table1Results { get; set; }

        /// <summary>
        /// 表格二结果：月K > 季K AND 周K > 月K（不含上穿当天）
        /// </summary>
        public List<FilterResultWithHistory> Table2Results { get; set; }

        /// <summary>
        /// 表格三结果：月K less than 季K AND 周K上穿季K
        /// </summary>
        public List<FilterResultWithHistory> Table3Results { get; set; }

        /// <summary>
        /// 表格四结果：月K less than 季K AND 周K > 季K（不含上穿当天）
        /// </summary>
        public List<FilterResultWithHistory> Table4Results { get; set; }

        /// <summary>
        /// 表格五结果：月K > 季K AND 周K less than 月K
        /// </summary>
        public List<FilterResultWithHistory> Table5Results { get; set; }

        /// <summary>
        /// 表格六结果：月K less than 季K AND 周K less than 季K
        /// </summary>
        public List<FilterResultWithHistory> Table6Results { get; set; }

        /// <summary>
        /// 过滤执行时间
        /// </summary>
        public DateTime FilterTime { get; set; }

        /// <summary>
        /// 过滤耗时（秒）
        /// </summary>
        public double ElapsedSeconds { get; set; }

        /// <summary>
        /// 处理的股票数量
        /// </summary>
        public int ProcessedCount { get; set; }

        public FilterResultEventArgs()
        {
            Table1Results = new List<FilterResultWithHistory>();
            Table2Results = new List<FilterResultWithHistory>();
            Table3Results = new List<FilterResultWithHistory>();
            Table4Results = new List<FilterResultWithHistory>();
            Table5Results = new List<FilterResultWithHistory>();
            Table6Results = new List<FilterResultWithHistory>();
            FilterTime = DateTime.Now;
        }
    }
}
