# JRCW 版本变更记录

> **用途**：按日期倒序记录「做了什么、为什么、落在哪」；**当前待办与状态以 [JRCW进度存档.md](./JRCW进度存档.md) 为准**  
> **格式**：`> **YYYY-MM-DD 变更（标题）**` + 背景 / 做法 / 落点；规格变更写清「见规划 §x」  
> 新会话默认 **不必** 拖本文；需要追溯历史时再按进度存档索引打开。

---

> **2026-10-09 变更（补齐 JRCW 标准三文档 + 开发资料目录）**  
> 1. **背景**：`docs/JRCW/` 此前不存在，而 JRCW 代码已成型 —— 新会话无法定位入口，点位名与参数默认值真假混在源码里；平台 `项目架构说明.md` §7.2 与代码也不一致（写 `DYC01BusinessService`，实为 `DYC04BusinessService`）。  
> 2. **做法**：  
>    - 按 `docs/BoxGluing/` 样板结构落地三文档：进度存档作热层（状态 / 待办 / 代码入口 / 索引）、版本变更倒序、规划作冷层（范围 / 架构 / 数据流 / 参数 / 风险）；  
>    - 事实一律取自代码：按既定阅读顺序先读平台规程与架构说明，再只读本项目 `Core`/`Services`/`ViewModels`/`Views`/`Tool` 的入口文件，**未做框架探查式扫仓**，也**未通读**两个业务服务全量实现（仅提取点位名、算法入口、参数模型字段）；  
>    - 参数数值抄自 `Business1ParamModel` / `Business2ParamModel` 字段默认值并明确标注「未经现场确认」；PLC 地址**未知即留空并标 ⏸**，不臆造；  
>    - 把发现的问题（DI 重复注册、`BusinessType3` 空档、架构文档不一致、文件名含空格、无 enable SQL、无 CSV）记入规划 §10 与进度存档待办，**本次不改业务代码**；  
>    - 新建 `开发资料/`（含 README 与状态标记 `pending:no-materials`），供现场原始资料投放。  
> 3. **落点**：[JRCW进度存档.md](./JRCW进度存档.md)、[JRCW版本变更记录.md](./JRCW版本变更记录.md)、[JRCW项目开发规划.md](./JRCW项目开发规划.md)、[README.md](./README.md)、[开发资料/](./开发资料/)。  
> 4. **验证**：仅新增文档与目录，未改动任何 `.cs` / `.xaml`，无需编译。  
> 5. **未做**：PLC 点位录入、参数现场确认、Python 算法验证、菜单启用、`BusinessType3` 决策。

> **接手前（既有代码归档）**  
> - `EBusinessType`（BusinessType1/2/3）+ `IBusinessService` + `BusinessServiceBase<T>` + 工厂 + 管理器一次建成，模式对齐 CCTB/JROH 的「单项目多业务」工厂模式。  
> - 两条业务服务（`YWC05BusinessService` 义乌C05、`DYC04BusinessService` 德阳C04）各自完成「PLC 信号 / CCD 采集 → 算法 → 闭环计算 → CSV 落盘」。  
> - `[Module]` 页面 `VM_JRCWClosedLoopPage`（实时监控参数表 + 设备信息 + 日志）、消息框对话框与项目主题样式。  
> - `ProjectSettingExtension` JRCW 参数区（业务类型 / 落盘根目录 / 算法名 / `Business1Param` / `Business2Param`）；`JRCWCsvHelper` 提供 CSV 写入与按列值查询。  
> - Core 侧算法模型：`MisJudge`、`ErrorCompensation`、`Disclass`、`Press`、`Judge`、`PyResult`（入参/出参成对）。  
> - ⚠️ 历史决策过程无记录（无提交说明、无会议纪要），本条仅记录**代码现存事实**，不代表设计意图已被确认。
