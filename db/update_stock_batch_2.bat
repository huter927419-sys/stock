@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo ============================================
echo 批量更新股票信息（第二批）
echo ============================================

set PGPASSWORD=123456
set PSQL="F:\dsfr\mqq\tools\bin\psql.exe"
set DB_HOST=localhost
set DB_PORT=8532
set DB_USER=postgres
set DB_NAME=stockdb

echo.
echo 正在更新股票信息...
%PSQL% -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -d %DB_NAME% -f db/update_stock_batch_2.sql

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ============================================
    echo ✅ 更新成功！
    echo ============================================
) else (
    echo.
    echo ============================================
    echo ❌ 更新失败，错误代码: %ERRORLEVEL%
    echo ============================================
)

endlocal
