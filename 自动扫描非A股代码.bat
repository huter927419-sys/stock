@echo off
chcp 65001 >nul
cls
echo.
echo ╔════════════════════════════════════════════════════════╗
echo ║          自动扫描数据库中的非A股代码                   ║
echo ╚════════════════════════════════════════════════════════╝
echo.
echo 正在扫描数据库，查找以下类型的代码：
echo   • 指数代码（000000-000199）
echo   • 名称包含"指数"、"债"、"基金"的代码
echo   • B股代码（200xxx, 900xxx）
echo   • 已退市股票
echo.
echo 按任意键开始扫描...
pause >nul

echo.
echo ═══════════════════════════════════════════════════════
echo 正在执行扫描...
echo ═══════════════════════════════════════════════════════
echo.

psql -h localhost -p 8532 -U postgres -d stockdb -f "db\find_all_non_astock_codes.sql" > "扫描结果.txt" 2>&1

if %errorlevel% equ 0 (
    echo.
    echo ✓ 扫描完成！结果已保存到：扫描结果.txt
    echo.
    echo 正在显示结果...
    echo.
    type "扫描结果.txt"
    echo.
    echo ═══════════════════════════════════════════════════════
    echo 完整结果请查看：扫描结果.txt
    echo ═══════════════════════════════════════════════════════
) else (
    echo.
    echo ✗ 扫描失败！
    echo.
    echo 请确认：
    echo   1. PostgreSQL服务正在运行
    echo   2. 数据库连接信息正确
    echo   3. psql.exe 在系统PATH中
    echo.
    echo 或者直接在 pgAdmin 中打开并执行：
    echo   db\find_all_non_astock_codes.sql
)

echo.
pause
