@echo off
chcp 65001 >nul
REM ============================================
REM 使用 pg_dump 导出 stock_daily_data 表数据
REM 数据量：约2000万条
REM ============================================

REM 数据库连接配置
set DB_HOST=localhost
set DB_PORT=8532
set DB_NAME=stockdb
set DB_USER=postgres
set DB_PASSWORD=cd123321

REM 导出文件配置
set EXPORT_DIR=.\exports
set TIMESTAMP=%date:~0,4%%date:~5,2%%date:~8,2%_%time:~0,2%%time:~3,2%%time:~6,2%
set TIMESTAMP=%TIMESTAMP: =0%
set EXPORT_FILE=%EXPORT_DIR%\stock_daily_data_%TIMESTAMP%.sql

REM 创建导出目录
if not exist "%EXPORT_DIR%" mkdir "%EXPORT_DIR%"

REM 查找 pg_dump 路径
set PGDUMP_PATH=
for %%p in (
   "F:\dsfr\mqq\tools\bin\pg_dump.exe"
   "C:\Program Files\PostgreSQL\16\bin\pg_dump.exe"
   "C:\Program Files\PostgreSQL\15\bin\pg_dump.exe"
   "C:\Program Files\PostgreSQL\14\bin\pg_dump.exe"
   "C:\Program Files\PostgreSQL\13\bin\pg_dump.exe"
) do (
    if exist %%p (
        set PGDUMP_PATH=%%p
        goto :found_pgdump
    )
)

REM 尝试直接使用 pg_dump（如果在 PATH 中）
where pg_dump >nul 2>&1
if %errorlevel%==0 (
    set PGDUMP_PATH=pg_dump
    goto :found_pgdump
)

echo [错误] 找不到 pg_dump.exe
echo 请确保已安装 PostgreSQL 客户端工具
echo 或者将 pg_dump.exe 的路径添加到 PATH 环境变量中
echo.
pause
exit /b 1

:found_pgdump
echo ============================================
echo 使用 pg_dump 导出 stock_daily_data 表数据
echo 数据库: %DB_NAME%@%DB_HOST%:%DB_PORT%
echo 导出文件: %EXPORT_FILE%
echo 使用工具: %PGDUMP_PATH%
echo ============================================
echo.

REM 设置密码环境变量
set PGPASSWORD=%DB_PASSWORD%

REM 方法1: 导出为自定义格式（压缩，推荐）
echo [方法1] 导出为自定义格式（压缩）...
set EXPORT_FILE_CUSTOM=%EXPORT_DIR%\stock_daily_data_%TIMESTAMP%.dump
"%PGDUMP_PATH%" -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -d %DB_NAME% ^
    -t public.stock_daily_data ^
    -Fc ^
    -f "%EXPORT_FILE_CUSTOM%"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ============================================
    echo 导出成功！（自定义格式）
    echo ============================================
    
    if exist "%EXPORT_FILE_CUSTOM%" (
        for %%A in ("%EXPORT_FILE_CUSTOM%") do (
            set SIZE=%%~zA
            set /a SIZE_MB=!SIZE!/1048576
            set /a SIZE_GB=!SIZE!/1073741824
            if !SIZE_GB! GTR 0 (
                echo 文件大小: !SIZE_GB! GB (!SIZE_MB! MB)
            ) else (
                echo 文件大小: !SIZE_MB! MB
            )
        )
    )
    
    echo.
    echo 恢复数据命令:
    echo pg_restore -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -d %DB_NAME% -t public.stock_daily_data "%EXPORT_FILE_CUSTOM%"
) else (
    echo 导出失败！
    goto :try_sql
)

goto :end

:try_sql
echo.
echo [方法2] 导出为 SQL 格式（未压缩）...
"%PGDUMP_PATH%" -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -d %DB_NAME% ^
    -t public.stock_daily_data ^
    --data-only ^
    --column-inserts ^
    -f "%EXPORT_FILE%"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ============================================
    echo 导出成功！（SQL格式）
    echo ============================================
    
    if exist "%EXPORT_FILE%" (
        for %%A in ("%EXPORT_FILE%") do (
            set SIZE=%%~zA
            set /a SIZE_MB=!SIZE!/1048576
            set /a SIZE_GB=!SIZE!/1073741824
            if !SIZE_GB! GTR 0 (
                echo 文件大小: !SIZE_GB! GB (!SIZE_MB! MB)
            ) else (
                echo 文件大小: !SIZE_MB! MB
            )
        )
    )
    
    echo.
    echo 恢复数据命令:
    echo psql -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -d %DB_NAME% -f "%EXPORT_FILE%"
) else (
    echo.
    echo ============================================
    echo 导出失败！
    echo ============================================
    echo 请检查：
    echo 1. 数据库连接是否正常
    echo 2. 数据库用户权限是否足够
    echo 3. 磁盘空间是否充足
    echo.
)

:end
REM 清除密码环境变量
set PGPASSWORD=

echo.
echo ============================================
echo 导出完成！
echo ============================================
pause
