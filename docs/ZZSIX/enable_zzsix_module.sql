-- ZZSIX 模块启用脚本
-- 库名以 appsettings.json / ConfigurationService 默认为准（当前 sunwoda_demo）
-- 用法：编译通过后执行本脚本，然后完整退出并重启 SEVB。

USE sunwoda_demo;

-- 确保使用通用主窗口（否则侧边栏可能看不到新模块）
UPDATE global_setting
SET value = 'MainWindow'
WHERE name = 'AppStartMainWindow';

-- 若首次启动尚未插入记录，可先手动插入
INSERT INTO workspace_project (vm_class_name, is_enabled)
SELECT 'VM_ZZSIXPage', 1
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM workspace_project WHERE vm_class_name = 'VM_ZZSIXPage'
);

-- 启用模块
UPDATE workspace_project
SET is_enabled = 1
WHERE vm_class_name = 'VM_ZZSIXPage';

-- 核对
SELECT vm_class_name, is_enabled
FROM workspace_project
WHERE vm_class_name = 'VM_ZZSIXPage';

SELECT name, value
FROM global_setting
WHERE name = 'AppStartMainWindow';

-- PLC 设备种子见同目录：seed_zzsix_plc.sql / PLC配置说明.md
-- （欧姆龙 Fins TCP 192.168.7.40:9600，报警位 D1996.0）
