# AIPredictive 进度存档

> **文档版本**：2026-10-14  
> **状态**：厂家下拉 + 44818；启动自动采集；**周期可配（IntervalMs，CSV 标签同步）**；分级预警已接入；默认仍 500ms  


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
| **现场跳板机 / 工控机 / PLC** | 规划 [§0.2](./AIPredictive项目开发规划.md#02-现场跳板机--工控机--plc2026-10-08)；PLC 全表 [开发资料/德阳C03线_叠片机PLC_IP.csv](./开发资料/德阳C03线_叠片机PLC_IP.csv) |
| 资料盘点与后续整理去向 | 规划 [§2](./AIPredictive项目开发规划.md#2-现有资料盘点)（含 [§2.4 平台 CIP 支持核查](./AIPredictive项目开发规划.md#24-平台-cip-支持核查与采集可行性2026-09-10)） |
| 架构分层 / 模块形态 / 代码入口 | 规划 [§3](./AIPredictive项目开发规划.md#3-平台约束与模块形态)（含 [§3.5 采集存储与跨部门取数方案](./AIPredictive项目开发规划.md#35-采集存储与跨部门取数方案2026-09-10-调研-待定位确认后定稿)） |
| 采集频率与数据量 / 分层采集 | 规划 [§C.0](./AIPredictive项目开发规划.md#c0-采集频率与数据量2026-09-10-分析含--待实测项) |
| 实施阶段与验收 | 规划 [§4](./AIPredictive项目开发规划.md#4-实施阶段与里程碑) |
| 风险与待确认 | 规划 [§6](./AIPredictive项目开发规划.md#6-风险与待确认事项) |
| 启用模块 SQL | [enable_aipredictive_module.sql](./enable_aipredictive_module.sql) |
| **原始开发资料投放** | [开发资料/](./开发资料/)（采集块已定稿；加密 xlsx 未录入） |
| **采集块 CSV（先导=程序别名）** | [开发资料/AIPredictive采集块_伺服.csv](./开发资料/AIPredictive采集块_伺服.csv)（230 点）；厂家对照见 [开发资料/两厂家对照说明.md](./开发资料/两厂家对照说明.md) |
| **赢合采集块（程序已接）** | [开发资料/赢合/赢合_采集块_伺服.csv](./开发资料/赢合/赢合_采集块_伺服.csv)（275 点）；界面选「赢合」加载 |
| **全量采集块备份** | [开发资料/备份_全量采集块_20260914/](./开发资料/备份_全量采集块_20260914/)（伺服 61/920 + 气缸 60/1674） |
| **现场结论简报（给管理层）** | [现场结论简报-20260911.md](./现场结论简报-20260911.md)（速度/资源/为何不能再大优化） |
| 平台新模块约定 | [新增业务模块上手指南](../新增业务模块上手指南.md) |

---

## 1. 当前状态

项目基础结构 demo 已完成：`VM_AIPredictivePage` + `AIPredictivePage` 三件套已建，`[Module("VM_AIPredictivePage", "AI预测分析")]` 已注册；`sunwoda_demo.workspace_project` 中该模块 `is_enabled = 1`，`AppStartMainWindow = MainWindow`；本地 `dotnet build` 0 error。**待人工完整重启应用后从左侧菜单进入验证。**

⚠️ **业务定位（预测对象 / 数据来源 / 输出形式）尚未确认**；**2026-09-14 起程序只采伺服最大扭矩 230 点**（气缸无扭矩、本期不采）；全量 CSV 见备份。加密 xlsx 仍未录入。

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
| 3 | 开发资料录入 | 🔄 | 采集块分表已定稿；程序侧已切仅扭矩；余：加密 xlsx + 规划回填 | [开发资料/](./开发资料/)、规划 §0、§2.2.2 |
| 12 | 点位表重整为采集块（数组连读） | ✅ | 全量备份 61+60 行；旧扁平/合并 CSV 已删；端口 44818 | 规划 §2.2.2 |
| 21 | 暂时只采扭矩（全量备份） | ✅ | 伺服 230 个最大扭矩；气缸 0；采集服务未改读法 | 规划 §2.2.2 |
| 4 | 服务分层骨架（按定位补 Service/Core 接口） | ✅ | 采集服务已落地（500ms 扭矩子集 + 三粒度 CSV）；算法等仍待定位 | 规划 §3、§4 |
| 13 | CIP 周期采集 + 每轮总耗时 | ✅ | 手动「开始采集」；周期 **500ms**；超时 Warn；约每秒 Info；失败置空 | 本文件 §2 |
| 22 | 扭矩子集周期改为 50ms | ✅ | 已由后续 #23/#24 调整 | 规划 §C.0 |
| 23 | 周期 250ms + 四粒度 CSV | ✅ | 已由 #24 改为 500ms 三粒度 | 规划 §3.5 / §C.0 |
| 24 | 周期改为 500ms 标准间隔 | ✅ | 去掉 250ms；CSV=500ms/1s/5s | 规划 §C.0 |
| 25 | 机台状态门控（AndonRunStatu） | ✅ | 每轮先读 INT16；运行采；异常宽限 3s；状态变更日志 | 规划 §C.0 |
| 26 | 界面可配 PLC IP + CSV 仅 500ms | ✅ | `plc_endpoint.json`；保存后重新开始采集生效；去掉 1s/5s | 本文件 §2 |
| 29 | 采集周期可配 + CSV 标签绑定 | ✅ | `IntervalMs` 入 json/界面；热切换；文件名 `AIPredictive_{N}ms_*`；预警跟标签读 | 本文件 §2 |
| 27 | 厂家下拉（先导/赢合）+ 端口顶死 44818 | ✅ | 选哪个只跑哪个；门控标签按厂家；端口不可改 | 本文件 §2 |
| 14 | 界面 PLC 连接灯 + 开始/停止采集 | ✅ | 红/绿灯；可先「测试连接」；再启停采集；离页自动停 | 本文件 §2 |
| 15 | 采集 CSV 按天分卷落盘 + 30 天清理 | ✅ | `D:\AIPredictiveData\yyyy-MM-dd\`；单文件 100MB 滚动；启动/跨日清理 | 本文件 §2 |
| 16 | CSV 表头改用参数中文名 | ✅ | 表头=`DisplayName`；取值仍按 PointKey；无需重提供原表 | 本文件 §2 |
| 17 | 独立「测试连接」按钮 | ✅ | 不启动采集循环；日志含成功/失败 IP:端口 | 本文件 §2 |
| 18 | StructMembers 改 multi-tag 批量读 | ✅ | batch=20；现场全量约 0.47s/轮；汇报已更新 | [现场结论简报](./现场结论简报-20260911.md) |
| 19 | 核对/修复 `ar_bIOT2FromPLC2`（Error 5） | ⏸ | 气缸本期不采；恢复全量后仍需电工核对符号 | 全量备份气缸 CSV |
| 20 | 临时资源监控（老板汇报，可删） | ✅ | 每秒采样；停止出独立报告到 TempBossReport | `_TempBossReport/` |
| 5 | 项目设置持久化（`project_setting`） | ❌ | 关键参数可配置并保存 | 规划 §3 |
| 6 | **平台 CIP / 符号寻址能力核查** | ✅ | 协议已支持；采集走项目侧符号/数组连读 | 规划 §2.4 |
| 7 | **现场实测：CIP 连通性 + 符号可读性 + 端口** | ✅ | 端口 **44818**；`st` 点号可读；`ar` 同步后可读 | 规划 §2.4.3 |
| 8 | **采集频率与数据量分析** | ✅ | 已出分类参考区间与量级测算，确认邻域瓶颈在 CIP 请求速率而非存储 | 规划 §C.0 |
| 9 | **采集存储与跨部门取数方案选型** | ⏸ | 待定位确认后定（TimescaleDB / InfluxDB / QuestDB / MySQL 分区） | 规划 §3.5 |
| 10 | 点位分档（预测必需 / 仅诊断 / 暂不需要） | 🔄 | 已临时只采最大扭矩；正式分档仍待工艺确认 | 规划 §C.0 |
| 11 | `ConvertValue` 补 `REAL` 类型别名 | ❌ | 可选：采集块已映射 `REAL`→`FLOAT`，可不改平台 | 规划 §2.2.2 / §2.4.2 |
| 28 | 分级预警接入上位机（先导3# 验证） | ✅ | 每2h分析一次，每次喂近2h切片→原版py；水印跳过；有一级/二级即弹（不抑制） | `AIPredictive/GradingAlert/` |

### 已完成（P0 骨架）

- ✅ 2026-09-10：`docs/AIPredictive/` 三文档 + `开发资料/` + `README.md`
- ✅ 2026-09-10：点位整理为**采集块分表**——`AIPredictive采集块_伺服.csv`（61 行 / 920 点）、`AIPredictive采集块_气缸.csv`（60 行 / 1674 点）；端口 44818；`REAL`→`FLOAT`；旧扁平表与合并采集块已删除
- ✅ 2026-09-10：现场 CIP 实测——端口 44818、结构体点号可读、数组在 PLC 同步后可读；平台 CIP 协议支持核查 + 频率/存储方案调研已落规划
- ✅ 2026-09-10：VM/View 三件套（`ViewModels/Pages/AIPredictive/`、`Views/Pages/AIPredictive/`）
- ✅ 2026-09-10：`enable_aipredictive_module.sql` 已写入并对 `sunwoda_demo` 执行（`is_enabled = 1`）
- ✅ 2026-09-10：本地编译 0 error
- ✅ 2026-09-10：页面顶部 PLC 红/绿灯 +「开始采集」「停止采集」；进页不再自动连 PLC
- ✅ 2026-09-10：采集 CSV 落盘——按天目录、100MB 分卷、保留 30 天（默认 `D:\AIPredictiveData`）
- ✅ 2026-09-11：独立「测试连接」；CSV 表头改为参数中文名（采集块清单已有 DisplayName）
- ✅ 2026-09-11：结构体改 multi-tag 批量读（现场约 0.47s/轮）；CIP 读法汇报更新
- ✅ 2026-09-11：「测试连接」移到连接状态文字右侧；现场替换包 v2 已出
- ✅ 2026-09-11：临时资源监控 TEMP_BOSS_REPORT（解耦可删；报告独立目录）
- ✅ 2026-09-14：全量采集块备份后，程序改为只采伺服最大扭矩 230 点（气缸无扭矩）
- ✅ 2026-09-14：采集周期 1000ms → **50ms**（超时不等待；Info 约每秒一条）
- ✅ 2026-09-14：现场替换包 `AIPredictive_replace_20260914_v1.zip`（本机 8080 提供下载）
- ✅ 2026-09-17：采集周期 50ms → **250ms**；CSV 分写 250ms/500ms/1s/5s 四套文件（供大数据组）
- ✅ 2026-09-17：现场替换包 `AIPredictive_replace_20260917_v1.zip`（本机 8080 提供下载）
- ✅ 2026-09-22：现场确认后周期改为 **500ms**；CSV 改为 500ms/1s/5s 三套（去掉 250ms）
- ✅ 2026-09-22：现场替换包 `AIPredictive_replace_20260922_v1.zip`（本机 8080 提供下载）
- ✅ 2026-09-23：机台状态门控——`AndonRunStatu`（CIP short）；仅 **7=运行** 读扭矩并写 CSV；状态变更 Info 日志
- ✅ 2026-09-23：替换包 `AIPredictive_replace_20260923_v1.zip` → `E:\共享文档\下载\`（HTTP :80）
- ✅ 2026-09-24：异常离开运行后 **宽限 3s** 仍采；界面可改 PLC IP/端口（`AIPredictive/plc_endpoint.json`，重开采集生效）；CSV 仅 500ms
- ✅ 2026-09-29：启动自动采集、去掉启停按钮、离页不停采、启动最小化；替换包 `AIPredictive_replace_20260929_v1.zip`
- ✅ 2026-10-10：分级预警接入（2h/2h）+ 红遮罩；替换包 `AIPredictive_replace_20261010_v1.zip`（含便携 Python）
- ✅ 2026-10-12：分级预警读 CSV 共享锁修复；热修包 `AIPredictive_replace_20261012_v1.zip`（仅 SEVB.dll）
- ✅ 2026-10-12：Python 相对路径启动修复；热修包 `AIPredictive_replace_20261012_v2.zip`（仅 SEVB.dll）
- ✅ 2026-10-14：采集周期可配（`IntervalMs`）+ CSV/预警标签同步；热修包 `AIPredictive_replace_20261014_v1.zip`
- ✅ 2026-09-29：赢合资料整理（点位 275、采集块、状态码）；对照见 `开发资料/两厂家对照说明.md`
- ✅ 2026-09-29：界面厂家下拉（先导/赢合互斥）+ CIP 端口固定 44818；`plc_endpoint.json` 含 Vendor；保存并重连
- ✅ 2026-10-08：录入现场地址——跳板 `10.146.41.211`；工控机（经跳板）先导1=`10.147.71.233` / 先导3=`10.147.71.95` / 赢合10=`10.147.71.151`；对应 PLC（C03 表）先导1=`10.147.72.10` / 先导3=`10.147.72.12` / 赢合10=`10.147.72.19`（`.15` 为赢合6#）

### 本期不做（摘要，全文见规划 §1.3）

在业务定位确认前，不引入算法模型、不接 PLC、不建 CSV/曲线、不做 MES 上报与报警中心。

---

## 2. 关键代码入口（精简）

| 职责 | 路径 |
|------|------|
| 页面 VM（`[Module]`、绑定、生命周期） | `SUNWODA_SEVB/ViewModels/Pages/AIPredictive/VM_AIPredictivePage.cs` |
| 页面 View | `SUNWODA_SEVB/Views/Pages/AIPredictive/AIPredictivePage.xaml`、`AIPredictivePage.xaml.cs` |
| 采集服务 | `SUNWODA_SEVB/Services/Project/AIPredictive/AIPredictiveAcquisitionService.cs` |
| **临时资源监控（可删）** | `SUNWODA_SEVB/Services/Project/AIPredictive/_TempBossReport/`（搜 `TEMP_BOSS_REPORT`） |
| CSV 落盘 | `SUNWODA_SEVB/Services/Project/AIPredictive/AIPredictiveCsvStorage.cs`（根目录 `D:\AIPredictiveData`） |
| 采集块加载 | `SUNWODA_SEVB/Services/Project/AIPredictive/AIPredictiveCollectBlockLoader.cs` |
| 接口 / 模型 | `SUNWODA_SEVB.Core/Interfaces/Project/AIPredictive/`、`SUNWODA_SEVB.Core/Models/Project/AIPredictive/` |
| DI 注册 | `AIPredictiveServiceCollectionExtensions.AddAIPredictiveServices()`（`App.xaml.cs` 已调用） |

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
