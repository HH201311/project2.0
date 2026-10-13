# JRCW 项目开发规划

> **文档版本**：2026-10-09  
> **定位**：冷层详规（范围 / 架构 / 数据流 / 参数 / 风险）；**当前状态与待办以 [JRCW进度存档.md](./JRCW进度存档.md) 为准**  
> **用途**：新会话默认**不必**读本文；按进度存档 §0 索引点读对应章节  
> **事实来源**：全部条目取自仓库代码；**未知项一律标 ⏸，不臆造**。

---

## 0. 文档索引（事实只写一处）

| 要查什么 | 章节 |
|----------|------|
| 业务范围 / 本期不做 / 验收边界 | [§1](#1-项目定位与目标) |
| 现场资料对接现状 | [§2](#2-对接方与资料) |
| 分层 / 服务清单 / DI / 生命周期 | [§3](#3-总体架构设计) |
| PLC 设备 / 点位名 / 触发信号 | [§4](#4-plc-与数据采集设计) |
| 参数键 / 参数模型 / 默认值 | [§5](#5-参数配置与持久化) |
| 两条业务的数据流与算法 | [§6](#6-两条业务的数据流与算法) |
| CSV 输出与目录 | [§7](#7-落盘与文件输出) |
| 新增模块约定对照 | [§8](#8-平台接入清单) |
| 风险与待确认 | [§10](#10-风险与待确认事项) |

---

## 1. 项目定位与目标

### 1.1 项目背景

JRCW = **卷绕极耳错位闭环**，位于 `SUNWODA SEVB`（`develop`）平台，归属「单项目多业务」工厂模式：一个项目代号下挂多条差异化业务，用 `EBusinessType` + 业务服务工厂/管理器区分。

同一模式在同仓已有先例：CCTB、JROH（见 [JROH项目开发规划.md](../JROH/JROH项目开发规划.md)）。

### 1.2 两条业务范围

| 业务（`EBusinessType`） | 服务实现 | 中文名 | 范围（据代码） |
|------------------------|----------|--------|----------------|
| `BusinessType1` | `YWC05BusinessService` | 义乌C05 | PLC 信号触发 → 读错位量 / 卷针变径位置 / 压花气压 / CCD 边距 → 算法判定 → 下发卷针纠偏（J1/J2/J3）与压花值 |
| `BusinessType2` | `DYC04BusinessService` | 德阳C04 | PLC 信号触发 → 读极耳层数 / 错位量 / 变径位置 / CCD 边距 → Python 算法（`Judge` / `Press`）→ 下发 |
| `BusinessType3` | ❌ 无实现 | 德阳C01 | 仅保留中文名；`BusinessServiceBase` 的加载/保存分支已注释（§10 #3） |

### 1.3 本期不做（明确边界）

- MES / Web 上传、报警中心接入
- `BusinessType3`（德阳C01）在需求明确前的实现
- 历史回放 / 查询页、曲线持久化
- 平台架构文档以外的重构

### 1.4 验收口径

- 页面可进、两条业务可启停（`StartBusinessAsync` / `StopBusinessAsync`）
- 点位地址按现场表录入后，采集数值正确、无丢点
- 算法输出与下发值可追踪（日志 + CSV 落盘）
- 参数修改落 `project_setting` 并重启后生效

---

## 2. 对接方与资料

| 资料 | 状态 | 去处 |
|------|------|------|
| PLC 点位表（地址 / 类型 / 设备） | ⏸ **缺** | [开发资料/](./开发资料/) → 整理为 `plc点位表.csv` |
| PLC 设备信息（IP / 端口 / 协议） | ⏸ **缺** | 同上 → `plc设备信息.csv` |
| 工艺阈值原始表 | ⏸ **缺** | 同上 → `工艺阈值表.csv` |
| Python 算法说明 / 脚本 | ⏸ **缺** | 同上 → 规划 §6 补全 |
| 现场 CCD 边距量纲与单位 | ⏸ **缺** | 影响 `PerfectBattery` 系列字段解释 |

> 投放规则：原始资料 → `开发资料/`；由工作流「开始录入」或按 [Agent协作开发规程.md](../Agent协作开发规程.md) §2.2 整理进本规划与 CSV。

---

## 3. 总体架构设计

### 3.1 分层

| 层 | 位置 | 职责 |
|----|------|------|
| 页面 | `ViewModels/Pages/JRCW/`、`Views/Pages/JRCW/` | 绑定、参数表、日志展示；进出页启停业务 |
| 业务服务 | `Services/Project/JRCW/Business/` | 采集 → 算法 → 闭环计算 → 下发 → 落盘 |
| 工厂 / 管理器 | 同上 | 按 `EBusinessType` 注册与调度各业务 |
| 模型 / 枚举 / 接口 | `Core/{Models,Enumerations,Interfaces}/Project/JRCW/` | 参数、采集、计算、算法入参出参 |
| 工具 | `Tool/Helper/JRCWHelper/`、`Tool/Extension/ProjectSettingExtension.cs` | CSV 读写、参数持久化 |
| 控件 / 样式 | `Component/Themes/Project/JRCW/`、`Core/Converters/Project/JRCW/` | 项目专用 UI |

**原则**：View / code-behind 不直访 PLC；重逻辑集中在 `Services/Project`；服务通过构造函数注入（见 `Services/ProjectServiceCollectionExtensions.cs`）。

### 3.2 生命周期与多态

- `BusinessServiceManager` 构造时 `RegisterService<YWC05BusinessService>()`、`RegisterService<DYC04BusinessService>()`；
- 页面 `OnInitializeAsync` → `StartBusinessAsync(BusinessType)`；`OnCleanupAsync` → `StopBusinessAsync(BusinessType)`；
- 业务基类 `BusinessServiceBase<T>` 提供 `StartAsync` / `StopAsync` / `RestartAsync` 与 `IBusinessService` 契约；
- 中文名由 `GetBusinessName(EBusinessType)` 决定：`BusinessType1 → 义乌C05`、`BusinessType2 → 德阳C04`、`BusinessType3 → 德阳C01`。

### 3.3 菜单与配置归属

- 页面 `[Module("VM_JRCWClosedLoopPage", "卷绕极耳错位闭环", Order = 0, Category = "VM_JRCWClosedLoopPage")]`；
- `JRCWCore.BelongToVM = "VM_JRCWClosedLoopPage"`：`project_setting.BelongToVM` 取值，**与菜单无关**；
- 业务类型本身也持久化在 `project_setting`（默认 `BusinessType1`）。

### 3.4 与同平台其他项目的异同

| 维度 | JRCW | JROH（对照） |
|------|------|--------------|
| 业务数量 | 3 个枚举 / 2 个实现 | 3 个枚举 / 3 个实现 |
| 数据来源 | PLC 触发 + CCD + **Python 算法** | PLC（PW/OH）与 **CSV 文件监听**（MC） |
| 页面 | 单页 `VM_JRCWClosedLoopPage`（业务可切换） | 每业务一页 |
| CSV 工具 | `JRCWCsvHelper.SearchCsv(路径, 列, 值, ...)` 按列值筛选 | `JROHCsvHelper.SearchCsv(路径, 列...)` 及无参版本 |

---

## 4. PLC 与数据采集设计

### 4.1 设备

| 项 | 值 | 状态 |
|----|----|------|
| PLC 名称 | `卷绕极耳错位闭环PLC`（`JRCWCore.PLCName`） | ⏸ 仓库内无种子/迁移，需现场确认 |
| IP / 端口 / 协议 | 未知 | ⏸ 待录入 |
| 读取方式 | `GetPLCAddressValue("<点位名>")` / `GetPLCAddressModel("<点位名>")` | 按**名称**解析地址 |
| 写入方式 | `WritePLCAsync("<点位名>", value)` 或 `WritePLCAsync(address, type, value)` | 见 §4.3 |

### 4.2 触发机制

代码中可见的使能/信号类点位（名称取自代码字面量，地址待录入）：

| 点位名 | 用途 | 出现位置 |
|--------|------|----------|
| `PLC闭环开启信号` | 闭环总开关 | 两条业务 |
| `闭环心跳信号` | 心跳 | `YWC05BusinessService` |
| `P辊压花功能` | 压花使能 | `DYC04BusinessService` |
| `P辊闭环(软件模式)` | 压花闭环模式选择 | `DYC04BusinessService` |
| `数采触发极耳层数` | 采集触发（层数） | `DYC04BusinessService` |

> ⏸ 具体触发时序（电平/上升沿、轮询周期）未在文档化说明中给出，需现场确认后补本节。

### 4.3 点位清单（按名称；**地址、类型全部待录入**）

**读点位（代码字面量，可直接核对）**：

| 点位名 | 字段落点 | 业务 |
|--------|----------|------|
| `卷针处虚拟码` | `ZJData_XNM` | 两条 |
| `卷针号` | `ZJData_JZH` | 两条 |
| `正极错位量` | `MVOf` / `MVOfP` | 两条 |
| `负极错位量` | `MVOfN` | 两条 |
| `卷针1变径位置` / `卷针2变径位置` | `SV_Pos1` / `SV_Pos2` | 两条 |
| `卷针3变径位置` | `SV_Pos3` | 德阳C04 |
| `实时压花气压` | `StampingAirPressure` | 两条 |
| `扫码处虚拟码` | `CCD_ScanCode` | 两条 |
| `Barcode码` | `CCD_Barcode` | 两条 |
| `入料位置-正极` / `入料位置-负极` | `FeedInletPositionOf*` / `InPOf*` | 两条 |
| `正极极耳远边距` / `正极极耳近边距` | `FarDistanceOfPositiveBatteryTab` / `NearDistanceOfPositiveBatteryTab` | 两条 |
| `负极极耳远边距` / `负极极耳近边距` | `FarDistanceOfNegativeBatteryTab` / `NearDistanceOfNegativeBatteryTab` | 两条 |

**写点位**：代码以 `GetPLCAddressModel(parameterName)` 取出地址对象后 `WritePLCAsync(address, type, value)` 下发，**点位名由变量传入**（未在代码中以字面量出现）→ ⏸ **具体下发点名待现场/算法说明确认**。已识别的下发语义：

| 下发语义 | 变量名（代码） | 备注 |
|----------|----------------|------|
| 卷针纠偏 J1 / J2 / J3 | `j1` / `j2` / `j3`（含 `j1Cache` / `j2Cache`） | `YWC05BusinessService` §8「调机下发」 |
| 压花值 | `press_j` | 两条业务 |
| 错位异常警告信号 | `parameterName`（变量） | `YWC05BusinessService` |
| 心跳 / 闭环开启回写 | `parameterName`（变量） | 值多为 `0/1` |
| 极耳层数回写 | `(float)paramModel.TabCount` | `DYC04BusinessService` |

### 4.4 采集节奏与并发

⏸ 轮询/采集周期未在文档化说明中给出；代码中未见显式周期常量，需现场确认后补本节。

---

## 5. 参数配置与持久化

### 5.1 存储机制

- 载体：`project_setting` 表（`IProjectSettingRepository`），按 `Name` + `BelongToVM` 定位；
- 归属：`JRCWCore.BelongToVM = "VM_JRCWClosedLoopPage"`；
- 扩展方法位于 `Tool/Extension/ProjectSettingExtension.cs` 的 `#region JRCW项目(卷绕极耳错位闭环)参数配置相关扩展方法`（约 1098 行起）。

| 配置键（`Name`） | 存储 | 默认值（源码） |
|------------------|------|----------------|
| 业务类型 | string | `BusinessType1` |
| 文件保存根目录 | string | `D:\通用上位机\JRCWDatas` |
| 算法名列表 | string | `MIS_JUDGE_C04_325,PRESS_C04_325` |
| `Business1Param` | JSON（string） | 见 §5.3 |
| `Business2Param` | JSON（string） | 见 §5.3 |

### 5.2 两个业务参数模型（字段语义）

**`Business1ParamModel`（义乌C05）字段**：`Nb`、`Na`、`T4`、`U_Limit`、`K`、`T1`、`T2`、`T3`、`J_d`、`P_Switch`、`P_t`、`P_j_d`。

**`Business2ParamModel`（德阳C04）字段**：

- 基础：`NeedleType`（枚举）、`TabCount`、`T4`、`U_Limit`、`K`、`Nb`、`Na`、`T1`、`T2`、`T3`、`J_d`
- 分段：`S_Nb1`、`S_Nbn`、`S_Na1`、`S_Nan`、`Cb1`、`Cbn`、`Ca1`、`Can`
- 完美电芯（3 组）：`PerfectBattery1~3` → `BatteryCode`、`P_θb_List`、`P_θa_List`、`P_θb1`、`P_θbn`、`P_θa1`、`P_θan`、`B120`、`B121`
- 压花：`CCDErrT`、`PressT`、`PressUp`、`PressDown`、`PressStep`、`PressK`、`PressJT`

> ⏸ 字段的**工艺语义**（量纲、取值范围、与 PLC 点位对应关系）未在代码注释中完整说明，需现场工艺确认后补全本节。

### 5.3 源码默认值（待现场确认）

> ⚠️ 以下数值**抄自代码字段默认值**，**未经现场确认**，禁止据此判定工艺对错。

| 字段 | 源码默认值 | 所属 |
|------|------------|------|
| `CCDErrT` | `10` | 德阳C04 |
| `PressStep` | `0.005f` | 德阳C04 |
| `PressK` | `-2.25f` | 德阳C04 |
| `PressJT` | `1`（byte） | 德阳C04 |
| `PressT` / `PressUp` / `PressDown` | 未在声明处赋值（默认 0）⏸ | 德阳C04 |
| `Business1ParamModel` 各字段 | 未在声明处赋值（默认 0）⏸ | 义乌C05 |

---

## 6. 两条业务的数据流与算法

### 6.1 义乌C05（`BusinessType1` / `YWC05BusinessService`）

1. 校验使能：`PLC闭环开启信号` → 读 `闭环心跳信号`（回写心跳）；
2. 采集：卷针处虚拟码 / 卷针号 / 正负极错位量 / 卷针 1~2 变径位置 / 实时压花气压 → 组装 PLC 数据模型；
3. CCD：扫码处虚拟码 / Barcode / 入料位置 / 正负极极耳远近边距 → 组装 CCD 数据模型；
4. 算法：判定与误差补偿（`ArithmeticModels/` 下 `MisJudge`、`ErrorCompensation`、`Disclass` 等入参出参模型）；
5. 下发：§8「调机下发」段——按卷针号（1/2）下发 `j1` / `j2` / `j3` 与 `press_j`，含 `j1Cache` / `j2Cache` 缓存分支；异常时仅记录日志并保留缓存值下发；
6. 落盘：`JRCWCsvHelper.WriteCSV` 写当日目录。

> ⏸ 算法具体公式、判据阈值、下发条件未在代码外文档化，需以现场工艺文件为准。

### 6.2 德阳C04（`BusinessType2` / `DYC04BusinessService`）

1. `InitializePythonCodeAsync()`：初始化 Python 代码（`PythonConfigHelper.AddAction((ExcutePython_Judge, args))` 等）；
2. 采集：卷针处虚拟码 / 卷针号 / 正负极错位量 / `数采触发极耳层数` / 卷针 1~3 变径位置 / 实时压花气压；CCD：扫码虚拟码 / Barcode / 正负极极耳远近边距；
3. 算法：`PyModule.FromString("Judge", pyCode)` → `po.mis_judge()` 取判定结果；另 `ExcutePython_Press` 走压花分支；
4. 下发：`WritePLCAsync(address, type, value)`（含 `(float)0` 复位、`j1`/`j2`/`press_j` 等）；
5. 落盘：同 §7。

> ⏸ 源码中另有 `//dynamic pm = PyModule.Import("DY_C04_325AH_0");` 注释，说明算法脚本曾以模块名加载，现改为内嵌 `pyCode`；实际脚本内容与版本需现场确认。

---

## 7. 落盘与文件输出

| 项 | 值 |
|----|----|
| 根目录配置键 | 「文件保存根目录」，默认 `D:\通用上位机\JRCWDatas` |
| 工具类 | `Tool/Helper/JRCWHelper/JRCWCsvHelper.cs` |
| 主要方法 | `WriteCSV(fullPath, DataTable)`、`DicToTable(params Dictionary<string,string>[])`、`SearchCsv(...)` |
| 写入锁 | `JRCWCsvHelper.WriteLocker`（静态） |
| 子目录规则 | ⏸ 未在文档化说明中给出，需按代码 `fullPath` 拼装规则确认后补 |
| 输出列 | ⏸ 由各业务的字典构造决定，待落盘实测后补 |

---

## 8. 平台接入清单

| 项 | JRCW 现状 |
|----|-----------|
| 文件放置 | 主工程 `ViewModels/Pages/JRCW`、`Views/Pages/JRCW`；`Component` 控件与主题；`Tool` CSV 与参数扩展 |
| 命名一致 | ✅ `[Module]` 第 1 参数与 VM 类名一致（`VM_JRCWClosedLoopPage`） |
| 菜单显示名 | ✅ 第 2 参数「卷绕极耳错位闭环」 |
| DI | ✅ `AddJRCWServices()`（⚠️ `DYC04BusinessService` 重复注册，见 §10 #4） |
| `project_setting` | ✅ 读取时自动播种 |
| **enable SQL** | ❌ 仓库内无 `enable_jrcw_module.sql`，启用方式待确认 |
| 编译 | ⏸ 本次仅新增文档，未编译 |
| 完整重启 | 若执行启用 SQL，**必须完整退出应用再启动**菜单才刷新 |

---

## 9. 实施阶段

| 阶段 | 内容 | 状态 |
|------|------|------|
| A | 代码骨架 + 页面 VM/View + 参数扩展 | ✅ 既有 |
| B | 两条业务闭环 + PLC 下发 + CSV 落盘 | ✅ 既有（未现场验证） |
| C | **文档建档 + 开发资料目录** | 🔄 本轮 |
| D | 现场 PLC 点位录入 + 联调（义乌C05 优先） | ❌ |
| E | Python 环境与德阳C04 算法联调 | ❌ |
| F | 真值确认：参数默认值 / 阈值 / 量纲 | ❌ |

---

## 10. 风险与待确认事项

| # | 事项 | 影响 | 处置 |
|---|------|------|------|
| 1 | `项目架构说明.md` §7.2 写 `DYC01BusinessService`，实际代码为 `DYC04BusinessService` | 新人误判业务数量与命名 | 更正平台文档（待办 #8） |
| 2 | **PLC 地址全部未知** | 无法真实联调，读值静默为 0 | 录入 `plc点位表.csv`（待办 #1） |
| 3 | `BusinessType3`（德阳C01）无实现、参数分支被注释 | 枚举与 UI 可能暴露空业务 | 落地或移除（待办 #4） |
| 4 | `AddJRCWServices()` 重复注册 `DYC04BusinessService` | DI 解析异常时难排查、语义不清 | 去重（待办 #5） |
| 5 | 参数默认值未经现场确认 | 现场按对话框设置可能不符合工艺 | 现场确认（待办 #6） |
| 6 | 德阳C04 依赖 Python 环境与内嵌脚本 | 环境未就绪则该业务不可用 | 现场验证（待办 #7） |
| 7 | 文件名含空格（`StringToVisibilityConverter .cs`、`Business1CCDDataModel .cs`） | 脚本/工具按名匹配易错 | 改名（待办 #9） |
| 8 | 无 `enable_jrcw_module.sql` | 菜单可能不显示 | 确认启用方式（待办 #3） |
| 9 | 参数/点位的工艺语义未文档化 | 交接成本高、易误改 | 现场资料投放后补 §5.2 / §6 |
| 10 | 历史决策无记录 | 设计意图不可考 | 后续变更按规程留痕 |
