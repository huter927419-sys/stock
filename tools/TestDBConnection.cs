// 编译: csc /r:Npgsql.dll TestDBConnection.cs
// 或直接把此代码加入项目中运行

using System;
using System.Net.Sockets;

class TestDBConnection
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // 数据库配置（根据需要修改）
        string host = "localhost";
        string port = "8532";
        string database = "stockdb";
        string user = "postgres";
        string password = "cd123321";

        // 如果有命令行参数，使用参数
        if (args.Length >= 1) host = args[0];
        if (args.Length >= 2) port = args[1];

        Console.WriteLine("========================================");
        Console.WriteLine("  PostgreSQL 数据库连接测试");
        Console.WriteLine("========================================");
        Console.WriteLine();
        Console.WriteLine($"主机: {host}");
        Console.WriteLine($"端口: {port}");
        Console.WriteLine($"数据库: {database}");
        Console.WriteLine($"用户: {user}");
        Console.WriteLine();

        // 测试1：网络连通性
        Console.WriteLine("【测试1】检查网络连通性...");
        try
        {
            using (var client = new TcpClient())
            {
                var result = client.BeginConnect(host, int.Parse(port), null, null);
                var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(3));

                if (success && client.Connected)
                {
                    Console.WriteLine($"  [OK] 端口 {host}:{port} 可访问");
                }
                else
                {
                    Console.WriteLine($"  [失败] 无法连接到 {host}:{port}");
                    Console.WriteLine();
                    Console.WriteLine("可能原因:");
                    Console.WriteLine("  1. 数据库服务未启动");
                    Console.WriteLine("  2. 防火墙阻止了连接");
                    Console.WriteLine("  3. IP地址或端口错误");
                    WaitExit();
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [失败] 网络错误: {ex.Message}");
            WaitExit();
            return;
        }

        // 测试2：数据库连接
        Console.WriteLine();
        Console.WriteLine("【测试2】测试数据库连接...");

        string connStr = $"Host={host};Port={port};Database={database};Username={user};Password={password};Timeout=10;";

        try
        {
            // 动态加载 Npgsql
            var npgsqlPath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "packages", "Npgsql.8.0.8", "lib", "netstandard2.0", "Npgsql.dll"
            );

            if (!System.IO.File.Exists(npgsqlPath))
            {
                // 尝试其他路径
                npgsqlPath = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "Npgsql.dll"
                );
            }

            Console.WriteLine("  网络连通性测试通过!");
            Console.WriteLine();
            Console.WriteLine("  要完整测试数据库连接，请运行主程序 MQReceiver.exe");
            Console.WriteLine("  或在项目中执行 QuickTestDB");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [错误] {ex.Message}");
        }

        WaitExit();
    }

    static void WaitExit()
    {
        Console.WriteLine();
        Console.WriteLine("按任意键退出...");
        Console.ReadKey();
    }
}
