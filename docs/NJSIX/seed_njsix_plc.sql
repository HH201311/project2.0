-- NJSIX PACK六面检 · PLC 设备种子（欧姆龙 Fins TCP）
-- 库名以 appsettings.json 为准（默认 sunwoda_demo）
-- 执行后请完整退出并重启 SEVB，使 PLCHostedService 加载新配置。

USE sunwoda_demo;

-- 1) 写入/更新六面检 PLC（IP/端口按现场）
INSERT INTO plc_config (
  name, device_id, Ip, port,
  brand_specification_protocal, data_sort_rule,
  cycle_read_time, cycle_write_time, remark, is_enable
)
SELECT
  '六面检PLC', 1, '192.168.7.40', 9600,
  'Omron_Fins_TCP', 'Default',
  500, 500, 'NJSIX D1996.0报警/.1本机心跳/.2对方心跳', 1
FROM DUAL
WHERE NOT EXISTS (
  SELECT 1 FROM plc_config WHERE name = '六面检PLC'
);

UPDATE plc_config
SET
  Ip = '192.168.7.40',
  port = 9600,
  brand_specification_protocal = 'Omron_Fins_TCP',
  is_enable = 1,
  remark = 'NJSIX D1996.0报警/.1本机心跳/.2对方心跳'
WHERE name = '六面检PLC';

-- 2)（可选）本机只跑六面检时，停用其它 PLC，避免抢连接/误写
-- UPDATE plc_config SET is_enable = 0 WHERE name <> '六面检PLC';

-- 3) 核对
SELECT id, name, Ip, port, brand_specification_protocal, is_enable, remark
FROM plc_config
WHERE name = '六面检PLC';

-- 业务点位（程序内写，不必进 plc_rw_config）：
--   D1996.0 报警：开窗 true / 全关 false
--   D1996.1 本机心跳：约每秒翻转
--   D1996.2 对方心跳：读跳变；5 秒无跳变判断连
-- project_setting（VM_NJSIXPage）默认键：
--   PlcDeviceName = 六面检PLC
--   PlcNgAlarmBitAddress = D1996.0
--   PlcHeartbeatOutBitAddress = D1996.1
--   PlcHeartbeatInBitAddress = D1996.2
--   PlcHeartbeatIntervalMs = 1000
--   PlcHeartbeatTimeoutMs = 5000
