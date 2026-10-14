# 仓库代码审查

傻瓜式用法：实现做完后**新开对话**，把 **[一键代码审查.md](./一键代码审查.md)** 拖给 Agent，回「全部默认」（阶段一可修 + 阶段二需求点检只验）。或工作流点「提交前正式审查」。

| 文件 | 说明 |
|------|------|
| [一键代码审查.md](./一键代码审查.md) | **唯一入口**：阶段一代码审查 + 阶段二需求点检 |
| [审查策略-需求点检与单元测试.md](./审查策略-需求点检与单元测试.md) | 点检策略：自拟断言、只验不补功能；样板 `docs/BoxGluing/` |
| [案例-FileSystemWatcher缓冲溢出丢检.md](./案例-FileSystemWatcher缓冲溢出丢检.md) | 整夹复制丢检教训；规范 D-06 / R2 对照 |
| [案例-收图半截图写稳EOI.md](./案例-收图半截图写稳EOI.md) | 视觉大图未写完即读 → 下半灰；体积写稳 + JPEG EOI；规范 D-06 / R2 |
| `审查报告-YYYYMMDD-HHmm.md` | 框架/多项目报告落于此；单产线优先 `docs/<项目>/`（仅命中审查策略 §4 触发条件时产出） |

写法标准见 [代码开发规范](../代码开发规范.md)（第二篇 **上位机红线 A–H**；专项细则 **R1 长跑内存**、**R2 FileSystemWatcher 防丢事件**、**R3 正式文件原子覆盖**）。

**与目录监控相关的历史教训**：

- NCLCCT 整夹复制丢检（`InternalBufferOverflowException`）→ 规范 D-06 / R2；审查触及 Watcher 时按一键审查 §D「FileSystemWatcher」条必查。
- ZZSIX 收图半截图（归档/预览下半灰：Created 即读）→ 消费侧体积写稳 + JPEG 尾部 EOI；见 [案例-收图半截图写稳EOI.md](./案例-收图半截图写稳EOI.md)。NJSIX / YWSIX 同源 `Pack6IngressService` 已带该逻辑。

**与落盘相关的历史教训**：NCLCCT 模型/DLL `.downloading` 覆盖、结果 CSV `.cache` 临时合并 → 规范 D-05 / R3；审查触及下载覆盖或 CSV 写盘时按一键审查 §D「正式文件落盘」条必查。
