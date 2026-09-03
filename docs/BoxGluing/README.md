# BoxGluing（口琴管涂胶）文档目录

口琴管涂胶业务模块，基于 SUNWODA SEVB（`develop`）平台。

## 怎么用（新会话）

1. **只拖拽** [BoxGluing进度存档.md](./BoxGluing进度存档.md)（索引 + 当前状态）  
2. 需要点位 / 架构 / 阈值时，按存档 **§0 文档索引** 再打开规划或 CSV  
3. 追溯历史时再打开 [BoxGluing版本变更记录.md](./BoxGluing版本变更记录.md)  
4. 任务结束后：更新进度存档待办 + 在版本变更记录顶部追加一条；规格变了再改规划对应节  

## 文档分工

| 文档 | 角色 | 何时改 |
|------|------|--------|
| [BoxGluing进度存档.md](./BoxGluing进度存档.md) | **热层 / 索引**：状态、待办、代码入口、指向详规的链接 | 每次开发后 |
| [BoxGluing版本变更记录.md](./BoxGluing版本变更记录.md) | **变更历史**：背景 / 做法 / 落点（按日倒序） | 每次开发后 |
| [BoxGluing项目开发规划.md](./BoxGluing项目开发规划.md) | **冷层 / 详规**：范围、架构、点位说明 | 需求或设计变更时 |
| CSV / SQL | **数据真相** | 现场资料变更时 |

## 附件

| 文件 | 说明 |
|------|------|
| [plc点位表.csv](./plc点位表.csv) | 过程量 / 启动 / 反馈 |
| [plc设备信息.csv](./plc设备信息.csv) | PLC 通讯 |
| [工艺阈值表.csv](./工艺阈值表.csv) | 工艺阈值 |
| [enable_boxgluing_module.sql](./enable_boxgluing_module.sql) | 启用模块 SQL |

## 平台文档

- [Agent协作开发规程](../Agent协作开发规程.md) — 建项目 / 会话规则 / Prompt 规程
- [项目架构说明](../项目架构说明.md)
- [新增业务模块上手指南](../新增业务模块上手指南.md)
- [docs/README.md](../README.md) — 全仓库文档约定
