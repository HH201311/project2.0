using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.JROH.MCBusinessModels
{
    /// <summary>
    /// 多级联动纠偏闭环计算数据类
    /// </summary>
    public class MCCalculateModel : ModelBase
    {
        private int totalCount;
        private int posCount;
        private int negCount;
        private float offsetValue;
        private string endMessage = string.Empty;

        /// <summary>
        /// 总个数
        /// </summary>
        public int TotalCount { get => totalCount; set => SetProperty(ref totalCount, value); }

        /// <summary>
        /// 大于偏差中值个数
        /// </summary>
        public int PosCount { get => posCount; set => SetProperty(ref posCount, value); }

        /// <summary>
        /// 低于偏差中值个数
        /// </summary>
        public int NegCount { get => negCount; set => SetProperty(ref negCount, value); }

        /// <summary>
        /// 计算偏差值
        /// </summary>
        public float OffsetValue { get => offsetValue; set => SetProperty(ref offsetValue, value); }

        /// <summary>
        /// 计算结束提示信息
        /// </summary>
        public string EndMessage { get => endMessage; set => SetProperty(ref endMessage, value); }
    }
}
