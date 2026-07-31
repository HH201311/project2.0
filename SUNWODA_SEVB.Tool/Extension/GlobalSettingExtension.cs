using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Tool.Extension
{
    public static class GlobalSettingExtension
    {
        #region IsInitPythonEnvironment
        /// <summary>
        /// 加载是否初始化Python环境的全局参数，如果不存在则返回默认值为false
        /// </summary>
        /// <param name="globalSettingRepository"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool LoadIsInitPythonEnvironmentGlobalParameters(this IGlobalSettingRepository globalSettingRepository)
        {
            if (globalSettingRepository == null) throw new ArgumentNullException(nameof(globalSettingRepository));
            var paramName = $"IsInitPythonEnvironment";
            var paramModel = globalSettingRepository.GetByName(paramName);
            if (paramModel == null) return false;
            return bool.TryParse(paramModel.Value, out bool result) ? result : false;
        }

        /// <summary>
        /// 保存是否初始化Python环境的全局参数
        /// </summary>
        /// <param name="globalSettingRepository"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SaveIsInitPythonEnvironmentGlobalParameters(this IGlobalSettingRepository globalSettingRepository, bool value)
        {
            if (globalSettingRepository == null) throw new ArgumentNullException(nameof(globalSettingRepository));
            var paramName = $"IsInitPythonEnvironment";
            if (!globalSettingRepository.Exists(model => model.Name == paramName))
            {
                globalSettingRepository.Add(
                    new GlobalSettingModel(paramName, "False", "bool", 2)
                    {
                        Remark = "是否初始化Python环境,true为开启,false为关闭,默认false"
                    }
                );
            }
            var paramModel = globalSettingRepository.GetByName(paramName);
            if (paramModel == null) return false;
            paramModel.Value = value.ToString();
            return globalSettingRepository.Update(paramModel);
        }
        #endregion

        #region PythonInstallDir
        /// <summary>
        /// 加载Python安装目录的全局参数，如果不存在则返回默认值为空字符串
        /// </summary>
        /// <param name="globalSettingRepository"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static string LoadPythonInstallDirGlobalParameters(this IGlobalSettingRepository globalSettingRepository)
        {
            if (globalSettingRepository == null) throw new ArgumentNullException(nameof(globalSettingRepository));
            var paramName = $"PythonInstallDir";
            var paramModel = globalSettingRepository.GetByName(paramName);
            if (paramModel == null) return string.Empty;
            return paramModel.Value;
        }

        /// <summary>
        /// 保存Python安装目录的全局参数
        /// </summary>
        /// <param name="globalSettingRepository"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SavePythonInstallDirGlobalParameters(this IGlobalSettingRepository globalSettingRepository, string value)
        {
            if (globalSettingRepository == null) throw new ArgumentNullException(nameof(globalSettingRepository));
            var paramName = $"PythonInstallDir";
            if (!globalSettingRepository.Exists(model => model.Name == paramName))
            {
                globalSettingRepository.Add(
                    new GlobalSettingModel(paramName, "D:\\通用上位机\\Python\\python", "string", 2)
                    {
                        Remark = "Python安装目录路径"
                    }
                );
            }
            var paramModel = globalSettingRepository.GetByName(paramName);
            if (paramModel == null) return false;
            paramModel.Value = value;
            return globalSettingRepository.Update(paramModel);
        }
        #endregion
    }
}
