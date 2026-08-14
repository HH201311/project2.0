using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels
{
    /// <summary>
    /// 算法获取URL模型类
    /// </summary>
    public class AlgorithmWebUrlModel : ModelBase
    {
        private bool isEnable;
        private string? baseUrl;
        private string? name;
        private string? description;

        /// <summary>
        /// 是否启用此Url 进行算法文件获取
        /// </summary>
        public bool IsEnable { get => isEnable; set => SetProperty(ref isEnable, value); }

        /// <summary>
        /// URL地址，由Web组提供
        /// </summary>
        public string? BaseUrl { get => baseUrl; set => SetProperty(ref baseUrl, value); }

        /// <summary>
        /// URL名称
        /// </summary>
        public string? Name { get => name; set => SetProperty(ref name, value); }

        /// <summary>
        /// URL描述
        /// </summary>
        public string? Description { get => description; set => SetProperty(ref description, value); }
    }
}
