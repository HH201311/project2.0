# 【AI视觉】离线CT接口文档

罗奕雄 | 今天修改
✦ AI 速览

# 1. 视觉检测结果接口文档

| 编制日期 | 编制人 | 版本 | 编制内容 |
| --- | --- | --- | --- |
| 2026-07-27 | 罗奕雄 | V1.0 | 惠州离线CT正极接口 |

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

