-- NCLCCT 模块启用脚本
-- 库名以 appsettings.json 为准（本机未落盘 appsettings.json，按代码默认值 sunwoda_demo）
-- 用法：先完整启动一次应用（平台会写入 workspace_project 记录），再执行本脚本，然后重启。
-- 说明：模块注册受 workspace_project.is_enabled 门控；记录不存在时菜单不会出现，故此处带 INSERT 兜底。

USE sunwoda_demo;

-- 确保使用通用主窗口（否则侧边栏可能看不到新模块）
UPDATE global_setting
SET value = 'MainWindow'
WHERE name = 'AppStartMainWindow';

-- 若首次启动尚未插入记录，可先手动插入
INSERT INTO workspace_project (vm_class_name, is_enabled)
SELECT 'VM_NCLCCTPage', 1
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM workspace_project WHERE vm_class_name = 'VM_NCLCCTPage'
);

-- 启用模块
UPDATE workspace_project
SET is_enabled = 1
WHERE vm_class_name = 'VM_NCLCCTPage';

-- 核对
SELECT vm_class_name, is_enabled
FROM workspace_project
WHERE vm_class_name = 'VM_NCLCCTPage';

SELECT name, value
FROM global_setting
WHERE name = 'AppStartMainWindow';
