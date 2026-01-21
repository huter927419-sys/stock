#!/bin/bash
# ============================================
# 使用 pg_dump 导出 stock_daily_data 表数据
# 适合大数据量导出，支持压缩
# ============================================

# 数据库连接配置
DB_HOST="localhost"
DB_PORT="8532"
DB_NAME="stockdb"
DB_USER="postgres"

# 导出文件配置
EXPORT_DIR="./exports"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
EXPORT_FILE="${EXPORT_DIR}/stock_daily_data_${TIMESTAMP}.sql"

# 创建导出目录
mkdir -p "${EXPORT_DIR}"

echo "============================================"
echo "使用 pg_dump 导出 stock_daily_data 表"
echo "数据库: ${DB_NAME}@${DB_HOST}:${DB_PORT}"
echo "导出文件: ${EXPORT_FILE}"
echo "============================================"

# 使用 pg_dump 导出单个表（压缩格式）
# -Fc: 自定义格式（压缩）
# -t: 指定表名
# -f: 输出文件
pg_dump -h "${DB_HOST}" -p "${DB_PORT}" -U "${DB_USER}" -d "${DB_NAME}" \
    -t public.stock_daily_data \
    -Fc \
    -f "${EXPORT_FILE}"

if [ $? -eq 0 ]; then
    echo "导出成功！"
    echo "文件: ${EXPORT_FILE}"
    echo "文件大小: $(du -h "${EXPORT_FILE}" | cut -f1)"
    echo ""
    echo "恢复数据命令:"
    echo "pg_restore -h ${DB_HOST} -p ${DB_PORT} -U ${DB_USER} -d ${DB_NAME} -t public.stock_daily_data ${EXPORT_FILE}"
else
    echo "导出失败！"
    exit 1
fi

echo "============================================"
echo "导出完成！"
echo "============================================"
