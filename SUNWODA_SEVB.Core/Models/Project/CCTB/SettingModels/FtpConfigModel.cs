using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels
{
    /// <summary>
    /// FTP配置类
    /// </summary>
    public class FtpConfigModel : ModelBase
    {
        private string ftpPath = "ftp://10.215.114.36/";
        private string userName = "ftpuser";
        private string password = "123456";
        private bool isUseSSL = false;

        /// <summary>
        /// Ftp路径
        /// </summary>
        public string FtpPath { get => ftpPath; set => SetProperty(ref ftpPath, value); }

        /// <summary>
        /// 用户名
        /// </summary>
        public string UserName { get => userName; set => SetProperty(ref userName, value); }

        /// <summary>
        /// 密码
        /// </summary>
        public string Password { get => password; set => SetProperty(ref password, value); }

        /// <summary>
        /// 是否使用SSL
        /// </summary>
        public bool IsUseSSL { get => isUseSSL; set => SetProperty(ref isUseSSL, value); }
    }
}
