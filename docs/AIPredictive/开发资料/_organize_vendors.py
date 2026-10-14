# -*- coding: utf-8 -*-
"""整理先导/赢合开发资料（不改业务代码）。"""
from pathlib import Path
import re
import csv
import shutil
from collections import defaultdict

base = Path(__file__).resolve().parent
docs = base.parent

xd = base / "先导"
yh = base / "赢合"
xd.mkdir(exist_ok=True)
yh.mkdir(exist_ok=True)

for src_name, dst_name in [
    ("先导_伺服.csv", "先导_伺服.csv"),
    ("先导_气缸.csv", "先导_气缸.csv"),
]:
    src = base / src_name
    dst = xd / dst_name
    if src.exists():
        shutil.move(str(src), str(dst))
        print("moved", src_name)

for alias, src in [
    ("AIPredictive采集块_伺服.csv", xd / "先导_伺服.csv"),
    ("AIPredictive采集块_气缸.csv", xd / "先导_气缸.csv"),
]:
    if src.exists():
        shutil.copy2(src, base / alias)
        print("alias", alias)

for src_name, dst_name in [
    ("赢合伺服.txt", "原始_赢合伺服.txt"),
    ("赢合机台状态.txt", "原始_赢合机台状态.txt"),
]:
    src = base / src_name
    if src.exists():
        shutil.move(str(src), str(yh / dst_name))
        print("moved", src_name)

raw_servo = yh / "原始_赢合伺服.txt"
if not raw_servo.exists():
    raise SystemExit(f"missing {raw_servo}")

points = []
for line in raw_servo.read_text(encoding="utf-8-sig").splitlines():
    if not line.strip():
        continue
    parts = line.split("\t")
    if len(parts) < 7:
        continue
    plc, proto, ip, port, name, addr, typ = [x.strip() for x in parts[:7]]
    if not addr or not name:
        continue
    points.append(
        {
            "plc": plc or "欧姆龙NX701-1700",
            "proto": proto or "TCP/IP",
            "ip": ip or "10.147.72.15",
            "port": port or "8500",
            "name": name,
            "addr": addr,
            "type": typ or "real",
        }
    )

flat_path = yh / "赢合_点位表_伺服.csv"
with flat_path.open("w", encoding="utf-8-sig", newline="") as f:
    w = csv.writer(f)
    w.writerow(
        [
            "厂家",
            "PLC型号",
            "协议原文",
            "IP",
            "端口",
            "参数名称",
            "参数地址",
            "原始类型",
            "平台类型",
            "数组根符号",
            "数组下标",
            "说明",
        ]
    )
    for p in points:
        m = re.match(r"^([A-Za-z_][\w.]*)\[(\d+)\]$", p["addr"])
        root = m.group(1) if m else ""
        idx = m.group(2) if m else ""
        plat = "FLOAT" if p["type"].lower() in ("real", "float", "lreal") else p["type"].upper()
        w.writerow(
            [
                "赢合",
                p["plc"],
                p["proto"],
                p["ip"],
                p["port"],
                p["name"],
                p["addr"],
                p["type"].upper(),
                plat,
                root,
                idx,
                "最大扭矩",
            ]
        )
print("flat points", len(points))

groups = defaultdict(list)
for p in points:
    m = re.match(r"^([A-Za-z_][\w.]*)\[(\d+)\]$", p["addr"])
    if not m:
        continue
    groups[m.group(1)].append((int(m.group(2)), p))

block_rows = []
for root, items in groups.items():
    items = sorted(items, key=lambda x: x[0])
    idxs = [i for i, _ in items]
    start, end = min(idxs), max(idxs)
    span = end - start + 1
    miss = [str(i) for i in range(start, end + 1) if i not in set(idxs)]
    listing = ";;".join(f"{i}|{p['name']}|" for i, p in items)
    ip = items[0][1]["ip"]
    port = items[0][1]["port"]
    plc = items[0][1]["plc"]
    suggest_len = end + 1 if start >= 1 else span
    block_rows.append(
        {
            "块ID": f"ARR_{root.replace('.', '_')}",
            "采集方式": "ArrayBatch",
            "符号名": root,
            "厂家": "赢合",
            "PLC型号": plc,
            "协议": "CIP",
            "IP": ip,
            "端口": port,
            "原始类型": "REAL",
            "平台类型": "FLOAT",
            "起始下标": start,
            "终止下标": end,
            "跨度元素数": span,
            "有效点数": len(items),
            "缺失下标": ",".join(miss),
            "建议读取长度_元素": suggest_len,
            "建议读取字节数": suggest_len * 4,
            "成员或元素清单": listing,
            "说明": (
                f"连读：符号名+长度；清单=下标|参数名称|；"
                f"原始端口资料={port}，CIP标准口44818待核实；数组下标从{start}起"
            ),
        }
    )

headers = [
    "块ID",
    "采集方式",
    "符号名",
    "厂家",
    "PLC型号",
    "协议",
    "IP",
    "端口",
    "原始类型",
    "平台类型",
    "起始下标",
    "终止下标",
    "跨度元素数",
    "有效点数",
    "缺失下标",
    "建议读取长度_元素",
    "建议读取字节数",
    "成员或元素清单",
    "说明",
]
block_path = yh / "赢合_采集块_伺服.csv"
with block_path.open("w", encoding="utf-8-sig", newline="") as f:
    w = csv.DictWriter(f, fieldnames=headers, lineterminator="\n")
    w.writeheader()
    for r in block_rows:
        w.writerow(r)
print("blocks", [(r["符号名"], r["有效点数"]) for r in block_rows])

status_raw = (yh / "原始_赢合机台状态.txt").read_text(encoding="utf-8-sig").splitlines()
status_path = yh / "赢合_机台状态.csv"
with status_path.open("w", encoding="utf-8-sig", newline="") as f:
    w = csv.writer(f)
    w.writerow(
        [
            "厂家",
            "线体",
            "工序",
            "PLC型号",
            "协议原文",
            "IP",
            "端口",
            "参数名称",
            "参数地址",
            "参数类型",
            "状态码",
            "状态含义",
            "备注",
        ]
    )
    if len(status_raw) >= 2:
        cols = status_raw[1].split("\t")
        remark = cols[20] if len(cols) > 20 else ""

        def c(i):
            return cols[i] if len(cols) > i else ""

        w.writerow(
            [
                "赢合",
                c(0),
                c(1),
                c(4),
                c(5),
                c(6),
                c(7),
                c(8),
                c(9),
                c(10),
                "7",
                "运行",
                remark,
            ]
        )
        for code, meaning in [("18", "停机"), ("19", "待机"), ("9", "故障")]:
            w.writerow(
                [
                    "赢合",
                    c(0),
                    c(1),
                    c(4),
                    c(5),
                    c(6),
                    c(7),
                    c(8),
                    c(9),
                    c(10),
                    code,
                    meaning,
                    "",
                ]
            )

(base / "两厂家对照说明.md").write_text(
    """# 先导 / 赢合 采集资料对照（仅整理，未改程序）

> 录入日期：2026-09-29  
> 程序当前仍只加载根目录别名 `AIPredictive采集块_伺服/气缸.csv`（内容=先导）。赢合采集块已整理，**切换厂家需后续改代码/界面选择**。

## 1. 目录

| 路径 | 说明 |
|------|------|
| [先导/先导_伺服.csv](./先导/先导_伺服.csv) | 先导扭矩采集块 |
| [先导/先导_气缸.csv](./先导/先导_气缸.csv) | 先导气缸（本期空表头） |
| [AIPredictive采集块_伺服.csv](./AIPredictive采集块_伺服.csv) | **程序用别名** → 先导伺服 |
| [AIPredictive采集块_气缸.csv](./AIPredictive采集块_气缸.csv) | **程序用别名** → 先导气缸 |
| [赢合/原始_赢合伺服.txt](./赢合/原始_赢合伺服.txt) | 赢合原始伺服点表 |
| [赢合/原始_赢合机台状态.txt](./赢合/原始_赢合机台状态.txt) | 赢合原始状态点 |
| [赢合/赢合_点位表_伺服.csv](./赢合/赢合_点位表_伺服.csv) | 赢合扁平点位（275 点） |
| [赢合/赢合_采集块_伺服.csv](./赢合/赢合_采集块_伺服.csv) | 赢合采集块草案（2×ArrayBatch） |
| [赢合/赢合_机台状态.csv](./赢合/赢合_机台状态.csv) | 赢合状态码表 |

## 2. 关键差异

| 项 | 先导（当前在采） | 赢合（已整理待接） |
|----|------------------|-------------------|
| IP | `10.147.72.10` | `10.147.72.15` |
| 端口（资料） | **44818**（现场 CIP 实测） | 资料写 **8500**（Tag Data Link）⏸ 是否等同 CIP 44818 待现场核实 |
| 协议 | CIP 符号 | 资料：Tag Data Link / TCP/IP；整理时按 **CIP 符号** 口径 |
| 扭矩符号 | `ar_rIOT6FromPLC2[n]` + `stEM*.MAx.Sta.rMaxTrq` | `axisTrq.max[n]`（201）+ `axisTrqMQ.max[n]`（74） |
| 扭矩点数 | 230 | **275**（201+74） |
| 机台状态标签 | `AndonRunStatu` | `Andon_RunStatu[0]` |
| 状态码 | 7运行 / 18停机 / 19待机 / 9故障 | **相同** |

## 3. 赢合数组缺失下标（资料原样）

- `axisTrq.max`：1..230，有效 201，缺：43-50, 92-100, 142-150, 200, 212, 214  
- `axisTrqMQ.max`：1..90，有效 74，缺：4, 24, 32, 41-50, 54, 74, 82  

## 4. 后续程序改造建议（本轮不做）

1. 界面「厂家」下拉：先导 / 赢合  
2. 按厂家加载对应采集块 CSV + 状态标签  
3. 赢合端口以现场实测为准（8500 vs 44818）  
4. 数组下标从 1 起：连读长度与偏移需现场探针确认  
""",
    encoding="utf-8",
)

(base / "README.md").write_text(
    """# AIPredictive 开发资料

原始资料与整理后的采集真相。**程序当前加载**：根目录 `AIPredictive采集块_*.csv`（先导别名）。

## 目录

| 路径 | 说明 |
|------|------|
| [两厂家对照说明.md](./两厂家对照说明.md) | 先导 vs 赢合差异与待核实项 |
| [先导/](./先导/) | 先导采集块（伺服扭矩 / 气缸空） |
| [赢合/](./赢合/) | 赢合原始 txt + 整理后的点位/采集块/状态 |
| [备份_全量采集块_20260914/](./备份_全量采集块_20260914/) | 先导全量备份（切扭矩前） |
| `AIPredictive采集块_伺服.csv` / `_气缸.csv` | 程序用别名（=先导） |

## 状态

- 先导：已在产采集（扭矩子集）  
- 赢合：资料已整理，**未接程序**；切换厂家见对照说明 §4  
""",
    encoding="utf-8",
)

for p in docs.glob("_preview_*"):
    p.unlink(missing_ok=True)
for name in ("_list_utf8.txt", "_yh_analysis.txt"):
    (docs / name).unlink(missing_ok=True)

print("DONE")
for p in sorted(base.rglob("*")):
    if p.is_file() and "备份_全量" not in p.as_posix() and p.name != "_organize_vendors.py":
        print(p.relative_to(base).as_posix(), p.stat().st_size)
