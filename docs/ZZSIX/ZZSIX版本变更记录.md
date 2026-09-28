# ZZSIX 版本变更记录

> **用途**：按日期倒序记录「做了什么、为什么、落在哪」；**当前待办与状态以 [ZZSIX进度存档.md](./ZZSIX进度存档.md) 为准**  
> **格式**：`> **YYYY-MM-DD 变更（标题）**` + 背景 / 做法 / 落点；规格变更写清「见规划 §x」  
> 新会话默认 **不必** 拖本文；需要追溯历史时再按进度存档索引打开。

---

> **2026-09-24 变更（视觉：新 ZZTWPack + 白标签版本号校验/设置）**  
> 1. **背景**：DLL 增 `version`/`versionRegion`；版本号默认 M00，需界面可改；与其它三码规则同路径 NG。  
> 2. **做法**：接新签名；配置键 `ExpectedWhiteLabelVersion`；功能设置录入；规则引擎白标签比对；橙框叠版本；全量替换 `ZZTWPack.dll`。  
> 3. **落点**：`Pack6ZztwVisionEngine`、`Pack6BarcodeRuleValidator`、`Pack6Config*`、`Pack6SettingsWindow`、`VM_ZZSIXPage`、Core/Dlls 与输出目录 DLL；补丁 `E:\共享文档\ZZSIX_PACK六面检_版本号校验与新DLL_20260924_0941.zip`。

> **2026-09-22 变更（规则：白标签序列码第3位==产品码第16位）**  
> 1. **背景**：现场要求黄框序列码第 3 位与青框产品码第 16 位须相同（代码下标 2 与 15）。  
> 2. **做法**：白标签模式在文件夹/24/前十位通过后做交叉位校验；不足位或不等 → NG；铭牌跳过。管线/离线传入 `PartSN`。  
> 3. **落点**：`Pack6BarcodeRuleValidator`、`Pack6PipelineService`、`VM_ZZSIXPage`；规划 §1.1.3、校验规则 README；补丁 `E:\共享文档\ZZSIX_PACK六面检_交叉位校验_20260922_1513.zip`。

> **2026-09-18 变更（PLC：D1996.1/.2 互跳心跳 + 顶栏呼吸灯）**  
> 1. **背景**：`.0` 专管报警；双方心跳改为各自每秒翻转，只看对方位是否还在跳；5 秒无跳变断连。  
> 2. **做法**：写 `D1996.1` 每秒翻转；读 `D1996.2` 跳变刷新；超时 5s；顶栏绿点呼吸 / 红点常亮；配置键 Out/In/Interval/Timeout。  
> 3. **落点**：`Pack6PlcAlarmService`、`Pack6Config*`、`VM_ZZSIXPage`、`ZZSIXPage.xaml`、`PLC配置说明.md`、`seed_zzsix_plc.sql`、pack6配置键；补丁 `E:\共享文档\ZZSIX_PACK六面检_PLC心跳与呼吸灯_20260918_1602.zip`。

> **2026-09-18 变更（发包：叠字+统计持久化重打，含日志 100）**  
> 1. **背景**：相对 1407 包补入检测日志限 100；计数/饼图仍全量。  
> 2. **做法**：Debug 重编后打 zip：`SUNWODA_SEVB*` + `Core*` + 替换说明。  
> 3. **落点**：`E:\共享文档\ZZSIX_PACK六面检_叠字与统计持久化_20260918_1417.zip`（约 65MB）。

> **2026-09-18 变更（待办：新检项统计持久化同口径）**  
> 1. **背景**：当前仅三码校验 / 一次优率有界面计数；未来卡扣等检项接入后须同样跨重启、仅清零清空。  
> 2. **做法**：进度存档新增待办 **#16**，挂靠 #15；§1 检项表与代码入口注明 `Pack6UiStatsStore`。  
> 3. **落点**：`ZZSIX进度存档.md` §1 / §2 / §3。

> **2026-09-18 变更（统计：底部检测日志限 100 条，计数仍全量）**  
> 1. **背景**：「最多 N 条」仅指底部「检测日志」表；三码/一次优率与饼图须全量持久，不清零则开关机原样。  
> 2. **做法**：`Pack6UiStatsStore.MaxLogs=100`；UI/落盘/恢复均截断检测日志；Stations 计数不加限。  
> 3. **落点**：`Pack6UiStatsStore`、`VM_ZZSIXPage`。

> **2026-09-18 变更（发包：叠字位置+统计持久化替换包）**  
> 1. **背景**：现场需覆盖叠字分色位置、仅码值、界面统计跨重启；相对 EOI/标准文件名包再发一包。  
> 2. **做法**：Debug 编译后打 zip：`SUNWODA_SEVB*` + `SUNWODA_SEVB.Core*` + 替换说明。  
> 3. **落点**：`E:\共享文档\ZZSIX_PACK六面检_叠字位置与统计持久化_20260918_1407.zip`（约 65MB）。

> **2026-09-18 变更（复测：叠字仅码值、无中文/QR 前缀）**  
> 1. **背景**：标注图只要码本身，不要 `QR:`/`序列码:`/`产品码:`；SEVB 早已仅码值，独立复测工具仍带前缀。  
> 2. **做法**：`ZztwDllRetest` 三框 `TryAddBox` 只传 `Trim(...)`；样张复测确认绿上/黄右/青下且无前缀。  
> 3. **落点**：`E:\WorkSpace\SWD\ZZSIX\复测\ZztwDllRetest\Program.cs`；标注样张 `_smoke_overlay\复测结果_*\标注图\`。

> **2026-09-18 变更（UI：叠字位置恢复分色规则）**  
> 1. **背景**：「统一框下」冲掉了约定；绿二维码应在上方、黄序列码右侧、青/铭牌下方。  
> 2. **做法**：`Pack6OverlayBox.LabelPlacement`；白标签绿 Above / 黄 Right / 青 Below；铭牌 Below。  
> 3. **落点**：`Pack6DetectionModels`、`Pack6ZztwVisionEngine`、`Pack6ResultOverlayHelper`。

> **2026-09-18 变更（统计：界面计数跨重启持久化）**  
> 1. **背景**：重启后三码/一次优率与结果日志被清零；现场要求仅「统计清零」才清空。  
> 2. **做法**：落盘 `{LocalPathPre}\检测结果\Pack6_界面统计.json`；进页恢复；整包结案后保存；清零写空快照。  
> 3. **落点**：`Pack6UiStatsStore`、`Pack6PipelineService`、`VM_ZZSIXPage`。

> **2026-09-18 变更（收图：仅标准十二相机文件名才入队存图）**   
> 1. **背景**：OK原图夹偶有杂图；原逻辑解析失败仍 fallback 入队并归档，产生垃圾存图。  
> 2. **做法**：须匹配 `{SN}-{相机}-{张序}-{OK|NG}_{时间戳}`，且相机∈十二清单、张序∈设计张数；否则 Warn 跳过（不入队、不存图）。  
> 3. **落点**：`Pack6IngressService.EnqueueReadyFile`；补丁 `E:\共享文档\ZZSIX_PACK六面检_EOI写稳与标准文件名_20260918_0951.zip`。

> **2026-09-18 变更（收图：JPEG EOI 写稳，超时仍入队）**  
> 1. **背景**：仅体积稳定仍可能半截拷贝（归档黑/灰、源路径事后完整）；需 EOI 校验且不能因等不到 EOI 卡死丢检。  
> 2. **做法**：写稳改为连续 5×200ms；JPEG 另等尾部 `FF D9`；超时仍无 EOI 则 Warn 日志后照常入队检测。  
> 3. **落点**：`Pack6IngressService`（Watcher + SN 补扫）；补丁同上。

> **2026-09-17 变更（规则+发包：三码以文件夹码为基准）**  
> 1. **背景**：互比多方时不易判断哪一张图错；现场要求以文件夹码为唯一基准，谁不等谁 NG。  
> 2. **做法**：帧级只比「本帧产品码(/白标签二维码) vs 文件夹」；去掉两白标签互比与跨路 sibling 比对；长度/档位对文件夹码校验。  
> 3. **落点**：`Pack6BarcodeRuleValidator`、`Pack6ProductStatus`、`Pack6PipelineService`；规划 §1.1.3、校验规则 README、进度存档；补丁 `E:\共享文档\ZZSIX_PACK六面检_文件夹基准校验_20260917_1932.zip`。

> **2026-09-17 变更（UI：缺陷报警窗文案换行+限高滚动）**  
> 1. **背景**：三码不一致等多条缺陷空格拼接后换行撑破固定高度窗，扫码输入被挤出可视区。  
> 2. **做法**：缺陷区 `ScrollViewer`（MaxHeight≈148）+ 扫码区固底；标题与码值/≠ 分行展示；整包缺陷按 `\n` 拼接。  
> 3. **落点**：`Pack6NgAlarmWindow.xaml(.cs)`、`VM_ZZSIXPage`。

> **2026-09-17 变更（存图：视觉算法异常独立目录+CSV）**  
> 1. **背景**：DLL/Halcon 等算法异常不应进「三码校验」；结果图不要叠异常横幅，且需可追溯报错原文。  
> 2. **做法**：`IsVisionAlgorithmError` 时只归档到 `NGImage/视觉算法异常`（原图+结果均源文件）；在结果侧该目录维护 `视觉算法异常.csv`。  
> 3. **落点**：`Pack6DefectFolderMapper`、`Pack6ImageArchiveService`。

> **2026-09-17 变更（存图：NG 结果图改为叠图）**  
> 1. **背景**：结果图应对齐预览，NG 也需带框与 NG 原因；原图目录仍只存源文件。  
> 2. **做法**：归档时 OK/NG 的「结果图片」均优先拷 `PreviewImagePath`；无叠图则退回原图。  
> 3. **落点**：`Pack6ImageArchiveService`。

> **2026-09-17 变更（功能+发包：缺陷报警窗 + 欧姆龙 D1996.0）**  
> 1. **背景**：现场接欧姆龙 Fins TCP；NG/缺张需专用扫码复位窗，且多窗全部关闭才复位 PLC。  
> 2. **做法**：整包结案后弹「Pack六面检测缺陷报警」（缺陷空格拼接）；扫码=SN 才关；每开窗写 `D1996.0=true`，窗数归零写 `false`；顶栏 5s 探活；走平台 `plc_config`。  
> 3. **落点**：`Pack6PlcAlarmService`、`Pack6NgAlarmWindow`、`VM_ZZSIXPage`、`ZZSIXPage`；SQL `seed_zzsix_plc.sql`、`PLC配置说明.md`；`E:\共享文档\ZZSIX_PACK六面检_综合补丁_替换文件_20260917_1705.zip`。

> **2026-09-17 变更（UI：去掉「规则: 规则OK」行）**  
> 1. **背景**：右侧统计区 `规则: 规则OK` 冗余，现场不需要。  
> 2. **做法**：删除 `ZZSIXPage` 该 TextBlock，并移除 `LastRuleInfo`。  
> 3. **落点**：`ZZSIXPage.xaml`、`VM_ZZSIXPage.cs`。

> **2026-09-17 变更（UI/存图：取消标注预览夹 + 码文改框下）**  
> 1. **背景**：结果根下另有「标注预览」夹多余；白标签/铭牌码文画在框左/右侧不便对照。  
> 2. **做法**：叠图改写系统临时目录，正式只保留「原始图片/结果图片」；识别码文统一画在检测框下方。  
> 3. **落点**：`Pack6ResultOverlayHelper`、`Pack6ImageArchiveService`、配置键说明；本轮不打包。

> **2026-09-17 变更（修复+发包：三码校验 OK 按套计 1 次）**  
> 1. **背景**：合并「三码校验」后仍按帧累加，一套三图最多记 3 次 OK。  
> 2. **做法**：帧级不再加统计；整包结案时若做过码检则计 1 次——前1+长左2-2+前3 齐全且全过才 OK，否则 NG。  
> 3. **落点**：`Pack6ProductStatus`、`Pack6ProductResult`、`Pack6ProductAggregator`、`Pack6PipelineService`；`E:\共享文档\ZZSIX_PACK六面检_综合补丁_替换文件_20260917_1057.zip`。

> **2026-09-17 变更（功能+发包：中途开检按 SN 补扫）**  
> 1. **背景**：生产中点启动时，该 SN 日期夹里已有图 Watcher 收不到，易缺张；仅靠超时结案会漏检。  
> 2. **做法**：产品首次建桶时补扫 `Y/Z:\OK原图\{日期}\{SN}\`；缺张结案前再补扫一次，有新图则 TouchProgress 暂缓超时；写稳 + `_seen` 去重；日期兼容 `yyyyMMdd`/`yyyy-MM-dd`。  
> 3. **落点**：`IZZSIXPack6IngressService`、`Pack6IngressService`、`Pack6FileNameParser`、`Pack6PipelineService`；`E:\共享文档\ZZSIX_PACK六面检_综合补丁_替换文件_20260917_1054.zip`。

> **2026-09-17 变更（文档：按检测项对齐「三码校验」）**  
> 1. **背景**：对外已是一个检测项，文档仍写「两个码位/前1+前3 分项」，进度存档未按检项分类。  
> 2. **做法**：进度存档 §1 按检测项表重构；规划 §1.1.2 改为「三码校验」一项三图；校验规则 README 写清一句话规则与 NG 目录。  
> 3. **落点**：`ZZSIX进度存档.md`、`ZZSIX项目开发规划.md`、`开发资料/校验规则/README.md`。

> **2026-09-17 变更（口径+发包：三项码检合并为「三码校验」）**  
> 1. **背景**：前1/长左2白标签/前3铭牌在界面与归档上拆成多项，现场要求合并为一个校验项。  
> 2. **做法**：统计仅「三码校验」；帧 StationName/OCR CSV 的 CheckItem 统一为三码校验；码检 NG 一律归档到 `NGImage/三码校验`（含原视觉 DLL 失败）。  
> 3. **落点**：`Pack6PipelineService`、`Pack6ZztwVisionEngine`、`Pack6DefectFolderMapper`、`Pack6ImageArchiveService`、`Pack6CsvResultStore`；`E:\共享文档\ZZSIX_PACK六面检_综合补丁_替换文件_20260917_1019.zip`。

> **2026-09-17 变更（功能+发包：接入长左2-2 白标签）**  
> 1. **背景**：码检应为三项——前1白标签、长左2白标签、前3铭牌；长左2-2 此前未走 OCR。  
> 2. **做法**：`长左2`+张序2 → 白标签三码；独立通道/统计「长左2白标签」；两白标签产品码/二维码不一致记缺陷；离线测试增选图入口。  
> 3. **落点**：`Pack6ZztwVisionEngine`、`Pack6WorkClassifier`、`Pack6Config`、`Pack6ProductStatus`、`Pack6PipelineService`、`VM_ZZSIXPage`、离线窗、配置键 CSV；`E:\共享文档\ZZSIX_PACK六面检_综合补丁_替换文件_20260917_1015.zip`。

> **2026-09-17 变更（修复+发包：收图写稳后再入队）**  
> 1. **背景**：Y/Z 原图正常，归档/预览下半灰且灰高不一——Created 即读导致半截图。  
> 2. **做法**：入队前体积连续 3 次不变且可读（最长 30s）；叠图改为 `FileShare.Read`。  
> 3. **落点**：`Pack6IngressService`、`Pack6ResultOverlayHelper`；`E:\共享文档\ZZSIX_PACK六面检_综合补丁_替换文件_20260917_0956.zip`。

> **2026-09-17 变更（修复+发包：已结案同码再入队堵死全线）**  
> 1. **背景**：现场日志 20:33 缺张结案后，20:42 同 SN 再次「进入提交坑位」；因 `IsClosed` 永不超时，提交头永久占用，之后无检测。  
> 2. **做法**：已结案/Abandoned 迟到图直接丢弃不入队；队头已结案则踢出并武装下一头（`TryEvictDeadHead`）。  
> 3. **落点**：`Pack6PipelineService`；`E:\共享文档\ZZSIX_PACK六面检_综合补丁_替换文件_20260917_0831.zip`。

> **2026-09-17 变更（文案+发包：提交坑位空闲→缺张超时）**  
> 1. **背景**：超时日志仍写「提交坑位空闲」，现场统一为「缺张超时」。  
> 2. **做法**：`ForceTimeout` 的 reason/Emit 去掉「提交坑位空闲/空闲」字样，统一为缺张超时秒数表述；Debug 打包。  
> 3. **落点**：`Pack6PipelineService`；`E:\共享文档\ZZSIX_PACK六面检_综合补丁_替换文件_20260917_0803.zip`。

> **2026-09-16 变更（发包：叠字仅码值+序列码右侧）**  
> 1. **背景**：图上不要「二维码/序列码/产品码」中文前缀；序列码改黄框右侧。  
> 2. **做法**：`BuildBoxes` 只写码值；黄/青框文字放右侧，绿框仍上方。  
> 3. **落点**：`Pack6ZztwVisionEngine`、`Pack6ResultOverlayHelper`；`E:\共享文档\ZZSIX_PACK六面检_综合补丁_替换文件_20260916_1758.zip`。

> **2026-09-16 变更（发包：含离线测试）**  
> 1. **背景**：离线选图调 DLL 等需现场替换。  
> 2. **做法**：Debug 编译后打 zip（SEVB/Core + deps + System.Drawing* + `Dlls\ZZTWPack.dll`）。  
> 3. **落点**：`E:\共享文档\ZZSIX_PACK六面检_综合补丁_替换文件_20260916_1752.zip`（约 65MB）。

> **2026-09-16 变更（功能：离线测试选图调 DLL）**  
> 1. **背景**：对齐旧枣庄「离线测试」：自动检测不启动时，选本地图单独调 ZZTWPack。  
> 2. **做法**：主界面「离线测试」弹窗；前1白标签/前3铭牌选图；强制真实 DLL；跑码规则；刷新预览；与「启动」互斥。  
> 3. **落点**：`Pack6OfflineTestWindow`、`VM_ZZSIXPage`、`ZZSIXPage`。

> **2026-09-16 变更（发包：NG目录扁平+缺张10分钟等）**  
> 1. **背景**：现场需替换含七项反馈、NG 目录扁平、缺张 10 分钟等改动。  
> 2. **做法**：Debug 编译后打 zip（SEVB/Core + deps + System.Drawing* + `Dlls\ZZTWPack.dll`）。  
> 3. **落点**：`E:\共享文档\ZZSIX_PACK六面检_综合补丁_替换文件_20260916_1736.zip`（约 65MB）。

> **2026-09-16 变更（存图目录扁平 + 缺张超时 10 分钟）**  
> 1. **背景**：NG 不要 `视觉检测异常/DLL脚本异常` 下级；`扫码异常` 改为 `三码校验异常` 且无细分；缺张改 10 分钟。  
> 2. **做法**：`Pack6DefectFolderMapper` 仅大类无 Sub；默认/库迁移 `PackMissingTimeoutSeconds=600`。  
> 3. **落点**：`Pack6DefectFolderMapper`、`Pack6Config*`、`VM_ZZSIXPage`、设置窗、配置键 CSV。

> **2026-09-16 变更（UI/存图/缺张：七项现场反馈）**  
> 1. **背景**：去掉顶栏三连接状态；叠字无灰底且分位放置；NG 归档要源图；运行中锁档位/设置；缺张改黄窗报警并不进统计；当前产品可复制；缺张超时默认 5 分钟可配。  
> 2. **做法**：见各落点文件。  
> 3. **落点**：`ZZSIXPage`、`Pack6SettingsWindow`、`Pack6ResultOverlayHelper`、`Pack6ImageArchiveService`、`Pack6MissingShotAlarmWindow`、`VM_ZZSIXPage`、`Pack6Config*`、`Pack6PipelineService`。

> **2026-09-16 变更（发包：含缺张项统计等）**  
> 1. **背景**：缺张项并列统计、日志行高、去废弃轮询等需现场替换。  
> 2. **做法**：Debug 编译后打 zip（SEVB/Core + deps + System.Drawing* + `Dlls\ZZTWPack.dll`）。  
> 3. **落点**：`E:\共享文档\ZZSIX_PACK六面检_综合补丁_替换文件_20260916_1641.zip`（约 65MB）。

> **2026-09-16 变更（统计：缺张项与码检并列）**  
> 1. **背景**：缺张此前只写结果日志，右侧检测项表未单独统计。  
> 2. **做法**：`StationStats` 增加「缺张项」；整包结案时超时计 NG、齐套未超时计 OK。  
> 3. **落点**：`Pack6PipelineService`。

> **2026-09-16 变更（UI：检测日志行高重叠）**  
> 1. **背景**：日志 `RowHeight=22` 过矮，上下行文字重叠。  
> 2. **做法**：去掉固定行高；`MinHeight=28`；NG 详情 `TextWrapping=Wrap`；关闭行虚拟化以便自动撑高。  
> 3. **落点**：`ZZSIXPage.xaml`。

> **2026-09-16 变更（UI：去掉废弃轮询秒设置）**  
> 1. **背景**：收图已不做定时补扫，设置窗再显示「(废弃)轮询秒」无意义。  
> 2. **做法**：`Pack6SettingsWindow` 删除该控件；配置键仍可读库兼容，界面不再展示。  
> 3. **落点**：`Pack6SettingsWindow.xaml`。

> **2026-09-16 变更（发包：不超前调度+新文件监视+NG警示+ZZTWPack）**  
> 1. **背景**：现场需替换含不超前调度、仅监视新文件、NG 警示、更新版视觉库等改动。  
> 2. **做法**：Debug 编译后打 zip（SEVB/Core + deps + System.Drawing* + `Dlls\ZZTWPack.dll`），含 `替换说明.txt`。  
> 3. **落点**：`E:\共享文档\ZZSIX_PACK六面检_综合补丁_替换文件_20260916_1612.zip`（约 65MB+；不含完整 Halcon 原生库）。

> **2026-09-16 变更（视觉：替换更新版 ZZTWPack.dll）**  
> 1. **背景**：视觉侧更新算法库，投放于 `开发资料/视觉资料`。  
> 2. **做法**：覆盖 `SUNWODA_SEVB.Core/Dlls/ZZTWPack.dll` 及 Debug 输出 `Dlls\`；Debug 编译 0 error（接口名仍为 `DecodeThreeCodesByOCR` / `DecodeNameplateProductSNByOCR`）。  
> 3. **落点**：`Core/Dlls/ZZTWPack.dll`；未改 `halcondotnet`（仍用工程 22.11.1.0）。

> **2026-09-16 变更（UI：NG 警示关窗清空 / 未关追加 / 仅确定）**  
> 1. **背景**：关窗后再 NG 不应带出已确认产品码；窗仍开着需追加；复制无用。  
> 2. **做法**：列表改由 View 持有；Closing 清空；关窗过程中新 NG 进 pending，Closed 后另开新窗；按钮仅「确定」。  
> 3. **落点**：`ZZSIXPage.xaml.cs`、`Pack6NgAlarmWindow`、`VM_ZZSIXPage`。

> **2026-09-16 变更（UI：NG 警示关闭后不累计旧码）**  
> 1. **背景**：关窗后再次 NG 不应再列出已确认过的产品码；窗仍开着时新 NG 需追加；复制无用。  
> 2. **做法**：警示 Closed → `AcknowledgeNgAlarmClosed` 清空缓冲；未关则继续追加；按钮改为「确定」。  
> 3. **落点**：`VM_ZZSIXPage`、`ZZSIXPage`、`Pack6NgAlarmWindow`。

> **2026-09-16 变更（调度/收图：不超前 + 仅监视新文件）**  
> 1. **背景**：现场暂无 PLC；超前跑下一包会导致「产品未到先检完」；回看补扫/已处理索引与实时监视冲突，不再需要。  
> 2. **做法**：`TryDequeue` 仅取 `_order[0]`；整包齐套/超时提交视为临时 PLC 放行下一包；`Pack6IngressService` 去掉回看与 `processed_files.idx`，只 Watcher 新文件 + 会话 `_seen`；设置页回看/轮询标废弃。  
> 3. **落点**：`Pack6PipelineService`、`Pack6IngressService`、`IZZSIXPack6IngressService`、`Pack6SettingsWindow`、`VM_ZZSIXPage`；进度存档 / 配置键 CSV。

> **2026-09-16 变更（发包：萝卜坑调度综合补丁）**  
> 1. **背景**：一检项一通道调度需现场替换。  
> 2. **做法**：Debug 编译后打 zip（SEVB/Core + deps + System.Drawing*），含 `替换说明.txt`。  
> 3. **落点**：`E:\共享文档\ZZSIX_PACK六面检_综合补丁_替换文件_20260916_1514.zip`（约 65MB；不含完整 Halcon Dlls）。

> **2026-09-16 变更（调度：一检项一通道萝卜坑）**  
> 1. **背景**：快/慢两通道不利于多 DLL 扩展；改为每种检测方式独立通道并行填坑，整包保序清坑。  
> 2. **做法**：`FrameReceive`/`WhiteLabel`/`Nameplate` 各一工人；检完即可做下一产品同类项；仅提交头产品武装缺张计时（上代清坑后下代才开始）；齐套提交仍绑定 CSV+优率+UI。  
> 3. **落点**：`Pack6DetectChannelKind`、`Pack6WorkClassifier`、`Pack6PipelineService`；规划 §3。

> **2026-09-16 变更（UI：缺张超时写入右下角 NG 并列出缺哪张）**  
> 1. **背景**：缺张强制提交后需在结果日志中可见，并写明具体缺哪些「相机-张序」。  
> 2. **做法**：聚合器记录已收张序；超时结案 Defects 含 `缺张:前-3,底1-2…`；结果列显示 `NG(缺张)`；仍触发 NG 警示。  
> 3. **落点**：`Pack6ProductStatus`、`Pack6ProductAggregator`、`Pack6PipelineService`、`VM_ZZSIXPage`。

> **2026-09-16 变更（发包：视觉 DLL 异常统一归类补丁）**  
> 1. **背景**：DLL 任意返回 err 均应归视觉检测异常并在图上中文标明。  
> 2. **做法**：Debug 编译后打 zip（SEVB/Core + deps + System.Drawing*），含 `替换说明.txt`。  
> 3. **落点**：`E:\共享文档\ZZSIX_PACK六面检_综合补丁_替换文件_20260916_1438.zip`（约 65MB；不含完整 Halcon Dlls）。

> **2026-09-16 变更（分类：视觉 DLL 任意返回异常均归视觉检测异常）**  
> 1. **背景**：不仅 #3111，凡 ZZTWPack 返回的非空 err 都属 DLL/脚本侧失败，勿当扫码业务 NG。  
> 2. **做法**：白标签/铭牌只要 `err` 非空即 `IsVisionAlgorithmError`；Defects/横幅写「视觉检测异常」；叠图中文横幅+详情；归档 `视觉检测异常/DLL脚本异常`。  
> 3. **落点**：`Pack6ZztwVisionEngine`、`Pack6DefectFolderMapper`、`Pack6ResultOverlayHelper`。

> **2026-09-16 变更（分类：Halcon/DLL 脚本失败归视觉检测异常）**  
> 1. **背景**：铭牌 OCR 返回 `HALCON #3111 empty region` 等被文案里的「铭牌」误归「扫码异常」。  
> 2. **做法**：识别 Halcon/DLL 脚本特征后标 `IsVisionAlgorithmError`；归档大类 `视觉检测异常/DLL脚本异常`；预览横幅改为「视觉检测异常」。  
> 3. **落点**：`Pack6DefectFolderMapper`、`Pack6ZztwVisionEngine`、`Pack6ResultOverlayHelper`、`Pack6CameraSlotVm`。

> **2026-09-16 变更（发包：缺张超时修复补丁）**  
> 1. **背景**：缺张误杀修复需现场替换。  
> 2. **做法**：Debug 编译后打 zip（SEVB/Core + deps + System.Drawing*），含 `替换说明.txt`。  
> 3. **落点**：`E:\共享文档\ZZSIX_PACK六面检_综合补丁_替换文件_20260916_1419.zip`（约 65MB；不含完整 Halcon Dlls）。

> **2026-09-16 变更（修复：缺张超时误杀整包未检图）**  
> 1. **背景**：旧逻辑自首帧入队起满 30s 即强制提交，补扫一包 52 张尚未检完也被「缺张」丢掉。  
> 2. **做法**：仅当快/慢队列皆空、无在检帧、未齐套，且距上次入队/处理空闲 ≥30s 才超时；队列有未检图绝不因墙钟清包。  
> 3. **落点**：`Pack6PipelineService`（`LastIdleAnchorUtc`/`InFlight`）、`pack6配置键.csv`。

> **2026-09-16 变更（发包：存图+NG警示综合补丁）**  
> 1. **背景**：现场需覆盖含存图归档、扫码异常大类、NG 非模态警示的程序。  
> 2. **做法**：Debug 编译后打 zip（SEVB/Core + deps + System.Drawing*），含 `替换说明.txt`。  
> 3. **落点**：`E:\共享文档\ZZSIX_PACK六面检_综合补丁_替换文件_20260916_1334.zip`（约 65MB；不含完整 Halcon Dlls）。

> **2026-09-16 变更（存图大类合并 + NG 非模态警示）**  
> 1. **背景**：码检四类缺陷统一为「扫码异常」；任一检查项 NG 需醒目警示但不阻断检测。  
> 2. **做法**：归档大类改为 `扫码异常/{三码校验|编码规则|白标签|铭牌}`；码检 NG 弹出非模态窗列累计 SN，「确定并复制」关窗并拷剪贴板，后续 NG 再弹。  
> 3. **落点**：`Pack6DefectFolderMapper`、`Pack6NgAlarmWindow`、`VM_ZZSIXPage`、`ZZSIXPage.xaml.cs`。

> **2026-09-16 变更（存图：对齐枣庄原始/结果图 + 缺陷子目录 + 磁盘清理）**  
> 1. **背景**：检测原图与结果图需按枣庄六面检方式落盘；含 NG 缺陷大类/细分目录；磁盘>90%删最早日期，默认至少保留 2 年。  
> 2. **做法**：`图像数据/原始图片|结果图片/OKImage|NGImage/[缺陷大类]/[细分]/{日期起相对路径}`；帧处理后归档；启动与运行期按占用清理。  
> 3. **落点**：`Pack6ImageArchiveService`、`Pack6DefectFolderMapper`、`Pack6PipelineService`、`Pack6Config*`、pack6配置键.csv。

> **2026-09-16 变更（UI：右侧统计改为检测项，去掉相机行）**  
> 1. **背景**：右下角按 12 相机列统计无意义；当前只上两路码检。   2. **做法**：统计表仅「前1白标签」「前3铭牌」；表头改为「检测项」；帧累计只匹配检测项 StationName。  
> 3. **落点**：`Pack6PipelineService.cs`、`VM_ZZSIXPage.cs`、`ZZSIXPage.xaml`。

> **2026-09-16 变更（预览：识别码改到检测框右侧，左上角仅 OK/NG）**  
> 1. **背景**：码文集中在左上角不便对照原印刷码；需靠近检测框且不挡 ROI；左上角只留判定结果。  
> 2. **做法**：各框码文画在 ROI 右侧（右侧放不下改左侧）；左上角仅「OK」或「NG」+ NG 详情；OCR/规则失败均写 `isOk`/`ngBanner`。  
> 3. **落点**：`Pack6ResultOverlayHelper.cs`、`Pack6ZztwVisionEngine.cs`、`Pack6PipelineService.cs`。

> **2026-09-16 变更（UI：产品类型下拉移到主界面）**  
> 1. **背景**：产品类型需常切换，不宜藏在设置窗。  
> 2. **做法**：主界面右侧「功能设置」上方增加产品类型 ComboBox；设置窗去掉该段；切换即写 `ProductCodeProfile`。  
> 3. **落点**：`ZZSIXPage.xaml`、`Pack6SettingsWindow.xaml`、`VM_ZZSIXPage.cs`。

> **2026-09-16 变更（UI：十二窗布局顺序）**  
> 1. **背景**：预览窗顺序需按现场习惯：上→长侧→底。  
> 2. **做法**：`Pack6CameraCatalog.Cameras` 改为 上左/上右/前/后 → 长左1/2/长右1/2 → 底1~4（UniformGrid 4×3 跟列表顺序）。  
> 3. **落点**：`Pack6CameraCatalog.cs`；规划 §1.1.4；进度存档。

> **2026-09-16 变更（发包：含产品序调度的综合补丁）**  
> 1. **背景**：调度改动需现场替换。  
> 2. **做法**：Debug 编译后打 zip（SEVB/Core + deps + System.Drawing*），含 `替换说明.txt`。  
> 3. **落点**：`E:\共享文档\ZZSIX_PACK六面检_综合补丁_替换文件_20260916_1011.zip`（约 65MB；不含完整 Halcon Dlls）。

> **2026-09-16 变更（调度：快/慢双通道 + 产品保序提交 + 缺张30s）**  
> 1. **背景**：无限 FIFO 时慢检项会堵队列；需按产品保序提交，同时允许快检超前一包以保吞吐。  
> 2. **做法**：入队按产品分快/慢队列；慢检仅头产品串行；快检可做头包及下一包（深度1）；整包提交绑定产品CSV+一次优率+UI事件且保序；缺张自首帧起最长 `PackMissingTimeoutSeconds`（默认30s）强制提交并打日志。慢检当前=码检，接口可扩展其它耗时项。  
> 3. **落点**：`Pack6PipelineService`、`Pack6ProductAggregator`、`Pack6WorkClassifier`、`Pack6Config`/`Pack6ConfigService`、`pack6配置键.csv`。

> **2026-09-16 变更（发包：综合替换补丁进共享文档）**  
> 1. **背景**：本会话合入 QR 校验、图例码文、摘要仅档位名、F3 修复、五档前缀等，需现场可替换包。  
> 2. **做法**：Debug 编译后打 zip（SEVB/Core + deps + System.Drawing*），含 `替换说明.txt`。  
> 3. **落点**：`E:\共享文档\ZZSIX_PACK六面检_综合补丁_替换文件_20260916_0928.zip`（约 65MB；不含完整 Halcon Dlls）。

> **2026-09-16 变更（校验：二维码纳入一致比对）**  
> 1. **背景**：原「三码」只比文件夹/白标签产品码/铭牌，未比二维码；样张上 QR 与产品码不一致时未报 NG。  
> 2. **做法**：白标签帧要求二维码非空且与产品码、文件夹一致；铭牌帧用已存 `WhiteLabelQrData` 一并比对；NG 文案含二维码。  
> 3. **落点**：`Pack6BarcodeRuleValidator`、`Pack6ProductStatus`、`Pack6PipelineService`、`Pack6ProductAggregator`；规划 §1.1.3 / 校验规则 README。

> **2026-09-16 变更（预览：识别码文改到图例外，不挡原码）**  
> 1. **背景**：叠字画在 ROI 旁会盖住印刷源码，无法比对 OCR 对错。  
> 2. **做法**：框内只画色框；识别结果集中在左上角半透明图例；有 NG 横幅时图例下移错开。  
> 3. **落点**：`Pack6ResultOverlayHelper.cs`。

> **2026-09-16 变更（UI：主界面摘要只显示当前档位）**  
> 1. **背景**：配置摘要仍带电芯/模拟/Y·Z/结果路径等多余文字。  
> 2. **做法**：`ConfigSummaryText` 仅显示「当前配置：{档位名}」。  
> 3. **落点**：`ZZSIXPage.xaml.cs`。

> **2026-09-16 变更（规格：五档产品前缀 + 三码优先 + 图上叠 NG）**  
> 1. **背景**：11 段下拉过细；现场只需五种产品编码，校验码前十位；主界面只显示档位名；失败须在预览左上角提示。  
> 2. **做法**：设置改为单一档位下拉（右侧只读前缀）；校验顺序先三码（文件夹/白标签/铭牌）再 24 位+前十位；失败重写标注预览叠 NG；配置键 `ProductCodeProfile`。见规划 §1.1.3。  
> 3. **落点**：`Pack6ProductCodeCatalog`、`Pack6BarcodeRuleValidator`、`Pack6PipelineService`、`Pack6ResultOverlayHelper`、`Pack6Config*`、`Pack6SettingsWindow`、`VM_ZZSIXPage`、`ZZSIXPage.xaml.cs`、pack6配置键.csv；删除分段行 VM；综合补丁 `E:\共享文档\ZZSIX_PACK六面检_综合补丁_替换文件_20260916_0910.zip`（含 F3 路由修复+叠框预览）。

> **2026-09-15 变更（修复：同一 SN 刷上千条铭牌 #3111）**  
> 1. **背景**：日志中 `12TPB1ALABP0SDG9F3300002` 连续大量「铭牌 OCR #3111 empty region」。  
> 2. **做法**：根因是关键词 `F3` 命中 SN 中的 `F3`，整包所有图误进铭牌 OCR。改为**标准文件名先按相机+张序路由**；关键词只扫去掉 SN 的片段；默认去掉 `F1`/`F3`/`white`；加载配置时 Sanitize。  
> 3. **落点**：`Pack6ZztwVisionEngine.ResolveOcrMode`、`Pack6Config`/`Pack6ConfigService`、`pack6配置键.csv`。

> **2026-09-15 变更（预览：识别框+码文叠图 / 双击全屏 / 算法异常）**  
> 1. **背景**：需在视觉返回图上框出识别区域并显示码文；双击可全屏；算法失败时显示「视觉算法异常」。  
> 2. **做法**：Halcon `SmallestRectangle1` 取 ROI → System.Drawing 叠框/字 → 落盘 `LocalPathPre/标注预览/yyyyMMdd/`（按回看×2 天裁剪）；槽位预览用标注图；双击打开全屏窗；异常帧打黄色横幅。  
> 3. **落点**：`Pack6ResultOverlayHelper`、`Pack6ZztwVisionEngine`、`Pack6CameraSlotVm`、`Pack6PreviewFullscreenWindow`、`ZZSIXPage`、`Pack6DetectionModels`。

> **2026-09-15 变更（修复：长时间运行内存只涨）**  
> 1. **背景**：现场反馈程序越跑内存越大。  
> 2. **做法**：`_seen` 先查已处理再入队、处理后 `NotifyProcessed` 释放；补扫跳过已处理路径；`processed` 索引运行期裁剪；Halcon `temporary_mem_cache=false`；预览同路径不重复解码；饼图节流；空闲扫超时 Pack。  
> 3. **落点**：`Pack6IngressService`、`Pack6PipelineService`、`Pack6ProcessedFileStore`、`Pack6ZztwVisionEngine`、`Pack6CameraSlotVm`、`VM_ZZSIXPage`；补丁 `E:\共享文档\ZZSIX_PACK六面检_内存修复_20260915_1812.zip`。

> **2026-09-15 变更（修复：Y/Z 补扫交织入队）**  
> 1. **背景**：日志 `2026-09-15-17.txt` 已监视 Y+Z 且补扫 4830 张，但管线只出前/后/底，无上左/长右；根因是补扫先扫完整个 Y 再扫 Z，Z 被压在队尾。  
> 2. **做法**：收集 Y+Z 后按产品 SN 分组再按时间入队；状态日志输出 Y/Z 分盘计数。  
> 3. **落点**：`Pack6IngressService.cs`；补丁 `E:\共享文档\ZZSIX_PACK六面检_YZ交织补扫修复_20260915_1753.zip`。

> **2026-09-15 变更（约定：halcondotnet 用仓库 22.11.1.0）**  
> 1. **背景**：视觉资料 `halcondotnet` 为 22.11.0.0，仓库原版为 22.11.1.0；覆盖会导致其它算法库 CS1705。罗文确认用原版。  
> 2. **做法**：Core\Dlls 与输出目录保持 **22.11.1.0**；仅 `ZZTWPack.dll`/`halcon.dll` 用视觉资料；勿再用资料版覆盖 `halcondotnet`。  
> 3. **落点**：`开发资料/视觉资料/README.md`；工程当前已是 22.11.1.0（此前误覆盖后已 checkout 恢复）。

> **2026-09-15 变更（设置：规则表完整分段 + 齿轮编辑选项）**  
> 1. **背景**：设置须对齐规则表原图（厂商/产品类型/规格/客户/项目车型/产品阶段/产地/日期/产线/序列号）；选项需可现场增删（如规格 `1A`）。  
> 2. **做法**：设置窗按 11 段下拉；右侧齿轮弹窗编辑选项并落库；阶段=允许列表校验；日期/产线/序列号可空仅校格式；产地默认含 `JS`。  
> 3. **落点**：`Pack6BarcodeFieldCatalog`、`Pack6BarcodeFieldRowVm`、`Pack6OptionListEditorWindow`、`Pack6SettingsWindow`、`Pack6Config`/`Pack6ConfigService`/`Pack6BarcodeRuleValidator`、`VM_ZZSIXPage`、pack6配置键.csv；补丁 `E:\共享文档\ZZSIX_PACK六面检_规则设置补丁_替换文件_20260915_1716.zip`。

> **2026-09-15 变更（资料：视觉 DLL 对齐重打）**  
> 1. **背景**：核对补丁包 DLL 来源；`ZZTWPack`/`halcon` 已是视觉资料，但 `halcondotnet.dll` 仍是工程旧哈希，与 `开发资料/视觉资料` 不一致。  
> 2. **做法**：曾短暂用视觉资料覆盖 `halcondotnet` 并打 1650 zip；**已更正：不得覆盖，见上条「用仓库 22.11.1.0」**。  
> 3. **落点**：`E:\共享文档\ZZSIX_PACK六面检_HalconDlls补丁_替换文件_20260915_1650.zip`（其中 `halcondotnet` 若为 22.11.0.0 勿用，以工程 22.11.1.0 为准）。

> **2026-09-15 变更（现场：口琴管日志 + Halcon 仍缺 DLL）**  
> 1. **背景**：`2026-09-15-16.txt` 大量「口琴管涂胶PLC / 欧姆龙伺服PLC」重连；OCR 仍 `Unable to load DLL 'halcon' (0x8007007E)`。此前补丁 zip 只带 SEVB/Core，**未带 `Dlls\`**。  
> 2. **做法**：Halcon 预加载失败即抛错并打「原生运行时就绪」日志；提供禁用无关 PLC 的 SQL；重新打包 **含完整 Dlls\halcon*** 的替换 zip。  
> 3. **落点**：`Pack6ZztwVisionEngine.cs`、`docs/ZZSIX/disable_unrelated_plc_for_zzsix.sql`、`E:\共享文档\ZZSIX_PACK六面检_HalconDlls补丁_替换文件_20260915_1647.zip`（约 149MB，含 Dlls）。

> **2026-09-15 变更（资料：补丁 zip 1632）**  
> 1. **背景**：结果目录、饼图百分比、Halcon 路径修复需再发替换包。  
> 2. **做法**：打包 SEVB + Core 的 dll/exe/pdb。  
> 3. **落点**：`E:\共享文档\ZZSIX_PACK六面检_界面补丁_替换文件_20260915_1632.zip`。

> **2026-09-15 变更（结果目录 + 饼图百分比）**  
> 1. **背景**：检测结果需落 `D:\六面检上位机软件`；饼图未正确显示 OK/NG 百分比（OxyPlot 格式串用错占位符）。  
> 2. **做法**：默认/迁移 LocalPathPre；饼图标签改为 `OK xx%`/`NG xx%`，InsideLabel 用 `{2:0.0}%`；右侧同步显示百分比文字。  
> 3. **落点**：`Pack6Config`、`Pack6ConfigService`、`VM_ZZSIXPage`、`ZZSIXPage.xaml`、pack6配置键.csv。

> **2026-09-15 变更（修复：Halcon 0x8007007E）**  
> 1. **背景**：点启动走真 OCR 时 `ReadImage`/`ZZTWPack` 加载 `halcon.dll`，该文件在 `Dlls\` 而非 exe 同级，报 Unable to load DLL 'halcon' (0x8007007E)。  
> 2. **做法**：`Pack6ZztwVisionEngine` 启动前 `SetDllDirectory` + PATH 前置 `Dlls`，并设置 `HALCON_LICENSE_FILE`（对齐离线 CT）。  
> 3. **落点**：`Pack6ZztwVisionEngine.cs`。

> **2026-09-15 变更（资料：补丁 zip 含 Core）**  
> 1. **背景**：掉线补扫依赖 Core 新接口，替换包需带 Core。  
> 2. **做法**：打包 `SUNWODA_SEVB` + `SUNWODA_SEVB.Core` 的 dll/pdb/exe。  
> 3. **落点**：`E:\共享文档\ZZSIX_PACK六面检_界面补丁_替换文件_20260915_1617.zip`。

> **2026-09-15 变更（实现：掉线补扫/已处理标记）**  
> 1. **背景**：仅 `FileSystemWatcher` 时，程序掉线期间落到 Y/Z OK原图的文件会漏检；CTOL 用 RemoteFileMonitor 的 `IsProcessed` + 补扫。  
> 2. **做法**：本地 `{LocalPathPre}/state/processed_files.idx` 标记（不改原图）；点「启动」后回看补扫 + 可选定时轮询；管线处理完再 Mark。打开软件**不**自动开检。  
> 3. **落点**：`Pack6ProcessedFileStore`、`Pack6IngressService`、`Pack6PipelineService`、配置键 `IngressLookbackDays`/`IngressPollSeconds`。

> **2026-09-15 变更（资料：界面补丁 zip 精简重打）**  
> 1. **背景**：二次改版后需再发替换包，且只要覆盖文件。  
> 2. **做法**：仅打包 `SUNWODA_SEVB.dll` / `.exe` / `.pdb` + 一行替换说明。  
> 3. **落点**：`E:\共享文档\ZZSIX_PACK六面检_界面补丁_替换文件_20260915_1546.zip`。

> **2026-09-15 变更（UI：预览占比/饼图/通信占位/启停）**  
> 1. **背景**：预览被压得太扁；统计行过高出滚动条；缺 PLC/MES/WEB 状态、OK/NG 饼图、底部启动+停止。  
> 2. **做法**：左栏 4:1；顶栏三类通信状态先显示「未接入」；工位统计改 18px 行高 ItemsControl；OxyPlot 饼图绑一次优率；右侧底部绿启动/红停止。  
> 3. **落点**：`ZZSIXPage.xaml`、`VM_ZZSIXPage.cs`。

> **2026-09-15 变更（资料：界面补丁 zip）**  
> 1. **背景**：现场已有完整 Debug 程序，只需覆盖本轮界面改版文件。  
> 2. **做法**：打包 `SUNWODA_SEVB.dll` / `.exe` / `.pdb` + 替换说明。  
> 3. **落点**：`E:\共享文档\ZZSIX_PACK六面检_界面补丁_替换文件_20260915_1533.zip`。

> **2026-09-15 变更（UI：对齐旧检测界面布局）**  
> 1. **背景**：主界面参数行占位过多、预览窗过大不便看图；需参照旧「PACK六面拍照工位检测」结构，而非自创布局。  
> 2. **做法**：厂区/路径等收进 `Pack6SettingsWindow`；主区一行配置摘要（值加粗）；左=十二窗缩略（`Stretch=Uniform`）+ 检测日志表；右=功能设置 / NG结果查询 / 优率 / 工位统计 / 清零 / 红色「停」；VM 通过 `RequestOpenSettings` 由 View 弹窗，避免 Views 反向依赖。  
> 3. **落点**：`ZZSIXPage.xaml` / `.xaml.cs`、`Pack6SettingsWindow.*`、`VM_ZZSIXPage.cs`；进度存档 #12。

> **2026-09-15 变更（资料：生产部署包重打）**  
> 1. **背景**：现场无库、菜单排查；需含建库脚本的完整包。  
> 2. **做法**：重编译后打包至 `E:\共享文档\ZZSIX_PACK六面检_生产部署包_20260915_1404`（及同目录无日期别名）；含 `init_sunwoda_demo`、启用 SQL、程序 DLL、菜单排查说明。  
> 3. **落点**：共享盘部署包；入口 `01_部署与联调\00_部署说明_必读.md`。

> **2026-09-15 变更（资料：生产部署包）**  
> 1. **背景**：需将联调资料与升级 DLL 放到共享盘供生产机台部署。  
> 2. **做法**：打包文档/SQL/样张/规则/ZZTWPack+OCR依赖+主程序 DLL 至 `E:\共享文档\ZZSIX_PACK六面检_生产部署包_20260915`。  
> 3. **落点**：共享盘部署包；入口 `01_部署与联调\00_部署说明_必读.md`。

> **2026-09-15 变更（实现：规则引擎 + 十二窗 + Y/Z 收图）**  
> 1. **背景**：资料齐备后按待办 #10～#14 落地；旧 Top/Bottom/Side 八窗逻辑与现场 12 相机不符。  
> 2. **做法**：`Pack6BarcodeRuleValidator`（长度/文件夹名/分段/失败定位）；设置下拉厂区·项目·规格；`Pack6CameraCatalog` + Ingress 监视 Y/Z OK原图；页面 4×3 十二窗；白标签↔铭牌一致在汇总中校验。  
> 3. **落点**：`Pack6*.cs`（Core/Services/VM/View）；[pack6配置键.csv](./pack6配置键.csv)；[pack6队列需求.csv](./pack6队列需求.csv)；进度存档。

> **2026-09-15 变更（规格：条码校验规则 + 十二窗/收图待办）**  
> 1. **背景**：现场规则表要求扫码后校验长度、与文件夹名一致、分段规则并指出不合格段；厂区/车型/项目需设置下拉；相机由 8 改为 12，监视逻辑须对齐 Y/Z OK原图命名。  
> 2. **做法**：规则写入规划 §1.1.3；十二相机 UI/Ingress 写入 §1.1.4；进度存档新增待办 #10～#14（本轮只文档，未改界面代码）。  
> 3. **落点**：[ZZSIX项目开发规划.md](./ZZSIX项目开发规划.md)；[ZZSIX进度存档.md](./ZZSIX进度存档.md)；[开发资料/校验规则/](./开发资料/校验规则/)。

> **2026-09-15 变更（口径：码检是前1+前3 两处，不是三个相机）**  
> 1. **背景**：用户提供前1 白标签、前3 铭牌样张，确认目前只检这两处；忘记「三码」原意。  
> 2. **做法**：规划 §1.1.2 写明「三码」= 白标签图内三项（QR/序列码/产品码）；关键词补 `前-1`/`前-3`；取消 Front 队列全量当白标签的兜底；样张入库。  
> 3. **落点**：规划 / 进度 / 配置键 / `Pack6Config` / `Pack6ZztwVisionEngine`；[开发资料/样张/](./开发资料/样张/)。

> **2026-09-15 变更（资料：OK原图按相机口径修订统计）**  
> 1. **背景**：现场确认监视 `Y/Z:\OK原图`；文件名中段为**相机名**，其后为该相机第几张（如 `长右1-1`）；**「定位」不是相机**。  
> 2. **做法**：对 dump 重统计 → **12 台相机**及每台设计张数（单包 52 张）；更新精简文档与摘要。  
> 3. **落点**：[../BoxGluing/存图路径/目录结构精简.md](../BoxGluing/存图路径/目录结构精简.md)；[开发资料/存图路径结构摘要.md](./开发资料/存图路径结构摘要.md)。

> **2026-09-15 变更（资料：OK原图目录树精简）**  
> 1. **背景**：现场 Y/Z 盘 `OK原图` 全量 tree 行数过多，需保留规则与统计又不淹没仓库。  
> 2. **做法**：从 `Y盘.txt`/`z盘.txt` 提炼层级、命名、视角枚举、按日数量与样例；全量文件仍保留。  
> 3. **落点**：[../BoxGluing/存图路径/目录结构精简.md](../BoxGluing/存图路径/目录结构精简.md)；[开发资料/存图路径结构摘要.md](./开发资料/存图路径结构摘要.md)；进度存档 §0。

> **2026-09-15 变更（资料：现场 VNC 连接信息）**  
> 1. **背景**：联调需要记录项目现场 VNC。  
> 2. **做法**：写入规划 §2.2（`10.178.202.110` / 密码见该节）；进度存档 §0 增加索引。  
> 3. **落点**：[ZZSIX项目开发规划.md](./ZZSIX项目开发规划.md) §2.2；[ZZSIX进度存档.md](./ZZSIX进度存档.md)。

> **2026-09-15 变更（范围：负责人确认本期仅码检）**  
> 1. **背景**：询问项目负责人后确认——其它检测项目尚未上线，现阶段只需先做码检测，待后续再添加新内容。  
> 2. **做法**：写入规划 §1.1.1 范围决策；§1.2/§1.3/§4/§5 与进度存档待办对齐（缺陷全量迁移、八窗等标为后续）。  
> 3. **落点**：[ZZSIX项目开发规划.md](./ZZSIX项目开发规划.md)；[ZZSIX进度存档.md](./ZZSIX进度存档.md)。

> **2026-09-14 变更（产品编码位数：恰好 24 位）**  
> 1. **背景**：视觉侧补充「除编码一致性、产品型号外，还要判断编码位数（24 位）」。  
> 2. **做法**：`ProductSNExactLength` 默认 24，白标签/铭牌 OCR 后严格判 `Length==24`，否则 NG；规则串默认改为空（避免 8 位示例规则误导）。一致性/型号跨窗比对仍待样张格式明确后再做。  
> 3. **落点**：`Pack6Config.cs`；`Pack6ZztwVisionEngine.cs`；`Pack6ConfigService.cs`；[pack6配置键.csv](./pack6配置键.csv)。

> **2026-09-14 变更（视觉资料：接入 ZZTWPack 白标签/铭牌 OCR）**  
> 1. **背景**：`开发资料/视觉资料` 提供枣庄 TW 算法说明书与 `ZZTWPack.dll`（.NET 8，Halcon+PaddleOCR），覆盖 P05 白标签三码与铭牌产品编码，**不是**旧 LrzWork 的 `ZZPack.AI` 全量六面检。  
> 2. **做法**：实现 `Pack6ZztwVisionEngine`；引用 DLL + `PaddleOCRSharp`/`Paddle.Runtime.win_x64`；配置 `ProductSNRule`/关键词；OCR 帧写 `*_Pack6_OCR.csv`；非 OCR 工位跳过；默认关闭模拟。  
> 3. **落点**：`Pack6ZztwVisionEngine.cs`；`Pack6Config*.cs`；`Pack6CsvResultStore.cs`；`VM_ZZSIXPage`/`ZZSIXPage`；`SUNWODA_SEVB.csproj`；`Core/Dlls/ZZTWPack.dll`；[pack6配置键.csv](./pack6配置键.csv)；[视觉资料/README.md](./开发资料/视觉资料/README.md)。

> **2026-09-14 变更（P0：PACK 六面检迁移骨架）**  
> 1. **背景**：用户确认 ZZSIX 只迁 PACK 六面检（旧 `VM_PagePack6Surface`）；旧单体约 8k+ 行，需按新 SEVB 分层重写。  
> 2. **做法**：落地 Config/Ingress/Pipeline/Vision(Sim)/Aggregator/CsvStore；主页改为「PACK六面检」；支持目录监视与「模拟一整包」冒烟；配置键与队列需求落 CSV；**未**接 ZZPack.AI / 八窗预览 / PLC 写。  
> 3. **落点**：`ViewModels/Pages/ZZSIX/VM_ZZSIXPage.cs`；`Views/Pages/ZZSIX/ZZSIXPage.xaml`；`Services/Project/ZZSIX/*`；`Core/Interfaces|Models/Project/ZZSIX/*`；`App.xaml.cs`；[pack6配置键.csv](./pack6配置键.csv)、[pack6队列需求.csv](./pack6队列需求.csv)；进度存档 / 规划。

> **2026-09-14 变更（分析：旧仓 LrzWork 迁移盘点）**  
> 1. **背景**：需将 `sunwoda1.0.0.2` 分支 `LrzWork`（.NET 4.8）迁入新 SEVB 的 ZZSIX。  
> 2. **做法**：相对 `master` 梳理工序与依赖；明确禁止整文件拷贝；BoxGluing 已在新仓独立存在故交叉引用；待确认首迁工序。  
> 3. **落点**：[开发资料/LrzWork迁移分析.md](./开发资料/LrzWork迁移分析.md)；进度存档待办 #1。

> **2026-09-14 变更（新建：项目基础结构 demo）**  
> 1. **背景**：按 SWD 工作流新建代号 ZZSIX，先落地可编译、菜单可进的空页骨架与三文档。  
> 2. **做法**：新建 `VM_ZZSIXPage` / `ZZSIXPage`；`[Module]` 菜单名「ZZSIX」；写入并执行 `enable_zzsix_module.sql`（库 `sunwoda_demo`）；创建 `开发资料/` 投放区。  
> 3. **落点**：`ViewModels/Pages/ZZSIX/VM_ZZSIXPage.cs`；`Views/Pages/ZZSIX/ZZSIXPage.xaml(.cs)`；[enable_zzsix_module.sql](./enable_zzsix_module.sql)；本目录三文档；[开发资料/](./开发资料/)。
