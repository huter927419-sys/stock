using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using MQReceiver.Tools;
using MQReceiver.DataProcessing.Factories;
using MQReceiver.DataProcessing.Repositories;
using MQReceiver.Repositories;

namespace MQReceiver.Views
{
    /// <summary>
    /// DataMigrationWindow.xaml 的交互逻辑
    /// </summary>
    public partial class DataMigrationWindow : Window
    {
        private bool _isMigrating = false;
        private DataMigrationTool _migrationTool;

        public DataMigrationWindow()
        {
            InitializeComponent();
            LogMessage("数据迁移工具已就绪");
            LogMessage("说明：此工具将 PostgreSQL 数据库中的所有数据迁移到 RocksDB 文件系统存储");
            LogMessage("迁移完成后，系统将切换到 RocksDB 存储后端，后续数据将只写入 RocksDB");
            LogMessage("========================================\n");
        }

        private void LogMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                TxtLog.ScrollToEnd();
            });
        }

        private void UpdateProgress(string message)
        {
            Dispatcher.Invoke(() =>
            {
                TxtProgress.Text = message;
            });
        }

        private void SetBusyState(bool isBusy)
        {
            Dispatcher.Invoke(() =>
            {
                _isMigrating = isBusy;
                BtnStartMigration.IsEnabled = !isBusy;
                BtnTestConnection.IsEnabled = !isBusy;
                BtnViewEarliest.IsEnabled = !isBusy;
                BtnQueryOneEarliest.IsEnabled = !isBusy;
                TxtQueryStockCode.IsEnabled = !isBusy;
                BtnBrowse.IsEnabled = !isBusy;
                TxtRocksDbPath.IsEnabled = !isBusy;
                ChkSkipRealTime.IsEnabled = !isBusy;
                ChkSkipLogs.IsEnabled = !isBusy;
                ChkVerify.IsEnabled = !isBusy;

                if (isBusy)
                {
                    ProgressBar.Visibility = Visibility.Visible;
                    ProgressBar.IsIndeterminate = true;
                }
                else
                {
                    ProgressBar.Visibility = Visibility.Collapsed;
                    ProgressBar.IsIndeterminate = false;
                }
            });
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择 RocksDB 数据目录",
                SelectedPath = TxtRocksDbPath.Text
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TxtRocksDbPath.Text = dialog.SelectedPath;
                LogMessage($"RocksDB 路径已设置为: {dialog.SelectedPath}");
            }
        }

        private async void BtnTestConnection_Click(object sender, RoutedEventArgs e)
        {
            if (_isMigrating)
                return;

            SetBusyState(true);
            UpdateProgress("正在测试连接...");
            LogMessage("========================================");
            LogMessage("测试数据库连接...");

            await Task.Run(() =>
            {
                try
                {
                    string rocksDbPath = "";
                    Dispatcher.Invoke(() => rocksDbPath = TxtRocksDbPath.Text);

                    _migrationTool = new DataMigrationTool(null, rocksDbPath);

                    // 测试 PostgreSQL
                    LogMessage("正在测试 PostgreSQL 连接...");
                    var pgStockRepo = new MQReceiver.Repositories.PostgresStockDataRepository();
                    bool pgConnected = pgStockRepo.TestConnection();

                    Dispatcher.Invoke(() =>
                    {
                        if (pgConnected)
                        {
                            PgStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(34, 197, 94)); // 绿色
                            TxtPgStatus.Text = "已连接";
                            TxtPgStatus.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                            LogMessage("✓ PostgreSQL 连接成功");
                        }
                        else
                        {
                            PgStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // 红色
                            TxtPgStatus.Text = "连接失败";
                            TxtPgStatus.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                            LogMessage("❌ PostgreSQL 连接失败");
                        }
                    });

                    // 测试 RocksDB
                    LogMessage("正在测试 RocksDB 初始化...");
                    var rocksStockRepo = new MQReceiver.DataProcessing.Repositories.RocksDBStockDataRepository(rocksDbPath);
                    bool rocksConnected = rocksStockRepo.TestConnection();

                    Dispatcher.Invoke(() =>
                    {
                        if (rocksConnected)
                        {
                            RocksStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                            TxtRocksStatus.Text = "已就绪";
                            TxtRocksStatus.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                            LogMessage("✓ RocksDB 初始化成功");
                        }
                        else
                        {
                            RocksStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                            TxtRocksStatus.Text = "初始化失败";
                            TxtRocksStatus.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                            LogMessage("❌ RocksDB 初始化失败");
                        }
                    });

                    if (pgConnected && rocksConnected)
                    {
                        LogMessage("✓ 所有连接测试通过，可以开始迁移");
                        UpdateProgress("连接测试成功");
                    }
                    else
                    {
                        LogMessage("⚠️ 连接测试未全部通过，请检查配置");
                        UpdateProgress("连接测试失败");
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"❌ 连接测试失败: {ex.Message}");
                    UpdateProgress("连接测试失败");
                }
                finally
                {
                    LogMessage("========================================\n");
                }
            });

            SetBusyState(false);
        }

        private async void BtnStartMigration_Click(object sender, RoutedEventArgs e)
        {
            if (_isMigrating)
                return;

            var result = MessageBox.Show(
                "确认要开始数据迁移吗？\n\n" +
                "迁移过程可能需要较长时间，请耐心等待。\n" +
                "迁移完成后，系统将自动切换到 RocksDB 存储后端。\n\n" +
                "注意：迁移过程中请勿关闭此窗口！",
                "确认迁移",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            SetBusyState(true);
            UpdateProgress("正在迁移数据...");

            bool skipRealTime = ChkSkipRealTime.IsChecked ?? false;
            bool skipLogs = ChkSkipLogs.IsChecked ?? false;
            bool verify = ChkVerify.IsChecked ?? true;
            string rocksDbPath = TxtRocksDbPath.Text;

            await Task.Run(() =>
            {
                try
                {
                    LogMessage("========================================");
                    LogMessage("开始数据迁移...");
                    LogMessage($"目标路径: {rocksDbPath}");
                    LogMessage($"跳过实时数据: {skipRealTime}");
                    LogMessage($"跳过日志数据: {skipLogs}");
                    LogMessage($"验证结果: {verify}");
                    LogMessage("========================================\n");

                    var startTime = DateTime.Now;

                    // 创建迁移工具并设置日志回调
                    _migrationTool = new DataMigrationTool(null, rocksDbPath);

                    // 重定向 Console 输出到窗口日志
                    var originalConsoleOut = Console.Out;
                    using (var writer = new StringWriter())
                    {
                        Console.SetOut(writer);

                        // 执行迁移
                        bool success = _migrationTool.MigrateAll(skipRealTime, skipLogs);

                        // 获取控制台输出并显示
                        var consoleOutput = writer.ToString();
                        foreach (var line in consoleOutput.Split('\n'))
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                LogMessage(line.Trim());
                            }
                        }

                        Console.SetOut(originalConsoleOut);

                        var duration = DateTime.Now - startTime;

                        if (success)
                        {
                            LogMessage($"\n✓ 迁移成功完成！耗时: {duration.TotalSeconds:F2} 秒");

                            // 验证迁移结果
                            if (verify)
                            {
                                LogMessage("\n正在验证迁移结果...");
                                Console.SetOut(writer);
                                _migrationTool.VerifyMigration();
                                Console.SetOut(originalConsoleOut);

                                var verifyOutput = writer.ToString();
                                foreach (var line in verifyOutput.Split('\n'))
                                {
                                    if (!string.IsNullOrWhiteSpace(line))
                                    {
                                        LogMessage(line.Trim());
                                    }
                                }
                            }

                            LogMessage("\n========================================");
                            LogMessage("正在切换到 RocksDB 存储后端...");

                            // 切换到 RocksDB
                            Dispatcher.Invoke(() =>
                            {
                                RepositoryFactory.Configure(
                                    RepositoryFactory.StorageBackend.RocksDB,
                                    dbPath: rocksDbPath
                                );
                            });

                            LogMessage("✓ 已切换到 RocksDB 存储后端");
                            LogMessage("后续数据将只写入 RocksDB，不再写入 PostgreSQL");
                            LogMessage("========================================");

                            UpdateProgress("迁移完成！");

                            Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show(
                                    $"数据迁移成功完成！\n\n" +
                                    $"耗时: {duration.TotalSeconds:F2} 秒\n" +
                                    $"数据路径: {rocksDbPath}\n\n" +
                                    $"系统已切换到 RocksDB 存储后端。\n" +
                                    $"后续数据将只写入 RocksDB。",
                                    "迁移成功",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                            });
                        }
                        else
                        {
                            LogMessage("\n❌ 迁移失败，请检查错误信息");
                            UpdateProgress("迁移失败");

                            Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show(
                                    "数据迁移失败！\n\n请查看日志了解详细错误信息。",
                                    "迁移失败",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"\n❌ 迁移过程中发生异常: {ex.Message}");
                    LogMessage($"堆栈跟踪:\n{ex.StackTrace}");
                    UpdateProgress("迁移失败");

                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(
                            $"迁移过程中发生错误：\n\n{ex.Message}",
                            "错误",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    });
                }
            });

            SetBusyState(false);
        }

        private async void BtnViewEarliest_Click(object sender, RoutedEventArgs e)
        {
            if (_isMigrating)
                return;

            string rocksDbPath = "";
            Dispatcher.Invoke(() => rocksDbPath = TxtRocksDbPath.Text?.Trim());
            if (string.IsNullOrEmpty(rocksDbPath))
            {
                LogMessage("请先填写 RocksDB 路径");
                return;
            }

            SetBusyState(true);
            UpdateProgress("正在统计历史最长股票并读取最早数据...");
            LogMessage("========================================");
            LogMessage("查看历史最长股票的最早数据...");

            await Task.Run(() =>
            {
                try
                {
                    var repo = new RocksDBStockDataRepository(rocksDbPath);
                    var codes = repo.GetAllStockCodes();
                    if (codes == null || codes.Count == 0)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            LogMessage("RocksDB 中暂无股票数据，请先完成迁移。");
                            UpdateProgress("就绪");
                        });
                        return;
                    }

                    var ranges = new List<(string Code, DateTime Start, DateTime End, int Count)>();
                    foreach (var code in codes)
                    {
                        var (start, end) = repo.GetDataDateRange(code);
                        if (start.HasValue && end.HasValue)
                        {
                            var data = repo.GetDailyData(code, start.Value, end.Value);
                            ranges.Add((code, start.Value, end.Value, data?.Count ?? 0));
                        }
                    }

                    var byEarliest = ranges.OrderBy(x => x.Start).ThenByDescending(x => x.Count).Take(15).ToList();
                    const int earliestRows = 20;
                    var reportPath = Path.Combine(rocksDbPath, "earliest_data_report.txt");
                    var lines = new List<string>
                    {
                        "========================================",
                        "历史最长股票的最早日线数据（RocksDB）",
                        $"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                        $"数据路径: {rocksDbPath}",
                        $"展示: 按首笔日期最早取前 15 只，每只取最早 {earliestRows} 条",
                        "========================================",
                        ""
                    };

                    var names = repo.GetAllStockNames();
                    foreach (var (code, start, end, count) in byEarliest)
                    {
                        var name = names != null && names.TryGetValue(code, out var n) ? n : code;
                        lines.Add($"【{code} {name}】 首笔: {start:yyyy-MM-dd} 末笔: {end:yyyy-MM-dd} 共 {count} 条");
                        lines.Add("");
                        var earliest = repo.GetEarliestDailyData(code, earliestRows);
                        foreach (var row in earliest)
                        {
                            var amountStr = row.Amount.HasValue ? $"{row.Amount.Value / 10000m:F0}万" : "-";
                            lines.Add($"  {row.TradeDate:yyyy-MM-dd}  O:{row.Open} H:{row.High} L:{row.Low} C:{row.Close} V:{row.Volume} 金额:{amountStr}");
                        }
                        lines.Add("");
                    }

                    File.WriteAllText(reportPath, string.Join(Environment.NewLine, lines));

                    Dispatcher.Invoke(() =>
                    {
                        LogMessage($"已生成报告，共 {byEarliest.Count} 只历史最长股票，每只最早 {earliestRows} 条。");
                        LogMessage($"报告文件: {reportPath}");
                        UpdateProgress("就绪");
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = reportPath,
                                UseShellExecute = true
                            });
                        }
                        catch
                        {
                            // 忽略打开失败
                        }
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        LogMessage($"查看最早数据失败: {ex.Message}");
                        UpdateProgress("就绪");
                        MessageBox.Show($"生成报告失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
            });

            SetBusyState(false);
        }

        private async void BtnQueryOneEarliest_Click(object sender, RoutedEventArgs e)
        {
            if (_isMigrating)
                return;

            string rocksDbPath = "";
            string code = "";
            Dispatcher.Invoke(() =>
            {
                rocksDbPath = TxtRocksDbPath.Text?.Trim();
                code = TxtQueryStockCode.Text?.Trim();
            });
            if (string.IsNullOrEmpty(rocksDbPath))
            {
                LogMessage("请先填写 RocksDB 路径");
                return;
            }
            if (string.IsNullOrEmpty(code))
            {
                LogMessage("请填写要查询的股票代码");
                return;
            }

            SetBusyState(true);
            UpdateProgress($"正在查询 {code} 最早数据...");
            LogMessage($"========================================");
            LogMessage($"查询股票 {code} 的最早日线数据");

            await Task.Run(() =>
            {
                try
                {
                    var repo = new RocksDBStockDataRepository(rocksDbPath);
                    var (start, end) = repo.GetDataDateRange(code);
                    if (!start.HasValue || !end.HasValue)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            LogMessage($"未找到 {code} 的数据，请确认 RocksDB 路径下已有该股票。");
                            UpdateProgress("就绪");
                        });
                        return;
                    }

                    var earliest = repo.GetEarliestDailyData(code, 30);
                    var names = repo.GetAllStockNames();
                    var name = (names != null && names.TryGetValue(code, out var n)) ? n : code;

                    Dispatcher.Invoke(() =>
                    {
                        LogMessage($"【{code} {name}】 首笔: {start:yyyy-MM-dd} 末笔: {end:yyyy-MM-dd}");
                        LogMessage("");
                        foreach (var row in earliest)
                        {
                            var amountStr = row.Amount.HasValue ? $"{row.Amount.Value / 10000m:F0}万" : "-";
                            LogMessage($"  {row.TradeDate:yyyy-MM-dd}  O:{row.Open} H:{row.High} L:{row.Low} C:{row.Close} V:{row.Volume} 金额:{amountStr}");
                        }
                        LogMessage($"共 {earliest.Count} 条（最早 30 条）");
                        LogMessage("========================================");
                        UpdateProgress("就绪");
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        LogMessage($"查询失败: {ex.Message}");
                        UpdateProgress("就绪");
                        MessageBox.Show($"查询失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
            });

            SetBusyState(false);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (_isMigrating)
            {
                var result = MessageBox.Show(
                    "迁移正在进行中，确定要关闭窗口吗？\n\n关闭窗口可能导致数据不完整！",
                    "警告",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            Close();
        }
    }
}
