using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using MQReceiver.Configuration;
using MQReceiver.DataProcessing.Repositories;
using MQReceiver.Helpers;
using MQReceiver.Repositories;
using MQReceiver.Services;
using MQReceiver.Views;

namespace MQReceiver
{
    /// <summary>
    /// 股票数据管理系统 - 主程序
    /// 版本: 2.1
    /// 支持模块化启动：MQ数据同步、数据预加载、KD过滤
    /// 架构改进：使用事件模式解耦服务层与UI层
    /// </summary>
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // 命令行：查询 RocksDB 日线数据并输出报告后退出
            if (args != null && args.Length > 0 && args[0] == "--check-rocksdb")
            {
                RunRocksDBCheck();
                return;
            }

            // 根据 App.config 的 StorageBackend / RocksDBPath 初始化存储后端（接收与过滤均使用该后端）
            try
            {
                StorageConfiguration.Initialize();
            }
            catch (IOException ex)
            {
                Console.WriteLine($"[启动] 存储初始化发生 IO 异常: {ex.Message}");
                Console.WriteLine($"[启动] 堆栈: {ex.StackTrace}");
                if (ex.InnerException != null)
                    Console.WriteLine($"[启动] 内部异常: {ex.InnerException.Message}");
                throw;
            }

            // 检查控制台是否可用
            if (!SafeConsole.IsConsoleAvailable)
            {
                // 没有控制台时，显示 WPF 启动菜单（[1]主程序 [2]HaiLiDrv [3]数据迁移 [0]退出）
                StartWpfWithMenu();
                return;
            }

            // 有控制台时，显示控制台菜单
            ShowMainMenu();

            // 处理用户选择
            while (true)
            {
                SafeConsole.Write("请输入选项 [0-2]: ");
                string input = SafeConsole.ReadLine();

                // 如果ReadLine返回null（控制台不可用），退出循环
                if (input == null)
                {
                    StartFilterService();
                    return;
                }

                if (string.IsNullOrWhiteSpace(input))
                {
                    continue;
                }

                int choice;
                if (!int.TryParse(input.Trim(), out choice))
                {
                    SafeConsole.WriteLine("无效的输入，请输入数字 0-2");
                    SafeConsole.WriteLine();
                    continue;
                }

                switch (choice)
                {
                    case 1:
                        StartMQService();
                        break;
                    case 2:
                        StartFilterService();
                        break;
                    case 0:
                        Exit();
                        return;
                    default:
                        SafeConsole.WriteLine("无效的选项，请输入 0-2");
                        SafeConsole.WriteLine();
                        break;
                }

                // 服务结束后，重新显示菜单
                if (choice >= 1 && choice <= 3)
                {
                    SafeConsole.WriteLine();
                    SafeConsole.WriteLine("按任意键返回主菜单...");
                    SafeConsole.ReadKey();
                    SafeConsole.Clear();
                    ShowMainMenu();
                }
            }
        }

        /// <summary>
        /// 显示主菜单
        /// </summary>
        static void ShowMainMenu()
        {
            SafeConsole.Clear();
            SafeConsole.WriteLine("========================================");
            SafeConsole.WriteLine("  股票数据管理系统");
            SafeConsole.WriteLine("  版本: 2.1");
            SafeConsole.WriteLine("========================================");
            SafeConsole.WriteLine();
            SafeConsole.WriteLine("请选择要启动的服务：");
            SafeConsole.WriteLine();
            SafeConsole.WriteLine("  [1] 启动MQ数据同步服务");
            SafeConsole.WriteLine("      - 接收虚拟机数据");
            SafeConsole.WriteLine("      - 保存到数据库");
            SafeConsole.WriteLine("      - 更新内存缓存");
            SafeConsole.WriteLine();
            SafeConsole.WriteLine("  [2] 启动KD过滤器");
            SafeConsole.WriteLine("      - 定时执行过滤");
            SafeConsole.WriteLine("      - 显示过滤结果");
            SafeConsole.WriteLine();
            SafeConsole.WriteLine("  [0] 退出");
            SafeConsole.WriteLine();
        }

        /// <summary>
        /// 启动MQ数据同步服务
        /// </summary>
        static void StartMQService()
        {
            SafeConsole.Clear();
            using (MQService service = new MQService())
            {
                service.Start();
            }
        }

        /// <summary>
        /// 无控制台时：先显示 WPF 启动菜单（[1]主程序 [2]HaiLiDrv [3]数据迁移 [0]退出），再根据选择打开对应窗口。
        /// </summary>
        static void StartWpfWithMenu()
        {
            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            var menuWindow = new StartupMenuWindow();
            app.MainWindow = menuWindow;
            menuWindow.Show();
            app.Run();
        }

        /// <summary>
        /// 启动主界面（过滤/计算器窗口）
        /// 界面内集成MQ服务和过滤服务的控制
        /// </summary>
        static void StartFilterService()
        {
            SafeConsole.Clear();

            // 创建 WPF Application（如果还没有）
            Application app = Application.Current;
            bool createdApp = false;
            if (app == null)
            {
                app = new Application();
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                createdApp = true;
            }

            FilterMainWindow mainWindow = null;

            try
            {
                // 创建主窗口（窗口内部管理MQ服务和过滤服务）
                mainWindow = new FilterMainWindow();

                // 窗口关闭时关闭应用
                mainWindow.Closed += (sender, e) =>
                {
                    if (createdApp)
                    {
                        app.Shutdown();
                    }
                };

                // 显示窗口
                mainWindow.Show();
                SafeConsole.WriteLine("主窗口已创建并显示");
                SafeConsole.WriteLine();
                SafeConsole.WriteLine("操作说明:");
                SafeConsole.WriteLine("1. 点击「启动MQ服务」接收实时数据");
                SafeConsole.WriteLine("2. 数据接收后，点击「启动过滤」进行分析");
                SafeConsole.WriteLine("3. 点击「刷新」可手动触发过滤");
                SafeConsole.WriteLine();

                // 只有在控制台可用时才检测键盘输入
                if (SafeConsole.IsConsoleAvailable)
                {
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        while (mainWindow != null && mainWindow.IsVisible)
                        {
                            if (SafeConsole.KeyAvailable)
                            {
                                var key = SafeConsole.ReadKey(true);
                                if (key.HasValue && (key.Value.Key == ConsoleKey.Q || key.Value.Key == ConsoleKey.Escape))
                                {
                                    SafeConsole.WriteLine();
                                    SafeConsole.WriteLine("正在关闭...");
                                    app.Dispatcher.Invoke(() =>
                                    {
                                        if (mainWindow != null)
                                        {
                                            mainWindow.Close();
                                        }
                                    });
                                    break;
                                }
                            }
                            System.Threading.Thread.Sleep(100);
                        }
                    });
                }

                // 运行WPF消息循环
                if (createdApp)
                {
                    app.Run();
                }
                else
                {
                    // 等待窗口关闭
                    while (mainWindow != null && mainWindow.IsVisible)
                    {
                        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                            System.Windows.Threading.DispatcherPriority.Background,
                            new Action(() => { }));
                        System.Threading.Thread.Sleep(50);
                    }
                }
            }
            catch (Exception ex)
            {
                SafeConsole.WriteLine($"启动失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 退出程序
        /// </summary>
        static void Exit()
        {
            SafeConsole.WriteLine();
            SafeConsole.WriteLine("感谢使用！");
            SafeConsole.WriteLine("按任意键退出...");
            SafeConsole.ReadKey();
        }

        /// <summary>
        /// 查询 RocksDB 日线数据：统计股票数、记录数、日期范围，并抽样输出到报告文件。
        /// 用法：MQReceiver.exe --check-rocksdb
        /// </summary>
        static void RunRocksDBCheck()
        {
            string dbPath = AppConfigProvider.Instance.GetString("RocksDBPath", "data/rocksdb");
            if (string.IsNullOrWhiteSpace(dbPath)) dbPath = "data/rocksdb";
            dbPath = dbPath.Trim();
            if (!Path.IsPathRooted(dbPath))
                dbPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? ".", dbPath));

            var sb = new StringBuilder();
            sb.AppendLine("========== RocksDB 日线数据查询报告 ==========");
            sb.AppendLine($"查询时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"RocksDB 路径: {dbPath}");
            sb.AppendLine();

            string klineDir = Path.Combine(dbPath, "kline");
            if (!Directory.Exists(dbPath))
            {
                sb.AppendLine("结论: 未找到 RocksDB 目录，日线数据不存在。");
                sb.AppendLine("说明: 请先运行「数据迁移」将 PostgreSQL 日线导入 RocksDB，或确认 App.config 中 RocksDBPath 指向正确目录。");
                WriteReportAndExit(sb, "RocksDB 目录不存在");
            }

            if (!Directory.Exists(klineDir))
            {
                sb.AppendLine("结论: 存在 RocksDB 根目录，但 kline 子目录不存在，日线数据为空。");
                sb.AppendLine("说明: 日线数据应存放在 kline 子目录下的 *.json 文件中（每只股票一个文件）。");
                WriteReportAndExit(sb, "kline 目录不存在");
            }

            var repo = new RocksDBStockDataRepository(dbPath);
            var stockCodes = repo.GetAllStockCodes();
            int stockCount = stockCodes.Count;
            sb.AppendLine($"股票数量（kline 下 *.json 文件数）: {stockCount}");

            if (stockCount == 0)
            {
                sb.AppendLine();
                sb.AppendLine("结论: 日线数据为空，没有任何股票的 K 线文件。");
                sb.AppendLine("说明: 请先运行「数据迁移」从 PostgreSQL 导入日线数据。");
                WriteReportAndExit(sb, "日线数据为空");
            }

            long totalRecords = 0;
            var dateRanges = new List<(string code, DateTime start, DateTime end, int count)>();
            int sampleSize = Math.Min(500, stockCount);
            for (int i = 0; i < sampleSize; i++)
            {
                var code = stockCodes[i];
                var (start, end) = repo.GetDataDateRange(code);
                int count = 0;
                if (start.HasValue && end.HasValue)
                {
                    var data = repo.GetDailyData(code, start.Value, end.Value);
                    count = data.Count;
                    totalRecords += count;
                    dateRanges.Add((code, start.Value, end.Value, count));
                }
            }
            if (stockCount > sampleSize)
            {
                long avgPerStock = sampleSize > 0 ? totalRecords / sampleSize : 0;
                totalRecords = avgPerStock * stockCount;
                sb.AppendLine($"日线总条数（估算）: 约 {totalRecords:N0}（基于前 {sampleSize} 只股票抽样）");
            }
            else
                sb.AppendLine($"日线总条数: {totalRecords:N0}");

            DateTime? globalLatest = repo.GetLatestTradeDate();
            sb.AppendLine($"全局最新交易日期: {(globalLatest.HasValue ? globalLatest.Value.ToString("yyyy-MM-dd") : "无")}");
            sb.AppendLine();

            sb.AppendLine("---------- 抽样：历史最长的 5 只股票（最早 3 条） ----------");
            var byStart = dateRanges.OrderBy(x => x.start).Take(5).ToList();
            foreach (var t in byStart)
            {
                var earliest = repo.GetEarliestDailyData(t.code, 3);
                sb.AppendLine($"  {t.code}  日期范围: {t.start:yyyy-MM-dd} ~ {t.end:yyyy-MM-dd}  共 {t.count} 条");
                foreach (var d in earliest)
                    sb.AppendLine($"    {d.TradeDate:yyyy-MM-dd}  O:{d.Open} H:{d.High} L:{d.Low} C:{d.Close} V:{d.Volume}");
            }

            sb.AppendLine();
            sb.AppendLine("---------- 抽样：最新日期的 2 只股票（最新 3 条） ----------");
            var byEnd = dateRanges.OrderByDescending(x => x.end).Take(2).ToList();
            foreach (var t in byEnd)
            {
                var latest = repo.GetLatestDailyData(t.code, 3);
                sb.AppendLine($"  {t.code}  最新 3 条:");
                foreach (var d in latest)
                    sb.AppendLine($"    {d.TradeDate:yyyy-MM-dd}  O:{d.Open} H:{d.High} L:{d.Low} C:{d.Close} V:{d.Volume}");
            }

            sb.AppendLine();
            sb.AppendLine("结论: RocksDB 日线数据存在，路径与抽样正常。");
            WriteReportAndExit(sb, null);
        }

        static void WriteReportAndExit(StringBuilder sb, string errorConclusion)
        {
            if (!string.IsNullOrEmpty(errorConclusion))
            {
                try { Console.WriteLine(errorConclusion); } catch { }
            }
            string baseDir = AppDomain.CurrentDomain.BaseDirectory ?? ".";
            string reportPath = Path.Combine(baseDir, "rocksdb_check_report.txt");
            try
            {
                File.WriteAllText(reportPath, sb.ToString(), Encoding.UTF8);
                Console.WriteLine($"报告已写入: {reportPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"写入报告失败: {ex.Message}");
                Console.WriteLine(sb.ToString());
            }
            Environment.Exit(string.IsNullOrEmpty(errorConclusion) ? 0 : 1);
        }
    }
}
