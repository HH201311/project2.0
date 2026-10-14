# 案例：视觉大图未写完即读 → 半截图（下半灰）

> **级别**：P1（误检/漏检可升 P0）  
> **发现项目**：ZZSIX（PACK 六面检，2026-09-17 / 09-18）；同源管线 **NJSIX / YWSIX** 已带同样写稳逻辑  
> **规范条文**：`docs/代码开发规范.md` 条款 **D-06** / 专项细则 **R2**（「就绪与读图」须在消费侧，不得占用 Watcher 回调）  
> **审查入口**：`docs/仓库代码审查/一键代码审查.md` §D「FileSystemWatcher / 目录监控」  
> **出处**：`docs/ZZSIX/ZZSIX版本变更记录.md`（2026-09-17「收图写稳后再入队」；2026-09-18「JPEG EOI 写稳」）

## 现象

- 视觉侧落盘的 **Y/Z OK 原图**本身完整、可打开。
- 上位机归档图 / 预览图出现 **下半截灰（或黑）**，灰带高度不固定（有时大半、有时一小条）。
- 典型触发：相机图体积大、网络盘拷贝慢；`FileSystemWatcher` 已 `Created`，业务立刻 `ImRead` / 解码。

## 根因

1. Windows 上文件 **创建即可见**，并不等于 **写完**。视觉或拷贝进程仍在追加字节时，上位机已打开读取。
2. 读到的是**当时长度**的半截 JPEG/位图：解码器常把未写完区域填成灰/黑，看起来像「检测画坏了」，实际是**读早了**。
3. **仅判断「能 `File.Open`」不够**：共享读打开时，写进程仍可继续写；「可打开」≠「内容完整」。
4. **仅判断体积连续不变**仍可能半截：网络盘偶发「Length 先停住、尾部 EOI 晚落盘」，或拷贝中途 Length 短暂不变。

业务队列再快也救不了：**入队前读进内存的已是半截字节**。

## 正确做法（审查通过标准）

| 项 | 要求 |
|----|------|
| Watcher 回调 | 只做路径校验 + 占坑去重，**立即** `Task.Run` / 入消费侧；禁止在回调里 `Sleep` / 等写稳 / 读图 |
| 写稳（体积） | 消费侧轮询：`Length` 连续 **N 次不变**（样板：5×200ms）且 ≥ 最小字节、可共享打开 |
| JPEG 完整性 | `.jpg`/`.jpeg` 另校验尾部 **EOI `FF D9`**（可选再验头 SOI `FF D8`）；只读末 2 字节即可 |
| 超时策略 | 单文件有上限（样板 30s）；体积超时仍不可读 → **跳过并打日志**；体积已稳但等不到 EOI → **Warn 后仍入队**，避免卡死丢检 |
| 叠图/再读 | 用 `FileShare.Read`（或 `ReadWrite`）打开，勿长期独占锁挡视觉写盘 |
| 同源项目 | 六面检类 `Pack6IngressService`（ZZ / NJ / YW）须带写稳；其它产线自写 Watcher 收图时同样检查 |

## 反例 → 正例（已修样板）

| 时间 | 问题 | 修法 |
|------|------|------|
| 2026-09-17 | `Created` 即读 → 归档/预览下半灰 | 入队前体积连续不变且可读（当时 3 次，最长 30s）；叠图改 `FileShare.Read` |
| 2026-09-18 | 仅体积稳定仍偶发半截拷贝 | 升为 5×200ms；JPEG 等尾部 `FF D9`；超时无 EOI 仍 Warn 入队 |

**代码落点（样板）**：

- `SUNWODA_SEVB/Services/Project/ZZSIX/Pack6IngressService.cs`（`WaitForFileReadyAsync` / `TryHasJpegEoi`）
- 同源：`.../NJSIX/Pack6IngressService.cs`、`.../YWSIX/Pack6IngressService.cs`
- 叠图：`Pack6ResultOverlayHelper`（`FileShare.Read`）

## Agent / 人工审查时

变更 diff 出现 `FileSystemWatcher`、`OnCreated`、收图入队、`ImRead`/`Bitmap`/`Image.FromFile`、网络盘原图路径时：

1. 是否在 **回调线程**里等文件 / 读图？（违 D-06 / R2）  
2. 入队或解码前是否有 **体积写稳**？JPEG 是否校验 **EOI**？  
3. 超时是「卡死不入队」还是「可观测地跳过 / Warn 后继续」？  
4. 不合规 → 至少 P1；半截图导致批量误 NG/误 OK 时按产线影响升 P0。  
