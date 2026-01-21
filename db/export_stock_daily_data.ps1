# ============================================
# 导出 stock_daily_data 表全量数据脚本 (PowerShell版本)
# 数据量：约2000万条
# ============================================

# 数据库连接配置
$DB_HOST = "localhost"
$DB_PORT = "8532"
$DB_NAME = "stockdb"
$DB_USER = "postgres"
$DB_PASSWORD = "cd123321"  # 从 App.config 读取

# 导出文件配置
$EXPORT_DIR = ".\exports"
$TIMESTAMP = Get-Date -Format "yyyyMMdd_HHmmss"
$EXPORT_FILE = Join-Path $EXPORT_DIR "stock_daily_data_$TIMESTAMP.csv"
$COMPRESSED_FILE = "$EXPORT_FILE.gz"

# 创建导出目录
if (-not (Test-Path $EXPORT_DIR)) {
    New-Item -ItemType Directory -Path $EXPORT_DIR | Out-Null
}

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "开始导出 stock_daily_data 表数据" -ForegroundColor Cyan
Write-Host "数据库: ${DB_NAME}@${DB_HOST}:${DB_PORT}" -ForegroundColor Cyan
Write-Host "导出文件: $EXPORT_FILE" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

# 设置环境变量（用于 psql）
$env:PGPASSWORD = $DB_PASSWORD

try {
    # 方法1: 使用 COPY 命令导出为 CSV（最快）
    Write-Host "使用 COPY 命令导出数据..." -ForegroundColor Yellow
    
    $copyCommand = @"
\copy (SELECT * FROM public.stock_daily_data ORDER BY stock_code, trade_date) TO '$EXPORT_FILE' WITH CSV HEADER;
"@
    
    $copyCommand | & "psql" -h $DB_HOST -p $DB_PORT -U $DB_USER -d $DB_NAME
    
    if ($LASTEXITCODE -eq 0) {
        $fileSize = (Get-Item $EXPORT_FILE).Length
        $fileSizeMB = [math]::Round($fileSize / 1MB, 2)
        Write-Host "导出成功！" -ForegroundColor Green
        Write-Host "文件大小: $fileSizeMB MB" -ForegroundColor Green
        
        # 压缩文件以节省空间
        Write-Host "正在压缩文件..." -ForegroundColor Yellow
        Compress-Archive -Path $EXPORT_FILE -DestinationPath "$EXPORT_FILE.zip" -Force
        
        if ($?) {
            Write-Host "压缩完成！" -ForegroundColor Green
            $compressedSize = (Get-Item "$EXPORT_FILE.zip").Length
            $compressedSizeMB = [math]::Round($compressedSize / 1MB, 2)
            Write-Host "压缩后文件: $EXPORT_FILE.zip" -ForegroundColor Green
            Write-Host "压缩后大小: $compressedSizeMB MB" -ForegroundColor Green
            Write-Host "压缩率: $([math]::Round((1 - $compressedSize / $fileSize) * 100, 2))%" -ForegroundColor Green
        }
    } else {
        throw "导出失败，退出代码: $LASTEXITCODE"
    }
} catch {
    Write-Host "错误: $_" -ForegroundColor Red
    exit 1
} finally {
    # 清除密码环境变量
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
}

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "导出完成！" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
