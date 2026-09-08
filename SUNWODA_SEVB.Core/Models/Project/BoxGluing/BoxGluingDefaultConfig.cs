namespace SUNWODA_SEVB.Core.Models.Project.BoxGluing
{
    /// <summary>
    /// 与 plc点位表.csv / 工艺阈值表.csv 对齐的默认配置。
    /// </summary>
    public static class BoxGluingDefaultConfig
    {
        public static BoxGluingAcquisitionConfig Create()
        {
            return new BoxGluingAcquisitionConfig
            {
                SampleIntervalMs = 80,
                TriggerPollIntervalMs = 50,
                CsvRootPath = @"D:\BoxGluingData",
                ChartWindowPoints = 3000,
                UseSimulation = false,
                Channels =
                [
                    // CSV 列序仍为扭矩→压力→液位；地址按现场 PLC 点位表（压力 D1600～、扭矩 D1608～）
                    Chan("伺服扭矩A1", "D1608", "Torque", 1, "A", true),
                    Chan("伺服扭矩B1", "D1609", "Torque", 1, "B", true),
                    Chan("伺服扭矩A2", "D1610", "Torque", 2, "A", true),
                    Chan("伺服扭矩B2", "D1611", "Torque", 2, "B", true),
                    Chan("伺服扭矩A3", "D1612", "Torque", 3, "A", true),
                    Chan("伺服扭矩B3", "D1613", "Torque", 3, "B", true),
                    Chan("伺服扭矩A4", "D1614", "Torque", 4, "A", false),
                    Chan("伺服扭矩B4", "D1615", "Torque", 4, "B", false),
                    Chan("入口压力A1", "D1600", "Pressure", 1, "A", true),
                    Chan("入口压力B1", "D1601", "Pressure", 1, "B", true),
                    Chan("入口压力A2", "D1602", "Pressure", 2, "A", true),
                    Chan("入口压力B2", "D1603", "Pressure", 2, "B", true),
                    Chan("入口压力A3", "D1604", "Pressure", 3, "A", true),
                    Chan("入口压力B3", "D1605", "Pressure", 3, "B", true),
                    Chan("入口压力A4", "D1606", "Pressure", 4, "A", false),
                    Chan("入口压力B4", "D1607", "Pressure", 4, "B", false),
                    Chan("中转液位A1", "D1616", "Level", 1, "A", true),
                    Chan("中转液位B1", "D1617", "Level", 1, "B", true),
                    Chan("中转液位A2", "D1618", "Level", 2, "A", false),
                    Chan("中转液位B2", "D1619", "Level", 2, "B", false),
                ],
                Triggers =
                [
                    new BoxGluingTriggerConfig { Label = "自动出胶1信号", Address = "D1620", ActiveValue = 1, Station = 1 },
                    new BoxGluingTriggerConfig { Label = "自动出胶2信号", Address = "D1621", ActiveValue = 1, Station = 2 },
                    new BoxGluingTriggerConfig { Label = "自动出胶3信号", Address = "D1622", ActiveValue = 1, Station = 3 },
                ],
                Thresholds =
                [
                    new BoxGluingThresholdConfig { Group = "Pressure", MeterFrom = 1, MeterTo = 2, Max = 70 },
                    new BoxGluingThresholdConfig { Group = "Torque", MeterFrom = 1, MeterTo = 2, Max = 25 },
                    new BoxGluingThresholdConfig { Group = "Level", MeterFrom = 1, MeterTo = 3, Min = 10, Max = 60 },
                    new BoxGluingThresholdConfig { Group = "Pressure", MeterFrom = 3, MeterTo = 3, Max = 85 },
                ]
            };
        }

        private static BoxGluingChannelConfig Chan(
            string label,
            string address,
            string group,
            int meter,
            string side,
            bool enabled)
        {
            return new BoxGluingChannelConfig
            {
                Label = label,
                Address = address,
                Group = group,
                Meter = meter,
                Side = side,
                Enabled = enabled
            };
        }
    }
}
