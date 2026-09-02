using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels
{
    /// <summary>
    /// PLC地址保存选择项类
    /// </summary>
    public class AddressItemModel : ModelBase
    {
        private int id;
        private string name = string.Empty;
        private string address = string.Empty;

        /// <summary>
        /// 数据库索引
        /// </summary>
        public int ID { get => id; set => SetProperty(ref id, value); }

        /// <summary>
        /// 变量名称
        /// </summary>
        public string Name { get => name; set => SetProperty(ref name, value); }

        /// <summary>
        /// PLC地址
        /// </summary>
        public string Address { get => address; set => SetProperty(ref address, value); }
    }
}
