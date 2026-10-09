-- NJSIX 模块启用脚本（本机 / 现场：只开 NJ，关其它业务模块）
-- 库名以 appsettings.json 为准（当前示例 sunwoda_demo）
-- 用法：替换程序后执行本脚本，然后完整退出并重启 SEVB。
-- 更完整说明见同目录 enable_njsix_only.sql（内容一致）。

USE sunwoda_demo;

UPDATE global_setting
SET value = 'MainWindow'
WHERE name = 'AppStartMainWindow';

-- 先全部关闭其它业务模块（保留设置 / 用户中心 / NJSIX）
UPDATE workspace_project
SET is_enabled = 0
WHERE vm_class_name NOT IN (
    'VM_SettingsPage',
    'VM_UserCenter',
    'VM_NJSIXPage'
);

UPDATE workspace_project
SET is_enabled = 1
WHERE vm_class_name IN ('VM_SettingsPage', 'VM_UserCenter');

INSERT INTO workspace_project (vm_class_name, is_enabled)
SELECT 'VM_NJSIXPage', 1
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM workspace_project WHERE vm_class_name = 'VM_NJSIXPage'
);

UPDATE workspace_project
SET is_enabled = 1
WHERE vm_class_name = 'VM_NJSIXPage';

SELECT vm_class_name, is_enabled
FROM workspace_project
WHERE is_enabled = 1
ORDER BY vm_class_name;

SELECT name, value
FROM global_setting
WHERE name = 'AppStartMainWindow';

-- PLC：南京暂无报警；无关 PLC 可选禁用见 disable_unrelated_plc_for_njsix.sql
