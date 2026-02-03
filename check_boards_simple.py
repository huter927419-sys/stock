#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""检查 Boards.json 中的股票代码是否在 RocksDB 中存在"""

import json
import os
import re
from pathlib import Path

# 配置路径
base_dir = Path(__file__).parent
boards_json_path = base_dir / "bin" / "x64" / "Debug" / "Config" / "Boards.json"
rocksdb_kline_path = base_dir / "bin" / "x64" / "Debug" / "data" / "rocksdb" / "kline"

print("========== 检查 Boards.json 中的股票代码 ==========\n")

# 1. 读取 Boards.json
if not boards_json_path.exists():
    print(f"错误: 找不到文件 {boards_json_path}")
    exit(1)

print(f"正在读取: {boards_json_path}")
with open(boards_json_path, 'r', encoding='utf-8') as f:
    boards = json.load(f)

# 2. 提取所有股票代码（规范化）
all_stock_codes = {}
for board in boards:
    if 'StockCodes' in board and board['StockCodes']:
        for code in board['StockCodes']:
            # 规范化代码：移除 SH/SZ 前缀
            normalized_code = code.strip().upper()
            if normalized_code.startswith('SH') or normalized_code.startswith('SZ'):
                normalized_code = normalized_code[2:]
            
            # 只处理6位数字代码
            if re.match(r'^\d{6}$', normalized_code):
                if normalized_code not in all_stock_codes:
                    all_stock_codes[normalized_code] = {
                        'original_code': code,
                        'boards': []
                    }
                all_stock_codes[normalized_code]['boards'].append(board.get('Name', '未命名板块'))

total_codes = len(all_stock_codes)
print(f"从 Boards.json 提取了 {total_codes} 个唯一股票代码\n")

# 3. 读取 RocksDB 中的股票代码（从 kline 目录的 JSON 文件）
print(f"正在扫描 RocksDB 目录: {rocksdb_kline_path}")
rocksdb_codes = set()
if rocksdb_kline_path.exists():
    json_files = list(rocksdb_kline_path.glob("*.json"))
    for json_file in json_files:
        code = json_file.stem  # 文件名（不含扩展名）
        if re.match(r'^\d{6}$', code):
            rocksdb_codes.add(code)
else:
    print(f"警告: RocksDB 目录不存在: {rocksdb_kline_path}")
    print("请确认 RocksDBPath 配置正确")

rocksdb_count = len(rocksdb_codes)
print(f"RocksDB 中找到 {rocksdb_count} 个股票代码文件\n")

# 4. 检查每个代码
found_codes = []
not_found_codes = []

for code, info in sorted(all_stock_codes.items()):
    if code in rocksdb_codes:
        found_codes.append({
            'code': code,
            'original_code': info['original_code'],
            'boards': ', '.join(info['boards'])
        })
    else:
        not_found_codes.append({
            'code': code,
            'original_code': info['original_code'],
            'boards': ', '.join(info['boards'])
        })

# 5. 显示结果
print("========== 检查结果 ==========\n")
found_count = len(found_codes)
not_found_count = len(not_found_codes)
found_pct = (found_count / total_codes * 100) if total_codes > 0 else 0
not_found_pct = (not_found_count / total_codes * 100) if total_codes > 0 else 0

print(f"已找到: {found_count} 个 ({found_pct:.2f}%)")
print(f"不存在: {not_found_count} 个 ({not_found_pct:.2f}%)")
print()

if not_found_codes:
    print("========== 不存在的股票代码（前50个） ==========")
    print(f"{'代码':<10} | {'原始代码':<14} | 板块")
    print("-" * 80)
    for item in not_found_codes[:50]:
        print(f"{item['code']:<10} | {item['original_code']:<14} | {item['boards']}")
    if len(not_found_codes) > 50:
        print(f"... 还有 {len(not_found_codes) - 50} 个")
    print()

if found_codes:
    print("========== 已找到的股票代码（前20个） ==========")
    print(f"{'代码':<10} | {'原始代码':<14} | 板块")
    print("-" * 80)
    for item in found_codes[:20]:
        print(f"{item['code']:<10} | {item['original_code']:<14} | {item['boards']}")
    if len(found_codes) > 20:
        print(f"... 还有 {len(found_codes) - 20} 个")
    print()

print("检查完成！")
