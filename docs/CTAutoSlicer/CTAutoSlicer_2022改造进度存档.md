# CTAutoSlicer 2022 改造进度存档

> **文档版本**：2026-08-20（切片导出：JPEG 后处理文件覆盖「全部是」）  
> **状态**：阶段一～五全部 ✅；阶段七 ✅ 独立 AI 标注模式；进程池并行导入 ✅。  
> **待确认**：两机真机联调；视觉 DLL 找点路径；Pre321 + 斜俯视找点真机核对；**LUT 中点拖拽 + 按 Y 扩展真机核对**；内容上移像素真机调参。  
> **用途**：供下一上下文快速接手，无需重读全部会话历史  
> **关联文档**：`docs/CTAutoSlicer迁移说明.md`、`docs/CT离线AI视觉接口文档.md`、**`docs/离线CT_DLL接入说明.md`**、**`docs/CTAutoCutImg接口文档V1.0.txt`**

**进度图例**：✅ 已完成　🟡 可选增强　⏸ 阻塞　❌ 未开始

> **2026-08-20 增量（切片导出：残留文件覆盖确认「全部是」）**：  
> 1. **背景**：上次导出中断时目标目录可能已有 JPEG 残留；再次保存会弹出「VGSTUDIO MAX 警告」（文件已存在，是否替换）。  
> 2. **做法**：点完「JPEG 质量」→「确定」后，短等并检测含 **「全部是(A)」** 的警告窗；有则 Invoke/Win32 点击「全部是」；写入等待环同步兜底。  
> 3. **区分**：与 3-2-1 完成后仅含「是(Y)」的同名警告窗区分——必须存在「全部是」按钮才处理。  
> 4. **落点**：`ConfirmJpegQualityDialogAsync` → `TryDismissFileOverwriteWarningAfterJpegAsync`；`TryDismissLateFileOverwriteWarningAsync`；常量 `VgOverwriteYesToAllButtonName`。

> **2026-08-12 增量（直方图 2d：按峰谷距离 Y 非对称扩展 LUT 两端）**：  
> 1. **背景**：同名正极预设下灰度跨度仍可能差数倍；仅中点平移无法适配段宽。  
> 2. **做法**：2c 中点对齐红线后，取 `Y = TargetX − ValleyX`（ROI 列距）；左下端点再向左移 `LutExpandLeftYFactor×Y`（默认 2），右上端点再向右移 `LutExpandRightYFactor×Y`（默认 3）；坐标钳制在直方图 ROI 内。  
> 3. **配置**：直方图标定页「左×Y / 右×Y」；因子为 0 或 Y&lt;2 时跳过扩展。  
> 4. **落点**：`ExpandLutSegmentByPeakValleyDistanceAsync`；`CTAutoSlicerSettings.LutExpandLeftYFactor/RightYFactor`；单测文案同步覆盖 2d。

> **2026-08-07 增量（正负极 LUT 双预设 + 双套标点）**：  
> 1. **背景**：正极/负极体积灰阶分布不同，单一 `testlut` 与单一 LUT 端点无法兼顾。  
> 2. **做法**：去掉单一 `RenderingPresetName`；直方图页配置 **正极预设**（默认 `testlut_Z`）与 **负极预设**（默认 `testlut_F`）；**LUT 关键段两端点亦分正/负极两套**分别标定。  
> 3. **选型**：按导入文件名（`Path.GetFileName`）判断：含 `-Z`→正极；含 `-F` 或无法识别→**负极（默认 F）**；同时含 `-Z` 与 `-F` 时亦默认 F。预设与标点共用同一电极判定。  
> 4. **UI**：渲染预设区块从「渲染与厚板」迁入「直方图标定」；LUT 标点拆成「标定正极 LUT 两点」「标定负极 LUT 两点」；厚板页仅保留厚板偏移。  
> 5. **落点**：`CTAutoSlicerSettings.IsPositiveElectrodeFile` / `ResolveRenderingPresetName` / `TryResolveLutSegmentPoints`；`ApplyRenderingPresetAsync`；`DragLutSegmentMidpointToRedLineAsync`（传 namingSourcePath）；旧单套标点启动时迁移到负极。

> **2026-08-07 增量（去掉 Esc 中断；Ctrl+X → Alt+X）**：  
> 1. **去掉**全局 Esc：单击锁输入、双击中断流程（易误触）。标定 overlay 内 Esc 取消仍保留，与全局 Hook 无关。  
> 2. **流程中断快捷键**：`Ctrl+X` 改为 **`Alt+X`**；暂停/继续仍为 `Alt+P`。  
> 3. 落点：`CTAutoSlicerInputHookService` / `VM_CTAutoSlicerPage` / `FlowControl`。

> **2026-08-06 增量（人工干预超时 10 分钟 + 按钮文案）**：  
> 1. **超时**：`DefaultManualInterventionTimeout` 由 **30 分钟** 改为 **10 分钟**（超时仍归档失败文件并继续下一文件）。  
> 2. **异常面板按钮**：原「处理完成」改为 **「放入异常文件夹并跳过」**（语义与归档到「运行失败的文件」一致）。  
> 3. 落点：`CTAutoSlicerSettings` / `CTAutoSlicerPage.xaml` / `VM_CTAutoSlicerPage.FlowControl` / `ManualInterventionGate` / `ProcessPool`。

> **2026-08-06 增量（切片导出先「上」后「前/右」）**：  
> 1. **背景**：三视图导出原顺序为前 → 右 → 上；「上」视图才是交接送检源，后导出会拖长可交接时刻。  
> 2. **做法**：`ExportImageStackSlicesAsync` 视图循环改为 **上 → 前 → 右**；交接仍在三视图全部完成后执行（行为不变，仅「上」落盘更早）。  
> 3. 落点：`VGAutomationService.SliceExport.cs`。

> **2026-08-05 增量（送检内容上移 OfflineCtContentShiftUpPx）**：  
> 1. **背景**：NJCT 测试图主体偏上并裁底部；现场图主体偏下 → 上方空、下方有效区被裁。  
> 2. **做法**：喂 AI 前（竖图校验之后）将内容向上平移 N 像素，**画布尺寸不变**，底部黑边填充；DLL 裁掉黑边而非有效区。  
> 3. **配置**：AI 标注 →「预处理与输出」→「内容上移」px；默认 **0**；持久化 `OfflineCtContentShiftUpPx`。  
> 4. 落点：`OfflineCtPreBrightenHelper.ShiftContentUpFillBlackBottom` / `PrepareFinalInputImage`；`CTAutoSlicerSettings` / VM / XAML。

> **2026-08-05 增量（送检竖图：宽&gt;高才 CW90）**：  
> 1. **根因**：切图交接 `CopyTopViewImageToHandoffFolder` 原先只 `File.Copy`，**不旋转**；CW90 仅在 AI 机 `PrepareFinalInputImage` 固定执行。看中转目录会以为「提前转向没生效」。  
> 2. **约定**：给视觉的最终图必须是**竖图**（高 ≥ 宽）。宽 &gt; 高则顺时针 90°；已竖则不转（避免交接已转后再固定转一次变回横图）。  
> 3. **切图**：交接落盘前 `EnsurePortraitByClockwise90IfLandscape`。  
> 4. **阶段七**：预处理改为同一规则（不再无条件固定旋转）。  
> 5. 落点：`OfflineCtAnnotationHelper` / `OfflineCtPreBrightenHelper` / `SliceExport.MirrorTopViewHandoffAsync` / `OfflineCtVision`。

> **2026-08-04 增量（AI 标注：固定 CW90 → input/）**：  
> 1. 取消界面「上视图顺时针旋转 90°」开关；预处理**固定** CW90°。  
> 2. 旋转（+可选提亮）后写入 **`{产品}/input/{源文件名}`**，再交给 NJCT（视觉 DLL）；报告同目录。  
> 3. 不再依赖 `OfflineCtExportInputForNjct` / `OfflineCtRotateTopViewCw90`（历史配置保留但不影响主流程）。  
> 4. 落点：`OfflineCtPreBrightenHelper` / `VGAutomationService.OfflineCtVision` / AI 侧栏 UI。

> **2026-08-04 增量（二次手动标注导出目录）**：  
> 1. 默认导出由 `{根}/output/` 改为 **`{根}/result/`**，避免打开目录本身已是 `output` 时出现 `output/output`。  
> 2. 全屏标注工具栏可浏览/改选导出目录，并一键恢复默认 `result`；续标仍兼容历史 `output/`。  
> 3. 落点：`ManualMeasureExportHelper` / `VM_ManualMeasureAnnotateWindow` / `ManualMeasureAnnotateWindow.xaml`。

> **2026-08-04 增量（阶段七 AI 尺寸单位统一为 mm）**：  
> 1. 原 `FormatPhysicalLength` / `FormatAiDimensionSummaryText` 在 `< 0.1 mm` 时切 μm，导致汇总栏 L1～L5 单位混用。  
> 2. 现统一为 **mm**（`F4`）：结果图旁注、底部「AI尺寸汇总」、二次标注左栏内存汇总一致。  
> 3. 落点：`OfflineCtResultOverlayHelper`、`OfflineCtAnnotationHelper`。

> **2026-08-04 增量（VM_CTAutoSlicerPage partial 拆分）**：  
> 1. 原约 **7294** 行单文件改为 `partial class`（零行为变更；`[Module]`/DI/XAML 绑定路径不变）。  
> 2. 主文件保留字段、属性、Command、构造与切图主流程入口；方法按职责迁入 Theme / Settings / RoiPreview / Calibration / AiAnnotate / StageTests / CutImgDllTest / FlowControl。  
> 3. 策略与 `VGAutomationService`（2026-07-24）一致；编译已通过（0 error）。

> **2026-08-04 增量（本地算法：直方图后保存 3D 初始原图）**：  
> 1. `Simple321PointSource=Local` 时，`RunSimple321AlignmentAsync` 在 **Pre321 切视角之前**：等待 `Vision3dInitialCaptureWaitMs`（默认 **2000**）→ 按 `Vision3dRoi*` 截一张**默认视角** 3D 原图。  
> 2. 独立目录 `Vision3dInitialCaptureDir`（默认 `C:\AutoSlice\cutpicture\3D初始`），与找点用的 `jietu` 分离；按日保留 7 天；文件名 `vision_3d_initial_{产品标签}_*.png`。  
> 3. 截图失败仅打日志，**不阻断** 3-2-1；`VisionDll` 路径不执行。UI「截图目录」可改路径。

> **2026-08-04 增量（DLL 离线测：单图 → 文件夹批量 + 命名）**：  
> 1. 「CTAutoCutImg DLL 离线测试」输入由单图改为**文件夹**：对目录顶层全部图片（png/jpg/bmp/tif）顺序跑通，单张失败不中断。  
> 2. 标签更名：**3D侧视图**（`GetSectionReferencePoints`）/ **2D侧视图**（`GetObjectTopBottomPoints`）；配置项 `CutImgDllTestSectionFolder` / `CutImgDllTestVectorFolder`（兼容旧单图路径，自动取所在目录）。  
> 3. 标注仍输出到 `CutImgDllTestOutputDir`（默认 `C:\AutoSlice\cutpicture\CutImgDllTest`）；流程栏汇总成功/失败数。

> **2026-08-04 增量（Pre321 门控 + 找点算法日志 + DLL 向量上→下 + 截图前等待 + 截图产品前缀）**：  
> 1. **缺陷**：`RunSimple321AlignmentAsync` 原先只要 `HasPre321ViewPrepPrerequisites` 就切视角，**未判断** `Simple321PointSource`；选「视觉 DLL」时也会跑 Pre321。  
> 2. **修复**：Pre321 **仅** `Simple321PointSource=Local` 且标定齐全才执行；`VisionDll` 一律跳过，**保持默认视角**截图找点（与源 DLL 约定一致）。  
> 3. **日志**：入口 / 3D 找点 / 向量找点 / 结束统一前缀 `【找点算法】`，明确写「上位机本地算法（OpenCV…）」或「视觉 DLL 算法（CTAutoCutImg.dll）」及具体方法名；流程栏与 `_logger` 同步。  
> 4. **DLL 向量点击**：`GetObjectTopBottomPoints` 原始 `[0]→[1]` 真机为下→上；点击前按屏幕 **Y 重排为上→下**（与本地一致）。  
> 5. **截图前等待**：3D /「上」视图截图前统一 `VisionCaptureWaitMs`（默认 **2000**），等 VG 画面坐标消失；Local / VisionDll 均生效。  
> 6. **截图产品前缀**：直方图 / 视觉对齐落盘文件名嵌入导入文件名主干前 **6** 字符（与导出名 `BuildExportBaseName` 同规则），如 `histogram_1161-F_….png`、`vision_3d_1161-F_….png`；主流程传排队条目路径，单测尽量取 CT 体素夹首个文件，否则 `unknown`。  
> 7. **本地 3D 找点分步调试**：调试→单步测试「4b 本地3D找点分步调试」弹窗；对 `vision_3d_*.png` 逐步执行 Mask/中灰/FloodFill/ROI/边缘/直线/Harris/最终三点，每步显示中间图与失败原因。落点 `Vision3dSectionPointsStepProbe` + `Vision3dPointsStepDebugWindow`。  
> 8. 落点：`SliceExportNamingHelper.BuildCaptureProductTag`；`VGAutomationService` 直方图/Simple321/ProcessPool/StageTests。

> **2026-07-31 增量（二次手动标注 · 测距）**：  
> 1. AI 模式侧栏新增「二次手动标注」（`NavManualMeasure=9`；单步测试=10、偏好=11）。  
> 2. AI 模式侧栏新增「检测统计」Demo（`NavStats=12`）：当日总数/OK/NG KPI + OK/NG 饼图 + 分时段产量图；假数据，可刷新。  
> 2. 打开文件夹递归枚举图片 → 全屏工作台：左侧已标/未标列表，中部两点测距（橡皮筋虚线 + CAD 垂直引出线 + 实时 μm/mm）。  
> 3. 像素精度复用 `OfflineCtPixelSizeUm`；`Shift` 正交；默认导出 `{根目录}/result/`（标注界面可改选；兼容历史 `output/`）。  
> 4. 落点：`ManualMeasureAnnotateWindow` / `VM_ManualMeasureAnnotateWindow` / `ManualMeasureExportHelper`；详规 **§22.8**。

> **2026-07-31 增量（直方图 2c：LUT 关键段中点对齐红线）**：  
> 1. **问题**：仅 LUT 预设时，峰值左右偏会导致折线关键斜段偏离峰区。  
> 2. **做法**：应用预设后一次性标定关键段两端点 → 运行时取中点 → 红线到位后水平拖中点到 `TargetScreenX`（整段平移）。  
> 3. **配置**：`LutSegmentPoint*Positive/Negative` + `LutSegmentPointsCalibratedPositive/Negative`；UI「直方图标定 → 标定正极/负极 LUT 两点」。  
> 4. **主流程**：`ApplyHistogramGrayScaleAdjustAsync` 内拖红线后接 `DragLutSegmentMidpointToRedLineAsync`；未标定则跳过。  
> 5. **单测**：「测试直方图灰度调整」同步要求 ROI + LUT 两点，覆盖拖红线 + 中点对齐。

> **2026-07-31 增量（Pre321 + 斜俯视找点 + UI）**：  
> 1. **找点前视角准备（Pre321）**：配置前缀 `Pre321View*`（点击坐标 + 箭头拖拽起终点 + 等待/步进）；服务 `VGAutomationService.Pre321ViewPrep.cs`；主流程在截 3D 前若 **Local 且** 标定齐全则先执行（**VisionDll 不跑**，见 2026-08-04）；单测在「视觉对齐 → ⓪」内。  
> 2. **仅本地算法可见**：`IsSimple321LocalPointSource`；「算法来源」=「上位机本地算法」时才显示 ⓪ 卡片；标题「（上位机本地算法配置）」。  
> 3. **本地 3D 找点重写**：极耳预焊遮挡下旧水平肩线法不适用斜俯视；主路径改为 **上平面种子 → FloodFill → 凸包极端四点**（`Vision3dSectionPointsDetector`）；输出约定不变 `[0]左上 [1]右上 [2]右下 [3]原点`。  
> 4. **UI**：①～④ ROI/下拉/偏移收进「视觉对齐标定」大卡片内四个子卡片；`CasComboBoxStyle` 修复「算法来源」下拉文字落在白底外。  
> 5. 参考图：`Assets/images/pre321_view_ref.png`（Content 复制到输出）。

> **2026-07-31 增量（切图 / AI 两机拆分）**：  
> 1. 流水线模式 `PipelineRunMode`：`SliceOnly`（默认）/ `AiAnnotateOnly`；调试顶栏单选胶囊切换；**本机** `Config/CTAutoSlicer/local.json` 持久化（不落共享 DB，避免两机互盖）。  
> 2. 切图终点：三视图导出后按「前缀+序号」取「上」视图第 `OfflineCtTopViewSliceIndex` 张（前缀 `ImageStackBaseFileName=0`，如 50→`050.jpg`），**改名为 `{导出名}.jpg`** 写入 `TopViewHandoffFolder/` 根目录（硬失败），**不再**串行 NJCT、**不再**套产品子文件夹。  
> 3. AI 模式：「开始流程」扫描交接目录产品子文件夹内图像 → CW90/预提亮/NJCT → `{SliceExportFolder}/{产品名}/`（不启 VG；不再套 AI标注 子目录）。  
> 4. 侧栏按模式显隐；同一「开始流程」按模式分发。

> **重要更正（2026-07-22）**：早期文档将阶段四拆成「4a / 4b」是误解。  
> **阶段四 = 简单 3-2-1 对齐本身**（对话框内：Ctrl+三点定平面 → 偏移 Edit 置 0 →「上」视图 Ctrl+两点定向量 → Ctrl+原点 → 完成）。  
> 「3-2-1」中的「2」即「上」视图向量两点：本地用 `VisionTopViewVectorPointsDetector`（**优先黄框右缘**）；选视觉 DLL 时用 `GetObjectTopBottomPoints`。

> **2026-07-30 增量（阶段四找点二选一）**：  
> 1. 配置项 `Simple321PointSource`：`Local`（默认，上位机 OpenCV）/ `VisionDll`（`CTAutoCutImg.dll` V1.0）。  
> 2. 界面：阶段四「找点算法」下拉；主流程与「测试简单 3-2-1」共用。  
> 3. DLL：`GetSectionReferencePoints`（3D 四点）+ `GetObjectTopBottomPoints`（向量两点）；封装仍为 `CutImgAlgorithmService`。  
> 4. 接口文档副本：`docs/CTAutoCutImg接口文档V1.0.txt`。

> **2026-07-23 增量（真机已单测跑通）**：  
> 1. 三点后、向量前：左侧无名偏移 Edit 点击 → 输入 `0` → Enter（`Simple321OffsetEdit*`）。  
> 2. 向量两点：优先检测 VG **黄色对齐框**右缘旁侧取点（与黄框平行、基线更长）；未检出则回退剪影右缘。  
> 3. 3-2-1 完成后、切片导出前：**对齐后捕捉按钮 + 箭头拖拽**（`PostAlign*`；单测「测试对齐后捕捉拖拽」）。  
> 4. **主流程与单测共用同一核心方法**（见 **§21**）。

> **2026-07-30 增量（AI 标注参数）**：  
> 1. 「上」视图送检图序号改为可配：`OfflineCtTopViewSliceIndex`（1～100，默认 50，兼容原 N/2）。  
> 2. 新增像素精度：`OfflineCtPixelSizeUm`（μm/px，默认 1.0）；界面展示 1 mm = 1000 μm 及 L_μm / L_mm 公式；写入 annotation.json。

> **2026-07-28 增量（界面 + AI 输入朝向）**：  
> 1. 主界面底部「截取3D标定图 / 截取上视图标定图」**迁入调试模式 → 单步测试**「标定采图」区；可输入存放路径（持久化）。  
> 2. 默认路径：`C:\AutoSlice\cutpicture\3D`、`C:\AutoSlice\cutpicture\上视图`（`VisionLearn3dSampleDir` / `VisionLearnTopViewSampleDir`）。  
> 3. AI 标注：送入 NJCT 前将「上」视图 **顺时针旋转 90°**（内存）+ 条件提亮，仅落盘 `AI标注/input_for_njct.*`；JSON/CSV 仍记原始上视图路径。

---

## 0.1 VG 进程池并行导入（2026-07-23）

> 瓶颈在单文件导入等待（十余分钟）。采用 **文件队列 + 最多 N 个进程槽**：导入启动/后处理串行置前；导入体积等待按 PID 静默 UIA 轮询（可并行）。

| 项 | 说明 |
|----|------|
| UI 参数 | `MaxVgProcesses`（默认 **4**，钳制 1～8），页面「导入参数」→「最大VG进程数」 |
| 调度 | 优先后处理 `ImportDone` → 槽未满则 `Process.Start` 新 VG 开导入 → 否则等待导入任务 |
| 拆分 | `StartImportOnlyAsync` / `WaitForImportCompleteAsync(activateWhenDone:false)` / `RunPostImportPipelineAsync` |
| 成功 | 删除该 `.vgl/.vgi`，`Kill` 对应 VG，腾槽 |
| 失败 | `Kill` 对应 VG，**保留文件**，记 `@@IMPORT_FAIL@@` |
| 附着 | 主流程**禁止**附着已有 VG（始终冷启动）；单步测试仍可用 `GetCTViewerProcess` |

```mermaid
flowchart TD
  Q[文件队列 .vgl/.vgi] --> Fill{槽位未满?}
  Fill -->|是| Launch[冷启动 VG + StartImportOnly]
  Launch --> Wait[后台静默等导入完成]
  Wait --> Done[ImportDone]
  Fill -->|否| Prefer
  Done --> Prefer{有 ImportDone?}
  Prefer -->|是| Post[置前 + 后处理串行]
  Prefer -->|否| Idle[等待导入 Task]
  Post -->|成功| Del[删文件 + Kill VG]
  Post -->|失败| Keep[保留文件 + Kill VG]
  Del --> Fill
  Keep --> Fill
```

---

## 0. 直方图灰度阈值（2026-07-17 起；2026-07-22 修订目标线；2026-07-31 增 LUT 中点对齐；2026-08-12 增按 Y 扩展）

> 背景：LUT 预设不保存直方图中间红色阈值竖线位置，需截图分析后拖拽。  
> 半坡目标约定：取**高度最高的两个尖峰**；左线=两峰间波谷，右线=右侧尖峰；  
> **目标线（红线）** = 左线 + (右线−左线)×**2/3**（靠右约三分之一，利于压掉左侧噪点灰阶；**2026-07-22 起不再用左右中点**）。  
> **2c LUT 中点对齐（2026-07-31）**：预设关键斜段两端一次性标定 → 中点水平拖到红线目标列，使折线随峰值左右偏自适应。  
> **2d 按 Y 扩展（2026-08-12）**：`Y = TargetX − ValleyX`；中点对齐后左端再左移 `左因子×Y`、右端再右移 `右因子×Y`，适配同电极下灰度跨度差异（非对称扩展后中点可略偏红线）。

| 阶段 | 状态 | 说明 |
|------|------|------|
| ROI 框选标定 | 🟡 UI 已实现 | 「截图标定」→ 上位机最小化 → 全屏灰遮罩框选 → ✓提交 / ✕重选 / Esc 取消；坐标写入 DB |
| 掩膜取色 | 🟡 UI 已实现 | 「取色」→ 9×9 放大格 + HEX/RGB → 左键填入 `HistogramMaskColor`（默认 `#6E6E6E`） |
| 按坐标截图 + 颜色替换 | 🟡 单步可测 | 目标色→纯绿 → **去全宽绿带 + 下填接回** → 峰谷 → `histogram_compare_*.png` |
| 红线初始位置 | 🟡 单步可测 | `HistogramRedLineDetector`；拖拽起点 |
| 峰谷 / 半坡 | ✅ 单步 + 主流程 | 两最高尖峰 → 左线=谷、右线=右尖、**目标=靠右 2/3**；调试图 `histogram_peaks_*.png` |
| 拖拽红线 | ✅ 单步 + 主流程 | `MouseDragPressMoveRelease` 水平拖到 T*（目标线） |
| **LUT 关键段两端标定** | ✅ UI | 「标定正极/负极 LUT 两点」→ 全屏依次点选；`LutSegmentPoint*Positive/Negative` |
| **LUT 中点对齐红线** | ✅ 单步 + 主流程 | 按 `-Z`/`-F` 取对应套中点 → 水平拖到 `TargetScreenX`；未标定则主流程跳过 |
| **LUT 按 Y 非对称扩展** | ✅ 单步 + 主流程 | `Y=Target−Valley`；左×`LutExpandLeftYFactor`、右×`LutExpandRightYFactor`；ROI 钳制；Y&lt;2 或因子 0 跳过 |
| 接入主流程 | ✅ | `ImportSingleVglAsync`：预设之后、厚板之前；未标定 ROI 则跳过整步；未标定 LUT 两点则跳过 2c/2d |

**关键入口**：

| 文件 | 说明 |
|------|------|
| `ScreenColorPickerOverlayWindow` | 全屏取色（先截虚拟屏再采样，避免取到预览窗） |
| `HistogramRoiCalibrationOverlayWindow` | 全屏 ROI 标定 |
| `ScreenTwoPointPickerOverlayWindow` | 全屏两点标定（LUT 关键段 P1→P2） |
| `HistogramGrayScaleAnalyzer` | 目标色→纯绿 + 峰谷半坡 + 对比/峰谷调试图；**TargetX = 左+(右−左)×2/3** |
| `VGAutomationService.CaptureHistogramRoiAsync` / `TestHistogramGrayScaleAdjustAsync` | 截图→分析→拖红线→LUT 中点对齐→按 Y 扩展 |
| `VGAutomationService.DragLutSegmentMidpointToRedLineAsync` | 2c：标定中点 → 红线目标列 |
| `VGAutomationService.ExpandLutSegmentByPeakValleyDistanceAsync` | 2d：按 Y 非对称扩展两端 |
| Settings：`HistogramMaskColor` / `HistogramMaskTolerance` / ROI / 截图目录（按日子目录，**保留 7 天**） | |
| Settings：`LutSegmentPoint*Positive/Negative` / `LutSegmentPointsCalibratedPositive/Negative` | 正/负极 LUT 关键段两端；主流程未标定对应套则跳过 2c/2d |
| Settings：`LutExpandLeftYFactor` / `LutExpandRightYFactor`（默认 **2** / **3**） | 2d 左右扩展倍数；UI「直方图标定」可调 |
| Settings：`Vision3dRoi*` / `VisionTopViewRoi*` / `Simple321ToolbarClickX/Y` / `Simple321OffsetEditX/Y` / `VisionCaptureDir`（`jietu`） / `Vision2dVectorCaptureDir`（`2Dxianglaing`） / **`EnableVisionAlignmentSteps`（默认开）** | 阶段四标定与主流程开关；均按日保留 **7 天** |
| Settings：`Pre321View*`（Click / Drag / WaitMs） | 找点前视角准备；仅 `Simple321PointSource=Local` 时 UI 显示 **且** 主流程执行；VisionDll 跳过 |
| Settings：`PostAlignCaptureButton*` / `PostAlignDrag*` / **`EnablePostAlignCaptureDragSteps`（默认开）** | 对齐后捕捉按钮 + 箭头拖拽；主流程在 3-2-1 后、导出前 |

**真机步骤**：露出直方图 → 取色对准填充灰 → 截图标定 ROI → **分别应用正/负极预设后标定对应 LUT 关键段两端** → 「测试直方图灰度调整」→ 核对 `compare`/`peaks` 图中「左/右/**目标**」线 → 观察红线拖到目标线后关键斜段中点是否对齐红线，再核对两端是否按 Y 因子左右扩展。

---

## 1. 改造背景

| 维度 | 改造前（2024 流程） | 改造后（2022 流程，当前） |
|------|---------------------|---------------------------|
| VG 版本 | VGSTUDIO MAX 2024，`Alt+F` → Ribbon 后台导入 | VGSTUDIO MAX **2022.3**，**`Ctrl+F+I`** 快速导入（按住 Ctrl，依次按 F、I；真机标定） |
| 输入数据 | 单个 **DICOM 文件夹**（`CTFileFolder` 填目录路径） | 文件夹内多个 **`.vgl` 工程文件**（直接子级） |
| 单次点击处理量 | 1 个 DICOM 体数据 | N 个 `.vgl`，按创建时间升序循环 |
| 导入后步骤 | 体积渲染等待 → 灰度直方图拖拽 → 简单对齐 → 双视图截图算角旋转 | 导入完成 → **渲染 LUT 预设** ✅ → 四象限厚板/对齐/切片（见 §7） |
| 进程名 | 曾硬编码 `vgstudiomax20241` | 从 UI 配置的 `VGAppPath` 推导 |

**业务名称**仍为「CT 自动化切片」。当前已跑通全流程：  
**批量 `.vgl` 导入 → LUT 预设 → 直方图灰度阈值 → 三象限厚板 → 简单 3-2-1 对齐 → 对齐后捕捉拖拽 → 上/前/右三视图切片导出**。  

**阶段四（视觉对齐）**：✅ **全部完成**（即简单 3-2-1，无独立「4b」）。  
- 单测 + 主流程（`EnableVisionAlignmentSteps`，默认开；厚板后、导出前）  
- **本地路径**：Pre321 斜俯视准备（仅 `Simple321PointSource=Local`）→ `Vision3dSectionPointsDetector`（FloodFill/凸包四点）+ `VisionTopViewVectorPointsDetector`（**黄框优先**）  
- **视觉 DLL 路径**：`Simple321PointSource=VisionDll` → **跳过 Pre321**，默认视角截图 → `GetSectionReferencePoints` + `GetObjectTopBottomPoints`（`CTAutoCutImg.dll`）  
- 顺序（Local）：⓪Pre321 → 截 3D → Ctrl+三点 → **偏移 Edit 置 0** →「上」Ctrl+两点 → Ctrl+原点 → 完成 → 警告「是」  
- 之后：**捕捉按钮 + 箭头拖拽**（`EnablePostAlignCaptureDragSteps`）  
- 未标定齐全时跳过并对该文件打日志；日志前缀 `【找点算法】` 标明当前路径  

**阶段五切片范围**：UI「切片导出参数」中按视角分别配置（`SliceRangeFront*Mm` / `Right*` / `Top*`）；导出时写入对应视图。见 §7.1 / §11 / §16～§23。

---

## 2. 当前已跑通的整体流程（阶段一～五）

用户选择 **CT 体素文件夹** → 点击 **「流程模拟」** → 自动执行：

```mermaid
flowchart TD
    Start[流程开始] --> Launch[连接/启动 VG 进程]
    Launch --> Step1[WaitForVgOpen<br/>轮询队列弹窗或「文件(F)」]
    Step1 -->|命中队列「自动保存恢复」| Skip[Drain：点「跳过」]
    Skip --> Step1
    Step1 -->|「文件(F)」可点| Front[后续步骤]
    Front --> StepN[ExecuteStepWithRetry]
    StepN -->|异常/失败| Drain[Drain 中断弹窗队列]
    Drain -->|已处理| StepN
    Drain -->|无弹窗| Fail[抛出步骤失败]
```

另：延迟出现的「自动保存恢复」不靠固定等待；登记在 `_interruptDialogQueue`，任意步骤异常或未达标时先 Drain 再重试。

另提供独立按钮：

- **「测试应用渲染预设」**：假定 VG 中**已导入体积**，仅执行预设应用（不重跑导入）。
- **「测试厚板设置(三象限)」**：假定 VG 中**已导入体积且已应用 LUT**，依次执行象限 1→2→3 厚板设置（不重跑导入）。

### 2.1 单文件导入子流程（`ImportSingleVglAsync`）

| 步骤 | 动作 | 完成判定 / 备注 |
|------|------|-----------------|
| 1 | `Ctrl+N` 新建/清空工程 | 固定等待 `NewProjectDialogWaitTime` |
| 2 | 处理保存提示 | 窗口 `VGSTUDIO MAX 问题` → `否(N)` / Enter |
| 3 | **`Ctrl+F+I`** 打开快速导入（图一） | 轮询 `快速导入...` 出现即返回；见 §15 |
| 4 | 点击 Edit `文件名(N):` 输入路径（图二）→ 点击 `打开(O)`（图三） | 不再用 `Alt+O` 为主路径；对话框关闭后交图四 |
| 5 | 等待文件选择向导（图四） | Button `完成(F)` 且 Enabled（**原逻辑保留**） |
| 6 | 向导内 Edit 再输入同一路径 | ValuePattern 或键盘 fallback |
| 7 | 点击「完成(F)」元素完成文件选择 | 向导消失；**勿用 Alt+F/Alt+N**（Alt+N=下一步） |
| 8 | 等待 `请耐心等待...` 出现 | 确认导入已开始 |
| 9 | 轮询进度框消失 | 超时上限 `ImportTimeoutMinutes` |
| 10 | `PostImportWaitTime` 稳定等待 | 默认 3000ms |
| 11 | `ApplyRenderingPresetAsync` | 等待 `PresetApplyWaitTime` → 按文件名 `-Z`/`-F` 选正/负极预设 → 打开「渲染」面板 ComboBox → 选中对应 ListItem |
| 11b | `ApplyHistogramGrayScaleAdjustAsync` | 已标定 ROI 时：截图→峰谷半坡→拖红线→LUT中点对齐→按Y扩展；未标定 ROI 跳过整步；未标定 LUT 两点跳过 2c/2d |
| 12 | `ApplyThickPlateForQuadrantsAsync` | 象限 1→2→3：视口右键 →「厚板」→ 启用厚板模式 → 组合模式「平均值」→ 确定 |

**失败策略**：单文件任意步骤超时/异常 → `@@IMPORT_FAIL@@文件名|原因` → **跳过继续**下一个文件。

> **2026-07-15 起**：图一～图三已按 Accessibility Insights 重写；图四 `ConfirmFileSelectionAsync` 保持原操作。细节与 Q&A 见 **§15**。

### 2.2 渲染预设应用子流程（`ApplyRenderingPresetAsync`）— 阶段二 ✅

| 步骤 | 动作 | 完成判定 / 备注 |
|------|------|-----------------|
| 1 | `PresetApplyWaitTime` 等待 | 默认 2000ms（导入进度消失 + `PostImportWaitTime` 之后） |
| 2 | `ActivateVgWindowAsync` 置前 VG | 与导入阶段一致 |
| 3 | 定位「渲染」窗口 + ComboBox | Automation：`Window「渲染」` → `ControlType.ComboBox` |
| 4 | 打开下拉 | **Expand**（每步最多 800ms）→ **Alt+Down** → 点击组合框；任一步检测到子 `List` 可见即继续，不再每步耗满 `PresetDropdownWaitTime` |
| 5 | 查找 `ListItem`（Name = 预设名） | `ComboBox → List` → 渲染面板 → 同进程顶层；100ms 轮询 |
| 6 | Win32 点击 + Enter 确认 | Qt：`Select` 仅高亮；**立即** Win32 点击 + 回车 |
| 7 | 完成判定 | **下拉已关闭** 或 ValuePattern 读数匹配；读数滞后时不失败（仅 UIA 诊断日志） |

**等待策略（预设，2026-07-16）**：跳过 `TrySetComboBoxValue` + 5s 读数等待（Qt 常误报成功却读数不变）；打开下拉每步最多 **800ms**；选中后以**下拉关闭**为主判据，与厚板组合模式一致。

**单步测试**：`TestRenderingPresetAsync` / 页面按钮「测试应用渲染预设」（不执行导入）。

**实现说明**：2022 真机验证后，灰度调整改为 **直接应用已保存的 LUT 预设**（如 `testlut`），不再拖拽直方图；页面参数「渲染预设名称」填 Insights 中 `ListItem` 的 Name。若 ValuePattern 已是目标预设名则**跳过**打开下拉。

### 2.3 三象限厚板设置子流程（`ApplyThickPlateForQuadrantsAsync`）— 阶段三 ✅

| 步骤 | 动作 | 完成判定 / 备注 |
|------|------|-----------------|
| 1 | 按视口中心点四象限划分定位 Q1～Q3 | `UIAutomationHelper.FindViewportQuadrants`（左上/右上/左下/右下）；Q3 右键点额外上移 `ViewportQuadrant3ClickOffsetY`（默认 -100px，避免靠近屏幕底缘菜单反向弹出） |
| 2 | 右键前 `ThickPlateStepDelayMs`（默认 500ms） | 固定步间等待 |
| 3 | 视口中心右键 → 偏移点击「厚板」 | 菜单不可见于 UIA，用 `ThickPlateMenuOffsetX/Y`（真机标定 90/127）；右键后等待 `ThickPlateMenuAfterRightClickDelayMs`（默认 200ms）再点菜单 |
| 4 | 等待「厚板设置」对话框 | `WaitForElementAsync` 轮询（找到即继续，非固定多等） |
| 5 | `TogglePattern` 勾选「启用厚板模式」 | 失败则 Win32 点击复选框中心 |
| 6 | 展开组合模式 → 选「平均值」 | `ExpandCollapse` + `WaitForElementAsync` 等 ListItem（100ms 轮询，找到即点）；Win32 点击 + Enter |
| 7 | Invoke / 点击「确定」 | 等待对话框消失 |

**等待策略（厚板）**：**混合模式** — 控件出现类步骤用轮询「找到即触发」；关键动作之间用 `ThickPlateStepDelayMs`（500ms）固定步间等待。组合模式**不以 UIA 组合框读数判成败**（Qt 读数滞后），以点击+回车后继续点「确定」、对话框关闭为准。

**单步测试**：`TestThickPlateAsync` / 页面按钮「测试厚板设置(三象限)」（不执行导入，一次跑完 Q1→Q2→Q3）。

---

## 3. 真机 UI Automation 标定（Accessibility Insights）

| 场景 | 窗口/控件 Name | 触发方式 |
|------|----------------|----------|
| 保存提示 | `VGSTUDIO MAX 问题` / `否(N)` | Enter 或 Invoke |
| **快速导入（图一～三，2026-07-15）** | 对话框 `快速导入...`；Edit `文件名(N):`（`Edit(50004)` / 亦可为 ComboBox 内 Edit）；Button `打开(O)`（AccessKey Alt+O） | **`Ctrl+F+I`**；点击输入框写路径；**点击「打开(O)」**（Invoke → Win32 点击） |
| 文件选择向导（图四） | 窗口 Name 含 `来自 .vgl 文件的体积`；内文「文件选择」；`完成(F)` / `下一步(N)` 等 | 等「完成(F)」→ 向导内输路径 → **直接点击「完成(F)」元素**（禁用 Alt+N/Alt+F，Alt+N=下一步） |
| 导入进度 | `请耐心等待...`（内文「正在导入体积」/「正在写入图像」） | 出现→**可见**进度框消失后再继续；仅离屏 UIA 残留可忽略 |
| 渲染预设下拉 | `渲染` 窗口 / `ComboBox` / `List` / `ListItem` Name=预设名 | Expand 或 Alt+Down；选中用 SelectionItem + Win32 点击 |
| 象限视口 | 四宫格 `Group`/`Pane`，按中心点划分 Q1～Q4 | 右键菜单「厚板」用相对偏移点击；Q3 右键点上移 100px |
| 厚板设置对话框 | 窗口 `厚板设置` / CheckBox `启用厚板模式` / ComboBox + ListItem `平均值` / Button `确定` | 组合模式：Win32 点击 + Enter；不以 UIA 读数判成败 |
| 切片导出对话框 | 窗口 `保存切片/图像堆栈` / Group `2D 视图`+ComboBox / Group `图像大小`+Edit / Group `堆栈设置` 内 Text `切片图数量：` + **Custom** 输入框 / Group `切片图范围` 内 Text `开始 [mm]`/`结束 [mm]` + **Custom** 输入框 / Button `保存...` | **Alt+F+T** |
| 保存图像对话框 | 嵌套 `对话框 保存图像...` / Edit `文件名:`（Alt+N）/ Button `保存(S)` | 保存后弹出 |
| JPEG 质量对话框 | 嵌套 `窗口 JPEG 质量` / Button `确定`（AccessKey **Enter**） | Invoke → Win32 点击 → Enter |
| 文件覆盖警告（残留） | `VGSTUDIO MAX 警告` / 文案含「已经存在」/ Button **`全部是(A)`**（AccessKey 常为 Enter） | JPEG「确定」后短等；有则 Invoke → Win32 点击「全部是」；写入等待环兜底 |
| 导出写入进度 | 嵌套 `窗口 请耐心等待...` / Text `正在写入图像` | 消失后再导出下一视图；默认超时 60s |
| ~~菜单 Plan B~~ | ~~`文件(F)` / `快速导入(I) Ctrl+I`~~ | **2026-07-15 已从主路径删除**（曾作为 Ctrl+I 失败后备） |
| VG 启动就绪 | `文件(F)` MenuItem 可交互 | `IsEnabled` + 非离屏 + 有尺寸 |

---

## 4. 关键代码入口（下一上下文从这里读）

### 4.1 主流程编排

| 文件 | 方法 | 说明 |
|------|------|------|
| `SUNWODA_SEVB/ViewModels/Pages/CTAutoSlicer/VM_CTAutoSlicerPage.cs` | `ExecuteRunSimulationAsync` / `ExecuteSlicePipelineAsync` | 页面核心：字段/属性/构造；切图主流程入口 |
| `.../VM_CTAutoSlicerPage.Settings.cs` | `LoadSettings` / `BuildSettings` | 参数加载与 `CTAutoSlicerSettings` 组装 |
| `.../VM_CTAutoSlicerPage.Theme.cs` | 主题色 / 预设 | 偏好配色 |
| `.../VM_CTAutoSlicerPage.RoiPreview.cs` | ROI 预览 Overlay / 主窗尺寸 | 截图标定预览与窗体记忆 |
| `.../VM_CTAutoSlicerPage.Calibration.cs` | `ExecuteCalibrate*` | 直方图/LUT/3D/上视图/321/Pre321/PostAlign 标定 |
| `.../VM_CTAutoSlicerPage.StageTests.cs` | `ExecuteTest*` | 单步测试命令 |
| `.../VM_CTAutoSlicerPage.CutImgDllTest.cs` | CutImg DLL 离线批量测 | 3D/2D 文件夹批量 |
| `.../VM_CTAutoSlicerPage.AiAnnotate.cs` | AI 预览 / 批处理 / 统计 Demo | 阶段七 UI 与入口 |
| `.../VM_CTAutoSlicerPage.FlowControl.cs` | 日志 / 人工干预 / 热键 | `AppendFlowLog`、暂停、Alt+X / Alt+P |
| `SUNWODA_SEVB/Services/Project/CTAutoSlicer/CTAutoSlicerService.cs` | `RunSimulationAsync` | 门面；管理 `IsRunning` / Cancel |
| `SUNWODA_SEVB/Services/Project/CTAutoSlicer/VGAutomationService.cs` | （partial 核心） | 字段/常量/构造/`WaitUntil*`；对外类型仍为 `VGAutomationService` |
| `.../VgAutomation/VGAutomationService.ProcessPool.cs` | `RunSimulationAsync` / `RunPostImportPipelineAsync` | **核心**：进程池调度 + 后处理串行编排 |
| `.../VgAutomation/VGAutomationService.InterruptAndWait.cs` | `WaitForVgOpenAsync` / 中断弹窗 | 启动等待、自动保存恢复、后处理独占、文件队列 |
| `.../VgAutomation/VGAutomationService.Import.cs` | `OpenQuickImportAsync` / `WaitForImportCompleteAsync` | 阶段一：快速导入 |
| `.../VgAutomation/VGAutomationService.LutAndHistogram.cs` | `ApplyRenderingPresetAsync` | 阶段二：LUT 预设 |
| `.../VgAutomation/VGAutomationService.ThickPlate.cs` | `ApplyThickPlateForQuadrantsAsync` | 阶段三：厚板 |
| `.../VgAutomation/VGAutomationService.Simple321.cs` | `RunSimple321AlignmentAsync` | 阶段四：简单 3-2-1（Local 截 3D 前可接 Pre321；VisionDll 跳过） |
| `.../VgAutomation/VGAutomationService.Pre321ViewPrep.cs` | `RunPre321ViewPrepAsync` | 找点前视角准备（点击+拖拽） |
| `.../VgAutomation/VGAutomationService.HistogramAndPostAlign.cs` | `ApplyHistogramGrayScaleAdjustAsync` / `DragLutSegmentMidpointToRedLineAsync` / `ExpandLutSegmentByPeakValleyDistanceAsync` / `RunPostAlignCaptureDragAsync` | 直方图灰度（含 LUT 中点 + 按 Y 扩展）+ 对齐后捕捉拖拽 |
| `.../VgAutomation/VGAutomationService.SliceExport.cs` | `ExportImageStackSlicesAsync` | 阶段五：切片导出 |
| `.../VgAutomation/VGAutomationService.UiPrimitives.cs` | 点击/重试/键盘/`Find*` 包装 | UIA 原语；通用查找转发至 `VgElementFinder` |
| `.../VgAutomation/VgElementFinder.cs` | `FindWindowByNameScoped` 等 | **内部**通用查找类（不改动 `Tool/Helper`） |
| `.../VgAutomation/VGAutomationService.StageTests.cs` / `PublicApi.cs` | `Test*` / `CaptureHistogramRoiAsync` / Lock | 单测入口与公开辅助 API |

> **拆分说明（2026-07-24）**：`VGAutomationService` 原约 7000 行单文件已按阶段拆为 `partial class`（零行为变更，DI/`ICTAutoSlicerService` 不变）。Phase 2 已抽出 `VgElementFinder`；Import/导出/3-2-1 等仍以 partial 文件为边界（共享 UIA 宿主私有方法，暂不强行拆独立类以免改公共可见性）。  
> **拆分说明（2026-08-04）**：`VM_CTAutoSlicerPage` 同样按职责拆为 `partial class`（见上表）；对外类型名与模块注册不变。

### 4.2 UI Automation 工具

| 文件 | 关键 API |
|------|----------|
| `SUNWODA_SEVB.Tool/Helper/UIAutomationHelper.cs` | `FindFileMenuForProcess`、`WaitForElementAsync`、`FindViewportQuadrants`、`TryToggleCheckBox`、`TrySelectListItem`、`GetComboBoxDisplayText` |
| `SUNWODA_SEVB.Tool/Helper/AutomationWinAPIHelper.cs`（`WinAPIHelper`） | `ClickScreen`（`SetCursorPos` + `mouse_event`）用于 Qt 下拉确认点击 |
| `SUNWODA_SEVB.Tool/Helper/MouseSimulationHelper.cs` | 点击/移动/拖拽（**TestOffset 用，主流程未用**） |
| `SUNWODA_SEVB.Tool/Helper/ScreenCaptureHelper.cs` | 区域截图（**主流程未用**） |
| `SUNWODA_SEVB.Tool/Helper/PathGenerationHelper.cs` | 直线/圆弧拖拽路径（**主流程未用**） |

### 4.3 参数模型

`SUNWODA_SEVB.Core/Models/Project/CTAutoSlicer/CTAutoSlicerSettings.cs`

- 文件后缀常量：`VglFileExtension = ".vgl"` / `VgiFileExtension = ".vgi"` / `ShortcutFileExtension = ".lnk"`
- 排队识别：`IsImportVolumeExtension`（真实文件）+ `LooksLikeImportVolumeShortcut` / `IsImportVolumeQueueEntry`（如「xxx.vgi - 快捷方式.lnk」）
- 快捷方式解析：`ShellLinkHelper.TryGetShortcutTarget` → VG 导入用目标路径；成功删除仅删 `.lnk`
- 进程池：`MaxVgProcesses`（默认 4）/ `DefaultMaxVgProcesses` / `AbsoluteMaxVgProcesses = 8`
- 默认 VG 就绪等待：`DefaultVGOpenWaitTime = 180000`
- 旧库值自动升级阈值：`LegacyVGOpenWaitTimeThreshold = 10000`（≤10000 视为过短，升为 180000）

---

## 5. 已实现的基础设施（非导入主链，但已可用）

| 能力 | 文件 | 状态 |
|------|------|------|
| 流程日志 UI（200 行、智能滚动、可清除） | `VM_CTAutoSlicerPage` + `CTAutoSlicerPage.xaml` | ✅ 已用 |
| 文件日志 `Assets/CTAutoSlicer/import_log.txt` | `VM_CTAutoSlicerPage.WriteFlowLogToFile` | ✅ 已用 |
| 全局热键（Alt+X 中断 / Alt+P 暂停） | `CTAutoSlicerInputHookService` | ✅ 已用（已移除 Esc） |
| CT 文件夹监控（仅记日志，**不触发流程**） | `CTFolderMonitorService` | ✅ 已挂载 |
| 进程 CPU/内存/GPU CSV | `ProcessMonitorService` | ✅ 页面初始化启动 |
| 角度算法 DLL 加载 | `AngleAlgorithmService.InitializeAsync` | ✅ 页面初始化；**阶段四主链用本地找点，不依赖本 DLL** |
| 切图视觉 DLL | `CTAutoCutImg.dll` + `CutImgAlgorithmService` | ✅ 已拷入 `Dlls\` 并封装反射调用（§16）；Halcon 依赖同目录 + `SetDllDirectory` |
| 偏移量测试按钮 | `VGAutomationService.TestOffsetAsync` | ✅ 灰度锚点/开始菜单等（主流程灰度已改为预设，锚点测试可逐步废弃） |
| 渲染预设单步测试 | `TestRenderingPresetAsync` +「测试应用渲染预设」按钮 | ✅ 已用 |
| 厚板三象限单步测试 | `TestThickPlateAsync` +「测试厚板设置(三象限)」按钮 | ✅ 已用 |
| DI 注册 | `ProjectServiceCollectionExtensions.AddCTAutoSlicerServices` | ✅ |

---

## 6. 运行参数一览（`project_setting`，`belong_to_vm = VM_CTAutoSlicerPage`）

### 6.1 阶段一 / 阶段二（预设）/ 阶段三（厚板）已生效

| 参数 | 默认值 | 用途 |
|------|--------|------|
| `VGAppPath` | 用户配置 | VG exe 路径；进程名由此推导 |
| `VGOpenWaitTime` | 180000 ms | 「文件(F)」就绪检测上限；每秒轮询 |
| `VGShowFrontWaitTime` | 2000 ms | 置前后等待（厚板单测已改用 `EnsureVgForeground`，不重复等 2s） |
| `CTFileFolder` | 用户选择 | 扫描 `.vgl`/`.vgi`，以及「xxx.vgi - 快捷方式.lnk」（解析目标导入，成功只删快捷方式） |
| `MaxVgProcesses` | 4（1～8） | **进程池最大 VG 并发数**；导入可重叠，后处理串行 |
| `QuickImportDialogWaitTime` | 10000 ms | **Ctrl+F+I** 后轮询「快速导入...」出现的上限（找到即继续） |
| `NewProjectDialogWaitTime` | 5000 ms | Ctrl+N 后等保存框 |
| `FileSelectionDialogWaitTime` | 15000 ms | 等「完成(F)」/ 进度框出现 |
| `ImportTimeoutMinutes` | 30 | 单文件导入进度框消失超时 |
| `PostImportWaitTime` | 3000 ms | 导入完成后稳定等待 |
| `RenderingPresetNamePositive` | `testlut_Z` | 正极；文件名含 `-Z` 时选用 |
| `RenderingPresetNameNegative` | `testlut_F` | 负极；文件名含 `-F` 或无法识别时默认选用 |
| `LutExpandLeftYFactor` / `LutExpandRightYFactor` | **2** / **3** | 直方图 2d：按 `Y=Target−Valley` 左右扩展倍数；0 关闭对应侧 |
| ~~`RenderingPresetName`~~ | ~~`testlut`~~ | **已拆为正负极双预设**（见上） |
| `PresetApplyWaitTime` | 2000 ms | 导入完成后、打开预设下拉前的等待 |
| `PresetDropdownWaitTime` | 5000 ms | 打开下拉后等待 `ListItem` 出现的超时 |
| `ThickPlateMenuOffsetX` / `Y` | 90 / 127 | 右键点后相对偏移，点击「厚板」菜单项 |
| `ThickPlateMenuAfterRightClickDelayMs` | 200 ms | 右键后、点「厚板」菜单前的等待（防卡顿） |
| `ThickPlateStepDelayMs` | 500 ms | 厚板流程关键动作之间的固定步间等待 |
| `ThickPlateStepRetryCount` | 1 | 厚板单象限失败重试次数 |
| `ViewportClickOffsetY` | 0 | 视口右键点相对中心的 Y 偏移 |
| `ViewportQuadrant3ClickOffsetY` | -100 | 第三象限（左下）额外上移，避免底缘菜单反向 |
| `ViewportMinWidth` / `MinHeight` | 400 / 300 | 四宫格视口候选最小尺寸 |
| `ThickPlateDialogWaitTime` | 3000 ms | 等待「厚板设置」出现/关闭的超时（轮询上限，非固定多等） |
| `ThickPlateDropdownWaitTime` | 3000 ms | 组合模式下拉 ListItem 出现超时（轮询上限） |
| `StepRetryCount` | 3 | 关键步骤重试次数（厚板用独立 `ThickPlateStepRetryCount`） |
| `StepRetryIntervalMs` | 2000 ms | 重试间隔 |

### 6.1.1 阶段五（切片导出）已生效

| 参数 | 默认值 | 用途 |
|------|--------|------|
| `SliceExportFolder` | 用户配置 | 切片导出目标目录 |
| `SliceExportImageWidth` | 3000 | 导出对话框「图像大小-宽」 |
| `SliceExportQuantity` | 100 | 局部图像（非 QJ）导出数量 |
| `SliceExportQuantityQj` | 150 | QJ 全局图像导出数量（运行时同步 UI） |
| `SliceRangeStartMm` | 20 | 切片范围开始 [mm] — **UI 配置**；前/右/上三视图导出共用 |
| `SliceRangeEndMm` | 70 | 切片范围结束 [mm] — **UI 配置**；前/右/上三视图导出共用 |
| `SliceExportDialogWaitTime` | 8000 ms | 等待导出/质量确认对话框超时 |
| `SliceExportWriteTimeoutMs` | 60000 ms | 等待「正在写入图像」进度框消失（默认 1 分钟） |
| `SliceExportViewFrontName` | `前 1` | 2D 视图下拉项（前） |
| `SliceExportViewRightName` | `右 1` | 2D 视图下拉项（右） |
| `SliceExportViewTopName` | `上 1` | 2D 视图下拉项（上） |

### 6.2 仍在 UI/模型中，但主流程未使用（多为 2024 遗留）

| 参数 | 原用途 | 下一阶段建议 |
|------|--------|--------------|
| `SelectCTFileFolderWaitTime` | 2024 DICOM 选目录等待 | 2022 导入后若不用可隐藏或复用 |
| `ImportFinishWaitTime` | 2024 导入完成等待 | 可能被 `PostImportWaitTime` 替代 |
| `VolumeRenderWaitTime` | 2024 体积渲染完成等待 | **2022 未使用**；无独立渲染等待，导入后直接应用 LUT 预设 |
| `ImportMenuItemOffset` | 2024 Ribbon 导入菜单 Y 偏移 | 菜单 Plan B 已删除；可废弃 |
| `StartMenuXOffset` / `StartMenuYOffset` | 2024 开始菜单位置 | 待确认 2022 是否仍需要 |
| `GrayScaleGraphAnchor*Offset` | 2024 灰度直方图拖拽锚点 | **已由渲染预设替代**；UI 保留，可后续隐藏 |
| `RotationRadius` | 视图旋转拖拽半径 | 2024 遗留；**阶段四新主链不以角度拖拽定方向**（见 §16），可暂保留 |
| `EnableVisionAlignmentSteps` | 是否执行阶段四简单 3-2-1 | 默认 `true`；关则跳过对齐直接导出 |

---

## 7. 2022 真机完整业务流程（目标链 vs 当前进度）

### 7.1 实验室确认的完整切片流程（预设之后）

VG 四象限布局下，单个体积从 **导入 + LUT 预设** 到 **切片完成** 的顺序如下（**以本节为准**）：

```text
✅ 1. 导入 .vgl 体积（Ctrl+F+I 快速导入 → 图一～四，见 §15）
✅ 2. 应用渲染 LUT 预设（「渲染」面板 ComboBox）
✅ 2b. 直方图灰度阈值（截图→峰谷半坡→拖红线；未标定 ROI 则跳过）
✅ 2c. LUT 关键段中点对齐红线（两端一次性标定；未标定则跳过）
✅ 2d. LUT 按峰谷 Y 非对称扩展两端（`Y=Target−Valley`；左×2 / 右×3 默认可配；未标定两端则跳过）

✅ 3. 第一、二、三象限：各视口右键 →「厚板」→ 启用 → 组合模式「平均值」→ 确定
      （`ApplyThickPlateForQuadrantsAsync`）

✅ 4. 阶段四 · 简单 3-2-1 对齐（全部完成，无独立 4b）
      **⓪ Pre321（仅本地算法）**：`Simple321PointSource=Local` 且标定齐全 → 点击切换视图 + 箭头拖拽至斜俯视；
      **VisionDll 一律跳过 Pre321**，按 **默认视角** 截图找点（`CTAutoCutImg`）
      → 第四象限 3D ROI 截图 → Local：Vision3dSectionPointsDetector / VisionDll：GetSectionReferencePoints
      → 绝对坐标打开「简单对齐」→ 重置
      → Ctrl+三点（左上→右上→右下）
      → **偏移 Edit 置 0 + Enter**（`Simple321OffsetEdit*`；否则「上」视图非预期）
      → 「上」视图 ROI 截图 → Local：VisionTopViewVectorPointsDetector（黄框优先）/ VisionDll：GetObjectTopBottomPoints → Ctrl+两点
      → Ctrl+原点 → 完成(F) → 警告「是(Y)」
      （`RunSimple321AlignmentAsync`；开关 `EnableVisionAlignmentSteps`；找点源 `Simple321PointSource`；日志前缀 `【找点算法】`）

✅ 4b'. 对齐后捕捉与拖拽（非旧「4b」）
      点视口底部无名捕捉按钮（UIA 索引/Invoke，失败绝对坐标）→ 按箭头标定直线拖拽
      （`RunPostAlignCaptureDragAsync`；开关 `EnablePostAlignCaptureDragSteps`）

✅ 5. Alt+F+T 三视图切片导出（`ExportImageStackSlicesAsync`）
      依次导出「上 / 前 / 右」；切片范围取自 UI 各视角 mm
      → **上视图交接**（`MirrorTopViewHandoffAsync`）：复制第 OfflineCtTopViewSliceIndex 张到 TopViewHandoffFolder
      （切图模式终点；硬失败）

✅ 6. 阶段七 · AI 图像标注（独立模式，见 §22）
      `PipelineRunMode=AiAnnotateOnly` → `RunAiAnnotationBatchAsync`
      扫描 TopViewHandoffFolder → CW90° → NJCT → `{结果根}/{产品名}/`
      （不启 VG；与切图主链解耦）

❌ 7. 收尾：导入成功后 .vgl 处理、文件夹监控自动跑批等（见 §8 阶段六，可选）
```

**说明**：

- **象限**：第四象限 = 3D（平面三点+原点）；第二象限 =「上」视图（向量两点）。  
- **无旧 4b**：早期文档的「4b 定方向」已取消；向量两点即 3-2-1 中的「2」。  
- **切图主链时序**：直方图 → 厚板 → 简单 3-2-1（含偏移置0）→ 对齐后捕捉拖拽 → 导出 → **交接上视图送检图**。  
- **AI 独立机**：读交接目录批量标注，与 VG 无关。  

### 7.2 任务流程：已改造步骤 vs 未改造步骤

**说明**：2022 **无**独立「体积渲染完成等待」步骤；导入进度消失后，以 `PostImportWaitTime` + `PresetApplyWaitTime` 等待，再应用 LUT 预设。

#### 已改造步骤（主流程已接入）

```text
✅ 锁定/解锁输入
✅ 启动/附着 VG 进程
✅ 「文件(F)」就绪检测（替代盲目固定等待）
✅ 多 .vgl 顺序导入（Ctrl+F+I 快速导入 →「请耐心等待」消失；检测禁止桌面整树扫描）
✅ 导入后短暂稳定等待（PostImportWaitTime，默认 3000ms）
✅ 渲染 LUT 预设应用（PresetApplyWaitTime 后 → 按 `-Z`/`-F` 选正极/负极 →「渲染」面板 ComboBox）
✅ 直方图灰度阈值（预设后：截图→峰谷半坡→拖红线→中点对齐→按Y扩展；未标定 ROI 则跳过）
✅ 三象限厚板设置（`ApplyThickPlateForQuadrantsAsync`，象限 1→2→3）
✅ 简单 3-2-1 对齐（含偏移置0 + 黄框向量；`RunSimple321AlignmentAsync` + `EnableVisionAlignmentSteps`）
✅ 对齐后捕捉拖拽（`TryRunPostAlignCaptureDragInMainFlowAsync` + `EnablePostAlignCaptureDragSteps`）
✅ 三视图切片导出（`ExportImageStackSlicesAsync`；上/前/右）
✅ 上视图交接（`MirrorTopViewHandoffAsync` → `TopViewHandoffFolder`）
✅ AI 图像标注批处理（`RunAiAnnotationBatchAsync`；独立模式，扫描交接目录）
✅ 「测试应用渲染预设」单步按钮
✅ 「测试直方图灰度调整」单步按钮
✅ 「测试厚板设置(三象限)」单步按钮
✅ 「测试简单 3-2-1 对齐」单步按钮
✅ 「测试对齐后捕捉拖拽」单步按钮
✅ 「测试切片导出(三视图)」单步按钮
✅ 「测试 AI 图像标注」单步按钮
✅ 单步测试「标定采图」：截取 3D / 上视图标定图 + 可配置存放路径
✅ 失败跳过、批次统计、流程日志
⏸ 批次结束 Ctrl+N（开发阶段暂注释，保留工程状态）
```

#### 可选 / 未做（非阶段四）

| 状态 | 步骤 | 说明 |
|------|------|------|
| ✅ | **阶段四 · 简单 3-2-1 对齐** | **已完成**（单测+主流程）。含三点+偏移置0+黄框向量+原点；**无独立旧 4b**。见 §16～§21 |
| ✅ | **对齐后捕捉拖拽** | **已完成**（单测+主流程）。见 §20 |
| ✅ | **切片范围（UI mm）** | 按前/右/上分视角配置 |
| ✅ | **Alt+F+T 保存影片/图像堆栈** | 三视图依次选参并导出；已接入主流程 |
| ✅ | **阶段七 · AI 图像标注** | **代码已接入**（主流程+UI+单测；推理前上视图 CW90°）；真机联调待确认。见 §22 |
| ❌ | 导入成功后删除 `.vgl` | 进程池路径已删排队文件；策略见 §0.1 |
| ❌ | 文件夹监控触发自动跑批 | 可选产品需求 |
| ~~🟡~~ | ~~阶段四 · 旧 4b~~ | **已取消** |

#### §7.2 附：与旧文档「双视图截图算角」的关系

| 旧描述（2024 迁移稿 / 早期阶段四草稿） | 当前结论（2026-07-21） |
|--------------------------------------|------------------------|
| `Ctrl+Alt+J` 简单对齐 + 前/右视图截图 + `MeasureAngle` + 拖拽旋转 | **不是 2022 当前主链** |
| 「简单 1,2,3 对齐选面 / 旋转角度 / 三切面范围」三句空描述 | **已具体化为**：3D 截图→三点点击定平面 → 「上」视图截图→两点定方向 → 导出 |
| `AngleAlgorithmService` / `Secondary_positioning` | **仍保留**（页面初始化加载）；阶段四主链用本地找点；旧角度 DLL 不参与 |
| `ScreenCaptureHelper` | **阶段四必用**：3D ROI、「上」视图 ROI 区域截图 |
| 视口放大按钮 `M`（Accessibility 同名不可区分） | 若截图前需最大化某视口，应 **按象限容器 BoundingRectangle 定位**，勿全局 `FindByName("M")` |

---

## 8. 下一阶段改造建议（推荐顺序）

### 阶段三：三象限厚板设置（✅ 已完成）

真机已验证：`ApplyThickPlateForQuadrantsAsync` 已接入 `ImportSingleVglAsync`；页面提供「测试厚板设置(三象限)」单步按钮。

### 阶段四：简单 3-2-1 对齐（✅ 全部完成；无独立 4b）

> **结论（2026-07-22）**：阶段四 **仅**「简单 3-2-1 对齐」，已全部完成（单测 + 主流程）。  
> 早期文档的「4a / 4b」拆分是误解——「3 / 2 / 1」三步都在同一对话框内完成。  
> ~~4b / `GetObjectTopBottomPoints` 另做定方向~~ → **已取消，不再开发**。

**在总流程中的位置**（`ImportSingleVglAsync`）：

```text
… → 直方图灰度调整 → 三象限厚板 → 【简单 3-2-1（含偏移置0）】→ 【对齐后捕捉拖拽】→ Alt+F+T 切片导出 → …
```

**对话框内步骤（不可颠倒；仅原点完成后点「完成」）**：

| 步 | 含义 | 采图 / 找点 | 动作 | 状态 |
|:--:|------|-------------|------|:----:|
| ⓪ | 找点前视角（仅 Local） | — | 点击切换视图 + 箭头拖拽（`Pre321View*`） | ✅ |
| 3 | 定平面 | 3D ROI → `Vision3dSectionPointsDetector`（斜俯视 FloodFill） | Ctrl+左上→右上→右下 | ✅ |
| 1.5 | 偏移置0 | — | 点击左侧无名 Edit → `0` → Enter | ✅ |
| 2 | 定向量 | 「上」ROI → `VisionTopViewVectorPointsDetector`（黄框优先） | Ctrl+上点→下点 | ✅ |
| 1 | 定原点 | 同上 3D 四点之原点 | Ctrl+原点 → 完成 → 警告「是」 | ✅ |

**已落地清单**：

1. ✅ 本地 3D 找点（斜俯视）+「上」视图向量找点（黄框优先，§18 / §19）  
2. ✅ Pre321 视角准备：`RunPre321ViewPrepAsync` / 标定 UI / 主流程仅 Local 接入（VisionDll 跳过，§23）  
3. ✅ `RunSimple321AlignmentAsync` / `ResetSimple321PlaneOffsetEditAsync` / `ClickTopViewVectorPointsAsync` / `TryRunVisionAlignmentInMainFlowAsync`  
4. ✅ Settings + UI：ROI、下拉、**偏移 Edit**、`VisionCaptureDir`、`Vision2dVectorCaptureDir`、`EnableVisionAlignmentSteps`、`Simple321PointSource`  
5. ✅ 对齐后捕捉拖拽：标定 + `RunPostAlignCaptureDragAsync` + 主流程 + 单测（§20）  
6. ✅ 单测按钮 + 离线探针  
7. ✅ 主流程接入（厚板后 → Pre321? → 3-2-1 → 捕捉拖拽 → 导出）  
8. ✅ UI：⓪ 仅本地算法显示；①～④ 大框套四小卡片；算法来源下拉样式修复

**接入点（已落地）**：

```csharp
await ApplyThickPlateForQuadrantsAsync(...);
await TryRunVisionAlignmentInMainFlowAsync(...);      // EnableVisionAlignmentSteps + 标定齐全时跑 3-2-1
await TryRunPostAlignCaptureDragInMainFlowAsync(...); // EnablePostAlignCaptureDragSteps + 标定齐全
await ExportImageStackSlicesAsync(...);
```

**阶段五**：切片导出已完成；范围取自 UI。可选巩固项见 §11。

### 阶段五：切片导出（✅ 已完成）

- **`OpenSaveImageStackDialogAsync`** — **Alt+F+T**，等待「保存影片/图像堆栈」对话框。
- **`ExportImageStackSlicesAsync`** — 循环 **上 / 前 / 右** 三视图（**先上**）：选 2D 视图 → 填宽度/数量/**切片范围（UI `SliceRangeStartMm`～`SliceRangeEndMm`）** → 保存 → JPEG → **（可选）覆盖警告「全部是」** → 等待写入进度 → 重开对话框。
- 命名规则见 `SliceExportNamingHelper`：导出名取导入文件名主干**前 6 字符**（例 `1161-F 2026-7-25….vgi - 快捷方式` → `1161-F`）；路径 `{导出根}/{1161-F}/{1161-F} 前|右|上/…`；阶段七结果图 `{1161-F}/{1161-F}.jpg`；二次标注叠加图亦以该名为主干。
- 页面「切片导出参数」Expander（含切片范围起止 mm）+「测试切片导出(三视图)」按钮。

**说明**：主流程在简单 3-2-1 之后导出；三视图共用同一 UI 切片范围。

### 阶段七：AI 图像标注（✅ 代码已接入；真机联调待确认）

> **2026-07-28**：已按惠州正极接口 V1.0 接入主流程与 UI。详规见 **§22**、`docs/CT离线AI视觉接口文档.md`、`docs/离线CT_DLL接入说明.md`。  
> **位置**：阶段五切片导出 **之后**；输入为「上」视图文件夹第 **OfflineCtTopViewSliceIndex** 张图（默认 50）。  
> **输出**：`{SliceExportFolder}/{exportBaseName}/AI标注/`（与前/右/上同级）。

**已落地**：

| 项 | 说明 |
|----|------|
| 服务 | `ICTOfflineVisionService` / `OfflineCtVisionService`（反射 `NJCT.NJSwdCT`） |
| 选图/落盘 | `OfflineCtAnnotationHelper`（`TryPickTopViewImageByIndex`） |
| 主流程 | `TryRunOfflineCtVisionAnnotationAsync`（软失败） |
| UI | 侧栏「AI图像标注」；单测「测试 AI 图像标注」 |
| 资源 | `Dlls\NJCT.dll`、`CF.dll`、`Dlls\Models\CTOffline\model_500000_900.onnx` |

**UI 配置**（默认值）：

| 参数 | 默认 | 说明 |
|------|------|------|
| `EnableOfflineCtVisionAnnotation` | `true` | 主流程开关 |
| `OfflineCtTopViewSliceIndex` | `50` | 「上」视图送检图序号（1～100） |
| `OfflineCtPixelSizeUm` | `1.0` | 像素精度（μm/px）；1 mm = 1000 μm |
| `OfflineCtModelPath` | 空→默认 onnx | ONNX 路径 |
| `OfflineCtIsRunCuda` | `true` | CUDA |
| `OfflineCtBlackHeight` | `1500` | 打黑高度 |
| `OfflineCtResultImageMode` | 增强图 | 增强 / 原图 / 两者 |
| `OfflineCtExportJson` / `Csv` | `true` | 坐标尺寸落盘 |

### 阶段六：收尾与自动化增强

5. **`OnImportSucceededAsync`** — `.vgl` 删除或移至备份目录（需业务确认）。  
6. **失败文件策略** — 可扩展 `failed/` 子目录。  
7. **`CTFolderMonitorService`** — 新 `.vgl` 进目录自动跑批（与 `IsRunning` 互斥）。  
8. **清理遗留 UI** — `VolumeRenderWaitTime`、灰度锚点偏移、`ImportMenuItemOffset` 等 2024 专用项，按 2022 实测隐藏或重命名。

### 已废弃 / 降级的旧建议

~~**`WaitForVolumeReadyAsync`（可选）**~~ — 当前导入链已稳定，非必需。  
~~**`AdjustGrayScaleAsync`（灰度直方图拖拽）**~~ — 已由 `ApplyRenderingPresetAsync` 替代。  
~~**阶段三旧版：`OpenSimpleAlignmentAsync` + `CaptureAndRotateViewAsync(前/右视图)`**~~ — 与 §7.1 / §16 不一致。  
~~**阶段四旧草稿：仅写「选面 / 转角 / 三切面范围」三句且依赖旧角度 DLL**~~ — **2026-07-22 起以 §16～§18 为准（4a 本地找点；DLL 封装保留）**。

### 阶段二（续）：可选优化

- 若导入完成信号不稳，可再加条件轮询（树节点/进度条）；**当前不做**。

---

## 9. 实现阶段三～五时的代码模板（建议）

在 `VGAutomationService.cs` 中扩展单文件流程（**与当前真实顺序对齐**）：

```csharp
// ImportSingleVglAsync 末尾：
await ApplyRenderingPresetAsync(...);
await ApplyHistogramGrayScaleAdjustAsync(...);   // 已实现；未标定 ROI 则跳过
await ApplyThickPlateForQuadrantsAsync(...);     // 阶段三 ✅

// 阶段四：视觉对齐 — 默认跳过，EnableVisionAlignmentSteps=true 时执行（见 §16）
if (settings.EnableVisionAlignmentSteps)
{
    await RunVisionAlignmentAsync(settings, ctViewerProcess, mainWindow, cancellationToken, progress);
}
else
{
    progress?.Report("跳过：阶段四视觉对齐（3D 三点选面 / 「上」视图方向对齐）");
}

// 阶段五：Alt+F+T 三视图切片导出 ✅
await ExportImageStackSlicesAsync(...);
```

阶段四若跳过，阶段五联调时可使用 **人工已在 VG 中完成对齐** 的工程文件。

**可复用依赖**：

- 象限定位：`UIAutomationHelper.FindViewportQuadrants` + 视口 `BoundingRectangle`（勿全局 `FindByName("M")`）
- 截图：`ScreenCaptureHelper.CaptureRegionAndSave`（3D ROI、「上」视图 ROI）
- 点击：`MouseSimulationHelper` / `WinAPIHelper.ClickScreen`
- 新算法：`CutImgAlgorithmService` → `CTAutoCutImg.dll` / 类型 `CT`（**不是**旧 `AngleAlgorithmService`）
- 旧角度 DLL / `PathGenerationHelper` / `RotationRadius`：阶段四新主链 **不依赖**；可保留供兼容

---

## 10. 已知问题与已处理项

| 问题 | 处理 |
|------|------|
| 冷启动 7s 就超时、不触发导入快捷键 | DB 旧值 7000；代码 `ResolveVgOpenWaitTime` + 页面加载自动升级 |
| 快速导入弹窗已开却空等 ~6～9s 才输入 | 旧 `FindQuickImportDialog` 对 `RootElement` 做 `FindAll(Descendants)`；已改为 VG 主窗 Children / 同进程顶层；见 §15 |
| 预设/厚板/导出步骤体感略卡 | 阶段二～四仍有多处 `RootElement.FindAll(Descendants)` 兜底；**2026-07-16** 已统一为 `Find*Scoped` |
| 预设步骤日志空等 ~15s（打开下拉→找到列表项） | 旧逻辑：`TrySetComboBoxValue` 后 `WaitForPresetApplied` 5s + 打开下拉每步 5s + 等 ListItem 5s；**2026-07-16** 已改为跳过 ValuePattern、打开每步 800ms、Win32 点击后以**下拉关闭**判成败 |
| 输入路径前无故放大快速导入窗 | 旧 `EnsureAutomationForeground` 调用 `ShowWindow(3)=SW_MAXIMIZE`；已改为仅 `SetForegroundWindow`；见 §15 |
| 后缀 `.vgi` → 实测为 `.vgl` | `VglFileExtension`；现同时支持 `.vgl`/`.vgi` |
| 体素文件夹不想复制巨大 `uint16_scv` | **支持「xxx.vgi - 快捷方式.lnk」**：解析目标路径导入，成功只删文件夹内快捷方式 |
| 点「完成」后很久才开下一 VG | 旧逻辑死等向导「完成」按钮 UIA 消失；**已改为**检测内文「**正在导入体积**」（或向导消失）即交手填下一槽 |
| 后处理中仍切换 VG 窗口 | 根因：`GetFreshMainWindow` 每次 `EnsureVgForeground`（最大化+置前），后台「导入体积中」轮询抢焦点；**已改为**取窗不置前 + 后处理独占暂停其他槽 UIA |
| 首次点击只开 VG 不导入 | 旧逻辑已移除；启动段重写为 `WaitForVgOpenAndDismissAutoSaveAsync`（弹窗按主窗**子窗口**查找，对齐 Insights 树） |
| NuGet NU1102（`System.ServiceModel.Security`） | 与 CTAutoSlicer 无关；需加 nuget.org 源 |
| `TestOffset` 中「导入菜单」仍用 2024 Qtitan Ribbon | 主流程已不用；测试按钮待 2022 更新或删除 |
| Qt 预设下拉 `Select` 仅高亮、不应用 | `SelectionItemPattern.Select` 后须 Win32 `ClickScreen`；点击前短暂 `Unlock` |
| Qt 厚板组合框 UIA 读数滞后 | 不以 `GetComboBoxDisplayText` 判成败；点击+回车后直接点「确定」，以对话框关闭为准 |
| 第三象限靠近屏幕底缘右键菜单反向 | `ViewportQuadrant3ClickOffsetY = -100`，右键点上移 |
| 预设测试结束跨线程异常 | `ExecuteTestRenderingPresetAsync` 去掉 `ConfigureAwait(false)`；`finally` 用 `Dispatcher.Invoke` |
| Halcon / `CTAutoCutImg` 加载失败 | 须 **`Dlls\` 同目录** + 调用前 **`SetDllDirectory`**；`halcon*.dll` 与 `CTAutoCutImg.dll` 一并部署，勿仅从 Demo 路径 Load |
| 「简单对齐」下拉 UIA 不可靠 | 使用 **`Simple321ToolbarClickX/Y` 屏幕绝对坐标** 点开下拉，再选菜单项（Insights 标定） |
| 4a「完成(F)」等待超时 | 流程中可能弹出 **警告**；自动化须穿插点击 **「是(Y)」** 后再继续判定完成 |
| 3D ROI 标定框错象限 | **勿框进第二象限「上」视图**；仅第四象限 3D 有效像素区，否则 DLL 点位与点击全偏 |

---

## 11. 验证清单

### 阶段一回归

- [ ] 页面「VG启动就绪最大等待」显示 **180000**（或用户自定义 ≥30000）
- [ ] 冷启动：`文件(F)` 就绪后即使晚几秒才出「自动保存恢复」，后续步骤异常日志应出现「检测到中断弹窗」→「已点击跳过」→ 重试成功
- [ ] 自动 **`Ctrl+F+I`** → 检测到「快速导入...」后**立即**输入路径（日志上 Ctrl+F+I 与「已检测到」应接近，不应空等数秒）→ 点击「打开(O)」→ 文件选择向导 → `请耐心等待` 消失
- [ ] 输入路径时**不应**最大化「快速导入」对话框（仅置前即可）
- [ ] 文件夹内 2+ `.vgl` 按创建时间顺序处理
- [ ] 故意损坏一个文件路径，验证失败跳过且批次继续
- [ ] 流程日志 + `import_log.txt` 有完整步骤
- [x] Alt+X 可中断（原 Esc / Ctrl+X 已废弃）

### 阶段二（渲染预设）回归

- [x] VG 已导入体积后，「测试应用渲染预设」可将组合框设为配置的预设名（如 `testlut`）
- [x] 「流程模拟」单文件链：导入完成 → 日志出现 `渲染预设「xxx」已应用`
- [ ] 换用其他 Insights 中存在的 `ListItem` Name，改「渲染预设名称」后仍能应用
- [ ] 预设名为空时跳过预设步骤（不报错中断）

### 阶段三（厚板 + 平均值）— 已完成

- [x] Insights 标定：象限一～三视口矩形、右键菜单偏移、组合模式「平均值」
- [x] 单步测试按钮「测试厚板设置(三象限)」：已导入 + 已预设前提下 Q1→Q2→Q3
- [x] 主流程 `ImportSingleVglAsync` 已接入 `ApplyThickPlateForQuadrantsAsync`
- [x] 第三象限底缘菜单反向弹出：右键点上移 100px

### 阶段四（简单 3-2-1）— ✅ 全部完成（无旧 4b）

- [x] 流程文档：时序、采图、点击换算、主流程接入（§7.1 / §8 / §16～§21 / §23）
- [x] 标定：3D ROI、「上」视图 ROI、`Simple321ToolbarClickX/Y`、**`Simple321OffsetEditX/Y`**
- [x] **Pre321**：`Pre321View*` 点击 + 箭头拖拽标定；单测在 ⓪；主流程仅 Local 且标定齐全时执行（VisionDll 跳过，2026-08-04）
- [x] **UI**：⓪ 仅「上位机本地算法」显示；①～④「视觉对齐标定」大框套四小卡片；算法来源下拉样式
- [x] 落盘：`VisionCaptureDir`（jietu）、`Vision2dVectorCaptureDir`（2Dxianglaing），均 **7 天**
- [x] 本地找点：`Vision3dSectionPointsDetector`（**斜俯视 FloodFill/凸包**）+ `VisionTopViewVectorPointsDetector`（**黄框优先**）+ 探针
- [x] 完整 3-2-1：重置 → Ctrl+三点 → **偏移置0** → Ctrl+向量两点 → Ctrl+原点 → 完成 → 警告「是」
- [x] 单测按钮「测试简单 3-2-1 对齐」
- [x] `EnableVisionAlignmentSteps` 主流程接入（默认开）
- [x] `Simple321PointSource` 本地 / 视觉 DLL 二选一
- [x] 对齐后捕捉拖拽：标定 UI + 单测「测试对齐后捕捉拖拽」+ 主流程（§20）
- [x] **主流程 ↔ 单测一致性核对**（§21，2026-07-23）
- [x] ~~独立旧 4b~~ **已取消**（非需求）
- [ ] 真机巩固（可选）：Pre321 后斜俯视四点与用户标注一致性（尤其原点）

### 阶段五（Alt+F+T 切片导出）— ✅ 已完成

- [x] Insights 标定：`Alt+F` + `T` 打开「保存影片/图像堆栈」
- [x] 三视图依次选择 + 参数填写 + 导出（上/前/右；先上）
- [x] **切片范围取自 UI**（按视角 Front/Right/Top）
- [x] 输出路径与命名规则可配置（`SliceExportNamingHelper`）
- [x] JPEG「确定」后：残留文件「VGSTUDIO MAX 警告」→「全部是(A)」（可选弹窗）
- [ ] 真机巩固（可选）：堆栈 Edit、保存对话框、JPEG 质量窗、覆盖「全部是」、写入进度

### 阶段七（AI 图像标注）— ✅ 代码已接入；真机联调待确认

- [x] 反射封装 `OfflineCtVisionService`（`ModelInit` / `ModelAction` / `ModelAction_origin`）
- [x] UI 侧栏「AI图像标注」+ Settings Load/Save
- [x] 主流程：导出后取「上」视图配置序号图 → **CW90°** → 落盘 `{主文件夹}/AI标注/`
- [x] 单测：「测试 AI 图像标注」（手动选图或按目录规则；同样先旋转）
- [x] JSON/CSV 可配置（默认勾选）；结果图模式三选一（默认增强图）
- [x] 推理前上视图顺时针 90°（内存）+ `input_for_njct.*`
- [x] **上视图切片序号可配（1～100）+ 像素精度 μm/px + 换算公式展示**
- [x] 工程编译通过（2026-07-28）
- [ ] 真机：日志出现选用配置序号（默认 `50.*`），`AI标注/` 含旋转图 + 结果图与 json/csv

### UI 增量（2026-07-28）— ✅ 标定采图迁入单步测试

- [x] 底部栏去掉「截取3D标定图 / 截取上视图标定图」
- [x] 调试模式 → 单步测试 →「标定采图」：路径输入 + 浏览 + 截取
- [x] 默认 `C:\AutoSlice\cutpicture\3D` / `C:\AutoSlice\cutpicture\上视图`（Settings 持久化）

---

## 12. 相关文件速查（实现状态截至 **2026-07-31**）

```
SUNWODA_SEVB.Core/Interfaces/Project/CTAutoSlicer/
  ICTAutoSlicerService.cs            ← ✅ TestPre321ViewPrepAsync 等
  IAngleAlgorithmService.cs          ← 旧角度 DLL（保留，阶段四主链不用）
  ICutImgAlgorithmService.cs         ← ✅ CTAutoCutImg 封装接口
  ICTOfflineVisionService.cs         ← ✅ 阶段七 NJCT 反射接口
SUNWODA_SEVB.Core/Models/Project/CTAutoSlicer/
  CTAutoSlicerSettings.cs            ← ✅ OfflineCt*；Pre321View*；VisionLearn*SampleDir
  PipelineRunMode.cs / CTAutoSlicerLocalConfig.cs  ← ✅ 两机拆分本机配置
  OfflineCtResultImageMode.cs        ← ✅ Enhance/Origin/Both
  OfflineCtInferResult.cs            ← ✅ 推理结果 DTO
SUNWODA_SEVB/Services/Project/CTAutoSlicer/
  CTAutoSlicerService.cs
  CTAutoSlicerLocalConfigService.cs  ← ✅ local.json
  VGAutomationService.cs
  VgAutomation/VGAutomationService.OfflineCtVision.cs
  VgAutomation/VGAutomationService.Pre321ViewPrep.cs  ← ✅ 找点前视角准备
  VgAutomation/VGAutomationService.Simple321.cs       ← ✅ 截 3D 前接 Pre321（仅 Local；VisionDll 跳过）
  OfflineCtVisionService.cs
  AngleAlgorithmService.cs
  CutImgAlgorithmService.cs
  …
SUNWODA_SEVB.Core/Dlls/
  NJCT.dll / CF.dll
  Models/CTOffline/model_500000_900.onnx
  CTAutoCutImg.dll                   ← 阶段四对照保留
  halcondotnet.dll / halcon*.dll
SUNWODA_SEVB.Tool/Helper/
  OfflineCtAnnotationHelper.cs
  OfflineCtPreBrightenHelper.cs
  SliceExportNamingHelper.cs
  Vision3dSectionPointsDetector.cs   ← ✅ 斜俯视 FloodFill/凸包四点（2026-07-31 重写）
  VisionTopViewVectorPointsDetector.cs
  ScreenCaptureHelper.cs
  …
SUNWODA_SEVB/ViewModels/Pages/CTAutoSlicer/
  VM_CTAutoSlicerPage.cs             ← ✅ 核心：字段/属性/构造/切图入口（partial）
  VM_CTAutoSlicerPage.Settings.cs    ← ✅ Load/BuildSettings
  VM_CTAutoSlicerPage.Theme.cs       ← ✅ 主题色
  VM_CTAutoSlicerPage.RoiPreview.cs  ← ✅ ROI 预览 / 主窗尺寸
  VM_CTAutoSlicerPage.Calibration.cs ← ✅ 标定 Overlay
  VM_CTAutoSlicerPage.StageTests.cs  ← ✅ 单步测试
  VM_CTAutoSlicerPage.CutImgDllTest.cs ← ✅ CutImg DLL 离线测
  VM_CTAutoSlicerPage.AiAnnotate.cs  ← ✅ AI 预览/批处理/统计
  VM_CTAutoSlicerPage.FlowControl.cs ← ✅ 日志/热键/人工干预
SUNWODA_SEVB/Views/Pages/CTAutoSlicer/
  CTAutoSlicerPage.xaml              ← ✅ ⓪ 条件显隐；视觉对齐大框四小卡；算法来源下拉
  CTAutoSlicerPageStyles.xaml        ← ✅ CasComboBoxStyle / CasFormSubSectionStyle
SUNWODA_SEVB/Assets/images/
  pre321_view_ref.png                ← ✅ Pre321 目标视角参考图
docs/
  CTAutoSlicer迁移说明.md
  CT离线AI视觉接口文档.md
  离线CT_DLL接入说明.md
  CTAutoSlicer_2022改造进度存档.md
  NJCT_1jpg失败原因分析.md
```

---

## 13. 给下一上下文的阅读顺序

1. **本文档** — 知道做到哪、还差什么（§7.1 总序；**§16**；**§18～§21**；**§22 AI**；**§23 Pre321/UI**；导入 §15）  
2. **`docs/CT离线AI视觉接口文档.md`** + **`docs/离线CT_DLL接入说明.md`** — 接口与依赖  
3. **`docs/NJCT_1jpg失败原因分析.md`** — 样本 `1.jpg` 失败根因（CF→`Threshold(200)` / 非 ROI&lt;100 硬伤）  
4. `VGAutomationService` — `RunPostImportPipelineAsync` / `RunOfflineCtVisionAnnotationAsync` / `Pre321ViewPrep` / `Simple321`  
5. `OfflineCtVisionService` + `OfflineCtAnnotationHelper` + **`OfflineCtPreBrightenHelper`** — 反射推理、CW90°、**预提亮**、落盘  
6. `ImportSingleVglAsync` / 进程池 — 主链：… → 导出 → **交接上视图**（切图）或 AI 批处理  
7. `docs/CTAutoSlicer迁移说明.md` §6 — 页面生命周期  

---

## 14. 一句话总结

**阶段一～五已跑通；阶段七 AI 图像标注为独立模式**（切图交接 / AI 批处理两机拆分）。阶段四本地路径：Pre321 斜俯视 → FloodFill 找点 → 3-2-1 → 对齐后拖拽。UI：⓪ 仅本地算法显示；①～④ 大框套小卡。待真机巩固 Pre321 后四点与两机联调。可选后续：文件夹监控跑批等（阶段六）。

---

## 15. 2026-07-15 快速导入（图一～三）重写与 Q&A

> 目标：按 Accessibility Insights 真机标定重写「打开快速导入 → 输入路径 → 点打开」；图四文件选择向导 **`ConfirmFileSelectionAsync` 不删，按原操作保留**。  
> 主战场：`VGAutomationService.cs`（`OpenQuickImportAsync` / `QuickImportFillAndOpenAsync` / `FindQuickImportDialog` / `BringAutomationElementToFront` / `SimulateCtrlFi`）。

### 15.1 标定流程（与人工操作一致）

```text
前置：VG 已打开，「文件(F)」可交互（含中断弹窗 Drain）
  → Ctrl+F+I（按住 Ctrl，依次按 F、I）
  → 图一：检查对话框「快速导入...」是否出现
  → 图二：点击 Edit「文件名(N):」，写入 .vgl 全路径
  → 图三：点击按钮「打开(O)」（Invoke，失败则 Win32 点击）
  → 等待「快速导入」对话框关闭
  → 图四：ConfirmFileSelectionAsync（等「完成(F)」→ 向导内输路径 → **点击「完成(F)」元素**）
  → WaitForImportCompleteAsync …
```

Insights 要点：

| 图 | Name / ControlType | 备注 |
|----|-------------------|------|
| 图一 | Window `快速导入...` | 父窗常见为 `新建项目 - VGSTUDIO MAX 2022.3 64 bit` |
| 图二 | Edit `文件名(N):`（亦可为 ComboBox 内 Edit） | AccessKey Alt+N；须先点击再写入 |
| 图三 | Button `打开(O)` | AccessKey Alt+O；主路径改为点击按钮，不再以 Alt+O 为主 |
| 图四 | Window Name 含 `来自 .vgl 文件的体积`；标题区「文件选择」 | 保留原确认/完成逻辑 |

### 15.2 相对旧实现删除 / 变更清单

| 项 | 旧 | 新（2026-07-15） |
|----|----|------------------|
| 打开快捷键 | `Ctrl+I`；失败 Plan B 菜单 | **`Ctrl+F+I`**（`SimulateCtrlFi`）；**删除**菜单 Plan B / `OpenQuickImportViaMenuPlanBAsync` |
| 打开文件 | 优先 `Alt+O`，失败再点「打开」 | **优先点击「打开(O)」**；删除对 Alt+O 主路径依赖 |
| 图三与图四耦合 | `QuickImportFillAndOpen` 内等待「完成(F)」 | 图三只等「快速导入」关闭；图四单独 `ConfirmFileSelectionAsync` |
| 二次等待弹窗 | Open 已找到后 Fill 再 `WaitForQuickImportDialog` | Open **返回**对话框句柄，Fill **不再二次等弹窗** |
| 置前 | `ShowWindow(hwnd, 3)` 最大化 | **仅 `SetForegroundWindow`**（`BringAutomationElementToFront`） |
| 查找范围 | 常先扫 `AutomationElement.RootElement` + `TreeScope.Descendants` | **VG 主窗 Children → Descendants → 同进程桌面顶层 Children**；禁止 Root 整树 Descendants（**含**预设 ListItem、厚板对话框/组合模式、导出对话框/另存为/JPEG 质量窗，2026-07-16） |
| 检测时取主窗 | `GetFreshMainWindow`（内含最大化主窗 + 抢焦点） | 轮询用 **`TryGetMainWindowElement`**（只取句柄，不最大化、不抢焦点） |

### 15.3 查找 / 轮询 / 失败策略（Q&A 固化）

**Q1：何时进行查找？**  
A：`OpenQuickImportAsync` 内 **先发送 `Ctrl+F+I`，立即**用 `WaitForElementAsync` 轮询 `FindQuickImportDialog`。找到即返回并立刻进入输入，不是固定干等满超时。

**Q2：轮询多久？多少次？**  

| 层级 | 参数 | 默认值 | 说明 |
|------|------|--------|------|
| 单次轮询上限 | `QuickImportDialogWaitTime` | **10000 ms** | 找到提前结束 |
| 轮询间隔 | `UiPollIntervalMs`（=`DefaultConditionPollIntervalMs`） | **100 ms** | 代码常量，非 DB 项 |
| 单次理论轮询次数 | — | 约 **≤100 次**（10s/100ms） | 实际以找到为准 |
| 步骤重试次数 | `StepRetryCount` | **3** | `ExecuteStepWithRetryAsync` |
| 重试间隔 | `StepRetryIntervalMs` | **2000 ms** | 第 2、3 次前等待 |
| 每次重试动作 | — | 再发 `Ctrl+F+I` + 再轮询最多 10s | — |

最坏约：`3 × 10s + 2 × 2s ≈ 34s`（另加中断弹窗 Drain）。

图二「文件名」控件另有短等：`min(QuickImportDialogWaitTime, 3000)`，间隔仍 100 ms。

**Q3：查找不到怎么办？**  
A：单次 10s 返回 `null` → 本轮未达标 → 间隔 2s 后重试（最多 3 次）。全部失败抛：`打开快速导入 在 3 次尝试后仍未完成` → 该 `.vgl` `@@IMPORT_FAIL@@` **跳过继续**下一批文件。

**Q4：为什么以前输入路径前要放大快速导入窗口？不放大能不能输入？**  
A：**放大不是输入前提**。旧代码 `EnsureAutomationForeground` 误用 `ShowWindowAsync(hwnd, 3)`（`SW_MAXIMIZE`），把对话框最大化。输入只需窗口置前并可点；已改为只 `SetForegroundWindow`。

**Q5：弹窗已经出现，为什么还空等好几秒才输入？是不是没检测「快速导入」？**  
A：**有检测，但旧检测极慢**。旧 `FindQuickImportDialog()` 在无主窗时（或兜底路径）对整个桌面 `RootElement.FindAll(TreeScope.Descendants)` 扫所有 Window/Pane，再校验按钮，单次查找可卡 **6～9 秒**。日志表现如：

```text
10:38:37  Ctrl+F+I 打开快速导入
10:38:46  已检测到「快速导入...」，立即输入路径   ← 空等约 9s = UIA 全桌面扫描
```

对话框视觉上已开，UIA 还在扫桌面。修复：只在 VG 范围内查找；Open 成功后把对话框传入 Fill，立即点「文件名」写入。

**Q6：检测时为何不能调用 `GetFreshMainWindow`？**  
A：`GetFreshMainWindow` 会 `EnsureVgForeground` → 主窗 `ShowWindow(3)` 最大化并抢焦点，可能干扰刚弹出的对话框。检测轮询改用 `TryGetMainWindowElement`。

### 15.4 代码落点速查

| 方法 | 职责 |
|------|------|
| `SimulateCtrlFi` | 按住 Ctrl，依次 F、I |
| `OpenQuickImportAsync` | 图一：发快捷键 + 轮询返回对话框 |
| `QuickImportFillAndOpenAsync` | 图二+三：点输入框、写路径、点打开、等对话框关 |
| `FillQuickImportFileNameByClickAsync` | 先点击再 ValuePattern / 键盘 |
| `ClickQuickImportOpenButtonAsync` | Invoke「打开(O)」→ Win32 |
| `FindQuickImportDialog` | 窄范围查找（见 §15.2） |
| `BringAutomationElementToFront` | 仅置前，不最大化 |
| `ConfirmFileSelectionAsync` | 图四（未改语义） |

### 15.5 建议回归日志特征

成功且体感正常时，相邻两条日志时间应接近（亚秒～一两秒内，视 VG 响应）：

```text
Ctrl+F+I 打开快速导入
已检测到「快速导入...」，立即输入路径
点击「文件名(N):」并输入路径：…
点击「打开(O)」按钮
「快速导入」对话框已关闭，等待文件选择向导
文件选择向导已出现，输入路径
Alt+F 完成文件选择
```

若「Ctrl+F+I」与「已检测到」再次相差数秒，优先怀疑查找范围又扫到桌面整树，或主窗句柄/子窗层级与 Insights 不一致。

---

## 16. 阶段四详规：简单 3-2-1 对齐（2026-07-22 终版）

> **状态**：✅ **全部完成**（单测 + 主流程）。  
> **范围**：阶段四 **仅**「简单 3-2-1 对齐」——对话框内完成 **3 平面点 + 2 向量点 + 1 原点**。  
> **找点二选一**：`Simple321PointSource` = `Local`（默认，OpenCV）或 `VisionDll`（`CTAutoCutImg` V1.0）。  
> **废弃**：旧 `MeasureAngle` 双视图算角链；独立「4b」规划名称（向量仍在 3-2-1 内，可选 DLL）。

### 16.1 视觉算法接口

#### 本地路径（`Simple321PointSource.Local`，默认 ✅）

| 项目 | 说明 |
|------|------|
| 类 | `SUNWODA_SEVB.Tool.Helper.Vision3dSectionPointsDetector` |
| 入口 | `Detect(imagePath)` → `DetectResult.Points` |
| 依赖 | OpenCvSharp（**不依赖** Halcon / `CTAutoCutImg`） |
| 输出约定 | 与 DLL 接口一一致：`[0..2]` 上盖三点，`[3]` 原点 |
| 详规 | **§18** |

#### `CTAutoCutImg.dll`（`Simple321PointSource.VisionDll`）

| 项目 | 说明 |
|------|------|
| 协议 | 反射动态调用（DLL 放服务器/程序目录，**非项目引用**） |
| DLL | `CTAutoCutImg.dll`（`Dlls\`；接口文档 V1.0，2026-07-21） |
| 类型名 | `CTAutoCutImg.CT`（反射须带命名空间；亦回退 `CT`） |
| 运行时 | .NET 8.0 + `halcondotnet`（文档写 20.11）；加载前 **`SetDllDirectory(Dlls)`** |
| 加载方式 | `Assembly.LoadFrom(dllPath)` |
| 封装 | `CutImgAlgorithmService` / `ICutImgAlgorithmService` |

**接口一：`GetSectionReferencePoints`**（3D 四点）

| 索引 | 含义 |
|:----:|------|
| 0 | 上盖第一个点 `(X, Y)` 像素 |
| 1 | 上盖第二个点 |
| 2 | 上盖第三个点 |
| 3 | 原点坐标 |

**接口二：`GetObjectTopBottomPoints`**（「上」视图向量两点；选 VisionDll 时主链调用）

| 索引 | 含义 |
|:----:|------|
| 0 | DLL 返回点 A `(X, Y)` 像素 |
| 1 | DLL 返回点 B `(X, Y)` 像素 |

**上位机点击顺序（2026-08-04）**：**上→下**（按屏幕 Y 重排后再 Ctrl）。  
真机观察 DLL 原始 `[0]→[1]` 常为下→上，与 VG 向量方向要求不符；故**不再按索引原样点击**，与本地算法统一为「先上后下」。

坐标单位：像素；相对**输入截图像素坐标系**。

**3-2-1 与 DLL 对应**：

| 步骤 | DLL | 点击 |
|:----:|-----|------|
| 1 | `GetSectionReferencePoints` `[0..2]` | Ctrl 三点定平面 |
| 2 | `GetObjectTopBottomPoints` → 按 Y 重排 | Ctrl **上点→下点** |
| 3 | `GetSectionReferencePoints` `[3]` 原点 | Ctrl 原点 |

**离线单测（不跑 VG）**：单步测试页「CTAutoCutImg DLL 离线测试」——**3D侧视图** / **2D侧视图** 各选一文件夹，两个按钮分别对该目录内全部图片批量测 `GetSectionReferencePoints` / `GetObjectTopBottomPoints`；标注图默认输出 `C:\AutoSlice\cutpicture\CutImgDllTest`。

### 16.2 简单 3-2-1 对齐流程（✅ 单测 + 主流程）

**前置**：VG 中已导入体积、LUT、（可选）直方图、厚板；四象限可见。**须同时标定** `Vision3dRoi*` 与 `VisionTopViewRoi*`。3D ROI **勿框「上」视图**。  
本地算法建议另标定 **Pre321**（§23），否则斜俯视找点可能不稳定。

```mermaid
flowchart TD
    A[单测/主流程入口] --> S{"Simple321PointSource?"}
    S -->|VisionDll| B
    S -->|Local| P{"Pre321 标定齐全?"}
    P -->|是| P2[点击切换视图 + 箭头拖拽]
    P -->|否| B
    P2 --> B[按 Vision3dRoi 截取第四象限 3D]
    B --> C[截图落盘 VisionCaptureDir]
    C --> D["按 Simple321PointSource：Local 斜俯视 Detect / VisionDll GetSectionReferencePoints"]
    D --> E[标注调试图 overlay]
    E --> F["Simple321ToolbarClick* 绝对坐标点开「简单对齐」下拉"]
    F --> G[选菜单项进入对齐模式]
    G --> H[点击「重置」]
    H --> I["步骤1：Ctrl+三点（左上→右上→右下）"]
    I --> I2["步骤1.5：偏移 Edit 置 0 + Enter"]
    I2 --> J["步骤2：截「上」视图 → 向量两点 → Ctrl+两点"]
    J --> K["步骤3：Ctrl+原点（3D）"]
    K --> L[点击「完成(F)」]
    L --> M{警告对话框?}
    M -->|是| N[点击「是(Y)」]
    M -->|否| O[3-2-1 完成]
    N --> O
```

**操作要点**：

1. **⓪ Pre321（仅 Local）**：截 3D 前点击切换视图 + 箭头拖拽至斜俯视（§23）；未标定则跳过打日志。**`VisionDll` 禁止跑 Pre321**，保持默认视角直接截图（2026-08-04 已门控）。  
   - **Local 额外**：Pre321 **之前**先等 `Vision3dInitialCaptureWaitMs`（默认 2s）→ 截默认视角 3D 原图到 `Vision3dInitialCaptureDir`（`3D初始`，强化学习样本；失败不阻断）。  
2. **采图（3D）**：截图前等待 `VisionCaptureWaitMs`（默认 2s，等坐标消失）→ 第四象限 **3D** 有效范围（`Vision3dRoi*`）→ `VisionCaptureDir`（默认 `jietu`，**7 天**）。
3. **找点（3D）**：Local 斜俯视 FloodFill/凸包（§18）；VisionDll `GetSectionReferencePoints`。`points[0..2]` 上盖三点（**左上 / 右上 / 右下**），`points[3]` 原点。日志带 `【找点算法】`。
4. **步骤顺序（关键）**：在「简单 3-2-1 对齐」对话框打开期间完成；**仅原点完成后才点「完成」**。**步骤2 必须先于步骤3**。
5. **步骤1.5 偏移置0**：三点后点击左侧无名 Edit（`Simple321OffsetEdit*`）→ 输入 `0` → Enter；否则「上」视图非预期。
6. **步骤2 向量**：截图前同样等待 `VisionCaptureWaitMs` → 截取 **`VisionTopViewRoi*`** → Local：`VisionTopViewVectorPointsDetector`（**优先黄框右缘旁侧**，§19）/ VisionDll：`GetObjectTopBottomPoints`（按 Y 上→下）→ Ctrl 点击 → 落盘 **`Vision2dVectorCaptureDir`**。
7. **UI**：「简单对齐」下拉须 **`Simple321ToolbarClickX/Y` 绝对屏幕坐标**。
8. **重置**：进入对齐后先点 **重置**，再 Ctrl+选点。
9. **完成与警告**：点 **完成(F)** 后若出现 **警告**，须点 **是(Y)**。
10. **失败策略**：算法/点击异常 → 日志；主流程未标定齐全则跳过该段。
11. **核对算法**：流程日志搜 `【找点算法】`，应与界面「算法来源」下拉一致；切 DLL 后不应再出现 Pre321 拖拽日志。
### 16.3 ~~子步骤旧 4b~~（已取消）

> **2026-07-22 终版**：不存在独立旧 4b。简单 3-2-1 的「2」=「上」视图向量两点（§16.2 步骤2 / §19）。  
> `GetObjectTopBottomPoints` 封装可保留于仓库，**主链不调用、不再排期**。

### 16.4 与主流程 / 直方图 / 厚板 / 导出的时序

| 顺序 | 步骤 | 状态 |
|:----:|------|:----:|
| 1 | 导入 `.vgl` | ✅ |
| 2 | LUT 预设 | ✅ |
| 2b | 直方图拖红线 | ✅（未标定则跳过） |
| 2c | LUT 关键段中点对齐红线 | ✅（未标定两端则跳过） |
| 2d | LUT 按 Y 非对称扩展 | ✅（未标定两端 / Y&lt;2 / 因子 0 则跳过） |
| 3 | Q1～Q3 厚板 + 平均值 | ✅ |
| **4** | **简单 3-2-1（三点 + 偏移置0 + 黄框向量 + 原点）** | ✅ **单测 + 主流程** |
| **4'** | **对齐后捕捉拖拽** | ✅ **单测 + 主流程**（§20） |
| 5 | Alt+F+T 三视图导出（上/前/右；先上） | ✅ |

说明：阶段四插在 **厚板之后、导出之前**；对齐后捕捉在 3-2-1 与导出之间。

### 16.5 坐标换算约定

设截图 ROI 屏幕左上角为 `(Rx, Ry)`，算法返回像素 `(Px, Py)`（相对截图像素）：

```text
screenX = Rx + Px
screenY = Ry + Py
```

注意：

- 若截图含 DPI / 多显示器虚拟桌面偏移，须与 `ScreenCaptureHelper` 所用坐标系一致（与直方图 ROI 同一套）。
- 点击前建议短时 Unlock 输入钩子（与预设下拉 Win32 点击同样处理），避免钩子吞掉鼠标。

### 16.6 配置项（已加入 `CTAutoSlicerSettings`）

| 参数 | 默认 | 用途 |
|------|------|------|
| `EnableVisionAlignmentSteps` | `true` | 主流程是否执行简单 3-2-1；关则跳过；开但未标定齐全则跳过打日志 |
| `Simple321PointSource` | `Local` | 找点来源：`Local`=上位机 OpenCV；`VisionDll`=`CTAutoCutImg` |
| `Pre321ViewClickX/Y` + Calibrated | 真机标定 | 找点前「切换视图」点击绝对坐标 |
| `Pre321ViewDragStart/End X/Y` + Calibrated | 真机标定 | 找点前箭头拖拽起终点 |
| `Pre321ViewClickWaitMs` / `DragStepDelayMs` | `400` / `8` | 点击后等待 / 拖拽步进 |
| `Vision3dRoiX/Y/Width/Height` | 真机标定 | 第四象限 3D；**勿含「上」视图** |
| `VisionTopViewRoiX/Y/Width/Height` | 真机标定 | 第二象限「上」视图（向量两点） |
| `Simple321ToolbarClickX/Y` | 真机标定 | 「简单对齐」下拉 **绝对坐标** |
| `Simple321OffsetEditX/Y` + Calibrated | 真机标定 | 三点后偏移 Edit **绝对坐标**（置 0） |
| `VisionClickIntervalMs` | `300` | 多点点击间隔 |
| `VisionCaptureWaitMs` | `2000` | 3D/「上」视图截图前等待（等画面坐标消失；Local/DLL 共用） |
| `VisionCaptureDir` | `C:\AutoSlice\cutpicture\jietu` | 3D 调试图；按日，**7 天** |
| `Vision3dInitialCaptureDir` | `C:\AutoSlice\cutpicture\3D初始` | Local：Pre321 前默认视角 3D 原图（强化学习）；按日，**7 天** |
| `Vision3dInitialCaptureWaitMs` | `2000` | 保存 3D 初始原图前等待 |
| `Vision2dVectorCaptureDir` | `C:\AutoSlice\cutpicture\2Dxianglaing` | 向量截图/标注；按日，**7 天** |
| `EnablePostAlignCaptureDragSteps` | `true` | 主流程是否执行对齐后捕捉拖拽 |
| `PostAlignCaptureButtonX/Y` + Index | 真机标定 | 无名捕捉按钮坐标 / UIA 索引 |
| `PostAlignDragStart/End X/Y` | 真机标定 | 箭头拖拽起终点 |
| `PostAlignClickWaitMs` / `DragStepDelayMs` | `400` / `8` | 点击后等待 / 拖拽步进 |

### 16.7 建议实现文件与方法名

| 落点 | 职责 |
|------|------|
| `Vision3dSectionPointsDetector` | ✅ **本地 3D 找点（斜俯视）**（§18） |
| `VisionTopViewVectorPointsDetector` | ✅ **「上」视图向量两点（黄框优先）**（§19） |
| `VGAutomationService.Pre321ViewPrep.cs` | ✅ **找点前视角准备**（§23） |
| `tools/Vision3dPointsProbe` / `tools/VisionTopViewVectorProbe` | ✅ 离线批量探针 |
| `VGAutomationService`（`RunSimple321AlignmentAsync` 等） | ✅ 简单 3-2-1 编排 + **主流程已接** |
| `ResetSimple321PlaneOffsetEditAsync` | ✅ 三点后偏移置 0 |
| `RunPostAlignCaptureDragAsync` | ✅ 对齐后捕捉 + 箭头拖拽（§20） |
| `ScreenArrowCalibrationOverlayWindow` | ✅ 箭头标定遮罩（Pre321 / PostAlign 共用） |
| `VM_CTAutoSlicerPage` | ✅ 单测 + 标定 UI；`IsSimple321LocalPointSource` |

### 16.8 联调检查清单

- [x] 3D 截图 → 本地 Detect → 标注四点
- [x] 「上」视图截图 → 黄框/向量两点检测 → 标注
- [x] 真机：ROI 与视口一致
- [x] 真机：绝对下拉 + 重置 + 完整 3-2-1（含偏移置0）+ 完成 + 警告是
- [x] `EnableVisionAlignmentSteps=true` 时主流程执行 3-2-1
- [x] Pre321 标定 + 单测 + 主流程仅 Local 接入；VisionDll 跳过（§23，2026-08-04）
- [x] 对齐后捕捉拖拽单测 + 主流程
- [x] 主/单测一致性（§21）
- [ ] 真机：斜俯视四点与人工标注一致性（尤其原点）

### 16.9 开放项（可选）

1. 失败时是否允许继续导出 — 可由产品再定。  
2. 黄框 HSV 若真机色偏，可再调阈值。  
3. 斜俯视原点是否应为「最上方尖点」而非近 TL — 待真机核对。  

---

## 17. 2026-07-22 联调纪要（阶段四终版）

| 项 | 结论 |
|----|------|
| 阶段四范围 | **仅简单 3-2-1**；**无独立 4b** |
| 找点（3D） | 本地 `Vision3dSectionPointsDetector`（**2026-07-31 起改为斜俯视 FloodFill**，见 §18） |
| 找点（向量） | 本地 `VisionTopViewVectorPointsDetector` |
| 单测 | 「测试简单 3-2-1 对齐」 |
| 顺序 | 重置 → Ctrl+三点 → **偏移置0** → 向量两点（上→下，黄框优先）→ 原点 → 完成 → 警告「是」 |
| 主流程 | ✅ `EnableVisionAlignmentSteps`（默认开） |
| 切片范围 | UI `SliceRangeStartMm`/`EndMm`；前/右/上三视图共用 |
| 用户确认 | 3-2-1 流程 OK；阶段四全部完成；无 4b |

---

## 18. 本地 3D 四点算法（`Vision3dSectionPointsDetector`，斜俯视）

> **背景**：极耳预焊遮挡角点时，水平肩线/剪影四角在默认视角不稳定；先经 **Pre321** 切到不被遮挡的斜俯视，再估上平面四点。  
> **文件**：`SUNWODA_SEVB.Tool/Helper/Vision3dSectionPointsDetector.cs`  
> **输出约定**：与 DLL 一致 — `[0]` 左上、`[1]` 右上、`[2]` 右下、`[3]` 原点（近 TL）。  
> **调试图**：`PutText` 须用 ASCII（Hershey 不支持中文，否则 marshal 失败）。

### 18.1 流程概要（2026-07-31 主路径）

```text
黑底截图 → 灰度 / 剪影 mask
  → 上平面种子点 → FloodFill 区域生长
  → 连通域外轮廓 / 凸包 → 极端四点（TL / TR / BR / BL）
  → 三点：左上、右上、右下（平面点，一般不用 BL）
  → 原点：近 TL（内偏）
  → 回退链：ROI 顶面 / 边缘轮廓 / 直线 / Harris（FloodFill 失败时）
```

> **废弃主路径**：旧「行宽肩线 SL/SR → 顶边 TL/TR」水平俯视法；斜俯视下肩线语义失效。

### 18.2 验证与探针

- 真机单测：「测试简单 3-2-1 对齐」→ 看 `VisionCaptureDir` 下 `*_points.png`。  
- 离线：`tools/Vision3dPointsProbe`；样图探针目录示例 `C:\AutoSlice\cutpicture\jietu\_pre321_test\`。  
- 样例结果量级（探针，非金标）：`P1≈(430,149) P2≈(538,112) P3≈(591,271) O≈(430,144)`。

### 18.3 与 Pre321 / 直方图的关系

- **Pre321** 保证截图视角含清晰上平面；未准备时算法可能抛「未能在斜俯视中稳定检出顶面四角」。  
- 直方图目标线靠右 1/3 利于压噪，剪影/FloodFill 更稳。

---

## 19. 「上」视图向量两点算法（`VisionTopViewVectorPointsDetector`）

> **背景**：简单 3-2-1 的「2」= 定向量；须 Ctrl+从上到下；须在原点之前、偏移置0之后完成。  
> **文件**：`SUNWODA_SEVB.Tool/Helper/VisionTopViewVectorPointsDetector.cs`  
> **输出约定**：`[0]`=上点，`[1]`=下点。  
> **2026-07-23**：主策略改为 **黄框右缘旁侧**（与黄框平行、基线更长）；外壳短段易导致不平行。

### 19.1 流程概要（主路径：黄框）

```text
「上」视图截图 → HSV 提取黄色对齐框（仅 Close，禁 Open，避免抹掉细线）
  → 最长周长黄轮廓 → MinAreaRect → 取右侧较竖直边
  → 沿右缘 10%/90% 取两点 → 向矩形内侧偏移 YellowEdgeOffsetPixels（黄线不可点）
  → 调试图：绿线=黄框右缘，红线=点击向量 + V-Top / V-Bot，角标 yellow-rect
```

### 19.2 回退：剪影右缘

未检出黄框时：Otsu 剪影 → 右缘竖直段 → 内收取点（`mode=silhouette-fallback`）。

### 19.3 关键常量

| 常量 | 值 | 含义 |
|------|-----|------|
| `YellowEdgeOffsetPixels` | `6` | 相对黄框右缘向内侧偏移（旁侧可点） |
| `MinPointSeparationPx` | `40` | 黄框路径最小竖直间距 |
| `TopPointFraction` / `BottomPointFraction` | `0.10` / `0.90` | 沿黄框右缘取点位置 |
| `EdgeInsetPixels` | `3` | 回退：剪影右缘内收 |

### 19.4 落盘与探针

- 运行时：`Vision2dVectorCaptureDir`\yyyy-MM-dd\；`*_vector_points.png` / `*_vector_yellow_mask.png`；**7 天**。  
- 离线：`tools/VisionTopViewVectorProbe`。

### 19.5 说明（无独立旧 4b）

简单 3-2-1 的「2」即本节向量两点；**不存在**另一步「旧 4b 定方向」。

---

## 20. 对齐后捕捉与拖拽（3-2-1 后、导出前）

> **状态**：✅ 单测 + 主流程（2026-07-23）。  
> **位置**：`RunPostImportPipelineAsync` 中，`TryRunVisionAlignmentInMainFlowAsync` 之后、`ExportImageStackSlicesAsync` 之前。

### 20.1 步骤

1. 定位视口底部无名捕捉 Button：优先 UIA（空 Name 小按钮，靠近标定坐标或按 `PostAlignCaptureButtonIndex`）→ Invoke → 失败 Win32；再失败用绝对坐标。  
2. 等待 `PostAlignClickWaitMs`。  
3. 按 `PostAlignDragStart/End` 直线拖拽（`MouseSimulationHelper.BuildLinearDragPath`）。

### 20.2 UI / 标定

- Expander「对齐后捕捉与拖拽」：标定捕捉按钮、标定拖拽箭头（`ScreenArrowCalibrationOverlayWindow`）、开关与等待参数。  
- 单测按钮：「测试对齐后捕捉拖拽」→ `TestPostAlignCaptureDragAsync` → `RunPostAlignCaptureDragAsync`。

### 20.3 开关

| 参数 | 默认 | 行为 |
|------|------|------|
| `EnablePostAlignCaptureDragSteps` | `true` | 主流程执行；关则跳过 |
| 标定不全 | — | 主流程跳过打日志；单测抛错 |

---

## 21. 主流程 ↔ 单测一致性核对（2026-07-23）

> **结论**：核心业务路径一致；差异仅为入口包装（附着已有 VG / 开关跳过 / 前置校验抛错 vs 跳过）。

### 21.1 共用核心方法（必须一致）

| 业务段 | 核心方法 | 主流程入口 | 单测入口 |
|--------|----------|------------|----------|
| LUT 预设 | `ApplyRenderingPresetAsync` | `RunPostImportPipelineAsync` | `TestRenderingPresetAsync` |
| 直方图 | `ApplyHistogramGrayScaleAdjustAsync` | 同上 | `TestHistogramGrayScaleAdjustAsync` |
| 厚板 | `ApplyThickPlateForQuadrantsAsync` | 同上 | `TestThickPlateAsync` |
| 简单 3-2-1 | **`RunSimple321AlignmentAsync`** | `TryRunVisionAlignmentInMainFlowAsync` | **`TestSimple321AlignmentAsync`** |
| 对齐后捕捉拖拽 | **`RunPostAlignCaptureDragAsync`** | `TryRunPostAlignCaptureDragInMainFlowAsync` | **`TestPostAlignCaptureDragAsync`** |
| 切片导出 | `ExportImageStackSlicesAsync` | 同上 | `TestSliceExportAsync` |
| AI 图像标注 | **`RunOfflineCtVisionAnnotationAsync`** | `TryRunOfflineCtVisionAnnotationAsync` | **`TestOfflineCtVisionAnnotationAsync`** |

### 21.2 有意差异（非 bug）

| 项 | 主流程 | 单测 |
|----|--------|------|
| VG 进程 | 冷启动进程池，禁止附着 | `GetCTViewerProcess` 可附着已开 VG |
| 未标定 | `Try*` **跳过**并打日志，继续后续步骤 | **抛错** / UI 拦截，便于排障 |
| 开关 | 尊重 `EnableVisionAlignmentSteps` / `EnablePostAlignCaptureDragSteps` / **`EnableOfflineCtVisionAnnotation`** | 点击即跑（仍校验前置） |
| 编排 | 全链串行：LUT→直方图→厚板→3-2-1→捕捉拖拽→导出→**AI标注** | 各按钮独立测一段 |

### 21.3 3-2-1 内部顺序（主/单测相同）

```text
打开对齐 → 重置
  → Ctrl+三点
  → 偏移 Edit 置 0 + Enter
  → 「上」黄框向量两点 Ctrl+点击
  → Ctrl+原点
  → 完成 → 警告「是」
```

### 21.4 核对时已修正

- 后处理注释与日志改为含「对齐后捕捉拖拽」。  
- `TestSimple321AlignmentAsync` 前置与 `HasSimple321Prerequisites` 对齐（含「上」ROI + 偏移 Edit）。

### 21.5 真机状态（用户确认）

- 各单步测试按钮均已跑通。  
- 主流程在标定齐全且开关开启时应与单测行为一致（同一核心方法）。

---

## 22. 阶段七：AI 图像标注（独立模式；2026-07-31 两机拆分）

> **状态**：✅ **已从切图主链拆出**；`PipelineRunMode=AiAnnotateOnly` 下「开始流程」批处理；单测保留；**上视图 CW90°**；真机两机联调待确认。  
> **位置**：不再挂在 `RunPostImportPipelineAsync` 末尾；切图机只负责把送检图写入 `TopViewHandoffFolder`。  
> **与阶段四区别**：阶段四用 OpenCV 本地找点做 VG 内 3-2-1 对齐；本阶段用 **`NJCT.NJSwdCT`** 对交接目录中的 JPEG 做抓点区域与 L1～L5 标注。

### 22.0 两机部署与流水线模式

| 项 | 说明 |
|----|------|
| `PipelineRunMode` | `SliceOnly`（默认）/ `AiAnnotateOnly`；调试顶栏胶囊切换；**本机 JSON** `{程序目录}/Config/CTAutoSlicer/local.json`（`CTAutoSlicerLocalConfigService`） |
| `TopViewHandoffFolder` | 共享交接目录；切图写入 `{exportBaseName}.ext`（扁平），AI 扫描根一层 |
| 切图机 | 侧栏：应用～切片；导出后 `MirrorTopViewHandoffAsync`（硬失败） |
| AI 机 | 侧栏：AI 图像标注、**二次手动标注**；「开始流程」→ `RunAiAnnotationBatchAsync`（不启 VG） |
| 结果根 | 复用 `SliceExportFolder`（AI 模式下 UI 文案为「标注结果根目录」） |

### 22.1 接口来源

| 项 | 值 |
|----|-----|
| 文档 | `docs/CT离线AI视觉接口文档.md`、`docs/离线CT_DLL接入说明.md` |
| 编制 | 罗奕雄，2026-07-27，V1.0 |
| 业务 | 惠州离线 CT 正极 |
| DLL | `NJCT.dll`（依赖同目录 `CF.dll`） |
| 类型 | `NJCT.NJSwdCT` |
| 模型 | `Dlls\Models\CTOffline\model_500000_900.onnx` |

### 22.2 三个接口方法

| 方法 | 输入要点 | 输出要点 |
|------|----------|----------|
| `ModelInit` | `ModelPath`、`IsRunCuda`（默认 true） | `IPredictor` 句柄、`ExInfo` |
| `ModelAction` | `ImgPath`、句柄、`BlackHeight` | 增强图 + 双电芯抓点/坐标/尺寸 |
| `ModelAction_origin` | 同上 | **原图** + 坐标（像素尺度） |

正常时列表长度均为 **2**；`RunInfo` 成功为 `""`；`List<HObject>` **必须释放**（服务内已处理）。

### 22.3 运行时依赖

- 反射 `Assembly.LoadFrom(Dlls\NJCT.dll)` + `SetDllDirectory(Dlls)`  
- `CF.dll`、`halcondotnet.dll`（22.11）、ONNX Runtime（Cuda 可选）  
- **方案 B 预提亮**（默认开）：**固定 CW90°** → 算 ROI 均值/P50 → 偏暗则增益提亮 → 落盘 `{产品}/input/` → 再调 NJCT；历史开关 `OfflineCtRotateTopViewCw90` / `OfflineCtExportInputForNjct` 已不影响主流程  
- 注意：仓库 `Dlls\NJCT.dll` 已覆盖为惠州正极包；CTNCLX 若共用该 DLL，联调时需确认兼容性

### 22.4 实现文件

| 文件 | 说明 |
|------|------|
| `ICTOfflineVisionService.cs` / `OfflineCtVisionService.cs` | 反射 ModelInit / ModelAction / ModelAction_origin |
| `OfflineCtAnnotationHelper.cs` | 按序号选图 + 交接复制/枚举 + JSON/CSV |
| `VGAutomationService.OfflineCtVision.cs` | `RunAiAnnotationBatchAsync` / 单测编排（推理前旋转） |
| `VGAutomationService.SliceExport.cs` | `MirrorTopViewHandoffAsync` |
| `PipelineRunMode.cs` | SliceOnly / AiAnnotateOnly |
| `OfflineCtResultImageMode.cs` | Enhance / Origin / Both |
| `CTAutoSlicerSettings` | `PipelineRunMode` / `TopViewHandoffFolder` / `OfflineCt*` |
| `CTAutoSlicerPage` 侧栏「AI图像标注」 | NavAiAnnotate=8 |
| `CTAutoSlicerPage` 侧栏「二次手动标注」 | NavManualMeasure=9；单步=10；偏好=11 |
| 单步测试「标定采图」 | 截取 3D / 上视图标定图（见 §22.7） |

### 22.5 接入时序与目录约定

```text
【切图机 · SliceOnly】
RunPostImportPipelineAsync：
  … → ExportImageStackSlicesAsync → (exportBaseName, quantity)
  → MirrorTopViewHandoffAsync
      → 源：{导出根}/{exportBaseName}/{exportBaseName} 上/{OfflineCtTopViewSliceIndex}.*
      → 目标：{TopViewHandoffFolder}/{exportBaseName}.*（扁平，无产品子目录）
      → 硬失败（交接目录未配 / 源图缺失 / 复制失败则中断该文件）

【AI 机 · AiAnnotateOnly】
RunAiAnnotationBatchAsync：
  → 清理 {Parent(标注结果根)}/交接备份/ 下超过 30 天的 yyyy-MM-dd 目录
  → 枚举 TopViewHandoffFolder 根下一层 jpg/png
  → 逐张 RunOfflineCtVisionAnnotationAsync(explicitImagePath, explicitOutputFolder)
      → 预处理：固定 CW90° + 条件提亮 → 落盘 {产品}/input/ → NJCT
      → 输出：{SliceExportFolder}/{产品名}/
           {产品名}.jpg（及可选 {产品名}_origin.jpg）/ annotation.json|csv
           input/{源文件名} + prebrighten_report.txt
           （勾选时另有… 已取消；input 固定写出）
      → 【成功后】备份并移出交接：
           源：{TopViewHandoffFolder}/{产品名}.*
           备：{Parent(SliceExportFolder)}/交接备份/{yyyy-MM-dd}/{产品名}.*
           再删交接源；备份失败则保留源以便重试
```

切图侧选图规则：填 N = 第 N 张；实际文件名 = `ImageStackBaseFileName` + N（默认前缀 `0`，如 50 → `050.jpg`/`050.png`…）；找不到则硬失败。交接落盘为 `{TopViewHandoffFolder}/{导出名}.jpg`（以产品名前六字符命名，避免多产品 050.jpg 冲突）。AI 批处理输出 `{结果根}/{产品名}/`，结果图名为 `{产品名}.jpg`（不再用 `result_enhance.jpg`，也不再套 `AI标注/`）。  
**朝向**：送检图须为**竖图**（高 ≥ 宽）；宽 &gt; 高时顺时针 90°。切图写入交接目录时即校验；阶段七写入 `{产品}/input/` 前再校验一次（已竖不重复转）。  
失败策略：切图交接**硬失败**；AI 批处理**单张软失败**继续下一张。  
**交接备份**：标注成功一项即备份到 **标注结果根目录（SliceExportFolder）上级** 的 `交接备份`，保留 **30 天**，超时按日子目录自动删除；交接源仍从「上视图交接目录」删除。

### 22.6 清单

- [x] 封装 `ICTOfflineVisionService`（反射 `NJCT.NJSwdCT`）  
- [x] Settings + UI 栏目（模型/CUDA/BlackHeight/结果图模式/JSON/CSV/**切片序号/像素精度**）  
- [x] 单测：「测试 AI 图像标注」  
- [x] **两机拆分**：`PipelineRunMode` + `TopViewHandoffFolder`；切图交接硬失败；AI 批处理独立入口  
- [x] Halcon `HObject` 释放与 `RunInfo` 日志  
- [x] 上视图推理前固定顺时针 90° + 条件提亮 → 落盘 `{产品}/input/` 供视觉 DLL  
- [x] 上视图切片序号可配（1～100）+ 像素精度（μm/px）与 mm/μm 换算展示  
- [x] **二次手动标注（测距）**：侧栏页签 + 全屏工作台 + 导出 `result/`（可改选；见 §22.8）  
- [x] **交接完成即备份**：标注成功 → `{Parent(标注结果根)}/交接备份/yyyy-MM-dd/{产品名}.*` → 删交接源；保留 30 天  
- [ ] 真机两机联调：切图交接 `{产品名}.jpg` 扁平落盘；AI 机批处理识别与 `{产品名}/` 落盘符合配置  

### 22.7 UI：标定采图迁入单步测试（2026-07-28）

> 原主界面底部临时按钮已移除；仅在**调试模式** → **单步测试**可见。

| 项 | 说明 |
|----|------|
| 入口 | 单步测试页「标定采图（调试）」 |
| 3D | 路径 `VisionLearn3dSampleDir`，默认 `C:\AutoSlice\cutpicture\3D`；按钮「截取3D标定图」 |
| 上视图 | 路径 `VisionLearnTopViewSampleDir`，默认 `C:\AutoSlice\cutpicture\上视图`；按钮「截取上视图标定图」 |
| 行为 | 最小化本软件 → 按已标定 ROI 截图保存到上述路径；路径可浏览并持久化 |
| 前置 | 须先在「视觉对齐」完成 3D / 上视图 ROI 标定 |

### 22.8 二次手动标注（测距；2026-07-31）

> **状态**：✅ 代码已落地。  
> **目的**：AI（NJCT）漏检处由人工补两点测距；不并入 `AI标注/annotation.json`，独立导出 `result/`（可改选）。

| 项 | 说明 |
|----|------|
| 侧栏 | AI 模式 →「二次手动标注」`NavManualMeasure=9` |
| 像素精度 | 复用 `OfflineCtPixelSizeUm`（μm/px）；`L_μm = N_px × s`，`L_mm = L_μm / 1000` |
| 入口 | 「打开文件夹开始标注」→ 递归枚举 jpg/png/bmp/tif（跳过已有 `result/` 与历史 `output/`） |
| 窗口 | `ManualMeasureAnnotateWindow` 最大化；左列表（绿=已标/灰=未标）+ 中部画布 |
| 测距 | 左键定 P1 → 橡皮筋虚线 + 实时距离 → 左键定 P2；`Shift` 正交；定 P1 后**右键**或 `Esc` 取消起点；`M`/`A` 测距模式 |
| 编辑 | 撤销上一条 / 清空当前 / 浏览模式点选尺寸线后 `Delete`；切换图片内存保留 |
| 续标 | 打开时优先读当前导出目录；无则回退历史 `output/{相对路径}.json` |
| 导出 | 默认 `{根目录}/result/`；工具栏可浏览改选；写入 `index.json` + 单图 json + `{名}_annotated.png` |

**关键文件**：

| 文件 | 职责 |
|------|------|
| `ManualMeasureModels.cs` | `ManualMeasureItem` / `ImageRecord` / `SessionExport` |
| `ManualMeasureExportHelper.cs` | 枚举、续标加载、导出 JSON/叠加图 |
| `VM_ManualMeasureAnnotateWindow.cs` | 会话状态与命令 |
| `ManualMeasureAnnotateWindow.xaml(.cs)` | 全屏交互与叠加绘制 |
| `VM_CTAutoSlicerPage.OpenManualMeasureFolder` | 选目录并打开工作台 |

---

## 23. 找点前视角准备 Pre321 + 视觉对齐 UI（2026-07-31；2026-08-04 门控修订）

> **状态**：✅ 代码已落地；真机斜俯视四点待巩固。  
> **动机**：极耳预焊遮挡默认视角角点 → 先切到斜俯视再采 3D 图找点（**仅本地 OpenCV 路径**）。  
> **约定**：视觉 DLL（`CTAutoCutImg`）在 **默认视角** 下找点，**不得**执行 Pre321。

### 23.1 Pre321 行为

| 项 | 说明 |
|----|------|
| 配置前缀 | `Pre321View*`（Click / Drag / WaitMs / DragStepDelayMs） |
| 服务 | `VGAutomationService.Pre321ViewPrep.cs`：`TestPre321ViewPrepAsync` / `RunPre321ViewPrepAsync` |
| 主流程门控 | `RunSimple321AlignmentAsync` 截 3D **之前**：仅当 **`!UseVisionDllForSimple321`（Local）且** `HasPre321ViewPrepPrerequisites` 才跑；VisionDll → 打日志跳过 |
| 单测入口 | 调试 → 视觉对齐 → **⓪ 3D找点前视角准备（上位机本地算法配置）** |
| 标定 | 屏幕绝对点击 + `ScreenArrowCalibrationOverlayWindow` 箭头拖拽（对齐「对齐后拖拽」交互） |
| 参考图 | `Assets/images/pre321_view_ref.png`；VM `Pre321ViewReferenceImage` |

### 23.2 显隐与文案

| 项 | 说明 |
|----|------|
| 条件 | 仅当 `Simple321PointSource == Local`（界面「上位机本地算法」）时显示 ⓪ |
| VM | `IsSimple321LocalPointSource`；`Simple321PointSource` 变更时 `OnPropertyChanged` |
| 标题 | 「⓪ 3D找点前视角准备（上位机本地算法配置）」 |
| 运行时 | UI 隐藏不足以保证安全：服务层必须再判 `UseVisionDllForSimple321`（2026-08-04 已修） |

### 23.3 UI 结构

| 区块 | 结构 |
|------|------|
| 找点算法 | 「算法来源」下拉（`CasComboBoxStyle`：Chrome 内 ContentPresenter + 透明 ToggleButton） |
| ⓪ | 独立橙边卡片；条件显隐见上 |
| ①～④ | **一大卡片「视觉对齐标定」** 内四个 `CasFormSubSectionStyle` 小卡片：3D ROI / 「上」ROI / 简单对齐下拉 / 偏移 Edit |

### 23.4 清单

- [x] Settings `Pre321View*` + `HasPre321ViewPrepPrerequisites`  
- [x] 服务 + 接口单测 + 主流程接入  
- [x] 仅本地算法显示；标题文案  
- [x] ①～④ 大框套小框；下拉样式修复  
- [x] 斜俯视 `Vision3dSectionPointsDetector` 重写（§18）  
- [x] **VisionDll 路径跳过 Pre321**（服务层门控，2026-08-04）  
- [x] 流程/文件日志标明当前找点算法（`【找点算法】` 前缀）  
- [ ] 真机：Pre321 后 `*_points.png` 与人工标注一致  
- [ ] 真机：VisionDll 默认视角找点与 DLL 离线单测一致  
