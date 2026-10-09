# ZZSIX PLC 配置说明（欧姆龙 Fins TCP）

## 1. 设备参数

| 项 | 值 |
|----|-----|
| 平台表 | `plc_config` |
| 设备名 | `六面检PLC`（与 `project_setting.PlcDeviceName` 一致） |
| 协议 | `Omron_Fins_TCP` |
| IP | `192.168.7.40` |
| 端口 | `9600` |

### D1996 位定义

| 位 | 方向 | 用途 |
|----|------|------|
| `D1996.0` | 上位机 → PLC | **报警**：开缺陷窗 `true`，全部关窗 `false` |
| `D1996.1` | 上位机 → PLC | **本机心跳**：约每秒翻转一次（true↔false） |
| `D1996.2` | PLC → 上位机 | **对方心跳**：PLC 侧同样每秒翻转；上位机读跳变 |

## 2. 执行 SQL

脚本：[`seed_zzsix_plc.sql`](./seed_zzsix_plc.sql)

```text
在 MySQL（sunwoda_demo）执行 seed_zzsix_plc.sql
→ 完整退出 SEVB → 再启动
```

模块启用仍用 [`enable_zzsix_module.sql`](./enable_zzsix_module.sql)。

## 3. 程序行为

1. 顶栏「PLC通讯状态」：绿点**呼吸灯**（约 1s 明暗）；对方 `.2` **5 秒内无跳变** → 红点常亮「未连接」。  
2. 心跳循环约 **500ms** 一轮：到 1s 间隔翻转写 `.1`；每轮读 `.2` 看是否变。  
3. 整包检测结束且 NG（含缺张）→ 弹「Pack六面检测缺陷报警」。  
4. 每开一扇窗 → 写 `D1996.0 = true`。  
5. 扫码与产品 SN 一致 → 关窗；**全部窗关闭后**写 `D1996.0 = false`。  
6. 报警非阻塞，检测继续。

配置键（`project_setting`）：`PlcNgAlarmBitAddress` / `PlcHeartbeatOutBitAddress` / `PlcHeartbeatInBitAddress` / `PlcHeartbeatIntervalMs` / `PlcHeartbeatTimeoutMs`。

## 4. 核对

```sql
SELECT name, Ip, port, brand_specification_protocal, is_enable
FROM plc_config WHERE name = '六面检PLC';
```

HSL 测试工具：协议选欧姆龙 Fins TCP；可读/写 `D1996.0`～`D1996.2`（bool）或读字 `D1996`。
