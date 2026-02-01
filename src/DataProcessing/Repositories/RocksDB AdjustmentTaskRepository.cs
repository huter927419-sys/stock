using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MQReceiver.Repositories;

namespace MQReceiver.DataProcessing.Repositories
{
    /// <summary>
    /// RocksDB 复权计算任务仓储实现
    /// </summary>
    public class RocksDBAdjustmentTaskRepository : IAdjustmentTaskRepository
    {
        private readonly string _dbPath;
        private readonly object _fileLock = new object();
        private long _nextId = 1;

        public RocksDBAdjustmentTaskRepository(string dbPath = "data/rocksdb")
        {
            _dbPath = dbPath;
            Initialize();
        }

        private bool Initialize()
        {
            try
            {
                var tasksDir = Path.Combine(_dbPath, "tasks");
                if (!Directory.Exists(tasksDir))
                {
                    Directory.CreateDirectory(tasksDir);
                }

                // 加载最大 ID
                var tasks = LoadAllTasks();
                if (tasks.Count > 0)
                {
                    _nextId = tasks.Max(t => t.Id) + 1;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] AdjustmentTask 初始化失败: {ex.Message}");
                return false;
            }
        }

        public int AddTask(AdjustmentTask task)
        {
            if (task == null)
                return 0;

            try
            {
                task.Id = _nextId++;
                task.CreateTime = DateTime.Now;

                var tasks = LoadAllTasks();
                tasks.Add(task);
                SaveAllTasks(tasks);

                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] AddTask 失败: {ex.Message}");
                return 0;
            }
        }

        public int AddTasks(List<AdjustmentTask> taskList)
        {
            if (taskList == null || taskList.Count == 0)
                return 0;

            try
            {
                var tasks = LoadAllTasks();

                foreach (var task in taskList)
                {
                    task.Id = _nextId++;
                    task.CreateTime = DateTime.Now;
                    tasks.Add(task);
                }

                SaveAllTasks(tasks);
                return taskList.Count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] AddTasks 失败: {ex.Message}");
                return 0;
            }
        }

        public List<AdjustmentTask> GetPendingTasks(int limit = 100)
        {
            try
            {
                var tasks = LoadAllTasks();
                return tasks
                    .Where(t => t.Status == "pending")
                    .OrderBy(t => t.Priority)
                    .ThenBy(t => t.CreateTime)
                    .Take(limit)
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] GetPendingTasks 失败: {ex.Message}");
                return new List<AdjustmentTask>();
            }
        }

        public bool UpdateTaskStatus(long taskId, string status, string errorMessage = null)
        {
            try
            {
                var tasks = LoadAllTasks();
                var task = tasks.FirstOrDefault(t => t.Id == taskId);

                if (task != null)
                {
                    task.Status = status;
                    task.ErrorMessage = errorMessage;

                    if (status == "processing")
                    {
                        task.StartTime = DateTime.Now;
                    }
                    else if (status == "completed" || status == "failed")
                    {
                        task.CompleteTime = DateTime.Now;
                    }

                    if (status == "failed")
                    {
                        task.RetryCount++;
                    }

                    SaveAllTasks(tasks);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] UpdateTaskStatus 失败: {ex.Message}");
                return false;
            }
        }

        public List<AdjustmentTask> GetTasksByStockCode(string stockCode)
        {
            try
            {
                var tasks = LoadAllTasks();
                return tasks
                    .Where(t => t.StockCode == stockCode)
                    .OrderByDescending(t => t.CreateTime)
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] GetTasksByStockCode 失败: {ex.Message}");
                return new List<AdjustmentTask>();
            }
        }

        public int DeleteCompletedTasks(DateTime beforeDate)
        {
            try
            {
                var tasks = LoadAllTasks();
                var toDelete = tasks.Where(t =>
                    t.Status == "completed" &&
                    t.CompleteTime.HasValue &&
                    t.CompleteTime.Value < beforeDate
                ).ToList();

                foreach (var task in toDelete)
                {
                    tasks.Remove(task);
                }

                if (toDelete.Count > 0)
                {
                    SaveAllTasks(tasks);
                }

                return toDelete.Count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] DeleteCompletedTasks 失败: {ex.Message}");
                return 0;
            }
        }

        public bool TestConnection()
        {
            return Initialize();
        }

        private List<AdjustmentTask> LoadAllTasks()
        {
            var fileName = Path.Combine(_dbPath, "tasks", "adjustment_tasks.json");
            if (!File.Exists(fileName))
            {
                return new List<AdjustmentTask>();
            }

            try
            {
                string jsonContent;
                lock (_fileLock)
                {
                    jsonContent = File.ReadAllText(fileName);
                }

                var tasks = JsonSerializer.Deserialize<List<AdjustmentTask>>(jsonContent);
                return tasks ?? new List<AdjustmentTask>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] LoadAllTasks 失败: {ex.Message}");
                return new List<AdjustmentTask>();
            }
        }

        private void SaveAllTasks(List<AdjustmentTask> tasks)
        {
            var fileName = Path.Combine(_dbPath, "tasks", "adjustment_tasks.json");

            try
            {
                string jsonContent = JsonSerializer.Serialize(tasks);
                lock (_fileLock)
                {
                    File.WriteAllText(fileName, jsonContent);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RocksDB] SaveAllTasks 失败: {ex.Message}");
                throw;
            }
        }
    }
}
