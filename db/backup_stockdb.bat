@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul
REM ============================================
REM 备份 stockdb 数据库
REM ============================================

REM 数据库连接配置
set DB_HOST=localhost
set DB_PORT=8532
set DB_NAME=stockdb
set DB_USER=postgres
set DB_PASSWORD=cd123321

REM 备份文件配置
set BACKUP_DIR=.\backups
REM 生成时间戳（格式：YYYYMMDD_HHMMSS，无中文）
for /f "tokens=2 delims==" %%I in ('wmic os get localdatetime /value') do set datetime=%%I
set TIMESTAMP=!datetime:~0,8!_!datetime:~8,6!

REM 创建备份目录
if not exist "%BACKUP_DIR%" mkdir "%BACKUP_DIR%"

REM 查找工具路径
set PGDUMP_PATH=
for %%p in (
   "F:\dsfr\mqq\tools\bin\pg_dump.exe"
   "C:\Program Files\PostgreSQL\16\bin\pg_dump.exe"
   "C:\Program Files\PostgreSQL\15\bin\pg_dump.exe"
) do (
    if exist %%p (
        set PGDUMP_PATH=%%p
        goto :found_pgdump
    )
)
where pg_dump >nul 2>&1
if %errorlevel%==0 set PGDUMP_PATH=pg_dump

:found_pgdump
if "%PGDUMP_PATH%"=="" (
    echo [错误] 找不到 pg_dump.exe
    pause
    exit /b 1
)

echo ============================================
echo 备份 stockdb 数据库
echo 数据库: %DB_NAME%@%DB_HOST%:%DB_PORT%
echo 使用工具: %PGDUMP_PATH%
echo ============================================
echo.

REM 设置密码环境变量
set PGPASSWORD=%DB_PASSWORD%

REM 选择备份方式
echo 请选择备份方式：
echo 1. 完整数据库备份 - 推荐，包含所有表和数据
echo 2. 仅备份 stock_daily_data 表
echo 3. 备份所有表结构 - 不含数据
echo.
set /p BACKUP_TYPE="请输入选项 (1/2/3，默认1): "
if "!BACKUP_TYPE!"=="" (
    set BACKUP_TYPE=1
)

if "!BACKUP_TYPE!"=="1" goto :backup_full
if "!BACKUP_TYPE!"=="2" goto :backup_table
if "!BACKUP_TYPE!"=="3" goto :backup_schema
echo [错误] 无效的选项
goto :end

:backup_full
    REM 完整数据库备份（自定义格式，压缩）
    set BACKUP_FILE=!BACKUP_DIR!\stockdb_full_!TIMESTAMP!.dump
    echo.
    echo [方式1] 完整数据库备份（压缩格式）...
    echo 备份文件: !BACKUP_FILE!
    "!PGDUMP_PATH!" -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -d %DB_NAME% -Fc -f "!BACKUP_FILE!"
    goto :check_result

:backup_table
    REM 仅备份 stock_daily_data 表（自定义格式，压缩）
    set BACKUP_FILE=!BACKUP_DIR!\stockdb_stock_daily_data_!TIMESTAMP!.dump
    echo.
    echo [方式2] 备份 stock_daily_data 表（压缩格式）...
    echo 备份文件: !BACKUP_FILE!
    "!PGDUMP_PATH!" -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -d %DB_NAME% -t public.stock_daily_data -Fc -f "!BACKUP_FILE!"
    goto :check_result

:backup_schema
    REM 仅备份表结构（不含数据）
    set BACKUP_FILE=!BACKUP_DIR!\stockdb_schema_!TIMESTAMP!.sql
    echo.
    echo [方式3] 备份表结构（不含数据）...
    echo 备份文件: !BACKUP_FILE!
    "!PGDUMP_PATH!" -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -d %DB_NAME% --schema-only -f "!BACKUP_FILE!"
    goto :check_result

:check_result
    if !ERRORLEVEL! EQU 0 (
        echo.
        echo ============================================
        echo 备份成功！
        echo ============================================
        if exist "!BACKUP_FILE!" (
            for %%A in ("!BACKUP_FILE!") do (
                set SIZE=%%~zA
                set /a SIZE_MB=!SIZE!/1048576
                set /a SIZE_GB=!SIZE!/1073741824
                if !SIZE_GB! GTR 0 (
                    echo 文件大小: !SIZE_GB! GB ^(!SIZE_MB! MB^)
                ) else (
                    echo 文件大小: !SIZE_MB! MB
                )
            )
        )
        echo.
        if "!BACKUP_TYPE!"=="3" (
            echo 恢复命令:
            echo psql -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -d %DB_NAME% -f "!BACKUP_FILE!"
        ) else (
            if "!BACKUP_TYPE!"=="2" (
                echo 恢复命令:
                echo pg_restore -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -d %DB_NAME% -t public.stock_daily_data "!BACKUP_FILE!"
            ) else (
                echo 恢复命令:
                echo pg_restore -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -d %DB_NAME% "!BACKUP_FILE!"
            )
        )
    ) else (
        echo.
        echo ============================================
        echo 备份失败！
        echo ============================================
    )

:end
REM 清除密码环境变量
set PGPASSWORD=

echo.
echo ============================================
echo 备份完成！
echo 备份文件保存在: %BACKUP_DIR%
echo ============================================
endlocal
pause
