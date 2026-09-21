# NCLCCT 版本变更记录

> **用途**：按日期倒序记录「做了什么、为什么、落在哪」；**当前待办与状态以 [NCLCCT进度存档.md](./NCLCCT进度存档.md) 为准**  
> **格式**：`> **YYYY-MM-DD 变更（标题）**` + 背景 / 做法 / 落点；规格变更写清「见规划 §x」  
> 新会话默认 **不必** 拖本文；需要追溯历史时再按进度存档索引打开。

---

> **2026-09-20 变更（华为 Gap 换算、管控判定与结果图底栏）**  
> 1. **背景**：算法返回像素间隙；现场公式为「像素数×像素精度 − 分组常量」。原先把 0.12 做成可配「像素精度」减量，与现场用语不符；结果图底栏大号字曾重叠/留白过大。  
> 2. **做法**：  
>    - 界面「Gap标准」仅保留可配 **像素精度 (mm/px)** 与管控上下限；去掉 0.12 设置项。  
>    - 换算：Gap1/2/3/10/11 = px×精度−0.12；Gap4~9 = px×精度−0.24。  
>    - OK/NG 仅管控 Gap1~6、Gap10（显示红绿与整图 NG）；Gap7~9、11 只展示不判 NG。  
>    - 结果图：内容缩放 + 底部 1/4 黑遮罩；`Times New Roman-Bold-56`；左栏管控说明、右栏测量值每列 3 行；画布可按字号加宽；按反馈收紧左右栏与标签–数值间距。  
> 3. **落点**：`VM_NCLCCTPage.cs`（`ConvertPrintfPxToMm` / `EvaluateHuaweiGapsOk` / `GenerateHuaweiResultImage` / `DrawHuaweiBottomValueBand`）；`NCLCCTPage.xaml` Gap标准菜单。  
> 4. **验证**：现场出图核对数值与红绿；底栏无重叠、留白可接受。

> **2026-09-20 变更（监控只收 R_origin；去掉尺寸门禁；Halcon 原生库路径）**  
> 1. **背景**：监控目录杂图多，只需 `*-R_origin.*`；CTNCLX 遗留尺寸门禁误拦华为 1440×1000；`Dlls\halcon.dll` 不在默认搜索路径导致 ModelAction 失败。  
> 2. **做法**：Watcher/`HandleFile`/导入统一校验 `R_origin`；删除宽高白名单；启动与检测前 `EnsureHalconNativeRuntimePath`（`Dlls` 入 PATH/`AddDllDirectory`，license 指向 `Dlls\license.dat`）。  
> 3. **落点**：`VM_NCLCCTPage.cs`。  
> 4. **验证**：`*-R_origin.png` 可入队；算法可跑通（依赖本机 Halcon 授权）。

> **2026-09-20 变更（修复 CUDA Provider 126：部署 CUDA12+cuDNN9 运行库）**  
> 1. **背景**：`IsRunCuda=true` 时 ORT 加载 `onnxruntime_providers_cuda.dll` 失败（Win32 126），模型句柄为空。根因是本机仅有 NVIDIA 驱动、无 CUDA Toolkit/cuDNN，Provider 依赖 DLL 找不到。  
> 2. **做法**：部署 `Dlls\CUDA12`（CUDA12+cuDNN9 运行库）；启动前把该目录加入 PATH/`AddDllDirectory`；保持 GPU 初始化，不回退 CPU。探针 `ModelInit(CUDA=true)` 句柄非空。  
> 3. **落点**：`SUNWODA_SEVB.Core/Dlls/CUDA12/`；`VM_NCLCCTPage.EnsureCudaRuntimePath`；`SUNWODA_SEVB.csproj`。  
> 4. **验证**：编译 0 error；探针 `handleNull=False`。

> **2026-09-20 变更（接入南昌华为视觉 NChwSwdCT，界面绘点）**  
> 1. **背景**：视觉资料目录交付 `NJCT.dll`（含 `NJCT.NChwSwdCT`）、`CF.dll`、`390000[hw].onnx`。截图文档中的 `long` 句柄/`AlignPoints` 签名与**实 DLL**不一致；按实 DLL 10 参 `ModelAction` 接入。  
> 2. **做法**：DLL/模型独立部署到 `Dlls\NCLCCT\` 与 `model\NCLCCT\model_hw.onnx`，不覆盖 CTNCLX 用的 `Dlls\NJCT.dll`。实现加载、`ModelInit`、`ModelAction`；从 `AllPointRegion` 提取点并 `GenerateHuaweiResultImage` 画到结果图，绑定 `LastProcessedImage`。  
> 3. **落点**：`VM_NCLCCTPage.cs`；`SUNWODA_SEVB.Core/Dlls/NCLCCT/`；`SUNWODA_SEVB/model/NCLCCT/`；`SUNWODA_SEVB.csproj`。  
> 4. **未做**：截图文档另一套接口待视觉确认后再迁。  
> 5. **验证**：Debug 编译 0 error。

> **2026-09-20 变更（按 CTNCLX 迁移南昌华为页面，视觉 DLL 留 TODO）**  
> 1. **背景**：NCLCCT 确认为南昌华为离线 CT。界面与非视觉流程与小米南昌离线 CT（CTNCLX）一致，视觉 DLL 由后续单独开发。菜单显示名继续用 `NCLCCT`。  
> 2. **做法**：把 CTNCLX 的检测页、监控路径对话框和检测模型迁到 NCLCCT 命名空间；页面类名保持已启用的 `VM_NCLCCTPage` / `NCLCCTPage`，配置归属改为 `VM_NCLCCTPage`。对话框类名改为 `NCLCCTMonitorPathSelectDialog`，避免与 CTNCLX 同名类在弹窗查找时冲突。视觉侧删除小米 DLL 名、公钥、下载地址和 `NJCT.NJSwdCT` 调用，改为 `TryLoadVisionAssembly` / `TryInitVisionModel` / `TryInvokeVisionAlgorithm` 三个空实现；推理参数数组与结果字段保留。未接通时结果图回退为原图，日志写「视觉DLL未实现」。  
> 3. **落点**：`VM_NCLCCTPage.cs`；`NCLCCTPage.xaml`；`ViewModels/Dialogs/NCLCCT/`；`Views/Dialogs/NCLCCT/`；`SUNWODA_SEVB.Core/Models/Project/NCLCCT/`；[NCLCCT项目开发规划.md](./NCLCCT项目开发规划.md) §1、§6、§7、§13；[项目架构说明.md](../项目架构说明.md) §5。  
> 4. **未做**：视觉 DLL 加载、模型初始化、推理。不改 CTNCLX。  
> 5. **验证**：`SUNWODA_SEVB` Debug 编译 0 error（.NET 8 SDK 8.0.424 的 MSBuild）。

> **2026-09-16 变更（项目初始化：骨架页 + 三文档 + 菜单启用）**  
> 1. **背景**：NCLCCT 为新立项项目，需先落「可编译 + 菜单可进空页」的基础结构，后续业务范围等现场资料投放后再定。  
> 2. **做法**：按 [新增业务模块上手指南](../新增业务模块上手指南.md) 建 VM/View 三件套并以 `[Module]` 注册，菜单显示名暂用代号 `NCLCCT`；建 `docs/NCLCCT/` 三文档与 `开发资料/`；写 `enable_nclcct_module.sql` 并对 `sunwoda_demo` 直接执行（`is_enabled = 1`），同时核对 `AppStartMainWindow = MainWindow`。本机 NuGet 源不可达，改用 .NET 8 SDK 的 MSBuild + 本地包缓存离线还原后编译通过（0 error）。  
> 3. **落点**：`SUNWODA_SEVB/ViewModels/Pages/NCLCCT/VM_NCLCCTPage.cs`；`SUNWODA_SEVB/Views/Pages/NCLCCT/NCLCCTPage.xaml(.cs)`；[NCLCCT进度存档.md](./NCLCCT进度存档.md)；[NCLCCT版本变更记录.md](./NCLCCT版本变更记录.md)；[NCLCCT项目开发规划.md](./NCLCCT项目开发规划.md)；[enable_nclcct_module.sql](./enable_nclcct_module.sql)；[开发资料/](./开发资料/)。  
> 4. **未做 / 待确认**：业务范围、点位、阈值、存储与算法全部 ⏸（见规划 §1、§5、§10）；菜单需用户完整重启后人工验收。  
> 5. **验证**：编译 0 error；SQL 执行结果 `VM_NCLCCTPage = 1`、`AppStartMainWindow = MainWindow`。
