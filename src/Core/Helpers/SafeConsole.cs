using System;
using System.IO;

namespace MQReceiver.Helpers
{
    /// <summary>
    /// 安全的控制台操作辅助类
    /// 当控制台不可用时（如WPF应用程序），静默处理异常
    /// </summary>
    public static class SafeConsole
    {
        private static bool? _isConsoleAvailable = null;
        private static readonly object _lockObject = new object();

        /// <summary>
        /// 检查控制台是否可用
        /// </summary>
        public static bool IsConsoleAvailable
        {
            get
            {
                if (_isConsoleAvailable.HasValue)
                    return _isConsoleAvailable.Value;

                lock (_lockObject)
                {
                    if (_isConsoleAvailable.HasValue)
                        return _isConsoleAvailable.Value;

                    try
                    {
                        // 尝试访问控制台属性来检测是否可用
                        int _ = Console.WindowWidth;
                        _isConsoleAvailable = true;
                    }
                    catch
                    {
                        _isConsoleAvailable = false;
                    }
                    return _isConsoleAvailable.Value;
                }
            }
        }

        /// <summary>
        /// 安全清屏
        /// </summary>
        public static void Clear()
        {
            if (!IsConsoleAvailable) return;

            try
            {
                Console.Clear();
            }
            catch (IOException)
            {
                // 忽略IO异常
            }
        }

        /// <summary>
        /// 安全写入一行
        /// </summary>
        public static void WriteLine(string value = "")
        {
            try
            {
                Console.WriteLine(value);
            }
            catch (IOException)
            {
                // 忽略IO异常
            }
        }

        /// <summary>
        /// 安全写入（带格式化）
        /// </summary>
        public static void WriteLine(string format, params object[] args)
        {
            try
            {
                Console.WriteLine(format, args);
            }
            catch (IOException)
            {
                // 忽略IO异常
            }
        }

        /// <summary>
        /// 安全写入
        /// </summary>
        public static void Write(string value)
        {
            try
            {
                Console.Write(value);
            }
            catch (IOException)
            {
                // 忽略IO异常
            }
        }

        /// <summary>
        /// 安全读取一行
        /// </summary>
        public static string ReadLine()
        {
            if (!IsConsoleAvailable) return null;

            try
            {
                return Console.ReadLine();
            }
            catch (IOException)
            {
                return null;
            }
        }

        /// <summary>
        /// 安全读取按键
        /// </summary>
        public static ConsoleKeyInfo? ReadKey(bool intercept = false)
        {
            if (!IsConsoleAvailable) return null;

            try
            {
                return Console.ReadKey(intercept);
            }
            catch (IOException)
            {
                return null;
            }
        }

        /// <summary>
        /// 检查是否有可用的按键
        /// </summary>
        public static bool KeyAvailable
        {
            get
            {
                if (!IsConsoleAvailable) return false;

                try
                {
                    return Console.KeyAvailable;
                }
                catch (IOException)
                {
                    return false;
                }
            }
        }
    }
}
