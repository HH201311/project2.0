using Newtonsoft.Json.Linq;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB.FormEnums;
using SUNWODA_SEVB.Core.Enumerations.Project.JRCW;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CCTB.AlarmModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.RoleModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
using SUNWODA_SEVB.Core.Models.Project.JRCW.Business1Models;
using SUNWODA_SEVB.Core.Models.Project.JRCW.Business2Models;
using SUNWODA_SEVB.Core.Models.Project.JRCW.Core;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.Tool.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Tool.Extension
{
    /// <summary>
    /// 
    /// </summary>
    public static class ProjectSettingExtension
    {
        /// <summary>
        /// 
        /// </summary>
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        #region 通用Json方式保存参数
        /// <summary>
        /// 保存为Json字符串的参数，适用于复杂对象的保存，调用时需要保证value对象是可序列化的
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="paramName"></param>
        /// <param name="value"></param>
        /// <param name="belongToVM"></param>
        /// <returns></returns>
        private static bool SaveProjectParametersAsJson(this IProjectSettingRepository projectSettingRepository, string paramName, object value, string belongToVM)
        {
            var paramrer = projectSettingRepository.GetByNameAndVM(paramName, belongToVM);
            if (paramrer == null) return false;
            paramrer.Value = JsonSerializer.Serialize(value, JsonOptions);
            return projectSettingRepository.Update(paramrer);
        }
        #endregion

        #region CCTB项目参数配置相关扩展方法
        #region IsStringReverseByteWord参数
        /// <summary>
        /// 加载PLC字符串颠倒（欧姆龙PLC需配置IsStringReverseByteWord）
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool LoadIsStringReverseByteWordProjectParameters(this IProjectSettingRepository projectSettingRepository)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"IsStringReverseByteWord";
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel == null) return false;
            return bool.TryParse(paramModel.Value, out bool result) ? result : false;
        }

        /// <summary>
        /// 保存PLC字符串颠倒配置
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="value"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SaveIsStringReverseByteWordProjectParameters(this IProjectSettingRepository projectSettingRepository, bool value)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"IsStringReverseByteWord";
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, "False", "bool", 2)
                    {
                        Remark = "涂布闭环字符串参数是否反转字节序配置"
                    }
                );
            }
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel == null) return false;
            paramModel.Value = value.ToString();
            return projectSettingRepository.Update(paramModel);
        }
        #endregion

        #region TBDataParamOptions参数
        /// <summary>
        /// 加载面密度界面显示的计算参数
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="sideType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static List<EParamOption> LoadParamOptionsProjectParameters(this IProjectSettingRepository projectSettingRepository, ESideType sideType)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"TBDataParamOptions{sideType}";
            var paramValues = new List<EParamOption>();
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel == null) return paramValues;
            paramModel?.Value?.Split(',')?.ToList().ForEach(o =>
            {
                if (Enum.TryParse(o, out EParamOption paramOption))
                    paramValues.Add(paramOption);
            });

            return paramValues;
        }

        /// <summary>
        /// 保存面密度界面显示的计算参数
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="sideType"></param>
        /// <param name="paramOptions"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void SaveParamOptionsProjectParameters(this IProjectSettingRepository projectSettingRepository, ESideType sideType, List<EParamOption> paramOptions)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));

            var paramName = $"TBDataParamOptions{sideType}";
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, "", DataTypeConverter.TypeToString(typeof(EParamOption[])), 2)
                    {
                        Remark = "涂布闭环面密度实时参数显示配置"
                    }
                );
            }
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel == null) return;
            paramModel.Value = string.Join(',', paramOptions);
            projectSettingRepository.Update(paramModel);
        }
        #endregion

        #region AlgorithmWebUrls参数
        /// <summary>
        /// 加载算法接口地址配置，参数名称为"AlgorithmWebUrls"，参数值为Json字符串，反序列化为List<AlgorithmWebUrlModel>
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static List<AlgorithmWebUrlModel> LoadAlgorithmWebUrlsProjectParameters(this IProjectSettingRepository projectSettingRepository)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"AlgorithmWebUrls";
            var models = new List<AlgorithmWebUrlModel>();
            models.Add(new AlgorithmWebUrlModel() { IsEnable = true, Name = "惠州WEB服务器", BaseUrl = "http://10.201.88.151:10100/" });
            models.Add(new AlgorithmWebUrlModel() { IsEnable = true, Name = "南昌WEB服务器", BaseUrl = "http://10.117.243.32:7097/" });
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonSerializer.Serialize(models, JsonOptions), "string", 2)
                    {
                        Remark = "涂布闭环算法接口地址配置[WebUrl]"
                    }
                );
            }
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel == null) return models;
            var result = paramModel.Value.TryDeserializeObject(out List<AlgorithmWebUrlModel>? paramValues);
            if (!result) return models;
            return paramValues ?? models;
        }

        /// <summary>
        /// 保存算法接口地址配置，参数名称为"AlgorithmWebUrls"，参数值为Json字符串，序列化List<AlgorithmWebUrlModel>对象
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SaveAlgorithmWebUrlsProjectParameters(this IProjectSettingRepository projectSettingRepository, List<AlgorithmWebUrlModel> value)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"AlgorithmWebUrls";
            var models = new List<AlgorithmWebUrlModel>();
            models.Add(new AlgorithmWebUrlModel() { IsEnable = true, Name = "惠州WEB服务器", BaseUrl = "http://10.201.88.151:10100/" });
            models.Add(new AlgorithmWebUrlModel() { IsEnable = true, Name = "南昌WEB服务器", BaseUrl = "http://10.117.243.32:7097/" });
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonSerializer.Serialize(models, JsonOptions), "string", 2)
                    {
                        Remark = "涂布闭环算法接口地址配置[WebUrl]"
                    }
                );
            }
            return projectSettingRepository.SaveProjectParametersAsJson(paramName, value, CCTBCore.BelongToVM);
        }
        #endregion

        #region AlgorithmModel参数
        /// <summary>
        /// 加载算法相关参数，参数名称格式为"AlgorithmSetting_{closedLoopType}{sideType}"，如"AlgorithmSetting_MDA"
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="closedLoopType"></param>
        /// <param name="sideType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static AlgorithmModel LoadAlgorithmProjectParameters(this IProjectSettingRepository projectSettingRepository, EClosedLoopType closedLoopType, ESideType sideType)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"AlgorithmSetting_{closedLoopType}{sideType}";
            var model = new AlgorithmModel();
            model.IsEnable = false;
            model.AlgorithmName = $"DY_{closedLoopType}_L1_1_N_{(sideType == ESideType.A ? "S" : "D")}_314Ah";
            model.AlgorithmType = AlgorithmModel.GetEAlgorithmType(closedLoopType, model.AlgorithmName);
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 2)
                    {
                        Remark = "涂布闭环算法设置"
                    }
                );
            }
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel == null) return model;
            var result = paramModel.Value.TryDeserializeObject(out AlgorithmModel? paramValue);
            if (result == false) return model;
            if (paramValue == null) return model;
            paramValue.AlgorithmType = AlgorithmModel.GetEAlgorithmType(closedLoopType, paramValue.AlgorithmName);
            return paramValue;
        }

        /// <summary>
        /// 保存算法相关参数
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="closedLoopType"></param>
        /// <param name="sideType"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SaveAlgorithmProjectParameters(this IProjectSettingRepository projectSettingRepository, EClosedLoopType closedLoopType, ESideType sideType, AlgorithmModel value)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"AlgorithmSetting_{closedLoopType}{sideType}";
            var model = new AlgorithmModel();
            model.IsEnable = false;
            model.AlgorithmName = $"DY_{closedLoopType}_L1_1_N_{(sideType == ESideType.A ? "S" : "D")}_314Ah";
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 2)
                    {
                        Remark = "涂布闭环算法设置"
                    }
                );
            }
            return projectSettingRepository.SaveProjectParametersAsJson(paramName, value, CCTBCore.BelongToVM);
        }
        #endregion

        #region ClosedLoopSetting参数
        /// <summary>
        /// 加载涂布闭环参数设置，参数名称格式为"ClosedLoopSetting_{closedLoopType}{sideType}"，如"ClosedLoopSetting_MD_A"
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="closedLoopType"></param>
        /// <param name="sideType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static ClosedLoopSettingModel? LoadClosedLoopSettingProjectParameters(this IProjectSettingRepository projectSettingRepository, EClosedLoopType closedLoopType, ESideType sideType)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"ClosedLoopSetting_{closedLoopType}{sideType}";
            var model = new ClosedLoopSettingModel();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 2)
                    {
                        Remark = "涂布闭环参数设置"
                    }
                );
            }
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel == null) return null;
            var paramValue = JsonSerializer.Deserialize<ClosedLoopSettingModel>(paramModel?.Value ?? "", JsonOptions);
            return paramValue;
        }

        /// <summary>
        /// 保存涂布闭环参数设置
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="closedLoopType"></param>
        /// <param name="sideType"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SaveClosedLoopSettingProjectParameters(this IProjectSettingRepository projectSettingRepository, EClosedLoopType closedLoopType, ESideType sideType, ClosedLoopSettingModel value)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"ClosedLoopSetting_{closedLoopType}{sideType}";
            var model = new ClosedLoopSettingModel();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 2)
                    {
                        Remark = "涂布闭环参数设置"
                    }
                );
            }
            return projectSettingRepository.SaveProjectParametersAsJson(paramName, value, CCTBCore.BelongToVM);
        }
        #endregion

        #region GeneralSettings参数
        /// <summary>
        /// 加载涂布闭环通用参数设置，参数名称为"GeneralSettings"
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static GeneralSettingsModel? LoadGeneralSettingsProjectParameters(this IProjectSettingRepository projectSettingRepository)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"GeneralSettings";
            var model = new GeneralSettingsModel();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 2)
                    {
                        Remark = "涂布闭环通用设置"
                    }
                );
            }
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel == null) return null;
            var paramValue = JsonSerializer.Deserialize<GeneralSettingsModel>(paramModel?.Value ?? "", JsonOptions);
            return paramValue;
        }

        /// <summary>
        /// 保存涂布闭环通用参数设置
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SaveGeneralSettingsProjectParameters(this IProjectSettingRepository projectSettingRepository, GeneralSettingsModel value)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"GeneralSettings";
            var model = new GeneralSettingsModel();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 2)
                    {
                        Remark = "涂布闭环通用设置"
                    }
                );
            }
            return projectSettingRepository.SaveProjectParametersAsJson(paramName, value, CCTBCore.BelongToVM);
        }
        #endregion

        #region BasicFunctionSetting参数
        /// <summary>
        /// 加载涂布闭环基础功能设置，参数名称为"BasicFunctionSetting"
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static BasicFunctionSettingModel? LoadBasicFunctionSettingProjectParameters(this IProjectSettingRepository projectSettingRepository)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"BasicFunctionSetting";
            var model = new BasicFunctionSettingModel();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 2)
                    {
                        Remark = "涂布闭环基础功能设置"
                    }
                );
            }
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel == null) return null;
            var paramValue = JsonSerializer.Deserialize<BasicFunctionSettingModel>(paramModel?.Value ?? "", JsonOptions);
            return paramValue;
        }

        /// <summary>
        /// 保存涂布闭环基础功能设置
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SaveBasicFunctionSettingProjectParameters(this IProjectSettingRepository projectSettingRepository, BasicFunctionSettingModel value)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"BasicFunctionSetting";
            var model = new BasicFunctionSettingModel();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 2)
                    {
                        Remark = "涂布闭环基础功能设置"
                    }
                );
            }
            return projectSettingRepository.SaveProjectParametersAsJson(paramName, value, CCTBCore.BelongToVM);
        }
        #endregion

        #region SpecialFunctionSetting参数
        /// <summary>
        /// 加载涂布闭环特殊功能设置，参数名称为"SpecialFunctionSetting"
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static SpecialFunctionSettingModel? LoadSpecialFunctionSettingProjectParameters(this IProjectSettingRepository projectSettingRepository)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"SpecialFunctionSetting";
            var model = new SpecialFunctionSettingModel();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 2)
                    {
                        Remark = "涂布闭环特殊功能设置"
                    }
                );
            }
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel == null) return null;
            var paramValue = JsonSerializer.Deserialize<SpecialFunctionSettingModel>(paramModel?.Value ?? "", JsonOptions);
            return paramValue;
        }

        /// <summary>
        /// 保存涂布闭环特殊功能设置
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SaveSpecialFunctionSettingProjectParameters(this IProjectSettingRepository projectSettingRepository, SpecialFunctionSettingModel value)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"SpecialFunctionSetting";
            var model = new SpecialFunctionSettingModel();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 2)
                    {
                        Remark = "涂布闭环特殊功能设置"
                    }
                );
            }
            return projectSettingRepository.SaveProjectParametersAsJson(paramName, value, CCTBCore.BelongToVM);
        }
        #endregion

        #region AlarmConfigs参数
        /// <summary>
        /// 加载涂布闭环报警信息配置，参数名称为"AlarmConfig"，如果没有配置则加载默认配置
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <returns></returns>
        public static List<AlarmConfigModel> LoadAlarmConfigWithDefault(this IProjectSettingRepository projectSettingRepository)
        {
            var configs = projectSettingRepository.LoadAlarmConfigProjectParameters();
            foreach (var config in AlarmDefaultConfig.Configs)
            {
                if (configs.Exists(o => o.Code == config.Key)) continue;
                var newConfig = config.Value.CopyByJson();
                if (newConfig == null) continue;
                configs.Add(newConfig);
            }

            return configs;
        }

        /// <summary>
        /// 加载涂布闭环报警信息配置，参数名称为"AlarmConfig"
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static List<AlarmConfigModel> LoadAlarmConfigProjectParameters(this IProjectSettingRepository projectSettingRepository)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"AlarmConfig";
            var model = new List<AlarmConfigModel>();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 2)
                    {
                        Remark = "涂布闭环报警信息配置"
                    }
                );
            }
            var paramValues = new List<AlarmConfigModel>();
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel == null) return paramValues;
            paramValues = JsonSerializer.Deserialize<List<AlarmConfigModel>>(paramModel?.Value ?? "", JsonOptions);
            return paramValues ?? new List<AlarmConfigModel>();
        }

        /// <summary>
        /// 保存涂布闭环报警信息配置
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SaveAlarmConfigProjectParameters(this IProjectSettingRepository projectSettingRepository, List<AlarmConfigModel> value)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"AlarmConfig";
            var model = new List<AlarmConfigModel>();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 2)
                    {
                        Remark = "涂布闭环报警信息配置"
                    }
                );
            }
            return projectSettingRepository.SaveProjectParametersAsJson(paramName, value, CCTBCore.BelongToVM);
        }
        #endregion

        #region AlarmParamConfig参数
        /// <summary>
        /// 加载涂布闭环报警信息配置，参数名称为"AlarmConfigParam"
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static AlarmConfigParamModel? LoadAlarmConfigParamProjectParameters(this IProjectSettingRepository projectSettingRepository)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"AlarmConfigParam";
            var model = new AlarmConfigParamModel();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 2)
                    {
                        Remark = "涂布闭环报警信息配置"
                    }
                );
            }
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel == null) return null;
            var paramValue = JsonSerializer.Deserialize<AlarmConfigParamModel>(paramModel?.Value ?? "", JsonOptions);
            return paramValue;
        }

        /// <summary>
        /// 保存涂布闭环报警信息配置
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SaveAlarmConfigParamProjectParameters(this IProjectSettingRepository projectSettingRepository, AlarmConfigParamModel value)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"AlarmConfigParam";
            var model = new AlarmConfigParamModel();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 2)
                    {
                        Remark = "涂布闭环报警信息配置"
                    }
                );
            }
            return projectSettingRepository.SaveProjectParametersAsJson(paramName, value, CCTBCore.BelongToVM);
        }
        #endregion

        #region CommunicationSetting参数
        /// <summary>
        /// 加载涂布闭环通讯配置，参数名称为"CommunicationSetting"
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static CommunicationSettingModel LoadCommunicationSettingProjectParameters(this IProjectSettingRepository projectSettingRepository)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"CommunicationSetting";
            var model = new CommunicationSettingModel();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 2)
                    {
                        Remark = "涂布闭环通讯配置"
                    }
                );
            }
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel == null || string.IsNullOrEmpty(paramModel.Value)) return model;
            var paramValue = JsonSerializer.Deserialize<CommunicationSettingModel>(paramModel.Value, JsonOptions);
            return paramValue ?? model;
        }

        /// <summary>
        /// 保存涂布闭环通讯配置
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SaveCommunicationSettingProjectParameters(this IProjectSettingRepository projectSettingRepository, CommunicationSettingModel value)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"CommunicationSetting";
            var model = new CommunicationSettingModel();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 2)
                    {
                        Remark = "涂布闭环通讯配置"
                    }
                );
            }
            return projectSettingRepository.SaveProjectParametersAsJson(paramName, value, CCTBCore.BelongToVM);
        }
        #endregion

        #region RoleManagementConfig参数
        /// <summary>
        /// 加载涂布闭环角色管理信息配置，参数名称为"RoleManagementConfig"，如果没有配置则加载默认配置
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <returns></returns>
        public static List<RoleManagementModel> LoadRoleManagementConfigWithDefault(this IProjectSettingRepository projectSettingRepository)
        {
            var configs = LoadRoleManagementConfigProjectParameters(projectSettingRepository);
            foreach (var config in RoleManagementDefaultConfig.Configs)
            {
                if (configs.Exists(o => o.Code == config.Key)) continue;
                var newConfig = config.Value.CopyByJson();
                if (newConfig == null) continue;
                configs.Add(newConfig);
            }

            return configs;
        }

        /// <summary>
        /// 加载涂布闭环角色管理信息配置，参数名称为"RoleManagementConfig"
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static List<RoleManagementModel> LoadRoleManagementConfigProjectParameters(this IProjectSettingRepository projectSettingRepository)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"RoleManagementConfig";
            var model = new List<RoleManagementModel>();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 4)
                    {
                        Remark = "涂布闭环角色管理信息配置"
                    }
                );
            }
            var paramValues = new List<RoleManagementModel>();
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel == null) return paramValues;
            paramValues = JsonSerializer.Deserialize<List<RoleManagementModel>>(paramModel?.Value ?? "", JsonOptions);
            return paramValues ?? new List<RoleManagementModel>();
        }

        /// <summary>
        /// 保存涂布闭环角色管理信息配置
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SaveRoleManagementConfigProjectParameters(this IProjectSettingRepository projectSettingRepository, List<RoleManagementModel> value)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"RoleManagementConfig";
            var model = new List<RoleManagementModel>();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 4)
                    {
                        Remark = "涂布闭环角色管理信息配置"
                    }
                );
            }
            return projectSettingRepository.SaveProjectParametersAsJson(paramName, value, CCTBCore.BelongToVM);
        }
        #endregion
        #endregion

        #region JRCW项目(卷绕极耳错位闭环)参数配置相关扩展方法
        #region BusinessType参数
        /// <summary>
        /// 加载极耳错位闭环业务类型配置信息，参数名称为"BusinessType"
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static EBusinessType LoadBusinessTypeProjectParameter(this IProjectSettingRepository projectSettingRepository)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"BusinessType";
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == JRCWCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, JRCWCore.BelongToVM, EBusinessType.BusinessType1.ToString(), "string", 4)
                    {
                        Remark = "卷绕极耳错位闭环业务类型配置"
                    }
                );
            }
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, JRCWCore.BelongToVM);
            if (paramModel == null) return default;
            return Enum.TryParse(paramModel.Value, out EBusinessType result) ? result : default;
        }

        /// <summary>
        /// 保存极耳错位闭环业务类型配置
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SaveBusinessTypeProjectParameter(this IProjectSettingRepository projectSettingRepository, EBusinessType value)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"BusinessType";
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == JRCWCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, JRCWCore.BelongToVM, value.ToString(), "string", 4)
                    {
                        Remark = "卷绕极耳错位闭环业务类型配置"
                    }
                );
            }
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, JRCWCore.BelongToVM);
            if (paramModel == null) return false;
            paramModel.Value = value.ToString();
            return projectSettingRepository.Update(paramModel);
        }
        #endregion

        #region FileSaveRootPath参数
        /// <summary>
        /// 加载极耳错位闭环文件根目录配置信息，参数名称为"FileSaveRootPath"
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static string LoadFileSaveRootPathProjectParameter(this IProjectSettingRepository projectSettingRepository)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var defauleValue = "D:\\通用上位机\\JRCWDatas";
            var paramName = $"FileSaveRootPath";
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == JRCWCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, JRCWCore.BelongToVM, "D:\\通用上位机\\JRCWDatas", "string", 2)
                    {
                        Remark = "卷绕极耳错位闭环文件根目录配置"
                    }
                );
            }
            var paramValue = projectSettingRepository.GetSettingValue(paramName, JRCWCore.BelongToVM);
            if (paramValue == null) return defauleValue;
            return paramValue.ToString();
        }

        /// <summary>
        /// 保存极耳错位闭环文件根目录配置
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SaveFileSaveRootPathProjectParameter(this IProjectSettingRepository projectSettingRepository, string value)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"FileSaveRootPath";
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == JRCWCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, JRCWCore.BelongToVM, value, "string", 2)
                    {
                        Remark = "卷绕极耳错位闭环文件根目录配置"
                    }
                );
            }
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, JRCWCore.BelongToVM);
            if (paramModel == null) return false;
            paramModel.Value = value;
            return projectSettingRepository.Update(paramModel);
        }
        #endregion

        #region BusinessType1Param参数
        #region 业务类型1-Business1Param
        /// <summary>
        /// 加载极耳错位闭环参数配置，参数名称为"BusinessType1Param"
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static Business1ParamModel LoadBusiness1ParamProjectParameters(this IProjectSettingRepository projectSettingRepository)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"Business1Param";
            var model = new Business1ParamModel();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == JRCWCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, JRCWCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 4)
                    {
                        Remark = "极耳错位闭环参数配置-业务1"
                    }
                );
            }
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, JRCWCore.BelongToVM);
            if (paramModel == null || string.IsNullOrEmpty(paramModel.Value)) return model;
            var paramValue = JsonSerializer.Deserialize<Business1ParamModel>(paramModel.Value, JsonOptions);
            return paramValue ?? model;
        }

        /// <summary>
        /// 保存耳错位闭环参数配置
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SaveBusiness1ParamProjectParameters(this IProjectSettingRepository projectSettingRepository, Business1ParamModel value)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"Business1Param";
            var model = new Business1ParamModel();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == JRCWCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, JRCWCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 4)
                    {
                        Remark = "极耳错位闭环参数配置-业务1"
                    }
                );
            }
            return projectSettingRepository.SaveProjectParametersAsJson(paramName, value, JRCWCore.BelongToVM);
        }
        #endregion

        #region 业务类型2-Business1Param
        /// <summary>
        /// 加载极耳错位闭环参数配置，参数名称为"BusinessType2Param"
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static Business2ParamModel LoadBusiness2ParamProjectParameters(this IProjectSettingRepository projectSettingRepository)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"Business2Param";
            var model = new Business2ParamModel();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == JRCWCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, JRCWCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 4)
                    {
                        Remark = "极耳错位闭环参数配置-业务1"
                    }
                );
            }
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, JRCWCore.BelongToVM);
            if (paramModel == null || string.IsNullOrEmpty(paramModel.Value)) return model;
            var paramValue = JsonSerializer.Deserialize<Business2ParamModel>(paramModel.Value, JsonOptions);
            return paramValue ?? model;
        }

        /// <summary>
        /// 保存耳错位闭环参数配置
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SaveBusiness2ParamProjectParameters(this IProjectSettingRepository projectSettingRepository, Business2ParamModel value)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"Business1Param";
            var model = new Business1ParamModel();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == JRCWCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, JRCWCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 4)
                    {
                        Remark = "极耳错位闭环参数配置-业务1"
                    }
                );
            }
            return projectSettingRepository.SaveProjectParametersAsJson(paramName, value, JRCWCore.BelongToVM);
        }
        #endregion
        #endregion
        #endregion
    }
}
