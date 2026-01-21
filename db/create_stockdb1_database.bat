@echo off
chcp 65001 >nul
REM ============================================
REM 创建 stockdb1 数据库
REM ============================================

set DB_HOST=localhost
set DB_PORT=8532
set DB_NAME=stockdb1
set DB_USER=postgres
set DB_PASSWORD=cd123321

REM 查找 psql 路径
set PSQL_PATH=
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
if "%PSQL_PATH%"=="" (
    echo [错误] 找不到 psql.exe
    pause
    exit /b 1
)

echo ============================================
echo 创建 stockdb1 数据库
echo ============================================
echo.

set PGPASSWORD=%DB_PASSWORD%

REM 检查数据库是否已存在
"%PSQL_PATH%" -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -d postgres -c "SELECT 1 FROM pg_database WHERE datname='%DB_NAME%'" | find "1" >nul
if %errorlevel% equ 0 (
    echo 数据库 %DB_NAME% 已存在
    set /p RECREATE="是否删除并重新创建？(Y/N，默认N): "
    if /i "!RECREATE!"=="Y" (
        echo 正在删除现有数据库...
        "%PSQL_PATH%" -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -d postgres -c "DROP DATABASE IF EXISTS %DB_NAME%;"
        if %errorlevel% neq 0 (
            echo [错误] 删除数据库失败
            goto :end
        )
        echo 数据库已删除
    ) else (
        echo 已取消操作
        goto :end
    )
)

REM 创建数据库
echo 正在创建数据库 %DB_NAME%...
"%PSQL_PATH%" -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -d postgres -c "CREATE DATABASE %DB_NAME% ENCODING 'UTF8';"
if %errorlevel% neq 0 (
    echo [错误] 创建数据库失败
    goto :end
)

echo 数据库创建成功！
echo.
set /p CREATE_TABLE="是否现在创建表结构？(Y/N): "
if /i "!CREATE_TABLE!"=="Y" (
    if exist "create_all_tables.sql" (
        echo 正在创建表结构...
        "%PSQL_PATH%" -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -d %DB_NAME% -f create_all_tables.sql
        if %errorlevel% equ 0 (
            echo 表结构创建成功！
        ) else (
            echo [错误] 创建表结构失败
        )
    ) else (
        echo [错误] 找不到 create_all_tables.sql 文件
    )
)

:end
set PGPASSWORD=
echo.
echo ============================================
echo 操作完成！
echo ============================================
pause
