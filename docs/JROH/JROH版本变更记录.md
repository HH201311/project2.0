# JROH 版本变更记录

> **用途**：按日期倒序记录「做了什么、为什么、落在哪」；**当前待办与状态以 [JROH进度存档.md](./JROH进度存档.md) 为准**
> **格式**：`> **YYYY-MM-DD 变更（标题）**` + 背景 / 做法 / 落点；规格变更写清「见规划 §x」
> 新会话默认 **不必** 拖本文；需要追溯历史时再按进度存档索引打开。

---
> **2026-10-10 变更（参数弹窗 Tab 文本消失修复 + 清理无用引用）**
> 1. **背景**：三个参数弹窗运行后 Tab 文本不可见/被裁。定位到 `LightTabItemStyle` 里的 `Margin="0,0,22,0"` —— 该**外边距占用了 Tab 条目的可用宽度，把文本压到看不见**（此前误判为行高问题，行高只是次要因素）。
> 2. **做法（仅改这 3 个 XAML 的同一段样式）**：
>    - **删除** `LightTabItemStyle` 的 `Margin="0,0,22,0"`；项间距改由内容内边距承担。
>    - `ContentPresenter` 的 `Margin` 由 `4,4,4,6` 改为 **`10,4,10,6`**（左右各 10px 呼吸，相邻项自然形成 20px 间距）。
>    - `LightTabControlStyle` 模板里的 items host 由 `TabPanel` 换成 **`StackPanel`**（水平堆叠、按内容测量，永不压缩子项）。
>    - 保留上一轮的高度修复：内容行 `Height="Auto"`、`MinHeight="34"`、内嵌 TextBlock `LineHeight="20"` + `LineStackingStrategy="BlockLineHeight"`。
>    - **清理无用引用**（每文件约 791 字符）：删除 `xmlns:convert` / `xmlns:d` / `xmlns:hc` / `xmlns:local` / `xmlns:mc` 与 `mc:Ignorable="d"`、两个未被引用的转换器资源、`JROHCustomControlStyles.xaml` 合并字典（`CCTB.IconFont`、`CCTB.SourceHanSansCN-*` 均来自全局 `App.xaml`，不依赖它）、重复的 `<!-- 底栏 -->` 注释。**现 PW/OH 仅剩 `xmlns` + `xmlns:x`；MC 另保留 `xmlns:uc`**（`FloderPicker` 需要）。
> 3. **落点**：`SUNWODA_SEVB/Views/Dialogs/JROH/BusinessDialogs/{PW,OH,MC}BusinessParamDialog.xaml`；**其他代码与极耳错位弹窗均未改动**。
> 4. **验证**：`dotnet build`（SDK 9.0.316）→ **0 error / 123 warning**；每次替换均通过 XML 解析校验，**校验不通过不写回文件**。
> 5. **经验**：自定义 `TabItem` 模板时不要在 TabItem 上放大外边距（尤其右侧）——它参与 TabPanel/StackPanel 的宽度分配，宽裕时会挤压 Header 文本；项间距建议放进模板内部（ContentPresenter 的 Margin/Padding）。

> **2026-10-10 变更（三个参数弹窗运行反馈修正：Tab 间距 / 宽度约束 / 底栏 / 紧凑化 / 根目录前景色）**
> 1. **背景**：v5 落地后实机运行发现 5 个问题：① Tab 与下方内容过紧且文字被截断；② 内容右侧超出窗口；③ 底部提示文本被截断且与按钮无间隔；④ 整体偏空、需更紧凑；⑤ 疑心参数绑定失效（MC 根目录看不到值）。
> 2. **排查结论（⑤）**：**不是绑定失效**。用脚本提取三个弹窗的绑定清单逐一核对：PW **50 项** / OH **50 项** / MC **11 项**（含 `WatcherRootPath`），**与改动前完全一致、无漏项**。根目录看不到值的原因是 `FloderPicker` 内部文本框沿用它自己的默认前景色（为深色弹窗设计的浅色），在浅色底上不可见。
> 3. **做法（仍仅改这 3 个 XAML）**：
>    - ① **导航改回原生控件**：`hc:TabControl` / `hc:TabItem` → 原生 `TabControl` / `TabItem`，自定义 `LightTabControlStyle`（模板内 `ContentPresenter Margin="0,12,0,0"`）与 TabItem 模板（`MinHeight=30`、`ContentPresenter Margin="2,5,2,7"`），解决「太紧 + 文字截断」。
>    - ② **宽度约束**：内容区 `ScrollViewer` 显式 `Width="538"`（与弹窗内容宽一致），避免在无约束宽度下 `*` 列退化为 `Auto` 导致右侧溢出。
>    - ③ **底栏重排**：改 `DockPanel`（按钮 `DockPanel.Dock="Right"` + `Margin="18,0,0,0"`），提示文案缩短为「保存后需二次确认才会写入；取消或关闭不修改。」并加 `TextTrimming="CharacterEllipsis"`，不再与按钮重叠。
>    - ④ **紧凑化**：控件高 36→32、行高 42→36、标签列 200→190、单值框 152→144、分区内边距 18/16→14/12、块间距 18→12、弹窗内边距 24/22→18/16、按钮 36→32 高 / 92→86 宽、内容区 `MaxHeight` 430→360。
>    - ⑤ **根目录可读**：`FloderPicker` 显式 `Foreground="#FF1E293B"`、`Height` 32、`Margin="8,0,4,0"`。
> 4. **落点**：`SUNWODA_SEVB/Views/Dialogs/JROH/BusinessDialogs/{PW,OH,MC}BusinessParamDialog.xaml`。
> 5. **验证**：`dotnet build`（SDK 9.0.316）→ **0 error / 121 warning**（均既有）；每次批量替换后均通过 XML 解析校验。⚠️ **仍需关闭运行中的应用后重新生成**再确认观感。

> **2026-10-10 变更（三个参数弹窗按 v5 落地：浅色对齐主界面 + 参数语义分区，仅动 XAML）**
> 1. **背景**：上一版「导航自适应 + 卡片化」被使用方回档后，经四轮示意图评审（v3 语义分区 → v4 精致化间距 + 根目录单行 → v5 配色对齐主界面）确认最终方案：**保留原有布局骨架**，只做「参数按语义分区 + 控件规格与配色统一」，并把配色统一到 JROH 主页面的**浅色**体系（此前弹窗是深色 `#2B2B2B`，与白底页面脱节）。
> 2. **做法（仅改 3 个 XAML；未动任何 `.cs` / VM / 服务 / 绑定路径 / 默认值）**：
>    - **配色对齐主界面**：弹窗由深色改为白底 `#FFFFFF`；分区底 `#F8FAFC` + 描边 `#E2E8F0`；正文 `#1E293B` / `#334155` / `#94A3B8`；行 hover `#EFF6FF`；强调色沿用项目绿 `#3DCC84`（Tab 选中、输入框聚焦），保存按钮 `#29CC7A`（取消按钮改白底 + `#CBD5E1` 边 + 深字，否则浅色底上白字不可见）。
>    - **参数语义分区**：① 成对参数 → 「正极 / 负极」两列表格；② 单值·共用参数（调节频次、调节间隔、OH 前/后分段个数、K、CCD 检测面、双面 OH、MC 的 T/K/D）→ **单列行**（标签 200 + 定宽 152 输入或开关 + 说明），不再挤占「正极」列、也不再是横跨整行的超宽框；③ 数据源（MC 的 CSV 根目录）→ **横向单行**（路径框 + 更改），不再用「标签列 + 内容列」；④ 尺寸基准（PW/OH 第一页）→ 保持三列表格（基准 / 下限 / 上限）。
>    - **控件规格**：输入框高 36 / 圆角 6 / 内边距 13 / Consolas 右对齐；行高 42；标签列 200；单值框 152；分区内边距 16–18、块间距 18；弹窗内边距 24；开关与按钮高 36。
>    - **交互反馈（纯 XAML）**：输入框 hover 边框加深、聚焦绿边 + 浅绿底；行 hover 淡高亮；Tab 选中深色字 + 绿色下划线；关闭按钮 hover 浅灰底。
>    - **保留**：顶部 Tab 位置与数量（PW/OH 各 2 个、MC 1 个）、保存/取消位置、弹窗宽度 576、全部绑定与 `Tag` 文案机制（开/关、已启用/已关闭、正面/反面）。
>    - **未做**（按「仅外观」约定）：mm 换算、上下限范围校验、数据源「打开目录」、监听状态徽标、OH 分段可视化条。
> 3. **落点**：`SUNWODA_SEVB/Views/Dialogs/JROH/BusinessDialogs/{PW,OH,MC}BusinessParamDialog.xaml`（BOM 与 CRLF 与仓库一致）；评审示意图 `docs/JROH/设计示意图/参数界面观感优化-可视图{,-v3,-v4,-v5}.html`。**极耳错位的 `MisBusinessParamDialog.xaml` 本轮未动**（按指定只改这 3 个）。
> 4. **验证**：`dotnet build`（SDK 9.0.316）→ **0 error / 123 warning**（均为既有 CS1998 / CS0414）；三个文件先经 XML 解析校验再替换，替换后 BOM 保留。⚠️ 验证时运行中的应用与 VS 占用 `bin`，故输出到 `obj\verify\`（已清理）；**实际观感需关闭应用后重新生成再确认**。
> 5. **待人工确认**：① 浅色弹窗与主界面观感是否一致；② 单值行 152px 宽度与数值右对齐是否需微调；③ 小屏（≤1366×768）下 576 宽弹窗是否需要自适应。

> ⚠️ **更正（2026-10-10 同日）**：下方「导航自适应 + 卡片化」那次改动**已由使用方回档**（运行后观感不佳），三个参数弹窗现为**原有布局**。优化方向改为「**保留现有布局，只调视觉**」，对照图见 [设计示意图/参数界面观感优化-可视图.html](./设计示意图/参数界面观感优化-可视图.html)（PW / OH / MC / 极耳错位 共 6 个视图的「现状 vs 优化后」）。该条目仅作历史记录保留。

> **2026-10-10 变更（三个参数弹窗 UI 重设计：导航自适应 + 卡片化，仅动 XAML）**〔⚠️ 已回档，见上〕
> 1. **背景**：`PWBusinessParamDialog` / `OHBusinessParamDialog` / `MCBusinessParamDialog` 原为「顶部 `hc:TabControl` 两页 + 手写 Grid 标签列 + 每控件自带 Margin」的同款表单：行列不齐、观感偏平；MC 只有 1 组参数却挤在 500px 宽内、右侧大片留白。经两轮示意图评审（[设计示意图/](./设计示意图/)），确认采用**方案二「导航自适应」**，其中 MC 内容改为上下结构。
> 2. **做法（仅改三个 XAML；未改任何 `.cs` / VM / 服务 / 绑定路径 / 默认值）**：
>    - **导航自适应**：组数 ≥ 2 → 顶部**胶囊分段**（`PillTabControlStyle` + `PillTabItemStyle`，选中态浅底 + 绿点，右侧补「共 N 组 · 参数规模」文案）；MC 仅 1 组 → **不摆页签**，改标题下一行轻量说明。
>    - **OH 由 2 页拆为 3 组**：尺寸基准 / 闭环调机 / 分段与检测面 —— 控件与绑定**原样搬迁**，未新增、未删除任何参数项（未加「单边闭环」等新开关）。
>    - **卡片化**：`CardBorderStyle`（`#0DFFFFFF`、圆角 10、内边距 15/17）分组，卡片头为「标题 + 右对齐说明」；参数行统一列宽（PW/OH 用 `UniformGrid`，MC 用 `Grid` 三列）；输入框统一 `ParamTextBoxStyle`（高 31、圆角 5、Consolas、数值右对齐、聚焦绿边），基准列加亮（`ParamTextBoxCenterStyle`），下限 / 上限用列头着色区分语义。
>    - **开关统一**：`PillSwitchStyle`（胶囊 + 状态点），**保留原 `Tag` 文案机制**（开 / 关、已启用 / 已关闭、正面 / 反面），绑定与判定逻辑不变。
>    - **MC 上下结构**：数据源卡片（`FloderPicker` 原绑定保留）→ 闭环参数卡片（方向修正系数 / T / K / D，带说明列）→ 功能开关卡片 → 风险提示条；弹窗宽度 880。
>    - **底栏常驻**：左侧说明「保存需二次确认 / 取消不写入」，右侧保存、取消（沿用 `Growl*ButtonStyle`）。
>    - **清理**：删除原三个文件里的隐式 `<Style TargetType="Border">`（会连带污染外层对话框 Border）与 `Rectangle` 分割线样式，改用带 `x:Key` 的显式样式。
>    - **明确未做**（按「仅外观」约定）：mm 换算列、上下限范围校验、数据源「打开目录」按钮、监听状态徽标、OH 分段可视化条 —— 均未实现；提示条为静态文案。
> 3. **落点**：`SUNWODA_SEVB/Views/Dialogs/JROH/BusinessDialogs/{PW,OH,MC}BusinessParamDialog.xaml`（BOM 与 CRLF 已保持与仓库一致）；评审示意图新增 `docs/JROH/设计示意图/参数界面重设计-示意图.html`、`参数界面重设计-示意图-方案二.html`。
> 4. **验证**：`dotnet build`（SDK 9.0.316）→ **0 error / 119 warning**（均为既有 CS1998 / CS0414）。⚠️ 验证时本机 `SUNWODA_SEVB.exe` 与 Visual Studio 正在运行，`bin\Debug` 下 DLL/PDB 被占用，常规 build 会在**复制输出**阶段报 MSB3021 / MSB3027（与源码无关），故改用 `-p:OutDir=obj\verify\` 输出到未占用目录完成 XAML 编译验证（该临时目录已清理）。**实际观感需关闭运行中的应用后重新生成再确认。**
> 5. **待人工确认**：① 小屏（≤1366×768）下弹窗宽度（PW/OH 1020、MC 880）是否需要自适应；② OH 拆成 3 组后的操作习惯；③ 是否需要把胶囊/卡片样式沉淀到 `JROHCustomControlStyles.xaml` 供复用（当前为三个文件各自内联，未动共享样式文件）。

> **2026-10-10 变更（新增第 4 条业务「极耳错位闭环」模板骨架：模型 / 页面 / 对话框 / 曲线 / 注册 / 持久化）**
> 1. **背景**：JROH 需新增极耳错位闭环业务。此前仓库只有半成品 `MisBusinessService.cs`（整份拷自 MC 服务，仍引用 `MCBusinessParamModel` / `MCBusinessGatherModel` / `MCBusinessCalculateModel` 与 `VM_MCBusinessParamDialog`），且 `EBusinessType.Mismatch` 走基类 switch 会抛「未实现的业务类型」、未注册进管理器与 DI，无参数模型、无页面、无对话框。现场资料未到，本轮按要求只补**同构模板骨架**。
> 2. **做法**：
>    - **模型（新增）**：`MisBusinessParamModel`（仅 JROH 四条业务共有的通用闭环参数：`IsUse` / 方向修正系数 / 正负极闭环开关）、`MisGatherData`、`MisBusinessGatherModel`（`GatherTime` / `VirtualCode` / 正负极两路占位通道）、`MisBusinessCalculateModel`（`CalculateTime` / 正负极是否调机与下发值 / `EndMessage`）；⏸ 专属参数与通道一律未添加（规范 B-09，禁止臆造）。
>    - **服务（重写）**：`MisBusinessService` 不再引用任何 MC 类型；`InitializeAsync` 载参数 + 拼 `{FileSaveRootPath}/Mismatch` 落盘路径 + 采集 TODO；`ExecuteAsync` 跑 `CalculateLoopAsync` 闭环消费循环；`CleanupAsync` 收敛队列并重建 + 先摘引用再 `Cancel/Dispose`；新增 `EnqueueGatherData`（后续采集通道统一入口）与 `TryCalculateAsync`（算法占位：Warn + 返回 false，**只落盘、不下发**）；**不读写任何 PLC 点位**；闭环结果落盘 `缓存数据_极耳错位闭环`。
>    - **界面（新增）**：`VM_MisOfJROHPage`（`[Module("VM_MisOfJROHPage","极耳错位闭环")]`，进出页自动启停业务、事件订阅、曲线按上限 2000 点裁剪、调机记录 20 行）+ `MisOfJROHPage.xaml(.cs)`；`VM_MisBusinessParamDialog` + `MisBusinessParamDialog.xaml(.cs)`（含「待资料」占位说明）；曲线控件 `MisMatchPlot.xaml(.cs)`（检测值/设定值双曲线，对齐 `MultiCorrectPlot`）。
>    - **接入点**：`BusinessServiceBase.LoadBusinessParamConfig` / `UpdateConfigAsync` 增 `Mismatch` 分支；`BusinessServiceManager` 注册 `MisBusinessService`；`AddJROHServices` 注册实现与接口映射；`ProjectSettingExtension` 新增 `Load/SaveMisBusinessParamProjectParameters`（键 `MisBusinessParam`，`BelongToVM = VM_JROHClosedLoopPage`，RoleRank 4）。
>    - **文档与配置**：`enable_jroh_module.sql` 增 `VM_MisOfJROHPage`（三页 → 四页）；[配置键表.csv](./配置键表.csv) 增业务参数键 / Mis 字段 / CSV 输出列 / 枚举取值；[plc点位表.csv](./plc点位表.csv) 增「极耳错位点位清单 ⏸ 待现场录入」说明；规划 §1 / §3 / §4 / §5 / §6（新增 §6.4）/ §7 / §8 / §9 / §10 同步。
> 3. **落点**：新增 `SUNWODA_SEVB.Core/Models/Project/JROH/MisBusinessModels/{MisBusinessParamModel,MisGatherData,MisBusinessGatherModel,MisBusinessCalculateModel}.cs`、`SUNWODA_SEVB/ViewModels/Pages/JROH/VM_MisOfJROHPage.cs`、`SUNWODA_SEVB/Views/Pages/JROH/MisOfJROHPage.xaml(.cs)`、`SUNWODA_SEVB/ViewModels/Dialogs/JROH/BusinessDialogs/VM_MisBusinessParamDialog.cs`、`SUNWODA_SEVB/Views/Dialogs/JROH/BusinessDialogs/MisBusinessParamDialog.xaml(.cs)`、`SUNWODA_SEVB.Component/UserControls/Project/JROH/MisMatchPlot.xaml(.cs)`；改 `MisBusinessService.cs`、`BusinessServiceBase.cs`、`BusinessServiceManager.cs`、`ProjectServiceCollectionExtensions.cs`、`ProjectSettingExtension.cs`、`docs/JROH/*`。
> 4. **验证**：`dotnet build SUNWODA_SEVB/SUNWODA_SEVB.csproj` 用 **SDK 9.0.316**、在 `SUNWODA_SEVB/` 目录下执行（临时 `global.json` 指定 SDK，**验证后已删除**）→ **0 error / 121 warning**（均为既有噪声）。首轮 1 处 `CS0160`（`ObjectDisposedException` 是 `InvalidOperationException` 子类，catch 不可达）**已修复并复编译通过**。另：改码过程中 `.cs`/`.xaml` 丢失的 **UTF-8 BOM 已全部修回**，`BusinessServiceBase` / `BusinessServiceManager` / `ProjectServiceCollectionExtensions` 现为**纯新增行**（`git diff --numstat` = 3/0、1/0、2/0），未改写任何其他业务代码。⏸ **未做**：数据库 SQL 未执行、应用未启动，页面与闭环**未实跑**。
> 5. **待人工确认 / 下一步**：① 执行 [enable_jroh_module.sql](./enable_jroh_module.sql) 并**完整重启**，确认菜单出现「极耳错位闭环」；② 现场资料（点位 / 阈值 / 采集方式）到位后按规划 §6.4 实现采集与算法（进度存档待办 #15）；③ 当前无采集、无下发，进页面只会有 Warn 日志，属预期（见进度存档 §4 第 13 点）。

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
