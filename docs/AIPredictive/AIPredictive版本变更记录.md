# AIPredictive 版本变更记录

> **用途**：按日期倒序记录「做了什么、为什么、落在哪」；**当前待办与状态以 [AIPredictive进度存档.md](./AIPredictive进度存档.md) 为准**  
> **格式**：`> **YYYY-MM-DD 变更（标题）**` + 背景 / 做法 / 落点；规格变更写清「见规划 §x」  
> 新会话默认 **不必** 拖本文；需要追溯历史时再按进度存档索引打开。

---

> **2026-10-13 变更（分级预警取消确认抑制）**  
> 1. **背景**：确认后按「轴|等级」抑制，后续轮次同轴同级不再弹；现场要每轮有一级/二级就直接弹。  
> 2. **做法**：去掉 `acked_alerts` / `Acknowledge`；`AlertsRaised` 对当轮全部一级/二级直接回调；确认按钮仅关遮罩。  
> 3. **落点**：`IAIPredictiveGradingAlertService`；`AIPredictiveGradingAlertService`；`AIPredictiveGradingAlertUiBinder`；热修包 `_upload_8080/AIPredictive_replace_20261013_v1.zip`（SEVB.dll + Core.dll）。  

> **2026-10-12 变更（分级预警 Python 相对路径启动修复）**  
> 1. **背景**：配置 `runtime\python.exe` 文件存在且可双击，但 Process.Start 报找不到文件。  
> 2. **做法**：启动前将相对 PythonExe 相对 GradingAlert 目录拼成绝对路径再启动。  
> 3. **落点**：`AIPredictiveGradingAlertService.ResolvePythonExe`；热修包 `_upload_8080/AIPredictive_replace_20261012_v2.zip`（仅 SEVB.dll）。  

> **2026-10-12 变更（分级预警读 CSV 共享锁修复）**  

> 1. **背景**：现场每 2h 一轮均报 IOException，采集正占用当天 `AIPredictive_500ms_*.csv`。  
> 2. **做法**：切片读取改为 `FileShare.ReadWrite|Delete`，与采集写句柄共存。  
> 3. **落点**：`AIPredictiveGradingAlertService.BuildRecentSlice`；替换包 `_upload_8080/AIPredictive_replace_20261012_v1.zip`（仅 SEVB.dll）。  

> **2026-10-10 变更（分级预警接入 · 2h 调度 / 2h 数据窗）**  
> 1. **背景**：需接入对方离线 V8.0；不改对方 py；防历史全量反复读入。  
> 2. **做法**：`IntervalMinutes=120` 且 `LookbackHours=2`（每 2 小时分析一次，每次喂最近 2h）；上位机覆盖写 `input_slice.csv` 后调原版 `分级预警推理.py`；水印跳过；确认抑制；红遮罩。  
> 3. **落点**：`AIPredictive/GradingAlert/`；`AIPredictiveGradingAlertService` / `HostedService` / `UiBinder`；进度 #28。  
> 4. **现场**：替换包 `_upload_8080/AIPredictive_replace_20261010_v1.zip`（含便携 Python runtime + wheels）；设备默认 `先导3号机`。  

> **2026-10-10 变更（现场替换包 v1 · 分级预警 + 便携 Python）**  
> 1. **背景**：产线已在跑旧版；工控机可能无系统 Python。  
> 2. **做法**：精简替换包仅 `SEVB.dll` / `Core.dll` + `AIPredictive\GradingAlert\`（含便携 Python）；**不含**产线已有采集块 CSV / `plc_endpoint.json`；另附离线 wheels 备用。  
> 3. **落点**：`_upload_8080/AIPredictive_replace_20261010_v1(.zip)`。  





> **2026-10-08 变更（工控机与 PLC IP 成对对照）**  

> 1. **背景**：经跳板机连的是工控机；《德阳 C03 线设备 IP》表「网口 IP」才是 PLC；旧资料把赢合默认写成 `.15`（实为赢合 6#）。  
> 2. **做法**：成对落档——先导1# 工控 `10.147.71.233` / PLC `10.147.72.10`；先导3# `10.147.71.95` / `10.147.72.12`；赢合10# `10.147.71.151` / `10.147.72.19`；另存叠片机 PLC 全表 CSV。  
> 3. **落点**：规划 §0.2；[两厂家对照说明.md](./开发资料/两厂家对照说明.md) §2.1；[德阳C03线_叠片机PLC_IP.csv](./开发资料/德阳C03线_叠片机PLC_IP.csv)；进度存档。  
> 4. **未改代码**（程序默认仍为先导 PLC `10.147.72.10`；采其它机台用界面改 IP）。  

> **2026-10-08 变更（现场工控机 IP 录入）**  
> 1. **背景**：联调需经跳板机再连各产线工控机；此前文档只记跳板机与 PLC，未列工控机 IP。  
> 2. **做法**：跳板机仍为 `10.146.41.211`；补录工控机——先导 1 号 `10.147.71.233`、先导 3 号 `10.147.71.95`、赢合 10 号 `10.147.71.151`；明确工控机（`10.147.71.*`）与 PLC（`10.147.72.*`）勿混用。  
> 3. **落点**：规划 §0.2；[两厂家对照说明.md](./开发资料/两厂家对照说明.md) §2.1；进度存档索引与已完成。  
> 4. **未改代码**（仅文档）。  

> **2026-09-29 变更（厂家下拉 + 端口顶死 44818）**  
> 1. **背景**：同一套程序要服务先导/赢合两家产线；用户确认端口一律 44818，选哪个厂家只跑哪个、不同时采。  
> 2. **做法**：`plc_endpoint.json` 增加 `Vendor`；加载对应采集块 CSV + Andon 标签（先导 `AndonRunStatu` / 赢合 `Andon_RunStatu[0]`）；连接端口强制 44818；界面 ComboBox + IP 可改、端口只读；保存后停再启。  
> 3. **落点**：`AIPredictiveVendorNames`；`AIPredictivePlcEndpointStore`；`CollectBlockLoader`；`AcquisitionService`；`VM_AIPredictivePage` / `AIPredictivePage.xaml`；csproj 拷贝 `赢合_采集块_伺服.csv`；对照说明。  
> 4. **验证**：`dotnet build -c Debug -m:1`。  

> **2026-09-29 变更（赢合资料整理，未改程序）**  
> 1. **背景**：同一套采集程序需支持赢合厂家；先只整理资料，厂家切换后续再改代码。  
> 2. **做法**：`开发资料/` 分 `先导/`、`赢合/`；赢合 txt → 点位表 275 点 + 采集块 2×ArrayBatch（`axisTrq.max`/`axisTrqMQ.max`）+ 机台状态 CSV；根目录保留程序别名指向先导；写 [两厂家对照说明.md](./开发资料/两厂家对照说明.md)。  
> 3. **落点**：`docs/AIPredictive/开发资料/`；规划 §0；进度存档索引。  
> 4. **⏸**：数组下标从 1 起的连读偏移（端口已按后续变更顶死 44818）。  

> **2026-09-29 变更（启动自动采集 + 最小化 + 无启停按钮）**  
> 1. **背景**：现场要开机自启后台采扭矩，不希望每次点开始，也不希望窗口弹全屏。  
> 2. **做法**：`App` 启动后后台 `StartAsync`；去掉界面开始/停止；离页不停采；改 IP「保存地址」自动停再启；`MainWindow` 启动即最小化。  
> 3. **落点**：`App.xaml.cs`；`MainWindow.xaml`；`VM_AIPredictivePage`；`AIPredictivePage.xaml`。  
> 4. **验证**：`dotnet build -c Debug -m:1` 0 error；替换包 `AIPredictive_replace_20260929_v1.zip`。  

> **2026-09-24 变更（异常宽限 3s + 可配 IP + CSV 仅 500ms）**  
> 1. **背景**：状态一变就停采易漏数；多机台需各改 PLC IP；大数据侧暂只需 500ms 粒度。  
> 2. **做法**：由运行切异常后继续采 **3s**，仍异常再停；界面可改 IP/端口，写入 `AIPredictive/plc_endpoint.json`，**停止并重新开始采集后生效**；CSV 去掉 1s/5s，只写 `AIPredictive_500ms_*.csv`。  
> 3. **落点**：`AIPredictiveAcquisitionService`；`AIPredictivePlcEndpointStore`；`VM_AIPredictivePage` / `AIPredictivePage.xaml`；`AIPredictiveCsvStorage`；规划 §C.0。  
> 4. **验证**：`dotnet build -c Debug -m:1` 0 error；整包 Debug → `E:\共享文档\下载\SUNWODA_SEVB_Debug_20260924.zip`。  

> **2026-09-23 变更（机台状态门控采扭矩）**  
> 1. **背景**：仅在机台正常运行时需要扭矩数据；现场可用 CIP short 读 `AndonRunStatu`（7=运行 / 18=停机 / 19=待机 / 9=故障）。  
> 2. **做法**：每轮先 `ReadInt16(AndonRunStatu)`；**仅状态=7** 才读扭矩块并写 CSV；非 7 / 读失败不读扭矩、不落盘；状态相对上一拍变化时打 Info/Warn 日志。不做绝对值、不做 &lt;0.2 过滤（按当轮确认）。  
> 3. **落点**：`AIPredictiveAcquisitionService`；`AIPredictiveRoundStats`；规划 §C.0；进度存档待办 #25。  
> 4. **验证**：`dotnet build SUNWODA_SEVB.csproj -c Debug -m:1` 0 error；替换包 `AIPredictive_replace_20260923_v1.zip` 已放到 `E:\共享文档\下载\`（本机 HTTP :80）及仓库 `_upload_8080/`。  

> **2026-09-23 变更（跳板机 VNC IP 更新）**  
> 1. **背景**：现场跳板机地址变更。  
> 2. **做法**：规划 §0.2 / 进度存档索引：`10.146.41.210` → **`10.146.41.211`**（密码不变）。  
> 3. **落点**：规划 §0.2；进度存档 §0。  

> **2026-09-22 变更（标准采集间隔改为 500ms）**  
> 1. **背景**：现场测试确认 500ms 可作为标准采集间隔；250ms 数据量仍偏大且粒度不可再对齐。  
> 2. **做法**：`DefaultIntervalMs` 250→**500**；CSV 去掉 250ms，保留 **500ms / 1s / 5s**（每 1/2/10 轮）；Info 每 2 轮约 1s 一条。  
> 3. **落点**：`AIPredictiveAcquisitionService`；`AIPredictiveCsvStorage`；`IAIPredictiveCsvStorage`；`VM_AIPredictivePage`；规划 §C.0 / §3.5。  
> 4. **验证**：`dotnet build SUNWODA_SEVB.csproj -c Debug -m:1` 0 error；替换包 `AIPredictive_replace_20260922_v1.zip`。  

> **2026-09-17 变更（现场替换包 v1 + 本机 8080）**  
> 1. **背景**：250ms + 四粒度 CSV 需上机验证。  
> 2. **做法**：打替换包 `_upload_8080/AIPredictive_replace_20260917_v1.zip`（SEVB.dll / Core.dll / 扭矩 CSV）；本机 `python -m http.server 8080`。  
> 3. **落点**：`_upload_8080/AIPredictive_replace_20260917_v1*`。  

> **2026-09-17 变更（周期 250ms + 四粒度 CSV）**  
> 1. **背景**：50ms 数据量过大；大数据组需要同一快照按多种时间粒度取数。  
> 2. **做法**：  
>    - `DefaultIntervalMs` 50→**250**；Info 每 4 轮（约 1s）一条  
>    - CSV 同时维护四路：`AIPredictive_{250ms|500ms|1s|5s}_yyyyMMdd_nnn.csv`（同日目录、各 100MB 滚动）  
>    - 分发规则（基周期 250ms）：每轮写 250ms；每 2 轮写 500ms；每 4 轮写 1s；每 20 轮写 5s（同一行快照复制，非另采）  
> 3. **落点**：`AIPredictiveCsvStorage`；`IAIPredictiveCsvStorage`；`AIPredictiveAcquisitionService`；`VM_AIPredictivePage`；规划 §C.0。  
> 4. **验证**：`dotnet build SUNWODA_SEVB.csproj -c Debug -m:1` 0 error。  

> **2026-09-14 变更（规划录入现场跳板机 VNC）**  
> 1. **背景**：远程联调需要跳板机入口，避免每次口头问。  
> 2. **做法**：规划新增 [§0.2](./AIPredictive项目开发规划.md#02-现场跳板机vnc2026-09-14-录入)：VNC `10.146.41.210`，密码按项目组提供记录；端口/账号未给，标 ⏸。  
> 3. **落点**：规划 §0.2 / §7；进度存档 §0 索引。  

> **2026-09-14 变更（现场替换包 + 本机 8080 下载）**  
> 1. **背景**：扭矩子集 + 50ms 周期需上机验证。  
> 2. **做法**：打替换包 `_upload_8080/AIPredictive_replace_20260914_v1.zip`（SEVB.dll / Core.dll / 扭矩 CSV）；本机 `python -m http.server 8080`。  
> 3. **落点**：`_upload_8080/AIPredictive_replace_20260914_v1*`。  

> **2026-09-14 变更（扭矩子集采集周期改为 50ms）**  
> 1. **背景**：点位已从 2594 收到 230 个最大扭矩，项目组要求 50ms 一轮。  
> 2. **做法**：`DefaultIntervalMs` 1000→**50**；单轮 CIP 超过 50ms 则 **Warn 且立即下一轮**（不硬等）；Info 日志改为首轮 + 每 20 轮（约 1s）一条，避免 20Hz 刷屏。CSV 仍每轮落盘。⏸ 现场需看耗时是否稳定 ≤50ms；193 个结构体仍按 batch=20，约 10 次 multi-tag + 1 次数组连读。50ms×230 点约 4600 点/秒，CSV 量级明显高于 1s。  
> 3. **落点**：`AIPredictiveAcquisitionService`；`IAIPredictiveAcquisitionService`；`VM_AIPredictivePage`；规划 §C.0。  
> 4. **验证**：`dotnet build SUNWODA_SEVB.csproj -c Debug -m:1` 0 error。  

> **2026-09-14 变更（暂时只采伺服最大扭矩；全量 CSV 备份）**  
> 1. **背景**：全量 2594 点过多，项目组要求暂时只采扭矩。现场点表里**没有实时扭矩**，仅有「最大扭矩」（结构体 `MAx.Sta.rMaxTrq` + 数组 `ar_rIOT6FromPLC2[11..47]`）。  
> 2. **做法**：  
>    - 全量原表拷入 [开发资料/备份_全量采集块_20260914/](./开发资料/备份_全量采集块_20260914/)（伺服 61/920、气缸 60/1674）  
>    - 程序主配置改为：伺服 **60 行 / 230 点**（1 数组 + 59 结构体，全部为最大扭矩）；气缸仅留表头（0 点，气缸无扭矩）  
>    - `ar_rIOT6FromPLC2` 仍按根符号连读 48 个 FLOAT（CIP 从 `[0]` 起，与既有读法一致），清单只保留下标 11..47；位置/速度/加速度数组与气缸整表不加载  
>    - 采集服务读法未改；恢复全量：用备份覆盖主配置后重新编译并完整重启  
> 3. **落点**：上述 CSV；`_filter_torque_only.py`；规划 §0 / §2.2.2 / §C.0；进度存档待办 #21；`AIPredictiveCollectBlockLoader` 注释。  
> 4. **验证**：`dotnet build SUNWODA_SEVB.csproj -c Debug -m:1` 0 error。  

> **2026-09-11 变更（合并现场简报：删旧两份，留一份给老板）**  
> 1. **背景**：运行简报与 CIP 读法汇报分开，老板不愿看太多。  
> 2. **做法**：合并为 [现场结论简报-20260911.md](./现场结论简报-20260911.md)；删除原「给管理层运行简报」与「CIP结构体读法现场测试汇报」；索引/规划链接改指向新文件。  
> 3. **落点**：上述 docs。  

> **2026-09-11 变更（管理层可读运行简报）**  
> 1. **背景**：现场 TempBossReport 两份 txt 为技术指标，老板不易读。  
> 2. **做法**：据监控数据改写白话简报（后已并入「现场结论简报」）。  
> 3. **落点**：原文件已删，见上条合并。  

> **2026-09-11 变更（临时资源监控 TEMP_BOSS_REPORT，可整夹删除）**  
> 1. **背景**：测试阶段需向老板汇报软件资源占用与采集轮次速率；正式交付要能干净删掉。  
> 2. **做法**：独立目录 `_TempBossReport/` + `AIPredictiveTempRuntimeMonitor`（与采集解耦，只读观察）；页内黄底「开始资源监控 / 停止并出报告」；每秒采 CPU/内存/线程等；停止时写出独立报告到 `D:\AIPredictiveData\TempBossReport\`（resource / acquisition_rate / samples csv）。删除指引见目录内 `README_DELETE_ME.txt`（搜 `TEMP_BOSS_REPORT`）。  
> 3. **落点**：`_TempBossReport/*`；`AIPredictiveServiceCollectionExtensions`；`VM_AIPredictivePage`；`AIPredictivePage.xaml`。  
> 4. **验证**：`dotnet build -c Debug -m:1` 0 error。  

> **2026-09-11 变更（测试连接按钮左移 + 现场替换包 v2）**  
> 1. **背景**：现场希望「测试连接」紧挨「未连接/已连接」文字右侧，并上机验证 multi-batch 采集。  
> 2. **做法**：`AIPredictivePage.xaml` 将测试连接按钮移入左侧连接状态 StackPanel；打包 `_upload_8080/AIPredictive_replace_20260911_v2.zip`（SEVB.dll / Core.dll / 采集块 CSV）。  
> 3. **落点**：`AIPredictivePage.xaml`；`_upload_8080/AIPredictive_replace_20260911_v2*`。  
> 4. **验证**：`dotnet build -c Debug -m:1` 0 error。  

> **2026-09-11 变更（结构体 multi-tag 批量读落地 + CIP 汇报更新）**  
> 1. **背景**：现场探针全量压测确认：整根结构体不可读；`Read(string[]…)` 批量读可行；BatchSize=20 时全量约 **0.47 s/轮**（旧逐成员约 2 s），足以支撑 1 s 周期。  
> 2. **做法**：  
>    - `AIPredictiveAcquisitionService`：`StructMembers` 按 FLOAT/BOOL 汇总后以 **batch=20** 调用 `cip.Read(addrs, lens)`；FLOAT 按连续 LE float32 解析；BOOL 接受 1/2/4 字节/项  
>    - 数组块仍整段 `ReadFloat`/`ReadBool`  
>    - 结论见 [现场结论简报-20260911.md](./现场结论简报-20260911.md)；规划 [§C.0](./AIPredictive项目开发规划.md#c0-采集频率与数据量2026-09-10-分析含--待实测项) CIP 约束改为实测结论   
> 3. **落点**：`AIPredictiveAcquisitionService.cs`；`AIPredictiveCollectModels` 注释；上述 docs。  
> 4. **已知遗留**：`ar_bIOT2FromPLC2` 现场 Error 5（进度待办 #19），需电工核对。  
> 5. **验证**：`dotnet build SUNWODA_SEVB.csproj -c Debug -m:1` 0 error。  

> **2026-09-11 变更（测试连接按钮 + CSV 表头改用参数中文名）**  
> 1. **背景**：现场要先单独测 CIP 通断，不想靠「开始采集」才连；落盘 CSV 表头此前是符号标签（如 `ar_xxx[0]` / `stEM100.MA1…`），不便阅读，需要参数中文名。  
> 2. **做法**：  
>    - 新增 `TestConnectionAsync`：只做 ConnectServer 探测，打日志后断开；结果写入 `IsPlcConnected`；界面加「测试连接」按钮  
>    - CSV `PrepareSession` 改为接收点位定义；表头优先 `DisplayName`（采集块清单里的参数名称，本就已加载），空名回退 PointKey，重名追加 PointKey 后缀；数据列仍按 PointKey 对齐取值  
>    - **无需**再提供原始伺服/气缸点位表  
> 3. **落点**：`IAIPredictiveAcquisitionService` / `AIPredictiveAcquisitionService`；`IAIPredictiveCsvStorage` / `AIPredictiveCsvStorage`；`VM_AIPredictivePage`；`AIPredictivePage.xaml`。  
> 4. **验证**：`dotnet build SUNWODA_SEVB.csproj -c Debug -m:1` 0 error。  

> **2026-09-10 变更（采集 CSV：按天目录 + 100MB 分卷 + 30 天清理）**  
> 1. **背景**：暂定用 CSV 存采集快照；数据量大，需按常规单文件体量分卷，并按天归档、定期删旧。  
> 2. **做法**：  
>    - 根目录默认 `D:\AIPredictiveData`；当天文件夹 `yyyy-MM-dd`；文件 `AIPredictive_yyyyMMdd_001.csv` 起，满 **100MB** 滚下一卷  
>    - 宽表一行一轮：`Timestamp,Round,ElapsedMs,Success/Fail,…点位列`；失败点空单元格  
>    - 启动会话与跨日时清理超过 **30 天** 的日期文件夹；落盘在 CIP 耗时统计之外  
> 3. **落点**：`IAIPredictiveCsvStorage`、`AIPredictiveCsvStorage`；采集服务每轮写入；DI；Core.csproj Include。  
> 4. **验证**：`dotnet build` 0 error。  

> **2026-09-10 变更（界面：PLC 红/绿灯 + 手动开始采集）**  
> 1. **背景**：进页自动连 PLC 不便现场观察；需要一眼看出连接状态，并手动点开始。  
> 2. **做法**：采集服务暴露 `IsPlcConnected`；页面顶部椭圆指示灯（红=未连接 / 绿=已连接）+「开始采集」「停止采集」；取消进页自动 `StartAsync`，离页仍安全停采。  
> 3. **落点**：`IAIPredictiveAcquisitionService`、`AIPredictiveAcquisitionService`、`VM_AIPredictivePage`、`AIPredictivePage.xaml`。  
> 4. **验证**：`dotnet build` 0 error。  

> **2026-09-10 变更（CIP 采集骨架：1s 全量 + 每轮总耗时日志）**  
> 1. **背景**：现场 CIP 已通；暂定 **1 秒一轮**读完全部采集块，并在日志中输出每轮总耗时以便分析。  
> 2. **做法**：  
>    - 输出目录加载 `AIPredictive/采集块_伺服|气缸.csv`（csproj `CopyToOutputDirectory`）  
>    - `OmronConnectedCipNet` 直连（旁路地址块轮询）；`ArrayBatch` 整段读，`StructMembers` 逐成员读；失败置空继续  
>    - 每轮日志：`AIPredictive 全量采集完成：轮次=…，总耗时=…ms，成功=…，失败=…`  
>    - 进页启动 / 离页停止  
> 3. **落点**：Core 模型/接口；`Services/Project/AIPredictive/*`；`VM_AIPredictivePage`；`App.AddAIPredictiveServices`；`SUNWODA_SEVB.csproj` / Core.csproj Include。  
> 4. **验证**：`dotnet build` 0 error。  

> **2026-09-10 变更（采集块分表：伺服 / 气缸；删除旧 CSV）**  
> 1. **背景**：要求伺服与气缸分开两张表；删除原扁平点位表与合并版采集块。  
> 2. **做法**：生成 `AIPredictive采集块_伺服.csv`（61 行 / 920 点）、`AIPredictive采集块_气缸.csv`（60 行 / 1674 点）；删除 `点位表_伺服/气缸.csv`、`采集块.csv`、`采集块_数组连读.csv`；脚本改为可自 `伺服.txt`/`气缸.txt` 再生（若 txt 缺失则以现 CSV 为真相）。  
> 3. **落点**：规划 §0 / §2.2 / §2.2.2、进度存档 §0、README、开发资料 README、版本记录。  

> **2026-09-10 变更（点位表重整为「采集块」CSV，便于 CIP 数组连读）**  
> 1. **背景**：扁平点位表 2594 行不便维护；同一数组应整段连读；现场实测端口 **44818**。  
> 2. **做法**：曾生成合并采集块（后已按需求拆分为伺服/气缸两表，见上条）。  
> 3. **落点**：见上条现行文件。  

> **2026-09-10 变更（平台 CIP 支持核查 + 采集频率/时序库方案调研；点位 CSV 落位修正）**  
> 1. **背景**：开发人员要确认三件事——平台是否支持欧姆龙 CIP 读取、这批点位该以什么频率采集、以及 CSV 存不住的情况下要不要上时序库、跨部门怎么取数。  
> 2. **做法（只读核查 + 调研，未改业务代码）**：  
>    - **平台源码核查**：枚举 `PLCBrandSpecificationProtocal` 含 `Omron_Connected_CIP`；`PLCService.ConfigureOmron()` 对该枚举构造 `OmronConnectedCipNet(ip, port)`；`ParsePLCBrandProtocol()` 支持字符串 `OMRON_CONNECTED_CIP`。  
>    - **定位三个卡点**：① 平台采集链路是「连续地址块 + 字节偏移」（`plc_rw_config` 的 `area_name/start_address/length` → `ConvertValue(bytes, type, index*addressWordLength, bitIndex)`），而现场点位是 Sysmac **符号寻址**（伺服 `ar_` 148 + `st…` 772、气缸 `ar_` 698 + `st…` 976）；② `ConvertValue()` **无 `REAL` 分支**（伺服 920 点全为 `REAL`），会静默返回空值；③ 结构体成员点号访问（`stEM901.MA1.Sta.rActPos`）在 HSL 官方文档**无示例**（HSL 走 `ReadStruct<T>` 整体读）。  
>    - **外部核实**（子代理检索 HSL/Omron 公开资料）：HSL 明确支持标签名与数组下标（官方示例 `"A"`、`"B[0]"`）；但**未列出 NJ501/NX701 支持声明**；**端口 8500 查不到依据**（EtherNet/IP 显式报文标准端口为 44818）。  
>    - **数据量测算**：2594 点位（920+1674），1 s 采集 ≈ 2.24 亿点/天、CSV 年增 ≈ 3 TB；并指出**邻域瓶颈是 CIP 请求速率而非存储**（符号无法打包成连续块，逐点读 2594 个变量约 13~52 s/轮）。  
>    - **存储与取数结论**：CSV 不能做主存储（无索引/并发冲突/清理需重写/无类型），降格为导出格式；时序库对比 TimescaleDB / InfluxDB / QuestDB / MySQL 分区；导出主通道 **Parquet**，辅以 **CSV（先降采样）** 与 **只读 SQL 账号直连**；明确**不共享数据库数据目录**。  
>    - **文档落位修正**：两份点位 CSV 已随原始资料移入 `开发资料/`，规划 §0/§2.2/§7、进度存档 §0、`README.md`、`开发资料/README.md`、本文件旧条目的链接与位置描述同步改为实际路径。  
> 3. **落点**：  
>    - 规划新增 [§2.4 平台 CIP 支持核查与采集可行性](./AIPredictive项目开发规划.md)、[§3.5 采集存储与跨部门取数方案](./AIPredictive项目开发规划.md)、[§C.0 采集频率与数据量](./AIPredictive项目开发规划.md)；§1.4#3、§2.3、§6 风险表（新增 #6~#10）同步更新  
>    - 进度存档：待办新增 #6~#11（核查/实测/分析/选型/点位分档/`REAL` 别名）  
> 4. **验证**：结论均标注来源或「⏸ 待实测」，未臆造；平台结论均有源码行号依据；外部结论附链接并注明「Omron/GitHub 来源因环境限制仅引用标题，须以现场实测为准」。  
> 5. **未做**：未改任何 `.cs`/`.xaml`（含 `REAL` 别名修正——已列入待办 #11 但未动手）；未连现场 PLC；未选定时序库；未新增 `plc_config` 配置。  

> **2026-09-10 变更（点位原始资料 → CSV：伺服 920 行 / 气缸 1674 行）**  
> 1. **背景**：开发人员反映 `开发资料/伺服.txt`、`开发资料/气缸.txt` 为制表符分隔的纯文本，程序读取与质检不便，需要先转成 CSV 格式。  
> 2. **做法**（**纯格式转换，零内容改动、零臆造**）：  
>    - 逐行按制表符切分 → 按 RFC4180 生成 CSV（字段含 `,`/`"`/换行时加引号并把 `"` 转义为 `""`；本次数据实际未触发转义）；  
>    - 统一输出口径：**UTF-8 with BOM + CRLF**，首行列名，行序与原始 txt 完全一致；  
>    - 不改字段顺序、不补空字段、不做单位换算、不合并两表、不推断设备编码与工位语义。  
>    - 数据体检：参数地址与参数名称在各自表内**均无重复**；空列（伺服 `设备编码`；气缸 `线体`、`设备编码`）在原始表中即为空，**原样保持空值**。  
> 3. **落点**：  
>    - 数据：[开发资料/AIPredictive点位表_伺服.csv](./开发资料/AIPredictive点位表_伺服.csv)（10 列 × 920 行）、[开发资料/AIPredictive点位表_气缸.csv](./开发资料/AIPredictive点位表_气缸.csv)（12 列 × 1674 行）  
>    - 文档：规划新增 [§2.2.1 CSV 数据字典](./AIPredictive项目开发规划.md#221-csv-数据字典程序读取口径)（列定义 / 非空情况 / 地址形态 / 引用口径），§0、§2.1、§2.3、§1.3、§1.4#3、§4 阶段 B、§7 索引同步更新  
>    - 原始文件 `开发资料/伺服.txt`、`开发资料/气缸.txt` **保留不删**（`开发资料/README.md` 登记去向）  
> 4. **验证**：用 CSV 解析器回读，与源 txt **逐字段比对 0 处不一致**（表头逐列一致、行数 920/1674 一致）；字节数核对一致（伺服 122235 = 122232 + 3 BOM；气缸 242310）。  
> 5. **未做 / 待确认**：气缸表 `线体`、`工序` 之外的语义映射、阈值、采样频率与设备编码 **原始表未给，标 ⏸ 待需求方确认**；加密 xlsx 仍未录入；两张表列数不同（10 vs 12），**不能直接纵向拼接**。  

> **2026-09-10 变更（菜单启用范围收敛 + 启动实测通过）**  
> 1. **背景**：启动后左侧菜单未见新页面；经核对，数据库记录与配置均正常，问题在于运行的是**另一份副本的旧 exe**（`E:\WorkSpace\SWD\BoxGluing\sunwoda2.0.0.1\...\SUNWODA_SEVB.exe`，2026-09-08 构建，不含本项目页面）。同时按要求收敛启用范围，避免菜单里混入其它项目模块。  
> 2. **做法**：  
>    - 数据库核对：`workspace_project` 中 `VM_AIPredictivePage` 存在且 `is_enabled = 1`；`global_setting.AppStartMainWindow = MainWindow`。  
>    - 将不需要启用的项目/测试模块置 0：`VM_BoxGluingMonitoringPage`、`VM_CTOLAutoDetectionPage`、`VM_xxxTESTPage`；保留平台基础页 `VM_SettingsPage`、`VM_UserCenter` 与本项目。  
>    - 启动实测：运行本副本新构建的 exe（`SUNWODA_SEVB/bin/Debug/net8.0-windows/SUNWODA_SEVB.exe`），观察日志确认注册与导航。  
> 3. **落点**：`sunwoda_demo.workspace_project`（启用集合见进度存档「数据库启用现状」）；[AIPredictive进度存档.md](./AIPredictive进度存档.md) 待办 #1 勾选完成。  
> 4. **验证（日志摘录）**：`注册业务模块: VM_AIPredictivePage (AI预测分析)`、`业务模块注册完成，总业务模块数: 3`、`VM_AIPredictivePage.OnInitialize → AIPredictive 页面初始化完成`、`成功导航到业务模块: VM_AIPredictivePage`。  
> 5. **注意**：切换项目开发时需把对应模块置 1（如回到 BoxGluing 需将 `VM_BoxGluingMonitoringPage` 置回 1）；数据库改动后必须完整退出应用再启动。  

> **2026-09-10 变更（项目初始化：骨架 + 三文档 + 资料归档 + 菜单启用）**  
> 1. **背景**：新建项目 AIPredictive，先按规程 §二 产出「能编译、菜单可进空页」的基础结构 demo，并把现场原始资料投放进 `开发资料/`，为后续需求确认与资料录入做准备。业务定位（预测目标 / 数据来源 / 输出形式）尚未确认，故本轮**只做骨架，不写业务逻辑**。  
> 2. **做法**：  
>    - 按 [新增业务模块上手指南](../新增业务模块上手指南.md) 建 VM/View 三件套，三个名字对齐：VM 类 `VM_AIPredictivePage` ↔ `[Module("VM_AIPredictivePage", "AI预测分析")]` ↔ `x:Class="SUNWODA_SEVB.Views.Pages.AIPredictive.AIPredictivePage"`；VM 注入 `ILoggerService<T>`，`OnInitialize` 设标题与占位状态文案。  
>    - 对照 `docs/BoxGluing/` 建三文档 + `README.md` + `开发资料/`（含目录说明）。  
>    - 编写 `enable_aipredictive_module.sql`（`INSERT ... WHERE NOT EXISTS` + `UPDATE is_enabled = 1`，并确保 `AppStartMainWindow = MainWindow`），对目标库 **直接执行**（库名以 `appsettings.json` 为准，本机与代码默认连接串均为 `sunwoda_demo`）。  
>    - 现场 3 份原始资料（伺服点位表、气缸点位表、德阳 C03 叠片段切叠一体机加密 Excel）**原样复制** 到 `docs/AIPredictive/开发资料/`；本轮不做整理录入（避免在定位未确认时臆造）。  
> 3. **落点**：  
>    - 代码：`SUNWODA_SEVB/ViewModels/Pages/AIPredictive/VM_AIPredictivePage.cs`、`SUNWODA_SEVB/Views/Pages/AIPredictive/AIPredictivePage.xaml(.cs)`  
>    - 文档：[AIPredictive进度存档.md](./AIPredictive进度存档.md)、[AIPredictive项目开发规划.md](./AIPredictive项目开发规划.md)、[README.md](./README.md)、[开发资料/README.md](./开发资料/README.md)  
>    - 数据：[enable_aipredictive_module.sql](./enable_aipredictive_module.sql)（已在 `sunwoda_demo` 执行，`VM_AIPredictivePage.is_enabled = 1`）  
>    - 资料：[开发资料/](./开发资料/)（3 份原始文件，状态 `pending`）  
> 4. **验证**：本地 `dotnet build SUNWODA_SEVB.sln -c Debug` **0 error**（28 个既有警告，非本项目引入）；`AIPredictivePage.g.cs` / `.baml` 已生成，主程序输出已刷新；数据库核对 `VM_AIPredictivePage = 1`、`AppStartMainWindow = MainWindow`。  
>    构建方式：本机 CLI 环境对 MSBuild 多节点并行敏感（并行时静默失败、报「0 error」），改用 `-m:1` 单节点还原 + 编译即通过（无外网时以 `%USERPROFILE%\.nuget\packages` 作 NuGet 源）；细节见进度存档「本机构建提示」。  
> 5. **未做**：业务功能、Service/Core 接口分层、`project_setting` 持久化、开发资料录入 → 见进度存档待办。  
> 6. **上线前置动作**：需**完整退出 SEVB 应用后重新启动**，左侧菜单才会出现「AI预测分析」。

> **接手前（资料归档）**  
> - 项目代号 `AIPredictive` 由工作流页确认后落盘；资料投放目录 `docs/AIPredictive/开发资料/` 依据 [Agent协作开发规程](../Agent协作开发规程.md) §2.2。
