# 【AI视觉】离线CT接口文档

罗奕雄 | 今天修改
✦ AI 速览

# 1. 视觉检测结果接口文档

| 编制日期 | 编制人 | 版本 | 编制内容 |
| --- | --- | --- | --- |
| 2026-07-27 | 罗奕雄 | V1.0 | 惠州离线CT正极接口 |
| 2026-08-01 | 上位机 | V1.1 | 补充四参数实测结构与样例（便于 AI 结果图绘制） |

## 1.1 模型初始化接口

**调用方式：反射动态调用【dll需放置于服务器，非本地引用调用】**

**Dll依赖环境：C#8.0 + Net Framework4.8 + CF.dll + halcondotnet.dll【22.11版本】 + ONNX包 (1.20.1版本 + 12.4Cuda)**

**Dll名称：x.dll 注：名称随着版本更新存在变更**

**命名空间：NJCT**

**接口类名称：NJSwdCT**

**接口方法名：ModelInit**

| 序号 | 是否必填 | I/O | 参数字段 | 数据类型 | 备注 |
| --- | --- | --- | --- | --- | --- |
| 1 | Y | Input | ModelPath | string | 模型文件绝对路径 |
| 4 | Y | Out | ModelHandle | IPredictor | 模型句柄（该类为自定义类，来源 IMEYModel.dll） |
| 5 | Y | Out | ExInfo | String | 接口异常返回信息 |
| 6 | N | Input | IsRunCuda | bool | 是否启用GPU【默认为true】 |

✏️ **成功返回结果**：模型句柄不为null，接口异常返回信息为””;
**异常返回接口**：异常情况见接口异常返回信息，模型句柄为null
**注**：模型初始化失败属于严重错误，无法进一步执行数据推理；

---

## 1.2 数据推理执行接口（增强效果显示接口）

**调用方式：反射动态调用【dll需放置于服务器，非本地引用调用】**

**Dll依赖环境：C#8.0 + Net Framework4.8 + CF.dll + halcondotnet.dll【22.11版本】 + ONNX包 (1.20.1版本 + 12.4Cuda)**

**Dll名称：x.dll 注：名称随着版本更新存在变更**

**命名空间：NJCT**

**接口类名称：NJSwdCT**

**接口方法名：ModelAction**

**注意：List<Hobject>需手动释放各索引Hobject对象，否则存在内存泄漏风险**

| 序号 | 是否必填 | I/O | 参数字段 | 数据类型 | 备注 |
| --- | --- | --- | --- | --- | --- |
| 2 | Y | Input | ImgPath | string | 检测图像完整路径（含后缀） |
| 3 | Y | Input | Models | Ipredictor | 模型句柄，调用模型初始化接口可得 |
| 4 | Y | Input | BlackHeight | int | 图片下方需打黑高度 |
| 5 | Y | Out | ResultImg | hobject | 结果显示图片【增强图】 |
| 6 | Y | Out | AllPointRegion | List<Hobject> | 正常：长度为2，返回每个电芯抓点区域<br><br>异常：null |
| 7 | Y | Out | AllRegionRow | List< Htuple > | 正常：长度为2，返回每个电芯抓点区域的row坐标【即L1~L5】<br><br>异常：null |
| 8 | Y | Out | AllRegionCol | List< Htuple > | 正常：长度为2，返回每个电芯抓点区域的col坐标【即L1~L5】<br><br>异常：null |
|  | Y | Out | AllPrintfValue | List< Htuple> | 正常：长度为2，返回每个电芯抓点尺寸数据 |
|  | Y | Out | RunInfo | String | 接口运行状态，成功返回””; 异常返回相关提示，需记录 |

---

## 1.3 数据推理执行接口（原图显示接口）

**调用方式：反射动态调用【dll需放置于服务器，非本地引用调用】**

**Dll依赖环境：C#8.0 + Net Framework4.8 + CF.dll + halcondotnet.dll【22.11版本】 + ONNX包 (1.20.1版本 + 12.4Cuda)**

**Dll名称：x.dll 注：名称随着版本更新存在变更**

**命名空间：NJCT**

**接口类名称：NJSwdCT**

**接口方法名：ModelAction_origin**

**注意：List<Hobject>需手动释放各索引Hobject对象，否则存在内存泄漏风险**

| 序号 | 是否必填 | I/O | 参数字段 | 数据类型 | 备注 |
| --- | --- | --- | --- | --- | --- |
| 2 | Y | Input | ImgPath | string | 检测图像完整路径（含后缀） |
| 3 | Y | Input | Models | Ipredictor | 模型句柄，调用模型初始化接口可得 |
| 4 | Y | Input | BlackHeight | int | 图片下方需打黑高度 |
| 5 | Y | Out | ResultImg | hobject | 结果显示图片【原图】 |
| 6 | Y | Out | AllPointRegion | List<Hobject> | 正常：长度为2，返回每个电芯抓点区域<br><br>异常：null |
| 7 | Y | Out | AllRegionRow | List< Htuple > | 正常：长度为2，返回每个电芯抓点区域的row坐标【即L1~L5】<br><br>异常：null |
| 8 | Y | Out | AllRegionCol | List< Htuple > | 正常：长度为2，返回每个电芯抓点区域的col坐标【即L1~L5】<br><br>异常：null |
|  | Y | Out | AllPrintfValue | List< Htuple> | 正常：长度为2，返回每个电芯抓点尺寸数据【像素点】<br><br>异常：null |
|  | Y | Out | RunInfo | String | 接口运行状态，成功返回””; 异常返回相关提示，需记录 |

---

## 1.4 四参数实测结构（用于 AI 结果图绘制）

> **实测日期**：2026-08-01  
> **样本**：`D:\3Ddeliever\input\1118_负极_147\1118_负极_147.png`（1845×3000）  
> **调用**：NJCT `BlackHeight=0`（DLL 打黑不用），CPU，未额外旋转  
> **重要**：以下结构来自当前仓库 `NJCT.dll`（惠州正极包）。**不要**套用 CTNCLX 旧页面对 16/51/10 的解析（那是另一代布局）。

### 1.4.1 坐标系与 ResultImg 对齐

| 接口 | ResultImg 尺寸（本次） | Row/Col 坐标系 | 绘制目标 |
| --- | --- | --- | --- |
| `ModelAction`（enhance） | ROI×2 → **3688×3000** | **2×**（相对算法裁剪区） | 叠在增强结果图上 |
| `ModelAction_origin`（origin） | ROI → **1844×1500** | **原图像素**（相对左上角裁剪区） | 叠在原图结果图 / 输入图左上 ROI |

硬编码裁剪：从输入图左上角取 **1844×1500**。  
origin 坐标约为 enhance 的 1/2（同点对位时不要混用）。

### 1.4.2 列表外层约定（两电芯）

成功时四个 Out 均为 `Count = 2`：

| 索引 | 含义 | 本次样例 Col（origin） |
| --- | --- | --- |
| `[0]` | 电芯 1（偏左） | 约 509 |
| `[1]` | 电芯 2（偏右） | 约 1355.5 |

每个电芯各自有一套 `AllPointRegion[i]` / `AllRegionRow[i]` / `AllRegionCol[i]` / `AllPrintfValue[i]`。

### 1.4.3 AllPointRegion — 抓点区域点集

| 项 | 实测 |
| --- | --- |
| 类型 | `List<HObject>`，长度 2 |
| 解析 | `Union1` → `Connection` → `AreaCenter` |
| 连通域数 | **每个电芯 30 点**（5 组 × 每组 6 点） |
| 几何 | 同一电芯内 **Col 基本恒定**（竖直线上的采样点）；Row 递增 |

**分组（按 Connection 索引顺序，Row 升序）：**

| 组序（建议对应 L） | 点索引 | 说明 |
| --- | --- | --- |
| 组0 | 0..5 | 6 个等距点 |
| 组1 | 6..11 | 6 个等距点 |
| 组2 | 12..17 | 6 个等距点 |
| 组3 | 18..23 | 6 个等距点 |
| 组4 | 24..29 | 6 个等距点 |

绘制建议：画小圆/十字标记 30 点；或只画每组首尾点。

### 1.4.4 AllRegionRow / AllRegionCol — L1~L5 端点

| 项 | 实测 |
| --- | --- |
| 类型 | `List<HTuple>`，长度 2 |
| 每个电芯 Length | **10**（不是 5，也不是旧版 16） |
| 语义 | **5 对端点**，对应 L1~L5：`(Row[2k], Col[2k])` ↔ `(Row[2k+1], Col[2k+1])`，k=0..4 |
| Col | 同一电芯内 10 个 Col **几乎相同**（竖直线段） |

**配对与 AllPrintfValue 关系（已用样例验算）：**

```
AllPrintfValue[k] ≈ |AllRegionRow[2k] - AllRegionRow[2k+1]| / 5
```

（每段 6 个抓点 → 5 个间距；Printf 即平均间距，单位：像素）

| k | 端点索引 | 含义（建议标注名） |
| --- | --- | --- |
| 0 | 0,1 | L1 |
| 1 | 2,3 | L2 |
| 2 | 4,5 | L3 |
| 3 | 6,7 | L4 |
| 4 | 8,9 | L5 |

绘制建议：对每个 k，在 `(rowA,col)`—`(rowB,col)` 画竖线（或双边括号），旁边标注 `AllPrintfValue[k]`。

### 1.4.5 AllPrintfValue — 尺寸数值

| 项 | 实测 |
| --- | --- |
| 类型 | `List<HTuple>`，长度 2 |
| 每个电芯 Length | **5**（对应 L1~L5） |
| 单位 | **像素**（origin 文档说明；enhance/origin 本次数值相同） |
| 含义 | 第 k 段抓点平均间距（见上式） |

换算到物理尺寸：`L_μm = N_px × OfflineCtPixelSizeUm`；`L_mm = L_μm / 1000`。

### 1.4.6 样例数据（origin，推荐用于叠原图）

输入：`1118_负极_147.png`；`RunInfo=""`（成功）。

**电芯 1（Cell[0]）**

| 参数 | 值 |
| --- | --- |
| AllRegionRow | `[816.5, 766.25, 524.25, 479.25, 434.25, 389, 618.25, 570.25, 717.25, 667.25]` |
| AllRegionCol | `[509, 509, 509, 509, 509, 509, 509, 509, 509, 509]` |
| AllPrintfValue | `[20.1, 18.0, 18.1, 19.2, 20.0]` |
| AllPointRegion | 30 点，Col≈508.5；Row 从 389 → 816（5×6） |

L 段展开（origin）：

| Lk | 端点 Row | Col | PrintfValue |
| --- | --- | --- | --- |
| L1 | 816.5 ↔ 766.25 | 509 | 20.1 |
| L2 | 524.25 ↔ 479.25 | 509 | 18.0 |
| L3 | 434.25 ↔ 389 | 509 | 18.1 |
| L4 | 618.25 ↔ 570.25 | 509 | 19.2 |
| L5 | 717.25 ↔ 667.25 | 509 | 20.0 |

**电芯 2（Cell[1]）**

| 参数 | 值 |
| --- | --- |
| AllRegionRow | `[808, 757.25, 519.25, 474.25, 430.25, 386.25, 611, 564.25, 708.25, 659.25]` |
| AllRegionCol | `[1355.5 × 10]` |
| AllPrintfValue | `[20.3, 18.0, 17.6, 18.7, 19.6]` |
| AllPointRegion | 30 点，Col≈1355 |

### 1.4.7 同一样例 enhance 对照（2× 坐标）

| 参数 | 电芯1 | 电芯2 |
| --- | --- | --- |
| AllRegionCol | `1018 × 10` | `2711 × 10` |
| AllRegionRow | `[1633, 1532.5, 1048.5, 958.5, 868.5, 778, 1236.5, 1140.5, 1434.5, 1334.5]` | `[1616, 1514.5, 1038.5, 948.5, 860.5, 772.5, 1222, 1128.5, 1416.5, 1318.5]` |
| AllPrintfValue | 与 origin **相同** `[20.1, 18, 18.1, 19.2, 20]` / `[20.3, 18, 17.6, 18.7, 19.6]` | |
| AllPointRegion | 30 点，Col=1018 / 2711 | |

### 1.4.8 上位机绘制步骤（已接入主流程）

主流程 AI 标注成功后，由 `OfflineCtResultOverlayHelper` 自动叠图再落盘（预览即最终标注图）：

1. `DispObj(ResultImg)`  
2. `DispObj(AllPointRegion[i])`（绿色 Region）  
3. `DispLine` + `DispCross`：L1~L5 端点对  
4. `DispText`：图上旁注 `Lk:xx.xpx`  
5. **底部信息遮罩**：高度由界面 **`OfflineCtBlackHeight`（你自行配置）** 决定；上位机在 ResultImg **底部覆盖**打黑并写 L1~L5（与二次手动标注同一项）。传给 NJCT 的 `BlackHeight` **固定 0**，不用 DLL 打黑。  



实现位置：

- `SUNWODA_SEVB.Tool/Helper/OfflineCtResultOverlayHelper.cs`
- `OfflineCtVisionService.Infer*` 成功写盘前调用
- `VGAutomationService.OfflineCtVision` 传入像素精度与界面 `OfflineCtBlackHeight`
