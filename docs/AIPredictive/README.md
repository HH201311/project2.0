# AIPredictive（AI 预测分析）文档目录

AI 预测分析业务模块，基于 SUNWODA SEVB（`develop`）平台。当前为**项目基础结构骨架**（可编译、菜单可进空页），业务定位待确认。

## 怎么用（新会话）

1. **只拖拽** [AIPredictive进度存档.md](./AIPredictive进度存档.md)（索引 + 当前状态）  
2. 需要需求 / 架构 / 资料清单时，按存档 **§0 文档索引** 再打开规划  
3. 追溯历史时再打开 [AIPredictive版本变更记录.md](./AIPredictive版本变更记录.md)  
4. 任务结束后：更新进度存档待办 + 在版本变更记录顶部追加一条；规格变了再改规划对应节  

## 文档分工

| 文档 | 角色 | 何时改 |
|------|------|--------|
| [AIPredictive进度存档.md](./AIPredictive进度存档.md) | **热层 / 索引**：状态、待办、代码入口、指向详规的链接 | 每次开发后 |
| [AIPredictive版本变更记录.md](./AIPredictive版本变更记录.md) | **变更历史**：背景 / 做法 / 落点（按日倒序） | 每次开发后 |
| [AIPredictive项目开发规划.md](./AIPredictive项目开发规划.md) | **冷层 / 详规**：范围、架构、资料与对接说明 | 需求或设计变更时 |
| [开发资料/](./开发资料/) | **原始资料投放区**（已投放 3 份；其中 2 份点位 txt 已转为 CSV） | 现场资料变更时 |
| [点位表 CSV](./AIPredictive点位表_伺服.csv) | **点位数据真相**（[伺服](./AIPredictive点位表_伺服.csv) / [气缸](./AIPredictive点位表_气缸.csv)，UTF-8 BOM + CRLF，列定义见规划 §2.2.1） | 现场点位变更时 |
| CSV / SQL | **数据真相**（整理后的定稿） | 录入或现场变更后 |

## 附件

| 文件 | 说明 |
|------|------|
| [enable_aipredictive_module.sql](./enable_aipredictive_module.sql) | 启用模块 SQL（已在 `sunwoda_demo` 执行） |
| [AIPredictive点位表_伺服.csv](./AIPredictive点位表_伺服.csv) | 伺服点位表（10 列 × 920 行；源自 `开发资料/伺服.txt`） |
| [AIPredictive点位表_气缸.csv](./AIPredictive点位表_气缸.csv) | 气缸点位表（12 列 × 1674 行；源自 `开发资料/气缸.txt`） |

## 平台文档

- [Agent协作开发规程](../Agent协作开发规程.md) — 建项目 / 会话规则 / Prompt 规程
- [项目架构说明](../项目架构说明.md)
- [新增业务模块上手指南](../新增业务模块上手指南.md)
- [docs/README.md](../README.md) — 全仓库文档约定
