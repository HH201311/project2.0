# CTAutoSlicer 迁移说明

> 文档版本：2026-07-07（develop 分支）  
> 用途：说明「CT 自动化切片」业务从独立项目迁移到 SUNWODA 平台时，为什么会产生这么多文件、每个文件做什么、整体调用链怎么走。  
> 关联文档：`docs/新增业务模块上手指南.md`、`docs/项目架构说明.md`

---

## 1. 先说结论

这次变更**不是**「在平台里新增一个普通业务页」，而是**把一个原本独立运行的桌面自动化业务，按平台分层重新拆装进来**。

因此文件数量会从文档里说的 **3 个页面文件**，扩展到 **20+ 个文件**：

| 接入方式 | 典型场景 | 最少需要新增什么 |
|----------|----------|------------------|
| **标准页面接入** | 平台已有能力，只挂一个新菜单页 | `VM_XxxPage.cs` + `XxxPage.xaml` + `XxxPage.xaml.cs` |
| **完整业务迁移** | 原项目自带服务、算法、自动化、监控 | 页面 + 接口 + 模型 + 服务 + 工具类 + DI 注册 + 依赖包 |

`CTAutoSlicer` 属于第二种。

---

## 2. 这次 Git 变更总览

### 2.1 已暂存变更（CTAutoSlicer 主体）

| 类型 | 数量 | 说明 |
|------|------|------|
| 新增文件 | 21 | 页面、服务、接口、模型、工具类 |
| 修改文件 | 3 | 项目文件与 DI 注册 |
| **合计** | **24** | 不含 `docs/` 与 `.cursorignore` |

### 2.2 修改的 3 个已跟踪文件

| 文件 | 为什么改 |
|------|----------|
| `SUNWODA_SEVB/Services/ProjectServiceCollectionExtensions.cs` | 注册 CTAutoSlicer 相关服务到 DI 容器 |
| `SUNWODA_SEVB.Core/SUNWODA_SEVB.Core.csproj` | 显式 Include 新增接口文件（Core 对 `Interfaces/Project/**` 采用白名单编译） |
| `SUNWODA_SEVB.Tool/SUNWODA_SEVB.Tool.csproj` | 新增 `SharpHook`、`System.Drawing.Common` 依赖 |

---

## 3. 为什么不是 3 个文件就够

`docs/新增业务模块上手指南.md` 中的「3 个文件」有一个隐含前提：

1. 平台底层能力已经具备  
2. 页面只负责展示、参数配置、调用现有服务  
3. 不需要新增项目级服务、算法、桌面自动化基础设施

而 `CTAutoSlicer` 的业务本质是：

- 启动并控制外部 VG 软件（`vgstudiomax20241.exe`）
- 通过 **UI Automation** 查找窗口和控件
- 通过 **鼠标/键盘模拟** 完成导入、对齐、旋转等操作
- 通过 **截图 + 算法 DLL** 计算角度
- 通过 **全局 Esc Hook** 支持流程中断
- 通过 **文件夹监控 / 进程监控** 记录运行状态

这些能力在原独立项目中已经存在，迁移到平台后必须拆到对应分层，而不能全部塞进 ViewModel。

---

## 4. 架构分层与文件职责

### 4.1 分层总图

```text
┌─────────────────────────────────────────────────────────────┐
│  UI 层（SUNWODA_SEVB）                                       │
│  VM_CTAutoSlicerPage / CTAutoSlicerPage                     │
│  - 参数绑定、命令、页面生命周期                               │
└───────────────────────────┬─────────────────────────────────┘
                            │ 注入
┌───────────────────────────▼─────────────────────────────────┐
│  业务服务层（SUNWODA_SEVB/Services/Project/CTAutoSlicer）    │
│  CTAutoSlicerService / VGAutomationService / ...            │
│  - 流程编排、自动化执行、监控、Hook                           │
└───────────┬───────────────────────────────┬─────────────────┘
            │ 依赖接口                       │ 依赖工具
┌───────────▼──────────────┐   ┌────────────▼────────────────┐
│  契约层（SUNWODA_SEVB.Core）│   │  工具层（SUNWODA_SEVB.Tool） │
│  Interfaces + Models       │   │  Helper/*                   │
└────────────────────────────┘   └─────────────────────────────┘
            │
┌───────────▼─────────────────────────────────────────────────┐
│  平台基础设施                                                 │
│  DI 注册 / 日志 / 项目配置仓储 / Web API 下载 DLL             │
└─────────────────────────────────────────────────────────────┘
```

### 4.2 为什么要这样拆

| 分层 | 目的 |
|------|------|
| **ViewModel / View** | 只管 UI 和页面状态，不直接写自动化细节 |
| **Service** | 承载业务流程，便于测试、复用、取消、日志 |
| **Interface** | ViewModel 依赖抽象，不绑死具体实现 |
| **Model** | 把运行参数集中管理，避免魔法字符串散落 |
| **Helper** | 把鼠标、截图、WinAPI 等底层能力抽成通用工具 |
| **DI 注册** | 让页面通过构造函数自动拿到服务实例 |

这也是 develop 分支上 CCTB、CTOL、JRCW 等产线项目的通用做法：**页面薄、服务厚**。

---

## 5. 文件清单（逐个说明）

### 5.1 页面层（3 个文件）—— 平台标准接入部分

| 文件 | 作用 |
|------|------|
| `SUNWODA_SEVB/ViewModels/Pages/CTAutoSlicer/VM_CTAutoSlicerPage.cs` | 页面逻辑中心。带 `[Module("VM_CTAutoSlicerPage", "CT自动化切片")]`，负责参数读写、命令绑定、生命周期管理 |
| `SUNWODA_SEVB/Views/Pages/CTAutoSlicer/CTAutoSlicerPage.xaml` | 页面 UI。参数表单、测试按钮、流程启动按钮、状态显示 |
| `SUNWODA_SEVB/Views/Pages/CTAutoSlicer/CTAutoSlicerPage.xaml.cs` | XAML 代码后置，通常只保留 `InitializeComponent()` |

**ViewModel 主要做什么：**

- 从 `IProjectSettingRepository` 读写模块参数（VG 路径、偏移量、等待时间等）
- 注入并调用 `ICTAutoSlicerService`、`IAngleAlgorithmService` 等服务
- 在 `OnNavigatedTo` 启动文件夹监控和全局 Esc Hook
- 在 `OnNavigatedFrom` / `OnCleanup` 停止流程并释放资源

这部分对应「新增业务模块上手指南」里的标准页面接入。

---

### 5.2 契约层（3 个文件）—— 定义“能做什么”

| 文件 | 作用 |
|------|------|
| `SUNWODA_SEVB.Core/Interfaces/Project/CTAutoSlicer/ICTAutoSlicerService.cs` | 切片业务对外接口：`RunSimulationAsync`、`TestOffsetAsync`、`Cancel` |
| `SUNWODA_SEVB.Core/Interfaces/Project/CTAutoSlicer/IAngleAlgorithmService.cs` | 角度算法接口：初始化 DLL、测量角度 |
| `SUNWODA_SEVB.Core/Models/Project/CTAutoSlicer/CTAutoSlicerSettings.cs` | 运行参数模型。包含 VG 路径、各类偏移量/等待时间、DLL 下载地址、参数完整性校验 |

**为什么需要它们：**

- ViewModel 不应该直接依赖 `VGAutomationService` 这种具体类
- 参数对象 `CTAutoSlicerSettings` 让服务之间传递配置时有统一结构
- 符合平台 `Core.Interfaces.Project.*` 的既有约定

**注意：** `SUNWODA_SEVB.Core.csproj` 对 `Interfaces/Project/**` 不是自动编译，而是手动 `<Compile Include=...>`，所以新增接口后必须改 csproj。

---

### 5.3 业务服务层（6 个文件）—— 迁移工作的核心

| 文件 | 作用 | 关键依赖 |
|------|------|----------|
| `CTAutoSlicerService.cs` | 对外业务门面。管理运行状态、取消令牌，把页面请求转给 `VGAutomationService` | `VGAutomationService` |
| `VGAutomationService.cs` | **主流程编排器**。启动 VG、找控件、模拟键鼠、导入 DICOM、截图、调算法、旋转视图 | UIAutomation、SharpHook、Helper 工具类 |
| `AngleAlgorithmService.cs` | 角度识别算法服务。本地加载或远程下载 DLL，反射调用 `Secondary_positioning` | Halcon、Web API |
| `CTFolderMonitorService.cs` | 监控 CT 文件目录变化，记录创建/修改/删除/重命名日志 | `FileSystemWatcher` |
| `CTAutoSlicerInputHookService.cs` | 全局 Esc Hook。单次 Esc 锁输入，双击 Esc 中断流程 | `SharpHook` |
| `ProcessMonitorService.cs` | 后台采样当前进程 CPU/内存/GPU 等信息，写入 CSV | `UIAutoMonitorCore`、`CsvFileHelper` |

#### 各服务之间的关系

```text
VM_CTAutoSlicerPage
    ├─► ICTAutoSlicerService (CTAutoSlicerService)
    │       └─► VGAutomationService
    │               └─► IAngleAlgorithmService (AngleAlgorithmService)
    ├─► CTFolderMonitorService
    ├─► CTAutoSlicerInputHookService
    └─► ProcessMonitorService
```

#### `VGAutomationService` 为什么是最大文件

它承载了原独立项目里的主要业务逻辑，包括但不限于：

1. 检查参数是否完整  
2. 启动或激活 `vgstudiomax20241.exe`  
3. 用 UI Automation 定位菜单、列表、按钮、视图区域  
4. 用模拟键鼠完成导入 DICOM、等待加载、调出简单对齐  
5. 截图前视图/右视图  
6. 调算法算角度并按方向拖动旋转  
7. 全流程加锁/解锁用户输入，避免人工干扰

所以它不是“可删的冗余代码”，而是这次迁移的主体资产。

---

### 5.4 工具辅助层（6 个文件）—— 底层能力沉淀

| 文件 | 作用 | 被谁使用 |
|------|------|----------|
| `UIAutomationHelper.cs` | 查找主窗口、子控件 | `VGAutomationService` |
| `MouseSimulationHelper.cs` | 鼠标点击、移动、拖拽 | `VGAutomationService` |
| `PathGenerationHelper.cs` | 生成直线/圆弧拖拽路径 | `VGAutomationService` |
| `ScreenCaptureHelper.cs` | 区域截图并保存 PNG | `VGAutomationService` |
| `WinAPIHelper.cs` | 窗口置前、输入锁定、释放工作集等 Win32 API | `VGAutomationService`、`CTAutoSlicerInputHookService`、`ProcessMonitorService` |
| `CsvFileHelper.cs` | 追加写入 CSV 监控文件 | `ProcessMonitorService` |

**为什么放到 `SUNWODA_SEVB.Tool`：**

- 这些是跨页面可复用的底层能力，不属于某一个 ViewModel
- 与平台现有 `Tool/Helper` 目录职责一致
- 后续如果别的自动化项目接入，可以直接复用

**为什么需要新增 NuGet 包：**

| 包 | 用途 |
|----|------|
| `SharpHook` | 全局键盘/鼠标事件模拟与 Hook |
| `System.Drawing.Common` | 屏幕区域截图 |

---

### 5.5 框架接入层（1 个修改文件）

文件：`SUNWODA_SEVB/Services/ProjectServiceCollectionExtensions.cs`

新增方法：`AddCTAutoSlicerServices()`

注册内容：

| 注册类型 | 生命周期 | 说明 |
|----------|----------|------|
| `VGAutomationService` | Singleton | 自动化执行器 |
| `IAngleAlgorithmService` → `AngleAlgorithmService` | Singleton | 算法服务 |
| `ICTAutoSlicerService` → `CTAutoSlicerService` | Singleton | 业务门面 |
| `CTFolderMonitorService` | Singleton | 文件夹监控 |
| `CTAutoSlicerInputHookService` | Singleton | 全局按键 Hook |
| `ProcessMonitorService` | Singleton | 进程监控 |

并在 `AddProjectServices()` 中调用：

```csharp
services.AddCTAutoSlicerServices();
```

**如果不做这一步：** 页面构造函数注入会失败，应用启动时 DI 无法解析相关服务。

---

## 6. 运行时调用链（建议先理解这条链）

### 6.1 页面进入

```text
用户点击菜单「CT自动化切片」
    → ModuleManager 导航到 VM_CTAutoSlicerPage
    → OnInitialize()
        → LoadSettings()
        → ProcessMonitorService.StartIfNotRunning()
        → AngleAlgorithmService.InitializeAsync()
    → OnNavigatedTo()
        → CTAutoSlicerInputHookService.Start()
        → CTFolderMonitorService.Start(CTFileFolder)
```

### 6.2 执行自动化切片

```text
用户点击「运行仿真/切片」
    → VM_CTAutoSlicerPage.ExecuteRunSimulationAsync()
    → CTAutoSlicerService.RunSimulationAsync(settings, token)
    → VGAutomationService.RunSimulationAsync(settings, token)
        → 启动/激活 VG
        → UIAutomationHelper 找控件
        → MouseSimulationHelper / PathGenerationHelper 操作界面
        → ScreenCaptureHelper 截图
        → AngleAlgorithmService.MeasureAngle()
        → 根据角度旋转视图
```

### 6.3 中断流程

```text
用户双击 Esc
    → CTAutoSlicerInputHookService 捕获
    → WinAPIHelper.BlockInput(false)
    → VM_CTAutoSlicerPage.OnDoubleEscPressed()
    → CTAutoSlicerService.Cancel()
```

### 6.4 页面离开

```text
OnNavigatedFrom()
    → 停止 Hook
    → Cancel 正在运行的流程

OnCleanup()
    → 释放 Hook / 文件夹监控 / CancellationTokenSource
```

---

## 7. 配置与启用方式

### 7.1 页面模块启用

首次编译运行后，平台会把 `VM_CTAutoSlicerPage` 写入 `workspace_project`，但默认不显示。需要执行：

```sql
USE sunwoda_demo;

SELECT vm_class_name, is_enabled
FROM workspace_project
WHERE vm_class_name = 'VM_CTAutoSlicerPage';

UPDATE workspace_project
SET is_enabled = 1
WHERE vm_class_name = 'VM_CTAutoSlicerPage';
```

修改后需**完全退出并重启应用**。

### 7.2 业务参数存储

模块运行参数不写死在代码里，而是通过 `IProjectSettingRepository` 存到 `project_setting` 表，`BelongToVM = 'VM_CTAutoSlicerPage'`。

典型参数包括：

- `VGAppPath`
- `StartMenuXOffset` / `StartMenuYOffset`
- `ImportMenuItemOffset`
- `CTFileFolder`
- 各类等待时间
- 灰度图锚点偏移
- `RotationRadius`
- DLL 下载地址

页面首次加载时会自动补默认值。

### 7.3 算法 DLL

`AngleAlgorithmService` 会优先加载程序目录下的本地 DLL：

- 默认文件名：`V.NJ.L246.01.01.03.240924.dll`

若本地不存在，则通过 Web API 远程下载。相关常量定义在 `CTAutoSlicerSettings` 中。

### 7.4 运行过程输出目录

| 目录 | 内容 |
|------|------|
| `Assets/CTAutoSlicer/images/` | 前视图、右视图截图 |
| `CTAutoSlicer/进程监控信息/` | 进程性能 CSV |

---

## 8. 和 LiWeiTest 的对比（帮助建立直觉）

| 对比项 | LiWeiTest | CTAutoSlicer |
|--------|-----------|--------------|
| 接入类型 | 标准页面接入 | 完整业务迁移 |
| 新增页面文件 | 3 个 | 3 个 |
| 新增服务 | 无 | 6 个 |
| 新增接口/模型 | 无 | 3 个 |
| 新增工具类 | 无 | 6 个 |
| 修改 csproj | 通常不需要 | 需要 |
| 修改 DI 注册 | 不需要 | 需要 |
| ViewModel 复杂度 | 低，仅示例绑定 | 高，注入多个服务并管理生命周期 |
| 外部依赖 | 无 | VG 软件、算法 DLL、SharpHook、UI Automation |

可以把 `LiWeiTest` 理解成：**学会怎么挂页面**；  
把 `CTAutoSlicer` 理解成：**学会怎么挂一整条产线业务能力**。

---

## 9. 后续阅读顺序（建议）

如果你要快速掌握这块代码，建议按这个顺序读：

1. `VM_CTAutoSlicerPage.cs` — 先看页面如何组织参数和命令  
2. `ProjectServiceCollectionExtensions.cs` — 看服务如何注册  
3. `ICTAutoSlicerService.cs` + `CTAutoSlicerService.cs` — 看业务门面  
4. `VGAutomationService.cs` — 看主流程（文件较长，可分阶段阅读）  
5. `AngleAlgorithmService.cs` — 看算法 DLL 加载逻辑  
6. `SUNWODA_SEVB.Tool/Helper/*` — 看底层工具能力  
7. `CTAutoSlicerPage.xaml` — 最后对照 UI 与 ViewModel 属性

---

## 10. 常见问题

### Q1：为什么不能把所有逻辑都写在 ViewModel？

因为 CTAutoSlicer 包含：

- 长流程异步任务
- 外部进程控制
- 全局 Hook
- 文件/进程监控
- 算法调用

这些都不适合放在 ViewModel。平台约定也是：**页面负责交互，服务负责业务**。

### Q2：Helper 能不能全删了，直接写在 Service 里？

短期可以，长期不建议。Helper 让 `VGAutomationService` 只关注流程，不被底层细节淹没，也方便其他自动化模块复用。

### Q3：为什么 Core 里新增了接口还要改 csproj？

因为 `SUNWODA_SEVB.Core` 对 `Interfaces/Project/**` 采用了「先排除、再白名单 Include」策略。新增接口文件后必须手动登记，否则不会参与编译。

### Q4：我只想做页面，不想碰服务，可以吗？

可以，但前提是平台里已经有对应服务。  
这次迁移的场景是“原项目整体迁入”，所以服务层必须一起迁。

---

## 11. 变更文件速查表

| 路径 | 层级 | 必要性 |
|------|------|--------|
| `ViewModels/Pages/CTAutoSlicer/VM_CTAutoSlicerPage.cs` | 页面 | 必需 |
| `Views/Pages/CTAutoSlicer/CTAutoSlicerPage.xaml` | 页面 | 必需 |
| `Views/Pages/CTAutoSlicer/CTAutoSlicerPage.xaml.cs` | 页面 | 必需 |
| `Core/Interfaces/Project/CTAutoSlicer/ICTAutoSlicerService.cs` | 契约 | 必需 |
| `Core/Interfaces/Project/CTAutoSlicer/IAngleAlgorithmService.cs` | 契约 | 必需 |
| `Core/Models/Project/CTAutoSlicer/CTAutoSlicerSettings.cs` | 模型 | 必需 |
| `Services/Project/CTAutoSlicer/CTAutoSlicerService.cs` | 服务 | 必需 |
| `Services/Project/CTAutoSlicer/VGAutomationService.cs` | 服务 | 必需 |
| `Services/Project/CTAutoSlicer/AngleAlgorithmService.cs` | 服务 | 必需 |
| `Services/Project/CTAutoSlicer/CTFolderMonitorService.cs` | 服务 | 建议保留 |
| `Services/Project/CTAutoSlicer/CTAutoSlicerInputHookService.cs` | 服务 | 建议保留 |
| `Services/Project/CTAutoSlicer/ProcessMonitorService.cs` | 服务 | 建议保留 |
| `Tool/Helper/UIAutomationHelper.cs` | 工具 | 必需 |
| `Tool/Helper/MouseSimulationHelper.cs` | 工具 | 必需 |
| `Tool/Helper/PathGenerationHelper.cs` | 工具 | 必需 |
| `Tool/Helper/ScreenCaptureHelper.cs` | 工具 | 必需 |
| `Tool/Helper/WinAPIHelper.cs` | 工具 | 必需 |
| `Tool/Helper/CsvFileHelper.cs` | 工具 | 建议保留 |
| `Services/ProjectServiceCollectionExtensions.cs` | 接入 | 必需 |
| `SUNWODA_SEVB.Core/SUNWODA_SEVB.Core.csproj` | 接入 | 必需 |
| `SUNWODA_SEVB.Tool/SUNWODA_SEVB.Tool.csproj` | 接入 | 必需 |

---

## 12. 一句话总结

**CTAutoSlicer 的 20+ 文件，并不是平台要求你必须写这么多，而是“完整业务迁移”必然带来的分层结果：3 个文件负责挂页面，其余文件负责把原项目的自动化能力、算法能力、监控能力按 SUNWODA 平台规范拆开并接进 DI。**
