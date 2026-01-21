@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul
REM ============================================
REM 将备份恢复到 stockdb1 数据库
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
echo ============================================
echo 恢复数据到 stockdb1 数据库
echo 目标数据库: %TARGET_DB%@%TARGET_HOST%:%TARGET_PORT%
echo 注意: 此操作不会影响 stockdb 数据库
echo ============================================
echo.

REM 设置密码环境变量
set PGPASSWORD=%TARGET_PASSWORD%

REM 检查目标数据库是否存在
echo [1/4] 检查目标数据库是否存在...
"!PSQL_PATH!" -h %TARGET_HOST% -p %TARGET_PORT% -U %TARGET_USER% -d postgres -c "SELECT 1 FROM pg_database WHERE datname='%TARGET_DB%'" | find "1" >nul
if %errorlevel% neq 0 (
    echo 数据库 %TARGET_DB% 不存在，正在创建...
    "!PSQL_PATH!" -h %TARGET_HOST% -p %TARGET_PORT% -U %TARGET_USER% -d postgres -c "CREATE DATABASE %TARGET_DB% ENCODING 'UTF8';"
    if %errorlevel% neq 0 (
        echo [错误] 无法创建数据库 %TARGET_DB%
        goto :end
    )
    echo 数据库创建成功！
) else (
    echo 数据库 %TARGET_DB% 已存在
)
echo.

REM 检查表是否存在，如果不存在则创建
echo [2/4] 检查表结构...
"!PSQL_PATH!" -h %TARGET_HOST% -p %TARGET_PORT% -U %TARGET_USER% -d %TARGET_DB% -c "\dt public.stock_daily_data" | find "stock_daily_data" >nul
if %errorlevel% neq 0 (
    echo 表 stock_daily_data 不存在，正在创建...
    if exist "create_all_tables.sql" (
        echo 正在创建表结构...
        "!PSQL_PATH!" -h %TARGET_HOST% -p %TARGET_PORT% -U %TARGET_USER% -d %TARGET_DB% -f create_all_tables.sql
        if %errorlevel% neq 0 (
            echo [错误] 创建表结构失败
            goto :end
        )
        echo 表结构创建成功！
    ) else (
        echo [错误] 找不到 create_all_tables.sql 文件
        echo 请确保该文件存在于当前目录
        goto :end
    )
) else (
    echo 表 stock_daily_data 已存在
)
echo.

REM 查找备份文件
echo [3/4] 查找备份文件...
set BACKUP_DIR=.\backups
set RESTORE_FILE=

REM 优先查找最新的完整数据库备份
for /f "delims=" %%f in ('dir /b /o-d "%BACKUP_DIR%\stockdb_full_*.dump" 2^>nul') do (
    set RESTORE_FILE=%BACKUP_DIR%\%%f
    set RESTORE_TYPE=full
    goto :found_backup
)

REM 如果没有完整备份，查找表备份
for /f "delims=" %%f in ('dir /b /o-d "%BACKUP_DIR%\stockdb_stock_daily_data_*.dump" 2^>nul') do (
    set RESTORE_FILE=%BACKUP_DIR%\%%f
    set RESTORE_TYPE=table
    goto :found_backup
)

REM 如果没有找到 .dump 文件，查找 .sql 文件
for /f "delims=" %%f in ('dir /b /o-d "%BACKUP_DIR%\stockdb_schema_*.sql" 2^>nul') do (
    set RESTORE_FILE=%BACKUP_DIR%\%%f
    set RESTORE_TYPE=schema
    goto :found_backup
)

echo [错误] 在 %BACKUP_DIR% 目录中找不到备份文件
echo 请先运行 backup_stockdb.bat 生成备份文件
goto :end

:found_backup
echo 找到备份文件: !RESTORE_FILE!
echo.

REM 询问是否清空现有数据
echo [4/4] 准备恢复数据...
set /p CLEAR_DATA="是否清空 stockdb1 中 stock_daily_data 表的现有数据？(Y/N，默认N): "
if /i "!CLEAR_DATA!"=="Y" (
    echo 正在清空现有数据...
    "!PSQL_PATH!" -h %TARGET_HOST% -p %TARGET_PORT% -U %TARGET_USER% -d %TARGET_DB% -c "TRUNCATE TABLE public.stock_daily_data;"
    if %errorlevel% neq 0 (
        echo [警告] 清空数据失败，将继续恢复（可能会产生重复数据）
    ) else (
        echo 数据已清空
    )
    echo.
)

REM 根据备份类型选择恢复方式
if "!RESTORE_TYPE!"=="full" (
    echo 使用 pg_restore 恢复完整数据库备份...
    "!PGRESTORE_PATH!" -h %TARGET_HOST% -p %TARGET_PORT% -U %TARGET_USER% -d %TARGET_DB% --clean --if-exists "!RESTORE_FILE!"
    if !errorlevel! equ 0 (
        echo.
        echo ============================================
        echo 恢复成功！
        echo ============================================
    ) else (
        echo.
        echo ============================================
        echo 恢复失败！
        echo ============================================
        goto :end
    )
) else if "!RESTORE_TYPE!"=="table" (
    echo 使用 pg_restore 恢复表备份...
    "!PGRESTORE_PATH!" -h %TARGET_HOST% -p %TARGET_PORT% -U %TARGET_USER% -d %TARGET_DB% -t public.stock_daily_data --clean --if-exists "!RESTORE_FILE!"
    if !errorlevel! equ 0 (
        echo.
        echo ============================================
        echo 恢复成功！
        echo ============================================
    ) else (
        echo.
        echo ============================================
        echo 恢复失败！
        echo ============================================
        goto :end
    )
) else if "!RESTORE_TYPE!"=="schema" (
    echo [警告] 这是表结构备份，不含数据
    echo 使用 psql 恢复表结构...
    "!PSQL_PATH!" -h %TARGET_HOST% -p %TARGET_PORT% -U %TARGET_USER% -d %TARGET_DB% -f "!RESTORE_FILE!"
    if !errorlevel! equ 0 (
        echo.
        echo ============================================
        echo 表结构恢复成功！
        echo 注意: 此备份不含数据，表为空
        echo ============================================
    ) else (
        echo.
        echo ============================================
        echo 恢复失败！
        echo ============================================
        goto :end
    )
)

REM 显示恢复后的数据统计
echo.
echo 数据统计:
"!PSQL_PATH!" -h %TARGET_HOST% -p %TARGET_PORT% -U %TARGET_USER% -d %TARGET_DB% -c "SELECT COUNT(*) as total_records FROM public.stock_daily_data;"

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
