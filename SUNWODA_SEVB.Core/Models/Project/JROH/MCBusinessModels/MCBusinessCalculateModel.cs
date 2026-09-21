using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.JROH.MCBusinessModels
{
    /// <summary>
    /// 多级联动纠偏闭环计算过程类
    /// </summary>
    public class MCBusinessCalculateModel : ModelBase
    {
        private DateTime calculateTime;

        private MCCalculateModel posInfeedModel = new();
        private MCCalculateModel posProcess4Model = new();
        private MCCalculateModel posProcess3Model = new();
        private MCCalculateModel posProcess2Model = new();
        private MCCalculateModel posProcess1Model = new();
        private MCCalculateModel negInfeedModel = new();
        private MCCalculateModel negProcess3Model = new();
        private MCCalculateModel negProcess2Model = new();
        private MCCalculateModel negProcess1Model = new();
        private MCCalculateModel upperDiaphragmModel = new();
        private MCCalculateModel lowerDiaphragmModel = new();

        private string endMessage = string.Empty;

        /// <summary>
        /// 保存时间
        /// </summary>
        public DateTime CalculateTime { get => calculateTime; set => SetProperty(ref calculateTime, value); }

        #region 正极多级纠偏联动
        /// <summary>
        /// 正极入料纠偏计算数据
        /// </summary>
        public MCCalculateModel PosInfeedModel { get => posInfeedModel; set => SetProperty(ref posInfeedModel, value); }

        /// <summary>
        /// 正极过程纠偏4计算数据
        /// </summary>
        public MCCalculateModel PosProcess4Model { get => posProcess4Model; set => SetProperty(ref posProcess4Model, value); }

        /// <summary>
        /// 正极过程纠偏3计算数据
        /// </summary>
        public MCCalculateModel PosProcess3Model { get => posProcess3Model; set => SetProperty(ref posProcess3Model, value); }

        /// <summary>
        /// 正极过程纠偏2计算数据
        /// </summary>
        public MCCalculateModel PosProcess2Model { get => posProcess2Model; set => SetProperty(ref posProcess2Model, value); }

        /// <summary>
        /// 正极过程纠偏1计算数据
        /// </summary>
        public MCCalculateModel PosProcess1Model { get => posProcess1Model; set => SetProperty(ref posProcess1Model, value); }
        #endregion

        #region 负极多级纠偏联动
        /// <summary>
        /// 负极入料纠偏计算数据
        /// </summary>
        public MCCalculateModel NegInfeedModel { get => negInfeedModel; set => SetProperty(ref negInfeedModel, value); }

        /// <summary>
        /// 负极过程纠偏3计算数据
        /// </summary>
        public MCCalculateModel NegProcess3Model { get => negProcess3Model; set => SetProperty(ref negProcess3Model, value); }

        /// <summary>
        /// 负极过程纠偏2计算数据
        /// </summary>
        public MCCalculateModel NegProcess2Model { get => negProcess2Model; set => SetProperty(ref negProcess2Model, value); }

        /// <summary>
        /// 负极过程纠偏1计算数据
        /// </summary>
        public MCCalculateModel NegProcess1Model { get => negProcess1Model; set => SetProperty(ref negProcess1Model, value); }
        #endregion

        #region 隔膜多级纠偏联动
        /// <summary>
        /// 上隔膜过程纠偏计算数据
        /// </summary>
        public MCCalculateModel UpperDiaphragmModel { get => upperDiaphragmModel; set => SetProperty(ref upperDiaphragmModel, value); }

        /// <summary>
        /// 下隔膜过程纠偏计算数据
        /// </summary>
        public MCCalculateModel LowerDiaphragmModel { get => lowerDiaphragmModel; set => SetProperty(ref lowerDiaphragmModel, value); }
        #endregion

        /// <summary>
        /// 结束消息
        /// </summary>
        public string EndMessage { get => endMessage; set => SetProperty(ref endMessage, value); }
    }
}
