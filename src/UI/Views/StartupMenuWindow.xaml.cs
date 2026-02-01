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
        private FilterMainWindow _mainProgramWindow;
        private DataMigrationWindow _dataMigrationWindow;

        public StartupMenuWindow()
        {
            try
            {
                InitializeComponent();
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
                case Key.D3:
                case Key.NumPad3:
                    StartDataMigration();
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

        /// <summary>
        /// 启动数据迁移工具
        /// </summary>
        private void DataMigration_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            StartDataMigration();
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
        /// 启动主程序（与 2、3 可同时各开一个；重复点击 [1] 则激活已打开的窗口）
        /// </summary>
        private void StartMainProgram()
        {
            if (_mainProgramWindow != null && _mainProgramWindow.IsLoaded)
            {
                _mainProgramWindow.Activate();
                Activate();
                return;
            }
            WindowHelper.CreateAndShowWindow(
                () => new FilterMainWindow(),
                "启动主程序失败",
                win => { _mainProgramWindow = win; win.Closed += (s, __) => _mainProgramWindow = null; Activate(); Focus(); }
            );
        }

        /// <summary>
        /// 启动 HaiLiDrv 数据窗口（独立模式，数据源为 MQ）
        /// </summary>
        private void StartHaiLiDrv()
        {
            WindowHelper.CreateAndShowWindow(
                () => new HaiLiDrvWindow(null, true),
                "启动 HaiLiDrv 数据窗口失败",
                win => { win.Closed += (s, __) => Activate(); Focus(); }
            );
        }

        /// <summary>
        /// 启动数据迁移工具（与 1、2 可同时各开一个；重复点击 [3] 则激活已打开的窗口）
        /// </summary>
        private void StartDataMigration()
        {
            if (_dataMigrationWindow != null && _dataMigrationWindow.IsLoaded)
            {
                _dataMigrationWindow.Activate();
                Activate();
                return;
            }
            WindowHelper.CreateAndShowWindow(
                () => new DataMigrationWindow(),
                "启动数据迁移工具失败",
                win => { _dataMigrationWindow = win; win.Closed += (s, __) => _dataMigrationWindow = null; Activate(); Focus(); }
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
