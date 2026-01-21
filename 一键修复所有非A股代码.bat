@echo off
chcp 65001 >nul
cls
echo.
echo ╔════════════════════════════════════════════════════════╗
echo ║          一键修复所有非A股代码问题                     ║
echo ╚════════════════════════════════════════════════════════╝
echo.
echo 此脚本将：
echo   1. 在数据库中停用所有指数、债券、基金代码
echo   2. 重新编译应用程序（更新黑名单）
echo   3. 重启应用程序
echo.
echo 受影响的代码类型：
echo   ✓ 指数代码（如 000001上证指数, 000101债券指数）
echo   ✓ 债券代码
echo   ✓ 基金/ETF代码
echo   ✓ B股代码
echo   ✓ 已退市股票
echo.
pause

echo.
echo ═══════════════════════════════════════════════════════
echo [步骤 1/3] 停用数据库中的非A股代码...
echo ═══════════════════════════════════════════════════════
psql -h localhost -p 8532 -U postgres -d stockdb -f "db\batch_disable_non_astock.sql"

if %errorlevel% neq 0 (
    echo.
    echo ✗ 数据库更新失败！
    echo.
    pause
    exit /b 1
)

echo.
echo ═══════════════════════════════════════════════════════
echo [步骤 2/3] 重新编译应用程序...
echo ═══════════════════════════════════════════════════════
msbuild MQReceiver.sln /p:Configuration=Release /t:Rebuild /v:minimal

if %errorlevel% neq 0 (
    echo.
    echo ✗ 编译失败！
    echo.
    pause
    exit /b 1
)

echo.
echo ═══════════════════════════════════════════════════════
echo [步骤 3/3] 重启应用程序...
echo ═══════════════════════════════════════════════════════
taskkill /F /IM MQReceiver.exe 2>nul
timeout /t 2 /nobreak >nul

echo 启动新版本...
start "" "bin\Release\MQReceiver.exe"
timeout /t 3 /nobreak >nul

echo.
echo ╔════════════════════════════════════════════════════════╗
echo ║                    ✓ 修复完成！                        ║
echo ╚════════════════════════════════════════════════════════╝
echo.
echo 已完成的操作：
echo   ✓ 数据库中的非A股代码已停用
echo   ✓ 应用程序黑名单已更新
echo   ✓ 应用程序已重启
echo.
echo 现在过滤结果中应该不再出现：
echo   • 000101 (上证5年期信用债指数)
echo   • 其他指数、债券、基金代码
echo.
echo 请运行一次过滤操作验证结果！
echo.
pause
