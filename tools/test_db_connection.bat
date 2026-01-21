@echo off
chcp 65001 >nul
echo ========================================
echo   PostgreSQL 数据库连接测试
echo ========================================
echo.

:: 数据库配置（根据需要修改）
set DB_HOST=localhost
set DB_PORT=8532
set DB_NAME=stockdb
set DB_USER=postgres
set DB_PASSWORD=cd123321

echo 连接信息:
echo   主机: %DB_HOST%
echo   端口: %DB_PORT%
echo   数据库: %DB_NAME%
echo   用户: %DB_USER%
echo.

:: 设置密码环境变量
set PGPASSWORD=%DB_PASSWORD%

:: 查找 psql 路径
set PSQL_PATH=
for %%p in (
   "F:\dsfr\mqq\tools\bin\psql.exe"
) do (
    if exist %%p (
        set PSQL_PATH=%%p
        goto :found
    )
)

:: 尝试直接使用 psql（如果在 PATH 中）
where psql >nul 2>&1
if %errorlevel%==0 (
    set PSQL_PATH=psql
    goto :found
)

echo [错误] 找不到 psql.exe
echo 请确保已安装 PostgreSQL 客户端工具
echo.
echo 可以从以下地址下载:
echo https://www.postgresql.org/download/
echo.
goto :end

:found
echo 使用 psql: %PSQL_PATH%
echo.
echo ----------------------------------------
echo 测试连接...
echo ----------------------------------------

:: 测试连接并获取版本
%PSQL_PATH% -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -d %DB_NAME% -c "SELECT version();" 2>&1

if %errorlevel%==0 (
    echo.
    echo ----------------------------------------
    echo [成功] 数据库连接正常!
    echo ----------------------------------------
    echo.
    echo 检查数据表...
    %PSQL_PATH% -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -d %DB_NAME% -c "\dt"

    echo.
    echo 数据统计:
    %PSQL_PATH% -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -d %DB_NAME% -c "SELECT 'stock_info' as table_name, COUNT(*) as count FROM stock_info UNION ALL SELECT 'stock_daily_data', COUNT(*) FROM stock_daily_data UNION ALL SELECT 'stock_exrights_data', COUNT(*) FROM stock_exrights_data UNION ALL SELECT 'stock_realtime_data', COUNT(*) FROM stock_realtime_data;"
) else (
    echo.
    echo ----------------------------------------
    echo [失败] 数据库连接失败!
    echo ----------------------------------------
    echo.
    echo 可能的原因:
    echo   1. 数据库服务未启动
    echo   2. 主机地址或端口错误
    echo   3. 用户名或密码错误
    echo   4. 防火墙阻止连接
    echo   5. PostgreSQL 未配置远程访问
    echo.
    echo 请检查:
    echo   - postgresql.conf: listen_addresses = '*'
    echo   - pg_hba.conf: host all all 0.0.0.0/0 md5
)

:end
echo.
echo ========================================
set PGPASSWORD=
pause
