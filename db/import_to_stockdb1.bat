@echo off
chcp 65001 >nul
REM ============================================
REM 将导出的数据导入到本地 stockdb1 数据库
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
echo 导入数据到 stockdb1 数据库
echo 目标数据库: %TARGET_DB%@%TARGET_HOST%:%TARGET_PORT%
echo 注意: 此操作不会影响 stockdb 数据库
echo ============================================
echo.

REM 设置密码环境变量
set PGPASSWORD=%TARGET_PASSWORD%

REM 检查数据库是否存在
echo [1/4] 检查目标数据库是否存在...
"%PSQL_PATH%" -h %TARGET_HOST% -p %TARGET_PORT% -U %TARGET_USER% -d postgres -c "SELECT 1 FROM pg_database WHERE datname='%TARGET_DB%'" | find "1" >nul
if %errorlevel% neq 0 (
    echo 数据库 %TARGET_DB% 不存在，正在创建...
    "%PSQL_PATH%" -h %TARGET_HOST% -p %TARGET_PORT% -U %TARGET_USER% -d postgres -c "CREATE DATABASE %TARGET_DB%;"
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
"%PSQL_PATH%" -h %TARGET_HOST% -p %TARGET_PORT% -U %TARGET_USER% -d %TARGET_DB% -c "\dt public.stock_daily_data" | find "stock_daily_data" >nul
if %errorlevel% neq 0 (
    echo 表 stock_daily_data 不存在，正在创建...
    echo 请确保已运行 create_all_tables.sql 创建表结构
    echo 或者手动创建表后再运行此脚本
    echo.
    echo 创建表结构的命令：
    echo "%PSQL_PATH%" -h %TARGET_HOST% -p %TARGET_PORT% -U %TARGET_USER% -d %TARGET_DB% -f create_all_tables.sql
    echo.
    set /p CREATE_TABLE="是否现在创建表结构？(Y/N): "
    if /i "!CREATE_TABLE!"=="Y" (
        if exist "create_all_tables.sql" (
            "%PSQL_PATH%" -h %TARGET_HOST% -p %TARGET_PORT% -U %TARGET_USER% -d %TARGET_DB% -f create_all_tables.sql
            if %errorlevel% neq 0 (
                echo [错误] 创建表结构失败
                goto :end
            )
            echo 表结构创建成功！
        ) else (
            echo [错误] 找不到 create_all_tables.sql 文件
            goto :end
        )
    ) else (
        echo 已取消，请先创建表结构后再运行此脚本
        goto :end
    )
) else (
    echo 表 stock_daily_data 已存在
)
echo.

REM 查找导出文件
echo [3/4] 查找导出文件...
set EXPORT_DIR=.\exports
set IMPORT_FILE=

REM 优先查找 .dump 文件（自定义格式）
for /f "delims=" %%f in ('dir /b /o-d "%EXPORT_DIR%\stock_daily_data_*.dump" 2^>nul') do (
    set IMPORT_FILE=%EXPORT_DIR%\%%f
    set IMPORT_TYPE=dump
    goto :found_file
)

REM 如果没有 .dump 文件，查找 .sql 文件
for /f "delims=" %%f in ('dir /b /o-d "%EXPORT_DIR%\stock_daily_data_*.sql" 2^>nul') do (
    set IMPORT_FILE=%EXPORT_DIR%\%%f
    set IMPORT_TYPE=sql
    goto :found_file
)

REM 如果没有找到文件，查找 .csv 文件
for /f "delims=" %%f in ('dir /b /o-d "%EXPORT_DIR%\stock_daily_data_*.csv" 2^>nul') do (
    set IMPORT_FILE=%EXPORT_DIR%\%%f
    set IMPORT_TYPE=csv
    goto :found_file
)

echo [错误] 在 %EXPORT_DIR% 目录中找不到导出文件
echo 请先运行导出脚本生成数据文件
goto :end

:found_file
echo 找到导出文件: %IMPORT_FILE%
echo.

REM 询问是否备份现有数据
echo [4/4] 准备导入数据...
set /p BACKUP_FIRST="是否先备份 stockdb1 中的现有数据？(Y/N，默认Y): "
if "%BACKUP_FIRST%"=="" set BACKUP_FIRST=Y
if /i "!BACKUP_FIRST!"=="Y" (
    echo.
    echo 正在备份现有数据...
    call "%~dp0backup_stockdb1.bat" stock_daily_data
    if %errorlevel% neq 0 (
        echo [警告] 备份失败，但将继续导入
    )
    echo.
)

REM 询问是否清空现有数据
set /p CLEAR_DATA="是否清空表 stock_daily_data 中的现有数据？(Y/N，默认N): "
if /i "!CLEAR_DATA!"=="Y" (
    echo 正在清空现有数据...
    "%PSQL_PATH%" -h %TARGET_HOST% -p %TARGET_PORT% -U %TARGET_USER% -d %TARGET_DB% -c "TRUNCATE TABLE public.stock_daily_data;"
    if %errorlevel% neq 0 (
        echo [警告] 清空数据失败，将继续导入（可能会产生重复数据）
    ) else (
        echo 数据已清空
    )
    echo.
)

REM 根据文件类型选择导入方式
if "%IMPORT_TYPE%"=="dump" (
    echo 使用 pg_restore 导入自定义格式文件...
    "%PGRESTORE_PATH%" -h %TARGET_HOST% -p %TARGET_PORT% -U %TARGET_USER% -d %TARGET_DB% -t public.stock_daily_data --clean --if-exists "%IMPORT_FILE%"
    if %errorlevel% equ 0 (
        echo.
        echo ============================================
        echo 导入成功！
        echo ============================================
    ) else (
        echo.
        echo ============================================
        echo 导入失败！
        echo ============================================
        goto :end
    )
) else if "%IMPORT_TYPE%"=="sql" (
    echo 使用 psql 导入 SQL 文件...
    "%PSQL_PATH%" -h %TARGET_HOST% -p %TARGET_PORT% -U %TARGET_USER% -d %TARGET_DB% -f "%IMPORT_FILE%"
    if %errorlevel% equ 0 (
        echo.
        echo ============================================
        echo 导入成功！
        echo ============================================
    ) else (
        echo.
        echo ============================================
        echo 导入失败！
        echo ============================================
        goto :end
    )
) else if "%IMPORT_TYPE%"=="csv" (
    echo 使用 COPY 命令导入 CSV 文件...
    "%PSQL_PATH%" -h %TARGET_HOST% -p %TARGET_PORT% -U %TARGET_USER% -d %TARGET_DB% -c "\copy public.stock_daily_data FROM '%IMPORT_FILE%' WITH CSV HEADER;"
    if %errorlevel% equ 0 (
        echo.
        echo ============================================
        echo 导入成功！
        echo ============================================
    ) else (
        echo.
        echo ============================================
        echo 导入失败！
        echo ============================================
        goto :end
    )
)

REM 显示导入后的数据统计
echo.
echo 数据统计:
"%PSQL_PATH%" -h %TARGET_HOST% -p %TARGET_PORT% -U %TARGET_USER% -d %TARGET_DB% -c "SELECT COUNT(*) as total_records FROM public.stock_daily_data;"

:end
REM 清除密码环境变量
set PGPASSWORD=

echo.
echo ============================================
echo 操作完成！
echo ============================================
pause
