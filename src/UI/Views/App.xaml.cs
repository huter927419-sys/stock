using System;
using System.Windows;

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

            // 如果是从控制台启动，不显示主窗口
            // 主窗口由Program.cs控制
        }
    }
}
