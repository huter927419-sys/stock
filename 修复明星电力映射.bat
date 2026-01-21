@echo off
chcp 65001 >nul
echo ╔════════════════════════════════════════════════╗
echo ║        修复明星电力代码-名称映射错误           ║
echo ╚════════════════════════════════════════════════╝
echo.
echo 问题：000101 被错误地映射为 "明星电力"
echo 正确：000101 应该是 "恒邦股份"（深圳）
echo       600101 应该是 "明星电力"（上海）
echo.
echo 即将执行修复...
echo.
pause

echo 正在执行修复脚本...
psql -h localhost -p 8532 -U postgres -d stockdb -f "db\fix_mingxing_dianli.sql"

if %errorlevel% equ 0 (
    echo.
    echo ✓ 修复成功！
    echo.
    echo 请重启应用程序以加载最新数据：
    echo   1. 关闭当前运行的 MQReceiver.exe
    echo   2. 重新启动程序
) else (
    echo.
    echo ✗ 修复失败，请检查错误信息
    echo.
    echo 您也可以在 pgAdmin 中手动执行：
    echo   db\fix_mingxing_dianli.sql
)

echo.
pause
