# NJSIX 项目开发规划

> **注意（2026-09-28）**：南京规格已落地——`NJX13Pack`（net8）、10 相机 / Z·T·S、OCR 按 Cam+Step 路由；**暂无 PLC 报警**；菜单 **NJSIX**。CSV 为文档真相，运行时以 `Pack6CameraCatalog` / `Pack6Config` 代码默认为准。

> 文档版本：2026-09-28（南京口径）  
> **角色：冷层详规** — 范围、架构、资料说明；**不要**在本文维护每日进度  
> **进度 / 待办 / 索引**：[NJSIX进度存档.md](./NJSIX进度存档.md)  
> 平台：SUNWODA SEVB（`develop`，.NET 8 WPF）  
> 对照源：仓库内 `ZZSIX`（枣庄）；南京差异见 §1  
> 维护人：接手开发者  

---

## 0. 开发资料录入（工作流）

> **状态**：算法 DLL + 接口文档已录入；样张目录已按需删除（以后现场再补）  
> **整理后真相**：[pack6配置键.csv](./pack6配置键.csv)、[pack6队列需求.csv](./pack6队列需求.csv)、[开发资料/dll/](./开发资料/dll/)、[校验规则/README.md](./开发资料/校验规则/README.md)（若仍有 ZZ 表述以 CSV/进度存档为准）  
> 卡扣/螺栓等其它检项上线前，新算法资料放入 `开发资料/` 后再增量录入。

---

## 1. 项目定位与目标

### 1.1 业务定位

**NJSIX** = **PACK 六面检（南京）** 上位机模块。  
收图：**Z:** 上盖 / **T:** 底部 / **S:** 四周；**10 逻辑相机、合计 22 张 OK**。  
算法：**`NJX13Pack`**（检测项仍对外称「三码校验」）。  
**报警**：南京现场暂无 PLC / 弹窗报警。  

OCR 机位（南京）：

| 取图 | 机位 | 模式 | 算法 |
|------|------|------|------|
| 同图双检 | **四周相机2 · Step1** | 白标签三码 + 铭牌产品码 | `DecodeThreeCodesByOCR` + `DecodeNameplateProductSNByOCR` |
| 单独白标签 | **四周相机6 · Step4** | 白标签三码 | `DecodeThreeCodesByOCR` |

命名：底/周 `Step{张序}_Cam{相机}_OK…`；上盖 `{SN}_Step{张序}_…`；**忽略 Window**；只收 OK。

### 1.1.1 范围决策

> **决策**：现阶段以码检测为主；其它缺陷检项待资料后再扩。  
> **报警**：暂取消；日后恢复见 [PLC配置说明.md](./PLC配置说明.md)。

### 1.1.2 条码校验（摘要）

以文件夹 SN 为基准；白标签要求二维码/产品码一致、交叉位、版本号（`ExpectedWhiteLabelVersion`，默认 M06）；铭牌产品码与文件夹一致。详规仍可参考校验规则 README，机位以本节 OCR 表为准。

### 1.1.3 十相机与齐套

见 [pack6队列需求.csv](./pack6队列需求.csv) 与代码 `Pack6CameraCatalog`（合计 22）。

### 1.1.4 配置键

见 [pack6配置键.csv](./pack6配置键.csv)。路径键名兼容旧库（`YOkRootPath`=`Z:\` 上盖，`ZOkRootPath`=`T:\` 底部）。

---

## 2. 架构要点

| 层 | 说明 |
|----|------|
| View / VM | `NJSIXPage` / `VM_NJSIXPage`（菜单名 NJSIX） |
| Ingress | 三根目录 Watcher → 解析 Cam/Step → 入队 |
| Pipeline | 齐套 / 缺张超时 / 汇总 |
| Vision | `Pack6NjtwVisionEngine` → `NJX13Pack` |
| PLC | `Pack6PlcAlarmService` **空实现** |
| 配置 | `Pack6ConfigService` → `project_setting`（`BelongToVM=VM_NJSIXPage`） |

模块启用：[enable_njsix_module.sql](./enable_njsix_module.sql)（本机禁用 `VM_ZZSIXPage`）。

---

## 3. 非目标 / 暂不做

- 真机联调（⏸ 不急）  
- PLC 报警写位与弹窗  
- 样张目录（已删，需要时再补）  
- 其它缺陷算法检项  

---

## 4. 维护约定

进度只写 [NJSIX进度存档.md](./NJSIX进度存档.md)；每次代码变更追加 [NJSIX版本变更记录.md](./NJSIX版本变更记录.md)。点位/路径/阈值以 CSV + 代码目录为准，禁止臆造。
