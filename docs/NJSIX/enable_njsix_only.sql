-- =============================================================================
-- NJSIX 现场机：只启用 NJSIX（关闭其它业务模块）
-- =============================================================================
-- 库名：以现场 appsettings.json 为准（示例 sunwoda_demo）
-- 用法：替换程序后执行本脚本 → 完整退出 SEVB → 再启动
-- 保留：VM_SettingsPage / VM_UserCenter / VM_NJSIXPage
-- =============================================================================

USE sunwoda_demo;

-- 主窗口
UPDATE global_setting
SET value = 'MainWindow'
WHERE name = 'AppStartMainWindow';

-- 先全部关闭业务菜单
UPDATE workspace_project
SET is_enabled = 0
WHERE vm_class_name NOT IN (
    'VM_SettingsPage',
    'VM_UserCenter',
    'VM_NJSIXPage'
);

-- 确保设置 / 用户中心开启
UPDATE workspace_project
SET is_enabled = 1
WHERE vm_class_name IN ('VM_SettingsPage', 'VM_UserCenter');

-- 插入 NJSIX（若不存在）
INSERT INTO workspace_project (vm_class_name, is_enabled)
SELECT 'VM_NJSIXPage', 1
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM workspace_project WHERE vm_class_name = 'VM_NJSIXPage'
);

-- 启用 NJSIX
UPDATE workspace_project
SET is_enabled = 1
WHERE vm_class_name = 'VM_NJSIXPage';

-- 核对：应仅见 Settings / UserCenter / NJSIX = 1
SELECT vm_class_name, is_enabled
FROM workspace_project
WHERE is_enabled = 1
ORDER BY vm_class_name;

SELECT name, value
FROM global_setting
WHERE name = 'AppStartMainWindow';

-- （可选）关掉无关 PLC，避免旧项目地址刷屏重连；若现场还要跑涂胶等则勿执行：
-- UPDATE plc_config SET IsEnable = 0
-- WHERE Name IN ('口琴管涂胶PLC', '欧姆龙伺服PLC') AND IsEnable = 1;
