using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Enumerations.Project.CCTB.FormEnums
{
    /// <summary>
    /// 用户权限枚举，表示用户在系统中对某个功能或模块的权限级别
    /// </summary>
    public enum EUserPermission
    {
        /// <summary>
        /// 无权限
        /// </summary>
        //NoPermission,

        /// <summary>
        /// 仅可查看
        /// </summary>
        ReadOnly,

        /// <summary>
        /// 仅可编辑（不可查看）
        /// </summary>
        //EditOnly,

        /// <summary>
        /// 可查看且可编辑
        /// </summary>
        ReadAndEdit
    }
}
