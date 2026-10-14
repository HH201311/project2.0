# YWSIX 版本变更记录

> **用途**：按日期倒序记录「做了什么、为什么、落在哪」；**当前待办与状态以 [YWSIX进度存档.md](./YWSIX进度存档.md) 为准**  
> **格式**：`> **YYYY-MM-DD 变更（标题）**` + 背景 / 做法 / 落点；规格变更写清「见规划 §x」  
> 新会话默认 **不必** 拖本文；需要追溯历史时再按进度存档索引打开。

---

> **2026-10-12 变更（修复「参数设置」点击无反应）**  
> 1. **背景**：管理员登录后点击无弹窗。  
> 2. **原因**：Unloaded 退订弹窗事件后，缓存页再进入不触发 DataContextChanged，RequestOpenPinSettings 无订阅者；另 IsEnabled+CanExecute 双绑易空转。  
> 3. **做法**：Loaded 重新订阅；Command 始终可点，无权限弹提示；去掉 IsEnabled 双绑。  
> 4. **落点**：`YWSIXPage.xaml(.cs)` / `VM_YWSIXPage`；`E:\共享文档\YWSIX_参数设置点击修复_替换文件_20261012_1714.zip`。

> **2026-10-12 变更（按钮改名「参数设置」+ 布局重排）**  
> 1. **背景**：PIN 入口需改名以容纳未来其它参数；原 5 键 UniformGrid 右下空一格难看。  
> 2. **做法**：文案改为「参数设置」；布局改为 功能设置|参数设置 / NG|离线 / 统计清零通栏。  
> 3. **落点**：`YWSIXPage.xaml`；窗标题；替换包 `E:\共享文档\YWSIX_参数设置按钮布局_替换文件_*`。

> **2026-10-12 变更（PIN 独立设置按钮 + 管理员权限 + 运行中热更新）**  
> 1. **背景**：PIN 阈值需单独入口；权限对齐 NCLCCT（侧边栏 role≥Admin）；检测运行中可改并立即生效。  
> 2. **做法**：新增「PIN参数设置」窗/按钮；功能设置去掉 PIN 区；`CanEditPinSettings` 订阅 `UserStateChangedEvent`；保存写库并 `RefreshRuntimeConfig`。  
> 3. **落点**：`Pack6PinSettingsWindow`；`YWSIXPage`；`VM_YWSIXPage`；`IYWSIXPack6VisionEngine.RefreshRuntimeConfig`；替换包 `E:\共享文档\YWSIX_PIN独立设置管理员权限_替换文件_20261012_1656.zip`。

> **2026-10-12 变更（PIN：中心坐标叠图 + 左上角测量值）**  
> 1. **背景**：视觉 DLL 已输出 4 点中心与 5 项指标；现场要求画到图上并在左上角显示测量值。  
> 2. **做法**：`DetectPinSkew` 用 L1/L2/S1/S2 十字与连线；左上角 OK/NG 下附 5 项测量与阈值；全 0 判未检测到。  
> 3. **落点**：`Pack6Ywx01VisionEngine` / `Pack6ResultOverlayHelper` / `Pack6DetectionModels`；替换包 `E:\共享文档\YWSIX_PIN叠图测量值_替换文件_20261012_1649.zip`（SEVB+Core）。

> **2026-10-12 变更（六窗预览 DecodePixelWidth 160→640）**  
> 1. **背景**：现场反馈预览太糊。  
> 2. **做法**：`Pack6CameraSlotVm` 解码宽改为 640（双击全屏仍用原路径）。  
> 3. **落点**：`Pack6CameraSlotVm.cs`；`E:\共享文档\YWSIX_预览清晰度640_替换文件_20261012_1628.zip`。

> **2026-10-12 变更（去掉主页顶部「当前配置」长条）**  
> 1. **背景**：用户不要该提示条。  
> 2. **做法**：删除 `YWSIXPage` 配置摘要 Border 及 code-behind 刷新逻辑；Rebuild 后打替换包。  
> 3. **落点**：`YWSIXPage.xaml` / `YWSIXPage.xaml.cs`；`E:\共享文档\YWSIX_去掉当前配置条_替换文件_20261012_1536.zip`（含 `SUNWODA_SEVB.dll`）。

> **2026-10-12 变更（Debug 整目录全量包 → E:\共享文档）**  
> 1. **背景**：视觉三件套已进运行目录；用户要求打全量包投产。  
> 2. **做法**：按 NJSIX 口径打包 `bin\Debug\net8.0-windows` 整目录；附部署说明与 `enable_ywsix_module.sql`。  
> 3. **落点**：`E:\共享文档\YWSIX_PACK六面检_Debug全量_20261012_1457.zip`（约 1.39GB）；同名前缀 `_部署说明.txt` / `_enable_ywsix_module.sql`。  
> 4. **验证**：zip 内含 `Dlls\YWSIX\YWX01Pack.dll` / `YOLO26Det.dll` / `YW_X01_TOP.onnx`；现场需完整重启 SEVB。

> **2026-10-12 变更（第二步：接入 YWX01Pack；顶面仅检 1～4 且数量必须相等）**  
> 1. **背景**：用户确认上盖类别数量 **必须相等**；顶面 **只检测张序 1～4**（5=拼接跳过、6=不检）；版本默认 M00；PIN 阈值按文档示例进设置可改。  
> 2. **做法**：实现 `Pack6Ywx01VisionEngine`（白标签四码 / 铭牌 / 上盖 YOLO / PIN）；`Pack6TopCoverExpectations` 固化 1～4 期望数；DI 改注册真引擎；`UseSimulation` 默认 false；功能设置增加 PIN/上盖模型/版本；`Dlls\YWSIX`（YWX01Pack / YOLO26Det / YW_X01_TOP.onnx）引用并 CopyToOutput。  
> 3. **落点**：`Pack6Ywx01VisionEngine.cs`；`Pack6TopCoverExpectations.cs`；`Pack6Config(.cs)` / `Pack6ConfigService.cs`；`Pack6SettingsWindow.xaml`；`VM_YWSIXPage.cs`；`SUNWODA_SEVB.csproj`；`YWSIXServiceCollectionExtensions.cs`；进度存档 / 本文。  
> 4. **验证**：`dotnet build SUNWODA_SEVB.csproj` → **0 error**；输出目录含 `Dlls\YWSIX\*`。  
> 5. **未做**：PLC 点位 CSV；真机联调。

> **2026-10-12 变更（第一步：照抄 NJSIX 界面/管线，6 相机 SaveImage，暂不调 DLL）**  
> 1. **背景**：义乌六面检与 NJSIX 界面接近；现场给出 `SaveImage` 目录结构与 `YWX01Pack` 算法文档；约定本步先映射相机/图片并移植管线，**DLL 调用留第二步**。  
> 2. **做法**：按现场扫描确认 6 相机（顶面6/前侧7/后侧6/左侧3/右侧4/底部5=31）；从 NJSIX 移植 Core/Services/VM/View 至 YWSIX 命名空间；收图改为单根 `\\10.130.105.114\SaveImage\SaveImage` + `OK-相机-张序-时间-SN.jpg`；预览改为 3×2；视觉注册 `Pack6SimulationVisionEngine`（默认 `UseSimulation=true`）；Core.csproj 白名单加入 YWSIX 接口。  
> 3. **落点**：`Core/Models|Interfaces/Project/YWSIX/*`；`Services/Project/YWSIX/*`；`ViewModels/Pages/YWSIX/*`；`Views/Pages/YWSIX/*`；`App.xaml.cs`；`SUNWODA_SEVB.Core.csproj`；[开发资料/文件夹结构.txt](./开发资料/文件夹结构.txt)；进度存档 / 本文。  
> 4. **验证**：`dotnet build SUNWODA_SEVB.sln` → **0 error**。  
> 5. **未做**：未引用/调用 `YWX01Pack`；OCR/上盖/PIN 机位路由未定；PLC/阈值 CSV 未建。

> **2026-10-12 变更（新建：项目基础结构 demo）**  
> 1. **背景**：按 SWD 工作流新建代号 YWSIX，先落地「可编译、菜单可进空页」的基础骨架与三份文档，业务范围待现场资料录入后再定。  
> 2. **做法**：按 [新增业务模块上手指南](../新增业务模块上手指南.md) 建 `VM_YWSIXPage` / `YWSIXPage`，`[Module]` 第二参数（左侧菜单显示名）取 `YWSIX`；创建 `docs/YWSIX/` 三文档并保留 `开发资料/` 投放区；写入 [enable_ywsix_module.sql](./enable_ywsix_module.sql)（库 `sunwoda_demo`，`vm_class_name = VM_YWSIXPage`，`is_enabled = 1`，`AppStartMainWindow = MainWindow`）并对目标库执行。  
> 3. **落点**：`ViewModels/Pages/YWSIX/VM_YWSIXPage.cs`；`Views/Pages/YWSIX/YWSIXPage.xaml(.cs)`；[enable_ywsix_module.sql](./enable_ywsix_module.sql)；[YWSIX进度存档.md](./YWSIX进度存档.md)、[YWSIX版本变更记录.md](./YWSIX版本变更记录.md)、[YWSIX项目开发规划.md](./YWSIX项目开发规划.md)；[开发资料/](./开发资料/)。  
> 4. **验证**：`dotnet build SUNWODA_SEVB.sln` → 0 error（31 个警告全部为既有代码）；SQL 已对目标库实际执行并核对 `VM_YWSIXPage | is_enabled = 1`、`AppStartMainWindow | MainWindow`。  
> 5. **未做**：未写任何 PLC / 工艺 / 算法 / MES 逻辑（资料未到，禁止臆造）；规划中未知项标 ⏸。
