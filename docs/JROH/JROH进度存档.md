# JROH 进度存档

> **文档版本**：2026-10-10
> **状态**：**新增第 4 条业务「极耳错位闭环」模板骨架**（2026-10-10：模型 / 页面 / 参数对话框 / 曲线控件 / 工厂·管理器·DI 注册 / 参数持久化齐备；**采集通道与闭环算法待现场资料**）；此前代码已成型（三页 + 三业务闭环 + CSV），**四页均未在数据库启用**，**点位表与现场真值未录入**
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
| **PLC 设备 / 点位名 / 下发名 / 触发信号** | [plc点位表.csv](./plc点位表.csv)（**⚠️ 地址列待现场录入**；极耳错位点位清单同为 ⏸ 待录入）；规划 [§4](./JROH项目开发规划.md#4-plc-与数据采集设计) |
| **参数键 / 业务参数模型 / 存储类型** | [配置键表.csv](./配置键表.csv)；规划 [§5](./JROH项目开发规划.md#5-参数配置与持久化) |
| 工艺阈值数值（CP 尺寸区间等） | [工艺阈值表.csv](./工艺阈值表.csv)（抄自代码默认值，**待现场确认**）；规划 [§5.3](./JROH项目开发规划.md#53-工艺阈值数值源码默认值待确认) |
| 架构分层 / 服务清单 / DI | 规划 [§3](./JROH项目开发规划.md#3-总体架构设计) |
| PW / OH / MC 三条闭环算法与数据流 | 规划 [§6](./JROH项目开发规划.md#6-三条闭环算法与数据流) |
| CSV 输出列与目录规则 | 规划 [§7](./JROH项目开发规划.md#7-落盘与文件输出) |
| 风险与待确认 | 规划 [§10](./JROH项目开发规划.md#10-风险与待确认事项) |
| 启用模块 SQL（含极耳错位页） | [enable_jroh_module.sql](./enable_jroh_module.sql) |
| **原始开发资料投放** | [开发资料/](./开发资料/)（已有 MC 膜卷号样例；**极耳错位资料尚未投放**） |
| 同构参照项目（单项目多业务工厂模式） | `Services/Project/JRCW/`；规划 [§3.4](./JROH项目开发规划.md#34-与-jrcw-的异同) |
| 平台新模块约定 | [新增业务模块上手指南.md](../新增业务模块上手指南.md) |

---

## 1. 当前状态

**JROH = 卷绕 OH 闭环，一个项目下挂四条独立业务**（工厂 + 管理器模式，对齐 JRCW）：

| 业务（`EBusinessType`） | 菜单页（`[Module]` 名） | 数据来源 | 闭环输出 |
|------------------------|------------------------|----------|----------|
| `PoleWidth` 极片宽度闭环 | `VM_PWOfJROHPage`「极片宽度闭环」 | PLC 触发 + CCD 模宽 | 正 / 负极纠偏值下发 |
| `CoatedOH` 包覆 OH 闭环 | `VM_OHOfJROHPage`「包覆 OH 闭环」 | PLC 双触发 + AC/SA 数组 | 前 / 中 / 后段正负极纠偏下发 |
| `MultiCorrect` 多级联动闭环 | `VM_MCOfJROHPage`「多级联动纠偏闭环」 | **文件监听（CSV）** | 11 路纠偏中值下发 |
| `Mismatch` 极耳错位闭环 | `VM_MisOfJROHPage`「极耳错位闭环」 | ⏸ **待资料**（模板骨架未接采集） | ⏸ **待资料**（算法占位，只落盘不下发） |

**已具备**：`EBusinessType` / `IBusinessService` / `BusinessServiceBase<T>` / 工厂 / 管理器 / DI 全部就位；四个 `[Module]` 页面 + 四个参数配置对话框 + 五类曲线/选择控件 + 主题样式齐备；四条业务的 CSV 落盘链路均已接（极耳错位为骨架落盘）。

**关键现状（阻断项）**：

- ❌ **四个页面均未启用**：`workspace_project` 无记录、`is_enabled = 0`，菜单看不到 → 见 [enable_jroh_module.sql](./enable_jroh_module.sql)
- ❌ **PLC 设备记录未确认**：`JROHCore.PLCName = "卷绕OH闭环PLC"`，仓库内无该 PLC 的种子/迁移，需现场确认存在
- ⏸ **PLC 点位表缺失**：三条既有业务按**参数名**读写（如 `GetPLCAddressValue("正极A面CCD模宽数据")`），**具体地址全部未知**；极耳错位连点位名都未录入 → 见 [plc点位表.csv](./plc点位表.csv)
- ⏸ **极耳错位闭环为模板骨架（2026-10-10）**：模型 / 页面 / 对话框 / 曲线 / 注册 / 持久化齐备，但 `MisBusinessService.InitializeAsync` 未接采集通道、`TryCalculateAsync` 只 Warn 并返回 false → **不读写任何 PLC 点位、无闭环下发**，采集数据只落盘 CSV。资料到位后按该两处 TODO 补齐
- ✅ **MC 数据源已接通（2026-09-21，代码已落盘）**：解析复用平台 `JROHCsvHelper.SearchCsv`，由 `MCBusinessService.ConvertToGatherModel` 逐路调用 `ConvertToMCGatherData` 按表头名映射 11 路（跳过重复表头行、清洗行首反引号、缺列容错）；监听 `Created`/`Renamed` + 800ms 防抖 + 文件指纹去重 + 膜卷号 15 位校验 + 目录日期校验 + 解析入队闭环；主界面曲线按 3 组 5+4+2 落地
- 📋 **提交前正式审查（2026-10-09）**：P0=0 / P1=7（已修 4）/ P2=4（已修 3）；待人工确认：本地测试库连接串、JRCW 压花阈值、样例 CSV 入库 → [审查报告-20261009-1506.md](./审查报告-20261009-1506.md)（其中「`MisBusinessService` 未接入」一项已由 2026-10-10 模板骨架处置）
- ⚠️ **隔膜「外侧破标定值」列在样例 CSV 中不存在**：该通道目标值占位 `0`；当前样例下合格点通常为 0 → 闭环走「无有效采样点，不下发」，但**无显式「目标列缺失不下发」保护**（个别点落在 (−2,2) 时仍可能按错误基准下发，见审查报告加严建议）—— 待现场补列后自然消解
- ⏸ **OH 单边闭环未验证**：`IsSigleClosedLoop = true` 的整链路未现场测过（见待办 #9）

### 待办

| # | 任务 | 状态 | 验收 | 详规索引 |
|---|------|------|------|----------|
| 1 | 四页菜单启用（含 BelongToVM 结论） | ❌ | 重启后左侧菜单可见并可进四页 | [enable_jroh_module.sql](./enable_jroh_module.sql)、§3.3 |
| 2 | 确认 `JROHCore.BelongToVM` | ⏸ | 问负责人：合并为单页 `VM_JROHClosedLoopPage`，还是分页各写本页 VM 名 | §3.3、§10 #1 |
| 3 | **OH 负极 PLC 写入名纠错** | ❌ | 负极写入用「负极…」名，且判据用 `ClosedLoopEnableNeg` | §6.2、§10 #2 |
| 4 | OH「中断」→「中段」文案 | ❌ | 下发参数名与日志文案统一 | §6.2 |
| 5 | `AdjustInterval` 落地或删除 | ⏸ | 确认用途；确认无用则删字段与对话框绑定 | §6.1、§10 #3 |
| 6 | **录入 PLC 点位表** | ⏸ | [plc点位表.csv](./plc点位表.csv) 地址 / 类型 / 设备全部填实（含极耳错位点位清单） | §4、§10 #4 |
| 7 | 确认 PLC 设备记录 `卷绕OH闭环PLC` | ⏸ | 现场 `plc_config` 存在且已连接 | §4.1 |
| 8 | 补 MC 的 CSV 解析 + 监听 + 曲线主界面 | 🔄 | ✅ 解析 / 监听 / 防抖 / 膜卷号校验 / 入队闭环 / 3 组 5+4+2 曲线已落盘；⏸ 隔膜标定值列缺失（只画检测值、不下发）；⏸ 现场实跑与 Tab 切换刷新未验证 | §6.3、§10 #5 |
| 9 | OH 链路修复 + 单边闭环验证 | ❌ | 先让 `coatedSigalListener` 轮询起来；再 `IsSigleClosedLoop` true/false 各现场测一轮 | §6.2、§10 #10 |
| 10 | 四个 CSV 定稿（点位/阈值/配置键） | 🔄 | 现场确认后覆盖「源码默认值」与「模板占位」标注 | §5.3、§7 |
| 11 | 编译验证（含本轮改动） | ✅ | 2026-10-10：SDK 9.0.316（临时 `global.json`，验证后已删）下 `dotnet build SUNWODA_SEVB.csproj` **0 error / 121 warning**；首轮 1 处 `CS0160`（catch 顺序）已修复并复编译通过。⏸ 应用未启动、页面与闭环未实跑 | — |
| 12 | 清理已注释模拟代码 | ❌ | MC/PW 服务内注释块删除 | §10 #7 |
| 13 | 平台架构文档同步 | ❌ | `docs/项目架构说明.md` §5.1/§5.2/§7 增列 JROH（含极耳错位业务） | — |
| 14 | 处理提交前审查遗留的 3 项待人工确认 | ⏸ | `ConfigurationService` 本地库 / `DYC04BusinessService` 阈值 / `开发资料` 样例 CSV 入库 | [审查报告-20261009-1506.md](./审查报告-20261009-1506.md) |
| 15 | **极耳错位闭环资料录入 + 采集/算法实现** | ⏸ | ① 现场资料 → `开发资料/`；② 点位进 [plc点位表.csv](./plc点位表.csv)；③ 补 `MisBusinessParamModel` 专属参数与 [配置键表.csv](./配置键表.csv)；④ 实现 `MisBusinessService.InitializeAsync`（采集）与 `TryCalculateAsync`（闭环+下发）；⑤ 现场实跑验收 | §3.1、§6、§10 |

### ✅ 已完成（2026-10-10：三个参数弹窗按 v5 落地，仅动 XAML）

> 评审路径：v3 语义分区 → v4 精致化间距 + 根目录单行 → **v5 配色对齐主界面**；定稿图 [设计示意图/参数界面观感优化-可视图-v5.html](./设计示意图/参数界面观感优化-可视图-v5.html)。
> 此前「导航自适应 + 卡片化」那一版（胶囊分段 + 卡片化 + OH 拆三组）**已被使用方回档，未保留**。

- ✅ 配色改为**白底浅色**，取色全部来自 JROH 主页面（`#FFFFFF` 底 / `#F8FAFC` 分区 / `#E2E8F0` 描边 / `#1E293B` 正文 / `#EFF6FF` hover / `#3DCC84` 强调 / `#29CC7A` 保存），解决「页面浅色、弹窗深色」的脱节
- ✅ 参数**语义分区**：成对参数 → 正极/负极两列；单值·共用参数 → 单列行（标签 200 + 定宽 152 + 说明）；MC 数据源根目录 → **横向单行**（路径框 + 更改，不再左右分列）；尺寸基准 → 三列表格
- ✅ 控件规格统一：输入框 36 高 / 圆角 6 / Consolas 右对齐、行高 42、分区内边距 16–18、开关与按钮 36 高、弹窗内边距 24、弹窗宽 576
- ✅ 交互反馈（纯 XAML）：输入框 hover/聚焦绿边、行 hover 浅蓝、Tab 选中绿色下划线、关闭按钮 hover
- ✅ 保留：Tab 位置与数量（PW/OH 各 2、MC 1）、保存/取消位置、全部绑定与 `Tag` 文案机制（开/关、已启用/已关闭、正面/反面）
- ✅ 验证：`dotnet build`（SDK 9.0.316）**0 error / 123 warning**（均既有）；三文件先经 XML 校验再替换，BOM 与 CRLF 保持；⚠️ 应用与 VS 占用 `bin`，验证输出到 `obj\verify\`（已清理），**观感需关闭应用重新生成后确认**
- ✅ **运行反馈修正（同日第二轮）**：① Tab 改回原生 `TabControl/TabItem` + 自定义模板（间距 12、`MinHeight=30`），解决「太紧 + 文字截断」；② 内容区 `ScrollViewer` 加 `Width="538"`，解决右侧溢出；③ 底栏改 `DockPanel` + 文案缩短 + `TextTrimming`，解决提示被截断/贴按钮；④ 全面紧凑化（控件 32 高、行高 36、分区内边距 14/12、弹窗内边距 18/16、`MaxHeight` 360）；⑤ `FloderPicker` 显式 `Foreground="#FF1E293B"`，解决根目录看不见值。**绑定已逐项核对：PW 50 / OH 50 / MC 11 项，与改动前一致，无失效**。复编译 **0 error / 121 warning**
- ✅ **Tab 文本不可见修复（同日第三轮）**：根因是 `LightTabItemStyle` 的 **`Margin="0,0,22,0"` 占用 Tab 条目宽度、把文本压没**（非行高问题）；已删除该外边距、`ContentPresenter` 改 `Margin="10,4,10,6"`、items host 由 `TabPanel` 换为 **`StackPanel`**（按内容测量不压缩），保留内容行 `Height="Auto"` + `MinHeight=34` + `LineHeight=20`。同时清理无用引用（`xmlns:convert/d/hc/local/mc`、两个未用转换器、`JROHCustomControlStyles` 合并字典、重复注释），现 PW/OH 仅剩 `xmlns` + `xmlns:x`，MC 另留 `xmlns:uc`。复编译 **0 error / 123 warning**
- ⏸ 未做（按「仅外观」约定）：mm 换算、上下限范围校验、数据源「打开目录」、监听状态徽标、OH 分段可视化条
- ⏸ 待人工确认：浅色一致性、单值行 152px 与右对齐、小屏下 576 宽弹窗是否要自适应
- 注：极耳错位（Mis）参数弹窗当时未动（按指定只改 3 个）；**已于 2026-10-13 按同一套浅色规则重做**，见下条

### ✅ 已完成（2026-10-13：极耳错位闭环参数落地，按现场参数表）

> 依据现场《CP控制参数 / 设备控制参数》表；不臆造数值（符号项默认 0 并标 ⏸）。

- ✅ `MisBusinessParamModel`：新增 29 项业务参数（CP 7 项 + 设备控制 22 项），保留 5 项通用项 → 共 **34 个属性**
- ✅ `MisBusinessParamDialog.xaml`：重做为浅色风格，**2 个 Tab**（CP控制参数 / 设备控制参数）；设备页分区为 缓存与卷针（两列）／换卷收集数（三列）／限幅（最大·最小 表格）／调节频次／前馈错位（D中·D尾 表格）+ 闭环开关 + ⏸ 提示条；**绑定 34 项与模型一一对应**
- ✅ **修复编译**：`MisGatherData.cs` 已不在仓库（`MisBusinessGatherModel` 报 CS0246），按原设计重建该文件
- ✅ `配置键表.csv`：Mis 字段由 5 行占位 → **34 行真实字段**（含类型/默认值/⏸）
- ✅ 验证：`dotnet build`（SDK 9.0.316）**0 error / 123 warning**；XAML 通过 XML 解析校验
- ⏸ 待现场：符号项（d / L0 / L总 / a / b / △S / D中 / D尾）数值与量纲
- ✅ **同日细化（2026-10-13 第二轮）**：① 「电芯结构 / 周长一致性数据 / 入料调节频次」三项**改为二选一开关**（`IsSingleTab` / `IsPosConsistency` / `Is2nInfeedFrequency`，做法同 OH 的 CCD 检测面，新增 Tag `单双极耳`/`正负极`/`2n3n`）；② **删除 5 项通用项**（`IsUse`、方向修正系数、正/负极闭环开关）→ 模型属性 34→**29**；③ CP 控制参数页由单列 7 行改为**两列并排 4 行**并压紧间距（控件 30 高、输入框 110、行距 2）；④ **删除 `MisGatherData` 类**，采集通道改用 `List<(DateTime,float,float)>`（连带改 `MisBusinessGatherModel` / `MisBusinessService` / `VM_MisOfJROHPage.AppendSeries`）；配置键表 Mis 字段 → 29 行。复编译 **0 error / 123 warning**
- ✅ **同日布局再调（2026-10-13 第三轮）**：CP 控制参数页**改回单列**；「缓存与卷针」行距 2；「换卷收集数」列间距 24（独立间隔列）；「调节频次」开关定宽 110 且左对齐（与输入框严格对齐）；限幅/前馈错位表格列间距 12。**随后修「缓存与卷针」超宽**：原布局 130+110+28+130+110=**508px** ＞ 分区可用 **504px**（弹窗 576 − 边框 2 − 页边距 36 − 分区内边距 32）→ 最右输入框被裁；改为「标签 118 + 间距 8 + 输入框 98」两组 + 组间距 28 = **476px**（余 28px），并统一文本与输入框间距为 8。复编译 **0 error / 123 warning**
- ✅ **配置键表重构（2026-10-13 第四轮 · 文档）**：依现场 4 份业务参数表把 [配置键表.csv](配置键表.csv) 改为**按业务分区**：新增首列「业务」，列结构 `业务, 分类, 参数键名, BelongToVM, Value类型, RoleRank, 源码默认值, 说明`，按 项目级 → PW → OH → MC → Mis → 通用 连续成区（149 行：PW 49 / OH 50 / MC 9 / Mis 30 / 通用 9 / 项目级 1），每区以「参数键」行开头；说明列写入参数表 mm 值便于对账
- 🔴 **量纲纠正（重要）**：尺寸类整数单位 = **µm（0.001 mm）**（参数表 mm 值 × 1000 = 代码默认值：196→196000、78.9→78900、2.5→2500）；规划 §5.3 第 1 条与 §10 待办 #8 旧的「0.1 µm / 19.6 mm / 差 10000 倍」结论**已改正**，待办 #8 收敛为「仅剩 UI 是否换算 mm 显示」
- ⚠️ 新记录 6 处**参数表与代码/界面不一致**（未改代码，待现场确认）：PW `CilOverNegTop_UpperLimit` 33500 vs 3.5mm、PW `AdjustFrequency` 2 vs 4、OH `MoldToNegSATop_LowerLimit` 100 vs 1mm、OH 界面 `CountOfFrontEnd/CountOfBackEnd` 与模型 `CountOfFront/CountOfBack` 名不一致、OH `IsSigleClosedLoop` 界面无控件、OH `IsUse` 未使用
- ✅ **配置键表乱码修复 + 模块归并（2026-10-13 第五轮 · 文档）**：上一轮误存为**无 BOM 的 UTF-8** 导致 Excel 打开乱码 → **删除后重新生成**（**UTF-8 with BOM** + CRLF），并把 4 条 `CSV输出列` 与 4 条 `EBusinessType` 从「通用」区**归回各自业务模块**；「通用」区只留 3 条纯全局约定（落盘目录模板 / CSV 分卷阈值 / 尺寸量纲）。最终 **151 行**：项目级 1 / PW 51 / OH 52 / MC 11 / Mis 32 / 通用 3
- ⚠️ 注意：闭环开关已删，`TryCalculateAsync` 注释已标注「下发前须与现场确认」；后续实现算法时**勿默认允许下发**（规范 A-03）

### 已完成（2026-10-10 本轮：极耳错位闭环模板骨架）

- ✅ 模型：`MisBusinessParamModel`（通用闭环参数占位）、`MisGatherData`、`MisBusinessGatherModel`、`MisBusinessCalculateModel`
- ✅ 服务：`MisBusinessService` 重写为同构骨架（不再引用 MC 类型）；`EnqueueGatherData` 采集入口 + 闭环消费循环 + CSV 落盘；**不读写 PLC 点位**
- ✅ 页面：`VM_MisOfJROHPage`「极耳错位闭环」+ `MisOfJROHPage.xaml`（正/负极两路曲线占位 + 调机日志）
- ✅ 对话框：`VM_MisBusinessParamDialog` + `MisBusinessParamDialog.xaml`（含「待资料」占位说明）
- ✅ 控件：`MisMatchPlot`（检测值 / 设定值双曲线）
- ✅ 接入：基类 `LoadBusinessParamConfig` / `UpdateConfigAsync` 的 `Mismatch` 分支、`BusinessServiceManager` 注册、`AddJROHServices` 注册、`Load/SaveMisBusinessParamProjectParameters`（键 `MisBusinessParam`）
- ✅ 文档：CSV（配置键 / 点位）与启用 SQL 同步至四页

### 已完成（接手前既有代码，首轮仅补文档）

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
| **Mis 业务（模板骨架）** | `Services/Project/JROH/Business/MisBusinessService.cs`（采集 TODO 在 `InitializeAsync`、算法 TODO 在 `TryCalculateAsync`；采集统一入口 `EnqueueGatherData`） |
| 参数 / 采集 / 计算模型 | `Core/Models/Project/JROH/{PW,OH,MC,Mis}BusinessModels/` |
| 参数持久化扩展 | `Tool/Extension/ProjectSettingExtension.cs`（`#region JROH项目`，约 1352 行起；Mis 段在文件末尾） |
| CSV 落盘工具 | `Tool/Helper/JROHHelpper/JROHCsvHelper.cs` |
| 页面 VM | `ViewModels/Pages/JROH/VM_{PW,OH,MC,Mis}OfJROHPage.cs` |
| 页面 View | `Views/Pages/JROH/{PW,OH,MC,Mis}OfJROHPage.xaml(.cs)` |
| 参数对话框 | `ViewModels/Dialogs/JROH/BusinessDialogs/`、`Views/Dialogs/JROH/BusinessDialogs/` |
| 曲线控件 | `Component/UserControls/Project/JROH/{PoleWidthPlot,CoatedOHPlot,MultiCorrectPlot,MisMatchPlot,FloderPicker}.xaml(.cs)`；`MultiCorrectPlot` / `MisMatchPlot` 支持检测值/设定值双曲线（`LineSeries1Collection` / `LineSeries2Collection`） |
| MC 曲线数据源 | `ViewModels/Pages/JROH/VM_MCOfJROHPage.cs` 的 11 路 `*Series1/2`（检测值 / 设定值） |
| Mis 曲线数据源 | `ViewModels/Pages/JROH/VM_MisOfJROHPage.cs` 的 `PositiveTabSeries1/2`、`NegativeTabSeries1/2`（占位两路） |
| 主题样式 | `Component/Themes/Project/JROH/JROHCustomControlStyles.xaml` |
| DI 注册 | `SUNWODA_SEVB/Services/ProjectServiceCollectionExtensions.cs` → `AddJROHServices()` |

分层原则见规划 §3.1（勿在本文展开）。

---

## 3. 下一会话阅读顺序

1. **本文** — 状态、待办、代码入口
2. 按任务打开 §0 索引中的规划章节或 CSV（例：纠错 OH 下发名 → 规划 §6.2 + [plc点位表.csv](./plc点位表.csv)；开发极耳错位 → 待办 #15 + `MisBusinessService.cs` 两处 TODO）
3. 改对应代码入口表中的文件
4. 收尾：**先更新本文**（勾待办）；在 [版本变更记录](./JROH版本变更记录.md) **顶部追加一条**；规格变了再改规划对应节

---

## 4. 首次接手须知（踩坑点）

> 以下均为本次通读代码实测所得，按「会踩坑」的先后排列，改码前务必先读。

1. **`OnDataReceived` 只推曲线，闭环由 `CalculateLoopAsync` 独立消费缓存** — 曲线 / 调机记录是 UI 旁路，别以为「界面没动就没跑闭环」。
2. **四页都会 `StartBusinessAsync`**（`OnInitializeAsync` 中，`OnCleanupAsync` 中 `StopBusinessAsync`）；但 `OHBusinessService.ExecuteAsync` 中 `await Task.WhenAny(signa1Task, calculateTask)` **只传入了 `poleSigalListener`（极片信号）的轮询任务**，`coatedSigalListener`（包覆信号）的 `PLCSigalListenerMonitorLoopAsync` **从未被启动** → 采集入队的唯一入口失效，OH 链路当前不通。待办 #9 处置。
3. **OH 触发顺序**：收到「极片数据触发信号」只更新 `moduleWidthOfPosA/B` 后 `return`，**不进闭环**；只有收到「包覆数据触发信号」才组装 `OHBusinessGatherModel` 并入队。
4. **OH / PW 在闭环开关关闭时不写 PLC**，只打 `Warn` 日志（文案为「推荐下发值」）。
5. **MC 的 CSV 监听已重做（2026-09-21 二轮）**：`Created` / `Renamed` 两事件（更名完成 = 数据就绪，不再监听持续写入）+ **800ms 防抖** + **文件指纹去重**（文件长度 + 最后写入时间），回调中真正解析文件并入队闭环（一个卷芯 = 一条采集数据）。
   - 触发口径：只有符合**膜卷号**命名规则的文件才被处理（纯数字临时文件跳过）；不做启动时存量扫描（只响应运行期间的文件事件），避免重启后重复投递。
6. **`JROHCore.BelongToVM` 是纯配置键**：`VM_JROHClosedLoopPage` 在仓库中**不存在任何同名类**（同名模式见 JRCW 的 `VM_JRCWClosedLoopPage`）。它只作 `project_setting.BelongToVM` 的取值，**与菜单无关**。
7. **MC 曲线保留策略已改（2026-09-21 二轮）**：原 `count >= param.T` 清空 + `count` 只增不减的写法已移除，改为**按上限裁剪**（每路最多保留最近 2000 点）；11 路数据集见 `VM_MCOfJROHPage` 的 22 个 `*Series1/2` 集合。
8. **`PWBusinessGatherModel` 和 `OHBusinessGatherModel` 不是 `ModelBase`**（不实现 `INotifyPropertyChanged`），而 `MCBusinessGatherModel` / `MisBusinessGatherModel` 是；改绑定时注意。
9. **膜卷号 CSV 的解析要点（2026-08-30 现场样例实测，2713 个文件）**：UTF-8 BOM；**第 1、2 行均为表头**（重复表头需跳过）；**数据行行首带一个反引号**；数值列用双引号包裹；共 61 列；时间格式 `yyyy-MM-dd HH:mm:ss.fff`。
   - 11 路通道按**表头名**（非列序号）映射：正极 入卷 + 蛇形1~4、负极 入卷 + 蛇形1~3、隔膜 上/下；**隔膜目标值列「上/下隔膜外侧破标定值」样例中不存在**（只采集检测值、不下发，⏸ 待现场补列）；正负极「放卷纠偏中值」列同样不存在，但放卷不是闭环输入通道，不影响闭环。
10. **命令行编译环境**：本机无 .NET 8 SDK（仅 9.0.316 / 10.0.401），目标框架为 `net8.0-windows`。
    - 用 **SDK 10 编译会假报 `CS2015 是二进制文件而非文本文件`**（全仓文件齐报，与源码无关）；
    - 用 **SDK 9（`9.0.316`）可正常编译**：`dotnet build SUNWODA_SEVB\SUNWODA_SEVB.csproj`（Visual Studio 内编译不受影响）。

11. **文档与代码曾不一致（2026-09-21 核查）**：本存档与版本变更记录中「MC CSV 监听解析 + 曲线三图」条目所描述的 `MCBusinessCsvParser` / `MCPlotSeries` / `PosPlotSeries` 在代码中**不存在**（全仓 grep 0 匹配），当时服务仍是「只监听 `Renamed` + 上报空模型」。本轮已按样例真正落盘。**教训：写码与写文档必须同批完成，收尾用 grep 核对新增类型是否真的存在。**

12. **命令行编译的两点经验（2026-09-21 补）**：① 本机默认 SDK 10 会假报 CS2015，编译前需让 SDK 9 生效（临时放 `SUNWODA_SEVB/global.json` 指定 `9.0.316`，用完删除）；② 无外网时可用本机包缓存离线还原：`dotnet restore SUNWODA_SEVB.csproj --source "%USERPROFILE%\.nuget\packages" -m:1 -nodeReuse:false -p:RestoreDisableParallel=true`（`SUNWODA_SEVB.MES` 用浮动版本 `6.0.*`，必须把包目录当**源**而非 fallback，否则 NU1100）。

13. **极耳错位闭环当前是「结构齐、逻辑空」的模板（2026-10-10）**：进页面、开参数对话框、落盘 CSV 都能跑，但**不会有采集数据、不会有任何 PLC 读写**——`InitializeAsync` 的采集通道与 `TryCalculateAsync` 的算法都是 TODO 占位。别把「页面正常、日志无异常」当作闭环已实现（规范 A-03：不得先报已完成）。补资料时只改这两处 + 模型字段 + 对话框，其余注册链路已通。

14. **四条业务彼此独立 + 编辑会丢 BOM（2026-10-10）**：
    - **不许动其他业务**：PW / OH / MC / Mis 各自独立，新增或修改极耳错位闭环时只在 `Mis*` 自身文件内实现；共享点（`BusinessServiceBase` 的 `LoadBusinessParamConfig` / `UpdateConfigAsync` 分派、`BusinessServiceManager` 构造注册、`AddJROHServices`、`ProjectSettingExtension` 的 `#region JROH项目`）**只允许新增行**，不得改写或重构其他业务的分支与模型。
    - **编辑工具会丢掉 UTF-8 BOM**：仓库 `.cs` / `.xaml` 普遍带 BOM（`docs/` 下的 md / csv / sql 则**不带**），用工具重写文件后 BOM 会消失，使共享文件出现与功能无关的 `-﻿using` 变更。改完请自检：`git diff --numstat <共享文件>` 应**只见「增」不见「删」**；若出现删除行，多半是 BOM 或行尾被改（本轮 4 个共享文件的 BOM 已修回）。
