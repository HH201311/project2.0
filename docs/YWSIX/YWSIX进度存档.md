# YWSIX 进度存档

> **文档版本**：2026-10-12  
> **状态**：第二步已落地——`YWX01Pack` 真调用 + 机位路由；顶面仅检 1～4（数量必须相等）；默认 `UseSimulation=false` / 版本 `M00`  
> **用途**：新会话 **只拖本文**；规格细节不写在本文，用下方索引跳转  
> **详规**：[YWSIX项目开发规划.md](./YWSIX项目开发规划.md)　**版本历史**：[YWSIX版本变更记录.md](./YWSIX版本变更记录.md)

**进度图例**：✅ 已完成　🔄 进行中　⏸ 阻塞（待资料/待确认）　❌ 未开始

---

## 0. 文档索引（事实只写一处）

| 要查什么 | 去哪里 |
|----------|--------|
| **版本变更 / 历史决策** | [YWSIX版本变更记录.md](./YWSIX版本变更记录.md) |
| 业务范围、本期不做 | 规划 [§1](./YWSIX项目开发规划.md#1-项目定位与目标) |
| 相机 / 图片目录 | [开发资料/文件夹结构.txt](./开发资料/文件夹结构.txt) |
| 算法接口 | [开发资料/视觉算法文档.txt](./开发资料/视觉算法文档.txt) |
| 启用模块 SQL | [enable_ywsix_module.sql](./enable_ywsix_module.sql) |
| **原始开发资料** | [开发资料/](./开发资料/) |

> PLC 点位表 / 设备信息 CSV：**尚未建立**（无现场资料，禁止臆造）。PIN 阈值已按算法文档示例进配置/功能设置，可改。

---

## 1. 当前状态（义乌规格 · 第二步）

| 项 | 口径 |
|----|------|
| 菜单 | **YWSIX**（`VM_YWSIXPage`） |
| 收图根 | `\\10.130.105.114\SaveImage\SaveImage\{日期}\{SN}\{相机}\原始图像\` |
| 6 窗 | 顶面(6)+前侧(7)+后侧(6)+左侧(3)+右侧(4)+底部(5) = **31 OK** |
| 命名 | `OK-{相机名}-{张序}-{时间戳}-{SN}.jpg` |
| 视觉 | **`Pack6Ywx01VisionEngine`**；默认 `UseSimulation=false`；DLL=`Dlls\YWSIX\` |
| 机位路由 | 后侧-1/右侧-2 白标签；后侧-3 铭牌；后侧-2 PIN；顶面 **1～4** 上盖（5 拼接跳过、6 不检） |
| 上盖判定 | `hanger_bushing` / `sealing_foam` **数量必须相等**（1:2/1，2:1/0，3:1/0，4:2/2） |
| 版本 | 白标签期望 **M00**（可改；空=不校验） |
| 报警 / PLC | 空实现（同 NJ 暂不用） |

### 待办

| # | 任务 | 状态 | 验收 |
|---|------|------|------|
| 1 | 现场资料：文件夹结构 + 算法文档对照 | ✅ | 6 相机映射已写入 Catalog / 文档 |
| 2 | 照抄 NJSIX 界面与管线（除 DLL） | ✅ | 编译 0 error；菜单可进业务页 |
| 3 | 第二步：接入 `YWX01Pack`（白标签/铭牌/上盖/PIN） | ✅ | 真调用 + 机位路由；顶面仅 1～4 |
| 4 | PLC / 工艺点位 CSV（资料到位后） | ⏸ | 禁止臆造 |
| 5 | 真机联调 SaveImage 收图齐套 + DLL | ⏸ | 网络盘可监视、31 张齐套、算法 OK/NG |

### 已完成（本步）

- ✅ `Pack6Ywx01VisionEngine` + DI 注册；`Dlls\YWSIX` 复制输出  
- ✅ 上盖期望数量表 `Pack6TopCoverExpectations`（必须相等）  
- ✅ PIN 阈值 / 上盖模型 / M00 / 关模拟 → Config + 功能设置 UI  
- ✅ 编译 0 error  
- ✅ 全量包：`E:\共享文档\YWSIX_PACK六面检_Debug全量_20261012_1457.zip`（约 1.39GB）  
- ✅ PIN 独立设置（管理员）+ 运行中热更新；叠图十字/测量值  

---

## 2. 关键代码入口

| 职责 | 路径 |
|------|------|
| 相机 / 文件名 | `Core/Models/Project/YWSIX/Pack6CameraCatalog.cs` / `Pack6FileNameParser.cs` |
| 上盖期望 | `Core/Models/Project/YWSIX/Pack6TopCoverExpectations.cs` |
| 收图 | `Services/Project/YWSIX/Pack6IngressService.cs` |
| 视觉（真 DLL） | `Services/Project/YWSIX/Pack6Ywx01VisionEngine.cs` |
| 配置 | `Services/Project/YWSIX/Pack6ConfigService.cs` |
| 主页 / 设置 | `VM_YWSIXPage` / `Pack6SettingsWindow` |
| DI | `YWSIXServiceCollectionExtensions.cs`；`App.xaml.cs` |

---

## 3. 下一会话阅读顺序

1. **本文** — 状态、待办  
2. 真机联调：SaveImage 监视 + 离线测试窗调单图  
3. PLC 资料到位后再建 CSV（禁止臆造）  
