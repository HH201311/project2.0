# JROH 版本变更记录

> **用途**：按日期倒序记录「做了什么、为什么、落在哪」；**当前待办与状态以 [JROH进度存档.md](./JROH进度存档.md) 为准**
> **格式**：`> **YYYY-MM-DD 变更（标题）**` + 背景 / 做法 / 落点；规格变更写清「见规划 §x」
> 新会话默认 **不必** 拖本文；需要追溯历史时再按进度存档索引打开。

---
> **2026-10-09 变更（提交前正式审查：修复 4 项 P1 + 3 项 P2，4 项待人工确认）**
> 1. **背景**：对本次开发提交 `184ad87`（卷绕OH代码开发）执行提交前正式审查（基线 develop，审查时工作区 clean 且与 `origin/develop` 同步，故审其相对父提交的全部改动）。提交中夹带了跨产线（JRCW）与环境配置改动。
> 2. **做法（已修复）**：
>    - **曲线功能**：`VM_MCOfJROHPage.AppendSeries` 补回 `targetSeries.Add(...)`（回退后只剩检测值，XAML 已绑定的 11 路「设定值」曲线恒为空）；
>    - **目录监控**：`MCBusinessService.OnCsvFileWatcherError` 溢出时除重建监听器外新增后台补扫 `RescanMissedFiles`（膜卷号校验 + 文件指纹去重），避免丢卷芯（规范 D-06 / R2）；
>    - **健壮性**：两份服务清理路径的空 catch 改为 `_logger.Warn`（A-01）；取消令牌改为先摘引用再 `Cancel/Dispose` 并捕获 `ObjectDisposedException`，避免二次清理抛异常；
>    - **清理**：删除回退残留死字段 `missingTargetColumnWarned`（CS0169）、`MisBusinessService .cs` 重命名为 `MisBusinessService.cs`、基类 `GetBusinessName` 补 `Mismatch => "极耳错位闭环"`。
> 3. **落点**：`SUNWODA_SEVB/Services/Project/JROH/Business/{MCBusinessService.cs,MisBusinessService.cs,BusinessServiceBase.cs}`、`SUNWODA_SEVB/ViewModels/Pages/JROH/VM_MCOfJROHPage.cs`；报告 [审查报告-20261009-1506.md](./审查报告-20261009-1506.md)。
> 4. **验证**：`dotnet build SUNWODA_SEVB/SUNWODA_SEVB.csproj` **0 error**（需 SDK 9；SDK 10 假报 CS2015）。
> 5. **待人工确认（未改）**：① `MisBusinessService` 未完成接入（`Mismatch` 走基类 switch 会抛「未实现的业务类型」、未注册进管理器、无独立参数模型/页面）；② `ConfigurationService` 启用了本地测试库 `sunwoda2_jrcw`；③ `DYC04BusinessService` 压花判据改为 `< 2 * pm.PressT`（跨产线工艺）；④ `docs/JROH/开发资料` 下 2713 个样例 CSV 入库。

> **2026-09-21 变更（MC 单路通道解析 ConvertToMCGatherData 实现与优化）**
> 1. **背景**：`ConvertToGatherModel` 已改为一列一调 `ConvertToMCGatherData(datas, timeKey, detKey, tarKey, setKey)`，但该方法是半成品（循环体为空、11 路调用参数全传空串、存在重复赋值且缺 `Snake1DataOfNeg`），`MCGatherData.HasTargetValue` 被回退而服务与 VM 仍在引用 —— 编译不过。
> 2. **做法**：
>    - 实现 `ConvertToMCGatherData`：列名映射按首行建一次（不逐行扫字典键）、时间解析失败即跳过（天然覆盖重复表头，去掉 `i == 1` 硬编码，避免误删真实数据）、清洗行首反引号（U+0060）、检测值缺失/非法沿用上一行、目标列缺失时 `HasTargetValue = false`、`setKey` 为空时回退取目标值末值；
>    - 修正 11 路调用参数（正极 5 / 负极 4 / 隔膜 2），去掉重复赋值并补 `Snake1DataOfNeg`；新增通道自检日志（全部无数据时 Warn、部分无数据时 Debug），便于现场发现列名不符；
>    - 恢复 `MCGatherData.HasTargetValue`，并在 `ExcuteMCCalcualteAsync` 加回「目标值列缺失 → 该通道不下发」保护（否则隔膜会以 `tar = 0` 为基准参与闭环、存在误写 PLC 风险），告警只记录一次。
> 3. **落点**：`SUNWODA_SEVB/Services/Project/JROH/Business/MCBusinessService.cs`；`SUNWODA_SEVB.Core/Models/Project/JROH/MCBusinessModels/MCGatherData.cs`。
> 4. **验证**：`dotnet build SUNWODA_SEVB\SUNWODA_SEVB.csproj` **0 error**，JROH 相关警告均为既有（CS1998 / CS0414），本轮无新增警告。
> 5. **未做 / 待确认**：现场目录实跑未验证；隔膜「上/下隔膜外侧破标定值」列仍待现场补列（⏸），补齐后该通道会自动纳入闭环。

> **2026-09-21 变更（MC 解析改接 JROHCsvHelper + 补全 ProcessCsvFile / 通道映射）**
> 1. **背景**：MC 的 CSV 解析改为复用平台 `JROHCsvHelper.SearchCsv`（自建解析器 `MCBusinessCsvParser.cs` 已移除）；`ProcessCsvFile` 遗留 `SearchCsv(fullPath,)` 语法错误、目录日期校验 TODO，且配套 `ConvertToGatherModel` 为空实现 —— 整条链路编译不过、不可用。
> 2. **做法**：
>    - 修复 `SearchCsv(fullPath)` 调用（原 `SearchCsv(fullPath,)` 无法编译）；
>    - 落地 TODO「检验文件夹日期」：校验文件位于 `{yyyy年M月}\{yyyy年M月d日/号}`（兼容有无前导零、`日`/`号`），并要求为真实日历日期、月目录与日目录年月一致，不合法则 Warn 并跳过；
>    - `ProcessCsvFile` 健壮性：文件存在性、指纹去重（**成功后才记账**，失败可在下一轮文件事件重试）、空数据行与「未匹配到通道列」告警；
>    - 补全 `ConvertToGatherModel`：按**表头名**匹配 11 路通道（不依赖列序号，列名映射只解析一次），跳过重复表头行、清洗行首反引号（U+0060）、非法/缺失数值沿用上一行；隔膜缺目标值列时 `HasTargetValue = false`（闭环不下发、曲线只绘检测值）。
> 3. **落点**：`SUNWODA_SEVB/Services/Project/JROH/Business/MCBusinessService.cs`（补 `using System.Globalization;`）。
> 4. **验证**：`dotnet build SUNWODA_SEVB\SUNWODA_SEVB.csproj` **0 error**（115 个警告均为既有）。
> 5. **未做 / 待确认**：现场目录实跑未验证；隔膜「外侧破标定值」列仍待现场补列（⏸）。
> 6. **文档更正**：本文件较旧条目与本存档、规划中提及的 `MCBusinessCsvParser` 均已随自建解析器移除，现以 `JROHCsvHelper.SearchCsv` + `MCBusinessService.ConvertToGatherModel` 为准。

> **2026-09-21 变更（MC 膜卷号 CSV 监听解析落地 + 主界面 11 图曲线）**
> 1. **背景**：MC 链路此前实际不可用——`MCBusinessService` 仅监听 `Renamed` 且回调只 `new` 一个空采集模型上报界面（不解析文件、不入队闭环）；`MCOfJROHPage` 顶部 3 个曲线图全部误绑到 `InfeedOfPosSeries1/2`；VM 按 `count >= param.T`（T 默认 1）**每帧清空**曲线。同时核查发现：本文件 2026-09-21 条目所述「MC CSV 监听解析 + 曲线三图」的改动**在代码中不存在**（全仓 grep `MCBusinessCsvParser` / `MCPlotSeries` / `PosPlotSeries` 均 0 匹配），本轮按现场样例重新真正实现。
> 2. **做法**：
>    - **新增 `MCBusinessCsvParser`**：按 2026-08-30 样例（2713 个卷芯 CSV）实测格式解析——UTF-8 BOM、第 1/2 行均为表头（跳过重复表头）、数据行行首反引号、数值列双引号包裹、61 列、时间 `yyyy-MM-dd HH:mm:ss.fff`；按**表头名**（非列序号）映射 11 路通道，单点缺失沿用上一行。
>    - **膜卷号校验**：实测 2709 个已完成文件全部为 `9 位数字 + 1 位字母 + 5 位数字`（15 位，如 `529126128L03465.csv`）；供应商写入中的纯数字临时文件（如 `3770.csv`）被过滤。⏸ 需求方口述为「日期 + 膜卷号固定长度」，样例无日期前缀，待资料补齐。
>    - **监听改造**：`Created` / `Renamed`（更名完成 = 数据就绪）+ 800ms 防抖 + 文件指纹（长度 + 最后写入时间）去重 + 监听异常自动重建（64KB 缓冲）；一个卷芯文件 = 一条采集数据入队。
>    - **闭环修复**：`dataCache` 结算后 `Clear()`（原实现跨卷累积）；`TotalCount == 0` 直接返回（原实现 `0/0` 整除）；比例判定改浮点除法；目标值列缺失的通道**不下发**。
>    - **模型补充**：`MCGatherData.HasTargetValue` 标记目标值列是否有效。
>    - **主界面曲线**：按 PPT《多级纠偏联动控制0914》slide10 与界面需求，曲线区改为 **3 组 Tab（正极 / 负极 / 隔膜）**，每组 **5 / 4 / 2** 个独立曲线图（11 路通道各一图，检测值 + 设定值），绑到 VM 已有的 11 路集合；VM 曲线改为**按上限裁剪最近 2000 点**。
> 3. **落点**：新增 `SUNWODA_SEVB/Services/Project/JROH/Business/MCBusinessCsvParser.cs`；改 `MCBusinessService.cs`、`Core/Models/Project/JROH/MCBusinessModels/MCGatherData.cs`、`ViewModels/Pages/JROH/VM_MCOfJROHPage.cs`、`Views/Pages/JROH/MCOfJROHPage.xaml`。
> 4. **验证**：✅ NuGet 已用本机包缓存离线还原成功（9 个项目；`SUNWODA_SEVB.MES` 用浮动版本 `6.0.*`，需把包目录当**源**而非 fallback，否则 NU1100）；`dotnet build SUNWODA_SEVB.csproj` 已执行——**XAML 与全部 C# 通过**，仅 `MCBusinessCsvParser` 报 1 处 `CS0103`（`out var tarValue` 作用域），**已修复**；⏸ 修复后的完整重编译因本会话沙箱权限审批不可用未能再执行，**请在 VS 中重新生成确认 0 error**。
> 5. **未做 / 待确认**：隔膜「外侧破标定值」列缺失（只画检测值、不下发，⏸）；MC 下发仍未接入 `ClosedLoopEnablePos/Neg` 开关；现场目录实跑与 Tab 切换后曲线刷新未验证。

> ⚠️ **更正（2026-09-21）**：下方「2026-09-21 MC CSV 监听解析 + 曲线三图」条目所述改动**当时并未落盘**（代码核查 0 匹配），本轮已重新实现，该条目仅作历史记录保留。

> **2026-09-21 变更（MC CSV 监听解析 + 曲线三图，待办 #8 主体完成）**
> 1. **背景**：现场张力系统样例（`开发资料/多级纠偏联动资料/路径.xlsx` + 2026-08-30 当日 2713 个卷芯 CSV）到位后，MC 链路原状为「只监听 `Renamed` + 解析空模型」，实际不可用。
> 2. **做法**：
>    - **以样例数据定列映射**（不臆造）：逐列核对 61 列表头与样本数值，确认 10 路为「`XX纠偏` → `XX纠偏中值`」；**放卷 2 路无中值列**，暂用「`XX放卷纠偏` + 左伺服实时位置」占位并在代码与文档双标注 ⏸；
>    - **新增 `MCBusinessCsvParser`**：跳过重复表头、剥离行首反引号与 BOM、容错解析（缺失/非法数值回退上一行）、时间 `yyyy-MM-dd HH:mm:ss.fff`、条码入 `VirtualCode`；
>    - **重做监听**：`Created`/`Changed`/`Renamed` + **1.5s 防抖** + **文件指纹去重** + **卷芯命名校验（16 位数字 + `L` + 4 位数字 = 21 位）** + 只处理启动后落盘文件 + 监听异常自动重建（缓冲 64KB）；
>    - **修复 `0/0` 整除**：`TotalCount == 0` 时记「无有效采样点」直接返回；比例判定改为浮点除法；
>    - **曲线控件改造**：`MultiCorrectPlot` 新增 `Series`（`MCPlotSeries` 集合，实时值实线 / 设定值虚线）与重绘节流（200ms、单曲线最多绘 2000 点），保留 `LineSeries1/2` 旧用法；
>    - **MC 页三图**：正极（放卷 + 蛇形1~4）/ 负极（放卷 + 蛇形1~3）/ 隔膜（上·下）各图叠加该组通道的实时值与设定值曲线；曲线按上限裁剪，不再用 `count >= param.T` 清空。
> 3. **落点**：新增 `Services/Project/JROH/Business/MCBusinessCsvParser.cs`、`Core/Models/Project/JROH/MCBusinessModels/MCPlotSeries.cs`；改 `MCBusinessService.cs`、`Component/UserControls/Project/JROH/MultiCorrectPlot.xaml.cs`、`ViewModels/Pages/JROH/VM_MCOfJROHPage.cs`、`Views/Pages/JROH/MCOfJROHPage.xaml`。
> 4. **验证**：`dotnet build SUNWODA_SEVB\SUNWODA_SEVB.csproj` **0 error**（本机需 SDK 9，SDK 10 会假报 `CS2015`，见进度存档 §4.10）；未做现场联调（需 PLC 点位与真实目录）。
> 5. **未做 / 待确认**：放卷 2 路中值列（等现场完整 CSV）；入卷 2 路（CSV 有数据但无对应下发点位，本期未纳入闭环与曲线）；现场目录实跑与调机记录联动。
> **2026-09-18 变更（首次补齐 JROH 三文档 + CSV + 启用 SQL）**
> 1. **背景**：`docs/` 下 BoxGluing / CTAutoSlicer / CTOL / ZZSIX 均已建档，**JROH 代码已成型却零文档**；新会话无法定位入口，PLC 点位与阈值真假混在代码默认值里。
> 2. **做法**：
>    - 按「Agent 协作开发规程 §2.2.1」只读本项目 `Views/ViewModels/Services/Core` 入口文件，**不做框架探查式扫仓**；
>    - 三文档对照 `docs/BoxGluing/` 结构落地：进度存档作热层（状态/待办/入口/索引），版本变更倒序，规划作冷层（范围/架构/算法/落盘/风险）；
>    - 事实一律取自代码，**参数数值抄自 `*BusinessParamModel` 字段默认值并明确标注「未经现场确认」**；PLC 地址**未知即留空并标 ⏸**，不臆造；
>    - 发现的问题（OH 负极写入名、`中断` 文案、`AdjustInterval` 未使用、MC 未解析 CSV、OH 未自启动等）记入规划 §10 与进度存档待办，**本次不改业务代码**。
> 3. **落点**：[JROH进度存档.md](./JROH进度存档.md)；[JROH版本变更记录.md](./JROH版本变更记录.md)；[JROH项目开发规划.md](./JROH项目开发规划.md)；[plc点位表.csv](./plc点位表.csv)；[工艺阈值表.csv](./工艺阈值表.csv)；[配置键表.csv](./配置键表.csv)；[enable_jroh_module.sql](./enable_jroh_module.sql)；[README.md](./README.md)；[开发资料/](./开发资料/)。
> 4. **未做**（留待后续待办）：菜单启用（未执行 SQL）、PLC 点位录入、MC CSV 解析、OH 单边闭环验证、编译验证。

> **接手前（既有代码归档）**
> - `EBusinessType`（PW / OH / MC）、`IBusinessService`、`BusinessServiceBase<T>`、`BusinessServiceFactory`、`BusinessServiceManager` 一次建成，模式对齐 JRCW。
> - 三条业务服务各自完成「信号监听 → 采集 → 闭环计算 → PLC 下发 → 调机记录 → CSV 落盘」。
> - 三个 `[Module]` 页面与 ViewModel、三个参数配置对话框、四个曲线控件（`PoleWidthPlot` / `CoatedOHPlot` / `MultiCorrectPlot` / `FloderPicker`）与 `JROHCustomControlStyles.xaml`。
> - `ProjectSettingExtension` 增补 JROH 参数区（`FileSaveRootPath` + 三个业务参数 JSON）；`JROHCsvHelper` 提供 CSV 分卷与列增补重写。
> - ⚠️ 历史决策过程无记录（无提交说明、无会议纪要），本条仅记录**代码现存事实**，不代表设计意图已被确认。
