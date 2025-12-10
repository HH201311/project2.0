using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Component
{
    /// <summary>
    /// 数据行模型
    /// </summary>
    public class RowData : ModelBase
    {
        private string time = "";
        public string Time
        {
            get => time;
            set => SetProperty(ref time, value);
        }
        public ObservableCollection<CellData> Cells { get; set; } = new();
    }
}
