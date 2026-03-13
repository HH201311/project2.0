using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB
{
    public class ArithmeticModel : ModelBase
    {
        private bool isEnable;
        private string? arithmeticName;

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
        public string? ArithmeticName { get => arithmeticName; set => SetProperty(ref arithmeticName, value); }

        /// <summary>
        /// 算法类型
        /// </summary>
        [JsonIgnore]
        public EArithmeticType ArithmeticType { get; set; }

        /// <summary>
        /// 获取算法类型
        /// </summary>
        /// <param name="closedLoopType"></param>
        /// <param name="arithmeticName"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public static EArithmeticType GetEArithmeticType(EClosedLoopType closedLoopType, string arithmeticName)
        {
            switch (closedLoopType)
            {
                case EClosedLoopType.MD:
                    return EArithmeticType.MDControl;
                case EClosedLoopType.TD:
                    return EArithmeticType.TDControl;
                case EClosedLoopType.MDTD:
                    {
                        if (arithmeticName is null) throw new ArgumentNullException(nameof(arithmeticName), "算法名称不能为空");
                        var isMatch = Regex.Match(arithmeticName, @"^.*_MDTD_.*$");
                        if (isMatch.Success) return EArithmeticType.MDTDControl;
                        isMatch = Regex.Match(arithmeticName, @"^.*_MULTIMDTD_.*$");
                        if (isMatch.Success) return EArithmeticType.MULTIMDTDControl;
                        throw new ArgumentException($"算术名称 {arithmeticName} 格式不正确，应包含MDTD或MULTIMDTD！", nameof(arithmeticName));
                    }
                default:
                    throw new ArgumentException($"不支持的闭环类型: {closedLoopType}", nameof(closedLoopType));
            }
        }
    }
}
