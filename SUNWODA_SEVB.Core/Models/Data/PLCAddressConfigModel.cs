namespace SUNWODA_SEVB.Core.Models.Data
{
    public class PLCAddressConfigModel
    {
        public int ID { get; set; }
        public int PLCID { get; set; }
        public int PLCRWID { get; set; }
        public int CategoryID { get; set; }
        public string ParameterName { get; set; } = null!;
        public string Type { get; set; } = null!;
        public ushort Length { get; set; }
        public string Address { get; set; } = null!;
        public string Unit { get; set; } = null!;
        public string Remark { get; set; } = null!;
        public bool IsMonitor { get; set; }
        public PLCAddressConfigModel() { }
        public PLCAddressConfigModel(
            int plcID,
            int plcRWID,
            int categoryID,
            string parameterName,
            string type,
            ushort length,
            string address,
            bool isMonitor
        )
        {
            PLCID = plcID;
            PLCRWID = plcRWID;
            CategoryID = categoryID;
            ParameterName = parameterName;
            Type = type;
            Length = length;
            Address = address;
            IsMonitor = isMonitor;
        }
    }
}
