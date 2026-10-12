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
using SUNWODA_SEVB.Core.Models.Project.JROH.Core;
using SUNWODA_SEVB.Core.Models.Project.JROH.MCBusinessModels;
using SUNWODA_SEVB.Core.Models.Project.JROH.MisBusinessModels;
using SUNWODA_SEVB.Core.Models.Project.JROH.OHBusinessModels;
using SUNWODA_SEVB.Core.Models.Project.JROH.PWBusinessModels;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.Tool.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ECCTBBusinessType = SUNWODA_SEVB.Core.Enumerations.Project.CCTB.EBusinessType;
using EJRCWBusinessType = SUNWODA_SEVB.Core.Enumerations.Project.JRCW.EBusinessType;

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
        private static bool SaveProjectParametersAsJson(this IProjectSettingRepository projectSettingRepository, string paramName, object? value, string belongToVM)
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
            if (paramModel != null) return bool.TryParse(paramModel.Value, out bool result) ? result : false;

            var models = false;
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, models.ToString(), "bool", 2)
                    {
                        Remark = "涂布闭环PLC字符串颠倒参数配置"
                    }
                );
            }
            return models;
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

        #region 产品型号ProductModel参数
        /// <summary>
        /// 加载产品型号参数
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static string LoadProductModelProjectParameters(this IProjectSettingRepository projectSettingRepository)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"ProductModel";
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel != null) return paramModel.Value;

            var models = "---";
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, models, "string", 2)
                    {
                        Remark = "涂布闭环产品型号配置[ProductModel]"
                    }
                );
            }
            return models;
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
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel != null)
            {
                var result = paramModel.Value.TryDeserializeObject(out List<AlgorithmWebUrlModel>? paramValues);
                if (result && paramValues != null) return paramValues;
            }

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
            return models;
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
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel != null && !string.IsNullOrEmpty(paramModel.Value))
            {
                var result = paramModel.Value.TryDeserializeObject(out AlgorithmModel? paramValue);
                if (result && paramValue != null)
                {
                    return paramValue;
                }
            }

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
            return model;
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

        #region ClosedLoopSwitch参数
        /// <summary>
        /// 加载业务闭环开关参数，参数名称格式为"ClosedLoopSwitch{closedLoopType}{sideType}"，如"ClosedLoopSwitchMDA"
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="closedLoopType"></param>
        /// <param name="sideType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool LoadClosedLoopSwitchProjectParameters(this IProjectSettingRepository projectSettingRepository, EClosedLoopType closedLoopType, ESideType sideType)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"ClosedLoopSwitch{closedLoopType}{sideType}";
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel == null) return false;
            return bool.TryParse(paramModel.Value, out bool result) ? result : false;
        }

        /// <summary>
        /// 保存业务闭环开关参数
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="closedLoopType"></param>
        /// <param name="sideType"></param>
        /// <param name="value"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SaveClosedLoopSwitchProjectParameters(this IProjectSettingRepository projectSettingRepository, EClosedLoopType closedLoopType, ESideType sideType, bool value)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"ClosedLoopSwitch{closedLoopType}{sideType}";
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, "False", "bool", 2)
                    {
                        Remark = "涂布业务闭环开关配置"
                    }
                );
            }
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel == null) return false;
            paramModel.Value = value.ToString();
            return projectSettingRepository.Update(paramModel);
        }
        #endregion

        #region ClosedLoopSetting参数
        /// <summary>
        /// 加载涂布闭环参数设置，参数名称格式为"ClosedLoopSetting_{closedLoopType}{sideType}"，如"ClosedLoopSetting_MDA"
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="closedLoopType"></param>
        /// <param name="sideType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static ClosedLoopSettingModel LoadClosedLoopSettingProjectParameters(this IProjectSettingRepository projectSettingRepository, EClosedLoopType closedLoopType, ESideType sideType)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"ClosedLoopSetting_{closedLoopType}{sideType}";
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel != null && !string.IsNullOrEmpty(paramModel.Value))
            {
                var paramValue = JsonSerializer.Deserialize<ClosedLoopSettingModel>(paramModel?.Value ?? "", JsonOptions);
                if (paramValue != null) return paramValue;
            }

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
            return model;
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
        public static GeneralSettingsModel LoadGeneralSettingsProjectParameters(this IProjectSettingRepository projectSettingRepository)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"GeneralSettings";
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel != null && !string.IsNullOrEmpty(paramModel.Value))
            {
                var paramValue = JsonSerializer.Deserialize<GeneralSettingsModel>(paramModel?.Value ?? "", JsonOptions);
                if (paramValue != null) return paramValue;
            }

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
            return model;
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
        public static BasicFunctionSettingModel LoadBasicFunctionSettingProjectParameters(this IProjectSettingRepository projectSettingRepository)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"BasicFunctionSetting";
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel != null && !string.IsNullOrEmpty(paramModel.Value))
            {
                var paramValue = JsonSerializer.Deserialize<BasicFunctionSettingModel>(paramModel?.Value ?? "", JsonOptions);
                if (paramValue != null) return paramValue;
            }

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
            return model;
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

        #region FtpConfig参数
        /// <summary>
        /// 加载涂布闭环FTP设置，参数名称为"FtpConfig"
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static FtpConfigModel LoadFtpConfigProjectParameters(this IProjectSettingRepository projectSettingRepository)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"FtpConfig";
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel != null && !string.IsNullOrEmpty(paramModel.Value))
            {
                var paramValue = JsonSerializer.Deserialize<FtpConfigModel>(paramModel?.Value ?? "", JsonOptions);
                if (paramValue != null) return paramValue;
            }

            var model = new FtpConfigModel();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 2)
                    {
                        Remark = "涂布闭环FTP设置"
                    }
                );
            }
            return model;
        }
        #endregion

        #region SpecialFunctionSetting参数
        /// <summary>
        /// 加载涂布闭环特殊功能设置，参数名称为"SpecialFunctionSetting"
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static SpecialFunctionSettingModel LoadSpecialFunctionSettingProjectParameters(this IProjectSettingRepository projectSettingRepository)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"SpecialFunctionSetting";
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel != null && !string.IsNullOrEmpty(paramModel.Value))
            {
                var paramValue = JsonSerializer.Deserialize<SpecialFunctionSettingModel>(paramModel.Value, JsonOptions);
                if (paramValue != null) return paramValue;
            }

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
            return model;
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
            var result = new List<AlarmConfigModel>();
            var configs = LoadAlarmConfigProjectParameters(projectSettingRepository);
            foreach (var kv in AlarmDefaultConfig.Configs)
            {
                if (configs.Exists(o => o.Code == kv.Key))
                {
                    var config = configs.First(o => o.Code == kv.Key);
                    config.Name = kv.Value.Name;
                    result.Add(config);
                    continue;
                }
                else
                {
                    var config = kv.Value.CopyByJson();
                    if (config != null) result.Add(config);
                }
            }

            return result;
        }

        /// <summary>
        /// 加载涂布闭环报警信息配置，参数名称为"AlarmConfig"
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        private static List<AlarmConfigModel> LoadAlarmConfigProjectParameters(this IProjectSettingRepository projectSettingRepository)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"AlarmConfig";
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel != null && !string.IsNullOrEmpty(paramModel.Value))
            {
                var paramValue = JsonSerializer.Deserialize<List<AlarmConfigModel>>(paramModel.Value, JsonOptions);
                if (paramValue != null) return paramValue;
            }

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
            return model;
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
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel != null && !string.IsNullOrEmpty(paramModel.Value))
            {
                var paramValue = JsonSerializer.Deserialize<AlarmConfigParamModel>(paramModel.Value, JsonOptions);
                if (paramValue != null) return paramValue;
            }

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
            return model;
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
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel != null && !string.IsNullOrEmpty(paramModel.Value))
            {
                var paramValue = JsonSerializer.Deserialize<CommunicationSettingModel>(paramModel.Value, JsonOptions);
                if (paramValue != null) return paramValue;
            }

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
            return model;
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

        #region MqttAddressSetting参数
        /// <summary>
        /// 加载Mqtt地址配置，参数名称为"MqttAddressSetting"
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="sideType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static MqttAddressModel LoadMqttAddressSettingProjectParameters(this IProjectSettingRepository projectSettingRepository, ESideType sideType)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"MqttAddressSetting{sideType}";
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel != null && !string.IsNullOrEmpty(paramModel.Value))
            {
                var paramValue = JsonSerializer.Deserialize<MqttAddressModel>(paramModel.Value, JsonOptions);
                if (paramValue != null) return paramValue;
            }

            var model = new MqttAddressModel();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 2)
                    {
                        Remark = "Mqtt地址配置"
                    }
                );
            }
            return model;
        }

        /// <summary>
        /// 保存Mqtt地址配置
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="sideType"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SaveMqttAddressSettingProjectParameters(this IProjectSettingRepository projectSettingRepository, ESideType sideType, MqttAddressModel value)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"MqttAddressSetting{sideType}";
            var model = new MqttAddressModel();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 2)
                    {
                        Remark = "Mqtt地址配置"
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
            var result = new List<RoleManagementModel>();
            var configs = LoadRoleManagementConfigProjectParameters(projectSettingRepository);
            foreach (var kv in RoleManagementDefaultConfig.Configs)
            {
                if (configs.Exists(o => o.Code == kv.Key))
                {
                    var config = configs.First(o => o.Code == kv.Key);
                    config.Name = kv.Value.Name;
                    result.Add(config);
                    continue;
                }
                else
                {
                    var config = kv.Value.CopyByJson();
                    if (config != null) result.Add(config);
                }
            }

            return result;
        }

        /// <summary>
        /// 加载涂布闭环角色管理信息配置，参数名称为"RoleManagementConfig"
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        private static List<RoleManagementModel> LoadRoleManagementConfigProjectParameters(this IProjectSettingRepository projectSettingRepository)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"RoleManagementConfig";
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel != null)
            {
                var paramValues = JsonSerializer.Deserialize<List<RoleManagementModel>>(paramModel.Value, JsonOptions);
                if (paramValues != null) return paramValues;
            }

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
            return model;
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

        #region PLCGatherConfig参数
        public static List<int> LoadPLCGatherConfig(this IProjectSettingRepository projectSettingRepository)
        {
            return projectSettingRepository.LoadPLCGatherConfigProjectParameters("PLC");
        }

        public static List<int> LoadAPLCGatherConfig(this IProjectSettingRepository projectSettingRepository)
        {
            return projectSettingRepository.LoadPLCGatherConfigProjectParameters("A");
        }

        public static List<int> LoadBPLCGatherConfig(this IProjectSettingRepository projectSettingRepository)
        {
            return projectSettingRepository.LoadPLCGatherConfigProjectParameters("B");
        }

        public static bool SavePLCGatherConfig(this IProjectSettingRepository projectSettingRepository, List<int> values)
        {
            return projectSettingRepository.SavePLCGatherConfigProjectParameters(values, "PLC");
        }

        public static bool SaveAPLCGatherConfig(this IProjectSettingRepository projectSettingRepository, List<int> values)
        {
            return projectSettingRepository.SavePLCGatherConfigProjectParameters(values, "A");
        }

        public static bool SaveBPLCGatherConfig(this IProjectSettingRepository projectSettingRepository, List<int> values)
        {
            return projectSettingRepository.SavePLCGatherConfigProjectParameters(values, "B");
        }

        private static List<int> LoadPLCGatherConfigProjectParameters(this IProjectSettingRepository projectSettingRepository, string flag)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"PLCGatherConfig_{flag}";
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel != null)
            {
                var paramValues = JsonSerializer.Deserialize<List<int>>(paramModel.Value, JsonOptions);
                if (paramValues != null) return paramValues;
            }

            var model = new List<int>();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 4)
                    {
                        Remark = $"涂布闭环PLC地址采集配置[{flag}]"
                    }
                );
            }
            return model;
        }

        private static bool SavePLCGatherConfigProjectParameters(this IProjectSettingRepository projectSettingRepository, List<int> values, string flag)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"PLCGatherConfig_{flag}";
            var model = new List<int>();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 4)
                    {
                        Remark = $"涂布闭环PLC地址采集配置[{flag}]"
                    }
                );
            }
            return projectSettingRepository.SaveProjectParametersAsJson(paramName, values, CCTBCore.BelongToVM);
        }
        #endregion

        #region FtpConfig参数
        /// <summary>
        /// 加载涂布闭环FTP配置，参数名称为"FtpConfig"
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static FtpConfigModel LoadFtpConfigProjectParameter(this IProjectSettingRepository projectSettingRepository)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = "FtpConfig";
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel != null)
            {
                var paramValues = JsonSerializer.Deserialize<FtpConfigModel>(paramModel.Value, JsonOptions);
                if (paramValues != null) return paramValues;
            }

            var model = new FtpConfigModel();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 4)
                    {
                        Remark = "FTP配置参数"
                    }
                );
            }
            return model;
        }

        /// <summary>
        /// 保存涂布闭环FTP配置
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SaveFtpConfigProjectParameter(this IProjectSettingRepository projectSettingRepository, FtpConfigModel value)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = "FtpConfig";
            var model = new FtpConfigModel();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 4)
                    {
                        Remark = "FTP配置参数"
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
        public static EJRCWBusinessType LoadBusinessTypeProjectParameter(this IProjectSettingRepository projectSettingRepository)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"BusinessType";
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, JRCWCore.BelongToVM);
            if (paramModel != null && Enum.TryParse(paramModel.Value, out EJRCWBusinessType result)) return result;

            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == JRCWCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, JRCWCore.BelongToVM, EJRCWBusinessType.BusinessType1.ToString(), "string", 2)
                    {
                        Remark = "卷绕极耳错位闭环业务类型配置"
                    }
                );
            }
            return default;
        }

        /// <summary>
        /// 保存极耳错位闭环业务类型配置
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SaveBusinessTypeProjectParameter(this IProjectSettingRepository projectSettingRepository, EJRCWBusinessType value)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"BusinessType";
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == JRCWCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, JRCWCore.BelongToVM, value.ToString(), "string", 2)
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
            var paramName = $"FileSaveRootPath";
            var paramValue = projectSettingRepository.GetSettingValue(paramName, JRCWCore.BelongToVM);
            if (paramValue != null) return paramValue.ToString();

            var defauleValue = "D:\\通用上位机\\JRCWDatas";
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == JRCWCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, JRCWCore.BelongToVM, "D:\\通用上位机\\JRCWDatas", "string", 2)
                    {
                        Remark = "卷绕极耳错位闭环文件根目录配置"
                    }
                );
            }
            return defauleValue;
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

        #region PyCodes
        /// <summary>
        /// 加载极耳错位闭环PyCodes参数配置，参数名称为"PyCodes"
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static List<string> LoadPyCodesProjectParamter(this IProjectSettingRepository projectSettingRepository)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"PyCodes";
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, JRCWCore.BelongToVM);
            if (paramModel != null && !string.IsNullOrEmpty(paramModel.Value))
            {
                var pyCodes = paramModel.Value.Split(',').ToList();
                return pyCodes;
            }

            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == JRCWCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, JRCWCore.BelongToVM, "MIS_JUDGE_C04_325,PRESS_C04_325", "string", 2)
                    {
                        Remark = "极耳错位闭环算法pyCode配置(英文逗号分隔)"
                    }
                );
            }
            return new();
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
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, JRCWCore.BelongToVM);
            if (paramModel != null && !string.IsNullOrEmpty(paramModel.Value))
            {
                var paramValue = JsonSerializer.Deserialize<Business1ParamModel>(paramModel.Value, JsonOptions);
                if (paramValue != null) return paramValue;
            }

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
            return model;
        }

        /// <summary>
        /// 保存耳错位闭环参数配置
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SaveBusiness1ParamProjectParameters(this IProjectSettingRepository projectSettingRepository, Business1ParamModel? value)
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

        #region 业务类型2-Business2Param
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
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, JRCWCore.BelongToVM);
            if (paramModel != null && !string.IsNullOrEmpty(paramModel.Value))
            {
                var paramValue = JsonSerializer.Deserialize<Business2ParamModel>(paramModel.Value, JsonOptions);
                if (paramValue != null) return paramValue;
            }

            var model = new Business2ParamModel();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == JRCWCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, JRCWCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 4)
                    {
                        Remark = "极耳错位闭环参数配置-业务2"
                    }
                );
            }
            return model;
        }

        /// <summary>
        /// 保存耳错位闭环参数配置
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SaveBusiness2ParamProjectParameters(this IProjectSettingRepository projectSettingRepository, Business2ParamModel? value)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"Business2Param";
            var model = new Business2ParamModel();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == JRCWCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, JRCWCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 4)
                    {
                        Remark = "极耳错位闭环参数配置-业务2"
                    }
                );
            }
            return projectSettingRepository.SaveProjectParametersAsJson(paramName, value, JRCWCore.BelongToVM);
        }
        #endregion
        #endregion
        #endregion

        #region JROH项目(卷绕oh闭环)参数配置相关扩展方法
        #region FileSaveRootPath参数
        /// <summary>
        /// 加载卷绕OH闭环文件根目录配置信息，参数名称为"FileSaveRootPath"
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static string LoadJROHFileSaveRootPathProjectParameter(this IProjectSettingRepository projectSettingRepository)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"FileSaveRootPath";
            var paramValue = projectSettingRepository.GetSettingValue(paramName, JROHCore.BelongToVM);
            if (paramValue != null) return paramValue.ToString();

            var defauleValue = "D:\\通用上位机\\JROHDatas";
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == JROHCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, JROHCore.BelongToVM, "D:\\通用上位机\\JROHDatas", "string", 2)
                    {
                        Remark = "卷绕OH闭环文件根目录配置"
                    }
                );
            }
            return defauleValue;
        }

        /// <summary>
        /// 保存卷绕OH闭环文件根目录配置
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SaveJROHFileSaveRootPathProjectParameter(this IProjectSettingRepository projectSettingRepository, string value)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"FileSaveRootPath";
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == JROHCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, JROHCore.BelongToVM, value, "string", 2)
                    {
                        Remark = "卷绕OH闭环文件根目录配置"
                    }
                );
            }
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, JROHCore.BelongToVM);
            if (paramModel == null) return false;
            paramModel.Value = value;
            return projectSettingRepository.Update(paramModel);
        }
        #endregion

        #region 业务类型PoleWidth-Business1Param
        /// <summary>
        /// 加载卷绕OH闭环参数配置，参数名称为"PWBusinessTypeParam"
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static PWBusinessParamModel LoadPWBusinessParamProjectParameters(this IProjectSettingRepository projectSettingRepository)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"PWBusinessParam";
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, JROHCore.BelongToVM);
            if (paramModel != null && !string.IsNullOrEmpty(paramModel.Value))
            {
                var paramValue = JsonSerializer.Deserialize<PWBusinessParamModel>(paramModel.Value, JsonOptions);
                if (paramValue != null) return paramValue;
            }

            var model = new PWBusinessParamModel();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == JROHCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, JROHCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 4)
                    {
                        Remark = "卷绕OH闭环参数配置-业务PoelWidth"
                    }
                );
            }
            return model;
        }

        /// <summary>
        /// 保存卷绕OH闭环参数配置
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SavePWBusinessParamProjectParameters(this IProjectSettingRepository projectSettingRepository, PWBusinessParamModel? value)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"PWBusinessParam";
            var model = new PWBusinessParamModel();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == JROHCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, JROHCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 4)
                    {
                        Remark = "卷绕OH闭环参数配置-业务PoelWidth"
                    }
                );
            }
            return projectSettingRepository.SaveProjectParametersAsJson(paramName, value, JROHCore.BelongToVM);
        }
        #endregion

        #region 业务类型OH-Business2Param
        /// <summary>
        /// 加载卷绕OH闭环参数配置，参数名称为"OHBusinessTypeParam"
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static OHBusinessParamModel LoadOHBusinessParamProjectParameters(this IProjectSettingRepository projectSettingRepository)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"OHBusinessParam";
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, JROHCore.BelongToVM);
            if (paramModel != null && !string.IsNullOrEmpty(paramModel.Value))
            {
                var paramValue = JsonSerializer.Deserialize<OHBusinessParamModel>(paramModel.Value, JsonOptions);
                if (paramValue != null) return paramValue;
            }

            var model = new OHBusinessParamModel();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == JROHCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, JROHCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 4)
                    {
                        Remark = "卷绕OH闭环参数配置-业务OH"
                    }
                );
            }
            return model;
        }

        /// <summary>
        /// 保存卷绕OH闭环参数配置
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SaveOHBusinessParamProjectParameters(this IProjectSettingRepository projectSettingRepository, OHBusinessParamModel? value)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"OHBusinessParam";
            var model = new OHBusinessParamModel();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == JROHCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, JROHCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 4)
                    {
                        Remark = "卷绕OH闭环参数配置-业务OH"
                    }
                );
            }
            return projectSettingRepository.SaveProjectParametersAsJson(paramName, value, JROHCore.BelongToVM);
        }
        #endregion

        #region 业务类型MC-Business3Param
        /// <summary>
        /// 加载卷绕OH闭环参数配置，参数名称为"OHBusinessTypeParam"
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static MCBusinessParamModel LoadMCBusinessParamProjectParameters(this IProjectSettingRepository projectSettingRepository)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"MCBusinessParam";
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, JROHCore.BelongToVM);
            if (paramModel != null && !string.IsNullOrEmpty(paramModel.Value))
            {
                var paramValue = JsonSerializer.Deserialize<MCBusinessParamModel>(paramModel.Value, JsonOptions);
                if (paramValue != null) return paramValue;
            }

            var model = new MCBusinessParamModel();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == JROHCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, JROHCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 4)
                    {
                        Remark = "卷绕OH闭环参数配置-业务MC"
                    }
                );
            }
            return model;
        }

        /// <summary>
        /// 保存卷绕OH闭环参数配置
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SaveMCBusinessParamProjectParameters(this IProjectSettingRepository projectSettingRepository, MCBusinessParamModel? value)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"MCBusinessParam";
            var model = new MCBusinessParamModel();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == JROHCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, JROHCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 4)
                    {
                        Remark = "卷绕OH闭环参数配置-业务MC"
                    }
                );
            }
            return projectSettingRepository.SaveProjectParametersAsJson(paramName, value, JROHCore.BelongToVM);
        }
        #endregion

        #region 业务类型Mismatch-Business4Param
        /// <summary>
        /// 加载极耳错位闭环参数配置，参数名称为"MisBusinessParam"
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static MisBusinessParamModel LoadMisBusinessParamProjectParameters(this IProjectSettingRepository projectSettingRepository)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"MisBusinessParam";
            var paramModel = projectSettingRepository.GetByNameAndVM(paramName, JROHCore.BelongToVM);
            if (paramModel != null && !string.IsNullOrEmpty(paramModel.Value))
            {
                var paramValue = JsonSerializer.Deserialize<MisBusinessParamModel>(paramModel.Value, JsonOptions);
                if (paramValue != null) return paramValue;
            }

            var model = new MisBusinessParamModel();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == JROHCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, JROHCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 4)
                    {
                        Remark = "卷绕OH闭环参数配置-业务Mismatch(极耳错位)"
                    }
                );
            }
            return model;
        }

        /// <summary>
        /// 保存极耳错位闭环参数配置
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SaveMisBusinessParamProjectParameters(this IProjectSettingRepository projectSettingRepository, MisBusinessParamModel? value)
        {
            if (projectSettingRepository == null) throw new ArgumentNullException(nameof(projectSettingRepository));
            var paramName = $"MisBusinessParam";
            var model = new MisBusinessParamModel();
            if (!projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == JROHCore.BelongToVM))
            {
                projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, JROHCore.BelongToVM, JsonSerializer.Serialize(model, JsonOptions), "string", 4)
                    {
                        Remark = "卷绕OH闭环参数配置-业务Mismatch(极耳错位)"
                    }
                );
            }
            return projectSettingRepository.SaveProjectParametersAsJson(paramName, value, JROHCore.BelongToVM);
        }
        #endregion
        #endregion
    }
}
