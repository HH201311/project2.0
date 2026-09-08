# BoxGluing 进度存档

> **文档版本**：2026-09-08  
> **状态**：过程量地址已按现场 PLC 点位纠正；继续现场联调抽检  
> **用途**：新会话 **只拖本文**；规格细节不写在本文，用下方索引跳转  
> **详规**：[BoxGluing项目开发规划.md](./BoxGluing项目开发规划.md)　**版本历史**：[BoxGluing版本变更记录.md](./BoxGluing版本变更记录.md)

**进度图例**：✅ 已完成　🔄 进行中　⏸ 阻塞　❌ 未开始

---

## 0. 文档索引（事实只写一处）

> 进度与待办 **只维护本文**；点位 / 架构 / 阈值 **只维护规划或 CSV**；历史变更 **只维护版本变更记录**。

| 要查什么 | 去哪里（勿在本文重复抄写） |
|----------|---------------------------|
| **版本变更 / 历史决策** | [BoxGluing版本变更记录.md](./BoxGluing版本变更记录.md) |
| 业务范围、本期不做 | 规划 [§1](./BoxGluing项目开发规划.md#1-项目定位与目标) |
| PLC 设备 / 过程量 / 启动 / 反馈 | [plc点位表.csv](./plc点位表.csv)、[plc设备信息.csv](./plc设备信息.csv)；规划 [§2.3](./BoxGluing项目开发规划.md#23-plc-点位表plc1已归档) |
| 工艺阈值数值 | [工艺阈值表.csv](./工艺阈值表.csv)；规划 [§2.2](./BoxGluing项目开发规划.md#22-工艺阈值表图片资料) |
| 过程量 vs 工艺阈值 vs PLC 限值 | 规划 [§2.3.5](./BoxGluing项目开发规划.md#235-过程量--工艺阈值--plc-限值--三者区分上线前再选型) |
| 采集状态机 / PerLane | 规划 [§4](./BoxGluing项目开发规划.md#4-数据采集设计) |
| CSV 列与目录规则 | 规划 [§5](./BoxGluing项目开发规划.md#5-csv-存储设计) |
| 曲线方案（ScottPlot） | 规划 [§6](./BoxGluing项目开发规划.md#6-曲线显示方案选型) |
| 架构分层 / 服务清单 | 规划 [§3](./BoxGluing项目开发规划.md#3-总体架构设计) |
| 风险与待确认 | 规划 [§10](./BoxGluing项目开发规划.md#10-风险与待确认事项) |
| 启用模块 SQL | [enable_boxgluing_module.sql](./enable_boxgluing_module.sql) |
| 平台新模块约定 | [新增业务模块上手指南](../新增业务模块上手指南.md) |

---

## 1. 当前状态

进页即监听、PerLane 采集、三出胶实时曲线、按出胶分 CSV 已落地。**入口压力/伺服扭矩地址已按现场 PLC 表纠正（压力 D1600～、扭矩 D1608～）**。下一步：工艺阈值水平参考线 + 现场复测曲线/CSV。

**待确认**：阈值线来源（工艺表 / PLC 限值 D1635～D1638 / 双源）— 见规划 §2.3.5。

### 待办

| # | 任务 | 状态 | 验收 | 详规索引 |
|---|------|------|------|----------|
| 1 | 工艺阈值水平参考线 | ❌ | 三图显示压力 70/85、扭矩 25、液位 10/60 | [工艺阈值表.csv](./工艺阈值表.csv)、规划 §2.2 / §6 |
| 2 | 采集耗时 / 丢点统计 | ❌ | 日志或 UI 可观测读块耗时 | 规划 §4 |
| 3 | `IBoxGluingAlgorithmService` 空接口 | ❌ | Core 接口 + NoOp 注册 | 规划 §8 |
| 4 | 现场 PLC 曲线 / CSV 抽检 | 🔄 | 地址纠正后复测真机一轮 | 规划 §5、§9 阶段 D；点位见 [plc点位表.csv](./plc点位表.csv) |
| 5 | 80ms 块读压测 | ❌ | Fins/TCP 无丢点 | 规划 §10 #4 |
| 6 | 阈值线来源确认 | ⏸ | 问负责人 | 规划 §2.3.5 |

### 已完成（P0）

- ✅ 菜单「口琴管涂胶监测」、DI、`project_setting`、PerLane、模拟出胶
- ✅ 三图 ScottPlot（6 线/图，Y -30～100）、按出胶分 CSV
- ✅ PLC 种子：`口琴管涂胶PLC`，`Omron_Fins_TCP`，`192.168.3.220:9600`
- ✅ 2026-09-08：按现场表纠正入口压力↔伺服扭矩地址（见版本变更记录）

### 本期不做（摘要，全文见规划 §1.3）

Python 算法、NG 回写 D1623～D1638、工位 4 / 液位 A2B2、MES、报警中心。

---

## 2. 关键代码入口（精简）

| 职责 | 路径 |
|------|------|
| 监测页 VM | `ViewModels/Pages/BoxGluing/VM_BoxGluingMonitoringPage.cs` |
| 监测页 View / Plot | `Views/Pages/BoxGluing/BoxGluingMonitoringPage.xaml`、`BoxGluingPlotHelper.cs` |
| 采集 | `Services/Project/BoxGluing/BoxGluingAcquisitionService.cs` |
| CSV | `Services/Project/BoxGluing/BoxGluingCsvStorage.cs` |
| 曲线缓冲 | `Services/Project/BoxGluing/BoxGluingChartDataService.cs` |
| 配置 | `Services/Project/BoxGluing/BoxGluingConfigService.cs` |
| DI | `Services/Project/BoxGluing/BoxGluingServiceCollectionExtensions.cs` |
| 接口 / 模型 | `Core/Interfaces/Project/BoxGluing/`、`Core/Models/Project/BoxGluing/` |

分层原则见规划 §3.1（勿在本文展开）。

---

## 3. 下一会话阅读顺序

1. **本文** — 状态、待办、代码入口  
2. 按任务打开 §0 索引中的规划章节或 CSV（例：画阈值线 → 工艺阈值表 + 规划 §6）  
3. 改对应代码入口表中的文件  
4. 收尾：**先更新本文**（勾待办）；在 [版本变更记录](./BoxGluing版本变更记录.md) **顶部追加一条**；规格变了再改规划对应节
