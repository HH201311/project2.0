# ZZSIX 版本变更记录

> **用途**：按日期倒序记录「做了什么、为什么、落在哪」；**当前待办与状态以 [ZZSIX进度存档.md](./ZZSIX进度存档.md) 为准**  
> **格式**：`> **YYYY-MM-DD 变更（标题）**` + 背景 / 做法 / 落点；规格变更写清「见规划 §x」  
> 新会话默认 **不必** 拖本文；需要追溯历史时再按进度存档索引打开。

---

> **2026-09-14 变更（分析：旧仓 LrzWork 迁移盘点）**  
> 1. **背景**：需将 `sunwoda1.0.0.2` 分支 `LrzWork`（.NET 4.8）迁入新 SEVB 的 ZZSIX。  
> 2. **做法**：相对 `master` 梳理工序与依赖；明确禁止整文件拷贝；BoxGluing 已在新仓独立存在故交叉引用；待确认首迁工序。  
> 3. **落点**：[开发资料/LrzWork迁移分析.md](./开发资料/LrzWork迁移分析.md)；进度存档待办 #1。

> **2026-09-14 变更（新建：项目基础结构 demo）**  
> 1. **背景**：按 SWD 工作流新建代号 ZZSIX，先落地可编译、菜单可进的空页骨架与三文档。  
> 2. **做法**：新建 `VM_ZZSIXPage` / `ZZSIXPage`；`[Module]` 菜单名「ZZSIX」；写入并执行 `enable_zzsix_module.sql`（库 `sunwoda_demo`）；创建 `开发资料/` 投放区。  
> 3. **落点**：`ViewModels/Pages/ZZSIX/VM_ZZSIXPage.cs`；`Views/Pages/ZZSIX/ZZSIXPage.xaml(.cs)`；[enable_zzsix_module.sql](./enable_zzsix_module.sql)；本目录三文档；[开发资料/](./开发资料/)。
