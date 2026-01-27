using System;
using System.Collections.Generic;
using System.IO;
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
    /// MQ服务包装器 - 支持UI日志输出
    /// 将日志通过事件发送到UI层显示
    /// 支持注入外部缓存以实现与计算服务的数据共享
    /// </summary>
    public class MQServiceWrapper : IDisposable
    {
        private TcpListener tcpListener;
        private volatile bool _isRunning = false;
        private int port;
        private string queueName;

        private DailyDataDBWriter dbWriter;
        private RealTimeDataDBWriter realTimeDbWriter;
        private ExRightsDataDBWriter exRightsDbWriter;
        private RealTimeDataCache realTimeCache;
        private bool _ownsCache = true;  // 是否拥有缓存的生命周期管理权
        private bool _disposed = false;

        private static readonly IConfigurationProvider _configProvider = AppConfigProvider.Instance;

        /// <summary>
        /// 日志消息事件
        /// </summary>
        public event EventHandler<string> LogMessage;

        /// <summary>
        /// 状态变化事件
        /// </summary>
        public event EventHandler<bool> StatusChanged;

        /// <summary>
        /// 是否正在运行
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// 获取实时数据缓存（只读访问）
        /// </summary>
        public RealTimeDataCache RealTimeCache => realTimeCache;

        /// <summary>
        /// 设置外部缓存（用于与计算服务共享）
        /// 必须在Start()之前调用
        /// </summary>
        public void SetExternalCache(RealTimeDataCache cache)
        {
            if (_isRunning)
                throw new InvalidOperationException("Cannot set cache while service is running");

            realTimeCache = cache;
            _ownsCache = false;  // 不拥有缓存的生命周期
        }

        /// <summary>
        /// 发送日志消息
        /// </summary>
        private void Log(string message)
        {
            LogMessage?.Invoke(this, message);
        }

        /// <summary>
        /// 启动MQ服务（非阻塞）
        /// </summary>
        public bool Start()
        {
            try
            {
                Log("========================================");
                Log("  启动MQ数据同步服务");
                Log("========================================");

                // 读取配置
                LoadConfig();

                // 初始化Redis连接（可选）
                try
                {
                    RedisHelper.Initialize();
                    Log("Redis连接初始化成功");
                }
                catch (Exception ex)
                {
                    Log($"警告: Redis初始化失败: {ex.Message}，将使用数据库直接查询");
                }

                // 初始化数据库写入器
                Log("正在初始化数据库连接...");
                dbWriter = new DailyDataDBWriter();
                realTimeDbWriter = new RealTimeDataDBWriter();
                exRightsDbWriter = new ExRightsDataDBWriter();

                if (!dbWriter.TestConnection() || !realTimeDbWriter.TestConnection() || !exRightsDbWriter.TestConnection())
                {
                    Log("错误: PostgreSQL数据库连接失败！");
                    Log("请检查数据库配置和连接。");
                    StatusChanged?.Invoke(this, false);
                    return false;
                }
                Log("PostgreSQL数据库连接成功");
                Log(DatabaseConnectionHelper.GetPoolInfo());

                // 自动创建数据库表（如果不存在）
                if (!DatabaseInitializer.Initialize())
                {
                    Log("警告: 数据库表初始化失败，但服务将继续运行");
                }

                // 初始化实时数据缓存（如果没有设置外部缓存）
                try
                {
                    if (realTimeCache == null)
                    {
                        realTimeCache = new RealTimeDataCache();
                        _ownsCache = true;
                        Log("实时数据缓存初始化成功（内部创建）");
                    }
                    else
                    {
                        Log($"使用外部共享缓存（当前数据量: {realTimeCache.Count}）");
                    }
                }
                catch (Exception ex)
                {
                    Log("警告: 初始化实时数据缓存失败: " + ex.Message);
                }

                // 启动TCP监听
                tcpListener = new TcpListener(IPAddress.Any, port);
                tcpListener.Start();
                _isRunning = true;
                StatusChanged?.Invoke(this, true);

                Log($"MQ接收器已启动，监听端口: {port}");
                Log($"队列名称: {queueName}");
                Log("等待虚拟机连接...");

                // 启动监听线程
                Thread listenThread = new Thread(ListenForClients)
                {
                    IsBackground = true,
                    Name = "MQListenerThread"
                };
                listenThread.Start();

                return true;
            }
            catch (Exception ex)
            {
                Log("错误: 启动MQ服务失败: " + ex.Message);
                StatusChanged?.Invoke(this, false);
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
                var listener = tcpListener;
                if (listener != null)
                {
                    listener.Stop();
                    Log("TCP监听已停止");
                }
            }
            catch (Exception ex)
            {
                Log("停止TCP监听时出错: " + ex.Message);
            }
            StatusChanged?.Invoke(this, false);
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
                    Stop();

                    // 只有当我们拥有缓存时才释放它
                    if (realTimeCache != null && _ownsCache)
                    {
                        realTimeCache.Dispose();
                        Log("实时数据缓存已释放");
                    }
                    realTimeCache = null;

                    dbWriter = null;
                    realTimeDbWriter = null;
                    exRightsDbWriter = null;
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
                    var listener = tcpListener;
                    if (listener == null)
                        break;

                    TcpClient client = listener.AcceptTcpClient();
                    Log($"客户端已连接: {client.Client.RemoteEndPoint}");

                    ThreadPool.QueueUserWorkItem(state =>
                    {
                        try
                        {
                            HandleClient((TcpClient)state);
                        }
                        catch (Exception ex)
                        {
                            Log("处理客户端时出错: " + ex.Message);
                        }
                    }, client);
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                    {
                        Log("接受客户端连接时出错: " + ex.Message);
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
                stream.ReadTimeout = 30000;

                while (client.Connected && _isRunning)
                {
                    byte[] lengthBytes = new byte[4];
                    int bytesRead = ReadBytes(stream, lengthBytes, 4);
                    if (bytesRead != 4)
                    {
                        break;
                    }

                    int messageLength = BitConverter.ToInt32(lengthBytes, 0);
                    if (messageLength <= 0 || messageLength > 10 * 1024 * 1024)
                    {
                        Log($"错误: 无效的消息长度: {messageLength}");
                        break;
                    }

                    byte[] queueNameLengthBytes = new byte[4];
                    bytesRead = ReadBytes(stream, queueNameLengthBytes, 4);
                    if (bytesRead != 4)
                    {
                        break;
                    }

                    int queueNameLength = BitConverter.ToInt32(queueNameLengthBytes, 0);
                    if (queueNameLength < 0 || queueNameLength > 256)
                    {
                        Log($"错误: 无效的队列名称长度: {queueNameLength}");
                        break;
                    }

                    byte[] queueNameBytes = new byte[queueNameLength];
                    bytesRead = ReadBytes(stream, queueNameBytes, queueNameLength);
                    if (bytesRead != queueNameLength)
                    {
                        break;
                    }

                    string receivedQueueName = Encoding.UTF8.GetString(queueNameBytes);

                    int dataLength = messageLength - 4 - 4 - queueNameLength;
                    byte[] dataBytes = new byte[dataLength];
                    bytesRead = ReadBytes(stream, dataBytes, dataLength);
                    if (bytesRead != dataLength)
                    {
                        break;
                    }

                    string jsonData = Encoding.UTF8.GetString(dataBytes);

                    Log($"收到数据: 队列={receivedQueueName}, 大小={dataLength} 字节");

                    bool success = false;
                    if (receivedQueueName == "daily_data_queue")
                    {
                        ProcessDailyData(jsonData);
                        success = true;
                    }
                    else if (receivedQueueName == "realtime_data_queue")
                    {
                        ProcessRealTimeData(jsonData);
                        success = true;
                    }
                    else if (receivedQueueName == "ex_rights_data_queue")
                    {
                        ProcessExRightsData(jsonData);
                        success = true;
                    }
                    else if (receivedQueueName == "market_table_queue")
                    {
                        ProcessMarketTableData(jsonData);
                        success = true;
                    }
                    else
                    {
                        Log($"未知的队列名称: {receivedQueueName}");
                    }

                    // 发送ACK确认响应给发送端
                    SendAck(stream, success);
                }
            }
            catch (Exception ex)
            {
                Log("处理客户端时出错: " + ex.Message);
            }
            finally
            {
                if (stream != null)
                {
                    stream.Close();
                }
                client.Close();
                Log("客户端已断开连接");
            }
        }

        private int ReadBytes(NetworkStream stream, byte[] buffer, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int bytesRead = stream.Read(buffer, totalRead, count - totalRead);
                if (bytesRead == 0)
                {
                    return totalRead;
                }
                totalRead += bytesRead;
            }
            return totalRead;
        }

        /// <summary>
        /// 发送ACK确认响应给发送端
        /// 协议：4字节 "ACK\0" 表示成功，"NAK\0" 表示失败
        /// </summary>
        private void SendAck(NetworkStream stream, bool success)
        {
            try
            {
                byte[] ack = success
                    ? new byte[] { (byte)'A', (byte)'C', (byte)'K', 0 }
                    : new byte[] { (byte)'N', (byte)'A', (byte)'K', 0 };
                stream.Write(ack, 0, 4);
                stream.Flush();
            }
            catch (Exception ex)
            {
                Log($"发送ACK失败: {ex.Message}");
            }
        }

        private void ProcessDailyData(string jsonData)
        {
            try
            {
                List<DailyDataRecord> records = StockDataParser.ParseDailyData(jsonData);

                if (records.Count > 0)
                {
                    int savedCount = dbWriter.SaveDailyData(records);
                    Log($"成功保存 {savedCount} 条日线数据到PostgreSQL");
                }
            }
            catch (Exception ex)
            {
                Log("处理日线数据失败: " + ex.Message);
            }
        }

        private void ProcessRealTimeData(string jsonData)
        {
            try
            {
                List<RealTimeDataRecord> records = StockDataParser.ParseRealTimeData(jsonData);

                if (records.Count > 0 && realTimeCache != null)
                {
                    // 计算ST股票和非A股股票
                    var filteredRecords = new List<RealTimeDataRecord>();
                    int stFilteredCount = 0;
                    
                    foreach (var record in records)
                    {
                        if (string.IsNullOrEmpty(record.StockCode))
                            continue;

                        // 计算非A股股票
                        if (!StockDataParser.IsValidStockCode(record.StockCode))
                            continue;

                        var existingRecord = realTimeCache.GetData(record.StockCode);
                        if (existingRecord != null && string.IsNullOrEmpty(record.StockName))
                        {
                            record.StockName = existingRecord.StockName;
                        }
                        
                        // 获取股票名称（用于检查ST股票）
                        string stockName = record.StockName;
                        if (string.IsNullOrEmpty(stockName))
                        {
                            stockName = StockInfoCache.Instance.GetStockName(record.StockCode);
                        }
                        
                        // 计算ST股票
                        if (StockDataParser.IsSTStock(stockName))
                        {
                            stFilteredCount++;
                            continue;
                        }
                        
                        filteredRecords.Add(record);
                    }
                    
                    if (stFilteredCount > 0)
                    {
                        Log($"[实时数据] 已计算 {stFilteredCount} 只ST股票");
                    }

                    realTimeCache.UpdateData(filteredRecords);
                    Log($"成功更新 {filteredRecords.Count} 条实时数据到内存缓存（当前缓存数量: {realTimeCache.Count}）");
                }
            }
            catch (Exception ex)
            {
                Log("处理实时数据失败: " + ex.Message);
            }
        }

        private void ProcessExRightsData(string jsonData)
        {
            try
            {
                List<ExRightsDataRecord> records = StockDataParser.ParseExRightsData(jsonData);

                if (records.Count > 0)
                {
                    int savedCount = exRightsDbWriter.SaveExRightsData(records);
                    Log($"成功保存 {savedCount} 条除权数据到PostgreSQL");
                }
            }
            catch (Exception ex)
            {
                Log("处理除权数据失败: " + ex.Message);
            }
        }

        private void ProcessMarketTableData(string jsonData)
        {
            try
            {
                List<MarketTableDataRecord> records = StockDataParser.ParseMarketTableData(jsonData);

                if (records.Count > 0)
                {
                    // 1. 更新 StockInfoCache（股票名称缓存）和异步保存到数据库
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
                        // 更新内存缓存（立即生效）
                        foreach (var info in stockInfoList)
                        {
                            StockInfoCache.Instance.UpdateStockInfo(info.StockCode, info.StockName, info.MarketCode);
                        }
                        Log($"已更新 {stockInfoList.Count} 条股票信息到 StockInfoCache");

                        // 异步保存到数据库
                        var listToSave = new List<(string StockCode, string StockName, ushort MarketCode)>(stockInfoList);
                        System.Threading.Tasks.Task.Run(() =>
                        {
                            try
                            {
                                var repository = new PostgresStockDataRepository();
                                int savedCount = repository.SaveStockInfo(listToSave);
                                Log($"异步保存 {savedCount} 条股票信息到数据库完成");
                            }
                            catch (Exception ex)
                            {
                                Log($"异步保存股票信息到数据库失败: {ex.Message}");
                            }
                        });
                    }

                    // 2. 更新 realTimeCache（实时数据缓存）
                    if (realTimeCache != null)
                    {
                        int updatedCount = 0;
                        foreach (var record in records)
                        {
                            if (string.IsNullOrEmpty(record.StockCode))
                                continue;

                            var existingRecord = realTimeCache.GetData(record.StockCode);
                            if (existingRecord != null)
                            {
                                existingRecord.StockName = record.StockName;
                                realTimeCache.UpdateData(existingRecord);
                                updatedCount++;
                            }
                            else
                            {
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
                        Log($"成功更新 {updatedCount} 条码表数据到实时缓存（当前缓存数量: {realTimeCache.Count}）");
                    }
                }
            }
            catch (Exception ex)
            {
                Log("处理码表数据失败: " + ex.Message);
            }
        }

        private void LoadConfig()
        {
            try
            {
                port = _configProvider.GetInt("MQPort", 5678);
                queueName = _configProvider.GetString("MQQueueName", "daily_data_queue");
            }
            catch
            {
                Log("警告: 读取配置失败，使用默认值");
                port = 5678;
                queueName = "daily_data_queue";
            }
        }
    }
}
