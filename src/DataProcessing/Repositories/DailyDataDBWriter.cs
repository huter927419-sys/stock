using System;
using System.Collections.Generic;
using MQReceiver.Configuration;
using MQReceiver.Helpers;

namespace MQReceiver.Repositories
{
    /// <summary>
    /// 日线数据数据库写入器 - 保存数据到数据库
    /// 注意：此类保留用于向后兼容，新代码建议使用 IStockDataRepository
    /// </summary>
    public class DailyDataDBWriter
    {
        private readonly string connectionString;
        private readonly IStockDataRepository _repository;
        private static readonly IConfigurationProvider _configProvider = AppConfigProvider.Instance;

        /// <summary>
        /// 使用默认仓储初始化（向后兼容）
        /// </summary>
        public DailyDataDBWriter()
        {
            // 使用统一的连接字符串生成器
            connectionString = DatabaseConnectionHelper.BuildConnectionString();
            _repository = new PostgresStockDataRepository(connectionString);

            string host = _configProvider.GetString("DatabaseHost", "localhost");
            int port = _configProvider.GetInt("DatabasePort", 8532);
            string database = _configProvider.GetString("DatabaseName", "stockdb");
            string username = _configProvider.GetString("DatabaseUser", "postgres");

            Console.WriteLine("数据库配置: Host={0}, Port={1}, Database={2}, User={3}",
                host, port, database, username);
            Console.WriteLine(DatabaseConnectionHelper.GetPoolInfo());
        }

        /// <summary>
        /// 使用指定仓储初始化（依赖注入）
        /// </summary>
        public DailyDataDBWriter(IStockDataRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            connectionString = DatabaseConnectionHelper.BuildConnectionString();
        }

        /// <summary>
        /// 测试数据库连接
        /// </summary>
        public bool TestConnection()
        {
            return _repository.TestConnection();
        }

        /// <summary>
        /// 保存日线数据到数据库
        /// </summary>
        public int SaveDailyData(List<DailyDataRecord> records)
        {
            return _repository.SaveDailyData(records);
        }
    }
}
