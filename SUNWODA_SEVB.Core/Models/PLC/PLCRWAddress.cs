using SUNWODA_SEVB.Core.Common;
using System.Windows.Input;

namespace SUNWODA_SEVB.Core.Models.PLC
{
    public class PLCRWAddress : ModelBase
    {
        private int id;
        public int ID
        {
            get => id;
            set => SetProperty(ref id, value);
        }

        private int plcId;
        public int PlcId
        {
            get => plcId;
            set => SetProperty(ref plcId, value);
        }

        private int addressRangeId;
        public int AddressRangeId
        {
            get => addressRangeId;
            set => SetProperty(ref addressRangeId, value);
        }

        private int categoryId;
        public int CategoryId
        {
            get => categoryId;
            set => SetProperty(ref categoryId, value);
        }

        private string? parameterName;
        public string? ParameterName
        {
            get => parameterName;
            set => SetProperty(ref parameterName, value);
        }

        private string? type;
        public string? Type
        {
            get => type;
            set => SetProperty(ref type, value);
        }

        private ushort length;
        public ushort Length
        {
            get => length;
            set => SetProperty(ref length, value);
        }

        private string? address;
        public string? Address
        {
            get => address;
            set => SetProperty(ref address, value);
        }

        private string? unit;
        public string? Unit
        {
            get => unit;
            set => SetProperty(ref unit, value);
        }

        private string? remark;
        public string? Remark
        {
            get => remark;
            set => SetProperty(ref remark, value);
        }

        private int index;
        public int Index
        {
            get => index;
            set => SetProperty(ref index, value);
        }

        private int bitIndex;
        public int BitIndex
        {
            get => bitIndex;
            set => SetProperty(ref bitIndex, value);
        }

        private dynamic? monitorValue;
        public dynamic? MonitorValue
        {
            get => monitorValue;
            set => SetProperty(ref monitorValue, value);
        }

        private bool isShowOnPlotView;
        public bool IsShowOnPlotView
        {
            get => isShowOnPlotView;
            set => SetProperty(ref isShowOnPlotView, value);
        }

        private ICommand? isShowOnPlotViewChangedCommand;
        public ICommand? IsShowOnPlotViewChangedCommand
        {
            get => isShowOnPlotViewChangedCommand;
            set => SetProperty(ref isShowOnPlotViewChangedCommand, value);
        }

        public PLCRWAddress() { }

        public PLCRWAddress(
            int id,
            int plcId,
            int addressRangeId,
            int categoryId,
            string parameterName,
            string type,
            ushort length,
            string address,
            string unit,
            string remark,
            int index,
            int bitIndex = 0
        )
        {
            ID = id;
            PlcId = plcId;
            AddressRangeId = addressRangeId;
            CategoryId = categoryId;
            ParameterName = parameterName;
            Type = type;
            Length = length;
            Address = address;
            Unit = unit;
            Remark = remark;
            Index = index;
            BitIndex = bitIndex;
            MonitorValue = GetDefaultDynamicValue(Type);
            IsShowOnPlotView = false;
        }

        private dynamic? GetDefaultDynamicValue(string dataType)
        {
            dynamic? resultValue = null;
            switch (dataType.ToUpper())
            {
                case "STRING":
                    resultValue = "";
                    break;
                case "FLOAT":
                    resultValue = default(float);
                    break;
                case "DOUBLE":
                    resultValue = default(double);
                    break;
                case "BYTE":
                    resultValue = default(byte);
                    break;
                case "SHORT":
                    resultValue = default(short);
                    break;
                case "USHORT":
                    resultValue = default(ushort);
                    break;
                case "INT":
                    resultValue = default(int);
                    break;
                case "UINT":
                    resultValue = default(uint);
                    break;
                case "LONG":
                    resultValue = default(long);
                    break;
                case "ULONG":
                    resultValue = default(ulong);
                    break;
                case "BOOL":
                    resultValue = default(bool);
                    break;
                default:
                    break;
            }
            return resultValue;
        }
    }
}
