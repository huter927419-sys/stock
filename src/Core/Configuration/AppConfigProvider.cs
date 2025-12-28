using System;
using System.Configuration;

namespace MQReceiver.Configuration
{
    /// <summary>
    /// App.config 配置提供者实现
    /// 从 ConfigurationManager.AppSettings 读取配置
    /// </summary>
    public class AppConfigProvider : IConfigurationProvider
    {
        private static readonly Lazy<AppConfigProvider> _instance =
            new Lazy<AppConfigProvider>(() => new AppConfigProvider());

        /// <summary>
        /// 单例实例
        /// </summary>
        public static AppConfigProvider Instance => _instance.Value;

        private AppConfigProvider()
        {
        }

        /// <summary>
        /// 获取字符串配置
        /// </summary>
        public string GetString(string key, string defaultValue = null)
        {
            var value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }

        /// <summary>
        /// 获取整数配置
        /// </summary>
        public int GetInt(string key, int defaultValue = 0)
        {
            var value = ConfigurationManager.AppSettings[key];
            if (string.IsNullOrEmpty(value))
                return defaultValue;

            return int.TryParse(value, out int result) ? result : defaultValue;
        }

        /// <summary>
        /// 获取十进制数配置
        /// </summary>
        public decimal GetDecimal(string key, decimal defaultValue = 0)
        {
            var value = ConfigurationManager.AppSettings[key];
            if (string.IsNullOrEmpty(value))
                return defaultValue;

            return decimal.TryParse(value, out decimal result) ? result : defaultValue;
        }

        /// <summary>
        /// 获取布尔配置
        /// </summary>
        public bool GetBool(string key, bool defaultValue = false)
        {
            var value = ConfigurationManager.AppSettings[key];
            if (string.IsNullOrEmpty(value))
                return defaultValue;

            return bool.TryParse(value, out bool result) ? result : defaultValue;
        }

        /// <summary>
        /// 获取可空十进制数配置
        /// </summary>
        public decimal? GetNullableDecimal(string key)
        {
            var value = ConfigurationManager.AppSettings[key];
            if (string.IsNullOrEmpty(value))
                return null;

            return decimal.TryParse(value, out decimal result) ? result : (decimal?)null;
        }

        /// <summary>
        /// 配置是否存在
        /// </summary>
        public bool HasKey(string key)
        {
            return ConfigurationManager.AppSettings[key] != null;
        }

        /// <summary>
        /// 获取数据库配置
        /// </summary>
        public DatabaseConfig GetDatabaseConfig()
        {
            return new DatabaseConfig
            {
                Host = GetString("DatabaseHost", "localhost"),
                Port = GetInt("DatabasePort", 8532),
                Database = GetString("DatabaseName", "stockdb"),
                Username = GetString("DatabaseUser", "postgres"),
                Password = GetString("DatabasePassword", "password"),
                MinPoolSize = GetInt("DatabaseMinPoolSize", 5),
                MaxPoolSize = GetInt("DatabaseMaxPoolSize", 20),
                ConnectionLifetime = GetInt("DatabaseConnectionLifetime", 300),
                CommandTimeout = GetInt("DatabaseCommandTimeout", 30)
            };
        }

        /// <summary>
        /// 获取Redis配置
        /// </summary>
        public RedisConfig GetRedisConfig()
        {
            return new RedisConfig
            {
                Host = GetString("RedisHost", "localhost"),
                Port = GetInt("RedisPort", 6379),
                Password = GetString("RedisPassword"),
                Database = GetInt("RedisDatabase", 0),
                ConnectTimeout = GetInt("RedisConnectTimeout", 5000),
                Enabled = GetBool("RedisEnabled", true)
            };
        }

        /// <summary>
        /// 获取过滤器配置
        /// </summary>
        public FilterConfig GetFilterConfig()
        {
            return new FilterConfig
            {
                IntervalMinutes = GetInt("FilterService_IntervalMinutes", 30),

                // 条件1
                Filter1_RequireWeeklyGoldenCross = GetBool("Filter1_RequireWeeklyGoldenCross", true),
                Filter1_RequireMonthlyGoldenCross = GetBool("Filter1_RequireMonthlyGoldenCross", true),
                Filter1_RequireQuarterlyGoldenCross = GetBool("Filter1_RequireQuarterlyGoldenCross", true),
                Filter1_WeeklyKDefaultMin = GetDecimal("Filter1_WeeklyKDefaultMin", 0),
                Filter1_MonthlyKDefaultMin = GetDecimal("Filter1_MonthlyKDefaultMin", 0),
                Filter1_QuarterlyKDefaultMin = GetDecimal("Filter1_QuarterlyKDefaultMin", 0),
                Filter1_WeeklyKMin = GetNullableDecimal("Filter1_WeeklyKMin"),
                Filter1_WeeklyKMax = GetNullableDecimal("Filter1_WeeklyKMax"),
                Filter1_MonthlyKMin = GetNullableDecimal("Filter1_MonthlyKMin"),
                Filter1_MonthlyKMax = GetNullableDecimal("Filter1_MonthlyKMax"),
                Filter1_QuarterlyKMin = GetNullableDecimal("Filter1_QuarterlyKMin"),
                Filter1_QuarterlyKMax = GetNullableDecimal("Filter1_QuarterlyKMax"),
                Filter1_PriceMin = GetNullableDecimal("Filter1_PriceMin"),
                Filter1_VolumeMin = GetNullableDecimal("Filter1_VolumeMin"),

                // 条件2
                Filter2_WeeklyKDefaultMin = GetDecimal("Filter2_WeeklyKDefaultMin", 0),
                Filter2_MonthlyKDefaultMin = GetDecimal("Filter2_MonthlyKDefaultMin", 0),
                Filter2_QuarterlyKDefaultMin = GetDecimal("Filter2_QuarterlyKDefaultMin", 0),
                Filter2_WeeklyKMin = GetNullableDecimal("Filter2_WeeklyKMin"),
                Filter2_WeeklyKMax = GetNullableDecimal("Filter2_WeeklyKMax"),
                Filter2_MonthlyKMin = GetNullableDecimal("Filter2_MonthlyKMin"),
                Filter2_MonthlyKMax = GetNullableDecimal("Filter2_MonthlyKMax"),
                Filter2_QuarterlyKMin = GetNullableDecimal("Filter2_QuarterlyKMin"),
                Filter2_QuarterlyKMax = GetNullableDecimal("Filter2_QuarterlyKMax"),
                Filter2_PriceMin = GetNullableDecimal("Filter2_PriceMin"),
                Filter2_VolumeMin = GetNullableDecimal("Filter2_VolumeMin"),

                // 条件3
                Filter3_WeeklyKDefaultMin = GetDecimal("Filter3_WeeklyKDefaultMin", 0),
                Filter3_MonthlyKDefaultMin = GetDecimal("Filter3_MonthlyKDefaultMin", 0),
                Filter3_QuarterlyKDefaultMin = GetDecimal("Filter3_QuarterlyKDefaultMin", 0),
                Filter3_WeeklyKMin = GetNullableDecimal("Filter3_WeeklyKMin"),
                Filter3_WeeklyKMax = GetNullableDecimal("Filter3_WeeklyKMax"),
                Filter3_MonthlyKMin = GetNullableDecimal("Filter3_MonthlyKMin"),
                Filter3_MonthlyKMax = GetNullableDecimal("Filter3_MonthlyKMax"),
                Filter3_QuarterlyKMin = GetNullableDecimal("Filter3_QuarterlyKMin"),
                Filter3_QuarterlyKMax = GetNullableDecimal("Filter3_QuarterlyKMax"),
                Filter3_PriceMin = GetNullableDecimal("Filter3_PriceMin"),
                Filter3_VolumeMin = GetNullableDecimal("Filter3_VolumeMin")
            };
        }

        /// <summary>
        /// 获取MQ配置
        /// </summary>
        public MQConfig GetMQConfig()
        {
            return new MQConfig
            {
                Host = GetString("RabbitMQ_Host", "192.168.159.128"),
                Port = GetInt("RabbitMQ_Port", 5672),
                Username = GetString("RabbitMQ_Username", "guest"),
                Password = GetString("RabbitMQ_Password", "guest"),
                VirtualHost = GetString("RabbitMQ_VirtualHost", "/"),
                QueueName = GetString("RabbitMQ_QueueName", "stock.daily.data"),
                ExchangeName = GetString("RabbitMQ_ExchangeName", "stock.exchange"),
                RoutingKey = GetString("RabbitMQ_RoutingKey", "stock.daily.#")
            };
        }

        /// <summary>
        /// 保存配置值到App.config
        /// </summary>
        public void SetValue(string key, string value)
        {
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                if (config.AppSettings.Settings[key] != null)
                {
                    config.AppSettings.Settings[key].Value = value;
                }
                else
                {
                    config.AppSettings.Settings.Add(key, value);
                }
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存十进制数配置
        /// </summary>
        public void SetDecimal(string key, decimal value)
        {
            SetValue(key, value.ToString());
        }
    }
}
