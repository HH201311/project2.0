# 仓库代码审查

傻瓜式用法：把 **[一键代码审查.md](./一键代码审查.md)** 拖给 Agent，回「全部默认」或答几道题即可。

| 文件 | 说明 |
|------|------|
| [一键代码审查.md](./一键代码审查.md) | **唯一入口**：完整 Prompt（先问答再审查） |
| [案例-FileSystemWatcher缓冲溢出丢检.md](./案例-FileSystemWatcher缓冲溢出丢检.md) | 整夹复制丢检教训；规范 D-06 / R2 对照 |
| `审查报告-YYYYMMDD-HHmm.md` | 框架/多项目报告落于此；单产线优先 `docs/<项目>/` |

写法标准见 [代码开发规范](../代码开发规范.md)（第二篇 **上位机红线 A–H**；专项细则 **R1 长跑内存**、**R2 FileSystemWatcher 防丢事件**、**R3 正式文件原子覆盖**）。

**与目录监控相关的历史教训**：NCLCCT 整夹复制丢检（`InternalBufferOverflowException`）→ 规范 D-06 / R2；审查触及 Watcher 时按一键审查 §D「FileSystemWatcher」条必查。

**与落盘相关的历史教训**：NCLCCT 模型/DLL `.downloading` 覆盖、结果 CSV `.cache` 临时合并 → 规范 D-05 / R3；审查触及下载覆盖或 CSV 写盘时按一键审查 §D「正式文件落盘」条必查。
