@echo off
chcp 65001 >nul
echo ========================================
echo   导出数据库表数据 (UTF-8编码)
echo ========================================
echo.

:: 数据库配置
set DB_HOST=localhost
set DB_PORT=8532
set DB_NAME=stockdb
set DB_USER=postgres
set DB_PASSWORD=cd123321

:: 输出目录
set OUTPUT_DIR=f:\dsfr\mqq\db

:: 设置环境变量
set PGPASSWORD=%DB_PASSWORD%
set PGCLIENTENCODING=UTF8

:: psql 路径
set PSQL_PATH=F:\dsfr\mqq\tools\bin\psql.exe
set PGDUMP_PATH=F:\dsfr\mqq\tools\bin\pg_dump.exe

if not exist "%PSQL_PATH%" (
    echo [错误] 找不到 psql.exe: %PSQL_PATH%
    goto :end
)

echo 导出目录: %OUTPUT_DIR%
echo.

:: 导出 stock_info 为 CSV
echo [1/4] 导出 stock_info (CSV)...
"%PSQL_PATH%" -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -d %DB_NAME% -c "\COPY stock_info TO '%OUTPUT_DIR%/stock_info_data.csv' WITH CSV HEADER ENCODING 'UTF8'"
if %errorlevel%==0 (
    echo   [OK] stock_info_data.csv
) else (
    echo   [失败]
)

:: 导出 stock_exrights_data 为 CSV
echo [2/4] 导出 stock_exrights_data (CSV)...
"%PSQL_PATH%" -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -d %DB_NAME% -c "\COPY stock_exrights_data TO '%OUTPUT_DIR%/stock_exrights_data.csv' WITH CSV HEADER ENCODING 'UTF8'"
if %errorlevel%==0 (
    echo   [OK] stock_exrights_data.csv
) else (
    echo   [失败]
)

:: 检查是否有 pg_dump
if not exist "%PGDUMP_PATH%" (
    echo.
    echo [跳过] pg_dump.exe 不存在，跳过SQL格式导出
    goto :done
)

:: 导出为 SQL INSERT 语句
echo [3/4] 导出 stock_info (SQL)...
"%PGDUMP_PATH%" -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -d %DB_NAME% -t stock_info --data-only --column-inserts --encoding=UTF8 -f "%OUTPUT_DIR%\stock_info_data.sql"
if %errorlevel%==0 (
    echo   [OK] stock_info_data.sql
) else (
    echo   [失败]
)

echo [4/4] 导出 stock_exrights_data (SQL)...
"%PGDUMP_PATH%" -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -d %DB_NAME% -t stock_exrights_data --data-only --column-inserts --encoding=UTF8 -f "%OUTPUT_DIR%\stock_exrights_data.sql"
if %errorlevel%==0 (
    echo   [OK] stock_exrights_data.sql
) else (
    echo   [失败]
)

:done
echo.
echo ========================================
echo 导出完成！文件保存在:
echo   %OUTPUT_DIR%
echo.
dir /b "%OUTPUT_DIR%\*.csv" "%OUTPUT_DIR%\*_data.sql" 2>nul
echo ========================================

:end
set PGPASSWORD=
echo.
pause
