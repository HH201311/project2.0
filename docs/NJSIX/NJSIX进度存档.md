# NJSIX 进度存档

> **文档版本**：2026-09-29  
> **状态**：南京口径已落地；结果根 **E:\六面检上位机软件**；期望版本 M06；自动启动；档位校验关；待真机联调（不急）。  
> **用途**：新会话 **只拖本文**；规格细节不写在本文，用下方索引跳转  
> **详规**：[NJSIX项目开发规划.md](./NJSIX项目开发规划.md)　**版本历史**：[NJSIX版本变更记录.md](./NJSIX版本变更记录.md)　**PLC**：[PLC配置说明.md](./PLC配置说明.md)　**算法**：[开发资料/dll/南京六面检算法接口文档.md](./开发资料/dll/南京六面检算法接口文档.md)

**进度图例**：✅ 已完成　🔄 进行中　⏸ 阻塞（待资料/待确认）　❌ 未开始

---

## 0. 文档索引（事实只写一处）

| 要查什么 | 去哪里 |
|----------|--------|
| **版本变更** | [NJSIX版本变更记录.md](./NJSIX版本变更记录.md) |
| 南京收图 / 10 相机 / 22 张 | 本文 §1 |
| 算法 DLL / NuGet | [开发资料/dll/](./开发资料/dll/)；`Core/Dlls/NJX13Pack.dll` |
| 配置键 / 队列 | [pack6配置键.csv](./pack6配置键.csv)、[pack6队列需求.csv](./pack6队列需求.csv)（南京口径） |
| PLC（暂禁用） | [PLC配置说明.md](./PLC配置说明.md) |
| 启用模块（只开 NJ） | [enable_njsix_module.sql](./enable_njsix_module.sql) |

---

## 1. 当前状态（南京规格）

| 项 | 口径 |
|----|------|
| 菜单 | **NJSIX**（`[Module]` 第二参数） |
| 收图根 | **Z:** 上盖 / **T:** 底部 / **S:** 四周；`日期\SN\图` |
| 10 窗 | 上盖(2) + 底1/2(各3) + 四周1/2/4/7(各1) + 四周3(2) + 四周5/6(各4) = **22 OK** |
| 命名 | 底/周：`Step{张序}_Cam{相机}_OK…`；上盖：`{SN}_Step{张序}_…`；**忽略 Window** |
| OCR | 四周2·Step1 = 同图双检；四周6·Step4 = 单独白标签 |
| 算法 | **`NJX13Pack`（net8.0）** |
| 报警 | **暂无**（无 PLC 写位、无 NG/缺张弹窗） |
| 本机模块 | **只开 NJSIX**；`VM_ZZSIXPage` 禁用 |
| 产品档位前缀 | 代码保留五档映射；**界面隐藏且不校验**（`EnforceProductCodeProfile=false`） |

### 待办

| # | 任务 | 状态 | 验收 |
|---|------|------|------|
| B1 | net8 `NJX13Pack` 切换引用 | ✅ | 编译 0 error |
| B2 | 真机联调 Z/T/S + 22 张 + 两路 OCR | ⏸ | 不急；有样张/产线再测 |
| B3 | NuGet Paddle/OpenCv | ✅ | 说明书 §3 |
| B4 | 文档 CSV/规划南京口径 | ✅ | 配置键/队列/规划/PLC 说明 |
| B5 | 默认路径 / 菜单 / 关报警 / 只开 NJ | ✅ | 见版本变更 |
| B6 | 删样张 / 合并离线按钮 / 去 ZZ 残余 | ✅ | 样张目录已删；离线 2 钮 |

### 已完成

- ✅ 10 窗 / Ingress / OCR / UI / `NJX13Pack`  
- ✅ 清理：默认 Z/T/S、菜单 NJSIX、无报警、只开 NJ、CSV 南京化、删样张与报警窗  

---

## 2. 关键代码入口

| 职责 | 路径 |
|------|------|
| 相机 / 文件名 | `Pack6CameraCatalog` / `Pack6FileNameParser` |
| 收图 | `Pack6IngressService` |
| 视觉 | `Pack6NjtwVisionEngine` → `NJX13Pack.AI` |
| PLC（空实现） | `Pack6PlcAlarmService` |
| 主页 | `VM_NJSIXPage` / `NJSIXPage.xaml` |
| 算法 DLL | `SUNWODA_SEVB.Core/Dlls/NJX13Pack.dll` |

---

## 3. 本机构建

SDK 8.0.424 MSBuild；DLL 应含 `.NETCoreApp,Version=v8.0`。完整退出 SEVB 后重启，菜单进「NJSIX」。
