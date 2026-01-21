@echo off
chcp 65001 > nul
echo ============================================
echo 捕获图表加载日志
echo ============================================
echo.
echo 正在启动程序并捕获日志...
echo 日志将保存到: chart_log.txt
echo.
echo 请按以下步骤操作:
echo 1. 程序启动后，点击任意股票代码
echo 2. 观察图表窗口是否正确显示
echo 3. 关闭程序后查看 chart_log.txt
echo.
echo 按任意键开始...
pause > nul

cd /d "%~dp0bin\Release"
if not exist "MQReceiver.exe" (
    echo 错误: 找不到 MQReceiver.exe
    echo 请先编译项目
    pause
    exit /b 1
)

echo.
echo ============================================
echo 正在启动程序（日志输出到控制台）...
echo ============================================
echo.

MQReceiver.exe > ..\..\chart_log.txt 2>&1

echo.
echo ============================================
echo 程序已退出
echo 日志已保存到: chart_log.txt
echo ============================================
echo.
echo 按任意键查看日志...
pause > nul

type ..\..\chart_log.txt

pause
