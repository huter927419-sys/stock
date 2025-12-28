using System;

namespace MQReceiver.Configuration
{
    /// <summary>
    /// 配置提供者接口
    /// 定义统一的配置访问方式
    /// </summary>
    public interface IConfigurationProvider
    {
        /// <summary>
        /// 获取字符串配置
        /// </summary>
        string GetString(string key, string defaultValue = null);

        /// <summary>
        /// 获取整数配置
        /// </summary>
        int GetInt(string key, int defaultValue = 0);

        /// <summary>
        /// 获取十进制数配置
        /// </summary>
        decimal GetDecimal(string key, decimal defaultValue = 0);

        /// <summary>
        /// 获取布尔配置
        /// </summary>
        bool GetBool(string key, bool defaultValue = false);

        /// <summary>
        /// 获取可空十进制数配置
        /// </summary>
        decimal? GetNullableDecimal(string key);

        /// <summary>
        /// 配置是否存在
        /// </summary>
        bool HasKey(string key);

        /// <summary>
        /// 获取数据库配置
        /// </summary>
        DatabaseConfig GetDatabaseConfig();

        /// <summary>
        /// 获取Redis配置
        /// </summary>
        RedisConfig GetRedisConfig();

        /// <summary>
        /// 获取过滤器配置
        /// </summary>
        FilterConfig GetFilterConfig();

        /// <summary>
        /// 获取MQ配置
        /// </summary>
        MQConfig GetMQConfig();

        /// <summary>
        /// 保存配置值
        /// </summary>
        void SetValue(string key, string value);

        /// <summary>
        /// 保存十进制数配置
        /// </summary>
        void SetDecimal(string key, decimal value);
    }

    /// <summary>
    /// 数据库配置
    /// </summary>
    public class DatabaseConfig
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 8532;
        public string Database { get; set; } = "stockdb";
        public string Username { get; set; } = "postgres";
        public string Password { get; set; } = "password";
        public int MinPoolSize { get; set; } = 5;
        public int MaxPoolSize { get; set; } = 20;
        public int ConnectionLifetime { get; set; } = 300;
        public int CommandTimeout { get; set; } = 30;
    }

    /// <summary>
    /// Redis配置
    /// </summary>
    public class RedisConfig
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 6379;
        public string Password { get; set; }
        public int Database { get; set; } = 0;
        public int ConnectTimeout { get; set; } = 5000;
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// 过滤器配置
    /// </summary>
    public class FilterConfig
    {
        public int IntervalMinutes { get; set; } = 30;

        // 条件1配置
        public bool Filter1_RequireWeeklyGoldenCross { get; set; } = true;
        public bool Filter1_RequireMonthlyGoldenCross { get; set; } = true;
        public bool Filter1_RequireQuarterlyGoldenCross { get; set; } = true;
        public decimal Filter1_WeeklyKDefaultMin { get; set; } = 0;
        public decimal Filter1_MonthlyKDefaultMin { get; set; } = 0;
        public decimal Filter1_QuarterlyKDefaultMin { get; set; } = 0;
        public decimal? Filter1_WeeklyKMin { get; set; }
        public decimal? Filter1_WeeklyKMax { get; set; }
        public decimal? Filter1_MonthlyKMin { get; set; }
        public decimal? Filter1_MonthlyKMax { get; set; }
        public decimal? Filter1_QuarterlyKMin { get; set; }
        public decimal? Filter1_QuarterlyKMax { get; set; }
        public decimal? Filter1_PriceMin { get; set; }
        public decimal? Filter1_VolumeMin { get; set; }

        // 条件2配置
        public decimal Filter2_WeeklyKDefaultMin { get; set; } = 0;
        public decimal Filter2_MonthlyKDefaultMin { get; set; } = 0;
        public decimal Filter2_QuarterlyKDefaultMin { get; set; } = 0;
        public decimal? Filter2_WeeklyKMin { get; set; }
        public decimal? Filter2_WeeklyKMax { get; set; }
        public decimal? Filter2_MonthlyKMin { get; set; }
        public decimal? Filter2_MonthlyKMax { get; set; }
        public decimal? Filter2_QuarterlyKMin { get; set; }
        public decimal? Filter2_QuarterlyKMax { get; set; }
        public decimal? Filter2_PriceMin { get; set; }
        public decimal? Filter2_VolumeMin { get; set; }

        // 条件3配置
        public decimal Filter3_WeeklyKDefaultMin { get; set; } = 0;
        public decimal Filter3_MonthlyKDefaultMin { get; set; } = 0;
        public decimal Filter3_QuarterlyKDefaultMin { get; set; } = 0;
        public decimal? Filter3_WeeklyKMin { get; set; }
        public decimal? Filter3_WeeklyKMax { get; set; }
        public decimal? Filter3_MonthlyKMin { get; set; }
        public decimal? Filter3_MonthlyKMax { get; set; }
        public decimal? Filter3_QuarterlyKMin { get; set; }
        public decimal? Filter3_QuarterlyKMax { get; set; }
        public decimal? Filter3_PriceMin { get; set; }
        public decimal? Filter3_VolumeMin { get; set; }
    }

    /// <summary>
    /// MQ配置
    /// </summary>
    public class MQConfig
    {
        public string Host { get; set; } = "192.168.159.128";
        public int Port { get; set; } = 5672;
        public string Username { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public string VirtualHost { get; set; } = "/";
        public string QueueName { get; set; } = "stock.daily.data";
        public string ExchangeName { get; set; } = "stock.exchange";
        public string RoutingKey { get; set; } = "stock.daily.#";
    }
}
