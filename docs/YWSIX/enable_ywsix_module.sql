-- YWSIX 模块启用脚本
-- 库名以 appsettings.json 为准（仓库内暂无该文件，按平台默认库 sunwoda_demo；启动时由 ConfigurationService 生成）
-- 用法：先完整启动一次应用（平台自动向 workspace_project 写入本模块记录），再执行本脚本，然后完整退出并重启 SEVB。
-- 说明：本脚本只启用 YWSIX 自身，不改动其它业务模块的启用状态。

USE sunwoda_demo;

-- 确保使用通用主窗口（否则左侧菜单可能看不到新模块）
UPDATE global_setting
SET value = 'MainWindow'
WHERE name = 'AppStartMainWindow';

-- 若首次启动尚未插入记录，则手动补一条
INSERT INTO workspace_project (vm_class_name, is_enabled)
SELECT 'VM_YWSIXPage', 1
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM workspace_project WHERE vm_class_name = 'VM_YWSIXPage'
);

-- 启用模块（vm_class_name 必须与 ViewModel 类名一致）
UPDATE workspace_project
SET is_enabled = 1
WHERE vm_class_name = 'VM_YWSIXPage';

-- 核对
SELECT vm_class_name, is_enabled
FROM workspace_project
WHERE vm_class_name = 'VM_YWSIXPage';

SELECT name, value
FROM global_setting
WHERE name = 'AppStartMainWindow';
