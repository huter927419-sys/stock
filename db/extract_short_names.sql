-- 从长公司名称中智能提取股票简称
-- 规则：
-- 1. 如果以省/市名开头，去掉省市名
-- 2. 去掉"股份有限"、"有限公司"等后缀
-- 3. 保留核心名称（通常是4个字）

-- 先备份当前数据（可选）
-- CREATE TABLE stock_info_backup AS SELECT * FROM stock_info;

-- 更新规则：
-- "浙江世宝股份有限" -> "世宝股份"
-- "上海凯宝药业股份" -> "凯宝药业"
-- "深圳市昌红科技股" -> "昌红科技"

BEGIN;

-- 步骤1：去掉省市前缀和公司后缀，提取核心名称
UPDATE stock_info
SET stock_name =
    CASE
        -- 已经是4个字或更短的，保持不变
        WHEN LENGTH(stock_name) <= 4 THEN stock_name

        -- 去掉省市前缀（如：浙江、上海、深圳市等）
        ELSE (
            SELECT
                CASE
                    -- 如果去掉前缀后能得到4个字的名称
                    WHEN LENGTH(
                        REGEXP_REPLACE(
                            REGEXP_REPLACE(
                                stock_name,
                                '^(浙江|江苏|上海|北京|广东|深圳|广州|天津|重庆|福建|山东|河南|河北|湖南|湖北|四川|安徽|陕西|辽宁|吉林|黑龙江|江西|云南|贵州|甘肃|海南|宁夏|青海|西藏|新疆|内蒙古|广西)省?市?',
                                ''
                            ),
                            '(股份有限公司|有限公司|股份有限|集团股份|控股股份|科技股份|实业股份|股份).*$',
                            ''
                        )
                    ) >= 2 THEN
                        LEFT(
                            REGEXP_REPLACE(
                                REGEXP_REPLACE(
                                    stock_name,
                                    '^(浙江|江苏|上海|北京|广东|深圳|广州|天津|重庆|福建|山东|河南|河北|湖南|湖北|四川|安徽|陕西|辽宁|吉林|黑龙江|江西|云南|贵州|甘肃|海南|宁夏|青海|西藏|新疆|内蒙古|广西)省?市?',
                                    ''
                                ),
                                '(股份有限公司|有限公司|股份有限|集团股份|控股股份|科技股份|实业股份|股份).*$',
                                ''
                            ),
                            4
                        )
                    -- 否则直接截取前4个字
                    ELSE LEFT(stock_name, 4)
                END
        )
    END,
    update_time = CURRENT_TIMESTAMP
WHERE LENGTH(stock_name) > 4
  AND stock_name <> stock_code;

COMMIT;

-- 显示更新后的统计
SELECT
    COUNT(*) AS total,
    SUM(CASE WHEN LENGTH(stock_name) <= 4 THEN 1 ELSE 0 END) AS short_name_count,
    SUM(CASE WHEN LENGTH(stock_name) > 4 THEN 1 ELSE 0 END) AS long_name_count
FROM stock_info;

-- 显示一些示例
SELECT stock_code, stock_name
FROM stock_info
WHERE stock_name <> stock_code
LIMIT 20;
