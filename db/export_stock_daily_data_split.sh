#!/bin/bash
# ============================================
# 分批导出 stock_daily_data 表数据（如果单次导出失败）
# 按股票代码或日期范围分批导出
# ============================================

# 数据库连接配置
DB_HOST="localhost"
DB_PORT="8532"
DB_NAME="stockdb"
DB_USER="postgres"

# 导出文件配置
EXPORT_DIR="./exports"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BATCH_SIZE=1000000  # 每批100万条

# 创建导出目录
mkdir -p "${EXPORT_DIR}"

echo "============================================"
echo "分批导出 stock_daily_data 表数据"
echo "每批: ${BATCH_SIZE} 条记录"
echo "============================================"

# 获取总记录数
TOTAL_COUNT=$(psql -h "${DB_HOST}" -p "${DB_PORT}" -U "${DB_USER}" -d "${DB_NAME}" -t -c "SELECT COUNT(*) FROM public.stock_daily_data;")
echo "总记录数: ${TOTAL_COUNT}"

# 计算批次数
BATCH_COUNT=$(( (TOTAL_COUNT + BATCH_SIZE - 1) / BATCH_SIZE ))
echo "将分 ${BATCH_COUNT} 批导出"
echo ""

# 分批导出
BATCH_NUM=1
OFFSET=0

while [ $OFFSET -lt $TOTAL_COUNT ]; do
    EXPORT_FILE="${EXPORT_DIR}/stock_daily_data_batch_${BATCH_NUM}_${TIMESTAMP}.csv"
    
    echo "正在导出第 ${BATCH_NUM}/${BATCH_COUNT} 批..."
    echo "文件: ${EXPORT_FILE}"
    
    psql -h "${DB_HOST}" -p "${DB_PORT}" -U "${DB_USER}" -d "${DB_NAME}" <<EOF
\copy (SELECT * FROM public.stock_daily_data ORDER BY id LIMIT ${BATCH_SIZE} OFFSET ${OFFSET}) TO '${EXPORT_FILE}' WITH CSV HEADER;
EOF
    
    if [ $? -eq 0 ]; then
        FILE_SIZE=$(du -h "${EXPORT_FILE}" | cut -f1)
        echo "第 ${BATCH_NUM} 批导出成功！文件大小: ${FILE_SIZE}"
        
        # 压缩文件
        gzip "${EXPORT_FILE}"
        echo "已压缩: ${EXPORT_FILE}.gz"
    else
        echo "第 ${BATCH_NUM} 批导出失败！"
        exit 1
    fi
    
    echo ""
    OFFSET=$((OFFSET + BATCH_SIZE))
    BATCH_NUM=$((BATCH_NUM + 1))
done

echo "============================================"
echo "所有批次导出完成！"
echo "文件保存在: ${EXPORT_DIR}"
echo "============================================"
