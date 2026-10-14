TEMP / 老板汇报用 — 资源监控（可整夹删除）
========================================

本目录仅用于测试阶段向老板汇报「软件资源占用 + 采集轮次速率」。
正式交付前请整夹删除，并去掉下列挂钩（搜关键字 TEMP_BOSS_REPORT）：

1. AIPredictiveServiceCollectionExtensions.cs 中对本服务的 DI 注册行
2. VM_AIPredictivePage.cs 中对本服务的注入与「开始/停止资源监控」命令
3. AIPredictivePage.xaml 中「临时：资源监控」整块 Border
4. 本目录 Services/Project/AIPredictive/_TempBossReport/

报告输出目录（默认）：
  D:\AIPredictiveData\TempBossReport\
  - resource_report_yyyyMMdd_HHmmss.txt
  - acquisition_rate_report_yyyyMMdd_HHmmss.txt
  - resource_samples_yyyyMMdd_HHmmss.csv（可选明细）
