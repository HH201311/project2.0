# LrzWork → ZZSIX 迁移分析

> **分析日期**：2026-09-14  
> **旧仓**：`E:\WorkSpace\SWD\ZZSIX\sunwoda1.0.0.2`（分支 `LrzWork`，相对 `master` 约 +46k / -17k 行）  
> **新仓目标**：`sunwoda2.0.0.1` → 项目代号 **ZZSIX**（当前仅空页 demo）  
> **结论**：可迁移，但必须按工序分阶段重写，禁止整文件拷贝旧 `BaseViewModel` / `ValueListener` 耦合代码。

---

## 1. 「lrzWork」是什么

| 误解 | 事实 |
|------|------|
| 独立解决方案 / 文件夹 | **Git 分支名** `LrzWork`（当前已检出） |
| 单一业务页 | **多工序合集**：打胶机族、六面检、机损配对、堆叠检验、飞书告警等 |
| 可直接升到 .NET 8 | 旧工程为 **.NET Framework 4.8**（`SUNWODA.UI` / `Utility`），需按新 SEVB 分层重写 |

旧框架特征（迁移时必须替换）：

- `BaseViewModel` + 页面单例 `Current`
- `ValueListener` / `Timer` 轮询 PLC
- `UserDefineVariableInfo.DicVariables` 全局字典
- `BLL.*` 直连库、HandyControl、Python.NET、Halcon/OpenCv/YOLO 等混在 ViewModel

新框架应对齐：`ViewModelBase` + `[Module]`、DI、`Services/Project/ZZSIX/`、`project_setting` / CSV 点位真相（样板见 `docs/BoxGluing/`）。

---

## 2. LrzWork 业务模块盘点（相对 master）

### A. 打胶 / 压合族（DJJC）

| 旧 VM | 约行数 | 说明 | 新仓现状 |
|-------|--------|------|----------|
| `VM_PageBoxGluing` | ~1.6k | 口琴管/箱体涂胶监测 | **已有独立项目 BoxGluing（P0 采集+曲线）**，勿再塞进 ZZSIX 重复做 |
| `VM_PagePressBase` | ~1.0k | 打胶机抽象基类 | 未迁 |
| `VM_PageModulePackaging` | ~0.2k | 模组入箱（继承 PressBase） | 未迁 |
| `VM_PagePipeCrimping` | ~0.3k | 相关压接（继承 PressBase） | 未迁 |
| `VM_PageBottomGluing` | ~0.6k | 底部压胶 | 未迁 |
| `VM_PageLengbanGluing` | ~1.3k | 冷板打胶 | 未迁 |
| `VM_PageBoxPress` | ~1.2k | 箱体压合 | 未迁 |
| `VM_PageJianbuPress` | ~1.5k | 键部压合 | 未迁 |
| `VM_PageStackingGluing` | ~1.3k | 堆叠打胶（有改动） | 未迁 |
| `VM_PageSidePanelGluing` / `InsertionPressureAlert` | 很大 | 同目录存量，未必全是本分支新增 | 未迁 |

### B. PACK 六面检

| 旧 VM | 约行数 | 说明 | 新仓现状 |
|-------|--------|------|----------|
| `VM_PagePack6Surface` | **~8.3k** | 枣庄六面检；含 X04→X04C 适配、多 YOLO/OCR | 未迁（最重） |
| `VM_PagePack6SurfaceNJ` | ~1.7k | 南京六面检 | 未迁 |
| `AIResults.cs` 等 | 很大 | 检测结果模型 | 未迁 |

依赖：Halcon、OpenCv、YOLO*、WeChatQR、RapidOCR、`ZZPack` 等原生/私有库 —— 迁移前需确认新仓是否已有等价封装。

### C. 机损配对（JSPD）

| 旧 VM | 约行数 | 说明 | 新仓现状 |
|-------|--------|------|----------|
| `VM_PageJSPDDetection` | ~3.8k | 机损配对主页 | 未迁（新仓有 HawkEye/PTO 机损，**不是同一工序**） |
| 若干 Fan/Zheng 参数子 VM | 各数百行 | 参数窗 | 未迁 |

### D. 堆叠工序检验（SPI）

| 旧 VM | 约行数 | 说明 |
|-------|--------|------|
| `VM_PageStackingProcessInspection` | ~1.6k | 电芯堆叠；多路 `ValueListener`（D6000～等） |

### E. 横切能力

| 组件 | 路径 | 说明 |
|------|------|------|
| 飞书告警 | `Utility/Notifications/*`、`FeishuPusher.cs` | 队列 + Options；可抽平台级服务，不一定挂在 ZZSIX |
| 检测报警辅助 | `DetectionAlarmRecordHelper.cs` | 写库辅助 |
| Web 文件下载 | `WebFileLoadHelper.cs` | 有改动 |

---

## 3. 迁移策略（建议）

1. **禁止**：把旧 VM 整文件粘进 `ViewModels/Pages/ZZSIX/`。  
2. **按工序建页**（或子目录）：一页一 `[Module]`，复杂逻辑进 `Services/Project/ZZSIX/`。  
3. **BoxGluing**：继续用已有 `docs/BoxGluing/`，ZZSIX 规划中只做交叉引用，不重复实现。  
4. **资料**：旧点位/配置先放 `docs/ZZSIX/开发资料/`，录入后再写 CSV。  
5. **验收节奏**：每工序「菜单可进 → 读配置 → 采集/算法 → 落盘/告警」分里程碑。

### 建议阶段

| 阶段 | 内容 | 预估难度 |
|------|------|----------|
| P0 | 选定 **1** 个主工序 + 空页扩展为真实 UI 骨架 + 点位 CSV | 中 |
| P1 | PLC 触发采集 / 曲线或图像流水线（视工序） | 高 |
| P2 | 算法 / YOLO / Python 对接 | 很高 |
| P3 | 飞书告警、报警落库（可平台复用） | 中 |

---

## 4. 待你确认（阻塞整仓开迁）

请指定 **ZZSIX 第一优先工序**（只选一个）：

1. PACK 六面检（枣庄 `Pack6Surface` / 南京 `Pack6SurfaceNJ`）  
2. 机损配对 JSPD  
3. 堆叠工序检验 SPI  
4. 打胶机族中的某一页（Bottom / Lengban / BoxPress / Jianbu / ModulePackaging / …）  
5. 仅先迁飞书告警横切能力  

确认后即可按该工序在 ZZSIX 下落盘重写（对照 BoxGluing 分层）。
