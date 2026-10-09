-- JROH 模块启用脚本（三个页面）
-- 库名以 appsettings.json 为准（默认 sunwoda_demo）
-- 用法：先完整启动一次应用（自动写入 workspace_project），再执行本脚本，然后【完整退出应用并重新启动】。
-- 说明：JROH 三个页面各自带 [Module]，需逐条启用；JROHCore.BelongToVM =
--       "VM_JROHClosedLoopPage" 只用于 project_setting 存储键，与菜单无关，勿写进本表。

USE sunwoda_demo;

-- 确保使用通用主窗口（否则侧边栏可能看不到新模块）
UPDATE global_setting
SET value = 'MainWindow'
WHERE name = 'AppStartMainWindow';

-- 若首次启动尚未插入记录，可先手动插入（三页）
INSERT INTO workspace_project (vm_class_name, is_enabled)
SELECT 'VM_PWOfJROHPage', 1 FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM workspace_project WHERE vm_class_name = 'VM_PWOfJROHPage');

INSERT INTO workspace_project (vm_class_name, is_enabled)
SELECT 'VM_OHOfJROHPage', 1 FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM workspace_project WHERE vm_class_name = 'VM_OHOfJROHPage');

INSERT INTO workspace_project (vm_class_name, is_enabled)
SELECT 'VM_MCOfJROHPage', 1 FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM workspace_project WHERE vm_class_name = 'VM_MCOfJROHPage');

-- 启用模块
UPDATE workspace_project
SET is_enabled = 1
WHERE vm_class_name IN ('VM_PWOfJROHPage', 'VM_OHOfJROHPage', 'VM_MCOfJROHPage');

-- 核对
SELECT vm_class_name, is_enabled
FROM workspace_project
WHERE vm_class_name IN ('VM_PWOfJROHPage', 'VM_OHOfJROHPage', 'VM_MCOfJROHPage');

SELECT name, value
FROM global_setting
WHERE name = 'AppStartMainWindow';
