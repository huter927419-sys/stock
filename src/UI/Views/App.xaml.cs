using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using MQReceiver.Helpers;

namespace MQReceiver.Views
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 全局未处理异常：记录 IOException 等，便于定位
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                if (ex != null)
                {
                    try
                    {
                        string msg = ex is IOException ? $"[未处理 IO 异常] {ex.Message}" : $"[未处理异常] {ex.Message}";
                        System.Diagnostics.Debug.WriteLine(msg);
                        System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                        if (ex.InnerException != null)
                            System.Diagnostics.Debug.WriteLine($"内部: {ex.InnerException.Message}");
                    }
                    catch { }
                }
            };
            DispatcherUnhandledException += (sender, args) =>
            {
                var ex = args.Exception;
                try
                {
                    string msg = ex is IOException ? $"[未处理 IO 异常] {ex.Message}" : $"[未处理异常] {ex.Message}";
                    System.Diagnostics.Debug.WriteLine(msg);
                    System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                }
                catch { }
                args.Handled = false;
            };

            // 仅执行数据库初始化（含 ADD COLUMN turnover_rate 等）后退出，便于脚本/迁移
            if (e.Args != null && e.Args.Length > 0 && e.Args[0] == "--db-init-only")
            {
                try
                {
                    DatabaseInitializer.Initialize();
                    System.Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("DatabaseInitializer failed: " + ex.Message);
                    System.Environment.Exit(1);
                }
                return;
            }

            // 如果是从控制台启动，不显示主窗口
            // 主窗口由Program.cs控制
        }
    }
}
