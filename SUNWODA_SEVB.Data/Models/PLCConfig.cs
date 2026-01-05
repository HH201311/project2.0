using SqlSugar;

namespace SUNWODA_SEVB.Data.Models
{
    //[SugarIndex("plc_name", nameof(Name), OrderByType.Desc, true)]
    [SugarTable("plc_config", "PLC设备配置表")]
    public class PLCConfig
    {
        [SugarColumn(ColumnName = "id", IsPrimaryKey = true, IsIdentity = true)]
        public int ID { get; set; }
        [SugarColumn(ColumnName = "name", ColumnDescription = "PLC名称")]
        public string Name { get; set; } = null!;
        [SugarColumn(ColumnName = "device_id", ColumnDescription = "设备ID")]
        public int DeviceID { get; set; }
        [SugarColumn(ColumnName = "Ip", ColumnDescription = "IP地址")]
        public string IP { get; set; } = null!;
        [SugarColumn(ColumnName = "port", ColumnDescription = "端口号")]
        public int Port { get; set; }
        [SugarColumn(ColumnName = "brand_specification_protocal", ColumnDescription = "品牌_规格_协议")]
        public string BrandSpecificationProtocal { get; set; } = null!;
        [SugarColumn(ColumnName = "data_sort_rule", ColumnDescription = "数据排列规则", IsNullable = true)]
        public string DataSortRule { get; set; } = null!;
        [SugarColumn(ColumnName = "cycle_read_time", ColumnDescription = "循环读取时间(ms)")]
        public int CycleReadTime { get; set; }
        [SugarColumn(ColumnName = "cycle_write_time", ColumnDescription = "循环写入时间(ms)")]
        public int CycleWriteTime { get; set; }
        [SugarColumn(ColumnName = "remark", ColumnDescription = "注释", IsNullable = true)]
        public string Remark { get; set; } = null!;
        [SugarColumn(ColumnName = "is_enable", ColumnDescription = "是否启用")]
        public bool IsEnable { get; set; }

    }
}
