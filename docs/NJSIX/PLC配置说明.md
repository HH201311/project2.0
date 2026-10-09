# NJSIX PLC 配置说明

> **2026-09-28**：南京现场**暂无报警 / 心跳**。`Pack6PlcAlarmService` 为空实现；主界面不显示 PLC 状态、不弹 NG/缺张报警窗。下列点位仅作日后恢复参考。

## 1. 设备参数（保留，当前不启用）

| 项 | 值 |
|----|-----|
| 平台表 | `plc_config` |
| 设备名 | `六面检PLC`（与 `project_setting.PlcDeviceName` 一致） |
| 协议 | `Omron_Fins_TCP` |
| IP | `192.168.7.40`（⏸ 南京实址待确认） |
| 端口 | `9600` |

### D1996 位定义（参考）

| 位 | 方向 | 用途 |
|----|------|------|
| `D1996.0` | 上位机 → PLC | 报警位（开窗 true / 全关 false） |
| `D1996.1` | 上位机 → PLC | 本机心跳翻转 |
| `D1996.2` | PLC → 上位机 | 对方心跳跳变 |

## 2. SQL

- 模块启用：[`enable_njsix_module.sql`](./enable_njsix_module.sql)（本机只开 `VM_NJSIXPage`）
- PLC 种子（可选，当前可不执行）：[`seed_njsix_plc.sql`](./seed_njsix_plc.sql)

## 3. 当前程序行为

1. **不探活、不写位、不弹报警窗**。  
2. NG / 缺张仅写入结果日志与预览，不触发 PLC。  
3. 配置键仍保留在 `project_setting`，便于日后恢复。

## 4. 日后恢复时

恢复 `Pack6PlcAlarmService` 真写实现 + 顶栏 PLC 灯 + NG 报警窗；再执行 seed 并核对：

```sql
SELECT name, Ip, port, brand_specification_protocal, is_enable
FROM plc_config WHERE name = '六面检PLC';
```
