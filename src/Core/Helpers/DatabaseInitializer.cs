using System;
using System.Threading;
using MQReceiver.Configuration;
using Npgsql;

namespace MQReceiver.Helpers
{
    /// <summary>
    /// 数据库初始化器
    /// 负责在程序启动时自动检查并创建所需的数据库表
    /// 使用 IF NOT EXISTS 确保幂等性，不会重复创建或覆盖已有数据
    /// 线程安全：使用双重检查锁定确保只初始化一次
    /// </summary>
    public static class DatabaseInitializer
    {
        private static volatile bool _isInitialized = false;
        private static readonly object _lockObject = new object();
        private static IConfigurationProvider _configProvider = AppConfigProvider.Instance;

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public static bool IsInitialized => _isInitialized;

        /// <summary>
        /// 设置配置提供者（用于依赖注入和单元测试）
        /// </summary>
        public static void SetConfigurationProvider(IConfigurationProvider provider)
        {
            _configProvider = provider ?? AppConfigProvider.Instance;
        }

        /// <summary>
        /// 初始化数据库（自动创建表，线程安全）
        /// </summary>
        /// <returns>是否成功</returns>
        public static bool Initialize()
        {
            // 检查配置是否启用自动创建表
            bool autoCreateTables = _configProvider.GetBool("DatabaseAutoCreateTables", true);
            if (!autoCreateTables)
            {
                Console.WriteLine("数据库自动创建表功能已禁用（配置: DatabaseAutoCreateTables=false）");
                _isInitialized = true;
                return true;
            }

            // 快速检查
            if (_isInitialized)
                return true;

            lock (_lockObject)
            {
                // 双重检查
                if (_isInitialized)
                    return true;

                try
                {
                    Console.WriteLine("正在检查并初始化数据库表结构...");

                    string connectionString = DatabaseConnectionHelper.BuildConnectionString();

                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        connection.Open();

                        // 1. 创建股票基本信息表
                        CreateStockInfoTable(connection);

                        // 2. 创建日线数据表
                        CreateDailyDataTable(connection);

                        // 3. 创建实时数据表
                        CreateRealTimeDataTable(connection);

                        // 4. 创建除权数据表
                        CreateExRightsDataTable(connection);

                        // 5. 创建数据接收日志表
                        CreateDataReceiveLogTable(connection);

                        // 6. 创建性能优化索引
                        CreatePerformanceIndexes(connection);
                    }

                    Thread.MemoryBarrier();
                    _isInitialized = true;
                    Console.WriteLine("数据库表结构初始化完成");
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"数据库初始化失败: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 创建股票基本信息表
        /// </summary>
        private static void CreateStockInfoTable(NpgsqlConnection connection)
        {
            string sql = @"
                CREATE TABLE IF NOT EXISTS stock_info (
                    stock_code VARCHAR(10) PRIMARY KEY,
                    stock_name VARCHAR(100),
                    market_code SMALLINT,
                    market_name VARCHAR(50),
                    stock_type VARCHAR(20),
                    is_active BOOLEAN DEFAULT TRUE,
                    create_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    update_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                );

                CREATE INDEX IF NOT EXISTS idx_stock_info_market_code ON stock_info(market_code);
            ";

            ExecuteSql(connection, sql, "stock_info");
        }

        /// <summary>
        /// 创建日线数据表
        /// </summary>
        private static void CreateDailyDataTable(NpgsqlConnection connection)
        {
            string sql = @"
                CREATE TABLE IF NOT EXISTS stock_daily_data (
                    id BIGSERIAL PRIMARY KEY,
                    stock_code VARCHAR(10) NOT NULL,
                    market_code SMALLINT,
                    trade_date DATE NOT NULL,
                    trade_datetime TIMESTAMP,
                    time_stamp INTEGER,
                    open_price NUMERIC(10, 3) NOT NULL,
                    high_price NUMERIC(10, 3) NOT NULL,
                    low_price NUMERIC(10, 3) NOT NULL,
                    close_price NUMERIC(10, 3) NOT NULL,
                    volume NUMERIC(20, 2) NOT NULL,
                    amount NUMERIC(20, 2) NOT NULL,
                    advance_count SMALLINT,
                    decline_count SMALLINT,
                    data_source VARCHAR(50) DEFAULT '龙卷风',
                    create_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    update_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    CONSTRAINT uk_stock_daily_data UNIQUE (stock_code, trade_date)
                );

                CREATE INDEX IF NOT EXISTS idx_stock_daily_data_stock_code ON stock_daily_data(stock_code);
                CREATE INDEX IF NOT EXISTS idx_stock_daily_data_trade_date ON stock_daily_data(trade_date);
                CREATE INDEX IF NOT EXISTS idx_stock_daily_data_market_code ON stock_daily_data(market_code);
                CREATE INDEX IF NOT EXISTS idx_stock_daily_data_time_stamp ON stock_daily_data(time_stamp);
                CREATE INDEX IF NOT EXISTS idx_stock_daily_data_code_date ON stock_daily_data(stock_code, trade_date DESC);
            ";

            ExecuteSql(connection, sql, "stock_daily_data");
        }

        /// <summary>
        /// 创建实时数据表
        /// </summary>
        private static void CreateRealTimeDataTable(NpgsqlConnection connection)
        {
            string sql = @"
                CREATE TABLE IF NOT EXISTS stock_realtime_data (
                    id BIGSERIAL PRIMARY KEY,
                    stock_code VARCHAR(10) NOT NULL,
                    stock_name VARCHAR(100),
                    market_code SMALLINT,
                    new_price NUMERIC(10, 3),
                    open_price NUMERIC(10, 3),
                    high_price NUMERIC(10, 3),
                    low_price NUMERIC(10, 3),
                    close_price NUMERIC(10, 3),
                    pre_close_price NUMERIC(10, 3),
                    volume NUMERIC(20, 2),
                    amount NUMERIC(20, 2),
                    buy_volume NUMERIC(20, 2),
                    sell_volume NUMERIC(20, 2),
                    buy_price1 NUMERIC(10, 3),
                    buy_volume1 NUMERIC(20, 2),
                    sell_price1 NUMERIC(10, 3),
                    sell_volume1 NUMERIC(20, 2),
                    trade_count INTEGER,
                    update_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    data_source VARCHAR(50) DEFAULT '龙卷风-MQ',
                    CONSTRAINT uk_stock_realtime_data UNIQUE (stock_code)
                );

                CREATE INDEX IF NOT EXISTS idx_stock_realtime_data_stock_code ON stock_realtime_data(stock_code);
                CREATE INDEX IF NOT EXISTS idx_stock_realtime_data_market_code ON stock_realtime_data(market_code);
                CREATE INDEX IF NOT EXISTS idx_stock_realtime_data_update_time ON stock_realtime_data(update_time DESC);
            ";

            ExecuteSql(connection, sql, "stock_realtime_data");
        }

        /// <summary>
        /// 创建除权数据表
        /// </summary>
        private static void CreateExRightsDataTable(NpgsqlConnection connection)
        {
            string sql = @"
                CREATE TABLE IF NOT EXISTS stock_exrights_data (
                    id BIGSERIAL PRIMARY KEY,
                    stock_code VARCHAR(10) NOT NULL,
                    market_code SMALLINT,
                    ex_rights_date DATE NOT NULL,
                    ex_rights_datetime TIMESTAMP,
                    time_stamp INTEGER,
                    give_per_10_shares NUMERIC(10, 4) DEFAULT 0,
                    pei_per_10_shares NUMERIC(10, 4) DEFAULT 0,
                    pei_price NUMERIC(10, 3) DEFAULT 0,
                    profit_per_share NUMERIC(10, 4) DEFAULT 0,
                    data_source VARCHAR(50) DEFAULT '龙卷风-MQ',
                    create_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    update_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    CONSTRAINT uk_stock_exrights_data UNIQUE (stock_code, ex_rights_date)
                );

                CREATE INDEX IF NOT EXISTS idx_stock_exrights_data_stock_code ON stock_exrights_data(stock_code);
                CREATE INDEX IF NOT EXISTS idx_stock_exrights_data_ex_rights_date ON stock_exrights_data(ex_rights_date);
                CREATE INDEX IF NOT EXISTS idx_stock_exrights_data_market_code ON stock_exrights_data(market_code);
                CREATE INDEX IF NOT EXISTS idx_stock_exrights_data_time_stamp ON stock_exrights_data(time_stamp);
                CREATE INDEX IF NOT EXISTS idx_stock_exrights_data_code_date ON stock_exrights_data(stock_code, ex_rights_date DESC);
            ";

            ExecuteSql(connection, sql, "stock_exrights_data");
        }

        /// <summary>
        /// 创建数据接收日志表
        /// </summary>
        private static void CreateDataReceiveLogTable(NpgsqlConnection connection)
        {
            string sql = @"
                CREATE TABLE IF NOT EXISTS data_receive_log (
                    id BIGSERIAL PRIMARY KEY,
                    receive_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    data_type VARCHAR(50),
                    record_count INTEGER,
                    queue_name VARCHAR(100),
                    source_ip VARCHAR(50),
                    status VARCHAR(20) DEFAULT 'success',
                    error_message TEXT,
                    processing_time_ms INTEGER
                );

                CREATE INDEX IF NOT EXISTS idx_data_receive_log_receive_time ON data_receive_log(receive_time);
                CREATE INDEX IF NOT EXISTS idx_data_receive_log_status ON data_receive_log(status);
            ";

            ExecuteSql(connection, sql, "data_receive_log");
        }

        /// <summary>
        /// 创建性能优化索引（额外的复合索引）
        /// </summary>
        private static void CreatePerformanceIndexes(NpgsqlConnection connection)
        {
            string sql = @"
                -- 日线数据升序索引（用于正向遍历）
                CREATE INDEX IF NOT EXISTS idx_stock_daily_data_code_date_asc
                ON stock_daily_data(stock_code, trade_date ASC);

                -- 实时数据股票代码索引
                CREATE INDEX IF NOT EXISTS idx_stock_realtime_data_code
                ON stock_realtime_data(stock_code);
            ";

            ExecuteSql(connection, sql, "performance_indexes");
        }

        /// <summary>
        /// 执行SQL语句
        /// </summary>
        private static void ExecuteSql(NpgsqlConnection connection, string sql, string tableName)
        {
            try
            {
                using (var command = new NpgsqlCommand(sql, connection))
                {
                    command.CommandTimeout = 60; // 60秒超时
                    command.ExecuteNonQuery();
                }
                Console.WriteLine($"  ✓ 表/索引已就绪: {tableName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ 创建 {tableName} 失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 检查表是否存在
        /// </summary>
        public static bool TableExists(string tableName)
        {
            try
            {
                string connectionString = DatabaseConnectionHelper.BuildConnectionString();

                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    string sql = @"
                        SELECT EXISTS (
                            SELECT FROM information_schema.tables
                            WHERE table_schema = 'public'
                            AND table_name = @tableName
                        );
                    ";

                    using (var command = new NpgsqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@tableName", tableName);
                        return (bool)command.ExecuteScalar();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"检查表是否存在时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 重置初始化状态（用于测试或重新初始化）
        /// </summary>
        public static void Reset()
        {
            lock (_lockObject)
            {
                _isInitialized = false;
            }
        }
    }
}
