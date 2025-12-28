-- ============================================
-- 创建 stockdb 数据库
-- ============================================
-- 说明：
-- 1. 此脚本需要在连接到 PostgreSQL 的默认数据库（postgres）时执行
-- 2. 执行方式：
--    psql -h localhost -p 8532 -U postgres -f create_database.sql
--    或者在 psql 中执行：
--    \i create_database.sql
-- ============================================

-- 检查数据库是否存在，如果不存在则创建
SELECT 'CREATE DATABASE stockdb'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'stockdb')\gexec

-- 设置数据库编码为 UTF-8
-- 注意：CREATE DATABASE 不能在事务块中执行，所以需要单独执行

-- 连接到新创建的数据库并设置权限
\c stockdb

-- 授予用户权限（如果需要）
-- GRANT ALL PRIVILEGES ON DATABASE stockdb TO postgres;

-- 显示创建结果
SELECT '数据库 stockdb 创建成功！' AS result;

