# CTAutoSlicer（惠州离线 CT 自动化切片）进度存档

> **文档版本**：2026-09-08（停用旧深档，仅维护标准三文档）  
> **状态**：阶段一～五全链跑通；阶段七 AI 图像标注（两机拆分）代码已接入；**真机两机联调、若干找点/直方图/亮度/内容上移项待真机核对**  
> **用途**：新会话 **只拖本文**；规格/接口细节用下方索引跳转  
> **详规**：[CTAutoSlicer项目开发规划.md](./CTAutoSlicer项目开发规划.md)　**版本历史**：[CTAutoSlicer版本变更记录.md](./CTAutoSlicer版本变更记录.md)

**进度图例**：✅ 已完成　🔄 进行中　🟡 可选增强　⏸ 阻塞/待确认　❌ 未开始

---

## 0. 文档索引（事实只写一处）

> 进度与待办 **只维护本文**；范围/架构/阶段约定 **只维护规划**；历史变更 **只维护版本变更记录**；接口字段 **只维护各接口文档**；参数默认值以代码 `CTAutoSlicerSettings` 为准。

| 要查什么 | 去哪里（勿在本文重复抄写） |
|----------|---------------------------|
| **版本变更 / 历史决策** | [CTAutoSlicer版本变更记录.md](./CTAutoSlicer版本变更记录.md) |
| 业务定位、改造背景、本期范围 | 规划 [§1](./CTAutoSlicer项目开发规划.md#1-项目定位与目标) |
| **完整业务链时序（切图→交接→AI）** | 规划 [§1.4](./CTAutoSlicer项目开发规划.md#14-完整业务链时序当前目标链) |
| 架构分层 / 服务清单 / 进程池 / 目录约定 | 规划 [§3](./CTAutoSlicer项目开发规划.md#3-总体架构设计) |
| 关键代码入口（VM / VG partial / Helper） | 规划 [§4](./CTAutoSlicer项目开发规划.md#4-关键代码入口) |
| 运行参数 / 默认值 | 规划 §5 要点；真值见 `Core/Models/.../CTAutoSlicerSettings.cs` |
| **直方图 / LUT 2c·2d 约定** | 规划 [§9.1](./CTAutoSlicer项目开发规划.md#91-直方图灰度与-lut-2c2d) |
| **阶段四 · 3-2-1 / 找点 / Pre321** | 规划 [§9.2](./CTAutoSlicer项目开发规划.md#92-阶段四简单-3-2-1与-pre321) |
| **切片导出 / 亮度校验 / 覆盖「全部是」** | 规划 [§9.4](./CTAutoSlicer项目开发规划.md#94-阶段五切片导出) |
| **阶段七 · AI / 两机拆分 / 二次手动标注** | 规划 [§9.5](./CTAutoSlicer项目开发规划.md#95-阶段七-ai-标注两机与二次手动) |
| **主流程 ↔ 单测一致性** | 规划 [§8](./CTAutoSlicer项目开发规划.md#8-开发约定对齐平台) |
| 风险台账 / 已知问题 / 验证未勾项 | 规划 [§6](./CTAutoSlicer项目开发规划.md#6-风险与待确认事项台账)、[§10](./CTAutoSlicer项目开发规划.md#10-验证清单未勾项摘要) |
| **AI 视觉接口（NJCT/CF/HZSwdCT）** | [CT离线AI视觉接口文档.md](./CT离线AI视觉接口文档.md) |
| **NJCT.dll 接入说明 / 待确认清单** | [离线CT_DLL接入说明.md](./离线CT_DLL接入说明.md) |
| **自动切图算法接口（CTAutoCutImg V1.0）** | [CTAutoCutImg接口文档V1.0.txt](./CTAutoCutImg接口文档V1.0.txt) |
| **原始开发资料投放** | [开发资料/](./开发资料/)（规程 §2.2；标识 `ingested`） |
| 找点算法实现细节 | `Tool/Helper/Vision3dSectionPointsDetector.cs`、`VisionTopViewVectorPointsDetector.cs` |
| 平台 / 新模块约定 | [Agent协作开发规程](../Agent协作开发规程.md)、[新增业务模块上手指南](../新增业务模块上手指南.md) |

---

## 1. 当前状态

**一句话**：阶段一～五（多 `.vgl` 导入 → LUT 预设 → 直方图灰度 → 三象限厚板 → 简单 3-2-1 → 对齐后捕捉拖拽 → 上/前/右三视图导出）已全链跑通；阶段七独立 AI 标注 + 二次手动标注已接入；**正/负极双预设与双套 LUT、Pre321（仅本地）、导出前亮度校验、NJCT 远端更新**均已实现。日常开发 **只维护本文 + 规划 + 版本变更**。

**遗留重点**：① 切图机 / AI 机 **两机真机联调**；② 本地斜俯视四点/原点与人工一致性；③ **VisionDll 默认视角找点**真机核对；④ 直方图 2c/2d、内容上移、亮度校验等真机调参；⑤ AI 工艺参数（`BlackHeight`、L1~L5 等）⏸。**工艺数值勿臆造。**

### 待办

| # | 任务 | 状态 | 验收 / 详规索引 |
|---|------|------|------------------|
| 1 | 切图机 / AI 机 **两机真机联调** | 🔄 | 交接 `{产品名}.jpg` 扁平；AI `{产品名}/` 落盘；规划 §9.5 |
| 2 | 阶段四本地：斜俯视四点/原点与人工标注一致性 | 🔄 待真机 | `*_points.png`；规划 §9.2 |
| 3 | **VisionDll 找点路径**真机核对（默认视角、跳过 Pre321） | 🔄 待真机 | 与 DLL 离线单测一致；规划 §9.2、CutImg 接口文档 |
| 4 | Pre321 / LUT 2c 中点 / 2d 按 Y / 目标线靠右 1/4 真机调参 | 🔄 | 规划 §9.1～§9.2 |
| 5 | **内容上移** `OfflineCtContentShiftUpPx` 真机调参 | 🔄 | 规划 §9.5 |
| 6 | 导出前亮度校验真机操作节奏 | 🔄 | 规划 §9.4 |
| 7 | **NJCT 远端下载 + IP 白名单联调**（`10.215.111.1`） | ⏸ | 接口文档 §1.5；仅 AI 标注机 |
| 8 | AI 工艺参数：`BlackHeight`、输入分辨率、L1~L5 语义、`AllPrintfValue` 单位、OK/NG 判定位置 | ⏸ 待现场 | [离线CT_DLL接入说明.md](./离线CT_DLL接入说明.md) §11 |
| 9 | `.vgl` 导入成功后删除策略 / 失败归档 / 文件夹监控自动跑批 | ❌ | 规划 §1.3 |
| 10 | 清理 2024 遗留 UI 参数（`VolumeRenderWaitTime`、灰度锚点等） | ❌ | `CTAutoSlicerSettings` 遗留项；规划 §5 |

### 已完成（主链 + 独立模式）

- ✅ 阶段一：多 `.vgl`/`.vgi`（含快捷方式.lnk）；**Ctrl+F+I** 快速导入；中断弹窗 Drain
- ✅ 阶段二：正/负极双预设 + 直方图（峰谷/半坡/红线 + 2c 中点 + 2d 按 Y）
- ✅ 阶段三：三象限厚板（Q1→Q2→Q3，「平均值」）
- ✅ 阶段四：简单 3-2-1（偏移置0 + 黄框向量 + 原点）；Local / VisionDll 二选一；**无独立旧 4b**；Pre321 仅 Local
- ✅ 对齐后捕捉 + 箭头拖拽（主流程 + 单测）
- ✅ 阶段五：三视图导出（先上）+ mm 范围 UI + JPEG + 覆盖「全部是」+ **导出前亮度校验**
- ✅ 上视图交接（`TopViewHandoffFolder` 扁平）+ **VG 进程池**（默认 4）
- ✅ 阶段七：`PipelineRunMode=AiAnnotateOnly` + 二次手动标注 + NJCT 远端 + 竖图规则 + 内容上移 + 热键 Alt+X/Alt+P/Alt+O
- ✅ 2026-09-08：标准三文档就绪；旧 `CTAutoSlicer_2022改造进度存档.md` **已删除**（内容已并入三文档 + 代码）

### 本期不做（摘要，全文见规划 §1.3）

- 文件夹监控**自动触发跑批**、`.vgl` 删除策略 —— 待产品确认
- 南昌 CTNCLX 业务逻辑不改（`NJSwdCT` 保持）

---

## 2. 关键代码入口（精简）

| 职责 | 路径 |
|------|------|
| 主流程编排（切图全链） | `ViewModels/Pages/CTAutoSlicer/VM_CTAutoSlicerPage.cs`（partial 主文件） |
| Settings / 主题 / ROI 预览 | `VM_CTAutoSlicerPage.Settings.cs` / `.Theme.cs` / `.RoiPreview.cs` |
| 标定 / 单测 / 热键 / AI / CutImg / 远端 | `.Calibration.cs` / `.StageTests.cs` / `.FlowControl.cs` / `.AiAnnotate.cs` / `.CutImgDllTest.cs` / `.OfflineCtDllUpdate.cs` |
| View | `Views/Pages/CTAutoSlicer/CTAutoSlicerPage.xaml(.cs)`、`CTAutoSlicerPageStyles.xaml` |
| 服务门面 | `Services/Project/CTAutoSlicer/CTAutoSlicerService.cs` |
| VG 自动化（partial） | `VGAutomationService.cs` + `VgAutomation/*`（ProcessPool / Import / LutAndHistogram / ThickPlate / Simple321 / Pre321ViewPrep / HistogramAndPostAlign / SliceExport / OfflineCtVision …） |
| `CTAutoCutImg` / `NJCT.HZSwdCT` | `CutImgAlgorithmService.cs` / `OfflineCtVisionService.cs` |
| NJCT 远端 / 本机 local.json | `OfflineCtDllUpdateService.cs`、`CTAutoSlicerLocalConfigService.cs` |
| 热键 / 目录监控 / 进程监控 | `CTAutoSlicerInputHookService.cs`、`CTFolderMonitorService.cs`、`ProcessMonitorService.cs` |
| 找点 / 标注 / 命名 Helper | `Tool/Helper/`：`Vision3dSectionPointsDetector`、`VisionTopViewVectorPointsDetector`、`OfflineCt*Helper`、`SliceExportNamingHelper`、`HistogramGrayScaleAnalyzer` 等 |
| 设置模型（参数默认值真相） | `Core/Models/Project/CTAutoSlicer/CTAutoSlicerSettings.cs` |
| DI | `ProjectServiceCollectionExtensions.cs` → `AddCTAutoSlicerServices()` |

分层原则见规划 §3.1。

---

## 3. 下一会话阅读顺序

1. **本文** — 状态、待办、代码入口  
2. 按 §0 打开 **规划**对应节  
3. 做阶段七 / AI → [CT离线AI视觉接口文档.md](./CT离线AI视觉接口文档.md) + [离线CT_DLL接入说明.md](./离线CT_DLL接入说明.md)  
4. 改参数默认值 → `CTAutoSlicerSettings.cs`；改找点算法 → 对应 Helper 源码  
5. 收尾：**先更新本文**，再在 [版本变更记录](./CTAutoSlicer版本变更记录.md) **顶部追加**；规格变了再改规划对应节
