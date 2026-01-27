using System;
using System.Windows;
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
