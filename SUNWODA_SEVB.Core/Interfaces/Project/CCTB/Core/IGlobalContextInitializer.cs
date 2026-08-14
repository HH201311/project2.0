using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Interfaces.Project.CCTB.Core
{
    /// <summary>
    /// 上下文服务初始化
    /// </summary>
    public interface IGlobalContextInitializer
    {
        /// <summary>
        /// 初始化
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// 清理
        /// </summary>
        void Cleanup();
    }
}
