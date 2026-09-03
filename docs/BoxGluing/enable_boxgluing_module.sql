-- BoxGluing 模块启用脚本
-- 库名以 appsettings.json 为准（默认 sunwoda_demo）
-- 用法：先完整启动一次应用（自动写入 workspace_project），再执行本脚本，然后重启。

USE sunwoda_demo;

-- 确保使用通用主窗口（否则侧边栏可能看不到新模块）
UPDATE global_setting
SET value = 'MainWindow'
WHERE name = 'AppStartMainWindow';

-- 若首次启动尚未插入记录，可先手动插入
INSERT INTO workspace_project (vm_class_name, is_enabled)
SELECT 'VM_BoxGluingMonitoringPage', 1
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM workspace_project WHERE vm_class_name = 'VM_BoxGluingMonitoringPage'
);

-- 启用模块
UPDATE workspace_project
SET is_enabled = 1
WHERE vm_class_name = 'VM_BoxGluingMonitoringPage';

-- 核对
SELECT vm_class_name, is_enabled
FROM workspace_project
WHERE vm_class_name = 'VM_BoxGluingMonitoringPage';

SELECT name, value
FROM global_setting
WHERE name = 'AppStartMainWindow';
