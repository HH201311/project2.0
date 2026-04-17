using Newtonsoft.Json;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB.FormEnums;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CCTB.AlarmModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Tool.Helper.CCTBHelper
{
    /// <summary>
    /// 常用操作工具类(CCTB专用)，包含一些常用的操作方法，如数据处理、计算等
    /// </summary>
    public class OperationUtil
    {
        private readonly IProjectSettingRepository _projectSettingRepository;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="projectSettingRepository"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public OperationUtil(IProjectSettingRepository projectSettingRepository)
        {
            _projectSettingRepository = projectSettingRepository ?? throw new ArgumentNullException(nameof(projectSettingRepository));
        }

        #region 参数配置相关
        #region TBDataParamOptions参数
        /// <summary>
        /// 加载面密度界面显示的计算参数
        /// </summary>
        /// <param name="sideType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public List<EParamOption> LoadParamOptionsProjectParameters(ESideType sideType)
        {
            if (_projectSettingRepository == null) throw new ArgumentNullException(nameof(_projectSettingRepository));
            var paramName = $"TBDataParamOptions{sideType}";
            if (!_projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, "", DataTypeConverter.TypeToString(typeof(EParamOption[])), 2)
                    {
                        Remark = "涂布闭环面密度实时参数显示配置"
                    }
                );
            }

            var paramValues = new List<EParamOption>();
            var paramModel = _projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
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
        /// <param name="sideType"></param>
        /// <param name="paramOptions"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public void SaveParamOptionsProjectParameters(ESideType sideType, List<EParamOption> paramOptions)
        {
            if (_projectSettingRepository == null) throw new ArgumentNullException(nameof(_projectSettingRepository));

            var paramName = $"TBDataParamOptions{sideType}";

            var paramModel = _projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel == null) return;
            paramModel.Value = string.Join(',', paramOptions);
            _projectSettingRepository.Update(paramModel);
        }
        #endregion

        #region ArithmeticModel参数
        /// <summary>
        /// 加载算法相关参数，参数名称格式为"ArithmeticSetting_{closedLoopType}{sideType}"，如"ArithmeticSetting_MD_A"
        /// </summary>
        /// <param name="closedLoopType"></param>
        /// <param name="sideType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public ArithmeticModel? LoadArithmeticProjectParamrers(EClosedLoopType closedLoopType, ESideType sideType)
        {
            if (_projectSettingRepository == null) throw new ArgumentNullException(nameof(_projectSettingRepository));
            var paramName = $"ArithmeticSetting_{closedLoopType}{sideType}";

            var model = new ArithmeticModel();
            model.IsEnable = false;
            model.ArithmeticName = $"DY-{closedLoopType}-L1-1-N-{(sideType == ESideType.A ? "S" : "D")}-314Ah";
            if (!_projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonConvert.SerializeObject(model), "string", 2)
                    {
                        Remark = "涂布闭环算法设置"
                    }
                );
            }

            var paramModel = _projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel == null) return null;
            var paramValue = JsonConvert.DeserializeObject<ArithmeticModel>(paramModel?.Value ?? "");
            return paramValue;
        }

        /// <summary>
        /// 保存算法相关参数
        /// </summary>
        /// <param name="closedLoopType"></param>
        /// <param name="sideType"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public bool SaveArithmeticProjectParamrers(EClosedLoopType closedLoopType, ESideType sideType, ArithmeticModel value)
        {
            if (_projectSettingRepository == null) throw new ArgumentNullException(nameof(_projectSettingRepository));
            var paramName = $"ArithmeticSetting_{closedLoopType}{sideType}";
            return SaveProjectParametersAsJson(paramName, value);
        }
        #endregion

        #region ClosedLoopSetting参数
        /// <summary>
        /// 加载涂布闭环参数设置，参数名称格式为"ClosedLoopSetting_{closedLoopType}{sideType}"，如"ClosedLoopSetting_MD_A"
        /// </summary>
        /// <param name="closedLoopType"></param>
        /// <param name="sideType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public ClosedLoopSettingModel? LoadClosedLoopSettingProjectParamrers(EClosedLoopType closedLoopType, ESideType sideType)
        {
            if (_projectSettingRepository == null) throw new ArgumentNullException(nameof(_projectSettingRepository));
            var paramName = $"ClosedLoopSetting_{closedLoopType}{sideType}";

            var model = new ClosedLoopSettingModel();
            if (!_projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonConvert.SerializeObject(model), "string", 2)
                    {
                        Remark = "涂布闭环参数设置"
                    }
                );
            }

            var paramModel = _projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel == null) return null;
            var paramValue = JsonConvert.DeserializeObject<ClosedLoopSettingModel>(paramModel?.Value ?? "");
            return paramValue;
        }

        /// <summary>
        /// 保存涂布闭环参数设置
        /// </summary>
        /// <param name="closedLoopType"></param>
        /// <param name="sideType"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public bool SaveClosedLoopSettingProjectParamrers(EClosedLoopType closedLoopType, ESideType sideType, ClosedLoopSettingModel value)
        {
            if (_projectSettingRepository == null) throw new ArgumentNullException(nameof(_projectSettingRepository));
            var paramName = $"ClosedLoopSetting_{closedLoopType}{sideType}";
            return SaveProjectParametersAsJson(paramName, value);
        }
        #endregion

        #region GeneralSettings参数
        /// <summary>
        /// 加载涂布闭环通用参数设置，参数名称为"GeneralSettings"
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public GeneralSettingsModel? LoadGeneralSettingsProjectParamrers()
        {
            if (_projectSettingRepository == null) throw new ArgumentNullException(nameof(_projectSettingRepository));
            var paramName = $"GeneralSettings";

            var model = new GeneralSettingsModel();
            if (!_projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonConvert.SerializeObject(model), "string", 2)
                    {
                        Remark = "涂布闭环通用设置"
                    }
                );
            }

            var paramModel = _projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel == null) return null;
            var paramValue = JsonConvert.DeserializeObject<GeneralSettingsModel>(paramModel?.Value ?? "");
            return paramValue;
        }

        /// <summary>
        /// 保存涂布闭环通用参数设置
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public bool SaveGeneralSettingsProjectParamrers(GeneralSettingsModel value)
        {
            if (_projectSettingRepository == null) throw new ArgumentNullException(nameof(_projectSettingRepository));
            var paramName = $"GeneralSettings";
            return SaveProjectParametersAsJson(paramName, value);
        }
        #endregion

        #region BasicFunctionSetting参数
        /// <summary>
        /// 加载涂布闭环基础功能设置，参数名称为"BasicFunctionSetting"
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public BasicFunctionSettingModel? LoadBasicFunctionSettingProjectParamrers()
        {
            if (_projectSettingRepository == null) throw new ArgumentNullException(nameof(_projectSettingRepository));
            var paramName = $"BasicFunctionSetting";

            var model = new BasicFunctionSettingModel();
            if (!_projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonConvert.SerializeObject(model), "string", 2)
                    {
                        Remark = "涂布闭环基础功能设置"
                    }
                );
            }

            var paramModel = _projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel == null) return null;
            var paramValue = JsonConvert.DeserializeObject<BasicFunctionSettingModel>(paramModel?.Value ?? "");
            return paramValue;
        }

        /// <summary>
        /// 保存涂布闭环基础功能设置
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public bool SaveBasicFunctionSettingProjectParamrers(BasicFunctionSettingModel value)
        {
            if (_projectSettingRepository == null) throw new ArgumentNullException(nameof(_projectSettingRepository));
            var paramName = $"BasicFunctionSetting";
            return SaveProjectParametersAsJson(paramName, value);
        }
        #endregion

        #region SpecialFunctionSetting参数
        /// <summary>
        /// 加载涂布闭环特殊功能设置，参数名称为"SpecialFunctionSetting"
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public SpecialFunctionSettingModel? LoadSpecialFunctionSettingProjectParamrers()
        {
            if (_projectSettingRepository == null) throw new ArgumentNullException(nameof(_projectSettingRepository));
            var paramName = $"SpecialFunctionSetting";

            var model = new SpecialFunctionSettingModel();
            if (!_projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonConvert.SerializeObject(model), "string", 2)
                    {
                        Remark = "涂布闭环特殊功能设置"
                    }
                );
            }

            var paramModel = _projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel == null) return null;
            var paramValue = JsonConvert.DeserializeObject<SpecialFunctionSettingModel>(paramModel?.Value ?? "");
            return paramValue;
        }

        /// <summary>
        /// 保存涂布闭环特殊功能设置
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public bool SaveSpecialFunctionSettingProjectParamrers(SpecialFunctionSettingModel value)
        {
            if (_projectSettingRepository == null) throw new ArgumentNullException(nameof(_projectSettingRepository));
            var paramName = $"SpecialFunctionSetting";
            return SaveProjectParametersAsJson(paramName, value);
        }
        #endregion

        #region AlarmConfigs参数
        /// <summary>
        /// 加载涂布闭环报警信息配置，参数名称为"AlarmConfigs"
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public List<AlarmConfigModel> LoadAlarmConfigsProjectParamrers()
        {
            if (_projectSettingRepository == null) throw new ArgumentNullException(nameof(_projectSettingRepository));
            var paramName = $"AlarmConfigs";

            var model = new List<AlarmConfigModel>();
            if (!_projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonConvert.SerializeObject(model), "string", 2)
                    {
                        Remark = "涂布闭环报警信息配置"
                    }
                );
            }

            var paramValues = new List<AlarmConfigModel>();
            var paramModel = _projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel == null) return paramValues;
            paramValues = JsonConvert.DeserializeObject<List<AlarmConfigModel>>(paramModel?.Value ?? "");
            return paramValues ?? new List<AlarmConfigModel>();
        }

        /// <summary>
        /// 保存涂布闭环报警信息配置
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public bool SaveAlarmConfigsProjectParamrers(List<AlarmConfigModel> value)
        {
            if (_projectSettingRepository == null) throw new ArgumentNullException(nameof(_projectSettingRepository));
            var paramName = $"AlarmConfigs";
            return SaveProjectParametersAsJson(paramName, value);
        }
        #endregion

        #region AlarmParamConfig参数
        /// <summary>
        /// 加载涂布闭环报警信息配置，参数名称为"AlarmConfigParam"
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public AlarmConfigParamModel? LoadAlarmConfigParamProjectParamrers()
        {
            if (_projectSettingRepository == null) throw new ArgumentNullException(nameof(_projectSettingRepository));
            var paramName = $"AlarmConfigParam";

            var model = new AlarmConfigParamModel();
            if (!_projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonConvert.SerializeObject(model), "string", 2)
                    {
                        Remark = "涂布闭环报警信息配置"
                    }
                );
            }

            var paramModel = _projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel == null) return null;
            var paramValue = JsonConvert.DeserializeObject<AlarmConfigParamModel>(paramModel?.Value ?? "");
            return paramValue;
        }

        /// <summary>
        /// 保存涂布闭环报警信息配置
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public bool SaveAlarmConfigParamProjectParamrers(AlarmConfigParamModel value)
        {
            if (_projectSettingRepository == null) throw new ArgumentNullException(nameof(_projectSettingRepository));
            var paramName = $"AlarmConfigParam";
            return SaveProjectParametersAsJson(paramName, value);
        }
        #endregion

        #region CommunicationSetting参数
        /// <summary>
        /// 加载涂布闭环通讯配置，参数名称为"CommunicationSetting"
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public CommunicationSettingModel? LoadCommunicationSettingProjectParamrers()
        {
            if (_projectSettingRepository == null) throw new ArgumentNullException(nameof(_projectSettingRepository));
            var paramName = $"CommunicationSetting";

            var model = new CommunicationSettingModel();
            if (!_projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, JsonConvert.SerializeObject(model), "string", 2)
                    {
                        Remark = "涂布闭环通讯配置"
                    }
                );
            }

            var paramModel = _projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramModel == null) return null;
            var paramValue = JsonConvert.DeserializeObject<CommunicationSettingModel>(paramModel?.Value ?? "");
            return paramValue;
        }

        /// <summary>
        /// 保存涂布闭环通讯配置
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public bool SaveCommunicationSettingProjectParamrers(CommunicationSettingModel value)
        {
            if (_projectSettingRepository == null) throw new ArgumentNullException(nameof(_projectSettingRepository));
            var paramName = $"CommunicationSetting";
            return SaveProjectParametersAsJson(paramName, value);
        }
        #endregion

        #region 通用Json方式保存参数
        /// <summary>
        /// 保存为Json字符串的参数，适用于复杂对象的保存，调用时需要保证value对象是可序列化的
        /// </summary>
        /// <param name="paramName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool SaveProjectParametersAsJson(string paramName, object value)
        {
            var paramrer = _projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramrer == null) return false;
            paramrer.Value = JsonConvert.SerializeObject(value);
            return _projectSettingRepository.Update(paramrer);
        }
        #endregion
        #endregion
    }
}
