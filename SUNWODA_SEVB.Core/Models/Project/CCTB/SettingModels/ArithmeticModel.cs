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
    /// 算法参数模型类
    /// </summary>
    public class AlgorithmModel : ModelBase
    {
        private bool isEnable;
        private string algorithmName = string.Empty;
        private string algorithmName1 = string.Empty;

        /// <summary>
        /// 请求调用的接口用户Key，由Web组提供
        /// </summary>
        [JsonIgnore]
        public const string AppKey = "PID_ONE";

        /// <summary>
        /// 运行模型类型:PRED或TRAIN
        /// </summary>
        [JsonIgnore]
        public const string ExecuteType = "PRED";

        /// <summary>
        /// 解密公共钥匙
        /// </summary>
        [JsonIgnore]
        public const string PublicKey = "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCAivKVcm+SvCC9nVCx3yd3b0IK1XK3x6+IRoD7szG0Dg76xrW2wW6N0FJ6gLzCDzeJvgba8ZM9H+u59M6+wxKKW1TVqhovGvWLFKwBAEKd04RjRRDb6szYCDakAEruIWMj85tjczJfDhirHej4Q3fdtulOmJqTTUNSdrwHEXZxZQIDAQAB";

        /// <summary>
        /// 是否启用获取PID算法文件
        /// </summary>
        public bool IsEnable { get => isEnable; set => SetProperty(ref isEnable, value); }

        /// <summary>
        /// 文件配置编码:由Web组提供
        /// </summary>
        public string AlgorithmName { get => algorithmName; set => SetProperty(ref algorithmName, value); }

        /// <summary>
        /// 文件配置编码:由Web组提供
        /// </summary>
        public string AlgorithmName1 { get => algorithmName1; set => SetProperty(ref algorithmName1, value); }

        /// <summary>
        /// 获取算法类型
        /// </summary>
        /// <param name="closedLoopType"></param>
        /// <param name="algorithmName"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public static EAlgorithmType GetEAlgorithmType(EClosedLoopType closedLoopType, string algorithmName, int code = 0)
        {
            switch (closedLoopType)
            {
                case EClosedLoopType.MD:
                    {
                        if (algorithmName is null) throw new ArgumentNullException(nameof(algorithmName), "算法名称不能为空");
                        if (code == 0) return EAlgorithmType.专家PID;
                        else if (code == 1) return EAlgorithmType.流量PID;
                        else return EAlgorithmType.Unknown;
                    }
                case EClosedLoopType.TD:
                    {
                        if (algorithmName is null) throw new ArgumentNullException(nameof(algorithmName), "算法名称不能为空");
                        var isMatch = Regex.Match(algorithmName, @"^.*_TD_.*$");
                        if (isMatch.Success) return EAlgorithmType.TDControl;
                        return EAlgorithmType.Unknown;
                    }
                case EClosedLoopType.MDTD:
                    {
                        if (algorithmName is null) throw new ArgumentNullException(nameof(algorithmName), "算法名称不能为空");
                        var isMatch = Regex.Match(algorithmName, @"^.*_MDTD_.*$");
                        if (isMatch.Success) return EAlgorithmType.MDTDControl;
                        isMatch = Regex.Match(algorithmName, @"^.*_MULTIMDTD_.*$");
                        if (isMatch.Success) return EAlgorithmType.MULTIMDTDControl;
                        return EAlgorithmType.Unknown;
                    }
                default:
                    throw new ArgumentException($"不支持的闭环类型: {closedLoopType}", nameof(closedLoopType));
            }
        }
    }
}
