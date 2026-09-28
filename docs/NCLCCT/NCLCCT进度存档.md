# NCLCCT 进度存档

> **文档版本**：2026-09-24  
> **状态**：南昌华为离线 CT。本地有 DLL/ONNX 用本地；无则云端临时/内存且不落盘；Gap 改参需侧边栏管理员；隐晦加载文案 + 进度  
> **用途**：新会话 **只拖本文**；规格细节不写在本文，用下方索引跳转  
> **详规**：[NCLCCT项目开发规划.md](./NCLCCT项目开发规划.md)　**版本历史**：[NCLCCT版本变更记录.md](./NCLCCT版本变更记录.md)

**进度图例**：✅ 已完成　🔄 进行中　⏸ 阻塞（待资料/待确认）　❌ 未开始

---

## 0. 文档索引（事实只写一处）

> 进度与待办 **只维护本文**；范围/架构/点位 **只维护规划或 CSV**；历史变更 **只维护版本变更记录**。

| 要查什么 | 去哪里（勿在本文重复抄写） |
|----------|---------------------------|
| **版本变更 / 历史决策** | [NCLCCT版本变更记录.md](./NCLCCT版本变更记录.md) |
| 业务范围、本期不做 | 规划 [§1](./NCLCCT项目开发规划.md#1-项目定位与目标) |
| 开发资料投放与录入状态 | 规划 [§0](./NCLCCT项目开发规划.md#0-开发资料录入工作流)、[开发资料/](./开发资料/) |
| 平台约束、模块形态、三名字对应 | 规划 [§3](./NCLCCT项目开发规划.md#3-平台约束必须遵守) |
| 架构分层 / 待建服务清单 | 规划 [§4](./NCLCCT项目开发规划.md#4-总体架构设计) |
| 设备与点位（⏸ 待资料） | 规划 [§5](./NCLCCT项目开发规划.md#5-数据与设备对接--全部待资料) |
| 存储 / 算法（⏸ 待定） | 规划 [§6](./NCLCCT项目开发规划.md#6-数据存储与算法--待定) |
| 界面与展示 | 规划 [§7](./NCLCCT项目开发规划.md#7-界面与展示) |
| 里程碑与阶段 | 规划 [§9](./NCLCCT项目开发规划.md#9-实施阶段与里程碑) |
| 风险与待确认 | 规划 [§10](./NCLCCT项目开发规划.md#10-风险与待确认事项) |
| 启用模块 SQL | [enable_nclcct_module.sql](./enable_nclcct_module.sql) |
| 平台新模块约定 | [新增业务模块上手指南](../新增业务模块上手指南.md) |
| 格式样板 | [docs/BoxGluing/](../BoxGluing/) |

**CSV / 接口文档**：暂无（资料录入后在本节补链接）。

---

## 1. 当前状态

**NCLCCT = 南昌华为离线 CT。** 界面流程按 CTNCLX；视觉已接 `Dlls\NCLCCT\NJCT.dll` 的 `NJCT.NChwSwdCT`，模型 `model\NCLCCT\model_hw.onnx`。推理后抓点画到结果图并显示。注意：截图接口文档与实 DLL 签名不一致，以实 DLL 为准。

### 待办

| # | 任务 | 状态 | 验收 | 详规索引 |
|---|------|------|------|----------|
| 1 | 人工验收：选图检测，界面出点与底栏数值 | 🔄 | 进 NCLCCT 选 `*-R_origin`，出结果图/点/底栏 Gap | — |
| 2 | 华为 Gap 规格判定（现场公式） | ✅ | Gap1~6、10 按管控判 OK/NG；mm=px×精度−分组常量 | 变更记录 2026-09-20 |
| 3 | 与视觉确认截图文档是否为下一版 DLL | ⏸ | 若升级为 long 句柄接口再改反射签名 | — |
| 4 | 底栏排版微调（若现场仍嫌疏/挤） | 🔄 | 按截图反馈微调列距 | — |
| 5 | 整夹监控不丢图 + CSV Gap + 默认 E 盘导出 | ✅ | 含子目录 `R_origin` 逐张入队；CSV 有 Gap1~11；结果在 `E:\IME检测结果` | 变更记录 2026-09-22 |
| 6 | 导出目录结构：原图/结果图按日+OK·NG·异常 | ✅ | 已演进为 `NG\超规|图像异常` + CSV 规格列 | 变更记录 2026-09-23 |
| 7 | CSV 规格列 / 临时合并写 / NG 分仓 | ✅ | Gap 后规格列；cache 合并；`NG\超规检测NG`·`图像异常NG` | 变更记录 2026-09-23 |
| 8 | 缺模型/DLL 下载进度条 + 无 100s 超时 | ✅ | 初始化遮罩 ProgressBar；流式下载；Timeout=Infinite | 变更记录 2026-09-23 |
| 9 | 每次启动强制重下 DLL/模型 | ❌ 已撤回 | 改回本地优先，见 #11 | 变更记录 2026-09-23 |
| 10 | 加载遮罩隐晦文案 | ✅ | 「正在加载算法文件…」+ 百分比，不暴露文件名/云端 | 变更记录 2026-09-23 |
| 11 | 本地优先：缺失才下载 + 清临时文件 | ✅ | 演进见 #12 | 变更记录 2026-09-23 |
| 12 | 云端不落盘：DLL 内存 / ONNX 临时后删 | ✅ | 无本地则 `%TEMP%` + Load(bytes)/Init 后删；失败 Warn | 变更记录 2026-09-24 |
| 13 | Gap 改参：侧边栏管理员权限 | ✅ | admin/sadmin 可改 Gap/导出路径；guest/工程师只读 | 变更记录 2026-09-24 |
| 14 | 结果图内容 75% + 底栏 25% | ✅ | contentScale=0.75，黑带不压检测标注 | 变更记录 2026-09-24 |
| 15 | 去掉 D:\TEST.png 启动自检 | ✅ | 不再探针写盘；云端华为 DLL 已不依赖该路径 | 变更记录 2026-09-24 |
| 16 | 清理 tools NCLCCT 探针/下载脚本 | ✅ | 已删；随后 #17 清空其余探针 | 变更记录 2026-09-24 |
| 17 | 清空 tools 独立探针工程 | ✅ | 未进 sln，删不影响 SEVB/其它业务运行 | 变更记录 2026-09-24 |

### 已完成

- ✅ 视觉 DLL / CF / ONNX 独立部署（不覆盖 CTNCLX）
- ✅ `ModelInit` / `ModelAction`（10 参）反射接入
- ✅ 监控/导入仅 `R_origin`；Halcon/CUDA 运行库路径就绪
- ✅ `GenerateHuaweiResultImage` 绘点 + 底栏管控/测量；Gap 红绿与整图 NG
- ✅ Gap标准菜单：像素精度 + 管控上下限可配
- ✅ 菜单 `VM_NCLCCTPage` / 显示名 `NCLCCT`
- ✅ Watcher 轻量入队 + 64KB 缓冲 + 溢出补扫；导入递归；CSV Gap 明细；默认 `E:\IME检测结果`

---

## 2. 关键代码入口（精简）

| 职责 | 路径 |
|------|------|
| 页面 VM（`[Module]` 注册处） | `SUNWODA_SEVB/ViewModels/Pages/NCLCCT/VM_NCLCCTPage.cs` |
| 视觉 DLL / 模型 | 同文件：`TryLoadVisionAssemblyAsync`、`TryInitVisionModelAsync`、`HttpPostDownload*` |
| 页面 View | `SUNWODA_SEVB/Views/Pages/NCLCCT/NCLCCTPage.xaml`、`NCLCCTPage.xaml.cs` |
| 监控路径对话框 | `SUNWODA_SEVB/ViewModels/Dialogs/NCLCCT/VM_NCLCCTMonitorPathSelectDialog.cs`；`Views/Dialogs/NCLCCT/NCLCCTMonitorPathSelectDialog.xaml` |
| 模型 | `SUNWODA_SEVB.Core/Models/Project/NCLCCT/DetectionInfoModel.cs`、`DetectionItemModel.cs` |
| 参照源（勿改） | `SUNWODA_SEVB/ViewModels/Pages/CTNCLX/VM_CTNCLXAutoDetectionPage.cs` |
| DI 注册入口 | `SUNWODA_SEVB/Services/ProjectServiceCollectionExtensions.cs`（`AddProjectServices()`） |
| 模块发现与注册 | `SUNWODA_SEVB/Services/ModuleManager.cs`（读 `workspace_project.is_enabled`） |

分层原则见规划 §4.1（勿在本文展开）。

---

## 3. 下一会话阅读顺序

1. **本文** — 状态、待办、代码入口  
2. 按任务打开 §0 索引中的规划章节或 CSV（例：做点位 → 规划 §5 + `plc点位表.csv`）  
3. 改对应代码入口表中的文件  
4. 收尾：**先更新本文**（勾待办）；在 [版本变更记录](./NCLCCT版本变更记录.md) **顶部追加一条**；规格变了再改规划对应节

---

## 4. 本机环境备忘（构建/数据库）

| 项 | 值 |
|----|-----|
| 构建方式 | `dotnet "C:\Program Files\dotnet\sdk\8.0.424\MSBuild.dll" SUNWODA_SEVB/SUNWODA_SEVB.csproj -t:Build -p:Configuration=Debug` |
| 还原 | 本机 NuGet 源不可达，用 `-p:RestoreSources="$env:USERPROFILE\.nuget\packages"` 离线还原 |
| 数据库 | MariaDB 12.3，`127.0.0.1:3306`，库 `sunwoda_demo`（客户端需 `--skip-ssl`） |
| 注意 | 默认 `dotnet build` 走 SDK 10.0.400 时还原异常，改用 SDK 8.0.424 的 MSBuild |
