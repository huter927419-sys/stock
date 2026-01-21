@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul
REM ============================================
REM 从 dump 文件导入数据到 stockdb1 数据库
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

echo ============================================
echo 从 dump 文件导入数据到 stockdb1
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

REM 查找 dump 文件
echo [2/4] 查找 dump 文件...
set BACKUP_DIR=.\backups
set DUMP_FILE=

REM 显示可用的 dump 文件
echo 可用的 dump 文件:
echo.
set FILE_COUNT=0
for /f "delims=" %%f in ('dir /b /o-d "%BACKUP_DIR%\*.dump" 2^>nul') do (
    set /a FILE_COUNT+=1
    echo   !FILE_COUNT!. %%f
    set DUMP_FILE_!FILE_COUNT!=%BACKUP_DIR%\%%f
)

if !FILE_COUNT! EQU 0 (
    echo [错误] 在 %BACKUP_DIR% 目录中找不到 dump 文件
    echo 请先运行 backup_stockdb.bat 生成备份文件
    goto :end
)

echo.
if !FILE_COUNT! EQU 1 (
    REM 只有一个文件，直接使用
    set DUMP_FILE=!DUMP_FILE_1!
    echo 自动选择: !DUMP_FILE!
) else (
    REM 多个文件，让用户选择
    set /p FILE_CHOICE="请选择要导入的 dump 文件 (1-!FILE_COUNT!，默认1): "
    if "!FILE_CHOICE!"=="" set FILE_CHOICE=1
    set DUMP_FILE=!DUMP_FILE_!FILE_CHOICE!!
    if "!DUMP_FILE!"=="" (
        echo [错误] 无效的选择
        goto :end
    )
)
echo.

REM 检查表是否存在
echo [3/4] 检查表结构...
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
        echo [警告] 找不到 create_all_tables.sql 文件
        echo 将尝试从 dump 文件恢复表结构
    )
) else (
    echo 表 stock_daily_data 已存在
)
echo.

REM 询问是否清空现有数据
echo [4/4] 准备导入数据...
set /p CLEAR_DATA="是否清空 stockdb1 中 stock_daily_data 表的现有数据？(Y/N，默认N): "
if /i "!CLEAR_DATA!"=="Y" (
    echo 正在清空现有数据...
    "!PSQL_PATH!" -h %TARGET_HOST% -p %TARGET_PORT% -U %TARGET_USER% -d %TARGET_DB% -c "TRUNCATE TABLE public.stock_daily_data;"
    if %errorlevel% neq 0 (
        echo [警告] 清空数据失败，将继续导入（可能会产生重复数据）
    ) else (
        echo 数据已清空
    )
    echo.
)

REM 判断是完整数据库备份还是表备份
echo 正在导入 dump 文件: !DUMP_FILE!
echo.

REM 尝试恢复数据
REM 先尝试仅恢复 stock_daily_data 表（不使用 --clean，避免清空数据）
echo 正在导入数据...
"!PGRESTORE_PATH!" -h %TARGET_HOST% -p %TARGET_PORT% -U %TARGET_USER% -d %TARGET_DB% -t public.stock_daily_data --if-exists --no-owner --no-privileges --data-only "!DUMP_FILE!" 2>&1
set RESTORE_RESULT=!errorlevel!

if !RESTORE_RESULT! neq 0 (
    REM 如果恢复表失败，尝试恢复整个数据库（仅数据）
    echo 尝试恢复整个数据库数据...
    "!PGRESTORE_PATH!" -h %TARGET_HOST% -p %TARGET_PORT% -U %TARGET_USER% -d %TARGET_DB% --if-exists --no-owner --no-privileges --data-only "!DUMP_FILE!" 2>&1
    set RESTORE_RESULT=!errorlevel!
    
    if !RESTORE_RESULT! neq 0 (
        REM 如果仅数据恢复失败，尝试完整恢复（包含表结构）
        echo 尝试完整恢复（包含表结构和数据）...
        "!PGRESTORE_PATH!" -h %TARGET_HOST% -p %TARGET_PORT% -U %TARGET_USER% -d %TARGET_DB% --if-exists --no-owner --no-privileges "!DUMP_FILE!" 2>&1
        set RESTORE_RESULT=!errorlevel!
    )
)

if !RESTORE_RESULT! equ 0 (
    echo.
    echo ============================================
    echo 导入成功！
    echo ============================================
    
    REM 显示恢复后的数据统计
    echo.
    echo 数据统计:
    "!PSQL_PATH!" -h %TARGET_HOST% -p %TARGET_PORT% -U %TARGET_USER% -d %TARGET_DB% -c "SELECT COUNT(*) as total_records FROM public.stock_daily_data;"
    
    REM 检查数据是否为0
    echo.
    for /f "skip=2 tokens=1" %%a in ('"!PSQL_PATH!" -h %TARGET_HOST% -p %TARGET_PORT% -U %TARGET_USER% -d %TARGET_DB% -t -A -c "SELECT COUNT(*) FROM public.stock_daily_data;"') do (
        set RECORD_COUNT=%%a
        goto :check_count
    )
    :check_count
    if "!RECORD_COUNT!"=="0" (
        echo [警告] 导入后数据为0条，可能的原因：
        echo 1. dump 文件是表结构备份（不含数据）
        echo 2. dump 文件中的数据已被清空
        echo 3. 表名或schema不匹配
        echo 4. 请检查 dump 文件内容: "!PGRESTORE_PATH!" -l "!DUMP_FILE!"
    )
) else (
    echo.
    echo ============================================
    echo 导入失败！
    echo ============================================
    echo 请检查：
    echo 1. dump 文件是否完整
    echo 2. 数据库连接是否正常
    echo 3. 表结构是否匹配
    echo 4. 查看上方的错误信息
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
exit /b 0
