# CTOL（在线 CT AI 检测）进度存档

> **文档版本**：2026-09-08
> **状态**：P0 掉片规则已合入 develop；P1 翻折 / P2 压伤「界面 + 持久化」已完成、判级等算法接口；P3 上线未开始
> **用途**：新会话 **只拖本文**；规格细节不写在本文，用下方索引跳转
> **详规**：[CTOL项目开发规划.md](./CTOL项目开发规划.md)　**版本历史**：[CTOL版本变更记录.md](./CTOL版本变更记录.md)

**进度图例**：✅ 已完成　🔄 进行中　⏸ 阻塞/待确认　❌ 未开始

---

## 0. 文档索引（事实只写一处）

> 进度与待办 **只维护本文**；范围/架构 **只维护规划**；本期改造细则 **只维护改造指南**；代码地图 **只维护接手指南**；历史变更 **只维护版本变更记录**。

| 要查什么 | 去哪里（勿在本文重复抄写） |
|----------|---------------------------|
| **版本变更 / 历史决策** | [CTOL版本变更记录.md](./CTOL版本变更记录.md) |
| 业务范围、本期做/不做 | 规划 [§1](./CTOL项目开发规划.md#1-项目定位与目标) |
| 总体架构 / 分层 / 服务清单 | 规划 [§3](./CTOL项目开发规划.md#3-总体架构设计) |
| 数据流（四路→算法→聚合→归档/落库） | 接手指南 [§4](./CTOL项目分析与接手指南.md) |
| 四路工位目录 / 视野 / 判级要点 | 接手指南 §1/§4；代码见本文 §2 |
| Gap 阈值键与默认值 | 规划 [§4.3](./CTOL项目开发规划.md#43-判级阈值键与默认值)；代码默认值见 `ImageProcessorBase` / 两 VM |
| **本期改造细则 P0/P1/P2/P3** | [CTOL改造指南.md](./CTOL改造指南.md)（掉片 / 翻折 / 压伤 / 上线） |
| 现场环境（拉1A/1B、账号、发布范围） | 接手指南 [§2](./CTOL项目分析与接手指南.md)、改造指南 §6 |
| 2026-09 历史提交审查结论 | [审查报告-20260903-1647.md](./审查报告-20260903-1647.md) |
| 目录 / 使用说明 | [README.md](./README.md) |
| **原始开发资料投放** | [开发资料/](./开发资料/)（规程 §2.2；当前为空，未录入） |
| 平台新模块约定 | [新增业务模块上手指南](../新增业务模块上手指南.md) |

---

## 1. 当前状态

**新架构页 `VM_CTOLAutoDetectionPage` + `Services/Project/CTOL/*` 已为主开发基准**；算法在多进程独立 `C01OnlineCT/C06OnlineCT` 子进程内运行（见规划 §3）。旧单体页 `VM_CTAutoDetectionC06Page` 保留做对照，**不在其上改业务**。

**已上线范围判定**：拉 1A / 拉 1B 为上线机（6A/6B 暂缓）。P0 掉片改造已合入 develop；**当前待办**集中在「现场联调、算法接口对接、P3 上线」与审查遗留问题。

### 待办

| # | 任务 | 状态 | 验收 / 详规索引 |
|---|------|------|------------------|
| 1 | P1 负极翻折现场联调（`NeFoldArea` 字段） | ⏸ 等算法 | Worker stdout JSON 出面积 → 判级生效；[改造指南 §3](./CTOL改造指南.md) |
| 2 | P2 负极压伤逐层判级接线（角度/压伤宽/撞伤宽数组） | ⏸ 等 DLL 接口文档 | [改造指南 §4](./CTOL改造指南.md) |
| 3 | P3 拉 1A/1B 统一上线（备份→更新主程序+「多进程程序」→抽检） | ❌ 未开始 | 改造指南 §6/§7 |
| 4 | 「多进程程序」澄清（进程名/安装路径/与主 WPF 关系） | ⏸ 待交接方 | 改造指南 §5 |
| 5 | 审查遗留：`QueryAsync` 空类型=全查是否真是缺陷（代码只查 `types.Count>0`） | ⏸ 待定 | 审查报告 P2 #4 邻近逻辑；代码 §7 |
| 6 | 共享/独立阈值模式切换入口（UI 未绑定开关；独立模式代码残留） | ⏸ 待定 | 见 §7 注意 |
| 7 | 现场核对已落库 Gap46 / Gap48 / Gap49~51 值 | 🔄 联调时 | 改造指南 §6 发布前检查 |
| 8 | Halcon license + 模型文件（`model\1222\C06_best.onnx` 等）开发机是否齐全 | ⏸ 待确认 | 接手指南 §10 |

### 已完成（P0 及以下）

- ✅ 2025-12 起：在线 CT C06 页与归档/定时上传助手（旧单体，见版本变更记录）
- ✅ 2026-03：重构在线 CT——新架构页 `VM_CTOLAutoDetectionPage` 雏形 + 平台控件/模型
- ✅ 2026-05：`Services/Project/CTOL/*` 服务层成形（聚合/落库/CSV/清理/每日上传）
- ✅ 2026-08-05：算法迁至独立 `C01OnlineCT`/`C06OnlineCT` 子进程（`CtSubprocessClient`，多进程）
- ✅ 2026-08-25：**P0 正极 Y 掉片**（跨图仅 a∧b、Gap46 默认 0.8、去掉 Gap47）+ 启用 `AddCTOLRemoteFileMonitor`
- ✅ 2026-08-25：P1 翻折 UI + `Gap48Limit` 持久化 + 面积判级预留（`TryJudgeNegativeProtrusion`）
- ✅ 2026-08-25：P2 压伤界面 `Gap49/50/51` + 落库（判级未接）；缺陷优先级补「负极突起」白名单

### 本期不做（摘要，全文见改造指南 / 规划）

- 不在旧页 C06 上改业务；暂不发布 6A/6B
- P2 压伤逐层判级、P1 翻折面积真正生效——**均依赖算法接口到齐**，未到前不臆造工艺值

---

## 2. 关键代码入口（精简）

| 职责 | 路径 |
|------|------|
| 新架构主 VM（共享阈值/检索/导出/权限） | `ViewModels/Pages/CTOL/VM_CTOLAutoDetectionPage.cs` |
| 单路监控卡片 VM（独立阈值/启停） | `ViewModels/Pages/CTOL/MonitorControlViewModel.cs` |
| 新架构页 View（监测/参数设置/数据检索） | `Views/Pages/CTOL/CTOLAutoDetectionPage.xaml` |
| 旧单体页（**仅对照，勿改业务**） | `ViewModels/Pages/CTOL/VM_CTAutoDetectionC06Page.cs`、`Views/Pages/CTOL/CTAutoDetectionC06Page.xaml` |
| 判级基类（`NgOkJudge`、子进程调用、阈值注入） | `Services/Project/CTOL/Processing/ImageProcessorBase.cs` |
| 四路处理器（LAB/LCD/RAB/RCD ↔ StationId 0~3） | `Services/Project/CTOL/Processing/{LAB,LCD,RAB,RCD}ImageProcessor.cs` |
| 条码聚合 8 张 + 跨图掉片终判 + 目录归档 | `Services/Project/CTOL/BarcodeGroupingService.cs` |
| 阈值运行时字典（共享/独立模式） | `Services/Project/CTOL/GapThresholdService.cs` |
| 结果落库（按天 SQLite）+ 查询/导出 | `Services/Project/CTOL/InspectionResultService.cs` |
| 间隙 CSV / 每日上传 / 结果图清理 | `Services/Project/CTOL/GapCsvService.cs`、`DailyUploadService.cs`、`ResultImageCleanupService.cs` |
| 配置持久化（`remote_file_monitor_config.json` + ProjectSetting） | `Services/Project/CTOL/RemoteFileMonitorConfigService.cs` |
| 算法子进程客户端（stdin/stdout 一行一 JSON） | `Communication/CTOL/CtSubprocessClient.cs` |
| 算法请求/结果/缺陷/检测 DTO | `Core/Models/Project/CTOL/CtAlgorithmResult.cs`、`CtWorkerRequest.cs`、`DefectData.cs`、`DetectionData.cs` |
| DI 注册 | `Services/ServiceCollectionExtensions.cs` → `AddCTOLRemoteFileMonitor()` |
| 启动注入 / `InitCTOLEnvironment` | `App.xaml.cs` |
| 运行配置（Monitor 四路） | `SUNWODA_SEVB/remote_file_monitor_config.json` |

分层原则见规划 §3.1（勿在本文展开）。

---

## 3. 下一会话阅读顺序

1. **本文** — 状态、待办、代码入口
2. 按待办 # 打开 §0 索引对应文档（例：做压伤判级 → [改造指南 §4](./CTOL改造指南.md) + `ImageProcessorBase.NgOkJudge`；现场联调 → 接手指南 §2）
3. 改对应代码入口表中的文件
4. 收尾：**先更新本文**（勾待办）；在 [版本变更记录](./CTOL版本变更记录.md) **顶部追加一条**；规格变了再改规划 / 改造指南对应节

---

## 附：与改造指南 / 审查报告的衔接

- 本期四个需求（掉片/翻折/压伤/多进程程序）的**细则、验收清单、待澄清问题**全部在 [CTOL改造指南.md](./CTOL改造指南.md)，本文只维护总体状态。
- [审查报告-20260903-1647.md](./审查报告-20260903-1647.md) 给出 2026-09-03 前一个月 CTOL 相关历史提交的 P1/P2 风险清单（构造器 fire-and-forget 预热、`NeFoldArea` fail-open、共享默认库改动、旧路径残留、提交卫生等），后续改造应逐条核对落账。
