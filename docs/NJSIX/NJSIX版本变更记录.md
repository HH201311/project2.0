# NJSIX 版本变更记录

> **用途**：按日期倒序记录「做了什么、为什么、落在哪」；**当前待办与状态以 [NJSIX进度存档.md](./NJSIX进度存档.md) 为准**  
> **格式**：`> **YYYY-MM-DD 变更（标题）**` + 背景 / 做法 / 落点；规格变更写清「见规划 §x」  
> 新会话默认 **不必** 拖本文；需要追溯历史时再按进度存档索引打开。

---

> **2026-09-29 变更（结果根目录 D:→E: + 替换包）**  
> 1. **背景**：现场结果/日志需落在 E 盘。  
> 2. **做法**：默认 `LocalPathPre=E:\六面检上位机软件`；加载时若仍为 `D:\六面检上位机软件`（或旧 `D:\NJSIX_Pack6`）则写回 E:；打替换 zip。  
> 3. **落点**：`Pack6Config`、`Pack6ConfigService`、`VM_NJSIXPage`、`Pack6UiStatsStore`；`E:\共享文档\NJSIX_PACK六面检_替换文件_*`。  

---

> **2026-09-29 变更（白标签期望版本 M00→M06 + 替换包）**  
> 1. **背景**：现场白标签数模版本为 M06。  
> 2. **做法**：默认 `ExpectedWhiteLabelVersion=M06`；加载时若库内仍为 M00 则写回 M06；打替换 zip。  
> 3. **落点**：`Pack6Config`、`Pack6ConfigService`、`VM_NJSIXPage`、设置窗；`E:\共享文档\NJSIX_PACK六面检_替换文件_*`。  

---

> **2026-09-29 变更（叠字微调：M06 下移、铭牌紫字上移）**  
> 1. **背景**：同图双检仍有橙字/紫字与邻框重叠。  
> 2. **做法**：`Pack6OverlayBox.LabelOffsetY`；版本号 +14、铭牌紫字 -22。  
> 3. **落点**：`Pack6DetectionModels`、`Pack6ResultOverlayHelper`、`Pack6NjtwVisionEngine`。  

---

> **2026-09-29 变更（替换包：自动启动/紫字右侧/关档位）**  
> 1. **背景**：现场需覆盖近期改动。  
> 2. **做法**：Debug 打替换 zip（SEVB + Core）。  
> 3. **落点**：`E:\共享文档\NJSIX_PACK六面检_替换文件_20260929_1045.zip`。  

---

> **2026-09-29 变更（隐藏产品类型下拉 + 关闭档位前缀校验）**  
> 1. **背景**：南京暂无产品档位/信号核对需求。  
> 2. **做法**：主界面「产品类型」`Visibility=Collapsed`；`Pack6Config.EnforceProductCodeProfile=false`；`ValidatePrefixRule` 跳过前十位比对（仍校长度 24）；摘要改显示 Z/T/S。  
> 3. **落点**：`Pack6Config`、`Pack6BarcodeRuleValidator`、`NJSIXPage.xaml(.cs)`、`VM_NJSIXPage`、pack6配置键.csv。  

---

> **2026-09-29 变更（进入页面自动启动检测）**  
> 1. **背景**：现场开机后易忘记点「启动」，漏收图。  
> 2. **做法**：`OnInitialize` / `OnNavigatedTo` 在配置加载后调用 `TryAutoStartAsync`（已在跑则跳过）。  
> 3. **落点**：`VM_NJSIXPage.cs`。  

---

> **2026-09-29 变更（同图双检：铭牌紫字改框右侧）**  
> 1. **背景**：白标签+铭牌同图时，铭牌紫字叠在框下方会与白标签产品码青字重叠。  
> 2. **做法**：同图双检路径铭牌 `Pack6OverlayLabelPlacement` 由 Below 改为 Right。  
> 3. **落点**：`Pack6NjtwVisionEngine.DetectCombinedWhiteLabelAndNameplate`。  

---

> **2026-09-29 变更（Debug 整目录全量包 + 只开 NJ SQL）**  
> 1. **背景**：现场机可能残留其它框架模块；用户要求全量=Debug 输出整目录，非精简子集。  
> 2. **做法**：打包 `SUNWODA_SEVB\bin\Debug\net8.0-windows` 全部文件；附 `enable_njsix_only.sql`（除设置/用户中心/NJSIX 外 `is_enabled=0`）。  
> 3. **落点**：`E:\共享文档\NJSIX_PACK六面检_Debug全量_20260929_0856.zip`（约 1.2GB）；[enable_njsix_only.sql](./enable_njsix_only.sql)。  

---

> **2026-09-29 变更（纠正国内两驱/四驱前缀：0A↔09）**  
> 1. **背景**：现场口径 0A=国内两驱、09=国内四驱，代码原先写反。  
> 2. **做法**：`Pack6ProductCodeCatalog` 对调 `12TPB0ALAB` / `12TPB09LAB`。  
> 3. **落点**：`Pack6BarcodeFieldCatalog.cs`；进度/版本记录。  

---

> **2026-09-28 变更（南京清理：默认/菜单/无报警/只开 NJ/CSV/去残余）**  
> 1. **背景**：用户确认：默认可改；CSV 仅文档无代码依赖可改写；真机联调不急；删 ZZ 残余；合并离线重复钮；删样张；南京暂无报警；本机只开 NJ；菜单改 NJSIX。  
> 2. **做法**：菜单 `[Module(..., "NJSIX")]`；顶栏去 PLC；`Pack6PlcAlarmService` 空实现；去 NG/缺张弹窗与窗体文件；离线窗合并为 2 钮；删 `ResolveQueueFallback`；配置加载迁移 Y:/Z:OK原图→Z/T/S、车间 ZZ→NJ；删 `开发资料/样张`；改写 pack6 两 CSV + PLC 说明 + enable SQL（关 `VM_ZZSIXPage`）；规划/进度对齐南京。  
> 3. **落点**：`VM_NJSIXPage`、`NJSIXPage.*`、`Pack6PlcAlarmService`、`Pack6ConfigService`、`Pack6IngressService`、`Pack6OfflineTestWindow`；`docs/NJSIX/*`；[enable_njsix_module.sql](./enable_njsix_module.sql)。  

---

> **2026-09-28 变更（NJX13Pack 换 net8.0 版并正式切换引用）**  
> 1. **背景**：视觉替换同路径 DLL；新包目标框架为 `.NETCoreApp,Version=v8.0`，与 SEVB net8 兼容。  
> 2. **做法**：同步 `docs/.../dll` → `Core/Dlls/NJX13Pack.dll`；`Pack6NjtwVisionEngine` 改为 `using NJX13Pack`；Debug 编译 0 error。  
> 3. **落点**：`Core/Dlls/NJX13Pack.dll`；`Pack6NjtwVisionEngine.cs`；[NJSIX进度存档.md](./NJSIX进度存档.md)。  

---

> **2026-09-28 变更（第二步：南京 10 相机 + Z/T/S 收图 + OCR 路由；NJX13Pack ⏸ net10）**  
> 1. **背景**：南京 10 窗共 22 张 OK；盘符 Z 上盖 / T 底部 / S 四周；Cam=相机号、Step=张序；只收 OK；OCR：四周2·Step1 同图双检、四周6·Step4 单独白标签。视觉提供 `NJX13Pack.dll` + 接口文档，NuGet 已齐（无需手工模型）。  
> 2. **做法**：改 `Pack6CameraCatalog`/`Pack6FileNameParser`/`Pack6IngressService`/`Pack6NjtwVisionEngine`（同图双检）/`Pack6WorkClassifier`/`Pack6BarcodeRuleValidator`/`Pack6ProductStatus`；UI 5×2；设置三路径；DLL 拷入 `Core/Dlls/NJX13Pack.dll`。对照文档：接口与 ZZTWPack 同构，但 **NJX13Pack 目标框架为 net10.0**，SEVB 为 net8 → CS1705；**临时** Vision 仍 `using ZZTWPack`，csproj 中 NJX13Pack Reference 注释保留，待 net8 版 DLL。  
> 3. **落点**：上述 NJSIX Models/Services/Views/VM；`SUNWODA_SEVB.csproj` / `Core.csproj`；[开发资料/dll/](./开发资料/dll/)；[NJSIX进度存档.md](./NJSIX进度存档.md)。  
> 3.1 **编译**：Debug **0 error**（临时 ZZTWPack）。输出已含 `Dlls\NJX13Pack.dll` 文件。  
> 4. **⏸ 需你提供**：请视觉用 **.NET 8** 重编 `NJX13Pack.dll`（字符串应含 `.NETCoreApp,Version=v8.0`，对比旧 `ZZTWPack`）。拿到后取消 csproj 注释并改 `using NJX13Pack` 即可。  
> 5. **现场**：完整重启 SEVB；设置确认上盖=Z:\、底部=T:\、四周=S:\。

---

> **2026-09-28 变更（第一步：按 ZZSIX 全量对照移植，命名改 NJSIX）**  
> 1. **背景**：NJ 相对 ZZ 仅细微差异，不重写；先做成与 ZZSIX 功能一致的可运行基线，第二步再改厂区差异。目标：拿到与 ZZ 生产同级的收图/管线/三码 OCR/PLC 报警/存图/设置/离线测试能力。  
> 2. **做法**：整树复制 Models/Interfaces/Services/ViewModels/Views；`ZZSIX`→`NJSIX`、`IZZSIX`→`INJSIX`、`VM_ZZSIXPage`→`VM_NJSIXPage`、`Pack6ZztwVisionEngine`→`Pack6NjtwVisionEngine`；**保留** `using ZZTWPack` 与 `Core/Dlls/ZZTWPack.dll`（+ Halcon 运行库，SEVB/Core csproj CopyToOutput）；`App.xaml.cs` 注册 `AddNJSIXPack6Services()`；Core.csproj 白名单 Include 9 个 NJ 接口；对照落盘 CSV/PLC SQL/样张/规划；执行 `enable_njsix_module.sql` + `seed_njsix_plc.sql`。  
> 3. **落点**：`SUNWODA_SEVB.Core/Models|Interfaces/Project/NJSIX/`；`SUNWODA_SEVB/Services|ViewModels|Views/.../NJSIX/`；`NJSIXServiceCollectionExtensions.cs`；`App.xaml.cs`；`SUNWODA_SEVB.Core.csproj`（接口 Include + ZZTWPack Copy）；[pack6配置键.csv](./pack6配置键.csv)；[pack6队列需求.csv](./pack6队列需求.csv)；[PLC配置说明.md](./PLC配置说明.md)；[seed_njsix_plc.sql](./seed_njsix_plc.sql)；[enable_njsix_module.sql](./enable_njsix_module.sql)；[NJSIX进度存档.md](./NJSIX进度存档.md)；[NJSIX项目开发规划.md](./NJSIX项目开发规划.md)。  
> 3.1 **编译验证**：SDK 8.0.424 MSBuild Debug Build → **0 个错误**；文件数与 ZZSIX 对齐（Models 11 / Interfaces 9 / Services 13 / VM 2 / Views 14）。  
> 3.2 **数据库验证**：`VM_NJSIXPage is_enabled=1`；`AppStartMainWindow=MainWindow`；`plc_config.六面检PLC` 已写入（IP/点位第一步与 ZZ 相同，remark 标 NJSIX）。  
> 3.3 **DLL**：输出目录已有 `Dlls\ZZTWPack.dll` + `halcon*.dll` / license；与 ZZ 共用同一算法包（未另造 NJ 版 DLL）。  
> 4. **未做（第二步）**：南京专属点位/盘符/阈值；停用本机 ZZ 菜单；缺失的「视觉资料说明书」原文（本仓 ZZSIX 侧亦无该目录落盘）。  
> 5. **现场操作**：**请完整退出 SEVB 后重新启动**；左侧进「PACK六面检」（`VM_NJSIXPage`）。若与 ZZ 模块同时启用会出现两个同名菜单，本机可只留 NJ（进度存档待办 #3）。

---

> **2026-09-28 变更（项目初始化：基础结构 Demo + 三文档 + 模块启用）**  
> 1. **背景**：新建项目代号 `NJSIX`，业务范围与现场资料尚未确定；先按规程 §二落地「能编译、菜单可进空页」的骨架，避免在无资料时臆造工艺与点位。  
> 2. **做法**：按 [新增业务模块上手指南](../新增业务模块上手指南.md) 建 VM/View 三件套，`[Module("VM_NJSIXPage", "NJSIX")]`（第二个参数为左侧菜单显示名，暂用代号）；VM 只做界面绑定 + 生命周期日志，注入 `ILoggerService<VM_NJSIXPage>`，业务逻辑留待资料录入后放 `Services/Project/NJSIX/`；建 `docs/NJSIX/` 三文档与 `开发资料/` 投放区（对照 `docs/BoxGluing/` 样板）；`enable_njsix_module.sql` 写入项目 docs 并对库 `sunwoda_demo` 执行（`VM_NJSIXPage` → `is_enabled = 1`，`AppStartMainWindow = MainWindow`）；本地编译 0 error。  
> 3. **落点**：`SUNWODA_SEVB/ViewModels/Pages/NJSIX/VM_NJSIXPage.cs`；`SUNWODA_SEVB/Views/Pages/NJSIX/NJSIXPage.xaml(.cs)`；[enable_njsix_module.sql](./enable_njsix_module.sql)；[NJSIX进度存档.md](./NJSIX进度存档.md)；[NJSIX项目开发规划.md](./NJSIX项目开发规划.md)；`开发资料/`。  
> 3.1 **编译验证**：SDK 8.0.424 MSBuild 全量 Rebuild → **0 个错误**（120 个警告全部来自既有模块；NJSIX 新增文件无警告）。类型配对已按 `ModuleManager.FindViewForViewModel`（`VM_` 去前缀 + `.ViewModels`→`.Views`）核对一致。  
> 3.2 **数据库验证**：`SELECT vm_class_name, is_enabled FROM workspace_project WHERE vm_class_name='VM_NJSIXPage'` → `VM_NJSIXPage = 1`；`global_setting.AppStartMainWindow = MainWindow`。  
> 4. **未做**：任何业务功能（PLC / 算法 / CSV / MES 均未接）；资料录入后由规划 §0 与进度存档待办推进。  
> 5. **现场操作**：**请完整退出 SEVB 应用后重新启动**，再在左侧菜单进入「NJSIX」确认页面正常显示，日志应出现 `注册业务模块: VM_NJSIXPage (NJSIX)`。
