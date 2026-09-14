# 枣庄TW\_Pack六面检算法接口文档说明书

|项目|内容|
|---|---|
|算法库名称|ZZTWPack\.dll（命名空间 `ZZTWPack`，类 `AI`）|
|文档版本|V1\.0|
|更新日期|2026\-09\-08|
|适用工位|P05 白标签三码识别 / 铭牌产品编码识别|

---

## 1\. 功能概述

算法库基于 **Halcon（二维码定位解码）\+ PaddleOCR 飞桨（印刷体文字识别）** 实现，对外提供两个核心接口：

|接口|功能|输出|
|---|---|---|
|`DecodeThreeCodesByOCR`|白标签图：定位二维码后按相对位置提取三码区域并解码|二维码信息、零部件序列码（纯数字）、产品编码，及三个显示区域|
|`DecodeNameplateProductSNByOCR`|铭牌图：通过框线结构定位产品编码单元格并解码|产品编码，及显示区域|

识别结果内置易混淆字符校正：

- 零部件序列码按"必为纯数字"先验，将形近字母映射为数字（O→0、I→1、B→8 等），其余非数字字符剔除；

- 产品编码支持自定义编码规则（`productSNRule`）按位校正：字母位将形近数字转为字母（0→O、1→I 等），数字位将形近字母转为数字。

---

## 2\. 运行环境要求

|项目|要求|
|---|---|
|操作系统|Windows x64|
|上位机运行时|\.NET 8 及以上（**不支持 \.NET Framework 4\.x 直接加载**；进程必须为 x64）|
|Halcon|halcondotnet\.dll \+ halcon\.dll，需有效运行许可（license）|
|VC\+\+ 运行库|建议安装 Visual C\+\+ 2015\-2022 Redistributable \(x64\)，未安装则需随包部署 msvcp140\*\.dll / concrt140\.dll|

---

## 3\. 部署清单

上位机**手动添加的文件 / 安装的包**如下：

|类型|名称|版本|说明|
|---|---|---|---|
|文件引用|`ZZTWPack.dll`|—|算法库|
|文件引用|`halcondotnet.dll`|与现场 Halcon 一致|Halcon 托管接口|
|NuGet 包|`OpenCvSharp4`|4\.13\.0\.20260627|OpenCV 托管程序集|
|NuGet 包|`OpenCvSharp4.runtime.win`|4\.13\.0\.20260627|OpenCV 原生库|
|NuGet 包|`PaddleOCRSharp`|6\.2\.0|PaddleOCR 封装库|
|NuGet 包|`Paddle.Runtime.win_x64`|3\.3\.0\.1|Paddle 推理原生库（**必须直接安装，见下方注意**）|

**注意：****`Paddle.Runtime.win_x64`**** 不能省略、也不能指望由 ****`PaddleOCRSharp`**** 传递带入**——其原生库通过普通 `build\` targets 复制，仅对直接安装它的工程生效。若缺失，运行时报 `Unable to load DLL 'PaddleOCR'`（实测验证）。

安装上述包后**自动带入、无需手动处理**的内容（不再逐项列出）：

- OpenCvSharpExtern\.dll、Paddle 原生推理库（paddle\_inference / mkldnn / tbb / opencv\_world470 等）

- System\.Drawing\.Common\.dll

- VC\+\+ 运行库 DLL

- OCR 模型 `inference\` 文件夹

---

## 4\. 接口详细说明

### 4\.1 白标签三码解码 `DecodeThreeCodesByOCR`

#### 重载一：带编码规则

```csharp
public string DecodeThreeCodesByOCR(HObject inputImage, string productSNRule,
    out HObject qrCodeRegion, out HObject partSNRegion, out HObject productSNRegion,
    out string qrData, out string partSN, out string productSN)
```

#### 重载二：无编码规则（等效于 productSNRule 传空串）

```csharp
public string DecodeThreeCodesByOCR(HObject inputImage,
    out HObject qrCodeRegion, out HObject partSNRegion, out HObject productSNRegion,
    out string qrData, out string partSN, out string productSN)
```

#### 参数说明

|参数|方向|类型|说明|
|---|---|---|---|
|inputImage|输入|HObject|白标签原图|
|productSNRule|输入|string|产品编码规则，见 4\.3 节；传 `""` 不做按位校正，仅保留数字\+大写字母|
|qrCodeRegion|输出|HObject|二维码区域（供显示叠加）|
|partSNRegion|输出|HObject|零部件序列码区域（供显示叠加）|
|productSNRegion|输出|HObject|产品编码区域（供显示叠加）|
|qrData|输出|string|二维码解码信息|
|partSN|输出|string|零部件序列码（纯数字）|
|productSN|输出|string|产品编码（数字\+大写字母）|
|返回值|—|string|**空串 = 成功**；否则为错误信息（见第 5 节）|

#### 处理流程

1. 二维码定位与解码（Halcon FindDataCode2d，自动去除结果末尾回车换行）；

2. 按二维码相对位置生成零部件序列码 ROI（右上方）与产品编码 ROI（右下方）；

3. 两个 ROI 分别经 PaddleOCR 识别（ROI 裁剪 → 文本框按左到右排序拼接）；

4. 序列码按纯数字校正，产品编码按编码规则校正。

### 4\.2 铭牌产品编码解码 `DecodeNameplateProductSNByOCR`

#### 重载一：带编码规则

```csharp
public string DecodeNameplateProductSNByOCR(HObject inputImage, string productSNRule,
    out HObject productSNRegion, out string productSN)
```

#### 重载二：无编码规则

```csharp
public string DecodeNameplateProductSNByOCR(HObject inputImage,
    out HObject productSNRegion, out string productSN)
```

#### 参数说明

|参数|方向|类型|说明|
|---|---|---|---|
|inputImage|输入|HObject|铭牌原图|
|productSNRule|输入|string|产品编码规则，见 4\.3 节|
|productSNRegion|输出|HObject|产品编码区域（供显示叠加）|
|productSN|输出|string|产品编码（数字\+大写字母）|
|返回值|—|string|空串 = 成功；否则为错误信息|

#### 处理流程

1. 通过铭牌表格横线（行带）\+ 竖线（左右边界）动态定位产品编码单元格 ROI；

2. ROI 经 PaddleOCR 识别；

3. 按编码规则校正易混淆字符后输出。

### 4\.3 编码规则 `productSNRule` 语法

规则字符串逐位描述产品编码格式，长度与编码位数一致：

|规则字符|含义|校正行为（该位识别结果不符时）|
|---|---|---|
|`L`|该位必须为大写字母|数字按形近映射转字母：0→O、1→I、2→Z、4→A、5→S、6→G、7→T、8→B|
|`D`|该位必须为数字|字母按形近映射转数字：O/Q/D/C→0、I/J/L→1、Z→2、A→4、S→5、G→6、T→7、B/E→8|
|其他字符|该位必须为该分隔符|强制还原为规则指定字符|

- 超出规则长度的字符仅保留数字和大写字母；

- 规则传空串 `""`：全部字符仅做"保留数字\+大写字母"过滤，不做按位校正。

示例：`"LLDDDDDD"` 表示前 2 位大写字母 \+ 后 6 位数字。若识别为 `"L0TPB1AL"` 且规则为 `"LLDDDDDD"`，则第 2 位 `0` 会被校正为 `O`。

---

## 5\. 返回错误信息表

|返回值|含义|上位机处理建议|
|---|---|---|
|`""`|成功|使用三个输出值|
|`未找到二维码`|图中未检出二维码|判 NG 或重拍|
|`零部件序列码OCR失败: 未识别到文本`|序列码区域未识别出字符|检查打码质量/光照|
|`产品编码OCR失败: 未识别到文本`|产品编码区域未识别出字符|检查打码质量/光照|
|`铭牌产品编码OCR失败: 未识别到文本`|铭牌区域定位失败或未识别出字符|检查铭牌框线成像|
|`零部件序列码OCR失败: 图像转换失败` 等|内部图像转换异常|检查输入图像有效性|
|`...异常: <详细异常信息>`|运行时异常（含异常消息）|结合详细消息排查（如缺 DLL、缺 license）|

---

## 6\. 调用示例

```csharp
using HalconDotNet;
using ZZTWPack;

AI ai = new AI();

//读入白标签图
HOperatorSet.ReadImage(out HObject image, @"D:\img\whiteLabel.jpg");

//白标签三码解码（编码规则按实际格式配置，无规则传 ""）
string err = ai.DecodeThreeCodesByOCR(image, "LLDDDDDD",
    out HObject qrCodeRegion, out HObject partSNRegion, out HObject productSNRegion,
    out string qrData, out string partSN, out string productSN);

if (err == "")
{
    //结果显示：区域叠加到 Halcon 窗口
    //HOperatorSet.DispObj(qrCodeRegion, hWindowHandle);
    //HOperatorSet.DispObj(partSNRegion, hWindowHandle);
    //HOperatorSet.DispObj(productSNRegion, hWindowHandle);

    //结果使用
    //textBoxQR.Text = qrData;
    //textBoxPartSN.Text = partSN;      //建议校验长度
    //textBoxProductSN.Text = productSN;
}
else
{
    //NG 处理：err 为错误信息
}

//区域由调用方负责释放（防内存泄漏）
qrCodeRegion?.Dispose();
partSNRegion?.Dispose();
productSNRegion?.Dispose();
image.Dispose();

//铭牌产品编码解码同理：
//string err2 = ai.DecodeNameplateProductSNByOCR(nameplateImage, "LLDDDDDD",
//    out HObject nameplateSNRegion, out string nameplateSN);
```

---

## 7\. 注意事项

1. **首次调用耗时**：第一次调用任一解码接口时加载 PaddleOCR 模型（约数秒），之后单张识别为毫秒级。建议程序启动或初始化阶段空跑一次预热。

2. **线程安全**：PaddleOCR 推理在库内部已加锁串行化，多线程调用不会崩溃，但会排队执行。

3. **资源释放责任**：所有 `out HObject` 区域参数由**调用方负责释放**（调用 `Dispose()`），否则造成 Halcon 内存泄漏；输入图像同理。

4. **结果长度校验**：校正机制可能剔除无法映射的字符导致输出变短，上位机应对序列码/产品编码做**长度校验**，长度不符判 NG 或触发重拍。

5. **启动日志**：控制台程序首次初始化会打印 PaddleOCRSharp 社区版横幅及 `ReduceMeanCheckIfOneDNNSupport` 调试日志，属正常现象，不影响功能；WinForms/WPF 无控制台时不显示。

6. **商用授权**：PaddleOCRSharp 当前为免费社区版（Free Community Edition），商用部署前请确认其授权条款。

---

## 8\. 验证记录

|测试集|数量|比对方式|结果|
|---|---|---|---|
|白标签（P05 前1工位）|8 张|二维码/序列码/产品编码 vs 文件名编码|8/8 一致|
|铭牌（P05 前3工位）|9 张|产品编码 vs 文件名编码|9/9 一致|

