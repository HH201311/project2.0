# CTOL（在线 CT AI 检测）版本变更记录

> **用途**：按日期倒序记录「做了什么、为什么、落在哪」；**当前待办与状态以 [CTOL进度存档.md](./CTOL进度存档.md) 为准**
> **格式**：`> **YYYY-MM-DD 变更（标题）**` + 背景 / 做法 / 落点；规格变更写清「见规划 §x」或「见改造指南 §x」
> 新会话默认 **不必** 拖本文；需要追溯历史时再按进度存档索引打开。
>
> 说明：CTOL 早期经历「旧单体页 C06」→「新架构页 + 服务化」→「算法多进程子进程」多次重构。本节自 2026-05 服务层成型后按 commit 与代码现状整理；更早历史仅作锚点，细节以代码为准。

---

> **2026-09-08 变更（补齐 docs/CTOL 标准三文档）**
> 1. **背景**：docs/CTOL 原有分析/改造/审查文档，缺「进度存档 / 版本变更记录 / 项目开发规划」标准三件套，不贴合仓库文档约定。
> 2. **做法**：按 [BoxGluing](../BoxGluing/) 样板新增三文档并互相索引；新增空目录 `开发资料/`（未录入）。
> 3. **落点**：本文；[CTOL进度存档.md](./CTOL进度存档.md)；[CTOL项目开发规划.md](./CTOL项目开发规划.md)；[开发资料/](./开发资料/)。

> **2026-09-03 变更（历史提交审查）**
> 1. **背景**：对约 2026-08-03～09-03 合入 develop 的 CTOL 相关历史提交做只读审查。
> 2. **做法**：产出 P1×4 / P2×4、**有条件通过**；指出 `WarmupAsync()` fire-and-forget、`NeFoldArea` fail-open、共享默认库被产线提交改写、掉片旧路径残留等风险。
> 3. **落点**：[审查报告-20260903-1647.md](./审查报告-20260903-1647.md)（本变更未改业务代码）。

> **2026-08-31 变更（默认库改回；非 CTOL 提交影响）**
> 1. **背景/做法**：`0bc2552` 等非 CTOL 提交把默认库由 `sunwoda_ctol` 改回 `sunwoda_demo`。**当前工作区默认库不是 `sunwoda_ctol`**（见审查报告）。CTOL 现场应依赖本机已落盘配置，勿依赖仓库默认连接串。
> 2. **落点**：默认配置（跨产线）；见审查报告 P1 #3。

> **2026-08-26 变更（数据库配置错误修复）**
> 1. **背景**：CTOL 启动连库异常，定位到默认库名配置错误（commit `ee2b3b6`）。
> 2. **做法**：将默认库名改为 `sunwoda_ctol`（后又被其它产线提交覆盖，见上条）。
> 3. **落点**：共享默认配置（跨产线踩踏风险见审查报告 P1 #3）。

> **2026-08-25 变更（P0 掉片合入 + 启用 DI + P1/P2 预留）**
> 1. **背景**：拉 1A/1B 本期需求：正极 Y 掉片规则调整、负极翻折/压伤参数化（commit `711fbcf` + `cce37d1`）。
> 2. **做法**：P0 掉片跨图仅 a∧b（去 Gap47）、Gap46 默认 0.8、UI 只留 SharedGap45/46；启用 `AddCTOLRemoteFileMonitor`；P1 翻折 `Gap48Limit` + `TryJudgeNegativeProtrusion`（面积 mm²）；P2 压伤 `Gap49/50/51` 界面与持久化；缺陷优先级补「负极突起」、DefectFileMover 白名单。
> 3. **落点**：`BarcodeGroupingService.CheckCrossYDrop`、`ImageProcessorBase`、`VM_CTOLAutoDetectionPage`、`CTOLAutoDetectionPage.xaml`；细则见 [改造指南](./CTOL改造指南.md)（P1/P2 判级待算法接口）。
> 4. **遗留**：独立模式（`MonitorControlViewModel`）仍残留 Gap47/旧三条件；现场确认模式与落库阈值（审查报告 P1 #4）。

> **2026-08-14 / 08-13 变更（改造指南细化 P1/P2）**
> 1. **背景/做法**：负极翻折确认沿用「负极突起」归档名、单位 mm²、面积 0→NG、面积未到→跳过；负极压伤确认按层遍历（角度/压伤宽/撞伤宽，约负极 108 层），判级待 DLL 接口文档。
> 2. **落点**：`CtAlgorithmResult.NeFoldArea`（`double?`）；改造指南 §3/§4（改文档，代码判级接线见下条）。

> **2026-08-05 变更（算法迁至独立子进程；CTOL 项目更新）**
> 1. **背景**：原模型在 WPF 主进程内加载，工位耦合、崩溃牵连整页；改造方向为多进程（commit `38ad79f`）。
> 2. **做法**：算法拆入 `C01OnlineCT.exe` / `C06OnlineCT.exe`（server 模式，模型只加载一次）；每工位一个独立 `CtSubprocessClient`（stdin 发请求、stdout 收一行一 JSON、`__READY__` 握手、单图 2 分钟超时、`StationId` 硬隔离、崩溃自动重启重载模型）。
> 3. **落点**：`Communication/CTOL/CtSubprocessClient.cs`、`Processing/ImageProcessorBase.cs`（`#define C01/C06` 编译切换）、四路 `*ImageProcessor`、`Core/Models/Project/CTOL/CtAlgorithmResult.cs`、`CtWorkerRequest.cs`。
> 4. **注意**：基类构造器内 `WarmupAsync()` fire-and-forget，为审查 P1 #1。

> **2026-05-24 变更（服务层成形 / 合并入 develop）**
> 1. **背景/做法**：业务逻辑由 VM 下沉到 `Services/Project/CTOL/*`：条码聚合 8 张、按天 SQLite 落库、间隙 CSV、阈值运行时字典、结果图渲染/清理、每日上传；新架构四路 `ImageProcessor` + `ImageProcessorBase`。
> 2. **落点**：`BarcodeGroupingService`、`InspectionResultService`、`GapThresholdService`、`GapCsvService`、`DailyUploadService`、`ResultImageCleanupService`、`RemoteFileMonitorConfigService`、`Processing/*`（commit `5e8fdc3`）。

> **2026-03-12 变更（在线 CT 重构：新架构页 + 平台化）**
> 1. **背景**：推进 SEVB 平台模块化（commit `2d962b5`）。
> 2. **做法**：新增新架构页 `VM_CTOLAutoDetectionPage` / `CTOLAutoDetectionPage.xaml`、平台 `ImageShowControl`/`PiePlotViewer` 控件、`Core/Models/Project/CTOL`（DefectData/DetectionData/MonitorConfig）；旧页 C06 同步演进。
> 3. **落点**：`ViewModels/Pages/CTOL`、`Views/Pages/CTOL`、`Component/UserControls/Project/CTOL`。

> **2025-12-25 变更（在线 CT 页/model 初建）**
> 1. **背景**：产线引入在线 CT AI 检测（commit `b312f1c`）。
> 2. **做法**：建立 C06 单体页与归档/上传助手。
> 3. **落点**：`VM_CTAutoDetectionC06Page.cs`、`CTAutoDetectionC06Page.xaml(.cs)`、`Tool/Helper/CTOL/DefectFileMoverService.cs`、`SimpleDailyTaskService.cs`。

---

*版本变更记录与 git 提交 hash 对应关系以 develop 分支为准；跨分支合入可能使提交说明与文件归属略有出入。*
