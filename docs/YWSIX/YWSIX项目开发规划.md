# YWSIX 项目开发规划

> 文档版本：2026-10-12（项目初始化：骨架落地，业务范围待资料）  
> **角色：冷层详规** — 范围、架构、资料说明；**不要**在本文维护每日进度  
> **进度 / 待办 / 索引**：[YWSIX进度存档.md](./YWSIX进度存档.md)（新会话只拖进度存档）  
> 平台：SUNWODA SEVB（`develop`，.NET 8 WPF）  
> 维护人：接手开发者

---

## 0. 开发资料录入（工作流）

> **状态**：🔄 已投放部分资料（文件夹结构 + 视觉算法文档）；PLC/工艺阈值仍待录入  
> **投放路径**：[开发资料/](./开发资料/) —— 现场 PLC 点位表、设备信息、工艺阈值原始表/截图、对接说明、接口文档放这里  
> **录入方式**：资料拷入后在 `/swd-workflow` 点「开始录入」，由 Agent 读取并整理进本规划对应章节与 CSV（CSV 放 `docs/YWSIX/` 根下，本文与进度存档 §0 建索引）  
> **标识文件**：`开发资料/.swd-materials-status`（由工作流写入 `ingested` / `skipped`；重新打开后可 `pending` + `mode=update`）

**录入后应更新**：§1 业务定位与范围、§2 现有资料盘点、§3 架构落点（如涉 PLC/算法）、进度存档 §0 索引。

---

## 1. 项目定位与目标

### 1.1 业务定位

**YWSIX** = **PACK 六面检（义乌）** 上位机模块。  
收图：单一 UNC `SaveImage\SaveImage\{日期}\{SN}\{相机}\原始图像\`；**6 逻辑相机、合计 31 张 OK**。  
算法资料：`YWX01Pack`（白标签四码 / 铭牌 / 上盖 YOLO / 连接器 PIN）；**第二步已接入真 DLL**（`UseSimulation` 可关回模拟）。  
上盖：顶面张序 **仅 1～4**；类别数量 **必须相等**（见 `Pack6TopCoverExpectations`）；5=拼接跳过、6=不检。  
界面形态对齐 **NJSIX**（左预览+日志，右功能/统计）。

### 1.2 本期范围

| 能力 | 是否本期 | 说明 |
|------|----------|------|
| 菜单骨架页 | ✅ 已做 | 项目初始化 demo |
| 界面 / 收图管线（照抄 NJSIX） | ✅ 已做 | 6 窗 + SaveImage Ingress |
| 视觉 DLL `YWX01Pack` | ✅ 已做 | 机位路由 + 上盖 1～4 等量判定 + PIN 阈值可配 |
| PLC 读写 / 采集 | ⏸ 待确认 | 无点位表，禁止臆造 |
| 工艺阈值 CSV | ⏸ 部分 | PIN/版本已进配置 UI；正式 CSV 待现场 |
| MES / 上报 | ⏸ 待确认 | 是否对接未知 |

### 1.3 明确不做（本期）

- 不在资料缺失时臆造 PLC 地址、IP、工艺阈值、算法接口字段（规范 `[P0][B-09]`）
- 不移植旧框架单体 ViewModel（UI / 采集 / 落盘 / 通信堆一处的写法）
- 不实现未确认的 MES 上报、报警中心、看门狗联动
- View / code-behind **不直接**读写 PLC、Socket、数据库（新代码，规范 §2 分层）

---

## 2. 现有资料盘点

| 资料 | 位置 | 状态 |
|------|------|------|
| 现场原始资料（PLC / 设备 / 工艺 / 对接） | [开发资料/](./开发资料/) | ⏸ 待投放 |
| PLC 点位表 CSV | `docs/YWSIX/plc点位表.csv` | ❌ 未建立（无资料） |
| 设备信息 CSV | `docs/YWSIX/plc设备信息.csv` | ❌ 未建立（无资料） |
| 工艺阈值表 CSV | `docs/YWSIX/工艺阈值表.csv` | ❌ 未建立（无资料） |
| 项目配置键 CSV | `docs/YWSIX/配置键.csv` | ❌ 未建立（无资料） |

**平台内可参照的成型样板**（读取方式方法时用，勿照抄业务口径）：

| 样板 | 可借鉴之处 |
|------|------------|
| [BoxGluing](../BoxGluing/) | PLC 周期采集、曲线显示、按出胶分文件 CSV、配置服务 + DI 注册 |
| [NJSIX](../NJSIX/) / [ZZSIX](../ZZSIX/) | 收图管线、相机窗、算法 DLL 接入、规则校验、结果 CSV 与统计 |
| [CTNCLX/NCLCCT](../项目架构说明.md) | 云端算法 DLL 按需拉取（SysCenter）与本地优先策略 |

---

## 3. 总体架构设计与落点

| 层 | 位置 | 职责 |
|----|------|------|
| ViewModel | `SUNWODA_SEVB/ViewModels/Pages/YWSIX/` | 界面状态、绑定属性、生命周期（读数据库/刷新界面） |
| View | `SUNWODA_SEVB/Views/Pages/YWSIX/` | XAML 布局；code-behind 仅保留必要交互 |
| 项目服务 | `SUNWODA_SEVB/Services/Project/YWSIX/`（未创建） | 采集、通信、算法、落盘等重逻辑 |
| 接口 / 模型 | `SUNWODA_SEVB.Core/Interfaces/Project/YWSIX/`、`Core/Models/Project/YWSIX/`（未创建） | 契约与数据模型 |
| DI 注册 | `SUNWODA_SEVB/Services/ProjectServiceCollectionExtensions.cs` | 项目服务注册入口 |
| 配置 | `project_setting` 表（`BelongToVM = VM_YWSIXPage`） | 页面级 JSON 配置 |

**命名约定**（详见 [新增业务模块上手指南](../新增业务模块上手指南.md)）：VM 类 `VM_YWSIXPage`，View 类 `YWSIXPage`，`x:Class` 与 `partial class` 必须完全一致。

---

## 4. 开发计划与验收

| 阶段 | 内容 | 验收 | 状态 |
|------|------|------|------|
| **P0 骨架** | VM/View 骨架、`[Module]`、三文档、`开发资料/`、enable SQL 执行 | 本地编译 0 error；完整重启后左侧菜单出现「YWSIX」并可进空页 | ✅ |
| **P1 资料与范围** | 投放现场资料 → 工作流录入 → 确认业务范围 | 规划 §0/§1/§2 填写；点位/阈值 CSV 建立 | ⏸ |
| **P2 首个业务功能** | 依确认范围实现（VM + View + 服务分层） | 功能可演示，写入版本变更记录 | ❌ |
| **P3 联调** | 真机/现场联调与数据抽检 | 结果与现场口径一致 | ❌ |

> 每阶段结束：更新进度存档 → 版本变更记录顶部追加 → 规格变化才改本文。

---

## 5. 风险与待确认事项

| # | 事项 | 状态 |
|---|------|------|
| 1 | 业务范围 / 工序对象 / 验收口径未确认 | ⏸ 需负责人说明 |
| 2 | 现场原始资料未投放（PLC 点位、设备、工艺阈值、对接说明） | ⏸ 需现场提供 |
| 3 | 算法/视觉对接方与接口（是否需 DLL / 云端拉取） | ⏸ 未知 |
| 4 | 目标数据库与现场库名（本机按平台默认 `sunwoda_demo` 执行） | ⏸ 现场部署前确认 |
| 5 | 是否需要 MES / Web 上报 | ⏸ 未知 |
| 6 | 菜单显示名是否改用简短中文（当前为 `YWSIX`） | ⏸ 可按需调整 |

---

## 6. 模块启用

- SQL：[enable_ywsix_module.sql](./enable_ywsix_module.sql)
- `vm_class_name`：`VM_YWSIXPage`
- 菜单显示名：`YWSIX`
- 库：`sunwoda_demo`（以 `appsettings.json` 的 `ConnectionStrings:DefaultConnection` 为准；仓库内暂无该文件，按平台默认值）
- 主窗口：`AppStartMainWindow = MainWindow`（通用壳才会显示所有已启用模块）
- 生效方式：**完整退出 SEVB 应用后重新启动**，再进左侧菜单
