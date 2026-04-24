using SUNWODA_SEVB.Core.Enumerations.Component;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB.FormEnums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.AlarmModels
{
    /// <summary>
    /// 报警信息默认配置类，包含系统中所有报警的默认信息配置
    /// </summary>
    public class AlarmDefaultConfig
    {
        /// <summary>
        /// 面密度异常报警
        /// </summary>
        public const string A001 = "A001";
        /// <summary>
        /// 面密度极差值过大报警
        /// </summary>
        public const string A002 = "A002";
        /// <summary>
        /// 测重仪通讯连接断开报警
        /// </summary>
        public const string A003 = "A003";
        /// <summary>
        /// 泵速下发异常
        /// </summary>
        public const string A004 = "A004";
        /// <summary>
        /// T块下发异常
        /// </summary>
        public const string A005 = "A005";
        /// <summary>
        /// T块执行异常报警
        /// </summary>
        public const string A006 = "A006";

        /// <summary>
        /// 报警信息默认配置表
        /// </summary>
        public static readonly Dictionary<string, AlarmConfigModel> Configs = new()
        {
            [A001] = new AlarmConfigModel(A001, "面密度异常报警", EAlarmLevel.一般, true),
            [A002] = new AlarmConfigModel(A002, "面密度极差值过大报警", EAlarmLevel.中等, true),
            [A003] = new AlarmConfigModel(A003, "测重仪通讯连接断开报警", EAlarmLevel.严重, true),
            [A004] = new AlarmConfigModel(A004, "泵速下发异常", EAlarmLevel.严重, true),
            [A005] = new AlarmConfigModel(A005, "T块下发异常", EAlarmLevel.严重, true),
            [A006] = new AlarmConfigModel(A006, "T块执行异常报警", EAlarmLevel.严重, true),
        };
    }
}
