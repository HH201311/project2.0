using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB
{
    /// <summary>
    /// 算法缓存模型
    /// </summary>
    public class AlgorithmCacheModel : ModelBase
    {
        private string algorithmName = string.Empty;
        private AlgorithmWebUrlModel? webUrl;
        private string modlePath = string.Empty;
        private string version = string.Empty;
        private string pythonCode = string.Empty;

        /// <summary>
        /// 算法名称
        /// </summary>
        public string AlgorithmName { get => algorithmName; set => SetProperty(ref algorithmName, value); }

        /// <summary>
        /// 算法获取的WEB地址信息
        /// </summary>
        public AlgorithmWebUrlModel? WebUrl { get => webUrl; set => SetProperty(ref webUrl, value); }

        /// <summary>
        /// pyd算法文件路径
        /// </summary>
        public string ModlePath { get => modlePath; set => SetProperty(ref modlePath, value); }

        /// <summary>
        /// pyd算法版本信息
        /// </summary>
        public string Version { get => version; set => SetProperty(ref version, value); }

        /// <summary>
        /// Python代码
        /// </summary>
        public string PythonCode { get => pythonCode; set => SetProperty(ref pythonCode, value); }
    }
}
