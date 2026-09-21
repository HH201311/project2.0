# CTAutoSlicer（惠州离线 CT 自动化切片）版本变更记录

> **用途**：按日期倒序记录「做了什么、为什么、落在哪」；**当前待办与状态以 [CTAutoSlicer进度存档.md](./CTAutoSlicer进度存档.md) 为准**  
> **格式**：`> **YYYY-MM-DD 变更（标题）**` + 背景 / 做法 / 落点；规格变更写清「见规划 §x」或「见接口文档 §x」  
> 新会话默认 **不必** 拖本文；需要追溯历史时再按进度存档索引打开。  
>  
> 说明：2022 改造自 **2026-07** 起开发；旧 `CTAutoSlicer_2022改造进度存档.md` 已于 2026-09-08 **删除**，约定以规划为准、默认值以 Settings 代码为准。

---

> **2026-09-08 变更（删除旧改造进度存档，仅保留标准三文档）**  
> 1. **背景**：后续开发只需进度存档 / 规划 / 版本变更；旧深档已拆分完毕，继续保留会造成双源与断链。  
> 2. **做法**：三文档去掉全部深档引用；参数默认值改指 `CTAutoSlicerSettings.cs`；删除 `CTAutoSlicer_2022改造进度存档.md`。  
> 3. **落点**：三文档；本目录文件列表。

> **2026-09-08 变更（深档内容拆分进标准三文档）**  
> 1. **背景**：首轮三文档仅为摘要+索引，冷层约定仍只在旧深档。  
> 2. **做法**：业务链 / 阶段约定 / 进程池 / 风险与验证未勾项迁入规划；进度存档补全待办与索引；接口真相落本目录根。  
> 3. **落点**：规划 / 进度存档 / 本文。

> **2026-09-08 变更（补齐 docs/CTAutoSlicer 标准三文档骨架）**  
> 1. **背景**：缺「进度存档 / 版本变更记录 / 项目开发规划」标准三件套。  
> 2. **做法**：按 [BoxGluing](../BoxGluing/) 样板新建三文档并互相索引。  
> 3. **落点**：本文；[CTAutoSlicer进度存档.md](./CTAutoSlicer进度存档.md)；[CTAutoSlicer项目开发规划.md](./CTAutoSlicer项目开发规划.md)；[开发资料/](./开发资料/)。

> **2026-08-31 变更（视觉人工干预 + 导出前亮度校验 + 直方图峰谷平滑）**
> 1. **背景**：视觉对齐失败时允许操作员确认人工处理后跳过（commit `68bb6e9`）；异常亮度/视图下直接导出易致切片不合格（`0bc2552`）；直方图顶缘锯齿把同峰毛刺误当双峰。
> 2. **做法**：新增视觉人工干预确认流程；导出前亮度校验（独立门闩，与 Alt+P 分离）；直方图柱高去尖刺后自适应**中值+高斯**平滑、局部峰 ±邻域判定、加大 `minSep`/`mergeDist`、两峰间须 ≥18% 谷深；目标线改 `Valley + (RightPeak−Valley)×3/4`（靠右约 1/4）。
> 3. **落点**：`HistogramGrayScaleAnalyzer.Optimize/FindBimodalHalfSlope`、`PreExportBrightnessCheckGate`、`VGAutomationService.SliceExport`、`VM_CTAutoSlicerPage`、XAML。

> **2026-08-28 变更（NJCT 双类名 + 远端 DLL 更新）**
> 1. **背景**：南昌 / 惠州原各一份同名 `NJCT.dll`，易冲突（commit `ec0bd1f`）；视觉合并为单文件用类名区分。
> 2. **做法**：CTAutoSlicer 改反射 `NJCT.HZSwdCT`；新增 `OfflineCtDllUpdateService`（SysCenter `HZ_LianXianCT_Dll` / `hz_lixian_ct`，仅 AI 标注机 `10.215.111.1` 白名单）；下载落 `Dlls\NJCT.dll` 与 exe 同级；本机版本记 `local.json`。
> 3. **落点**：`OfflineCtDllUpdateService` / `VM_CTAutoSlicerPage.OfflineCtDllUpdate.cs` / Settings；接口文档升级 V1.2。

> **2026-08-21 变更（二次手动标注窗口图片异步加载）**
> 1. **背景/做法**：手动标注大图加载卡顿；导出成功后删除源图避免重复加载（commit `3eb2a98`）。
> 2. **落点**：`ManualMeasureAnnotateWindow` / 导出逻辑。

> **2026-08-20 变更（导出残留文件覆盖「全部是」+ 全局热键 Alt+O）**
> 1. **背景**：上次导出中断残留 JPEG，重存弹「VGSTUDIO MAX 警告（是否替换）」（commit `bda4064`）。
> 2. **做法**：JPEG 确定后检测含「全部是(A)」警告窗并点击；新增 Alt+O 锁定/解锁键鼠。
> 3. **落点**：`ConfirmJpegQualityDialogAsync` → `TryDismissFileOverwriteWarningAfterJpegAsync`；`CTAutoSlicerInputHookService`。

> **2026-08-12 变更（直方图 2d：按峰谷 Y 非对称扩展 LUT 两端）**
> 1. **背景**：同名正极预设灰度跨度仍可差数倍；仅中点平移无法适配段宽（commit `839aba7`）。
> 2. **做法**：取 `Y = TargetX − ValleyX`，中点对齐后左端点左移 `LutExpandLeftYFactor×Y`（默认 2）、右端点右移 `LutExpandRightYFactor×Y`（默认 3）；ROI 钳制；因子 0 或 Y<2 跳过。
> 3. **落点**：`ExpandLutSegmentByPeakValleyDistanceAsync`；Settings。

> **2026-08-07 变更（正负极 LUT 双预设 + 双套标点；Ctrl+X→Alt+X）**
> 1. **背景**：正/负极灰阶分布不同，单一 `testlut` 与单套端点无法兼顾（commit `4531a44`/`90f9d5a`）。
> 2. **做法**：按文件名 `-Z`→正极、`-F` 或无法识别→负极，拆双预设（默认 `testlut_Z`/`testlut_F`）与双套 LUT 两点；去掉全局 Esc，中断改 `Alt+X`。
> 3. **落点**：`ResolveRenderingPresetName` / `TryResolveLutSegmentPoints` / `DragLutSegmentMidpointToRedLineAsync` / InputHook / VM。

> **2026-08-06 变更（人工干预超时 10 分钟 + 切片先上后前右）**
> 1. **背景/做法**：人工干预超时 30→10 分钟，超时归档失败文件继续；异常按钮文案改「放入异常文件夹并跳过」（commit `8d0a1c1`）；三视图导出顺序改上→前→右，让送检「上」图先落盘。
> 2. **落点**：`CTAutoSlicerSettings` / `ManualInterventionGate` / `VGAutomationService.SliceExport.cs`。

> **2026-08-05 变更（送检竖图规则 + 内容上移 + AI 预处理固定 CW90）**
> 1. **背景**：给视觉的最终图必须竖图（高≥宽）；NJCT 测试主体偏上裁底、现场偏下裁顶（commit `4ce7146`/`800482b`）。
> 2. **做法**：交接与阶段七统一「宽>高才 CW90」；新增 `OfflineCtContentShiftUpPx`（内容上移、底部黑边填充）；AI 预处理固定 CW90 + 条件提亮落 `{产品}/input/`。
> 3. **落点**：`EnsurePortraitByClockwise90IfLandscape`、`OfflineCtPreBrightenHelper.ShiftContentUpFillBlackBottom`、`OfflineCtAnnotationHelper`。

> **2026-08-04 变更（AI 尺寸单位 mm + 二次标注导出 result/ + VM partial 拆分 + 本地 3D 初始图）**
> 1. **背景/做法**：L1~L5 单位统一 mm（避免 μm/mm 混用）；二次标注默认导出改 `{根}/result/`；`VM_CTAutoSlicerPage` 拆 partial（7294 行→按职责）；本地 `Simple321PointSource=Local` 时 Pre321 前截默认视角 3D 初始图到 `3D初始`（供强化学习，失败不阻断）。
> 2. **落点**：`OfflineCtResultOverlayHelper`、`ManualMeasureExportHelper`、`VM_CTAutoSlicerPage.*.cs`、`Vision3dInitialCaptureDir`。

> **2026-07-31 变更（切图/AI 两机拆分 + 二次手动标注 + Pre321/斜俯视找点 + LUT 中点对齐）**
> 1. **背景**：切图（VG）与 AI 标注（NJCT）负载/权限不同机（commit `5388100`/`1f45ac2`/`c0f4190`/`9c7c506`）；极耳预焊遮挡旧水平肩线法不稳。
> 2. **做法**：`PipelineRunMode`（SliceOnly/AiAnnotateOnly）+ `TopViewHandoffFolder` + 本机 `local.json`；阶段四本地 3D 找点重写为**斜俯视 FloodFill/凸包**（`Vision3dSectionPointsDetector`）；`Simple321PointSource=Local/VisionDll` 二选一；二次手动标注（测距）；LUT 2c 中点对齐红线。
> 3. **落点**：`VGAutomationService.Pre321ViewPrep.cs`/`Simple321.cs`、`OfflineCtVisionService`、`ManualMeasure*`、`OfflineCt*`；约定见规划 §9。

> **2026-07-30 变更（AI 标注序号 + 像素精度 μm/px）**
> 1. **做法**：上视图送检图序号可配 `OfflineCtTopViewSliceIndex`（默认 50）；`OfflineCtPixelSizeUm`（默认 1.0）+ mm/μm 换算展示（commit `b0c5c87`/`a026369`）。
> 2. **落点**：`OfflineCtAnnotationHelper`、Settings。

> **2026-07-28 变更（AI 图像标注接入 + UI 标定采图迁单步）**
> 1. **背景**：实现惠州离线 CT 正极 AI 视觉标注（反射 `NJCT.HZSwdCT`，commit `a420039`）。
> 2. **做法**：`ICTOfflineVisionService/OfflineCtVisionService`；主流程导出后取「上」第 N 张 → CW90 → 落 `AI标注/`；标定采图迁入「单步测试」。
> 3. **落点**：`OfflineCtVisionService`、`VGAutomationService.OfflineCtVision.cs`、XAML；接口文档 V1.0。

> **2026-07-24 变更（VG 进程池并行导入 + 文件结构拆分）**
> 1. **背景**：单文件导入等待十余分钟是瓶颈（commit `ceffcb2`）。
> 2. **做法**：文件队列 + 最多 `MaxVgProcesses`（默认 4）VG 进程；导入体积等待可并行、后处理串行；`VGAutomationService`/VM 拆 partial。
> 3. **落点**：`VGAutomationService.ProcessPool.cs`、`Import.cs`、`InterruptAndWait.cs`；约定见规划 §3.3。

> **2026-07-23 变更（阶段四真机单测跑通 + 偏移置0 + 黄框向量 + 对齐后捕捉）**
> 1. **背景/做法**：三点后偏移 Edit 置 0；向量优先黄框右缘；3-2-1 后捕捉按钮+箭头拖拽；主/单测共用核心方法（规划 §8 / §9.2）。
> 2. **落点**：`RunSimple321AlignmentAsync` / `RunPostAlignCaptureDragAsync` / Settings。

> **2026-07-22 变更（阶段四改本地找点 + DLL 标点并存）**
> 1. **背景**：先试 `CTAutoCutImg.dll` 标点（commit `607066e`），随即改自实现 OpenCV 找点（`fb71cac`），确认 2022 简单 3-2-1 流程（**无独立旧 4b**）。
> 2. **做法**：本地 `Vision3dSectionPointsDetector` + `VisionTopViewVectorPointsDetector`；DLL 封装保留 `Simple321PointSource=VisionDll` 路径。
> 3. **落点**：规划 §9.2；`CutImgAlgorithmService`。

> **2026-07-17 变更（直方图灰度阈值功能）**
> 1. **背景/做法**：掩膜取色→截图→峰谷半坡→拖红线（commit `7755b6b`/`b965307`/`44510c1`）。
> 2. **落点**：`HistogramGrayScaleAnalyzer`、标定 Overlay；约定见规划 §9.1。

> **2026-07-15 变更（2022 切片基础流程 + 快速导入重写）**
> 1. **背景**：从 2024 流程（单 DICOM + 体积渲染 + 灰度拖拽 + Alt+F 后台导入）切到 2022.3 流程（多 `.vgl` + `Ctrl+F+I` 快速导入）。
> 2. **做法**：切片基础主流程（commit `7dee9f8`）；`VGAutomationService` 重构支持中断弹窗轮询与「自动保存恢复」跳过（`56d7cdc`/`c98ebd6`/`6551de2`），快速导入按 Accessibility Insights 重写。
> 3. **落点**：`VGAutomationService`、`UIAutomationHelper` 等；约定见规划 §1 / §8。

---

*本记录与 git 提交 hash（如 `7dee9f8`…`68bb6e9`）对应；跨分支合入可能使归属略有出入。*
