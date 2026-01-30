using System;
using System.Windows;

namespace MQReceiver.Helpers
{
    /// <summary>
    /// 窗口辅助类
    /// 提供窗口创建、显示、错误处理等通用功能
    /// </summary>
    public static class WindowHelper
    {
        /// <summary>
        /// 安全地创建并显示窗口，带错误处理
        /// </summary>
        /// <typeparam name="T">窗口类型</typeparam>
        /// <param name="windowFactory">窗口创建工厂方法</param>
        /// <param name="errorTitle">错误标题</param>
        /// <param name="onSuccess">成功创建窗口后的回调（可选）</param>
        /// <returns>创建的窗口实例，失败返回null</returns>
        public static T CreateAndShowWindow<T>(
            Func<T> windowFactory,
            string errorTitle = "启动失败",
            Action<T> onSuccess = null) where T : Window
        {
            try
            {
                var window = windowFactory();
                if (window != null)
                {
                    window.Show();
                    window.Activate();
                    window.Focus();
                    
                    onSuccess?.Invoke(window);
                    return window;
                }
            }
            catch (Exception ex)
            {
                ShowErrorDialog(errorTitle, $"创建窗口失败: {ex.Message}\n\n堆栈跟踪:\n{ex.StackTrace}");
            }
            return null;
        }

        /// <summary>
        /// 显示错误对话框
        /// </summary>
        public static void ShowErrorDialog(string title, string message)
        {
            try
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                // 如果MessageBox也失败，输出到Debug
                System.Diagnostics.Debug.WriteLine($"[{title}] {message}");
            }
        }

        /// <summary>
        /// 确保窗口可见并激活
        /// </summary>
        public static void EnsureWindowVisible(Window window)
        {
            if (window == null) return;

            try
            {
                window.Visibility = Visibility.Visible;
                window.WindowState = WindowState.Normal;
                window.Activate();
                window.Focus();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WindowHelper] 确保窗口可见失败: {ex.Message}");
            }
        }
    }
}
