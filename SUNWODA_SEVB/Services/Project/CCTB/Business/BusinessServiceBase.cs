using Dm.util;
using DocumentFormat.OpenXml.Drawing.Diagrams;
using HandyControl.Controls;
using HandyControl.Data;
using HandyControl.Tools.Extension;
using HslCommunication;
using Masuit.Tools.Hardware;
using Newtonsoft.Json;
using Python.Runtime;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Interfaces.Project.CCTB;
using SUNWODA_SEVB.Core.Models.PLC;
using SUNWODA_SEVB.Core.Models.Project.CCTB.BusinessModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.DHSocketServer;
using SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.WMSocketClient;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.Tool.Helper.CCTBHelper;
using SUNWODA_SEVB.ViewModels.Dialogs.CCTB;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.WMSocketClient.MeasurementFrame;

namespace SUNWODA_SEVB.Services.Project.CCTB.Business
{
    /// <summary>
    /// 业务服务基类
    /// </summary>
    public abstract class BusinessServiceBase<T> : ModelBase, IBusinessService where T : BusinessServiceBase<T>
    {
        private readonly ILoggerService<T> _logger;
        private readonly IPLCService _plcService;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly ICommunicationService _communicationService;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger"></param>
        protected BusinessServiceBase(ILoggerService<T> logger,
            IPLCService plcService,
            IProjectSettingRepository projectSettingRepository,
            ICommunicationService communicationService)
        {
            _logger = logger;
            _plcService = plcService;
            _projectSettingRepository = projectSettingRepository;
            _communicationService = communicationService;
        }

        protected CancellationTokenSource? _cts;
        protected readonly object _lockObj = new object();

        /// <summary>
        /// 业务类型
        /// </summary>
        public abstract EBusinessType BusinessType { get; }

        /// <summary>
        /// 闭环类型
        /// </summary>
        public abstract EClosedLoopType ClosedLoopType { get; }

        /// <summary>
        /// 业务名称
        /// </summary>
        public virtual string BusinessName => GetBusinessName(BusinessType);

        /// <summary>
        /// 运行状态
        /// </summary>
        public bool IsRunning { get; protected set; }

        /// <summary>
        /// 初始化业务相关资源
        /// </summary>
        protected abstract Task InitializeAsync(CancellationToken token);

        /// <summary>
        /// 执行业务逻辑
        /// </summary>
        protected abstract Task ExecuteAsync(CancellationToken token);

        /// <summary>
        /// 清理业务资源
        /// </summary>
        protected abstract Task CleanupAsync();

        /// <summary>
        /// 开始业务
        /// </summary>
        public virtual async Task StartAsync(CancellationToken token = default)
        {
            lock (_lockObj)
            {
                if (IsRunning)
                {
                    throw new InvalidOperationException($"业务 {BusinessType} 已在运行中");
                }
                IsRunning = true;
                _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            }

            try
            {
                await InitializeAsync(_cts.Token);
                await ExecuteAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                // 正常取消
                _logger.Warn($"业务 {BusinessType}:{BusinessName} 已取消运行！");
            }
            catch (Exception ex)
            {
                // 记录异常
                _logger.Error($"业务 {BusinessType}:{BusinessName} 运行异常[{ex.Message}]");
                throw;
            }
            finally
            {
                await CleanupAsync();
                IsRunning = false;
            }
        }

        /// <summary>
        /// 停止业务
        /// </summary>
        public virtual async Task StopAsync(CancellationToken token = default)
        {
            if (!IsRunning) return;

            _cts?.Cancel();

            // 等待业务完全停止
            while (IsRunning && !token.IsCancellationRequested)
            {
                await Task.Delay(100, token);
            }

            await CleanupAsync();
        }

        /// <summary>
        /// 重启业务
        /// </summary>
        public virtual async Task RestartAsync(CancellationToken token = default)
        {
            await StopAsync(token);
            await Task.Delay(1000, token); // 等待资源释放
            await StartAsync(token);
        }

        /// <summary>
        /// 获取业务名称
        /// </summary>
        /// <param name="businessType"></param>
        /// <returns></returns>
        protected virtual string GetBusinessName(EBusinessType businessType)
        {
            return businessType switch
            {
                EBusinessType.BackBusiness => "后台任务",
                EBusinessType.MDBusiness => "MD闭环",
                EBusinessType.TDBusiness => "TD闭环",
                EBusinessType.MDTDBusiness => "MDTD闭环",
                _ => "Unknow"
            };
        }

        #region PLC读写操作
        /// <summary>
        /// 读取PLC地址数据：返回类型Float
        /// </summary>
        /// <param name="parameterName"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        protected float ReadPLCValueFloat(string parameterName)
        {
            var value = ReadPLCValueString(parameterName);
            return float.TryParse(value, out float vFloat) ? vFloat : 0;
        }

        /// <summary>
        /// PLC读值
        /// </summary>
        /// <param name="parameterName"></param>
        /// <returns></returns>
        protected string ReadPLCValueString(string? parameterName)
        {
            var value = _plcService.RWAddresses.Values.FirstOrDefault(o => o.ParameterName == parameterName)?.MonitorValue;
            return value?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// PLC读值
        /// </summary>
        /// <param name="parameterName"></param>
        /// <returns></returns>
        protected PLCRWAddress? GetPLCAddressModel(string parameterName)
        {
            var value = _plcService.RWAddresses.Values.FirstOrDefault(o => o.ParameterName == parameterName);
            return value;
        }

        /// <summary>
        /// PLC写值
        /// </summary>
        /// <param name="parameterName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        protected async Task<OperateResult> WritePLCAsync(string parameterName, dynamic value)
        {
            var plcAddress = GetPLCAddressModel(parameterName);
            if (plcAddress == null) return new OperateResult { IsSuccess = false, Message = $"未查询到[{parameterName}]PLC地址配置！" };

            return await WritePLCAsync(plcAddress?.Address, plcAddress?.Type, value);
        }

        /// <summary>
        /// PLC写值
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        protected async Task<OperateResult> WritePLCAsync(string? address, string? type, dynamic value)
        {
            if (string.IsNullOrEmpty(address)) throw new ArgumentNullException(nameof(address));
            if (string.IsNullOrEmpty(type)) throw new ArgumentNullException(nameof(type));
            if (value == null) throw new ArgumentNullException(nameof(value));

            if (!(_plcService.GetPLCByName(CCTBCore.PLCName) is PLCDevice plc))
                throw new ArgumentException($"未查询到已连接的PLC设备[{CCTBCore.PLCName}]！", nameof(CCTBCore.PLCName));

            var result = await plc.WriteAsync(address, type, value);
            _logger.Debug($"PLC执行写入:地址[{address}]类型[{type}]值[{value}]，写入结果[{result.Message}]");
            return result;
        }

        /// <summary>
        /// 读取PLC地址ByIDs
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        protected void ReadPLCValuesByIDs(int[] ids, ref Dictionary<string, string> pairs)
        {
            foreach (var id in ids)
            {
                var address = _plcService.RWAddresses.Values.FirstOrDefault(o => o.ID == id);
                if (address == null) continue;
                var key = address.ParameterName ?? address.ID.ToString();
                pairs[key] = address.MonitorValue?.ToString() ?? string.Empty;
            }
        }
        #endregion

        #region 辅助方法
        #region 数据来源判断
        /// <summary>
        /// 校验是否是闭环需要的数据
        /// 备注：涂布客户端、涂布服务端、模头服务端、Mqtt客户端
        /// </summary>
        /// <param name="modelName"></param>
        /// <returns></returns>
        public virtual bool IsNeedCoatingData(string? modelName)
        {
            if (string.IsNullOrEmpty(modelName)) return false;

            var setting = LoadBasicFunctionSetting();
            if (setting == null) return false;

            // Socket客户端数据：大成
            if (setting.CoatingDataSource == EDataSourceOfClosedLoop.WMSocketClient &&
                modelName == ICommunicationService.ModuleToConfigGroupMap[ICommunicationService.WMSocketClientDeviceName])
                return true;

            // Socket服务端数据：锐奇
            if (setting.CoatingDataSource == EDataSourceOfClosedLoop.WMSocketServer &&
                modelName == ICommunicationService.ModuleToConfigGroupMap[ICommunicationService.WMSocketServerDeviceName])
                return true;

            // Socket服务端数据(模头)：上川
            if (setting.CoatingDataSource == EDataSourceOfClosedLoop.DHSocketServer &&
                modelName == ICommunicationService.ModuleToConfigGroupMap[ICommunicationService.DHSocketServerDeviceName])
                return true;

            // Mqtt客户端数据：曼恩斯特
            if (setting.CoatingDataSource == EDataSourceOfClosedLoop.DHMqttClient &&
               (modelName == ICommunicationService.ModuleToConfigGroupMap[ICommunicationService.DHAMqttClientDeviceName] ||
                modelName == ICommunicationService.ModuleToConfigGroupMap[ICommunicationService.DHBMqttClientDeviceName]))
                return true;

            return false;
        }

        /// <summary>
        /// 校验是否是闭环需要的数据
        /// 备注：模头服务端、Mqtt客户端
        /// </summary>
        /// <param name="modelName"></param>
        /// <returns></returns>
        public virtual bool IsNeedTBLockData(string? modelName)
        {
            if (string.IsNullOrEmpty(modelName)) return false;

            var setting = LoadBasicFunctionSetting();
            if (setting == null) return false;

            // Socket服务端数据(模头)：上川
            if (setting.TBlockDataSource == EDataSourceOfClosedLoop.DHSocketServer &&
               (modelName == ICommunicationService.ModuleToConfigGroupMap[ICommunicationService.DHASocketClientDeviceName] ||
                modelName == ICommunicationService.ModuleToConfigGroupMap[ICommunicationService.DHBSocketClientDeviceName]))
                return true;

            // Mqtt客户端数据：曼恩斯特
            if (setting.TBlockDataSource == EDataSourceOfClosedLoop.DHMqttClient &&
               (modelName == ICommunicationService.ModuleToConfigGroupMap[ICommunicationService.DHAMqttClientDeviceName] ||
                modelName == ICommunicationService.ModuleToConfigGroupMap[ICommunicationService.DHBMqttClientDeviceName]))
                return true;

            return false;
        }
        #endregion

        /// <summary>
        /// 获取时间
        /// </summary>
        /// <param name="ts"></param>
        /// <returns></returns>
        protected DateTime ToLocalDateTime(long ts)
        {
            var dto = ts >= 1_000_000_000_000L   // 13 位多为毫秒
                ? DateTimeOffset.FromUnixTimeMilliseconds(ts)
                : DateTimeOffset.FromUnixTimeSeconds(ts);

            return dto.ToLocalTime().DateTime;
        }

        /// <summary>
        /// 获取当前时间的毫秒级 Unix 时间戳（long 类型）
        /// </summary>
        protected long GetTimeStampMilliseconds()
        {
            // 基准时间：1970-01-01 00:00:00 UTC
            var unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            // 当前 UTC 时间 - 基准时间 = 时间差
            var timeSpan = DateTime.UtcNow - unixEpoch;
            // 转换为毫秒（long 类型）
            return (long)timeSpan.TotalMilliseconds;
        }

        /// <summary>
        /// 获取动态目标值
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="targetValueA"></param>
        /// <param name="targetValueB"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        protected float GetDynamicTargetValue(ESideType sideType, float targetValueA, float targetValueB)
        {
            return sideType switch
            {
                ESideType.A => targetValueA,
                ESideType.B => targetValueA + targetValueB,
                _ => throw new ArgumentException($"未实现 [{sideType}] 的动态目标设定值配置！", nameof(sideType))
            };
        }

        /// <summary>
        /// 获取公差值
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="toleranceA"></param>
        /// <param name="toleranceB"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        protected float GetToleranceValue(ESideType sideType, float toleranceA, float toleranceB)
        {
            return sideType switch
            {
                ESideType.A => toleranceA,
                ESideType.B => toleranceB,
                _ => throw new ArgumentException($"未实现 [{sideType}] 的动公差值配置！", nameof(sideType))
            };
        }

        /// <summary>
        /// 重量数据处理
        /// </summary>
        /// <param name="weightDataType"></param>
        /// <param name="datas"></param>
        /// <param name="gm"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        protected List<float> GetWeights(EWeightDataType weightDataType, List<float> datas, GeneralSettingsModel gm)
        {
            if (datas == null) throw new ArgumentNullException(nameof(datas));
            return weightDataType switch
            {
                EWeightDataType.干膜净重分区数据 or
                EWeightDataType.干膜总重分区数据 or
                EWeightDataType.湿膜净重分区数据 or
                EWeightDataType.湿膜总重分区数据 => new List<float>(datas),
                EWeightDataType.干膜净重毫米数据 or
                EWeightDataType.干膜总重毫米数据 or
                EWeightDataType.湿膜净重毫米数据 or
                EWeightDataType.湿膜总重毫米数据 => PartitionedData(datas.ToArray(), gm).ToList(),
                _ => throw new ArgumentException($"未实现 [{weightDataType}] 重量数据处理！", nameof(weightDataType))
            };
        }

        /// <summary>
        /// 后去目标值数据
        /// </summary>
        /// <param name="weightDataType"></param>
        /// <param name="substrateValue"></param>
        /// <param name="dynamicTargetValue"></param>
        /// <param name="pm"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        protected float GetTargetValue(EWeightDataType weightDataType, float substrateValue, float dynamicTargetValue, ClosedLoopSettingModel pm)
        {
            var targetValue1 = dynamicTargetValue + pm.TOffset;
            var targetValue2 = pm.TChange + pm.TOffset;
            var targetValue3 = substrateValue + dynamicTargetValue + pm.TOffset;
            var targetValue4 = substrateValue + pm.TChange + pm.TOffset;
            return weightDataType switch
            {
                EWeightDataType.干膜净重分区数据 or
                EWeightDataType.干膜净重毫米数据 or
                EWeightDataType.湿膜净重毫米数据 or
                EWeightDataType.湿膜净重分区数据 => pm.IsUseDynamicTargetValue ? targetValue1 : targetValue2,
                EWeightDataType.干膜总重分区数据 or
                EWeightDataType.干膜总重毫米数据 or
                EWeightDataType.湿膜总重分区数据 or
                EWeightDataType.湿膜总重毫米数据 => pm.IsUseDynamicTargetValue ? targetValue3 : targetValue4,
                _ => throw new ArgumentException($"未实现 [{weightDataType}] 目标值处理！", nameof(weightDataType))
            };
        }

        /// <summary>
        /// 获取扫描类型描述
        /// </summary>
        /// <param name="scanType"></param>
        /// <returns></returns>
        protected string GetScanType(float scanType)
        {
            return scanType switch
            {
                0 => "[0]未扫描",
                1 => "[1]同步扫描",
                2 => "[2]自由扫描",
                _ => $"[{scanType}]未知扫描类型"
            };
        }

        /// <summary>
        /// 获取进退刀数据
        /// </summary>
        /// <param name="sideType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="Exception"></exception>
        protected float GetKnifeSwitch(ESideType sideType)
        {
            var parameterName = sideType switch
            {
                ESideType.A => "机头A模头位置状态",
                ESideType.B => "机头B模头位置状态",
                _ => throw new ArgumentException($"未实现 [{sideType}] 进退刀 PLC地址！")
            };

            return ReadPLCValueFloat(parameterName);
        }

        /// <summary>
        /// 获取泵速设定值
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="pumpCount"></param>
        /// <param name="isRound"></param>
        /// <param name="digits"></param>
        /// <returns></returns>
        protected Dictionary<byte, (float, float)> GetPumpSetValues(ESideType sideType, byte pumpCount, bool isRound, byte digits)
        {
            var result = new Dictionary<byte, (float, float)>();
            for (var i = 0; i < pumpCount; i++)
            {
                var parameterName = $"机头{sideType}浆料设定泵速{i + 1}";
                var speed = ReadPLCValueFloat(parameterName);
                // 不四舍五入
                if (!isRound)
                {
                    var pow = Math.Pow(10, digits);
                    // 截取小数位长度
                    speed = (float)(Math.Truncate(speed * pow) / pow);
                }
                speed = (float)Math.Round(speed, digits);

                parameterName = $"机头{sideType}螺杆泵启动标识{i + 1}";
                var flag = ReadPLCValueFloat(parameterName);

                result[(byte)(i + 1)] = (speed, flag);
            }

            return result;
        }

        /// <summary>
        /// 获取闭环开关信号
        /// </summary>
        /// <param name="sideType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="Exception"></exception>
        protected float GetCLSwitchSignal(ESideType sideType)
        {
            var parameterName = sideType switch
            {
                ESideType.A => "机头A泵速闭环有效",
                ESideType.B => "机头B泵速闭环有效",
                _ => throw new ArgumentException($"未实现 [{sideType}] 闭环开关信号 PLC地址！")
            };

            return ReadPLCValueFloat(parameterName);
        }

        /// <summary>
        /// 获取闭环开关信号
        /// </summary>
        /// <param name="sideType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="Exception"></exception>
        protected float GetCLSwitchSignal_Supplier(ESideType sideType)
        {
            var parameterName = sideType switch
            {
                ESideType.A => "机头A泵速闭环有效_供应商",
                ESideType.B => "机头B泵速闭环有效_供应商",
                _ => throw new ArgumentException($"未实现 [{sideType}] 闭环开关信号_供应商 PLC地址！")
            };

            return ReadPLCValueFloat(parameterName);
        }

        /// <summary>
        /// 毫米数据分区计算
        /// </summary>
        /// <param name="datas"></param>
        /// <param name="gm"></param>
        /// <returns></returns>
        private float[] PartitionedData(float[] datas, GeneralSettingsModel gm)
        {
            if (gm.PartitionedModels.Count <= 0) throw new ArgumentException("毫米分区配置参数异常！", nameof(gm));
            if (datas == null || datas.Length == 0) throw new ArgumentException("数据数组不能为空!", nameof(datas));

            var result = new List<float>();
            foreach (var pm in gm.PartitionedModels)
            {
                // 获取分区的毫米数据，数据不足则补0
                var pDatas = datas.ToList();
                if (pDatas.Count < pm.EndIndex) pDatas.AddRange(Enumerable.Repeat(0f, pm.EndIndex - pDatas.Count));
                // 截取当前膜区起止位置的所有数据
                var pmDatas = pDatas.Skip(pm.StartIndex - 1).Take(pm.EndIndex - (pm.StartIndex - 1)).ToList();
                // 计算商和余数 (有效涂膜区/分区个数)
                var quotient = Math.DivRem(pmDatas.Count, pm.PartitionedCount, out int remainder);
                // 有效涂膜区%分区个数 > (1/2)*分区个数
                var coefficient1 = 2 * remainder > pm.PartitionedCount;
                int skipCount = 0, takeCount = 0;
                for (int i = 0; i < pm.PartitionedCount; i++)
                {
                    skipCount += takeCount;
                    if (remainder == 0)// 如果分配均匀
                    {
                        takeCount = quotient;
                    }
                    else
                    {
                        if (coefficient1)// 如果：有效涂膜区%分区个数 > (1/2)*分区个数
                        {
                            var quotient2 = Math.DivRem((pm.PartitionedCount - remainder), 2, out int remainder2);
                            // 是否是余数边缘部分
                            var coefficient2 = i < quotient2 || i >= (quotient2 + remainder);
                            takeCount = quotient + (coefficient2 ? 0 : 1);
                        }
                        else
                        {
                            var quotient2 = Math.DivRem(remainder, 2, out int remainder2);
                            // 是否是余数边缘部分
                            var coefficient2 = i < quotient2 || i >= (quotient2 + (pm.PartitionedCount - remainder));
                            takeCount = quotient + (coefficient2 ? 1 : 0);
                        }
                    }
                    // 获取分区数据均值
                    var newData = pmDatas.Skip(skipCount).Take(takeCount).Average();
                    result.Add(newData);
                }
                // 膜区后加0用作分隔
                result.Add(0);
            }
            return result.ToArray();
        }

        #region 数据判异
        /// <summary>
        /// 剔除规则 
        /// 备注：
        ///     1、0数据判异规则；
        ///     2、速度判异规则
        ///     3、阈值判异规则
        ///     4、公差值判异规则（MD）
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="closedLoopType"></param>
        /// <param name="sideType"></param>
        /// <param name="pm"></param>
        /// <param name="cm"></param>
        protected void ApplyEliminationRules<TModel>(EClosedLoopType closedLoopType, ESideType sideType, ClosedLoopSettingModel pm, ref TModel cm) where TModel : BaseCalculateModel
        {
            var data = cm.Weights.Where(x => x != 0).ToArray();
            #region 0数据判异规则
            if (data.Count() == 0)// 超规格剔除 去除空值
            {
                _logger.Info($"{closedLoopType}剔除{cm.MessageTag}架[超规格:0]数据" + string.Join(",", cm.Weights));
                cm.Rid_JudgeResult = true;
                cm.Rid_JudgeDescription = "触发0数据判异规则";
                return;
            }
            #endregion

            #region 速度判异规则
            if (cm.Velocity < pm.Rid_VelocityCondition)// 超规格剔除
            {
                _logger.Info($"{closedLoopType}剔除{cm.MessageTag}架[超规格:走带速度{cm.Velocity} < {pm.Rid_VelocityCondition}]数据" + string.Join(",", cm.Weights));
                cm.Rid_JudgeResult = true;
                cm.Rid_JudgeDescription = "触发速度判异规则";
                return;
            }
            #endregion

            #region 阈值判异规则
            var updata = cm.TargetValue + Math.Abs(pm.Rid_Threshold);
            var lowdata = cm.TargetValue - Math.Abs(pm.Rid_Threshold);
            var count = data.Where(x => (x >= updata) || (x <= lowdata)).ToArray().Count();
            if (count > pm.Rid_Count)//超规格剔除
            {
                _logger.Info($"{closedLoopType}剔除{sideType}面-{cm.MessageTag}架[超规格:{cm.TargetValue}±{Math.Abs(pm.Rid_Threshold)}]数据" + string.Join(",", cm.Weights));
                cm.Rid_JudgeResult = true;
                cm.Rid_JudgeDescription = $"触发阈值判异规则[{count} > {pm.Rid_Count}]";
                return;
            }
            #endregion

            #region 公差值判异规则
            if ((cm is MDTDCalculateModel mdtdCM) && mdtdCM.Tolerance == 0)
            {
                _logger.Info($"{closedLoopType}剔除{cm.MessageTag}架[超规格:公差值{mdtdCM.Tolerance}]数据");
                cm.Rid_JudgeResult = true;
                cm.Rid_JudgeDescription = "触发公差值判异规则";
                return;
            }
            #endregion

            #region T块数据判异
            if ((cm is TDCalculateModel tdCM) && tdCM.TBlockData == null)
            {
                _logger.Info($"{closedLoopType}剔除{cm.MessageTag}架[数据为空:公差值{JsonConvert.SerializeObject(tdCM.TBlockData)}]数据");
                cm.Rid_JudgeResult = true;
                cm.Rid_JudgeDescription = "触发T块数据判异规则";
                return;
            }
            if ((cm is MDTDCalculateModel mdtdCM1) && mdtdCM1.TBlockData == null)
            {
                _logger.Info($"{closedLoopType}剔除{cm.MessageTag}架[数据为空:公差值{JsonConvert.SerializeObject(mdtdCM1.TBlockData)}]数据");
                cm.Rid_JudgeResult = true;
                cm.Rid_JudgeDescription = "触发T块数据判异规则";
                return;
            }
            #endregion

            cm.Rid_JudgeResult = false;
            cm.Rid_JudgeDescription = "未触发判异规则";
        }
        #endregion

        #region 数据预处理
        /// <summary>
        /// 数据源更新判断
        /// 备注：
        ///     返回数据源更新之后的数据列表
        /// </summary>
        /// <typeparam name="TModel"></typeparam>
        /// <param name="datas"></param>
        /// <param name="pm"></param>
        /// <returns></returns>
        protected List<(CoatingDataModel cd, TModel cm)> GetCacheAfterReboot<TModel>(List<(CoatingDataModel cd, TModel cm)> datas, ClosedLoopSettingModel pm) where TModel : BaseCalculateModel
        {
            if (datas.Count <= 0) return new List<(CoatingDataModel cd, TModel cm)>();
            var currentData = datas.Last();
            var lastIndex = datas.FindLastIndex(o => o.cd.WeightDataType != currentData.cd.WeightDataType);
            var dataCache = datas.Skip(lastIndex + 1).ToList();
            return GetCacheAfterRebootByZero(dataCache, pm);
        }

        /// <summary>
        /// 开机/重启判断
        /// 备注：
        ///     返回开机/重启/进刀(若开启)之后的数据列表：剔除开头0数据/固定趟数数据
        /// </summary>
        /// <typeparam name="TModel"></typeparam>
        /// <param name="datas"></param>
        /// <param name="pm"></param>
        /// <returns></returns>
        private List<(CoatingDataModel cd, TModel cm)> GetCacheAfterRebootByZero<TModel>(List<(CoatingDataModel cd, TModel cm)> datas, ClosedLoopSettingModel pm) where TModel : BaseCalculateModel
        {
            // 初始化返回数据
            var result = new List<(CoatingDataModel cd, TModel cm)>();
            if (datas.Count <= 0) return result;
            var currentData = datas.Last();
            switch (pm.BootUp_EBootUpType)
            {
                case EBootUpType.DataNumber:
                    var count = 0;// 0数据连续计数
                    for (int i = datas.Count - 1; i >= 0; i--)
                    {
                        result.Insert(0, datas[i]);
                        // 0数据判断
                        count = (datas[i].cm.WeightAvg == 0) ? (count + 1) : 0;
                        if (count >= pm.BootUp_ZeroCount)// 剔除0数据
                        {
                            result.RemoveRange(0, count);
                            break;
                        }
                    }
                    break;
                case EBootUpType.Time:
                    for (int i = datas.Count - 1; i >= 0; i--)
                    {
                        result.Insert(0, datas[i]);
                        // 开机时间数据判断
                        var lastGatherTime = i - 1 > 0 ? datas[i - 1].cd.GatherTime : DateTime.MinValue;
                        var bootTime = (result[0].cd.GatherTime - lastGatherTime).TotalSeconds;
                        if (bootTime >= pm.BootUp_BootTime) break;
                    }
                    break;
            }

            // 进刀(1)闭环开启，退刀(0)闭环关闭
            var tBKnifeIndex = result.FindLastIndex(o => o.cm.KnifeSwitch == 0);
            if (currentData.cm.IsUseJudgeOfKnife) result.RemoveRange(0, tBKnifeIndex + 1);

            // 开机剔除固定数据量
            result.RemoveRange(0, Math.Min(result.Count, pm.BootUp_RidCount));
            return result;
        }

        /// <summary>
        /// 异常数据间隔判断判断
        /// 备注：
        ///     返回异常数据间隔之后的列表：已剔除开头异常数据
        /// </summary>
        /// <typeparam name="TModel"></typeparam>
        /// <param name="datas"></param>
        /// <param name="pm"></param>
        /// <returns></returns>
        protected List<(CoatingDataModel cd, TModel cm)> GetCacheAfterFiltrate<TModel>(List<(CoatingDataModel cd, TModel cm)> datas, ClosedLoopSettingModel pm) where TModel : BaseCalculateModel
        {
            var result = new List<(CoatingDataModel cd, TModel cm)>();
            if (datas.Count <= 0) return result;
            var currentData = datas.Last();
            var count = 0; // 异常数据连续计数
            for (int i = datas.Count - 1; i >= 0; i--)
            {
                result.Insert(0, datas[i]);
                // 异常数据判断
                count = datas[i].cm.Rid_JudgeResult ? (count + 1) : 0;
                if (count >= pm.Rid_Interval)
                {
                    result.RemoveRange(0, count);
                    break;
                }
            }
            return result;
        }
        #endregion
        #endregion

        #region 参数获取
        /// <summary>
        /// 获取闭环开关配置
        /// </summary>
        /// <param name="sideType"></param>
        /// <returns></returns>
        protected bool LoadClosedSwitch(ESideType sideType)
        {
            var instance = GlobalContextModel.Instance;
            var isEnable = (sideType, ClosedLoopType) switch
            {
                (ESideType.A, EClosedLoopType.MD) => instance.ClosedLoopSwitchMDA && !instance.ClosedLoopSwitchTDA,
                (ESideType.A, EClosedLoopType.TD) => !instance.ClosedLoopSwitchMDA && instance.ClosedLoopSwitchTDA,
                (ESideType.A, EClosedLoopType.MDTD) => instance.ClosedLoopSwitchMDA && instance.ClosedLoopSwitchTDA,
                (ESideType.A, EClosedLoopType.CCD) => instance.ClosedLoopSwitchCCDA,
                (ESideType.B, EClosedLoopType.MD) => instance.ClosedLoopSwitchMDB && !instance.ClosedLoopSwitchTDB,
                (ESideType.B, EClosedLoopType.TD) => !instance.ClosedLoopSwitchMDB && instance.ClosedLoopSwitchTDB,
                (ESideType.B, EClosedLoopType.MDTD) => instance.ClosedLoopSwitchMDB && instance.ClosedLoopSwitchTDB,
                (ESideType.B, EClosedLoopType.CCD) => instance.ClosedLoopSwitchCCDB,
                _ => false
            };
            return isEnable;
        }

        /// <summary>
        /// 加载业务参数配置
        /// </summary>
        /// <param name="sideType"></param>
        /// <returns></returns>
        protected ClosedLoopSettingModel LoadParamSettingModel(ESideType sideType)
        {
            var pm = (sideType, ClosedLoopType) switch
            {
                (ESideType.A, EClosedLoopType.MD) => CCTBCore.ClosedLoopSettingMDA,
                (ESideType.A, EClosedLoopType.TD) => CCTBCore.ClosedLoopSettingMDA,
                (ESideType.A, EClosedLoopType.MDTD) => CCTBCore.ClosedLoopSettingMDA,
                (ESideType.B, EClosedLoopType.MD) => CCTBCore.ClosedLoopSettingMDA,
                (ESideType.B, EClosedLoopType.TD) => CCTBCore.ClosedLoopSettingMDA,
                (ESideType.B, EClosedLoopType.MDTD) => CCTBCore.ClosedLoopSettingMDA,
                _ => null
            };

            return pm ?? _projectSettingRepository.LoadClosedLoopSettingProjectParameters(ClosedLoopType, sideType);
        }

        /// <summary>
        /// 获取通用配置
        /// </summary>
        /// <returns></returns>
        protected GeneralSettingsModel LoadGeneralSettingsModel()
        {
            return CCTBCore.GeneralSettings ?? _projectSettingRepository.LoadGeneralSettingsProjectParameters();
        }

        /// <summary>
        /// 获取基础配置参数
        /// </summary>
        /// <returns></returns>
        protected BasicFunctionSettingModel LoadBasicFunctionSetting()
        {
            return CCTBCore.BasicFunctionSetting ?? _projectSettingRepository.LoadBasicFunctionSettingProjectParameters();
        }

        /// <summary>
        /// 获取特殊配置参数
        /// </summary>
        /// <returns></returns>
        protected SpecialFunctionSettingModel LoadSpecialFunctionSetting()
        {
            return CCTBCore.SpecialFunctionSetting ?? _projectSettingRepository.LoadSpecialFunctionSettingProjectParameters();
        }

        /// <summary>
        /// 获取膜卷号
        /// </summary>
        /// <returns></returns>
        protected string GetProductSN()
        {
            var bf = CCTBCore.BasicFunctionSetting;
            if (bf == null) return string.Empty;

            return bf.ModeOfRollNumReading switch
            {
                ERollNumReading.RemoteFile => CCTBCore.GetProductSn(bf.PathOfRollNumReadingTxt),
                ERollNumReading.PLC => ReadPLCValueString("膜卷号"),
                _ => string.Empty,
            };
        }
        #endregion

        #region 协议相关
        /// <summary>
        /// 获取命令数据
        /// </summary>
        /// <param name="attachment"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        protected byte[] BuildBrRequestPacket(string attachment, byte[]? data = default)
        {
            // 创建请求帧
            var header = FrameHeader.ReadRequest;
            var request = MeasurementFrame.Create(header, attachment, ReadOnlySpan<byte>.Empty);
            var result = request.Serialize();

            return result;
        }

        /// <summary>
        /// 创建命令行
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="command"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        protected byte[] BuildDHRequestPacket(int sequence, string command, string data = "")
        {
            string header = "\u0002", interval = "\u0009", Tail = "\u0003";
            var dataBuffer = Encoding.UTF8.GetBytes(data);
            var arqCRC32 = new ArqCRC32();
            var dataCRC32 = arqCRC32.Get_CRC(dataBuffer, dataBuffer.Length);
            var message = header + sequence + interval + command + interval + data + interval + dataCRC32 + Tail;
            return Encoding.UTF8.GetBytes(message);
        }
        #endregion

        #region 调机相关
        /// <summary>
        /// 泵速调机下发
        /// </summary>
        /// <param name="adjustMode"></param>
        /// <param name="dataSourceType"></param>
        /// <param name="cd"></param>
        /// <param name="newB"></param>
        /// <param name="isAdjust"></param>
        /// <exception cref="Exception"></exception>
        protected async void SetNewBCommon(EModeOfPumpAdjust adjustMode, EDataSourceOfClosedLoop dataSourceType, CoatingDataModel cd, Dictionary<int, (float setB, float offsetB)> bPairs, bool isAdjust = true)
        {
            var positions = bPairs.Values.Select(o => o.setB).ToList();
            var adjustOffsets = bPairs.Values.Select(v => v.offsetB).ToList();
            if (!isAdjust)
            {
                _logger.AdjustmentBRecommend(cd.SideType, ClosedLoopType, cd.PassCount, new List<float>(), adjustOffsets);
                var msg = $"推荐 {ClosedLoopType} 业务[{cd.SideType}]面泵速调节值:[{JsonConvert.SerializeObject(bPairs)}]";
                CCTBCore.ShowGrowWarning(msg, "调机推荐");
                return;
            }

            try
            {
                _logger.Info($"{ClosedLoopType} 业务[{cd.SideType}]下发泵速&调机值！");
                var am = _projectSettingRepository.LoadMqttAddressSettingProjectParameters(cd.SideType);
                var task = adjustMode switch
                {
                    EModeOfPumpAdjust.PLC => SetNewBByPLC(cd, bPairs),
                    EModeOfPumpAdjust.Network => dataSourceType == EDataSourceOfClosedLoop.DHMqttClient ? SetNewBByMqtt(am, cd, bPairs) : SetNewBBySocket(cd, bPairs),
                    _ => throw new Exception($"不支持的数据来源及下发方式[{adjustMode}]")
                };
                await task.WaitAsync(_cts?.Token ?? default);

                _logger.AdjustmentBSuccess(cd.SideType, ClosedLoopType, cd.PassCount, new List<float>(), adjustOffsets);
                var msg1 = $"[{ClosedLoopType}]业务[{cd.SideType}]面泵速调节值:{JsonConvert.SerializeObject(bPairs)}";
                CCTBCore.ShowGrowSuccess(msg1, "调机下发成功");
            }
            catch (Exception ex)
            {
                _logger.AdjustmentBFailed(cd.SideType, ClosedLoopType, cd.PassCount, new List<float>(), adjustOffsets);
                var msg1 = $"[{ClosedLoopType}]业务[{cd.SideType}]面泵速调节值下发异常:{ex.Message}";
                CCTBCore.ShowGrowError(msg1, "调机下发失败");
            }
        }

        /// <summary>
        /// PLC方式下发泵速
        /// </summary>
        /// <param name="cd"></param>
        /// <param name="newB"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task SetNewBByPLC(CoatingDataModel cd, Dictionary<int, (float setB, float offsetB)> bPairs)
        {
            foreach (var kv in bPairs)
            {
                // 1.清除完成信号
                var parameterName = $"机头{cd.SideType}泵速微调写入完成{kv.Key}";
                var address = GetPLCAddressModel(parameterName);
                if (address != null)
                {
                    var re = await WritePLCAsync(parameterName, 0);
                    if (!re.IsSuccess)
                    {
                        var message = $"{ClosedLoopType} 业务泵速完成信号复位[{parameterName}:{0}]失败:{re.Message}";
                        throw new Exception(message);
                    }
                }

                // 2.下发泵速调机值
                parameterName = $"机头{cd.SideType}泵速微调写入值{kv.Key}";
                var re1 = await WritePLCAsync(parameterName, kv.Value.offsetB);
                if (!re1.IsSuccess)
                {
                    var message = $"{ClosedLoopType} 业务泵速调机值下发[{parameterName}:{0}]失败:{re1.Message}";
                    throw new Exception(message);
                }

                // 3.下发更新标识
                parameterName = $"机头{cd.SideType}泵速微调写入使能{kv.Key}";
                re1 = await WritePLCAsync(parameterName, 1);
                if (!re1.IsSuccess)
                {
                    var message = $"{ClosedLoopType} 业务泵速调机下发完成信号置位[{parameterName}:{0}]失败:{re1.Message}";
                    throw new Exception(message);
                }
            }
        }

        /// <summary>
        /// Mqtt方式下发泵速
        /// </summary>
        /// <param name="cSetting"></param>
        /// <param name="cd"></param>
        /// <param name="newB1"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task SetNewBByMqtt(MqttAddressModel am, CoatingDataModel cd, Dictionary<int, (float setB, float offsetB)> bPairs)
        {
            var protocol = am.Protocol;
            var flagAddress = am.FlagAddress;

            var client = cd.SideType switch
            {
                ESideType.A => _communicationService.DHAMqttClient,
                ESideType.B => _communicationService.DHBMqttClient,
                _ => throw new Exception($"不支持的AB面类型[{cd.SideType}]！")
            };

            if (client == null || !client.IsConnected) throw new Exception($"[{client?.ClientId}]设备未启用或未连接！");

            var clientId = client.ActiveOptions.ClientId;
            foreach (var kv in bPairs)
            {
                if (am.PumpAddresses.Count < kv.Key) throw new Exception($"Mqtt 客户端不包含配置 泵{kv.Key} 地址！");
                var address = am.PumpAddresses[kv.Key];
                if (string.IsNullOrEmpty(address)) throw new Exception($"Mqtt 客户端配置 泵{kv.Key} 地址不可为空！");
                var speed = kv.Value.offsetB;
                var speedTopic = $"v2.0/{protocol}@plcgw/mteng_A@manst/0/manst/group/{protocol}/REQ/SET/0/float/{address}/1";
                if (protocol == "s7")
                {
                    var matches = Regex.Matches(address, @"\d+");
                    var (db, block) = (int.Parse(matches[0].Value), int.Parse(matches[1].Value));
                    speedTopic = $"v2.0/{protocol}@plcgw/mteng_A@manst/0/manst/group/{protocol}/REQ/SET/0/float/{block}/{db}/1";
                }
                var re = await client.PublishAsync(speedTopic, speed.ToString());
                if (!re.IsSuccess)
                {
                    var message = $"{ClosedLoopType} 业务泵速[{address}:{speed}]下发失败:{re.Message}";
                    throw new Exception(message);
                }
            }

            await Task.Delay(100); // 适当延时，避免消息过快导致PLC处理不过来
            var flagTopic = $"v2.0/{protocol}@plcgw/mteng_A@manst/0/manst/group/{protocol}/REQ/SET/0/float/{flagAddress}/1";
            var re1 = await client.PublishAsync(flagTopic, "3.141592");
            if (!re1.IsSuccess)
            {
                var message = $"{ClosedLoopType} 业务泵速flag[{flagAddress}:{3.141592}]下发失败:{re1.Message}";
                throw new Exception(message);
            }
        }

        /// <summary>
        /// Socket方式下发泵速
        /// </summary>
        /// <param name="cd"></param>
        /// <param name="newB"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task SetNewBBySocket(CoatingDataModel cd, Dictionary<int, (float setB, float offsetB)> bPairs)
        {
            var quest = new MAdjustRequestModel()
            {
                AdjustTime = DateTime.Now,
                AdjustPumpLis = new List<AdjustPump>()
            };
            foreach (var kv in bPairs)
            {
                var adjustPump = new AdjustPump()
                {
                    PumpTag = $"泵{kv.Key}",
                    AdjustValue = kv.Value.offsetB
                };
                quest.AdjustPumpLis.Add(adjustPump);
            }

            var client = cd.SideType switch
            {
                ESideType.A => _communicationService.DHASocketClient,
                ESideType.B => _communicationService.DHBSocketClient,
                _ => throw new Exception($"不支持的AB面类型[{cd.SideType}]！")
            };

            if (client == null || !client.IsConnected) throw new Exception($"[{client?.ClientId}]设备未启用或未连接！");
            var sendBuffer = BuildDHRequestPacket(1, "MADJUSTREQUEST", JsonConvert.SerializeObject(quest));
            await client.SendAsync(sendBuffer);
        }

        /// <summary>
        /// T块调机下发
        /// </summary>
        /// <param name="cd"></param>
        /// <param name="weightAvg"></param>
        /// <param name="tBlockData"></param>
        /// <param name="newT"></param>
        /// <exception cref="Exception"></exception>
        protected async void SetNewTCommon(CoatingDataModel cd, float weightAvg, TBlockDataModel tBlockData, Dictionary<string, int> newT)
        {
            var positions = tBlockData.TBlockInfoLis.Select(o => o.TPosition).ToList();
            var adjustOffsets = newT.Values.ToList();

            // T块下发仅需满足：SC 上位机开关标识为 2/4    --By 2210120078  20251202
            // 曼恩斯特：待定  -TODO
            if (!(tBlockData?.TAdjustMode == 2 || tBlockData?.TAdjustMode == 4))
            {
                _logger.AdjustmentTRecommend(cd.SideType, ClosedLoopType, cd.PassCount, positions, adjustOffsets);
                var msg = $"推荐{ClosedLoopType} 业务[{cd.SideType}]面T块调节值:[{JsonConvert.SerializeObject(newT)}]";
                CCTBCore.ShowGrowWarning(msg, "调机下发推荐");
                return;
            }

            try
            {
                var bf = LoadBasicFunctionSetting();
                var task = bf.TBlockDataSource switch
                {
                    EDataSourceOfClosedLoop.DHSocketServer => SetNewTByDHSocket(cd, weightAvg, tBlockData, newT),
                    EDataSourceOfClosedLoop.DHMqttClient => SetNewTByDHMqtt(cd, weightAvg, tBlockData, newT),
                    _ => throw new Exception($"不支持的数据来源及下发方式[{bf.TBlockDataSource}]")
                };

                await task.WaitAsync(_cts?.Token ?? default);
                _logger.AdjustmentTSuccess(cd.SideType, ClosedLoopType, cd.PassCount, positions, adjustOffsets);
                var msg1 = $"{ClosedLoopType} 业务[{cd.SideType}]面T块调节值:[{JsonConvert.SerializeObject(newT)}]";
                CCTBCore.ShowGrowSuccess(msg1, "调机下发成功");
            }
            catch (Exception ex)
            {
                _logger.AdjustmentTFailed(cd.SideType, ClosedLoopType, cd.PassCount, positions, adjustOffsets);
                var msg = $"{ClosedLoopType} 业务[{cd.SideType}]面T块调节值:[{JsonConvert.SerializeObject(newT)}]下发失败:{ex.Message}";
                CCTBCore.ShowGrowError(msg, "调机下发失败");
                throw new Exception(msg);
            }
        }

        /// <summary>
        /// Mqtt方式下发T块调机值
        /// </summary>
        /// <param name="cd"></param>
        /// <param name="weightAvg"></param>
        /// <param name="tBlockData"></param>
        /// <param name="newT"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task SetNewTByDHMqtt(CoatingDataModel cd, float weightAvg, TBlockDataModel tBlockData, Dictionary<string, int> newT)
        {
            var adjustValues = new List<float>();

            foreach (var tBlockInfo in tBlockData.TBlockInfoLis)
            {
                var key = $"T块{tBlockInfo.TId}调节量";
                var tAdjustValue = tBlockInfo.TPosition;
                // 加上原本的T块值
                if (newT.ContainsKey(key)) tAdjustValue += newT[key];
                adjustValues.Add(tAdjustValue);
            }

            var client = cd.SideType switch
            {
                ESideType.A => _communicationService.DHAMqttClient,
                ESideType.B => _communicationService.DHBMqttClient,
                _ => throw new Exception($"不支持的AB面类型[{cd.SideType}]！")
            };

            if (client == null || !client.IsConnected) throw new Exception($"[{client?.ClientId}]设备未启用或未连接！");

            var clientId = client.ActiveOptions.ClientId;
            var topic = $"v2.0/tcpcan_A@manst/{clientId}@manst/0/manst/0/{clientId}/REQ/SET/0/move/all/abspos";
            var payload = string.Join(",", adjustValues.Select(p => p.ToString()));
            var re = await client.PublishAsync(topic, payload);
            if (!re.IsSuccess)
            {
                _logger.Error($"{ClosedLoopType} 业务T块下发失败:{re.Message}");
                Growl.ErrorGlobal($"{ClosedLoopType} 业务T块下发失败:{re.Message}");
            }
        }

        /// <summary>
        /// Socket方式下发T块调机值
        /// </summary>
        /// <param name="cd"></param>
        /// <param name="weightAvg"></param>
        /// <param name="tBlockData"></param>
        /// <param name="newT"></param>
        /// <returns></returns>
        private async Task SetNewTByDHSocket(CoatingDataModel cd, float weightAvg, TBlockDataModel tBlockData, Dictionary<string, int> newT)
        {
            var quest = new TAdjustRequestModel()
            {
                AdjustTime = DateTime.Now,
                AdjustTaskLis = new List<AdjustTask>(),
            };
            foreach (var tBlockInfo in tBlockData.TBlockInfoLis)
            {
                var key = $"T块{tBlockInfo.TId}调节量";
                var tAdjustValue = newT.ContainsKey(key) ? newT[key] : 0;
                var adjustTask = new AdjustTask()
                {
                    TId = tBlockInfo.TId,
                    TPosition = tBlockInfo.TPosition,
                    TAdjustValue = tAdjustValue,
                    ScanNum = (int)cd.PassCount,
                    TriggerEpkValue = weightAvg
                };
                quest.AdjustTaskLis.Add(adjustTask);
            }

            var client = cd.SideType switch
            {
                ESideType.A => _communicationService.DHASocketClient,
                ESideType.B => _communicationService.DHBSocketClient,
                _ => throw new Exception($"不支持的AB面类型[{cd.SideType}]！")
            };

            if (client == null || !client.IsConnected) throw new Exception($"[{client?.ClientId}]设备未启用或未连接！");
            var sendBuffer = BuildDHRequestPacket(1, "TADJUSTREQUEST", JsonConvert.SerializeObject(quest));
            await client.SendAsync(sendBuffer);
        }
        #endregion

        /// <summary>
        /// 保存CSV文件
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="row"></param>
        protected async void WriteToCsvAsync(string filePath, Dictionary<string, string> row)
        {
            // 数据保存
            var t = Task.Run(() =>
            {
                try
                {
                    var dt = CCTBCsvHelper.DicToTable(row);
                    lock (CCTBCsvHelper.WriteLocker) CCTBCsvHelper.WriteCSV(filePath, dt);
                }
                catch (Exception ex)
                {
                    _logger.Error($"数据保存CSV文件异常 [{filePath}]", ex);
                }
            });

            await t.WaitAsync(_cts?.Token ?? default);
        }
    }
}
