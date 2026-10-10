# JROH 进度存档

> **文档版本**：2026-09-21
> **状态**：**首次补齐文档**（此前 `docs/` 下无 JROH 任何文档）；代码已成型（三页 + 三业务闭环 + CSV），**三页均未在数据库启用**，**点位表与现场真值未录入**
> **用途**：新会话 **只拖本文**；规格细节不写在本文，用下方索引跳转
> **详规**：[JROH项目开发规划.md](./JROH项目开发规划.md)　**版本历史**：[JROH版本变更记录.md](./JROH版本变更记录.md)

**进度图例**：✅ 已完成　🔄 进行中　⏸ 阻塞　❌ 未开始

---

## 0. 文档索引（事实只写一处）

> 进度与待办 **只维护本文**；点位/阈值 **只维护 CSV 或规划**；历史变更 **只维护版本变更记录**。
> ⚠️ 本文与规划中所有「参数默认值」均抄自代码（`*BusinessParamModel` 字段默认值），**未经现场工艺确认**；确认后请以 CSV 为唯一真相。

| 要查什么 | 去哪里（勿在本文重复抄写） |
|----------|---------------------------|
| **版本变更 / 历史决策** | [JROH版本变更记录.md](./JROH版本变更记录.md) |
| 业务范围、本期不做、验收边界 | 规划 [§1](./JROH项目开发规划.md#1-项目定位与目标) |
| **PLC 设备 / 点位名 / 下发名 / 触发信号** | [plc点位表.csv](./plc点位表.csv)（**⚠️ 地址列待现场录入**）；规划 [§4](./JROH项目开发规划.md#4-plc-与数据采集设计) |
| **参数键 / 业务参数模型 / 存储类型** | [配置键表.csv](./配置键表.csv)；规划 [§5](./JROH项目开发规划.md#5-参数配置与持久化) |
| 工艺阈值数值（CP 尺寸区间） | [工艺阈值表.csv](./工艺阈值表.csv)（抄自代码默认值，**待现场确认**）；规划 [§5.3](./JROH项目开发规划.md#53-工艺阈值数值源码默认值待确认) |
| 架构分层 / 服务清单 / DI | 规划 [§3](./JROH项目开发规划.md#3-总体架构设计) |
| PW / OH / MC 三条闭环算法与数据流 | 规划 [§6](./JROH项目开发规划.md#6-三条闭环算法与数据流) |
| CSV 输出列与目录规则 | 规划 [§7](./JROH项目开发规划.md#7-落盘与文件输出) |
| 风险与待确认 | 规划 [§10](./JROH项目开发规划.md#10-风险与待确认事项) |
| 启用模块 SQL | [enable_jroh_module.sql](./enable_jroh_module.sql) |
| **原始开发资料投放** | [开发资料/](./开发资料/)（**当前为空，等现场 PLC 表 / CSV 样例**） |
| 同构参照项目（单项目多业务工厂模式） | `Services/Project/JRCW/`；规划 [§3.4](./JROH项目开发规划.md#34-与-jrcw-的异同) |
| 平台新模块约定 | [新增业务模块上手指南.md](../新增业务模块上手指南.md) |

---

## 1. 当前状态

**JROH = 卷绕 OH 闭环，一个项目下挂三条独立业务**（工厂 + 管理器模式，对齐 JRCW）：

| 业务（`EBusinessType`） | 菜单页（`[Module]` 名） | 数据来源 | 闭环输出 |
|------------------------|------------------------|----------|----------|
| `PoleWidth` 极片宽度闭环 | `VM_PWOfJROHPage`「极片宽度闭环」 | PLC 触发 + CCD 模宽 | 正 / 负极纠偏值下发 |
| `CoatedOH` 包覆 OH 闭环 | `VM_OHOfJROHPage`「包覆 OH 闭环」 | PLC 双触发 + AC/SA 数组 | 前 / 中 / 后段正负极纠偏下发 |
| `MultiCorrect` 多级联动闭环 | `VM_MCOfJROHPage`「多级联动纠偏闭环」 | **文件监听（CSV）** | 11 路纠偏中值下发 |

**已具备**：`EBusinessType` / `IBusinessService` / `BusinessServiceBase<T>` / 工厂 / 管理器 / DI 全部就位；三个 `[Module]` 页面 + 三个参数配置对话框 + 四类曲线控件 + 主题样式齐备；三条业务均落盘 CSV。

**关键现状（阻断项）**：

- ❌ **三个页面均未启用**：`workspace_project` 无记录、`is_enabled = 0`，菜单看不到 → 见 [enable_jroh_module.sql](./enable_jroh_module.sql)
- ❌ **PLC 设备记录未确认**：`JROHCore.PLCName = "卷绕OH闭环PLC"`，仓库内无该 PLC 的种子/迁移，需现场确认存在
- ⏸ **PLC 点位表缺失**：三条业务按**参数名**读写（如 `GetPLCAddressValue("正极A面CCD模宽数据")`），**具体地址全部未知** → 见 [plc点位表.csv](./plc点位表.csv)
- ✅ **MC 数据源已接通（2026-09-21，代码已落盘）**：解析复用平台 `JROHCsvHelper.SearchCsv`，由 `MCBusinessService.ConvertToGatherModel` 逐路调用 `ConvertToMCGatherData` 按表头名映射 11 路（跳过重复表头行、清洗行首反引号、缺列容错）；监听 `Created`/`Renamed` + 800ms 防抖 + 文件指纹去重 + 膜卷号 15 位校验 + 目录日期校验 + 解析入队闭环；主界面曲线按 3 组 5+4+2 落地
- 📋 **提交前正式审查（2026-10-09）**：P0=0 / P1=7（已修 4）/ P2=4（已修 3）；待人工确认：`MisBusinessService` 接入、本地测试库连接串、JRCW 压花阈值、样例 CSV 入库 → [审查报告-20261009-1506.md](./审查报告-20261009-1506.md)
- ⚠️ **隔膜「外侧破标定值」列在样例 CSV 中不存在**：该通道目标值占位 `0`；当前样例下合格点通常为 0 → 闭环走「无有效采样点，不下发」，但**无显式「目标列缺失不下发」保护**（个别点落在 (−2,2) 时仍可能按错误基准下发，见审查报告加严建议）—— 待现场补列后自然消解
- ⏸ **OH 单边闭环未验证**：`IsSigleClosedLoop = true` 的整链路未现场测过（见待办 #9）

### 待办

| # | 任务 | 状态 | 验收 | 详规索引 |
|---|------|------|------|----------|
| 1 | 三页菜单启用（含 BelongToVM 结论） | ❌ | 重启后左侧菜单可见并可进三页 | [enable_jroh_module.sql](./enable_jroh_module.sql)、§3.3 |
| 2 | 确认 `JROHCore.BelongToVM` | ⏸ | 问负责人：合并为单页 `VM_JROHClosedLoopPage`，还是分三页各写本页 VM 名 | §3.3、§10 #1 |
| 3 | **OH 负极 PLC 写入名纠错** | ❌ | 负极写入用「负极…」名，且判据用 `ClosedLoopEnableNeg` | §6.2、§10 #2 |
| 4 | OH「中断」→「中段」文案 | ❌ | 下发参数名与日志文案统一 | §6.2 |
| 5 | `AdjustInterval` 落地或删除 | ⏸ | 确认用途；确认无用则删字段与对话框绑定 | §6.1、§10 #3 |
| 6 | **录入 PLC 点位表** | ⏸ | [plc点位表.csv](./plc点位表.csv) 地址 / 类型 / 设备全部填实 | §4、§10 #4 |
| 7 | 确认 PLC 设备记录 `卷绕OH闭环PLC` | ⏸ | 现场 `plc_config` 存在且已连接 | §4.1 |
| 8 | 补 MC 的 CSV 解析 + 监听 + 曲线主界面 | 🔄 | ✅ 解析 / 监听 / 防抖 / 膜卷号校验 / 入队闭环 / 3 组 5+4+2 曲线已落盘；⏸ 隔膜标定值列缺失（只画检测值、不下发）；⏸ 现场实跑与 Tab 切换刷新未验证 | §6.3、§10 #5 |
| 9 | OH 链路修复 + 单边闭环验证 | ❌ | 先让 `coatedSigalListener` 轮询起来；再 `IsSigleClosedLoop` true/false 各现场测一轮 | §6.2、§10 #10 |
| 10 | 三个 CSV 定稿（点位/阈值/配置键） | 🔄 | 现场确认后覆盖「源码默认值」标注 | §5.3、§7 |
| 11 | 编译验证（含本轮改动） | 🔄 | 首轮 2026-09-21 **0 error**（本机需 SDK 9；SDK 10 假报 CS2015）。本轮：NuGet 已用本机缓存离线还原成功；编译**跑到仅剩 1 处 CS0103（`MCBusinessCsvParser` 的 `out var tarValue` 作用域）且已修复**；⏸ 修复后重编译因沙箱权限审批不可用未执行 | — |
| 12 | 清理已注释模拟代码 | ❌ | MC/PW 服务内注释块删除 | §10 #7 |
| 13 | 平台架构文档同步 | ❌ | `docs/项目架构说明.md` §5.1/§5.2/§7 增列 JROH | — |
| 14 | 处理提交前审查遗留的 4 项待人工确认 | ⏸ | MisBusiness 接入 / `ConfigurationService` 本地库 / `DYC04BusinessService` 阈值 / `开发资料` 样例 CSV 入库 | [审查报告-20261009-1506.md](./审查报告-20261009-1506.md) |

### 已完成（接手前既有代码，本次仅补文档）

- ✅ 三业务枚举 / 接口 / 抽象基类 / 工厂 / 管理器 / DI 注册（`AddJROHServices`）
- ✅ 三个 `[Module]` 页面 + ViewModel，进出页面自动启停对应业务
- ✅ 四个曲线控件：`PoleWidthPlot`、`CoatedOHPlot`、`MultiCorrectPlot`、`FloderPicker`
- ✅ 三个参数配置对话框 + `project_setting` JSON 持久化（`BelongToVM = "VM_JROHClosedLoopPage"`）
- ✅ 三条业务闭环计算 + PLC 下发 + 调机记录（内存 20 行）+ 曲线（上限 1000 点）
- ✅ CSV 落盘 `JROHCsvHelper`（100MB 自动 `.cache` 分卷、列增补流式重写、写锁串行）

### 本期不做（摘要，全文见规划 §1.3）

MES / Web 上传、报警中心接入、`VM_JROHClosedLoopPage` 单页合并重构、历史回放与查询页、曲线持久化。

---

## 2. 关键代码入口（精简）

| 职责 | 路径 |
|------|------|
| 常量（`BelongToVM` / `PLCName`） | `Core/Models/Project/JROH/Core/JROHCore.cs` |
| 业务枚举 | `Core/Enumerations/Project/JROH/EBusinessType.cs` |
| 业务接口 | `Core/Interfaces/Project/JROH/IBusinessService.cs` |
| 基类 / 工厂 / 管理器 | `Services/Project/JROH/Business/BusinessServiceBase.cs`、`BusinessServiceFactory.cs`、`BusinessServiceManager.cs` |
| PW 业务 | `Services/Project/JROH/Business/PWBusinessService.cs` |
| OH 业务 | `Services/Project/JROH/Business/OHBusinessService.cs` |
| MC 业务 | `Services/Project/JROH/Business/MCBusinessService.cs` |
| MC 通道解析 | `Services/Project/JROH/Business/MCBusinessService.cs`（`ProcessCsvFile` / `ConvertToGatherModel` / `ConvertToMCGatherData`） |
| 参数 / 采集 / 计算模型 | `Core/Models/Project/JROH/{PW,OH,MC}BusinessModels/` |
| 参数持久化扩展 | `Tool/Extension/ProjectSettingExtension.cs`（`#region JROH项目`，约 1352 行起） |
| CSV 落盘工具 | `Tool/Helper/JROHHelpper/JROHCsvHelper.cs` |
| 页面 VM | `ViewModels/Pages/JROH/VM_{PW,OH,MC}OfJROHPage.cs` |
| 页面 View | `Views/Pages/JROH/{PW,OH,MC}OfJROHPage.xaml(.cs)` |
| 参数对话框 | `ViewModels/Dialogs/JROH/BusinessDialogs/`、`Views/Dialogs/JROH/BusinessDialogs/` |
| 曲线控件 | `Component/UserControls/Project/JROH/{PoleWidthPlot,CoatedOHPlot,MultiCorrectPlot,FloderPicker}.xaml(.cs)`；`MultiCorrectPlot` 支持检测值/设定值双曲线（`LineSeries1Collection` / `LineSeries2Collection`） |
| MC 曲线数据源 | `ViewModels/Pages/JROH/VM_MCOfJROHPage.cs` 的 11 路 `*Series1/2`（检测值 / 设定值） |
| 主题样式 | `Component/Themes/Project/JROH/JROHCustomControlStyles.xaml` |
| DI 注册 | `SUNWODA_SEVB/Services/ProjectServiceCollectionExtensions.cs` → `AddJROHServices()` |

分层原则见规划 §3.1（勿在本文展开）。

---

## 3. 下一会话阅读顺序

1. **本文** — 状态、待办、代码入口
2. 按任务打开 §0 索引中的规划章节或 CSV（例：纠错 OH 下发名 → 规划 §6.2 + [plc点位表.csv](./plc点位表.csv)）
3. 改对应代码入口表中的文件
4. 收尾：**先更新本文**（勾待办）；在 [版本变更记录](./JROH版本变更记录.md) **顶部追加一条**；规格变了再改规划对应节

---

## 4. 首次接手须知（踩坑点）

> 以下均为本次通读代码实测所得，按「会踩坑」的先后排列，改码前务必先读。

1. **`OnDataReceived` 只推曲线，闭环由 `CalculateLoopAsync` 独立消费缓存** — 曲线 / 调机记录是 UI 旁路，别以为「界面没动就没跑闭环」。
2. **三页都会 `StartBusinessAsync`**（`OnInitializeAsync` 中，`OnCleanupAsync` 中 `StopBusinessAsync`）；但 `OHBusinessService.ExecuteAsync` 中 `await Task.WhenAny(signa1Task, calculateTask)` **只传入了 `poleSigalListener`（极片信号）的轮询任务**，`coatedSigalListener`（包覆信号）的 `PLCSigalListenerMonitorLoopAsync` **从未被启动** → 采集入队的唯一入口失效，OH 链路当前不通。待办 #9 处置。
3. **OH 触发顺序**：收到「极片数据触发信号」只更新 `moduleWidthOfPosA/B` 后 `return`，**不进闭环**；只有收到「包覆数据触发信号」才组装 `OHBusinessGatherModel` 并入队。
4. **OH / PW 在闭环开关关闭时不写 PLC**，只打 `Warn` 日志（文案为「推荐下发值」）。
5. **MC 的 CSV 监听已重做（2026-09-21 二轮）**：`Created` / `Renamed` 两事件（更名完成 = 数据就绪，不再监听持续写入）+ **800ms 防抖** + **文件指纹去重**（文件长度 + 最后写入时间），回调中真正解析文件并入队闭环（一个卷芯 = 一条采集数据）。
   - 触发口径：只有符合**膜卷号**命名规则的文件才被处理（纯数字临时文件跳过）；不做启动时存量扫描（只响应运行期间的文件事件），避免重启后重复投递。
6. **`JROHCore.BelongToVM` 是纯配置键**：`VM_JROHClosedLoopPage` 在仓库中**不存在任何同名类**（同名模式见 JRCW 的 `VM_JRCWClosedLoopPage`）。它只作 `project_setting.BelongToVM` 的取值，**与菜单无关**。
7. **MC 曲线保留策略已改（2026-09-21 二轮）**：原 `count >= param.T` 清空 + `count` 只增不减的写法已移除，改为**按上限裁剪**（每路最多保留最近 2000 点）；11 路数据集见 `VM_MCOfJROHPage` 的 22 个 `*Series1/2` 集合。
8. **`PWBusinessGatherModel` 和 `OHBusinessGatherModel` 不是 `ModelBase`**（不实现 `INotifyPropertyChanged`），而 `MCBusinessGatherModel` 是；改绑定时注意。

9. **膜卷号 CSV 的解析要点（2026-08-30 现场样例实测，2713 个文件）**：UTF-8 BOM；**第 1、2 行均为表头**（重复表头需跳过）；**数据行行首带一个反引号**；数值列用双引号包裹；共 61 列；时间格式 `yyyy-MM-dd HH:mm:ss.fff`。
   - 11 路通道按**表头名**（非列序号）映射：正极 入卷 + 蛇形1~4、负极 入卷 + 蛇形1~3、隔膜 上/下；**隔膜目标值列「上/下隔膜外侧破标定值」样例中不存在**（只采集检测值、不下发，⏸ 待现场补列）；正负极「放卷纠偏中值」列同样不存在，但放卷不是闭环输入通道，不影响闭环。
10. **命令行编译环境**：本机无 .NET 8 SDK（仅 9.0.316 / 10.0.401），目标框架为 `net8.0-windows`。
    - 用 **SDK 10 编译会假报 `CS2015 是二进制文件而非文本文件`**（全仓文件齐报，与源码无关）；
    - 用 **SDK 9（`9.0.316`）可正常编译**：`dotnet build SUNWODA_SEVB\SUNWODA_SEVB.csproj`（Visual Studio 内编译不受影响）。

11. **文档与代码曾不一致（2026-09-21 核查）**：本存档与版本变更记录中「MC CSV 监听解析 + 曲线三图」条目所描述的 `MCBusinessCsvParser` / `MCPlotSeries` / `PosPlotSeries` 在代码中**不存在**（全仓 grep 0 匹配），当时服务仍是「只监听 `Renamed` + 上报空模型」。本轮已按样例真正落盘。**教训：写码与写文档必须同批完成，收尾用 grep 核对新增类型是否真的存在。**

12. **命令行编译的两点经验（2026-09-21 补）**：① 本机默认 SDK 10 会假报 CS2015，编译前需让 SDK 9 生效（临时放 `SUNWODA_SEVB/global.json` 指定 `9.0.316`，用完删除）；② 无外网时可用本机包缓存离线还原：`dotnet restore SUNWODA_SEVB.csproj --source "%USERPROFILE%\.nuget\packages" -m:1 -nodeReuse:false -p:RestoreDisableParallel=true`（`SUNWODA_SEVB.MES` 用浮动版本 `6.0.*`，必须把包目录当**源**而非 fallback，否则 NU1100）。
