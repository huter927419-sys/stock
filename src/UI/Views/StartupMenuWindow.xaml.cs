using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MQReceiver.Services;
using MQReceiver.Helpers;

namespace MQReceiver.Views
{
    /// <summary>
    /// StartupMenuWindow.xaml 的交互逻辑
    /// 可视化启动菜单窗口
    /// </summary>
    public partial class StartupMenuWindow : Window
    {
        public StartupMenuWindow()
        {
                    try
            {
        .        InitializeComponent();
                this.KeyDown += StartupMenuWindow_KeyDown;
                this.Focusable = true;
                this.Focus();
                
                // 确保窗口可见
                this.Visibility = Visibility.Visible;
                this.WindowState = WindowState.Normal;
                
                System.Diagnostics.Debug.WriteLine("[StartupMenuWindow] 窗口已初始化");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StartupMenuWindow] 初始化失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[StartupMenuWindow] 堆栈跟踪: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// 键盘快捷键处理
        /// </summary>
        private void StartupMenuWindow_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.D1:
                case Key.NumPad1:
                    StartMainProgram();
                    break;
                case Key.D2:
                case Key.NumPad2:
                    StartHaiLiDrv();
                    break;
                // MQ服务已移除，主程序启动时自动启动
                // case Key.D3:
                // case Key.NumPad3:
                //     StartMQService();
                //     break;
                case Key.D0:
                case Key.NumPad0:
                    Exit();
                    break;
                case Key.Escape:
                    Exit();
                    break;
            }
        }

        /// <summary>
        /// 菜单项鼠标悬停效果
        /// </summary>
        private void MenuOption_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border)
            {
                border.Background = new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x42));
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x9E, 0xFF));
            }
        }

        /// <summary>
        /// 菜单项鼠标离开效果
        /// </summary>
        private void MenuOption_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border)
            {
                border.Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30));
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46));
            }
        }

        /// <summary>
        /// 启动主程序
        /// </summary>
        private void MainProgram_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            StartMainProgram();
        }

        /// <summary>
        /// 启动HaiLiDrv窗口
        /// </summary>
        private void HaiLiDrv_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            StartHaiLiDrv();
        }

        // MQ服务已移除，主程序启动时自动启动
        // /// <summary>
        // /// 启动MQ服务
        // /// </summary>
        // private void MQService_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        // {
        //     StartMQService();
        // }

        /// <summary>
        /// 退出
        /// </summary>
        private void Exit_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Exit();
        }

        /// <summary>
        /// 启动主程序
        /// </summary>
        private void StartMainProgram()
        {
            // 使用WindowHelper统一处理窗口创建和错误处理
            WindowHelper.CreateAndShowWindow(
                () => new FilterMainWindow(),
                "启动主程序失败"
            );
        }

        /// <summary>
        /// 启动HaiLiDrv独立窗口
        /// </summary>
        private void StartHaiLiDrv()
        {
            // 使用CacheManager创建独立缓存
            var cache = CacheManager.CreateStandaloneCache();
            
            // 使用WindowHelper统一处理窗口创建和错误处理
            WindowHelper.CreateAndShowWindow(
                () => new HaiLiDrvWindow(cache, isStandaloneMode: true),
                "启动HaiLiDrv窗口失败",
                (window) =>
                {
                    // 窗口关闭时释放缓存资源
                    window.Closed += (s, e) => 
                    { 
                        CacheManager.ReleaseStandaloneCache(cache);
                    };
                }
            );
        }

        // MQ服务已移除，主程序启动时自动启动
        // /// <summary>
        // /// 启动MQ服务（控制台模式）
        // /// </summary>
        // private void StartMQService()
        // {
        //     try
        //     {
        //         // 直接启动MQ服务（控制台模式）
        //         // 注意：这会在控制台运行，如果没有控制台会失败
        //         System.Diagnostics.Process.Start(System.Reflection.Assembly.GetExecutingAssembly().Location, "--mq-service");
        //     }
        //     catch (Exception ex)
        //     {
        //         WindowHelper.ShowErrorDialog(
        //             "启动MQ服务失败",
        //             $"{ex.Message}\n\n提示：MQ服务需要在控制台模式下运行\n\n堆栈跟踪:\n{ex.StackTrace}"
        //         );
        //     }
        // }

        /// <summary>
        /// 退出程序
        /// </summary>
        private void Exit()
        {
            this.Close();
        }
    }
}
