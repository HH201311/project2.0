# JROH 项目开发规划

> **文档角色**：**冷层详规** —— 范围、架构、算法、对接、资料索引。新会话默认**不必**整份读；请从 [JROH进度存档.md](./JROH进度存档.md) §0 索引按需跳转。
> **代号**：`JROH`　**项目中文名（拟）**：卷绕 OH 闭环　**维护人**：待指定　**库名**：`sunwoda_demo`
> **事实来源约定**：点位 / 阈值 / 配置键以 CSV 为准；**本文所有数值均抄自代码字段默认值，未经现场工艺确认**；未知项一律标 ⏸，禁止臆造。

---

## 1. 项目定位与目标

### 1.1 项目背景

JROH 是**卷绕工序的 OH（Overhang，极片/隔膜相对超出量）闭环控制系统**，运行于 SUNWODA SEVB 上位机平台。它复用平台既有的「单项目多业务场景」模式（与 `JRCW` 卷绕极耳错位闭环同构），在一个项目下并行承载三条互相独立的闭环业务。

### 1.2 三条业务范围

| 业务（`EBusinessType`） | 中文名 | 闭环目标 | 数据来源 |
|------------------------|--------|----------|----------|
| `PoleWidth` | 极片宽度闭环 | 按 CCD 模宽推算正/负极总宽目标，输出纠偏值 | PLC（CCD 模宽 + 飞行原点 Y） |
| `CoatedOH` | 包覆 OH 闭环 | 按 AC（负极到正极）/ SA（隔膜到负极）分段统计，输出前/中/后段正负极纠偏 | PLC（数组量 + 前/中/后纠偏值） |
| `MultiCorrect` | 多级联动纠偏闭环 | 11 路纠偏通道按"T 次采集调一次"输出纠偏中值 | **文件监听（CSV）**，非 PLC |

### 1.3 本期不做（明确边界）

- **MES / Web 上传**：三条业务均未接 MES、未调 `IWebApiClient` 上传（服务已注入备用）。
- **报警中心接入**：PW 的工艺越界仅 `MessageBox.Show` 弹窗，未进平台报警体系。
- **单页合并**：不把三页合并为 `VM_JROHClosedLoopPage`（该名字当前只作配置键，见 §3.3）。
- **历史查询页 / 曲线持久化**：曲线仅内存保留最近 1000 点，退出即失。
- **PLC 点位自动标定 / 自学习**：无。

### 1.4 验收口径

| 层级 | 验收标准 |
|------|----------|
| 可进入 | 完整重启应用后，左侧菜单可见并进入三个页面，无异常日志 |
| 可通过 | 三业务闭环按 §6 计算并下发；闭环关闭时只记「推荐下发值」不写 PLC |
| 可追溯 | 每次闭环落一行 CSV，列见 §7；调机记录表保留最近 20 行 |
| 真值 | §4/§5 的 CSV 由现场确认后覆盖，代码默认值视为占位 |

---

## 2. 对接方与资料

| 对接方 | 事项 | 状态 |
|--------|------|------|
| 现场工艺 | CP 尺寸基准/上下限、AC/SA/K/D 阈值确认 | ⏸ 未录入 |
| 现场电气 | `卷绕OH闭环PLC` 设备记录与点位地址表 | ⏸ 未录入 |
| 上游张力系统 | MC 业务监听的张力 CSV 格式与投递方式（写入 vs 重命名） | ⏸ 未确认 |
| 平台组 | `docs/项目架构说明.md` 增列 JROH 模块 | ❌ 未做 |

**原始资料投放区**：[开发资料/](./开发资料/)（规程 §2.2）。整理定稿后：点位 → [plc点位表.csv](./plc点位表.csv)，阈值 → [工艺阈值表.csv](./工艺阈值表.csv)，配置 → [配置键表.csv](./配置键表.csv)。

---

## 3. 总体架构设计

### 3.1 分层

```
Core    : EBusinessType / IBusinessService / JROHCore（常量）
          Models/Project/JROH/{PW,OH,MC}BusinessModels/*  ← 参数/采集/计算三类模型
Tool    : ProjectSettingExtension（参数持久化）/ JROHCsvHelper（CSV 落盘）
Services: Project/JROH/Business/BusinessServiceBase<T>  ← 抽象基类（DB + PLC + 弹窗 + 限幅工具）
          ├─ PWBusinessService / OHBusinessService / MCBusinessService
          ├─ BusinessServiceFactory（按 EBusinessType 取服务）
          └─ BusinessServiceManager（启动/停止 + 异常兜底 + 状态查询）
ViewModels/Pages/JROH : VM_{PW,OH,MC}OfJROHPage（曲线 + 调机记录 + 参数入口）
Views/Pages/JROH      : {PW,OH,MC}OfJROHPage.xaml
Component             : UserControls/Project/JROH/*（4 个曲线/选择控件）+ Themes/.../JROH*
```

### 3.2 生命周期与多态

`BusinessServiceBase<T>` 定义模板方法，三条业务只实现三段：

| 抽象成员 | 职责 | PW | OH | MC |
|----------|------|----|----|----|
| `InitializeAsync` | 载参数、拼落盘路径、起监听 | PLC 信号 | PLC 双信号 | **CSV 文件监听** |
| `ExecuteAsync` | 阻塞跑循环 | 信号 + 计算 | 信号 + 计算 | 仅计算 |
| `CleanupAsync` | 释放 | 同构 | 同构 | 关闭 watcher |

基类同时提供：`StartAsync`（加锁防重入 + `CancellationTokenSource` 链接）、`StopAsync`（轮询 `IsRunning` 至退出）、`RestartAsync`、`LoadBusinessParamConfig<T>` / `UpdateConfigAsync`（按 `BusinessType` 分派到对应仓储方法）、`GetPLCAddressValue` / `WritePLCAsync`、`ShowBusinessConfigDialogAsync<T>`（加载 → 弹框 → 重载比对 → 确认 → JSON 保存）、`ApplyDeadzone` 用的 `FindClosest`。

### 3.3 菜单与配置归属（**关键，待确认**）

- **菜单**：靠 `[Module]` + 数据库 `workspace_project.is_enabled = 1`，**三个页面各自独立**：
  `VM_PWOfJROHPage`「极片宽度闭环」/ `VM_OHOfJROHPage`「包覆 OH 闭环」/ `VM_MCOfJROHPage`「多级联动纠偏闭环」。
- **配置归属**：`JROHCore.BelongToVM = "VM_JROHClosedLoopPage"`，仅作为 `project_setting.BelongToVM` 的**存储键**。
- ⏸ **`VM_JROHClosedLoopPage` 在仓库中不存在任何同名类**（同名模式见 JRCW 的 `VM_JRCWClosedLoopPage`）。因此存在两种待确认设计意图：
  1. 三页原计划合并为一个 `VM_JROHClosedLoopPage`，后拆成三页但常量未改；
  2. 或刻意保持单键共享配置。
  **结论未定 → 见 §10 #1；改之前先问负责人。**

### 3.4 与 JRCW 的异同

| 维度 | JRCW | JROH |
|------|------|------|
| 业务数 | 2（`YWC05` / `DYC01`） | 3（`PW` / `OH` / `MC`） |
| 业务归属 | 一个 `VM_JRCWClosedLoopPage` | **三页各自 `[Module]`** |
| 配置键 | `VM_JRCWClosedLoopPage`（有对应类） | `VM_JROHClosedLoopPage`（**无对应类**） |
| 业务基类 | `JRCWBusinessServiceBase` | `BusinessServiceBase<T>` |
| 复用 | `JRCWMessageBoxDialog` 被 JROH 基类直接复用 | 同上 |

---

## 4. PLC 与数据采集设计

### 4.1 设备

| 项 | 值（源码常量） | 状态 |
|----|----------------|------|
| 设备名 | `JROHCore.PLCName = "卷绕OH闭环PLC"` | ⏸ 现场 `plc_config` 待确认 |
| IP / 端口 / 协议 | 未知 | ⏸ 待现场录入 |

基类写值前会 `_plcService.GetPLCByName(JROHCore.PLCName)`，**取不到即抛异常**；`GetPLCAddressValue` 则按 `ParameterName` 在 `_plcService.RWAddresses` 中线性查找，**查不到返回 `null`/空串/0，不报错**。

### 4.2 触发机制

三条业务统一使用 `ValueListener` + `PeriodicTimer(100ms)` 轮询同名参数的最新值，仅当**值发生变化**时进入回调：

| 业务 | 触发信号（`ValueListener.Name`） | 行为 |
|------|----------------------------------|------|
| PW | `极片数据触发信号` | `1` → `Task.Delay(200)` → 采集 → 入队 |
| OH | `极片数据触发信号` | `1` → 只更新 `moduleWidthOfPosA/B`，**不入队** |
| OH | `包覆数据触发信号` | `1` → 组装完整模型 → 入队 |
| MC | 无 | 由 `FileSystemWatcher` 驱动 |

信号值 `0` 视为复位（仅记日志）；其它值记 Error。

### 4.3 点位清单

**完整点位清单（点位名 / 读写 / 所属业务 / 地址待填）见 [plc点位表.csv](./plc点位表.csv)。**

> ⚠️ 地址、数据类型（`plc_config` 中的 `Type`）**全部未知**；OH 的 `顶部AC` / `底部AC` / `底部SA` / `顶部SA` 按 `int[]` 读取，其长度语义为 **90 个点的分段数据**（`CountOfFront` + 中段 + `CountOfBack = 90`，见 §6.2）。

### 4.4 采集节奏与并发

- 轮询周期 100 ms；置位后固定 `Task.Delay(200)` 再读，等待 PLC 数据稳定。
- 采集结果进 `BlockingCollection<T>`，`CalculateLoopAsync` 用 `GetConsumingEnumerable()` 串行消费 → **单消费者，无并发计算**。
- OH 的 `moduleWidthOfPosA/B` 是服务级字段，由「极片信号」写、「包覆信号」读 → **存在跨信号状态共享**，若两信号乱序需现场验证。

---

## 5. 参数配置与持久化

### 5.1 存储机制

所有参数落在平台表 `project_setting`，按 `Name + BelongToVM` 唯一。JROH 区由 `ProjectSettingExtension` 的 `#region JROH项目(卷绕oh闭环)参数配置相关扩展方法`（约 1352 行起）实现，规则：

- 首次读取时若记录不存在 → **自动 `Add` 一行并写默认值**（这也是默认值进入现场的途径）；
- 业务参数整体以 **JSON 字符串**存 `Value`，`Type = "string"`，`RoleRank = 4`；
- 根路径参数 `Type = "string"`，`RoleRank = 2`。

**完整键表见 [配置键表.csv](./配置键表.csv)。**

### 5.2 三个业务参数模型（字段语义）

| 模型 | 配置区 | 控制区要点 |
|------|--------|-----------|
| `PWBusinessParamModel` | `IsUse` + **18 组 CP 尺寸**（每组 基准/下限/上限） | 方向修正系数（正/负）、最大/最小限幅（正/负）、CP 管控开关（正/负）、OH 总宽防呆尺寸（正/负）、闭环开关（正/负）、调节频次 `AdjustFrequency`、**`AdjustInterval`（未使用，见 §10 #3）** |
| `OHBusinessParamModel` | `IsUse` + **18 组 CP 尺寸**（同结构，数值不同） | `IsSigleClosedLoop`、方向修正系数、前/后分段数 `CountOfFront`/`CountOfBack`（中段 = 90 − 前 − 后）、检测面 `IsFrontOfPosDetextion`、双面 OH `DoubleSideEnableOfPos`、`K`、限幅（正/负）、闭环开关（正/负）、`AdjustFrequency` |
| `MCBusinessParamModel` | `WatcherRootPath`（**MC 独有**） | 方向修正系数（正/负）、`T`（采集 T 个调一次）、`K`（触发调节阈值 50%~80%）、`D`（调整反馈偏差 0.05~0.3）、闭环开关（正/负） |

> `K` / `D` 的取值区间来自 XML 注释原文（"范围防呆"），**实际防呆校验是否在对话框实现需核对 XAML** → §10 #6。

### 5.3 工艺阈值数值（源码默认值，待确认）

**18 组 CP 尺寸的逐项数值（PW / OH 两套）见 [工艺阈值表.csv](./工艺阈值表.csv)**，此处不重复抄写（事实只写一处）。

需注意的量纲与陷阱：

1. **数值按万分之一毫米（0.1 µm）存整数**：如 `196000` 对应 **19.6 mm**；`190800` → 19.08 mm。UI 轴标签写 `宽度 (mm)`，但曲线绑定的是**原始整数**（`VM_PWOfJROHPage` 直接把 `ModuleWidthOfPos` 塞进 `SeriesOfPos`）→ ⏸ 量纲与显示单位是否一致待确认（§10 #8）。
2. PW 的 SA 顶部值**不是参数**，而是**运行时算出**：`TotalWidthOfMold_Center − PoleWidthOfNeg_Center − MoldToNegSABottom_Center`（代码注释举例 = 2.8 mm）。
3. OH 的 `K` 单位是「料线向大板移动 0.15 所需的纠偏值」，默认 `1000`；与 MC 的 `K`（比例阈值 0.5）**同名字段、完全不同语义**，勿混用。
4. PW 的 `MinAdjustmentLimit*` 默认 100、`MaxAdjustmentLimit*` 默认 400 属**死区内 + 限幅**双重作用，见 §6.1。

---

## 6. 三条闭环算法与数据流

> 计算过程模型统一保存 `CalculateTime` + `EndMessage`，并落 CSV（§7）。

### 6.1 PW 极片宽度闭环

**输入**：`正极A面CCD模宽数据`、`正极B面CCD模宽数据`、`正极CCD模宽数据`、`负极CCD模宽数据`、`正极飞行原点Y`、`负极飞行原点Y`、`虚拟码`。

**正极链路**：

1. `maxModuleWidthOfPos = max(A面, B面)`（取模宽较大者作基准）。
2. `TargetTotalWidthOfNeg = maxModuleWidthOfPos + NegToPosACTop_Center + NegToPosACBottom_Center`。
3. **工艺防呆**：要求 `maxModuleWidthOfPos ≤ TargetTotalWidthOfNeg` 且 `TargetTotalWidthOfNeg ≥ TotalWidthOfMold_LowerLimit − MoldToNegSABottom_Center − MoldToNegSATop_Center − OhWidthCheckValueNeg`；不满足则 `MessageBox.Show("正极模宽不符合工艺要求!")` 并 **`continue` 跳过本轮**。
4. `TargetTotalWidthOfPos = TargetTotalWidthOfNeg + CilOverNegTop_Center − NegToPosACBottom_Center`。
5. CP 管控（`CpControlEnablePos`）把目标裁剪进 `[PoleWidthOfPos_LowerLimit, PoleWidthOfPos_UpperLimit]` → 得 `w1`。
6. `DeltaW = w1 − 正极CCD模宽数据`。
7. **死区 + 限幅**：`|DeltaW| < MinAdjustmentLimitPos` → 偏移取 0；`|DeltaW| > MaxAdjustmentLimitPos` → 按符号截到 `MaxAdjustmentLimitPos`。
8. `adjustValueOfPos = (int)(正极飞行原点Y + AdjustCorrectionOfPos × 偏移)`。
9. 累积 `AdjustFrequency` 次后取**算术平均**得最终下发值；`ClosedLoopEnablePos` 为真 → 写 `正极纠偏值下发`，否则只记「正极推荐下发值」。

**负极链路**：同构，用 `CpControlEnableNeg` / `PoleWidthOfNeg_*` / `Min|MaxAdjustmentLimitNeg` / `AdjustCorrectionOfNeg`，写 `负极纠偏值下发`。
⚠️ 差异：负极缓存用 `Take(AdjustFrequency).Average()` 后 `RemoveRange(0, AdjustFrequency)`，正极用 `Average()` 后 `Clear()`。

### 6.2 OH 包覆 OH 闭环

**输入**：`虚拟码`、正极 A/B 面模宽、`顶部AC` `底部AC` `底部SA` `顶部SA`（各 90 点）、`正极/负极 × 前/中/后 段纠偏值`。

**分段规则**（`GetSectionRange`）：`前段 = [0, CountOfFront)`；`中段 = [CountOfFront, 90 − CountOfBack)`；`后段 = [90 − CountOfBack, 90)`。默认 `5 / 80 / 5`。

**双面模式（`IsSigleClosedLoop = false` 默认）**：

1. **A 值** = 检测面 − 非检测面（由 `IsFrontOfPosDetextion` 决定正极取 A 面还是 B 面）。规则：`A ≥ 0 → A = 0`；`A < 0 → 发送 A/2`（即正极正反面模宽补偿值）。
2. 各段内对 AC / SA 求均值，得 `e*`（正极侧）与 `f*`（负极侧）系列，再减去**中段均值**得到段间相对量：
   - 正极：`eAvg − eAvgOfMiddle`
   - 负极：`a/2 + fAvg − fAvgOfMiddle`
3. `ApplyDeadzoneAndClamp(value, 死区, 最大幅值)`：`|value| < 死区 → 0`；超上限取 `+max`，超下限取 `−max`。死区/幅值分别取 `MinLimitOfPos|Neg`、`MaxLimitOfPos|Neg`。
4. 结果按段写入 `IssueValueOf{Pos|Neg}{Front|Middle|Back}`，再 `PLCIssueActionAsync` 下发前/中/后三组。

**单边模式（`IsSigleClosedLoop = true`）**：`cmSigalCache` 缓存中间结果后统一结算（`CalculateSigalSide`）→ **整链路未现场验证**（§10 #9）。

**⚠️ 已发现缺陷（待办 #3 / #4）**：`PLCIssueActionAsync` 中

```csharp
var paramNameOfNeg = $"正极{sectionsName}纠偏下发值";      // ← 应为「负极」
if (!pm.ClosedLoopEnablePos) ...                          // ← 应为 ClosedLoopEnableNeg
```

即**负极下发实际写到正极点位名**，且负极闭环开关被忽略。另 `sectionsName` 的 Middle 分支文案为 `"中断"`，疑为 **`"中段"`** 笔误，会写错点位名与日志。

### 6.3 MC 多级联动纠偏闭环

**数据来源（关键）**：**不是 PLC**，而是 `MCBusinessParamModel.WatcherRootPath` 目录下的 CSV（默认 `D:\MESDATA\12_张力数据-TensionData-ข้อมูลแรงตึง`），`FileSystemWatcher(path, "*.csv")` + `IncludeSubdirectories = true` + 64KB 缓冲。
> **实测目录规则（2026-08-30 样例）**：`{WatcherRootPath}\{yyyy年MM月}\{yyyy年MM月dd日}\{膜卷号}.csv`；**数据行行首带一个反引号**，第 2 行为重复表头需跳过，61 列，时间格式 `yyyy-MM-dd HH:mm:ss.fff`。

**11 路通道**：正极 入料/蛇形4/蛇形3/蛇形2/蛇形1（5）+ 负极 入料/蛇形3/蛇形2/蛇形1（4）+ 上隔膜/下隔膜（2）。

**算法**（`ExcuteMCCalcualteAsync`，对每路独立执行）：

1. 累积 `T` 条采集数据后统一计算。
2. 只取**检测值与目标值偏离 < 2** 的点：`|detValue − tarValue| < 2`。
3. `TotalCount` = 合格点数；`PosCount` = 其中 `detValue > tarValue` 的数；`NegCount = Total − Pos`。
4. `PosCount / TotalCount > K` → `OffsetValue = −K × D`；`NegCount / TotalCount > K` → `OffsetValue = +K × D`。
5. `OffsetValue == 0` → 记「无需调机！」**不下发**；否则按**点位名**下发，失败记 Error。

**✅ 已实现（2026-09-21，代码已落盘，待办 #8）**：解析复用平台 `JROHCsvHelper.SearchCsv`，通道映射在 `MCBusinessService.ConvertToGatherModel`（按表头名、跳过重复表头行、清洗行首反引号）；监听为 `Created`/`Renamed` + 800ms 防抖 + 文件指纹去重 + 膜卷号命名校验 + 目录日期校验，并修复 `0/0` 整除（`TotalCount == 0` 直接返回）与 `dataCache` 跨卷累积。**列映射以现场样例实测为准（按表头名映射，非列序号）**：
   - **9 路**有完整「检测值 / 中值」列：正极 入卷（`正极入卷纠偏实时值` / `正极入卷纠偏中值`）+ 蛇形 1~4（`正极蛇形纠偏` / `正极蛇形N纠偏` 配 `正极蛇形N纠偏中值`）、负极 入卷 + 蛇形 1~3（同理）；
   - ⏸ **隔膜 2 路缺目标值列**：PPT 明确 D目标 = 「上/下隔膜外侧破标定值」，但样例 CSV 中该列**不存在**；故只采集 `上/下隔膜外侧破损实时`，闭环**不下发**该通道、曲线只绘检测值，**待现场补列**；
   - 样例中正负极「放卷纠偏」无中值列，但放卷是闭环**输出目标**（`正极/负极放卷纠偏设定中值`）而非输入通道，不影响本 11 路；
   - 膜卷号命名规则：`9 位数字 + 1 位字母 + 5 位数字`（共 15 位，如 `529126128L03465.csv`）；供应商先写纯数字临时文件（如 `3770.csv`），当前卷芯完成后更名。⏸ 需求方口述「日期 + 膜卷号固定长度」，样例无日期前缀，待资料补齐。

---

## 7. 落盘与文件输出

### 7.1 目录规则

```
{FileSaveRootPath}/{EBusinessType}/缓存数据_{BusinessName}/{yyyyMMdd}/{内容}.csv
```

| 项 | 值 |
|----|----|
| 默认 `FileSaveRootPath` | `D:\通用上位机\JROHDatas` |
| `EBusinessType` | `PoleWidth` / `CoatedOH` / `MultiCorrect`（枚举名） |
| `BusinessName` | 极片宽度闭环 / 包覆 OH 闭环 / 多级联动闭环 |
| 示例 | `D:\通用上位机\JROHDatas\PoleWidth\缓存数据_极片宽度闭环\20260918\xxx.csv` |

> MC 的 `WatcherRootPath`（张力输入）与 `FileSaveRootPath`（闭环输出）是**两个互不相关的目录**，勿混。

### 7.2 `JROHCsvHelper` 行为

- 路径含控制字符会被清洗（`char.IsControl` 过滤，针对 ASCII 0）；非 `.csv` 自动改扩展名。
- **表头自适应**：读首行比对，新数据出现新列 → **流式重写整个文件**补列（老行补空字段），否则直接追加。
- **100 MB 分卷**：写入前若超限，`File.Move` 为 `{name}.csv.cache{n}`（`n` 自增到不冲突）。
- 转义：含 `,` `"` `\n` `\r` 时用双引号包裹并把 `"` 转成 `""`；`FileShare.ReadWrite` 允许被占用时读。
- **并发**：调用方统一 `lock (JROHCsvHelper.WriteLocker)`；但 `WriteCSV` 内部的 `MoveLargerFileToCache` 与 `File.Delete/Move` **在锁外**（同一调用栈内仍受锁保护）。

### 7.3 输出列

三业务的 CSV 列由各自 `ConvertToDictionary` 决定，**列名清单见 [配置键表.csv](./配置键表.csv) 的"CSV 输出列"分节**（或直接读源码，详见规划 §0 索引）。通用首两列：`采集时间`（`yyyy-MM-dd HH:mm:ss.fff`）、`虚拟码 `（**注意：列名尾部有一处多余空格**，源自源码字面量）；尾列：`闭环结果信息`。

---

## 8. 平台接入清单（新增模块约定对照）

| 项 | JROH 现状 |
|----|-----------|
| 文件放置 | 主工程 `ViewModels/Pages/JROH`、`Views/Pages/JROH`（SDK 通配自动包含）；`Component` 侧控件与主题；`Tool` 侧 CSV 助手 |
| 命名一致 | ✅ 三个 `[Module]` 第 1 参数与 VM 类名一致 |
| 菜单显示名 | ✅ 第 2 参数：极片宽度闭环 / 包覆 OH 闭环 / 多级联动纠偏闭环 |
| DI | ✅ `ProjectServiceCollectionExtensions.AddJROHServices()`，在 `AddProjectServices` 中已调用 |
| `project_setting` | ✅ 读取时自动播种 |
| **enable SQL** | ❌ **未执行** → [enable_jroh_module.sql](./enable_jroh_module.sql) |
| 编译 | ⏸ 本次未执行（环境需 .NET 8 Windows + 完整依赖） |
| 完整重启 | 执行 SQL 后**必须完整退出应用再启动**，菜单才刷新 |

---

## 9. 实施阶段

| 阶段 | 内容 | 状态 |
|------|------|------|
| A | 代码骨架 + 三页 VM/View + 参数对话框 | ✅ 既有 |
| B | 三业务闭环 + PLC 下发 + CSV 落盘 | ✅ 既有（含 §6.2/§6.3 缺陷） |
| C | **文档建档 + 菜单启用 + 首次编译** | 🔄 本轮 |
| D | 现场 PLC 点位录入 + 联调（PW/OH 优先） | ❌ |
| E | MC 张力 CSV 格式对接与解析 | ✅ |
| F | 真值确认：阈值 / 量纲 / BelongToVM 收口 | ❌ |

---

## 10. 风险与待确认事项

| # | 事项 | 影响 | 处置 |
|---|------|------|------|
| 1 | `BelongToVM` 指向不存在的 `VM_JROHClosedLoopPage` | 配置归属语义不明，未来重构会丢配置 | **问负责人**；若合并页面则需数据迁移 SQL |
| 2 | **OH 负极下发写到正极点位名 + 用错开关** | **直接写错设备参数（P0）** | 先修 `PLCIssueActionAsync` 再联调 |
| 3 | `AdjustInterval` 声明未使用（`PWBusinessParamModel`） | 现场按对话框设置却不生效 | 确认语义后落地或删字段 |
| 4 | PLC 地址全部未知 | 无法真实联调，读值静默为 0 | 录入 [plc点位表.csv](./plc点位表.csv) |
| 5 | MC 未解析 CSV + `0/0` 整除 | MC 链路不可用 | ✅ 2026-09-21 二轮已实现解析 / 监听 / 入队 / 除零保护；⏸ 隔膜标定值列缺失（只画检测值、不下发）；⏸ 现场实跑未验证 |
| 6 | `K` / `D` 的"范围防呆"是否真实现 | 注释承诺 ≠ 行为 | 核对 `*BusinessParamDialog.xaml` 校验 |
| 7 | 服务内保留已注释的模拟数据代码 | 误读为有效逻辑 | 清理（待办 #12） |
| 8 | 尺寸量纲：整数疑似 0.1 µm，曲线标签写 mm | 界面读数可能差 10000 倍 | 现场确认后统一 |
| 9 | OH 单边闭环（`IsSigleClosedLoop`）未验证 | 切换即未知行为 | 现场各测一轮 |
| 10 | **OH 的包覆信号轮询循环未启动** | `ExecuteAsync` 只监听了极片信号，采集永不入队 → OH 进页无数据 | 见进度存档 §4 第 2 点；待办 #9 |
| 11 | PW 工艺防呆不通过时 `continue` 跳过整轮 | 负极也不算、不落盘 | 确认是否为期望 |
| 12 | `[Module]` 三页 `Order = 0`、`Category` 各自 VM 名 | 菜单排序/层级可能不符合预期 | 进页确认后调整 |
