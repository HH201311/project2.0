-- sunwoda_demo 平台基础表初始化脚本
-- 依据 SUNWODA_SEVB.Data/Models 与 DatabaseInitializer 生成

CREATE DATABASE IF NOT EXISTS `sunwoda_demo` DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
USE `sunwoda_demo`;

SET FOREIGN_KEY_CHECKS = 0;

DROP TABLE IF EXISTS `web_interface_logs`;
DROP TABLE IF EXISTS `mes_interface_logs`;
DROP TABLE IF EXISTS `plc_address_config`;
DROP TABLE IF EXISTS `plc_rw_config`;
DROP TABLE IF EXISTS `plc_config`;
DROP TABLE IF EXISTS `device`;
DROP TABLE IF EXISTS `mes_setting`;
DROP TABLE IF EXISTS `project_setting`;
DROP TABLE IF EXISTS `app_logs`;
DROP TABLE IF EXISTS `Users`;
DROP TABLE IF EXISTS `workspace_project`;
DROP TABLE IF EXISTS `global_setting`;

CREATE TABLE `global_setting` (
  `id` int NOT NULL AUTO_INCREMENT,
  `name` varchar(255) NOT NULL,
  `value` varchar(255) NOT NULL,
  `type` varchar(255) NOT NULL,
  `unit` varchar(255) NULL,
  `role_rank` int NOT NULL,
  `remark` varchar(255) NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE `workspace_project` (
  `id` int NOT NULL AUTO_INCREMENT,
  `vm_class_name` varchar(255) NOT NULL,
  `is_enabled` tinyint(1) NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE `Users` (
  `id` int NOT NULL AUTO_INCREMENT,
  `user_account` varchar(255) NOT NULL,
  `user_name` varchar(255) NOT NULL,
  `password` varchar(255) NOT NULL,
  `role_id` int NOT NULL,
  `remark` varchar(255) NULL,
  `created_time` datetime NULL,
  `last_login_time` datetime NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE `project_setting` (
  `id` int NOT NULL AUTO_INCREMENT,
  `name` varchar(255) NOT NULL,
  `belong_to_vm` varchar(255) NOT NULL,
  `value` text NOT NULL,
  `type` varchar(255) NOT NULL,
  `unit` varchar(255) NULL,
  `role_rank` int NOT NULL,
  `remark` varchar(255) NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE `app_logs` (
  `id` int NOT NULL AUTO_INCREMENT,
  `logtime` datetime NOT NULL,
  `loglever` varchar(255) NOT NULL,
  `logger` varchar(255) NOT NULL,
  `Message` text NOT NULL,
  `exception` text NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE `mes_setting` (
  `id` int NOT NULL AUTO_INCREMENT,
  `profile_name` varchar(255) NOT NULL,
  `key` varchar(255) NOT NULL,
  `value` varchar(255) NOT NULL,
  `type` varchar(255) NOT NULL,
  `remark` varchar(255) NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE `device` (
  `id` int NOT NULL AUTO_INCREMENT,
  `number` varchar(255) NOT NULL,
  `name` varchar(255) NOT NULL,
  `base_name` varchar(255) NOT NULL,
  `line_name` varchar(255) NOT NULL,
  `remark` varchar(255) NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE `plc_config` (
  `id` int NOT NULL AUTO_INCREMENT,
  `name` varchar(255) NOT NULL,
  `device_id` int NOT NULL,
  `Ip` varchar(255) NOT NULL,
  `port` int NOT NULL,
  `brand_specification_protocal` varchar(255) NOT NULL,
  `data_sort_rule` varchar(255) NULL,
  `cycle_read_time` int NOT NULL,
  `cycle_write_time` int NOT NULL,
  `remark` varchar(255) NULL,
  `is_enable` tinyint(1) NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE `plc_rw_config` (
  `id` int NOT NULL AUTO_INCREMENT,
  `name` varchar(255) NOT NULL,
  `plc_id` int NOT NULL,
  `area_name` varchar(255) NOT NULL,
  `start_address` varchar(255) NOT NULL,
  `length` int NOT NULL,
  `rw_mode` varchar(255) NOT NULL,
  `cycle` int NOT NULL,
  `address_type` int NOT NULL DEFAULT 1,
  `is_enable` tinyint(1) NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE `plc_address_config` (
  `id` int NOT NULL AUTO_INCREMENT,
  `plc_id` int NOT NULL,
  `plc_rw_id` int NOT NULL,
  `category_id` int NOT NULL,
  `parameter_name` varchar(255) NOT NULL,
  `type` varchar(255) NOT NULL,
  `length` int NOT NULL,
  `adress` varchar(255) NOT NULL,
  `uint` varchar(255) NULL,
  `remark` varchar(255) NULL,
  `is_monitor` tinyint(1) NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE `mes_interface_logs` (
  `id` int NOT NULL AUTO_INCREMENT,
  `log_date` datetime NOT NULL,
  `method` varchar(255) NOT NULL,
  `input_json` varchar(255) NULL,
  `output_json` varchar(255) NULL,
  `start_time` datetime NOT NULL,
  `end_time` datetime NOT NULL,
  `consuming_time` bigint NOT NULL,
  `success_flag` tinyint(1) NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE `web_interface_logs` (
  `id` int NOT NULL AUTO_INCREMENT,
  `log_date` datetime NOT NULL,
  `method` varchar(255) NOT NULL,
  `input_json` text NULL,
  `output_json` text NULL,
  `consuming_time` bigint NOT NULL,
  `success_flag` tinyint(1) NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

SET FOREIGN_KEY_CHECKS = 1;

-- 默认 global_setting
INSERT INTO `global_setting` (`name`,`value`,`type`,`unit`,`role_rank`,`remark`) VALUES
('ProcessCode','','string',NULL,2,'工序编码'),
('IsCycleReadPLC','false','bool',NULL,2,'是否开启循环读取PLC'),
('IsCycleWritePLC','false','bool',NULL,2,'是否开启循环写入PLC'),
('DefaultProject','','string',NULL,2,'启动默认显示项目(VM)'),
('CurrentUserAccount','guest','string',NULL,8,'当前用户'),
('IsMESEnabled','false','bool',NULL,2,'是否启用MES功能'),
('PLCConnectTime','5000','int','ms',2,'PLC重连时间'),
('PythonInstallDir','','string',NULL,2,'Python安装目录'),
('PythonSearchDir','','string',NULL,2,'Python搜索目录'),
('IsInitPythonEnvironment','false','bool',NULL,2,'是否初始化Python环境'),
('IsEnableWatchDogService','false','bool',NULL,2,'是否启用看门狗服务'),
('IsUseWeb','false','bool',NULL,2,'是否启用WEB服务'),
('isEnableHeartbeat','false','bool',NULL,2,'是否启用web心跳'),
('isEnableMachineErrorLog','false','bool',NULL,2,'是否启用上位机预警飞书推送'),
('MesProfile','test','string',NULL,2,'MES环境：生产/测试'),
('AppStartMainWindow','MainWindow','string',NULL,2,'启动主窗口类型');

-- 默认用户（密码见 DatabaseInitializer）
INSERT INTO `Users` (`user_account`,`user_name`,`password`,`role_id`,`created_time`) VALUES
('guest','访客(默认)','$2a$11$GQdP.7SEjd9PovZxjloe6.VJfMHM4Rs4A9mNPGQ4LRCC15QJBtMqO',0,NOW()),
('engineer','工程师(默认)','$2a$11$iexUYYtduvoAeJXKbKrdeedrLqPtK7Le1SR7zQydSvHdmTAf6hXSu',1,NOW()),
('admin','管理员(默认)','$2a$11$XD5CJG1cOp8p1yV9DSzeTeNQMjjzSHQPso21fjNOvqfAvte7azuLW',2,NOW()),
('sadmin','超级管理员(默认)','$2a$11$kgaV6j85/qUDtjPXtyMZkeCFwHlU9NWJ1a9QLuvfTrzWqWutu5YmS',3,NOW());

-- 默认 MES 配置
INSERT INTO `mes_setting` (`profile_name`,`key`,`value`,`type`,`remark`) VALUES
('test','BaseUrl','http://10.178.5.6:9002','string','MES服务器地址'),
('test','TimeoutSeconds','30','int','超时时间(秒)'),
('test','EnableRetry','false','bool','是否启用重试'),
('test','MaxRetryCount','3','int','重试次数');

-- 常用模块（首次启动后程序还会自动补全 workspace_project）
INSERT INTO `workspace_project` (`vm_class_name`,`is_enabled`) VALUES
('VM_SettingsPage',1),
('VM_UserCenter',1),
('VM_CTAutoSlicerPage',0);
