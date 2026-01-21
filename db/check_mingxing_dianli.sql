-- 检查"明星电力"的代码映射问题

-- 1. 在stock_info表中查找所有包含"明星"的股票
SELECT stock_code, stock_name, is_active, market_code
FROM stock_info
WHERE stock_name LIKE '%明星%'
ORDER BY stock_code;

-- 2. 检查000722（明星电力的正确代码）
SELECT stock_code, stock_name, is_active, market_code
FROM stock_info
WHERE stock_code = '000722';

-- 3. 检查000101（可能被错误标记为明星电力）
SELECT stock_code, stock_name, is_active, market_code
FROM stock_info
WHERE stock_code = '000101';

-- 4. 查看是否有重复或错误的映射
SELECT stock_code, stock_name, COUNT(*) as count
FROM stock_info
WHERE stock_name LIKE '%明星%' OR stock_code IN ('000722', '000101')
GROUP BY stock_code, stock_name
ORDER BY stock_code;

-- 5. 检查最近的日线数据，看实际是哪个股票
SELECT stock_code, trade_date, open, high, low, close
FROM stock_daily_data
WHERE stock_code IN ('000722', '000101')
ORDER BY stock_code, trade_date DESC
LIMIT 20;
