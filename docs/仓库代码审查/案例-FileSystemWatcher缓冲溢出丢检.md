# 案例：FileSystemWatcher 缓冲溢出导致整夹丢检

> **级别**：P1（产线批量漏检可升 P0）  
> **发现项目**：NCLCCT（2026-09-22）  
> **规范条文**：`docs/代码开发规范.md` 条款 **D-06** / 专项细则 **R2**  
> **审查入口**：`docs/仓库代码审查/一键代码审查.md` §D「FileSystemWatcher / 目录监控」

## 现象

- 向监控目录**整夹复制**（含子目录）后，软件只检测极少张（甚至 1 张）。
- 右上角红字：`InternalBufferOverflowException: Too many changes at once in directory:…`

## 根因

1. Windows / .NET `FileSystemWatcher` 内部事件缓冲默认约 **8KB**，整夹突发 Created 易溢出。
2. 溢出后**未送达回调的事件被丢弃**，且不可恢复。
3. 若回调内再做 `Sleep` / `WaitFileReady` / `ImRead`，清空缓冲更慢，更容易溢出。
4. `Error` 仅弹窗、不重启、不补扫 → 漏检永久残留。

业务队列（如 `ConcurrentQueue`）再长也救不了：**事件在入队前就被 OS 丢掉了**。

## 正确做法（审查通过标准）

| 项 | 要求 |
|----|------|
| 回调 | 只路径校验 + 入队，立即返回 |
| 就绪/读图/算法 | 放在消费线程 |
| 缓冲 | `InternalBufferSize = 64 * 1024` |
| 子目录 | `IncludeSubdirectories = true`（若业务需要） |
| Error | 溢出 → 重启 Watcher + 监控根符合规则的全量补扫入队（去重） |

## 反例位置（已修）

- `SUNWODA_SEVB/ViewModels/Pages/NCLCCT/VM_NCLCCTPage.cs`（2026-09-22）
- 同源迁移的 CTNCLX 等页面若仍「回调内读图」，审查时应同样按 D-06 / R2 标出。

## Agent / 人工审查时

变更 diff 出现 `FileSystemWatcher`、`OnCreated`、`IncludeSubdirectories`、`InternalBuffer`、监控路径切换时：

1. 对照规范 R2 检查表逐条勾选。  
2. 不合规 → 至少 P1，要求修复后再合入/上线。  
