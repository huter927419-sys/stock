#!/bin/bash
# ============================================
# 导出 stock_daily_data 表全量数据脚本
# 数据量：约2000万条
# ============================================

# 数据库连接配置
DB_HOST="localhost"
DB_PORT="8532"
DB_NAME="stockdb"
DB_USER="postgres"
# DB_PASSWORD 通过环境变量或 .pgpass 文件提供

# 导出文件配置
EXPORT_DIR="./exports"
EXPORT_FILE="${EXPORT_DIR}/stock_daily_data_$(date +%Y%m%d_%H%M%S).csv"
COMPRESSED_FILE="${EXPORT_FILE}.gz"

# 创建导出目录
mkdir -p "${EXPORT_DIR}"

echo "============================================"
echo "开始导出 stock_daily_data 表数据"
echo "数据库: ${DB_NAME}@${DB_HOST}:${DB_PORT}"
echo "导出文件: ${EXPORT_FILE}"
echo "============================================"

# 方法1: 使用 COPY 命令导出为 CSV（最快）
echo "使用 COPY 命令导出数据..."
psql -h "${DB_HOST}" -p "${DB_PORT}" -U "${DB_USER}" -d "${DB_NAME}" <<EOF
\copy (SELECT * FROM public.stock_daily_data ORDER BY stock_code, trade_date) TO '${EXPORT_FILE}' WITH CSV HEADER;
EOF

if [ $? -eq 0 ]; then
    echo "导出成功！"
    echo "文件大小: $(du -h "${EXPORT_FILE}" | cut -f1)"
    
    # 压缩文件以节省空间
    echo "正在压缩文件..."
    gzip "${EXPORT_FILE}"
    
    if [ $? -eq 0 ]; then
        echo "压缩完成！"
        echo "压缩后文件: ${COMPRESSED_FILE}"
        echo "压缩后大小: $(du -h "${COMPRESSED_FILE}" | cut -f1)"
    fi
else
    echo "导出失败！"
    exit 1
fi

echo "============================================"
echo "导出完成！"
echo "============================================"
