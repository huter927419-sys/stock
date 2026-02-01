using System;
using System.Collections.Generic;

namespace MQReceiver.Repositories
{
    /// <summary>
    /// 复权计算任务仓储接口
    /// </summary>
    public interface IAdjustmentTaskRepository
    {
        /// <summary>
        /// 添加复权计算任务
        /// </summary>
        int AddTask(AdjustmentTask task);

        /// <summary>
        /// 批量添加任务
        /// </summary>
        int AddTasks(List<AdjustmentTask> tasks);

        /// <summary>
        /// 获取待处理的任务（按优先级排序）
        /// </summary>
        List<AdjustmentTask> GetPendingTasks(int limit = 100);

        /// <summary>
        /// 更新任务状态
        /// </summary>
        bool UpdateTaskStatus(long taskId, string status, string errorMessage = null);

        /// <summary>
        /// 获取指定股票的任务
        /// </summary>
        List<AdjustmentTask> GetTasksByStockCode(string stockCode);

        /// <summary>
        /// 删除已完成的任务
        /// </summary>
        int DeleteCompletedTasks(DateTime beforeDate);
    }

    /// <summary>
    /// 复权计算任务
    /// </summary>
    public class AdjustmentTask
    {
        public long Id { get; set; }
        public string StockCode { get; set; }
        public string TaskType { get; set; }
        public DateTime TriggerDate { get; set; }
        public string Status { get; set; }
        public int Priority { get; set; }
        public string ErrorMessage { get; set; }
        public int RetryCount { get; set; }
        public int MaxRetries { get; set; }
        public DateTime CreateTime { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? CompleteTime { get; set; }
    }
}
