# ZZSIX 项目开发规划

> 文档版本：2026-09-14（PACK 六面检 P0）  
> **角色：冷层详规** — 范围、架构、资料说明；**不要**在本文维护每日进度  
> **进度 / 待办 / 索引**：[ZZSIX进度存档.md](./ZZSIX进度存档.md)  
> 平台：SUNWODA SEVB（`develop`，.NET 8 WPF）  
> 旧源：`sunwoda1.0.0.2` 分支 `LrzWork` → `VM_PagePack6Surface`  
> 维护人：接手开发者  

---

## 0. 开发资料录入（工作流）

> **状态**：部分整理（配置键/队列 CSV 已落盘）· 原始投放见 [开发资料/](./开发资料/)  
> **整理后真相**：[pack6配置键.csv](./pack6配置键.csv)、[pack6队列需求.csv](./pack6队列需求.csv)  
> 现场 ONNX / ROI / 真实相机路径请放入 `开发资料/` 后再录入。

---

## 1. 项目定位与目标

### 1.1 业务定位

**ZZSIX** = **PACK 六面检**（枣庄主流程优先）：监视相机落盘目录 → 分队列处理 → AI 检项 → Pack 齐套汇总 → CSV / 报警 / PLC。

旧实现：`VM_PagePack6Surface`（约 8k+ 行单体 ViewModel）。新实现按服务拆分重写，禁止整文件拷贝。

### 1.2 本期范围

| 能力 | 状态 | 说明 |
|------|------|------|
| 菜单页启停 / 配置 / 统计 / 日志 | ✅ P0 | `VM_ZZSIXPage` |
| 三目录 FileSystemWatcher 收图 | ✅ P0 | Top/Bottom/Side |
| 中央单消费者管线 | ✅ P0 | `Pack6PipelineService` |
| Pack 齐套 / 超时汇总 | ✅ P0 | 队列需求见 CSV |
| 结果 CSV | ✅ P0 | `{LocalPathPre}/检测结果/` |
| 模拟视觉引擎 | ✅ P0 | 未接 ZZPack |
| ZZPack.AI 全检项 / X04C | ❌ P1 | |
| 八窗实时预览 / 参数窗 | ❌ P1 | |
| PLC_HeartBeat / AlarmSignal | ❌ P1 | 仅登记键名，地址不臆造 |
| 南京 Pack6SurfaceNJ | ❌ P2 | |

### 1.3 明确不做（本期）

- 打胶机族、JSPD、堆叠检验（LrzWork 其它工序）
- 不移植旧 `BaseViewModel` / `UserDefineVariableInfo` 全局字典
- 不在无现场资料时臆造 PLC 寄存器地址

---

## 2. 现有资料盘点

| 资料 | 位置 |
|------|------|
| LrzWork 迁移分析 | [开发资料/LrzWork迁移分析.md](./开发资料/LrzWork迁移分析.md) |
| 配置键 | [pack6配置键.csv](./pack6配置键.csv) |
| 队列需求 | [pack6队列需求.csv](./pack6队列需求.csv) |
| 旧主 VM | `...\sunwoda1.0.0.2\SUNWODA.UI\ViewModel\VM\Wincc\Pack\VM_PagePack6Surface.cs` |

---

## 3. 总体架构设计

| 层 | 职责 |
|----|------|
| View / VM | 启停、路径、统计、日志、模拟一整包 |
| Ingress | 目录监视 → `Pack6IngressItem` |
| Pipeline | 中央队列调度 |
| Vision | `IZZSIXPack6VisionEngine`（P0 模拟 / P1 ZZPack） |
| Aggregator | Pack 齐套与超时 |
| ResultStore | CSV 落盘 |
| Config | `project_setting` ↔ 运行参数 |

主流程对齐旧：`OnFileCreated` → 队列 → `ProcessFile`/`AiDetectionInImage` → `CheckProductCompletion` → `SaveDetectionResult`。

---

## 4. 开发计划与验收

| 阶段 | 内容 | 验收 |
|------|------|------|
| P0 | 模拟闭环 | 启动 → 模拟一整包 → CSV 有 OK 行 |
| P1 | 真算法 + 预览 + PLC | 上盖真检；心跳/报警按现场地址 |
| P2 | 南京线 | 10 窗路由 + 算法 |

---

## 5. 风险与待确认事项

| # | 事项 | 状态 |
|---|------|------|
| 1 | ZZPack / YOLO* 在 .NET 8 的引用与模型路径 | ⏸ 待现场 DLL |
| 2 | 文件名→队列路由是否与现场命名一致 | ⏸ 待样张验证 |
| 3 | PLC 地址仅键名，需现场 `PlcAddress` | ⏸ |
| 4 | 南京线是否同仓同页 | ⏸ 默认 P2 |

---

## 6. 模块启用

- SQL：[enable_zzsix_module.sql](./enable_zzsix_module.sql)
- `vm_class_name`：`VM_ZZSIXPage`
- 菜单显示名：`PACK六面检`
- 库：`sunwoda_demo`
