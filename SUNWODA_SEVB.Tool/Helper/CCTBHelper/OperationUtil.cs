using Newtonsoft.Json;
using Python.Runtime;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB.FormEnums;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB.AlarmModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.ArithmeticModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.RoleModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
using SUNWODA_SEVB.Tool.Extension;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Tool.Helper.CCTBHelper
{
    /// <summary>
    /// 常用操作工具类(CCTB专用)，包含一些常用的操作方法，如数据处理、计算等
    /// </summary>
    public class OperationUtil
    {
        private readonly ILoggerService _logger;

        /// <summary>
        /// 构造函数，注入日志服务
        /// </summary>
        /// <param name="logger"></param>
        public OperationUtil(ILoggerService logger)
        {
            _logger = logger;
        }

        #region 膜卷号
        /// <summary>
        /// 获取膜卷号
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>

        public string GetProductSn(string filePath)
        {
            var content = ReadFileSafely(filePath, Encoding.UTF8);
            var productSn = "";
            var match = Regex.Match(content,
                @"MachineID：*(.+)\r\nMoNumber：*(.+)\r\nPreProductSn：*(.+)\r\nOperatorID：*(.+)\r\nGroupCode：*(.+)\r\nProductSn：*(.+)\r\nModelNum：*(.+)\r\nOperatorNumber：*(.+)\r\n*(.+)");
            if (match.Success)
            {
                productSn = match.Groups[6].Value.Trim();
            }
            else
            {
                _logger.Error("未找到 ProductSn 字段。");
            }
            return productSn;
        }

        /// <summary>
        /// 安全地读取文件内容
        /// </summary>
        /// <param name="path"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        private string ReadFileSafely(string path, Encoding encoding)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sr = new StreamReader(fs, encoding, detectEncodingFromByteOrderMarks: true);
            return sr.ReadToEnd();
        }
        #endregion
    }
}
