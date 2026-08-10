# 惠州离线 CT — DLL 接入说明（给下一会话）

> 编制目的：汇总当前仓库已知信息，说明如何使用 `NJCT.dll` / `CF.dll` 做电芯 CT 视觉检测接入。  
> 源码来源：工作区内 `CF/`、`NJCT/` 为从 DLL 反编译得到，接口文档见 `CT离线AI视觉接口文档.md`。  
> 日期：2026-07-28

---

## 1. 项目一句话结论

这是一套**惠州离线 CT 正极 AI 视觉检测**资源：

- 上位机/服务器通过**反射动态加载** `NJCT.dll` 调用接口；
- `NJCT` 负责业务检测（Halcon 后处理 + 抓点/尺寸）；
- `CF` 负责 ONNX 模型推理（可选 CUDA）；
- 输入主要是：**模型路径 + 图片路径 + 底部打黑高度**；
- 输出主要是：**结果图 + 两电芯抓点区域/坐标/尺寸 + 运行信息**。

---

## 2. 目录与组件职责

| 路径/文件 | 职责 |
|---|---|
| `NJCT.dll` / `NJCT/` | **对外业务 DLL**。命名空间 `NJCT`，类 `NJSwdCT`。接口文档中的方法都在这里。 |
| `CF.dll` / `CF/` / `NJCT/lib/CF.dll` | **ONNX 推理库**。`SeDPredictor` 实现 `IPredictor`，被 NJCT 的 `ModelInit` 创建。 |
| `model_500000_900.onnx` | 现场模型文件（示例/部署用）。 |
| `图片/` | 测试用 CT 图。 |
| `CT离线AI视觉接口文档.md` | 官方接口表（V1.0，2026-07-27）。 |
| `halcondotnet.dll`（环境依赖，仓库内可能未带齐） | Halcon 22.11 的 .NET 封装。 |

### 2.1 CF 做什么

- 加载 `.onnx`，建 `InferenceSession`；
- 可选 CUDA（代码内有约 18GB 显存限额配置）；
- `Predict(Mat)`：图像 → Blob 归一化 → 推理 → 单通道结果图缩回原尺寸。

**CF 不负责电芯业务逻辑。**

### 2.2 NJCT 做什么

- 对外提供 `ModelInit` / `ModelAction` / `ModelAction_origin`（文档接口）；
- 另有 `ActionGetPoint`（文档未列，更细的点分类输出，偏内部/旧流程）；
- 流程：读图 → 裁 ROI → CF 推理 → Halcon 找电芯头/内层/抓点 → 输出结果。

### 2.3 关系示意

```
上位机(反射调用)
    │
    ▼
NJCT.NJSwdCT  ──ModelInit/Predict──►  CF.SeDPredictor  ──►  *.onnx (+CUDA)
    │
    └── Halcon 后处理 ──► ResultImg + 抓点/坐标/尺寸 + RunInfo
```

---

## 3. 运行环境依赖（接入前必须满足）

按接口文档 + 代码引用：

| 项 | 要求 |
|---|---|
| 语言/框架 | C# / **.NET Framework 4.8**（文档写 C#8.0 + Net Framework4.8） |
| 业务 DLL | `NJCT.dll`（版本变更时文件名可能变，以现场为准） |
| 推理 DLL | `CF.dll`（与 NJCT 同目录或按依赖可解析） |
| 视觉库 | `halcondotnet.dll` **Halcon 22.11** |
| AI 运行时 | ONNX Runtime **1.20.1** + **CUDA 12.4**（若 `IsRunCuda=true`） |
| 图像库 | OpenCvSharp（CF/NJCT 均使用） |
| 调用方式 | **反射动态调用**（DLL 放服务器，非本地工程强引用——按文档要求） |

初始化失败属于严重错误，不能继续推理。

---

## 4. 接入调用顺序（推荐）

```
1. 反射加载 NJCT.dll，创建 NJSwdCT 实例
2. 调用 ModelInit(ModelPath, out handle, out ExInfo, IsRunCuda?)
   └─ handle == null 则失败，记录 ExInfo，停止
3. 对每张图调用：
   - ModelAction(...)           // 需要增强结果图
   或
   - ModelAction_origin(...)    // 需要原图尺度结果图/坐标
4. 检查 RunInfo；为空表示成功
5. 使用 ResultImg / 坐标 / 尺寸；释放 List<HObject> 中各对象
6. 进程退出前 Dispose 模型句柄（IPredictor : IDisposable）
```

**注意：** 模型初始化通常**整机启动做一次**，不要每张图都 `ModelInit`。

---

## 5. 接口清单与参数说明

> 关键原则：**不是文档里所有字段都要外部“填业务值”**。  
> - `Input`：外部给出  
> - `Out`：接口返回，调用方用 `out` 接住即可  

### 5.1 ModelInit — 模型初始化

**命名空间：** `NJCT`  
**类：** `NJSwdCT`  
**方法：** `ModelInit`

| 参数 | I/O | 必填 | 类型 | 谁给 | 说明 |
|---|---|---|---|---|---|
| `ModelPath` | In | Y | string | 外部 | 模型绝对路径，后缀需含 `onnx` |
| `ModelHandle` | Out | Y | `IPredictor` | 接口返回 | 成功非 null；失败 null |
| `ExInfo` | Out | Y | string | 接口返回 | 成功 `""`；失败写原因 |
| `IsRunCuda` | In | N | bool | 外部可选 | 默认 `true` |

代码行为摘要：

- 路径不存在 → `ExInfo = "模型路径不存在，请检查相关输入"`
- 后缀异常 → `ExInfo = "模型文件后缀异常"`
- 其它异常 → `ExInfo = "模型加载失败：" + Message`
- 成功 → `SeDPredictor.Create(ModelPath, useCuda: IsRunCuda)`

### 5.2 ModelAction — 推理（增强图显示）

| 参数 | I/O | 必填 | 类型 | 谁给 | 说明 |
|---|---|---|---|---|---|
| `ImgPath` | In | Y | string | 外部 | 检测图像完整路径 |
| `Models`/`ModelHandle` | In | Y | `IPredictor` | 外部（来自 Init） | 复用初始化句柄 |
| `BlackHeight` | In | Y | int | 外部 | 图片下方打黑高度（像素） |
| `ResultImg` | Out | Y | `HObject` | 接口返回 | 增强结果显示图 |
| `AllPointRegion` | Out | Y | `List<HObject>` | 接口返回 | 正常长度 2：各电芯抓点区域 |
| `AllRegionRow` | Out | Y | `List<HTuple>` | 接口返回 | 正常长度 2：抓点 row（L1~L5） |
| `AllRegionCol` | Out | Y | `List<HTuple>` | 接口返回 | 正常长度 2：抓点 col（L1~L5） |
| `AllPrintfValue` | Out | Y | `List<HTuple>` | 接口返回 | 正常长度 2：抓点尺寸数据 |
| `RunInfo` | Out | Y | string | 接口返回 | 成功 `""`；异常写提示 |

内存：`List<HObject>` 需**手动释放各索引对象**，否则可能泄漏。

### 5.3 ModelAction_origin — 推理（原图显示）

输入/输出字段与 `ModelAction` 相同；差别见第 6 节。  
`AllPrintfValue` 文档注明为**像素点**尺寸。

### 5.4 外部真正需要准备的输入（精简）

| 输入 | 何时需要 |
|---|---|
| `ModelPath` | 初始化一次 |
| `IsRunCuda` | 可选，默认 true |
| `ImgPath` | 每张图 |
| `ModelHandle` | 每张图（复用 Init 结果） |
| `BlackHeight` | 每张图（文档无默认值，代码也无默认） |

**常见误解纠正：**  
不是“只给一张图就行”。至少还要有**已加载模型**和 **BlackHeight**。  
也不是“只返回一张做好的图”：还会返回抓点区域、坐标、尺寸、`RunInfo`。

---

## 6. 增强图 vs 原图：是否同一套检测？

**检测主流程基本相同，最终显示与坐标尺度不同。**

两边共同步骤（代码）：

1. `Cv2.ImRead` 读图  
2. 裁剪 ROI：`Rect(0, 0, 1844, 1500)`（硬编码）  
3. `ModelHandle.Predict`  
4. 转 Halcon 图并 **Zoom ×2**  
5. `GetDianXinTopNC` → `GetDianXinNeiCengLinesNC` → 对 index=1,2 做 `GetDianXinTwoCengRegionNC`  
6. 底部按 `BlackHeight` 打黑  

差异：

| 项 | `ModelAction` | `ModelAction_origin` |
|---|---|---|
| 结果图 | 处理后灰度图 + `CoherenceEnhancingDiff` 相干增强 | 重新 `ReadImage` 原图再转灰度 |
| 抓点 Region | 不额外缩放 | `ZoomRegion(0.5)` |
| Row/Col | 留在 2 倍坐标系 | 除以 2，贴近原图像素 |
| 适用 | 看增强效果、人工观察 | 叠原图显示、按原图像素对位 |

**不要混用两边的坐标。**

---

## 7. 各 Out 参数用途（给上位机怎么用）

| Out | 用途 |
|---|---|
| `ResultImg` | 界面显示、存图、人工复核 |
| `AllPointRegion` | 抓点区域；可叠加显示、区域分析；用完释放 |
| `AllRegionRow` / `AllRegionCol` | 精确点位（约 L1~L5）；画点、对位、记录 |
| `AllPrintfValue` | 尺寸数据；OK/NG 判定、报表、MES |
| `RunInfo` / `ExInfo` | 成败与异常原因；必须记录日志 |

实务取舍：

- 只关心画面 → `ResultImg` + `RunInfo`
- 产线要数据 → `AllRegionRow/Col` + `AllPrintfValue` 为主
- 要画区域轮廓 → `AllPointRegion`

正常时列表长度一般为 **2**（两个电芯）。异常时文档写部分结果可能为 null；代码路径上失败时常通过 `RunInfo` 提示并提前返回，接入时务必先判 `RunInfo`。

---

## 8. 内部实现要点（排查/联调用）

以下为反编译代码观察到的硬编码/行为，接入时需知情：

1. **ROI 固定裁剪** `1844 × 1500`（从左上角）。输入图若尺寸不足或构图不符，可能异常或效果不对。  
2. AI 结果会 **放大 2 倍** 再做 Halcon 算法。  
3. 期望选出 **2 个电芯** 做抓点循环（`for index = 1; index < 3`）。  
4. 常见异常文案（不完全列表）：  
   - 电芯数量异常  
   - 电芯头部异常 / 头部间隙异常  
   - 电芯内层间隙异常  
   - 抓点异常 / 内层抓点数量异常  
   - **当前图像暂不支持检测** ← 这是 NJCT 最外层 `catch` 的兜底文案，**不等于业务判定**；真实异常被吞掉。  
    本仓库 `OfflineCtVisionService` 在收到该文案后会 **自动检测** 并改写 RunInfo 为「具体原因」：  
    输入图尺寸（须 ≥1844×1500）、ROI 平均灰度（过暗判定）、D: 可写空间、Halcon 许可、非 ASCII 路径等；**不再罗列多选让人猜**。  
5. `ConvertMat32FC1ToHObject`：非 `CV_32FC1` 时会 `ImWrite("D:\\TEST.png")` 再 `ReadImage`；**必须保证 D: 可写且有足够剩余空间**。  
6. `ActionGetPoint`：输出按 Z45 / F45 / F0 / 内层等点区域分类，**不在官方接口文档中**；除非明确要求，接入优先用文档三个接口。  
7. 文档写 `ModelHandle` 来源 `IMEYModel.dll`，本仓库实际类型在 **`CF.IPredictor` / `CF.SeDPredictor`**，以本仓库为准。

本仓库 `OfflineCtVisionService` 已做防护：设置 `HALCON_LICENSE_FILE`、推理前检查 D: 剩余空间、非 ASCII 路径自动转存到临时 ASCII 文件；失败时由 `OfflineCtImageDiagnostics` 给出具体原因。

---

## 9. 伪代码示例（逻辑参考）

```csharp
// 反射创建 NJCT.NJSwdCT 实例 nj（具体反射代码按现场框架）

string exInfo;
CF.IPredictor handle;
nj.ModelInit(@"D:\path\model_500000_900.onnx", out handle, out exInfo, IsRunCuda: true);
if (handle == null)
{
    // 记录 exInfo，禁止继续
    return;
}

string runInfo;
HalconDotNet.HObject resultImg;
List<HalconDotNet.HObject> allPointRegion;
List<HalconDotNet.HTuple> allRegionRow, allRegionCol, allPrintfValue;

int blackHeight = ???; // 现场工艺参数，必须明确

// 选其一：
nj.ModelAction(imgPath, handle, blackHeight,
    out resultImg, out allPointRegion, out allRegionRow,
    out allRegionCol, out allPrintfValue, out runInfo);

// 或：
// nj.ModelAction_origin(imgPath, handle, blackHeight, ...);

if (!string.IsNullOrEmpty(runInfo))
{
    // 检测失败：记日志，按业务决定是否仍显示 resultImg
}
else
{
    // 使用 resultImg / allRegionRow / allRegionCol / allPrintfValue
}

// 释放 allPointRegion 中每个 HObject
// 进程结束：handle.Dispose()
```

---

## 10. 已澄清的理解（会话结论）

1. **CT**：业务场景名（离线电芯 CT 正极视觉检测）。  
2. **CF**：推理引擎 DLL，不是业务 API。  
3. **NJCT**：对外业务 DLL，接入方主要对接它。  
4. 文档参数**不全是外部输入**；只有 `Input` 需要外部给出，`Out` 是返回值。  
5. “只给图拿做好的图”——**大体方向对，但不完整**：还要模型句柄与 `BlackHeight`；返回也不止一张图。  
6. 增强/原图两接口：**同一套检测，显示与坐标尺度不同。**

---

## 11. 疑问与待现场确认清单（给下一会话跟进）

以下问题当前仓库**尚未从文档/代码完全钉死**，接入前建议向算法/现场负责人确认：

### 11.1 参数与工艺

1. **`BlackHeight` 推荐值 / 取值范围是多少？** 与相机分辨率、工装底部遮挡如何对应？  
2. **输入图必须满足的分辨率、位深、通道、命名规则？** ROI 固定 1844×1500，原图是否必须 ≥ 该尺寸？  
3. **L1~L5 分别对应什么物理/工艺含义？** 顺序是否固定？  
4. **`AllPrintfValue` 每个元素具体含义与单位？**（文档写像素；几个数、是否已换算 mm？）  
5. OK/NG 判定阈值是在 **DLL 内** 还是 **上位机** 用返回值自己判？

### 11.2 部署与环境

6. 反射加载时，`NJCT.dll`、`CF.dll`、Halcon、ONNX CUDA 的**标准目录布局**是什么？  
7. 无 GPU / CUDA 失败时，`IsRunCuda=false` 是否为正式兜底方案？性能是否可接受？  
8. 文档写依赖 `IMEYModel.dll`，本仓库是 `CF.dll`——**现场最终以哪个程序集名为准？**  
9. DLL 文件名“随版本变更”——版本如何识别、如何兼容？

### 11.3 坐标与显示

10. 上位机画点时，**增强接口返回坐标**应对齐到哪张图？是否必须只用 `ModelAction_origin`？  
11. `ResultImg` 的宽高与输入图、与 Row/Col 坐标系如何严格对应？  
12. 异常时 `AllPointRegion` 等是空 List、null、还是部分有值？**以代码实测为准需补测试。**

### 11.4 资源与稳定性

13. `HObject` / `List<HObject>` 的标准释放写法（现场是否有统一工具方法）？  
14. 模型句柄是否线程安全？**能否多线程并行 `ModelAction`？**（当前未见文档说明）  
15. 长时间运行是否有已知内存增长问题（Halcon 未释放、ONNX session 等）？

### 11.5 业务范围

16. 本接口是否**仅正极**？负极/其它工位是否另一套 DLL？  
17. `ActionGetPoint` 是否允许上位机使用？与 `ModelAction` 数据是否一一对应？  
18. 测试图 `图片/` 的合格样例期望输出（坐标/尺寸样例）是否有金标准？

---

## 12. 下一会话建议动作

1. 按本文第 4~5 节写最小反射调用 Demo（先 Init，再对 `图片/` 跑一张）。  
2. 分别跑 `ModelAction` 与 `ModelAction_origin`，对比 `ResultImg` 与 Row/Col 数值差异并记录。  
3. 向现场确认第 11 节清单，尤其是 **BlackHeight、L1~L5、PrintfValue、坐标系**。  
4. 补一份“成功/失败”样例日志与释放代码模板。  
5. 若需二次开发，以 `NJCT/NJSwdCT.cs`、`CF/*.cs` 为阅读入口；注意其为反编译代码，可读性差但行为可信。

---

## 13. 关键速查表

| 问题 | 答案 |
|---|---|
| 接哪个 DLL？ | 主要接 **NJCT**；依赖 **CF** + Halcon + ONNX |
| 入口类？ | `NJCT.NJSwdCT` |
| 必调接口？ | `ModelInit` + (`ModelAction` 或 `ModelAction_origin`) |
| 外部最少输入？ | 模型路径、图片路径、BlackHeight（+可选 CUDA） |
| 返回什么？ | 结果图、2 电芯抓点区域/坐标/尺寸、RunInfo |
| 增强和原图区别？ | 检测同套路；图效果与坐标尺度不同 |
| 文档所有参数都要填？ | **否**，只填 Input；Out 是返回 |

---

*本文基于当前工作区资源与既有会话分析整理，若与现场最新 DLL/文档冲突，以现场交付物为准，并回写本文第 11 节结论。*

---

## 14. 相关分析文档

- **`docs/NJCT_1jpg失败原因分析.md`**（2026-07-29）：对照 `C:\AutoSlice\output\test` 样本与 `C:\AutoSlice\NJCT` / `CF` 反编译，说明失败图在「ROI 均值≥100」时仍可能因 **AI mask 亮区不足 + `GetDianXinTopNC` 的 `Threshold(200)`** 报电芯数量/头部异常；与 VG 直方图提亮直接相关。
