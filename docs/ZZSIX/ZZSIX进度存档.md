# ZZSIX 进度存档

> **文档版本**：2026-09-14  
> **状态**：PACK 六面检 **P0 已落地**（收图监视 + 中央管线 + 模拟视觉 + Pack 汇总 + CSV）；真实 ZZPack.AI / 八窗预览 / PLC 心跳报警待 P1  
> **用途**：新会话 **只拖本文**；规格细节不写在本文，用下方索引跳转  
> **详规**：[ZZSIX项目开发规划.md](./ZZSIX项目开发规划.md)　**版本历史**：[ZZSIX版本变更记录.md](./ZZSIX版本变更记录.md)

**进度图例**：✅ 已完成　🔄 进行中　⏸ 阻塞　❌ 未开始

---

## 0. 文档索引（事实只写一处）

> 进度与待办 **只维护本文**；点位 / 架构 / 阈值 **只维护规划或 CSV**；历史变更 **只维护版本变更记录**。

| 要查什么 | 去哪里（勿在本文重复抄写） |
|----------|---------------------------|
| **版本变更 / 历史决策** | [ZZSIX版本变更记录.md](./ZZSIX版本变更记录.md) |
| 业务范围、本期不做 | 规划 [§1](./ZZSIX项目开发规划.md#1-项目定位与目标) |
| 配置键 / 队列需求 | [pack6配置键.csv](./pack6配置键.csv)、[pack6队列需求.csv](./pack6队列需求.csv) |
| 架构分层 | 规划 [§3](./ZZSIX项目开发规划.md#3-总体架构设计) |
| 风险与待确认 | 规划 [§5](./ZZSIX项目开发规划.md#5-风险与待确认事项) |
| 启用模块 SQL | [enable_zzsix_module.sql](./enable_zzsix_module.sql) |
| **原始开发资料投放** | [开发资料/](./开发资料/) |
| **LrzWork 迁移盘点** | [开发资料/LrzWork迁移分析.md](./开发资料/LrzWork迁移分析.md) |
| 平台新模块约定 | [新增业务模块上手指南](../新增业务模块上手指南.md) |

---

## 1. 当前状态

ZZSIX 范围已定为 **PACK 六面检（枣庄主流程）**。P0：菜单「PACK六面检」可启停；监视 Top/Bottom/Side 目录；模拟引擎跑通 Pack 齐套与 CSV；「模拟一整包」可本地冒烟。

### 待办

| # | 任务 | 状态 | 验收 | 详规索引 |
|---|------|------|------|----------|
| 1 | 确认第一优先工序 = PACK 六面检 | ✅ | 用户确认 | 迁移分析 |
| 2 | P0：收图→管线→模拟检测→汇总→CSV | ✅ | 编译 0 error；模拟一整包出 CSV | 规划 §4 |
| 3 | P1：接入真实 ZZPack.AI / X04C 检项 | ❌ | 上盖至少 1 项真检通过 | 规划 §3 / §4 |
| 4 | P1：八窗图像预览 + 参数窗 | ❌ | 主界面可见实时图 | 规划 §4 |
| 5 | P1：PLC 心跳 / AlarmSignal 写 | ❌ | 键名已登记，地址来自现场配置 | [pack6配置键.csv](./pack6配置键.csv) |
| 6 | P2：南京 Pack6SurfaceNJ | ❌ | 10 窗路由 + 算法 | 规划 §1.3 |
| 7 | 开发资料：现场路径/ONNX/ROI 录入 | ❌ | 规划 §0 标记已录入 | [开发资料/](./开发资料/) |

### 已完成

- ✅ 菜单「PACK六面检」、`VM_ZZSIXPage` / `ZZSIXPage`
- ✅ 服务分层：Config / Ingress / Pipeline / Vision(Sim) / Aggregator / CsvStore
- ✅ `enable_zzsix_module.sql`（`VM_ZZSIXPage`）
- ✅ 配置键 / 队列需求 CSV

### 本期不做（摘要，全文见规划 §1.3）

打胶机族、JSPD、堆叠检验、南京六面检真算法、飞书告警（可后续横切）。

---

## 2. 关键代码入口（精简）

| 职责 | 路径 |
|------|------|
| 主页 VM | `ViewModels/Pages/ZZSIX/VM_ZZSIXPage.cs` |
| 主页 View | `Views/Pages/ZZSIX/ZZSIXPage.xaml` |
| DI | `Services/Project/ZZSIX/ZZSIXServiceCollectionExtensions.cs`（`App.xaml.cs` 注册） |
| 配置 | `Services/Project/ZZSIX/Pack6ConfigService.cs` |
| 收图 | `Services/Project/ZZSIX/Pack6IngressService.cs` |
| 管线 | `Services/Project/ZZSIX/Pack6PipelineService.cs` |
| 视觉（模拟） | `Services/Project/ZZSIX/Pack6SimulationVisionEngine.cs` |
| 汇总 | `Services/Project/ZZSIX/Pack6ProductAggregator.cs` |
| CSV | `Services/Project/ZZSIX/Pack6CsvResultStore.cs` |
| 接口 / 模型 | `Core/Interfaces/Project/ZZSIX/`、`Core/Models/Project/ZZSIX/` |

分层原则见规划 §3。

---

## 3. 下一会话阅读顺序

1. **本文** — 状态、待办、代码入口  
2. P1 接算法 → 规划 §3 + 旧仓 `VM_PagePack6Surface.AiDetectionInImage`  
3. 收尾：更新本文待办；版本变更顶部追加；规格变了再改规划  
