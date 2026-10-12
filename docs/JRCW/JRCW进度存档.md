# JRCW 进度存档

> **文档版本**：2026-10-09  
> **状态**：**本次补齐标准三文档**（此前 `docs/JRCW/` 不存在）；代码已成型（单项目多业务工厂模式 + Python 算法），**点位地址与现场真值未录入**  
> **用途**：新会话 **只拖本文**；规格细节不写在本文，用下方索引跳转  
> **详规**：[JRCW项目开发规划.md](./JRCW项目开发规划.md)　**版本历史**：[JRCW版本变更记录.md](./JRCW版本变更记录.md)

**进度图例**：✅ 已完成　🔄 进行中　⏸ 阻塞　❌ 未开始

---

## 0. 文档索引（事实只写一处）

> 进度与待办 **只维护本文**；点位 / 架构 / 参数 **只维护规划或 CSV**；历史变更 **只维护版本变更记录**。
> ⚠️ 规划中列出的参数默认值均**抄自源码**（`Business1ParamModel` / `Business2ParamModel` 字段默认值），**未经现场工艺确认**；确认后请以现场 CSV 为唯一真相。

| 要查什么 | 去哪里（勿在本文重复抄写） |
|----------|---------------------------|
| **版本变更 / 历史决策** | [JRCW版本变更记录.md](./JRCW版本变更记录.md) |
| 业务范围、本期不做、验收边界 | 规划 [§1](./JRCW项目开发规划.md#1-项目定位与目标) |
| **PLC 设备 / 点位名 / 下发名 / 触发信号** | 规划 [§4](./JRCW项目开发规划.md#4-plc-与数据采集设计)（**⚠️ 地址全部未录入 ⏸**） |
| **参数键 / 参数模型 / 存储类型** | 规划 [§5](./JRCW项目开发规划.md#5-参数配置与持久化) |
| 架构分层 / 服务清单 / DI | 规划 [§3](./JRCW项目开发规划.md#3-总体架构设计) |
| 业务一（义乌C05）/ 业务二（德阳C04）数据流与算法 | 规划 [§6](./JRCW项目开发规划.md#6-两条业务的数据流与算法) |
| 落盘与文件输出 | 规划 [§7](./JRCW项目开发规划.md#7-落盘与文件输出) |
| 风险与待确认 | 规划 [§10](./JRCW项目开发规划.md#10-风险与待确认事项) |
| **原始开发资料投放** | [开发资料/](./开发资料/)（**当前为空，等现场 PLC 表 / 算法说明**） |
| 平台新模块约定 | [新增业务模块上手指南.md](../新增业务模块上手指南.md) |

**本项目相关非标准文档（一律保留，勿删）**：

| 文档 | 说明 |
|------|------|
| [项目架构说明.md §7.2](../项目架构说明.md) | 平台侧对 JRCW 服务的摘要（⚠️ 其中写 `DYC01BusinessService`，实际代码为 `DYC04BusinessService`，见规划 §10 #1） |
| [接手任务规划.md](../接手任务规划.md) | 接手全局备忘（§5.2 业务模块一览含 JRCW 行） |
| [代码开发规范.md](../代码开发规范.md) | 写码红线与审查标准 |
| [仓库代码审查/](../仓库代码审查/) | 一键审查流程与案例 |

---

## 1. 当前状态

**JRCW = 卷绕极耳错位闭环，一个项目下挂两条已实现业务**（工厂 + 管理器模式）：

| 业务（`EBusinessType`） | 服务实现 | 中文名（`BusinessName`） | 数据来源 |
|------------------------|----------|------------------------|----------|
| `BusinessType1` | `YWC05BusinessService` | 义乌C05 | PLC 触发 + 错位量/变径位置 + Python 算法 |
| `BusinessType2` | `DYC04BusinessService` | 德阳C04 | PLC 触发 + 极耳边距/CCD + Python 算法 |
| `BusinessType3` | ❌ **无实现** | 德阳C01（仅中文名保留） | 参数持久化分支已注释，未接入 |

**已具备**：`EBusinessType` / `IBusinessService` / `BusinessServiceBase<T>` / 工厂 / 管理器 / DI 就位；`[Module]` 页面 `VM_JRCWClosedLoopPage`（实时参数表 + 设备信息 + 日志）；`JRCWCsvHelper`（落盘 / 按列值查询）；`ProjectSettingExtension` 的 JRCW 参数区（约 1098 行起）；Core 侧算法入参/出参模型齐备。

**关键现状（阻断项）**：

- ⏸ **PLC 点位地址全部未录入**：两条业务按**点位名**读写（如 `GetPLCAddressValue("正极错位量")`），**具体地址未知** → 见规划 §4
- ⏸ **PLC 设备记录未确认**：`JRCWCore.PLCName = "卷绕极耳错位闭环PLC"`，仓库内无该 PLC 的种子/迁移，需现场确认存在
- ⏸ **Python 算法依赖未验证**：`BusinessType2` 走 `PythonConfigHelper` + `PyModule.FromString("Judge", pyCode)`，算法名来自配置（默认 `MIS_JUDGE_C04_325,PRESS_C04_325`）
- ❌ **`BusinessType3`（德阳C01）是空档**：无服务实现，`BusinessServiceBase` 的加载/保存分支被注释
- ⚠️ **DI 重复注册**：`AddJRCWServices()` 中 `DYC04BusinessService` 被注册两次
- ❌ **无 `enable_jrcw_module.sql`**：菜单启用方式待确认
- ⏸ **无现场 CSV**：`docs/JRCW/` 根下暂无点位表 / 设备信息 / 工艺阈值表（本次仅建档）

### 待办

| # | 任务 | 状态 | 验收 | 详规索引 |
|---|------|------|------|----------|
| 1 | **录入 PLC 点位表**（地址 / 类型 / 设备） | ⏸ | 新建 `plc点位表.csv` 并填实 | §4、§10 #2 |
| 2 | 确认 PLC 设备记录 `卷绕极耳错位闭环PLC` | ⏸ | 现场 `plc_config` 存在且已连接 | §4.1 |
| 3 | 确认菜单启用方式（是否需要 enable SQL） | ❌ | 重启后左侧菜单可见并可进页 | §8 |
| 4 | `BusinessType3`（德阳C01）落地或明确不做 | ❌ | 有服务实现 + 参数持久化；或从枚举/UI 移除 | §10 #3 |
| 5 | 修复 DI 重复注册 `DYC04BusinessService` | ❌ | `AddJRCWServices` 中每个服务注册一次 | §10 #4 |
| 6 | 参数默认值现场确认（两个 ParamModel） | ⏸ | 以现场工艺表为准，覆盖「源码默认值」标注 | §5、§10 #5 |
| 7 | Python 算法与环境验证 | ⏸ | `MIS_JUDGE_C04_325` / `PRESS_C04_325` 现场跑通 | §6.2、§10 #6 |
| 8 | 平台架构文档同步 | ❌ | `项目架构说明.md` §7.2 更正 `DYC01` → `DYC04` | §10 #1 |
| 9 | 清理含空格的文件名 | ❌ | `StringToVisibilityConverter .cs`、`Business1CCDDataModel .cs` 等改名 | §10 #7 |

### 已完成（接手前既有代码，本次仅补文档）

- ✅ 业务枚举 / 接口 / 抽象基类 / 工厂 / 管理器 / DI（`AddJRCWServices`）
- ✅ 两条业务服务：采集 → 算法 → 闭环计算 → CSV 落盘
- ✅ `[Module]` 页面 `VM_JRCWClosedLoopPage` 与消息框对话框、主题样式
- ✅ `JRCWCsvHelper`（`WriteCSV` / `DicToTable` / `SearchCsv`）
- ✅ `ProjectSettingExtension` JRCW 参数区（业务类型 / 落盘根目录 / 算法名 / 两个参数 JSON）
- ✅ Core 侧算法模型（`MisJudge` / `ErrorCompensation` / `Disclass` / `Press` / `Judge` / `PyResult`）

### 本期不做（摘要，全文见规划 §1.3）

MES / Web 上传、报警中心接入、`BusinessType3` 未明确前的实现、历史回放与查询页。

---

## 2. 关键代码入口（精简）

| 职责 | 路径 |
|------|------|
| 常量（`BelongToVM` / `PLCName`） | `Core/Models/Project/JRCW/Core/JRCWCore.cs` |
| 业务枚举 | `Core/Enumerations/Project/JRCW/EBusinessType.cs` |
| 业务接口 | `Core/Interfaces/Project/JRCW/IBusinessService.cs` |
| 基类 / 工厂 / 管理器 | `Services/Project/JRCW/Business/{BusinessServiceBase,BusinessServiceFactory,BusinessServiceManager}.cs` |
| 业务一（义乌C05） | `Services/Project/JRCW/Business/YWC05BusinessService.cs` |
| 业务二（德阳C04） | `Services/Project/JRCW/Business/DYC04BusinessService.cs` |
| 参数模型 | `Core/Models/Project/JRCW/{Business1Models,Business2Models}/` |
| 算法入参 / 出参模型 | `Core/Models/Project/JRCW/ArithmeticModels/` |
| 参数持久化扩展 | `Tool/Extension/ProjectSettingExtension.cs`（`#region JRCW项目`，约 1098 行起） |
| CSV 工具 | `Tool/Helper/JRCWHelper/JRCWCsvHelper.cs` |
| 页面 VM / View | `ViewModels/Pages/JRCW/VM_JRCWClosedLoopPage.cs`、`Views/Pages/JRCW/JRCWClosedLoopPage.xaml` |
| 消息框对话框 | `ViewModels/Dialogs/JRCW/VM_JRCWMessageBoxDialog.cs`、`Views/Dialogs/JRCW/JRCWMessageBoxDialog.xaml` |
| 主题样式 | `Component/Themes/Project/JRCW/JRCWCustomControlStyles.xaml` |
| 页面相关控件 | `Component/Themes/Project/JRCW/`、`Core/Converters/Project/JRCW/` |
| DI 注册 | `Services/ProjectServiceCollectionExtensions.cs` → `AddJRCWServices()` |

分层原则见规划 §3.1（勿在本文展开）。

---

## 3. 下一会话阅读顺序

1. **本文** — 状态、待办、代码入口  
2. 按任务打开 §0 索引中的规划章节（例：录入点位 → 规划 §4；改参数 → 规划 §5）  
3. 改对应代码入口表中的文件  
4. 收尾：**先更新本文**（勾待办）；在 [版本变更记录](./JRCW版本变更记录.md) **顶部追加一条**；规格变了再改规划对应节

---

## 4. 首次接手须知（踩坑点）

> 以下均为本次按代码实测所得，按「会踩坑」的先后排列，改码前务必先读。

1. **枚举与服务是两套命名**：`BusinessType1/2/3` 对应中文「义乌C05 / 德阳C04 / 德阳C01」，而服务类名是 `YWC05BusinessService` / `DYC04BusinessService`。**不要按类名猜业务类型**，以 `BusinessType` 属性与 `BusinessServiceBase.GetBusinessName` 为准。
2. **`BusinessType3` 是空档**：枚举有 3 个值，但只有 2 个服务实现；`BusinessServiceBase` 中 `BusinessType3` 的加载/保存分支**已被注释**，新增服务前先看规划 §10 #3。
3. **DI 里 `DYC04BusinessService` 注册了两次**（`AddJRCWServices`），排查 DI 解析异常时优先看这里。
4. **`JRCWCore.BelongToVM = "VM_JRCWClosedLoopPage"`**：它同时是 `project_setting.BelongToVM` 的取值与页面 VM 类名，**与菜单无关**。
5. **点位按名称读写**：`GetPLCAddressValue("<点位名>")` / `WritePLCAsync("<点位名>", value)`，名称为中文；**地址未录入时读值静默为 0 / 默认值**，联调前必须先补规划 §4 的点位表。
6. **业务二（德阳C04）依赖 Python**：`InitializePythonCodeAsync()` + `PythonConfigHelper.AddAction(...)` + `PyModule.FromString("Judge", pyCode)`；算法名来自配置键（默认 `MIS_JUDGE_C04_325,PRESS_C04_325`）。Python 环境未就绪时该业务不可用（见平台架构说明 §9）。
7. **参数默认值抄自源码**：`Business1ParamModel`（`Nb`/`Na`/`T1`~`T4`/`K`/`J_d`/`P_Switch`/`P_t`/`P_j_d`/`U_Limit`）与 `Business2ParamModel`（含 `NeedleType`、`TabCount`、`S_*`/`C*` 系列、`Press*` 系列、`PerfectBattery1~3`）的默认值**未经现场确认**，禁止据此判断工艺对错。
8. **`JRCWCsvHelper.SearchCsv` 是「按列值筛选」版本**（`filePath, searchColumn, searchValue, params columnsToSelect`），与 JROH 的同名方法签名不同，**不要跨项目照搬调用**。
9. **部分文件名含空格**（`StringToVisibilityConverter .cs`、`Business1CCDDataModel .cs`），脚本/工具按名匹配时容易踩坑，见规划 §10 #7。
