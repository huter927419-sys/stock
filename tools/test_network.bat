@echo off
chcp 65001 >nul
echo ========================================
echo   数据库网络连接测试 (无需psql)
echo ========================================
echo.

:: 数据库配置（根据需要修改）
set DB_HOST=localhost
set DB_PORT=8532

echo 测试连接到: %DB_HOST%:%DB_PORT%
echo.

echo 【测试1】Ping 主机...
ping -n 1 %DB_HOST% >nul 2>&1
if %errorlevel%==0 (
    echo   [OK] 主机 %DB_HOST% 可达
) else (
    echo   [警告] 无法 ping 通主机 (可能是防火墙禁止ICMP)
)

echo.
echo 【测试2】测试端口连通性...
powershell -Command "$tcp = New-Object System.Net.Sockets.TcpClient; try { $tcp.Connect('%DB_HOST%', %DB_PORT%); Write-Host '  [OK] 端口 %DB_PORT% 可访问'; $tcp.Close() } catch { Write-Host '  [失败] 无法连接到端口 %DB_PORT%' }"

echo.
echo 【测试3】检查本机 PostgreSQL 服务...
sc query postgresql-x64-16 >nul 2>&1
if %errorlevel%==0 (
    echo   [OK] PostgreSQL 16 服务已安装
    sc query postgresql-x64-16 | findstr STATE
) else (
    sc query postgresql-x64-15 >nul 2>&1
    if %errorlevel%==0 (
        echo   [OK] PostgreSQL 15 服务已安装
        sc query postgresql-x64-15 | findstr STATE
    ) else (
        echo   [信息] 本机未安装 PostgreSQL 服务
        echo          (如果数据库在远程服务器，这是正常的)
    )
)

echo.
echo ========================================
echo 测试完成!
echo.
echo 如果端口测试失败，请检查:
echo   1. 数据库服务是否启动
echo   2. 防火墙是否开放 %DB_PORT% 端口
echo   3. DB_HOST 和 DB_PORT 配置是否正确
echo ========================================
echo.
pause
