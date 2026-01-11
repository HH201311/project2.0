using SqlSugar;

namespace SUNWODA_SEVB.Data.Models
{
    //[SugarIndex("project_name", nameof(Name), OrderByType.Desc)]
    //[SugarIndex("belong_to_vm", nameof(BelongToVM), OrderByType.Desc)]
    [SugarTable("project_setting", "应用自定义配置表")]
    public class ProjectSetting
    {
        [SugarColumn(ColumnName = "id", IsPrimaryKey = true, IsIdentity = true)]
        public int ID { get; set; }
        [SugarColumn(ColumnName = "name", ColumnDescription = "变量名称")]
        public string Name { get; set; } = null!;
        [SugarColumn(ColumnName = "belong_to_vm", ColumnDescription = "属于哪个应用的VM")]
        public string BelongToVM { get; set; } = null!;
        [SugarColumn(ColumnName = "value", ColumnDescription = "值",ColumnDataType ="text")]
        public string Value { get; set; } = null!;
        [SugarColumn(ColumnName = "type", ColumnDescription = "变量类型")]
        public string Type { get; set; } = null!;
        [SugarColumn(ColumnName = "unit", ColumnDescription = "单位", IsNullable = true)]
        public string Unit { get; set; } = null!;
        [SugarColumn(ColumnName = "role_rank", ColumnDescription = "用户权限")]
        public int RoleRank { get; set; }
        [SugarColumn(ColumnName = "remark", ColumnDescription = "注释", IsNullable = true)]
        public string Remark { get; set; } = null!;

    }
}