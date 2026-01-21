@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul
REM ============================================
REM 强制导入 dump 文件到 stockdb1（会先删除表）
REM 不会影响 stockdb 数据库
REM ============================================

REM 数据库连接配置（目标数据库）
set TARGET_HOST=localhost
set TARGET_PORT=8532
set TARGET_DB=stockdb1
set TARGET_USER=postgres
set TARGET_PASSWORD=cd123321

REM 查找工具路径
set PSQL_PATH=
set PGRESTORE_PATH=
for %%p in (
   "F:\dsfr\mqq\tools\bin\psql.exe"
   "C:\Program Files\PostgreSQL\16\bin\psql.exe"
   "C:\Program Files\PostgreSQL\15\bin\psql.exe"
) do (
    if exist %%p (
        set PSQL_PATH=%%p
        goto :found_psql
    )
)
where psql >nul 2>&1
if %errorlevel%==0 set PSQL_PATH=psql

:found_psql
for %%p in (
   "F:\dsfr\mqq\tools\bin\pg_restore.exe"
   "C:\Program Files\PostgreSQL\16\bin\pg_restore.exe"
   "C:\Program Files\PostgreSQL\15\bin\pg_restore.exe"
) do (
    if exist %%p (
        set PGRESTORE_PATH=%%p
        goto :found_pgrestore
    )
)
where pg_restore >nul 2>&1
if %errorlevel%==0 set PGRESTORE_PATH=pg_restore

:found_pgrestore
if "%PGRESTORE_PATH%"=="" (
    echo [错误] 找不到 pg_restore.exe
    pause
    exit /b 1
)

REM 获取 dump 文件路径（从命令行参数或使用默认值）
set DUMP_FILE=%1
if "%DUMP_FILE%"=="" (
    set DUMP_FILE=F:\dsfr\mqq\db\backups\stockdb_full_周日022601_150452.dump
)

REM 检查文件是否存在
if not exist "!DUMP_FILE!" (
    echo [错误] 找不到 dump 文件: !DUMP_FILE!
    pause
    exit /b 1
)

echo ============================================
echo 强制导入 dump 文件到 stockdb1
echo 目标数据库: !TARGET_DB!@!TARGET_HOST!:!TARGET_PORT!
echo Dump 文件: !DUMP_FILE!
echo 注意: 此操作会先删除 stock_daily_data 表，然后重新导入
echo 注意: 此操作不会影响 stockdb 数据库
echo ============================================
echo.

set /p CONFIRM="确认要继续吗？(Y/N，默认N): "
if /i not "!CONFIRM!"=="Y" (
    echo 操作已取消
    pause
    exit /b 0
)

REM 设置密码环境变量
set PGPASSWORD=!TARGET_PASSWORD!

REM 检查目标数据库是否存在
echo [1/4] 检查目标数据库是否存在...
"!PSQL_PATH!" -h !TARGET_HOST! -p !TARGET_PORT! -U !TARGET_USER! -d postgres -c "SELECT 1 FROM pg_database WHERE datname='!TARGET_DB!';" | find "1" >nul
if !errorlevel! neq 0 (
    echo 数据库 !TARGET_DB! 不存在，正在创建...
    "!PSQL_PATH!" -h !TARGET_HOST! -p !TARGET_PORT! -U !TARGET_USER! -d postgres -c "CREATE DATABASE !TARGET_DB! ENCODING 'UTF8';"
    if !errorlevel! neq 0 (
        echo [错误] 无法创建数据库 !TARGET_DB!
        goto :end
    )
    echo 数据库创建成功！
) else (
    echo 数据库 !TARGET_DB! 已存在
)
echo.

REM 删除现有表（如果存在）
echo [2/4] 删除现有表（如果存在）...
"!PSQL_PATH!" -h !TARGET_HOST! -p !TARGET_PORT! -U !TARGET_USER! -d !TARGET_DB! -c "DROP TABLE IF EXISTS public.stock_daily_data CASCADE;" 2>&1
if !errorlevel! neq 0 (
    echo [警告] 删除表失败，将继续尝试导入
) else (
    echo 表已删除
)
echo.

REM 导入数据（完整恢复，使用 --clean --if-exists 避免序列冲突）
echo [3/4] 正在导入数据（完整恢复）...
echo [注意] 使用 --clean --if-exists 选项，会删除现有的表结构和序列
"!PGRESTORE_PATH!" -h !TARGET_HOST! -p !TARGET_PORT! -U !TARGET_USER! -d !TARGET_DB! --clean --if-exists --no-owner --no-privileges --verbose "!DUMP_FILE!" 2>&1
set RESTORE_RESULT=!errorlevel!

if !RESTORE_RESULT! equ 0 (
    echo.
    echo ============================================
    echo 导入成功！
    echo ============================================
    
    REM 显示恢复后的数据统计
    echo.
    echo 数据统计:
    "!PSQL_PATH!" -h !TARGET_HOST! -p !TARGET_PORT! -U !TARGET_USER! -d !TARGET_DB! -c "SELECT COUNT(*) as total_records FROM public.stock_daily_data;"
    
    REM 检查数据是否为0
    echo.
    set RECORD_COUNT=
    for /f "skip=2 tokens=1" %%a in ('"!PSQL_PATH!" -h !TARGET_HOST! -p !TARGET_PORT! -U !TARGET_USER! -d !TARGET_DB! -t -A -c "SELECT COUNT(*) FROM public.stock_daily_data;"') do (
        set RECORD_COUNT=%%a
        goto :check_count
    )
    :check_count
    if "!RECORD_COUNT!"=="0" (
        echo.
        echo [警告] 导入后数据为0条
        echo.
        echo 可能的原因：
        echo   1. dump 文件中的数据已被清空
        echo   2. 恢复过程中出现错误（请查看上方的详细输出）
        echo   3. 表名或schema不匹配
    ) else (
        echo.
        echo 导入成功！共导入 !RECORD_COUNT! 条记录
    )
) else (
    echo.
    echo ============================================
    echo 导入失败！
    echo ============================================
    echo 请查看上方的详细错误信息
    echo.
)

:end
REM 清除密码环境变量
set PGPASSWORD=

echo.
echo ============================================
echo 操作完成！
echo 注意: stockdb 数据库未被修改
echo ============================================
endlocal
pause
exit /b 0
