using System;
using System.Windows;
using MQReceiver.Helpers;
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
            // 检查控制台是否可用
            if (!SafeConsole.IsConsoleAvailable)
            {
                // 没有控制台时，直接启动KD过滤器服务（WPF模式）
                StartFilterService();
                return;
            }

            // 显示启动菜单
            ShowMainMenu();

            // 处理用户选择
            while (true)
            {
                SafeConsole.Write("请输入选项 [0-3]: ");
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
                    SafeConsole.WriteLine("无效的输入，请输入数字 0-3");
                    SafeConsole.WriteLine();
                    continue;
                }

                switch (choice)
                {
                    case 1:
                        StartMQService();
                        break;
                    case 2:
                        StartPreloadService();
                        break;
                    case 3:
                        StartFilterService();
                        break;
                    case 0:
                        Exit();
                        return;
                    default:
                        SafeConsole.WriteLine("无效的选项，请输入 0-3");
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
            SafeConsole.WriteLine("  [2] 预加载数据到Redis");
            SafeConsole.WriteLine("      - 加载历史KD数据");
            SafeConsole.WriteLine("      - 提升过滤性能");
            SafeConsole.WriteLine();
            SafeConsole.WriteLine("  [3] 启动KD过滤器");
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
        /// 启动数据预加载服务
        /// </summary>
        static void StartPreloadService()
        {
            SafeConsole.Clear();
            DataPreloadService service = new DataPreloadService();
            service.Start();
        }

        /// <summary>
        /// 启动主界面
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
    }
}
