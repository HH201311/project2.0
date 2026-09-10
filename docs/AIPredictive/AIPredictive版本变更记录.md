# AIPredictive 版本变更记录

> **用途**：按日期倒序记录「做了什么、为什么、落在哪」；**当前待办与状态以 [AIPredictive进度存档.md](./AIPredictive进度存档.md) 为准**  
> **格式**：`> **YYYY-MM-DD 变更（标题）**` + 背景 / 做法 / 落点；规格变更写清「见规划 §x」  
> 新会话默认 **不必** 拖本文；需要追溯历史时再按进度存档索引打开。

---

> **2026-09-10 变更（点位原始资料 → CSV：伺服 920 行 / 气缸 1674 行）**  
> 1. **背景**：开发人员反映 `开发资料/伺服.txt`、`开发资料/气缸.txt` 为制表符分隔的纯文本，程序读取与质检不便，需要先转成 CSV 格式。  
> 2. **做法**（**纯格式转换，零内容改动、零臆造**）：  
>    - 逐行按制表符切分 → 按 RFC4180 生成 CSV（字段含 `,`/`"`/换行时加引号并把 `"` 转义为 `""`；本次数据实际未触发转义）；  
>    - 统一输出口径：**UTF-8 with BOM + CRLF**，首行列名，行序与原始 txt 完全一致；  
>    - 不改字段顺序、不补空字段、不做单位换算、不合并两表、不推断设备编码与工位语义。  
>    - 数据体检：参数地址与参数名称在各自表内**均无重复**；空列（伺服 `设备编码`；气缸 `线体`、`设备编码`）在原始表中即为空，**原样保持空值**。  
> 3. **落点**：  
>    - 数据：[AIPredictive点位表_伺服.csv](./AIPredictive点位表_伺服.csv)（10 列 × 920 行）、[AIPredictive点位表_气缸.csv](./AIPredictive点位表_气缸.csv)（12 列 × 1674 行）  
>    - 文档：规划新增 [§2.2.1 CSV 数据字典](./AIPredictive项目开发规划.md#221-csv-数据字典程序读取口径)（列定义 / 非空情况 / 地址形态 / 引用口径），§0、§2.1、§2.3、§1.3、§1.4#3、§4 阶段 B、§7 索引同步更新  
>    - 原始文件 `开发资料/伺服.txt`、`开发资料/气缸.txt` **保留不删**（`开发资料/README.md` 登记去向）  
> 4. **验证**：用 CSV 解析器回读，与源 txt **逐字段比对 0 处不一致**（表头逐列一致、行数 920/1674 一致）；字节数核对一致（伺服 122235 = 122232 + 3 BOM；气缸 242310）。  
> 5. **未做 / 待确认**：气缸表 `线体`、`工序` 之外的语义映射、阈值、采样频率与设备编码 **原始表未给，标 ⏸ 待需求方确认**；加密 xlsx 仍未录入；两张表列数不同（10 vs 12），**不能直接纵向拼接**。  

> **2026-09-10 变更（菜单启用范围收敛 + 启动实测通过）**  
> 1. **背景**：启动后左侧菜单未见新页面；经核对，数据库记录与配置均正常，问题在于运行的是**另一份副本的旧 exe**（`E:\WorkSpace\SWD\BoxGluing\sunwoda2.0.0.1\...\SUNWODA_SEVB.exe`，2026-09-08 构建，不含本项目页面）。同时按要求收敛启用范围，避免菜单里混入其它项目模块。  
> 2. **做法**：  
>    - 数据库核对：`workspace_project` 中 `VM_AIPredictivePage` 存在且 `is_enabled = 1`；`global_setting.AppStartMainWindow = MainWindow`。  
>    - 将不需要启用的项目/测试模块置 0：`VM_BoxGluingMonitoringPage`、`VM_CTOLAutoDetectionPage`、`VM_xxxTESTPage`；保留平台基础页 `VM_SettingsPage`、`VM_UserCenter` 与本项目。  
>    - 启动实测：运行本副本新构建的 exe（`SUNWODA_SEVB/bin/Debug/net8.0-windows/SUNWODA_SEVB.exe`），观察日志确认注册与导航。  
> 3. **落点**：`sunwoda_demo.workspace_project`（启用集合见进度存档「数据库启用现状」）；[AIPredictive进度存档.md](./AIPredictive进度存档.md) 待办 #1 勾选完成。  
> 4. **验证（日志摘录）**：`注册业务模块: VM_AIPredictivePage (AI预测分析)`、`业务模块注册完成，总业务模块数: 3`、`VM_AIPredictivePage.OnInitialize → AIPredictive 页面初始化完成`、`成功导航到业务模块: VM_AIPredictivePage`。  
> 5. **注意**：切换项目开发时需把对应模块置 1（如回到 BoxGluing 需将 `VM_BoxGluingMonitoringPage` 置回 1）；数据库改动后必须完整退出应用再启动。  

> **2026-09-10 变更（项目初始化：骨架 + 三文档 + 资料归档 + 菜单启用）**  
> 1. **背景**：新建项目 AIPredictive，先按规程 §二 产出「能编译、菜单可进空页」的基础结构 demo，并把现场原始资料投放进 `开发资料/`，为后续需求确认与资料录入做准备。业务定位（预测目标 / 数据来源 / 输出形式）尚未确认，故本轮**只做骨架，不写业务逻辑**。  
> 2. **做法**：  
>    - 按 [新增业务模块上手指南](../新增业务模块上手指南.md) 建 VM/View 三件套，三个名字对齐：VM 类 `VM_AIPredictivePage` ↔ `[Module("VM_AIPredictivePage", "AI预测分析")]` ↔ `x:Class="SUNWODA_SEVB.Views.Pages.AIPredictive.AIPredictivePage"`；VM 注入 `ILoggerService<T>`，`OnInitialize` 设标题与占位状态文案。  
>    - 对照 `docs/BoxGluing/` 建三文档 + `README.md` + `开发资料/`（含目录说明）。  
>    - 编写 `enable_aipredictive_module.sql`（`INSERT ... WHERE NOT EXISTS` + `UPDATE is_enabled = 1`，并确保 `AppStartMainWindow = MainWindow`），对目标库 **直接执行**（库名以 `appsettings.json` 为准，本机与代码默认连接串均为 `sunwoda_demo`）。  
>    - 现场 3 份原始资料（伺服点位表、气缸点位表、德阳 C03 叠片段切叠一体机加密 Excel）**原样复制** 到 `docs/AIPredictive/开发资料/`；本轮不做整理录入（避免在定位未确认时臆造）。  
> 3. **落点**：  
>    - 代码：`SUNWODA_SEVB/ViewModels/Pages/AIPredictive/VM_AIPredictivePage.cs`、`SUNWODA_SEVB/Views/Pages/AIPredictive/AIPredictivePage.xaml(.cs)`  
>    - 文档：[AIPredictive进度存档.md](./AIPredictive进度存档.md)、[AIPredictive项目开发规划.md](./AIPredictive项目开发规划.md)、[README.md](./README.md)、[开发资料/README.md](./开发资料/README.md)  
>    - 数据：[enable_aipredictive_module.sql](./enable_aipredictive_module.sql)（已在 `sunwoda_demo` 执行，`VM_AIPredictivePage.is_enabled = 1`）  
>    - 资料：[开发资料/](./开发资料/)（3 份原始文件，状态 `pending`）  
> 4. **验证**：本地 `dotnet build SUNWODA_SEVB.sln -c Debug` **0 error**（28 个既有警告，非本项目引入）；`AIPredictivePage.g.cs` / `.baml` 已生成，主程序输出已刷新；数据库核对 `VM_AIPredictivePage = 1`、`AppStartMainWindow = MainWindow`。  
>    构建方式：本机 CLI 环境对 MSBuild 多节点并行敏感（并行时静默失败、报「0 error」），改用 `-m:1` 单节点还原 + 编译即通过（无外网时以 `%USERPROFILE%\.nuget\packages` 作 NuGet 源）；细节见进度存档「本机构建提示」。  
> 5. **未做**：业务功能、Service/Core 接口分层、`project_setting` 持久化、开发资料录入 → 见进度存档待办。  
> 6. **上线前置动作**：需**完整退出 SEVB 应用后重新启动**，左侧菜单才会出现「AI预测分析」。

> **接手前（资料归档）**  
> - 项目代号 `AIPredictive` 由工作流页确认后落盘；资料投放目录 `docs/AIPredictive/开发资料/` 依据 [Agent协作开发规程](../Agent协作开发规程.md) §2.2。
