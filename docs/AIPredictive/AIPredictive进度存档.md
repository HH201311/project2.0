# AIPredictive 进度存档

> **文档版本**：2026-09-10  
> **状态**：项目骨架已落地（VM/View + 菜单启用 + 编译通过）；业务定位待确认  
> **用途**：新会话 **只拖本文**；规格细节不写在本文，用下方索引跳转  
> **详规**：[AIPredictive项目开发规划.md](./AIPredictive项目开发规划.md)　**版本历史**：[AIPredictive版本变更记录.md](./AIPredictive版本变更记录.md)

**进度图例**：✅ 已完成　🔄 进行中　⏸ 阻塞　❌ 未开始

---

## 0. 文档索引（事实只写一处）

> 进度与待办 **只维护本文**；需求 / 架构 / 点位 **只维护规划或 CSV**；历史变更 **只维护版本变更记录**。

| 要查什么 | 去哪里（勿在本文重复抄写） |
|----------|---------------------------|
| **版本变更 / 历史决策** | [AIPredictive版本变更记录.md](./AIPredictive版本变更记录.md) |
| 业务定位、本期范围、本期不做 | 规划 [§1](./AIPredictive项目开发规划.md#1-项目定位与目标) |
| 现场原始资料清单 / 投放状态 | 规划 [§0](./AIPredictive项目开发规划.md#0-开发资料录入工作流)、[开发资料/](./开发资料/) |
| 资料盘点与后续整理去向 | 规划 [§2](./AIPredictive项目开发规划.md#2-现有资料盘点) |
| 架构分层 / 模块形态 / 代码入口 | 规划 [§3](./AIPredictive项目开发规划.md#3-平台约束与模块形态) |
| 实施阶段与验收 | 规划 [§4](./AIPredictive项目开发规划.md#4-实施阶段与里程碑) |
| 风险与待确认 | 规划 [§6](./AIPredictive项目开发规划.md#6-风险与待确认事项) |
| 启用模块 SQL | [enable_aipredictive_module.sql](./enable_aipredictive_module.sql) |
| **原始开发资料投放** | [开发资料/](./开发资料/)（规程 §2.2；当前状态：**已投放；点位表已转 CSV**） |
| **点位表 CSV（真相，供程序读取）** | [AIPredictive点位表_伺服.csv](./AIPredictive点位表_伺服.csv)、[AIPredictive点位表_气缸.csv](./AIPredictive点位表_气缸.csv)（列定义见规划 [§2.2.1](./AIPredictive项目开发规划.md#221-csv-数据字典程序读取口径)） |
| 平台新模块约定 | [新增业务模块上手指南](../新增业务模块上手指南.md) |

---

## 1. 当前状态

项目基础结构 demo 已完成：`VM_AIPredictivePage` + `AIPredictivePage` 三件套已建，`[Module("VM_AIPredictivePage", "AI预测分析")]` 已注册；`sunwoda_demo.workspace_project` 中该模块 `is_enabled = 1`，`AppStartMainWindow = MainWindow`；本地 `dotnet build` 0 error。**待人工完整重启应用后从左侧菜单进入验证。**

⚠️ **业务定位（预测对象 / 数据来源 / 输出形式）尚未确认**，本轮只做骨架，未写任何业务逻辑；现场 3 份原始资料已投放，其中 2 份点位 txt **已转 CSV**（伺服 920 行 / 气缸 1674 行，见 §0 索引），加密 xlsx 仍未录入（见规划 §0 / §2）。

**待确认**：AIPredictive 的预测目标与数据来源（规划 §1.4、§6 #1）。

### 数据库启用现状（`sunwoda_demo.workspace_project`，2026-09-10）

| vm_class_name | is_enabled | 说明 |
|---------------|-----------|------|
| `VM_SettingsPage` | 1 | 平台基础页（设置） |
| `VM_UserCenter` | 1 | 平台基础页（用户） |
| `VM_AIPredictivePage` | 1 | **本项目**（AI预测分析） |
| `VM_BoxGluingMonitoringPage`、`VM_CTOLAutoDetectionPage`、`VM_xxxTESTPage` | 0 | 其它项目/测试模块，按要求置 0 |

> 切换开发项目时：把要做的模块置 1、其余置 0（`AppStartMainWindow` 保持 `MainWindow`）。
> 启动实测（2026-09-10 11:13，本副本 exe）：日志 `注册业务模块: VM_AIPredictivePage (AI预测分析)`、`业务模块注册完成，总业务模块数: 3`、`成功导航到业务模块: VM_AIPredictivePage`。

### 本机构建提示（环境）

- 本机 CLI 下 MSBuild **多节点并行**评估会静默失败（表现为「生成失败／0 error」）；还原与编译请加 `-m:1`，例如：  
  `dotnet restore SUNWODA_SEVB.sln -m:1 --source "<全局包缓存路径>"` 然后 `dotnet build SUNWODA_SEVB.sln -c Debug -m:1 --no-restore`  
- 无外网时可用本地 NuGet 全局缓存（`%USERPROFILE%\.nuget\packages`）作源；仅 `SUNWODA_SEVB.MES.csproj` 的 `System.ServiceModel.* 6.0.*` 为浮动版本，缓存内为 6.0.0，可解析。  
- 2026-09-10 实测：该方式还原 0 error、编译 0 error（28 个既有警告，与本项目无关）。

### 待办

| # | 任务 | 状态 | 验收 | 详规索引 |
|---|------|------|------|----------|
| 1 | 重启应用验证菜单「AI预测分析」可进空页 | ✅ | 左侧菜单出现并可打开，无异常 | 本文件 §2 |
| 2 | 业务定位与预测目标确认 | ⏸ | 与需求方确认后补写规划 §1 | 规划 §1.4 / §6 |
| 3 | 开发资料录入 | 🔄 | 点位表 CSV 已完成（逐字段与源 txt 一致，有空列说明）；余：加密 xlsx 录入 + 规划章节回填 | [开发资料/](./开发资料/)、规划 §0、§2.2.1 |
| 4 | 服务分层骨架（按定位补 Service/Core 接口） | ❌ | 定位确认后建 `Services/Project/AIPredictive/` + DI | 规划 §3、§4 |
| 5 | 项目设置持久化（`project_setting`） | ❌ | 关键参数可配置并保存 | 规划 §3 |

### 已完成（P0 骨架）

- ✅ 2026-09-10：`docs/AIPredictive/` 三文档 + `开发资料/` + `README.md`
- ✅ 2026-09-10：点位原始资料转 CSV（`AIPredictive点位表_伺服.csv` 10 列×920 行、`AIPredictive点位表_气缸.csv` 12 列×1674 行；UTF-8 BOM + CRLF；纯格式转换，校验 0 差异）
- ✅ 2026-09-10：VM/View 三件套（`ViewModels/Pages/AIPredictive/`、`Views/Pages/AIPredictive/`）
- ✅ 2026-09-10：`enable_aipredictive_module.sql` 已写入并对 `sunwoda_demo` 执行（`is_enabled = 1`）
- ✅ 2026-09-10：本地编译 0 error

### 本期不做（摘要，全文见规划 §1.3）

在业务定位确认前，不引入算法模型、不接 PLC、不建 CSV/曲线、不做 MES 上报与报警中心。

---

## 2. 关键代码入口（精简）

| 职责 | 路径 |
|------|------|
| 页面 VM（`[Module]`、绑定、生命周期） | `SUNWODA_SEVB/ViewModels/Pages/AIPredictive/VM_AIPredictivePage.cs` |
| 页面 View | `SUNWODA_SEVB/Views/Pages/AIPredictive/AIPredictivePage.xaml`、`AIPredictivePage.xaml.cs` |
| 业务服务（**待建**） | `SUNWODA_SEVB.Services/Services/Project/AIPredictive/` |
| 接口 / 模型（**待建**） | `SUNWODA_SEVB.Core/Interfaces/Project/AIPredictive/`、`SUNWODA_SEVB.Core/Models/Project/AIPredictive/` |
| DI 注册（**待加**） | `ProjectServiceCollectionExtensions.AddProjectServices()` |

分层原则见规划 §3.1（勿在本文展开）。

模块标识：

| 项 | 值 |
|----|-----|
| 模块 ID（`vm_class_name`） | `VM_AIPredictivePage` |
| View 类 | `AIPredictivePage` |
| 左侧菜单显示名 | `AI预测分析` |
| 命名空间 | `SUNWODA_SEVB.ViewModels.Pages.AIPredictive` / `SUNWODA_SEVB.Views.Pages.AIPredictive` |

---

## 3. 下一会话阅读顺序

1. **本文** — 状态、待办、代码入口  
2. 按任务打开 §0 索引中的规划章节（例：确认需求 → 规划 §1；录入资料 → 规划 §0 + `开发资料/`）  
3. 改对应代码入口表中的文件  
4. 收尾：**先更新本文**（勾待办）；在 [版本变更记录](./AIPredictive版本变更记录.md) **顶部追加一条**；规格变了再改规划对应节
