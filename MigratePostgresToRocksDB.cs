using System;
using MQReceiver.Tools;

namespace MQReceiver
{
    /// <summary>
    /// 数据迁移工具入口程序
    /// 用法: MigratePostgresToRocksDB.exe [RocksDB路径]
    /// </summary>
    class MigratePostgresToRocksDB
    {
        static void Main(string[] args)
        {
            Console.WriteLine(@"
╔═══════════════════════════════════════════════════════════╗
║                                                           ║
║         数据迁移工具: PostgreSQL -> RocksDB               ║
║                                                           ║
╚═══════════════════════════════════════════════════════════╝
");

            // 解析命令行参数
            string rocksDbPath = "data/rocksdb";
            bool skipRealTime = false;
            bool skipLogs = false;
            bool verify = true;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--path":
                    case "-p":
                        if (i + 1 < args.Length)
                        {
                            rocksDbPath = args[i + 1];
                            i++;
                        }
                        break;
                    case "--skip-realtime":
                        skipRealTime = true;
                        break;
                    case "--skip-logs":
                        skipLogs = true;
                        break;
                    case "--no-verify":
                        verify = false;
                        break;
                    case "--help":
                    case "-h":
                        ShowHelp();
                        return;
                }
            }

            Console.WriteLine($"配置:");
            Console.WriteLine($"  RocksDB 路径: {rocksDbPath}");
            Console.WriteLine($"  跳过实时数据: {skipRealTime}");
            Console.WriteLine($"  跳过日志数据: {skipLogs}");
            Console.WriteLine($"  验证迁移结果: {verify}");
            Console.WriteLine();

            // 确认开始迁移
            Console.Write("是否开始迁移? (y/n): ");
            var confirm = Console.ReadLine()?.Trim().ToLower();
            if (confirm != "y" && confirm != "yes")
            {
                Console.WriteLine("已取消迁移");
                return;
            }

            try
            {
                // 创建迁移工具实例
                var migrationTool = new DataMigrationTool(null, rocksDbPath);

                // 执行迁移
                var startTime = DateTime.Now;
                bool success = migrationTool.MigrateAll(skipRealTime, skipLogs);
                var duration = DateTime.Now - startTime;

                if (success)
                {
                    Console.WriteLine($"\n迁移耗时: {duration.TotalSeconds:F2} 秒");

                    // 验证迁移结果
                    if (verify)
                    {
                        migrationTool.VerifyMigration();
                    }

                    Console.WriteLine("\n✓ 迁移成功完成！");
                    Console.WriteLine($"\n数据已保存到: {rocksDbPath}");
                }
                else
                {
                    Console.WriteLine("\n❌ 迁移失败，请检查错误信息");
                    Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ 发生严重错误: {ex.Message}");
                Console.WriteLine($"堆栈跟踪:\n{ex.StackTrace}");
                Environment.Exit(1);
            }

            Console.WriteLine("\n按任意键退出...");
            Console.ReadKey();
        }

        static void ShowHelp()
        {
            Console.WriteLine(@"
用法: MigratePostgresToRocksDB.exe [选项]

选项:
  --path, -p <路径>        指定 RocksDB 数据目录 (默认: data/rocksdb)
  --skip-realtime          跳过实时数据迁移
  --skip-logs              跳过日志数据迁移
  --no-verify              不验证迁移结果
  --help, -h               显示此帮助信息

示例:
  MigratePostgresToRocksDB.exe
  MigratePostgresToRocksDB.exe --path ./mydata/rocksdb
  MigratePostgresToRocksDB.exe --skip-realtime --skip-logs
  MigratePostgresToRocksDB.exe --no-verify

说明:
  该工具会从 PostgreSQL 数据库中读取所有数据并迁移到 RocksDB (文件系统) 存储中。

  迁移内容包括:
  1. 股票日线数据 (stock_daily_data)
  2. 股票基本信息 (stock_info)
  3. 除权数据 (stock_exrights_data)
  4. 实时数据 (stock_realtime_data, 可选)
  5. 复权计算任务 (adjustment_task)
  6. 数据接收日志 (data_receive_log, 可选)

注意:
  - PostgreSQL 连接信息从配置文件读取
  - 迁移过程可能需要较长时间，请耐心等待
  - 迁移前请确保目标目录有足够的磁盘空间
  - 日志数据默认只迁移最近30天的记录
");
        }
    }
}
