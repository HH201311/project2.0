using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.JROH.MisBusinessModels
{
    /// <summary>
    /// 极耳错位闭环参数类
    /// </summary>
    /// <remarks>
    /// <para>本类当前为**模板骨架**：仅包含 JROH 四条业务共有的通用闭环控制参数（与 PW / OH / MC 参数模型的同名字段一致），用于打通「页面 - 服务 - 参数对话框 - project_setting 持久化」链路。</para>
    /// <para>⏸ 极耳错位专属参数（极耳层数、极耳角度阈值、极耳圈周长、限幅值与死区、调节频次等）<b>尚无现场资料，一律未添加</b>，禁止臆造（规范 B-09）。</para>
    /// <para>资料到位后在本类补充字段，并同步更新 <c>docs/JROH/配置键表.csv</c>、<c>docs/JROH/工艺阈值表.csv</c> 与参数对话框 <c>MisBusinessParamDialog.xaml</c>。</para>
    /// </remarks>
    public class MisBusinessParamModel : ModelBase
    {
        #region 配置参数
        private bool _isUse;
        private float _adjustCorrectionOfPos = 1;
        private float _adjustCorrectionOfNeg = -1;
        private bool _closedLoopEnablePos = true;
        private bool _closedLoopEnableNeg = true;

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsUse { get => _isUse; set => SetProperty(ref _isUse, value); }
        #endregion

        #region 闭环控制参数 - 控制逻辑
        /// <summary>
        /// 正极方向修正系数
        /// </summary>
        public float AdjustCorrectionOfPos { get => _adjustCorrectionOfPos; set => SetProperty(ref _adjustCorrectionOfPos, value); }

        /// <summary>
        /// 负极方向修正系数
        /// </summary>
        public float AdjustCorrectionOfNeg { get => _adjustCorrectionOfNeg; set => SetProperty(ref _adjustCorrectionOfNeg, value); }

        /// <summary>
        /// 正极闭环功能开启按钮
        /// </summary>
        public bool ClosedLoopEnablePos { get => _closedLoopEnablePos; set => SetProperty(ref _closedLoopEnablePos, value); }

        /// <summary>
        /// 负极闭环功能开启按钮
        /// </summary>
        public bool ClosedLoopEnableNeg { get => _closedLoopEnableNeg; set => SetProperty(ref _closedLoopEnableNeg, value); }
        #endregion
    }
}
