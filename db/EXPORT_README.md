# stock_daily_data 表数据导出脚本说明

## 数据量
- 约 **2000万条** 记录
- 建议预留足够的磁盘空间（建议至少50GB）

## 脚本说明

### 1. PowerShell 版本（推荐 - Windows）
**文件**: `export_stock_daily_data.ps1`

**特点**:
- 使用 COPY 命令（最快）
- 自动压缩为 ZIP 格式
- 显示进度和文件大小

**使用方法**:
```powershell
cd db
.\export_stock_daily_data.ps1
```

### 2. 批处理版本（Windows - COPY命令）
**文件**: `export_stock_daily_data.bat`

**特点**:
- 简单易用，双击运行
- 使用 COPY 命令导出为 CSV
- 文件较大，但可以直接用 Excel 打开

**使用方法**:
```cmd
cd db
export_stock_daily_data.bat
```

### 2b. pg_dump 批处理版本（Windows - 推荐）
**文件**: `export_stock_daily_data_pgdump.bat`

**特点**:
- 使用 PostgreSQL 官方工具 pg_dump
- 支持压缩格式（.dump），文件更小
- 也可以导出为 SQL 格式
- 更适合大数据量导出

**使用方法**:
```cmd
cd db
export_stock_daily_data_pgdump.bat
```

**恢复数据**:
```cmd
REM 恢复自定义格式（.dump）
pg_restore -h localhost -p 8532 -U postgres -d stockdb -t public.stock_daily_data exports\stock_daily_data_*.dump

REM 恢复 SQL 格式
psql -h localhost -p 8532 -U postgres -d stockdb -f exports\stock_daily_data_*.sql
```

### 3. Bash 版本（Linux/Mac）
**文件**: `export_stock_daily_data.sh`

**特点**:
- 使用 COPY 命令
- 自动压缩为 GZ 格式

**使用方法**:
```bash
cd db
chmod +x export_stock_daily_data.sh
./export_stock_daily_data.sh
```

### 4. pg_dump 版本（推荐 - 官方工具）
**文件**: `export_stock_daily_data_pgdump.sh`

**特点**:
- 使用 PostgreSQL 官方工具
- 导出为压缩的自定义格式（.sql）
- 可以使用 pg_restore 恢复

**使用方法**:
```bash
cd db
chmod +x export_stock_daily_data_pgdump.sh
./export_stock_daily_data_pgdump.sh
```

**恢复数据**:
```bash
pg_restore -h localhost -p 8532 -U postgres -d stockdb -t public.stock_daily_data exports/stock_daily_data_*.sql
```

### 5. 分批导出版本（如果单次导出失败）
**文件**: `export_stock_daily_data_split.sh`

**特点**:
- 每批导出100万条记录
- 自动压缩每个批次
- 适合内存不足或网络不稳定的情况

**使用方法**:
```bash
cd db
chmod +x export_stock_daily_data_split.sh
./export_stock_daily_data_split.sh
```

## 配置说明

所有脚本都使用以下数据库配置（从 `App.config` 读取）:
- **Host**: localhost
- **Port**: 8532
- **Database**: stockdb
- **User**: postgres
- **Password**: cd123321

如需修改，请编辑对应脚本中的配置变量。

## 导出文件位置

所有导出文件保存在 `db/exports/` 目录下，文件名格式：
- CSV格式: `stock_daily_data_YYYYMMDD_HHMMSS.csv`
- 压缩格式: `stock_daily_data_YYYYMMDD_HHMMSS.csv.gz` 或 `.zip`
- pg_dump格式: `stock_daily_data_YYYYMMDD_HHMMSS.sql`

## 性能优化建议

1. **使用 COPY 命令**（最快，推荐）
   - 比 SELECT 查询快10-100倍
   - 直接写入文件，不经过客户端

2. **压缩导出文件**
   - CSV文件通常可以压缩到原来的10-30%
   - 节省磁盘空间和传输时间

3. **分批导出**（如果遇到内存问题）
   - 使用 `export_stock_daily_data_split.sh`
   - 每批100万条，避免内存溢出

4. **使用 pg_dump**（最可靠）
   - PostgreSQL官方工具
   - 支持断点续传
   - 压缩格式更高效

## 导入数据

### CSV格式导入
```sql
\copy public.stock_daily_data FROM 'exports/stock_daily_data_*.csv' WITH CSV HEADER;
```

### pg_dump格式导入
```bash
pg_restore -h localhost -p 8532 -U postgres -d stockdb -t public.stock_daily_data exports/stock_daily_data_*.sql
```

## 注意事项

1. **磁盘空间**: 确保有足够空间（建议至少50GB）
2. **执行时间**: 2000万条数据导出可能需要30分钟到2小时
3. **网络稳定性**: 如果通过网络导出，确保连接稳定
4. **权限**: 确保数据库用户有 SELECT 权限
5. **文件权限**: 确保导出目录有写入权限

## 故障排除

### 问题1: 导出失败，提示权限不足
**解决**: 检查数据库用户权限
```sql
GRANT SELECT ON public.stock_daily_data TO postgres;
```

### 问题2: 磁盘空间不足
**解决**: 
- 清理临时文件
- 使用压缩导出
- 分批导出

### 问题3: 导出速度慢
**解决**:
- 使用 COPY 命令而不是 SELECT
- 关闭不必要的索引（导出时）
- 增加数据库连接超时时间

### 问题4: 内存不足
**解决**: 使用分批导出脚本 `export_stock_daily_data_split.sh`
