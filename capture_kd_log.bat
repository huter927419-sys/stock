@echo off
chcp 65001 >nul
echo ========================================
echo KD计算日志捕获工具
echo ========================================
echo.
echo 此脚本将运行程序并捕获所有输出到日志文件
echo.
echo 日志文件将保存为: kd_log_%date:~0,4%%date:~5,2%%date:~8,2%_%time:~0,2%%time:~3,2%%time:~6,2%.txt
echo.
echo 按任意键开始运行程序...
pause >nul

set "logfile=kd_log_%date:~0,4%%date:~5,2%%date:~8,2%_%time:~0,2%%time:~3,2%%time:~6,2%.txt"
set "logfile=%logfile: =0%"

echo.
echo 正在运行程序，输出将保存到: %logfile%
echo 提示: 打开图表窗口（特别是股票代码000001）会输出详细的KD调试信息
echo 按Ctrl+C停止程序
echo.

cd /d "%~dp0"
if exist "bin\Release\MQReceiver.exe" (
    bin\Release\MQReceiver.exe > "%logfile%" 2>&1
) else if exist "bin\Debug\MQReceiver.exe" (
    bin\Debug\MQReceiver.exe > "%logfile%" 2>&1
) else (
    echo 错误: 找不到可执行文件
    echo 请确保程序已编译
    pause
    exit /b 1
)

echo.
echo 程序已停止
echo 日志文件: %logfile%
echo.
echo 现在可以运行分析脚本:
echo   .\analyze_kd_log.ps1 -LogFile "%logfile%"
echo.
pause
