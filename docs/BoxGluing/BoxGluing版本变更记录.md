# BoxGluing 版本变更记录

> **用途**：按日期倒序记录「做了什么、为什么、落在哪」；**当前待办与状态以 [BoxGluing进度存档.md](./BoxGluing进度存档.md) 为准**  
> **格式**：`> **YYYY-MM-DD 变更（标题）**` + 背景 / 做法 / 落点；规格变更写清「见规划 §x」  
> 新会话默认 **不必** 拖本文；需要追溯历史时再按进度存档索引打开。

---

> **2026-09-09 变更（现场配置：Monitor 并入 appsettings，禁用坏掉的分文件）**  
> 1. **背景**：`optional:true` 的 `remote_file_monitor_config.json` 存在但解析失败仍会致命；现场连环报 Failed to load configuration。  
> 2. **做法**：加固 `EncryptedJsonConfiguration`；`App` 改为对该文件走同一加载器；现场急救包把 Monitor 写入明文 `appsettings`，并要求删除损坏的 `remote_file_monitor_config.json`。  
> 3. **落点**：`EncryptedJsonConfiguration.cs`；`App.xaml.cs`；`BoxGluing_StartFix.zip`。

> **2026-09-09 变更（现场配置加载：明文 appsettings + Monitor 分文件）**  
> 1. **背景**：现场替换依赖后连环缺 DLL；随后 `appsettings` 加载失败 / `Monitor:Targets` 为空。  
> 2. **做法**：早期曾分文件；后因现场分文件损坏改为并入 appsettings（见上条）。  
> 3. **落点**：现场包迭代。

> **2026-09-08 变更（现场纠正：入口压力/伺服扭矩地址对调）**  
> 1. **背景**：联调发现压力读数为异常小负数；对照 PLC「上传MES」点位表，原文档把入口压力与伺服扭矩地址写反。  
> 2. **做法**：入口压力改为 D1600～D1607，伺服扭矩改为 D1608～D1615；液位/出胶/反馈不变。见规划 §2.3.2、§4.3。编译时补齐 Core 对 BoxGluing 接口的显式 Include，并将主工程 ScottPlot.WPF 对齐至 5.0.56。配置加载保留现场有效连接串，避免被代码默认值覆盖。  
> 3. **落点**：[plc点位表.csv](./plc点位表.csv)；`BoxGluingDefaultConfig.cs`；`ConfigurationService.cs`；`SUNWODA_SEVB.Core.csproj`；`SUNWODA_SEVB.csproj`；规划 §2.3 / §4.3；进度存档。

> **2026-09-08 变更（约定开发资料目录 + 工作流录入）**  
> 1. **背景**：原始现场资料与整理后 CSV 混放，不便新手投放与 Agent 录入。  
> 2. **做法**：新增 `开发资料/`；规程 §2.2；样板标记已录入。  
> 3. **落点**：[开发资料/](./开发资料/)；[Agent协作开发规程.md](../Agent协作开发规程.md)；本规划 §0。

> **2026-08-24 变更（进度存档改为索引热层；版本变更独立成文）**  
> 1. **背景**：避免新会话整份读入规划；历史变更与当前状态分离。  
> 2. **做法**：进度存档只保留状态 / 待办 / 入口 / 索引；本文件专记版本变更。  
> 3. **落点**：本文件；[BoxGluing进度存档.md](./BoxGluing进度存档.md)；[README.md](./README.md)。

> **2026-08-18 变更（P0：采集 + 三出胶曲线 + 分文件 CSV）**  
> 1. **背景**：替代旧单体 ViewModel，在 SEVB 平台实现监测与落盘。  
> 2. **做法**：PerLane D1620～D1622；80ms 块读；三图 ScottPlot；CSV 按日出胶分文件；模拟 5～8s。  
> 3. **落点**：`BoxGluingAcquisitionService` 等（见进度存档 §2）；规格见规划 §3～§6。  
> 4. **未做**：阈值线、算法、NG 回写 → 见进度存档待办。

> **2026-08-18 变更（三工位 + 液位公用）**  
> 1. **做法**：出胶 N ↔ 工位 N；液位仅 D1616/D1617，三路公用；工位 4 预留。  
> 2. **落点**：`BoxGluingChannelConfig`；点位真相见 [plc点位表.csv](./plc点位表.csv)、规划 §2.3。

> **接手前（资料归档）**  
> - 旧项目要点提取（禁止整文件拷贝）；`plc.1` / 工艺图 → CSV；`enable_boxgluing_module.sql`
