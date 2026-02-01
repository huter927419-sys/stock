using System;
using System.Collections.Generic;
using MQReceiver.Repositories;

namespace MQReceiver.DataProcessing.Repositories
{
    /// <summary>
    /// RocksDB 配置选项
    /// </summary>
    public class RocksDBOptions
    {
        public string DatabasePath { get; set; } = "data/rocksdb";
        public bool CreateIfMissing { get; set; } = true;
        public bool ErrorIfExists { get; set; } = false;
        public int MaxOpenFiles { get; set; } = 1000;
        public int WriteBufferSize { get; set; } = 64 * 1024 * 1024;
        public int MaxWriteBufferNumber { get; set; } = 3;
        public int TargetFileSizeBase { get; set; } = 64 * 1024 * 1024;
        public int Level0FileNumCompactionTrigger { get; set; } = 4;
        public int Level0SlowdownWritesTrigger { get; set; } = 8;
        public int Level0StopWritesTrigger { get; set; } = 12;
        public CompressionType Compression { get; set; } = CompressionType.LZ4;
    }

    /// <summary>
    /// 压缩类型
    /// </summary>
    public enum CompressionType
    {
        NoCompression,
        Snappy,
        ZLib,
        BZip2,
        LZ4,
        LZ4HC
    }

    /// <summary>
    /// RocksDB K线数据仓储接口
    /// 继承自 IKlineDataRepository，用于替换 PostgreSQL
    /// </summary>
    public interface IRocksDBKlineDataRepository : IKlineDataRepository
    {
        /// <summary>
        /// 初始化数据库
        /// </summary>
        bool Initialize();

        /// <summary>
        /// 关闭数据库
        /// </summary>
        void Close();

        /// <summary>
        /// 备份数据库
        /// </summary>
        bool Backup(string backupPath);

        /// <summary>
        /// 恢复数据库
        /// </summary>
        bool Restore(string backupPath);

        /// <summary>
        /// 压缩数据库
        /// </summary>
        bool Compact();

        /// <summary>
        /// 获取数据库统计信息
        /// </summary>
        Dictionary<string, string> GetStatistics();
    }
}
