@echo off
chcp 65001 >nul
REM ============================================
REM 导出 stock_daily_data 表全量数据脚本 (Windows批处理版本)
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
set EXPORT_FILE=%EXPORT_DIR%\stock_daily_data_%TIMESTAMP%.csv

REM 创建导出目录
if not exist "%EXPORT_DIR%" mkdir "%EXPORT_DIR%"

REM 查找 psql 路径
set PSQL_PATH=
for %%p in (
   "F:\dsfr\mqq\tools\bin\psql.exe"
   "C:\Program Files\PostgreSQL\16\bin\psql.exe"
   "C:\Program Files\PostgreSQL\15\bin\psql.exe"
   "C:\Program Files\PostgreSQL\14\bin\psql.exe"
   "C:\Program Files\PostgreSQL\13\bin\psql.exe"
) do (
    if exist %%p (
        set PSQL_PATH=%%p
        goto :found_psql
    )
)

REM 尝试直接使用 psql（如果在 PATH 中）
where psql >nul 2>&1
if %errorlevel%==0 (
    set PSQL_PATH=psql
    goto :found_psql
)

echo [错误] 找不到 psql.exe
echo 请确保已安装 PostgreSQL 客户端工具
echo 或者将 psql.exe 的路径添加到 PATH 环境变量中
echo.
pause
exit /b 1

:found_psql
echo ============================================
echo 开始导出 stock_daily_data 表数据
echo 数据库: %DB_NAME%@%DB_HOST%:%DB_PORT%
echo 导出文件: %EXPORT_FILE%
echo 使用 psql: %PSQL_PATH%
echo ============================================

REM 设置密码环境变量
set PGPASSWORD=%DB_PASSWORD%

REM 使用 COPY 命令导出
echo 使用 COPY 命令导出数据...
"%PSQL_PATH%" -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -d %DB_NAME% -c "\copy (SELECT * FROM public.stock_daily_data ORDER BY stock_code, trade_date) TO '%EXPORT_FILE%' WITH CSV HEADER;"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ============================================
    echo 导出成功！
    echo ============================================
    
    REM 显示文件大小
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
    echo 提示: 可以使用 7-Zip 或 WinRAR 压缩文件以节省空间
    echo 压缩命令示例: 7z a "%EXPORT_FILE%.7z" "%EXPORT_FILE%"
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
    exit /b 1
)

REM 清除密码环境变量
set PGPASSWORD=

echo ============================================
echo 导出完成！
echo ============================================
pause
