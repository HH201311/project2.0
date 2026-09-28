# NCLCCT 版本变更记录

> **用途**：按日期倒序记录「做了什么、为什么、落在哪」；**当前待办与状态以 [NCLCCT进度存档.md](./NCLCCT进度存档.md) 为准**  
> **格式**：`> **YYYY-MM-DD 变更（标题）**` + 背景 / 做法 / 落点；规格变更写清「见规划 §x」  
> 新会话默认 **不必** 拖本文；需要追溯历史时再按进度存档索引打开。

---

> **2026-09-28 变更（离线跑完清掉顶部进度提示）**  
> 1. **背景**：离线批次结束后不应残留「当前进度：N / N」。  
> 2. **做法**：`done >= total` 时隐藏进度文案并复位计数；进行中仍显示并禁用连续检测。  
> 3. **落点**：`VM_NCLCCTPage.RefreshFolderImportProgressUi`；替换包 `E:\共享文档\NCLCCT_离线进度清提示与左右环互斥_替换文件_20260928_1339.zip`。  

> **2026-09-28 变更（左/右环路径文案 + 离线进度提示 + 互斥连续检测）**  
> 1. **背景**：监听路径需称「左环/右环」；离线文件夹导入时要明示模式与进度，并禁止同时开连续检测。  
> 2. **做法**：弹窗与 Tooltip 改为左环/右环监听路径；顶部文案改为「当前处于离线检测模式，当前进度：已检 / 总数」；离线未完成时禁用「开始/连续检测」。  
> 3. **落点**：`NCLCCTMonitorPathSelectDialog.xaml`；`VM_NCLCCTMonitorPathSelectDialog.cs`；`NCLCCTPage.xaml`；`VM_NCLCCTPage.cs`。  

> **2026-09-28 变更（文件夹导入顶部进度 已检/总数）**  
> 1. **背景**：手动按文件夹导入时不知还剩多少张，顶部菜单右侧空隙需进度提示。  
> 2. **做法**：菜单栏右侧显示「文件夹检测：已完成 / 总数」；仅统计文件夹导入入队路径；先收集再定总数再入队，避免与消费线程竞态。  
> 3. **落点**：`NCLCCTPage.xaml`；`VM_NCLCCTPage.cs`（`FolderImportProgressText` / `_folderImportPaths`）；替换包 `E:\共享文档\NCLCCT_双监控扫码NG与文件夹进度_替换文件_20260928_1120.zip`。  

> **2026-09-28 变更（双监控目录 + 「扫码NG」跳过）**  
> 1. **背景**：现场连续检测需同时盯两个落盘根目录；「扫码NG」子目录下图片为前工序 NG，不应再检。  
> 2. **做法**：开启连续检测弹窗改为必选路径1/路径2，配置 `ImageMonitorPath`/`ImageMonitorPath2`，双 `FileSystemWatcher`；入队前若路径任一段目录名为「扫码NG」则跳过（Watcher/溢出补扫/按文件夹导入同规则）。  
> 3. **落点**：`VM_NCLCCTPage.cs`；`VM_NCLCCTMonitorPathSelectDialog.cs`；`NCLCCTMonitorPathSelectDialog.xaml`；`NCLCCTPage.xaml` Tooltip；同上替换包。  

> **2026-09-24 变更（清空 tools 下独立探针工程）**  
> 1. **背景**：`tools/` 内均为离线探针（切图/找点/NJCT 失败复现等），未进 `SUNWODA_SEVB.sln`，不影响主程序编译与现场运行。  
> 2. **做法**：删除 `CutImgProbe`、`Vision3dPointsProbe`、`VisionNjctFailProbe`、`VisionTopViewVectorProbe`（此前已删 NCLCCT 探针与下载脚本）；保留空 `tools/` 目录。  
> 3. **落点**：`tools/`。  

> **2026-09-24 变更（清理 tools 下 NCLCCT 临时探针与下载脚本）**  
> 1. **背景**：接入/排查期留下的探针与一键下载脚本已无保留必要。  
> 2. **做法**：删除 `tools/CloudNjctCheck`、`CudaModelInitProbe`、`VisionNclcctActionProbe`、`VisionNclcctDump`、`DownloadNclcctNjctDll.ps1`、`一键下载NCLCCT_NJCT.bat`（及共享目录副本）。  
> 3. **落点**：`tools/`。  

> **2026-09-24 变更（去掉 D:\\TEST.png 启动自检写盘）**  
> 1. **背景**：旧华为 DLL 曾硬编码写 `D:\TEST.png`；上位机用 `EnsureNjctTempPngWritable` 探针写 1 字节自检。云端新 `NChwSwdCT` 已改为内存转换，自检反而持续制造该文件。CTNCLX 无此逻辑，非小米迁移，是 2026-09-21 接华为时参照 CTAutoSlicer/探针加的。  
> 2. **做法**：删除自检方法及调用。  
> 3. **落点**：`VM_NCLCCTPage.cs`；替换包 `E:\共享文档\NCLCCT_去掉TEST自检_替换文件_20260924_1514.zip`。  

> **2026-09-24 变更（结果图内容 75% + 底栏 25%，避免黑带压图）**  
> 1. **背景**：内容曾缩 80%、底栏占 1/4，中间约 5% 重叠，黑带挡住标注。  
> 2. **做法**：`contentScale` 改为 `0.75`，与 `bandH = H/4` 对齐。  
> 3. **落点**：`VM_NCLCCTPage.GenerateHuaweiResultImage`；替换包 `E:\共享文档\NCLCCT_结果图75底栏_替换文件_20260924_1423.zip`（同内容英名 `NCLCCT_jieguotu75_tidailan_tihuan_20260924_1423.zip`）。  

> **2026-09-24 变更（Gap 参数改参：侧边栏管理员权限）**  
> 1. **背景**：Gap 标准/导出路径改参需管理员；用侧边栏用户中心，不另做密码框。  
> 2. **做法**：读 `CurrentUserAccount` + `Users.RoleId`；`roleId≥Admin(2)`（含 admin/sadmin）且非连续检测才可改；订阅 `UserStateChangedEvent`。guest/工程师只读。  
> 3. **落点**：`VM_NCLCCTPage.cs`；替换包 `E:\共享文档\NCLCCT_Gap改参管理员权限_替换文件_20260924_1147.zip`。  

> **2026-09-24 变更（云端 DLL/ONNX：内存或临时加载，不留正式文件）**  
> 1. **背景**：要求本地有则用本地（断网可跑）；无本地才下云端；云端资源加载后不落盘正式路径，防 DLL/模型暴露，重启再下。ORT 为路径 Session，Init 后删临时可能失败。  
> 2. **做法**：DLL 云端 → `%TEMP%\nclcct_*.dll` → `Assembly.Load(bytes)` → 删临时；ONNX 云端 → `%TEMP%\nclcct_*.onnx` → `ModelInit` → 尽量删临时；删失败 Warn 日志。本地正式文件仍优先 `LoadFrom`/`ModelInit`。  
> 3. **落点**：`VM_NCLCCTPage.cs`；替换包 `E:\共享文档\NCLCCT_云端不落盘_替换文件_20260924_1136.zip`。  

> **2026-09-23 变更（恢复本地优先：缺失才下载 + 清临时文件）**  
> 1. **背景**：曾改为每次强制重下；现场要求改回「有本地则加载本地，无本地才下云端」，且下载后不留 `.downloading`。  
> 2. **做法**：DLL/ONNX 均 `File.Exists` 优先本地；缺失才 `.downloading`→正式文件；启动与 finally 清理临时文件。  
> 3. **落点**：`VM_NCLCCTPage.cs`；替换包见 `E:\共享文档\NCLCCT_本地优先下载_替换文件_*.zip`。  

> **2026-09-23 变更（加载遮罩隐晦文案）**  
> 1. **背景**：操作员界面暴露「强制重新下载视觉模型 / model_hw.onnx / 已接收 MB」。  
> 2. **做法**：遮罩改为「正在加载算法组件/文件/初始化算法环境」+ 百分比；详细文件名与云端信息仅写日志。  
> 3. **落点**：`VM_NCLCCTPage.cs`；替换包见 `E:\共享文档\NCLCCT_加载文案隐晦_替换文件_*.zip`。  

> **2026-09-23 变更（每次启动强制重下 DLL/模型）**  
> 1. **背景**：现场要求重启即重新拉取云端算法/模型，不能长期沿用本机缓存。  
> 2. **做法**：初始化始终 SysCenter 重下 `NJCT.dll` 与 `model_hw.onnx`；先写 `.downloading`，成功后再覆盖正式文件；失败且旧文件仍在则回退告警。  
> 3. **落点**：`VM_NCLCCTPage.cs`；替换包见 `E:\共享文档\NCLCCT_每次启动强制重下_替换文件_*.zip`。  

> **2026-09-23 变更（下载遮罩文案标明缺失资源）**  
> 1. **背景**：进度文案被刷成笼统「正在下载…」，现场看不出缺 DLL 还是模型。  
> 2. **做法**：遮罩固定写「本机缺失算法 DLL（NJCT.dll）/视觉模型（xxx.onnx），正在下载…」并带字节与百分比。  
> 3. **落点**：`VM_NCLCCTPage.cs`；替换包见 `E:\共享文档\NCLCCT_模型下载进度条_替换文件_*.zip`。  

> **2026-09-23 变更（模型/DLL 下载进度条 + 取消 100s 超时）**  
> 1. **背景**：本机缺 `model_hw.onnx` 时 SysCenter 分块下载易超默认 HttpClient 100s → `TaskCanceledException`；界面仅有「运行资源请求中」无进度。  
> 2. **做法**：`Timeout=Infinite`；流式下载并上报已收字节/百分比；初始化遮罩改为文案+ProgressBar；模型先写 `.downloading` 再 Move；DLL/模型下载与 ModelInit 异步化，避免卡死 UI。  
> 3. **落点**：`VM_NCLCCTPage.cs`、`NCLCCTPage.xaml`；替换包见 `E:\共享文档\NCLCCT_模型下载进度条_替换文件_*.zip`。  

> **2026-09-23 变更（CSV 规格列 + 临时合并写 + NG 分仓）**  
> 1. **背景**：CSV 需在 Gap 值后带规格上下限；Excel 打开时不丢行；NG 拆「超规检测NG / 图像异常NG」且目录为 `NG\子类`。  
> 2. **做法**：表头追加 `Gap1~3_上限,Gap4~9_上限/下限,Gap10上限`（无 Gap1~3_下限）；去「点数」；先写 `.cache` 再经临时文件合并正式 CSV；归档 `...\NG\超规检测NG` / `...\NG\图像异常NG`。  
> 3. **落点**：`VM_NCLCCTPage.cs`；`E:\共享文档\NCLCCT_CSV规格与NG分仓_替换文件_20260923_0931.zip`。  

> **2026-09-22 变更（导出目录对齐：原图/结果图按日+OK·NG·异常）**  
> 1. **背景**：现场要求 `E:\IME检测结果\原图|结果图\{yyyyMMdd}\{OK|NG|检测异常}`，CSV 为 `结果图\{日期}\Gap检测结果.csv`；原先仍写 `汇总\` 且 E 盘未建（配置优先旧路径）。  
> 2. **做法**：检测异常=推理失败或 RunInfo 非空；原图分仓、结果图 OK/NG；启动纠正空/盘符根/旧「检测结果」为 `E:\IME检测结果` 并建骨架。  
> 3. **落点**：`VM_NCLCCTPage.cs`；`E:\共享文档\NCLCCT_IME导出目录结构_替换文件_20260922_1054.zip`。  

> **2026-09-22 变更（修复：缺 OpenCvSharp 导致 GetTypes 失败、模块数 0）**  
> 1. **背景**：现场日志 `Could not load … OpenCvSharp` → `ModuleManager`/`DatabaseInitializer` 的 `GetTypes` 抛 `ReflectionTypeLoadException` →「总业务模块数: 0」侧边栏空。非 NCLCCT 业务逻辑删除依赖，而是扫描类型时遇缺 DLL 整段放弃。  
> 2. **做法**：`GetLoadableTypes` 容错保留已加载类型；替换包补 `OpenCvSharp*.dll` + `opencv_world470.dll`。  
> 3. **落点**：`ModuleManager.cs`、`DatabaseInitializer.cs`；`E:\共享文档\NCLCCT_侧边栏OpenCv容错_替换文件_20260922_0957.zip`。  

> **2026-09-22 变更（修复：模块注册并发连库导致侧边栏空白）**  
> 1. **背景**：替换后启动报 `SqlSugar … Cannot Open when State is Connecting`，侧边栏无模块。根因是 `ModuleManager` 对程序集 `Parallel.ForEach` + 同步 `GetIsEnabled` 共用连接；单次失败还拖垮整程序集注册。  
> 2. **做法**：模块注册改为串行；单模块 try/catch；`MappingRepository` 同步查询统一 `CopyNew()`，无数据返回 null。  
> 3. **落点**：`ModuleManager.cs`、`MappingRepository.cs`；规范 §5 SqlSugar 条；替换包 `E:\共享文档\NCLCCT_侧边栏连库修复_替换文件_20260922_0949.zip`（仅 SEVB+Data+Core）。  

> **2026-09-22 变更（发包修正：最小包仅主程序+Core）**  
> 1. **背景**：0937 整批覆盖 `SUNWODA_SEVB.*`（含 Data/Component 等）后侧边栏无界面；NCLCCT 本轮代码仅改 `VM_NCLCCTPage.cs`。  
> 2. **做法**：说明多余程序集风险；重打最小包仅 `SUNWODA_SEVB` + `Core`；现场须先还原 0937 再只盖最小包。  
> 3. **落点**：`E:\共享文档\NCLCCT_监控CSV导出补丁_最小替换_*.zip`。  

> **2026-09-22 变更（发包修复：主 DLL 须与 Core 等同批）**  
> 1. **背景**：第一版只替换 `SUNWODA_SEVB.dll`，现场旧 `Core` 无 `IZZSIXPack6ConfigService`，启动报 `Could not load type ... from assembly SUNWODA_SEVB.Core`。  
> 2. **做法**：重打替换包，纳入主程序 + 全部 `SUNWODA_SEVB.*` dll/pdb（含 Core）同源覆盖；替换说明写明勿只换主 DLL。  
> 3. **落点**：`E:\共享文档\NCLCCT_监控CSV导出补丁_替换文件_20260922_0937.zip`。  

> **2026-09-22 变更（监控不丢图 + CSV Gap 明细 + 默认导出 E:\IME检测结果）**  
> 1. **背景**：整夹（含子目录）复制进监控目录时仅检到少量图，并弹出 `InternalBufferOverflowException`；汇总 CSV 只有 OK/NG；结果默认落到源图旁「检测结果」。  
> 2. **做法**：  
>    - Watcher：`InternalBufferSize=64KB`；回调仅路径入队；`WaitFileReady` 移到检测线程；溢出后重启 Watcher + 监控根 `AllDirectories` 补扫入队。  
>    - 导入文件夹改为递归子目录、轻量入队。  
>    - 汇总 CSV 增加 Gap1~11(mm)、超限明细；同日旧表头自动改写 `*_gaps.csv`。  
>    - 默认导出根 `E:\IME检测结果`（无则创建）；历史 `C:\`/盘符根启动时纠正。  
>    - 审查沉淀：规范 §6.2 + 一键审查 §D + [案例-FileSystemWatcher缓冲溢出丢检.md](../仓库代码审查/案例-FileSystemWatcher缓冲溢出丢检.md)。  
> 3. **落点**：`VM_NCLCCTPage.cs`；现场替换包以 **0937** 版为准（含 Core；0932 仅主 DLL 会导致启动失败）。  
> 4. **验证**：Debug 编译 0 error。  

> **2026-09-21 变更（云端 DLL + Gap7 画线 + 间隙偏移）**  
> 1. **云端**：本机无 `Dlls\NCLCCT\NJCT.dll` 时走 SysCenter（本地优先）；DLL 内存加载不落盘，模型缺则落盘。`appKey=nc_lixian_ct_hw`，`Dll/Model` 码分别为 `NC_LiXianCT_HW_Dll` / `NC_LiXianCT_HW_Model`；`CF.dll` 仍本机。架构说明见 [项目架构说明.md](../项目架构说明.md) §11.4。  
> 2. **ModelAction**：适配新 DLL 9 参（去掉 `AllJianXiValue`）。  
> 3. **Gap**：全部 `mm = px×scale − 0.12`；Gap7 按视觉点对画线，竖线左偏 80px + 虚线引出。  
> 4. **落点**：`VM_NCLCCTPage.cs`。  

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
> 4. **未做 / 待确认**：业务范围、点位、相机、存储与算法全部 ⏸（见规划 §1、§5、§10）；菜单需用户完整重启后人工验收。  
> 5. **验证**：编译 0 error；SQL 执行结果 `VM_NCLCCTPage = 1`、`AppStartMainWindow = MainWindow`。
