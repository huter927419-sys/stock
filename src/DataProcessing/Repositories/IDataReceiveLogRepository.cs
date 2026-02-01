using System;
using System.Collections.Generic;

namespace MQReceiver.Repositories
{
    /// <summary>
    /// 数据接收日志仓储接口
    /// </summary>
    public interface IDataReceiveLogRepository
    {
        /// <summary>
        /// 添加日志记录
        /// </summary>
        int AddLog(DataReceiveLog log);

        /// <summary>
        /// 获取最近的日志
        /// </summary>
        List<DataReceiveLog> GetRecentLogs(int limit = 100);

        /// <summary>
        /// 获取指定时间范围的日志
        /// </summary>
        List<DataReceiveLog> GetLogsByTimeRange(DateTime startTime, DateTime endTime);

        /// <summary>
        /// 获取失败的日志
        /// </summary>
        List<DataReceiveLog> GetFailedLogs(int limit = 100);

        /// <summary>
        /// 删除旧日志
        /// </summary>
        int DeleteOldLogs(DateTime beforeDate);
    }

    /// <summary>
    /// 数据接收日志
    /// </summary>
    public class DataReceiveLog
    {
        public long Id { get; set; }
        public DateTime ReceiveTime { get; set; }
        public string DataType { get; set; }
        public int RecordCount { get; set; }
        public string QueueName { get; set; }
        public string SourceIp { get; set; }
        public string Status { get; set; }
        public string ErrorMessage { get; set; }
        public int ProcessingTimeMs { get; set; }
    }
}
