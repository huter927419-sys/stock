using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MQReceiver.Cache;
using MQReceiver.Configuration;
using MQReceiver.Helpers;
using MQReceiver.Models;
using MQReceiver.Repositories;

namespace MQReceiver.Services
{
    /// <summary>
    /// MQ数据同步服务
    /// 负责接收虚拟机数据并保存到数据库
    /// 线程安全：使用volatile确保多线程间的可见性
    /// </summary>
    public class MQService : IDisposable
    {
        private TcpListener tcpListener;
        private volatile bool _isRunning = false;
        private int port;
        private string queueName;

        private DailyDataDBWriter dbWriter;
        private RealTimeDataDBWriter realTimeDbWriter;
        private ExRightsDataDBWriter exRightsDbWriter;
        private RealTimeDataCache realTimeCache;
        private bool _disposed = false;

        private static readonly IConfigurationProvider _configProvider = AppConfigProvider.Instance;

        /// <summary>
        /// 启动MQ服务
        /// </summary>
        public bool Start()
        {
            try
            {
                Console.WriteLine("========================================");
                Console.WriteLine("  启动MQ数据同步服务");
                Console.WriteLine("========================================");
                Console.WriteLine();

                // 读取配置
                LoadConfig();

                // 初始化Redis连接（可选）
                try
                {
                    RedisHelper.Initialize();
                    Console.WriteLine("Redis连接初始化成功");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"警告: Redis初始化失败: {ex.Message}，将使用数据库直接查询");
                }

                // 初始化数据库写入器
                Console.WriteLine("正在初始化数据库连接...");
                dbWriter = new DailyDataDBWriter();
                realTimeDbWriter = new RealTimeDataDBWriter();
                exRightsDbWriter = new ExRightsDataDBWriter();

                if (!dbWriter.TestConnection() || !realTimeDbWriter.TestConnection() || !exRightsDbWriter.TestConnection())
                {
                    Console.WriteLine("错误: PostgreSQL数据库连接失败！");
                    Console.WriteLine("请检查数据库配置和连接。");
                    return false;
                }
                Console.WriteLine("PostgreSQL数据库连接成功");
                Console.WriteLine(DatabaseConnectionHelper.GetPoolInfo());

                // 自动创建数据库表（如果不存在）
                if (!DatabaseInitializer.Initialize())
                {
                    Console.WriteLine("警告: 数据库表初始化失败，但服务将继续运行");
                }
                Console.WriteLine();

                // 初始化实时数据缓存
                try
                {
                    realTimeCache = new RealTimeDataCache();
                    Console.WriteLine("实时数据缓存初始化成功");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("警告: 初始化实时数据缓存失败: " + ex.Message);
                }

                // 启动TCP监听
                tcpListener = new TcpListener(IPAddress.Any, port);
                tcpListener.Start();
                _isRunning = true;
                Console.WriteLine("MQ接收器已启动，监听端口: {0}", port);
                Console.WriteLine("队列名称: {0}", queueName);
                Console.WriteLine("等待虚拟机连接...");
                Console.WriteLine();
                Console.WriteLine("提示: 按 'Q' 键退出服务");
                Console.WriteLine();

                // 启动监听线程
                Thread listenThread = new Thread(ListenForClients)
                {
                    IsBackground = false,
                    Name = "MQListenerThread"
                };
                listenThread.Start();

                // 等待用户输入退出
                while (_isRunning)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Q || key.Key == ConsoleKey.Escape)
                    {
                        Console.WriteLine();
                        Console.WriteLine("正在停止MQ服务...");
                        Stop();
                        break;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("错误: 启动MQ服务失败: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 停止MQ服务
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
            try
            {
                // 获取本地引用，避免在操作过程中被其他线程修改
                var listener = tcpListener;
                if (listener != null)
                {
                    listener.Stop();
                    Console.WriteLine("TCP监听已停止");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("停止TCP监听时出错: " + ex.Message);
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 停止服务
                    Stop();

                    // 释放实时数据缓存
                    if (realTimeCache != null)
                    {
                        realTimeCache.Dispose();
                        realTimeCache = null;
                        Console.WriteLine("实时数据缓存已释放");
                    }

                    // 释放数据库写入器（如果实现了IDisposable）
                    dbWriter = null;
                    realTimeDbWriter = null;
                    exRightsDbWriter = null;

                    // 关闭Redis连接
                    RedisHelper.Close();
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// 监听客户端连接
        /// </summary>
        private void ListenForClients()
        {
            while (_isRunning)
            {
                try
                {
                    // 获取本地引用
                    var listener = tcpListener;
                    if (listener == null)
                        break;

                    TcpClient client = listener.AcceptTcpClient();
                    Console.WriteLine("客户端已连接: {0}", client.Client.RemoteEndPoint);

                    // 使用线程池处理客户端（优化线程使用）
                    ThreadPool.QueueUserWorkItem(state =>
                    {
                        try
                        {
                            HandleClient((TcpClient)state);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("处理客户端时出错: " + ex.Message);
                        }
                    }, client);
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                    {
                        Console.WriteLine("接受客户端连接时出错: " + ex.Message);
                    }
                }
            }
        }

        /// <summary>
        /// 处理客户端连接
        /// </summary>
        private void HandleClient(TcpClient client)
        {
            NetworkStream stream = null;
            try
            {
                stream = client.GetStream();
                stream.ReadTimeout = 30000; // 30秒超时

                while (client.Connected && _isRunning)
                {
                    // 读取消息长度（4字节）
                    byte[] lengthBytes = new byte[4];
                    int bytesRead = ReadBytes(stream, lengthBytes, 4);
                    if (bytesRead != 4)
                    {
                        break; // 连接已关闭
                    }

                    int messageLength = BitConverter.ToInt32(lengthBytes, 0);
                    if (messageLength <= 0 || messageLength > 10 * 1024 * 1024) // 最大10MB
                    {
                        Console.WriteLine("错误: 无效的消息长度: {0}", messageLength);
                        break;
                    }

                    // 读取队列名称长度（4字节）
                    byte[] queueNameLengthBytes = new byte[4];
                    bytesRead = ReadBytes(stream, queueNameLengthBytes, 4);
                    if (bytesRead != 4)
                    {
                        break;
                    }

                    int queueNameLength = BitConverter.ToInt32(queueNameLengthBytes, 0);
                    if (queueNameLength < 0 || queueNameLength > 256)
                    {
                        Console.WriteLine("错误: 无效的队列名称长度: {0}", queueNameLength);
                        break;
                    }

                    // 读取队列名称
                    byte[] queueNameBytes = new byte[queueNameLength];
                    bytesRead = ReadBytes(stream, queueNameBytes, queueNameLength);
                    if (bytesRead != queueNameLength)
                    {
                        break;
                    }

                    string receivedQueueName = Encoding.UTF8.GetString(queueNameBytes);

                    // 读取JSON数据
                    int dataLength = messageLength - 4 - 4 - queueNameLength;
                    byte[] dataBytes = new byte[dataLength];
                    bytesRead = ReadBytes(stream, dataBytes, dataLength);
                    if (bytesRead != dataLength)
                    {
                        break;
                    }

                    string jsonData = Encoding.UTF8.GetString(dataBytes);

                    // 根据队列名称处理不同的数据
                    Console.WriteLine("收到数据: 队列={0}, 大小={1} 字节", receivedQueueName, dataLength);

                    if (receivedQueueName == "daily_data_queue")
                    {
                        ProcessDailyData(jsonData);
                    }
                    else if (receivedQueueName == "realtime_data_queue")
                    {
                        ProcessRealTimeData(jsonData);
                    }
                    else if (receivedQueueName == "ex_rights_data_queue")
                    {
                        ProcessExRightsData(jsonData);
                    }
                    else if (receivedQueueName == "market_table_queue")
                    {
                        ProcessMarketTableData(jsonData);
                    }
                    else
                    {
                        Console.WriteLine("未知的队列名称: {0}", receivedQueueName);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("处理客户端时出错: " + ex.Message);
            }
            finally
            {
                if (stream != null)
                {
                    stream.Close();
                }
                client.Close();
                Console.WriteLine("客户端已断开连接");
            }
        }

        /// <summary>
        /// 读取指定数量的字节
        /// </summary>
        private int ReadBytes(NetworkStream stream, byte[] buffer, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int bytesRead = stream.Read(buffer, totalRead, count - totalRead);
                if (bytesRead == 0)
                {
                    return totalRead; // 连接已关闭
                }
                totalRead += bytesRead;
            }
            return totalRead;
        }

        /// <summary>
        /// 处理日线数据
        /// </summary>
        private void ProcessDailyData(string jsonData)
        {
            try
            {
                List<DailyDataRecord> records = StockDataParser.ParseDailyData(jsonData);

                if (records.Count > 0)
                {
                    int savedCount = dbWriter.SaveDailyData(records);
                    Console.WriteLine("成功保存 {0} 条日线数据到PostgreSQL", savedCount);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("处理日线数据失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 处理实时数据
        /// </summary>
        private void ProcessRealTimeData(string jsonData)
        {
            try
            {
                List<RealTimeDataRecord> records = StockDataParser.ParseRealTimeData(jsonData);

                if (records.Count > 0 && realTimeCache != null)
                {
                    // 保护性更新：如果新数据中StockName为空，保留原有的StockName（来自码表）
                    foreach (var record in records)
                    {
                        if (string.IsNullOrEmpty(record.StockCode))
                            continue;

                        var existingRecord = realTimeCache.GetData(record.StockCode);
                        if (existingRecord != null && string.IsNullOrEmpty(record.StockName))
                        {
                            // 保留原有的StockName
                            record.StockName = existingRecord.StockName;
                        }
                    }

                    realTimeCache.UpdateData(records);
                    Console.WriteLine("成功更新 {0} 条实时数据到内存缓存（当前缓存数量: {1}）",
                        records.Count, realTimeCache.Count);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("处理实时数据失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 处理除权数据
        /// </summary>
        private void ProcessExRightsData(string jsonData)
        {
            try
            {
                List<ExRightsDataRecord> records = StockDataParser.ParseExRightsData(jsonData);

                if (records.Count > 0)
                {
                    int savedCount = exRightsDbWriter.SaveExRightsData(records);
                    Console.WriteLine("成功保存 {0} 条除权数据到PostgreSQL", savedCount);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("处理除权数据失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 处理码表数据
        /// </summary>
        private void ProcessMarketTableData(string jsonData)
        {
            try
            {
                List<MarketTableDataRecord> records = StockDataParser.ParseMarketTableData(jsonData);

                if (records.Count > 0)
                {
                    // 保存股票信息到数据库（stock_info表）
                    var stockInfoList = new List<(string StockCode, string StockName, ushort MarketCode)>();
                    foreach (var record in records)
                    {
                        if (!string.IsNullOrEmpty(record.StockCode) && !string.IsNullOrEmpty(record.StockName))
                        {
                            stockInfoList.Add((record.StockCode, record.StockName, (ushort)record.MarketCode));
                        }
                    }

                    if (stockInfoList.Count > 0)
                    {
                        // 1. 先更新内存缓存（立即生效，无延迟）
                        foreach (var info in stockInfoList)
                        {
                            StockInfoCache.Instance.UpdateStockInfo(info.StockCode, info.StockName, info.MarketCode);
                        }
                        Console.WriteLine("已更新 {0} 条股票信息到内存缓存", stockInfoList.Count);

                        // 2. 异步保存到数据库（后台执行，不阻塞主流程）
                        var listToSave = new List<(string StockCode, string StockName, ushort MarketCode)>(stockInfoList);
                        System.Threading.Tasks.Task.Run(() =>
                        {
                            try
                            {
                                var repository = new PostgresStockDataRepository();
                                int savedCount = repository.SaveStockInfo(listToSave);
                                Console.WriteLine("异步保存 {0} 条股票信息到数据库完成", savedCount);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("异步保存股票信息到数据库失败: {0}", ex.Message);
                            }
                        });
                    }

                    // 将码表数据更新到实时数据缓存中（更新股票名称）
                    if (realTimeCache != null)
                    {
                        int updatedCount = 0;
                        foreach (var record in records)
                        {
                            if (string.IsNullOrEmpty(record.StockCode))
                                continue;

                            // 获取现有的实时数据记录
                            var existingRecord = realTimeCache.GetData(record.StockCode);
                            if (existingRecord != null)
                            {
                                // 更新股票名称
                                existingRecord.StockName = record.StockName;
                                realTimeCache.UpdateData(existingRecord);
                                updatedCount++;
                            }
                            else
                            {
                                // 如果没有实时数据，创建一个只包含基本信息的记录
                                var newRecord = new RealTimeDataRecord
                                {
                                    StockCode = record.StockCode,
                                    StockName = record.StockName,
                                    MarketCode = (ushort)record.MarketCode,
                                    UpdateTime = record.UpdateTime
                                };
                                realTimeCache.UpdateData(newRecord);
                                updatedCount++;
                            }
                        }
                        Console.WriteLine("成功更新 {0} 条码表数据到内存缓存（当前缓存数量: {1}）",
                            updatedCount, realTimeCache.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("处理码表数据失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        private void LoadConfig()
        {
            try
            {
                port = _configProvider.GetInt("MQPort", 5678);
                queueName = _configProvider.GetString("MQQueueName", "daily_data_queue");
            }
            catch
            {
                Console.WriteLine("警告: 读取配置失败，使用默认值");
                port = 5678;
                queueName = "daily_data_queue";
            }
        }
    }
}
