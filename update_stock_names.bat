@echo off
chcp 65001 >nul
echo ========================================
echo 股票代码名称更新工具 (联网验证版)
echo ========================================
echo.
echo 本脚本将:
echo   1. 标记15个无效代码为 is_active=FALSE
echo   2. 更新200+常见A股的正确名称
echo   3. 确保 000851 (高鸿股份) 为有效代码
echo.
echo 按任意键开始更新... 或按 Ctrl+C 取消
pause >nul

echo.
echo 正在连接数据库...

set PGPASSWORD=123456

F:\dsfr\mqq\tools\bin\psql.exe -h localhost -p 8532 -U postgres -d stockdb -f db\update_stock_names_verified.sql

if %errorlevel% equ 0 (
    echo.
    echo ========================================
    echo ✓ 更新成功完成!
    echo ========================================
    echo.
    echo 验证结果:
    echo.
    
    :: 显示000851的状态
    echo 【关键股票验证】
    F:\dsfr\mqq\tools\bin\psql.exe -h localhost -p 8532 -U postgres -d stockdb -c "SELECT stock_code, stock_name, is_active FROM stock_info WHERE stock_code = '000851';"
    
    echo.
    echo 【无效代码统计】
    F:\dsfr\mqq\tools\bin\psql.exe -h localhost -p 8532 -U postgres -d stockdb -c "SELECT COUNT(*) as 无效代码数量 FROM stock_info WHERE is_active = FALSE;"
    
    echo.
    echo 【有效A股统计】
    F:\dsfr\mqq\tools\bin\psql.exe -h localhost -p 8532 -U postgres -d stockdb -c "SELECT COUNT(*) as 有效A股数量 FROM stock_info WHERE is_active = TRUE AND stock_code ~ '^[0-9]{6}$';"
    
) else (
    echo.
    echo ========================================
    echo ✗ 更新失败! 请检查错误信息
    echo ========================================
)

echo.
echo 按任意键退出...
pause >nul
