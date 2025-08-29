using Mapster;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Data.Models;

namespace SUNWODA_SEVB.Data.Mappings
{
    /// <summary>
    /// 统一的Mapster映射配置
    /// </summary>
    public static class MapsterConfig
    {
        private static bool _isConfigured = false;

        public static void Configure()
        {
            if (_isConfigured) return;

            // AppLog映射配置
            TypeAdapterConfig<AppLogModel, AppLog>
                .NewConfig()
                .PreserveReference(true);
            //.Map(dest => dest.ID, src => src.ID)
            //.Map(dest => dest.LogTime, src => src.LogTime)
            //.Map(dest => dest.LogLevel, src => src.LogLevel)
            //.Map(dest => dest.Logger, src => src.Logger)
            //.Map(dest => dest.Message, src => src.Message)
            //.Map(dest => dest.Exception, src => src.Exception);

            TypeAdapterConfig<AppLog, AppLogModel>
                .NewConfig()
                .PreserveReference(true);
            //.Map(dest => dest.ID, src => src.ID)
            //.Map(dest => dest.LogTime, src => src.LogTime)
            //.Map(dest => dest.LogLevel, src => src.LogLevel)
            //.Map(dest => dest.Logger, src => src.Logger)
            //.Map(dest => dest.Message, src => src.Message)
            //.Map(dest => dest.Exception, src => src.Exception);

            // Device映射配置
            TypeAdapterConfig<DeviceModel, Device>
                .NewConfig()
                .PreserveReference(true);

            TypeAdapterConfig<Device, DeviceModel>
                .NewConfig()
                .PreserveReference(true);

            // GlobalSetting映射配置
            TypeAdapterConfig<GlobalSettingModel, GlobalSetting>
                .NewConfig()
                .PreserveReference(true);

            TypeAdapterConfig<GlobalSetting, GlobalSettingModel>
                .NewConfig()
                .PreserveReference(true);

            // MesInterfaceLog映射配置
            TypeAdapterConfig<MesInterfaceLogModel, MesInterfaceLog>
                .NewConfig()
                .PreserveReference(true);

            TypeAdapterConfig<MesInterfaceLog, MesInterfaceLogModel>
                .NewConfig()
                .PreserveReference(true);

            // PLCAddressConfig映射配置
            TypeAdapterConfig<PLCAddressConfigModel, PLCAddressConfig>
                .NewConfig()
                .PreserveReference(true);

            TypeAdapterConfig<PLCAddressConfig, PLCAddressConfigModel>
                .NewConfig()
                .PreserveReference(true);

            // PLCConfig映射配置
            TypeAdapterConfig<PLCConfigModel, PLCConfig>
                .NewConfig()
                .PreserveReference(true);

            TypeAdapterConfig<PLCConfig, PLCConfigModel>
                .NewConfig()
                .PreserveReference(true);

            // PLCRWConfig映射配置
            TypeAdapterConfig<PLCRWConfigModel, PLCRWConfig>
                .NewConfig()
                .PreserveReference(true);

            TypeAdapterConfig<PLCRWConfig, PLCRWConfigModel>
                .NewConfig()
                .PreserveReference(true);

            // ProjectSetting映射配置
            TypeAdapterConfig<ProjectSettingModel, ProjectSetting>
                .NewConfig()
                .PreserveReference(true);

            TypeAdapterConfig<ProjectSetting, ProjectSettingModel>
                .NewConfig()
                .PreserveReference(true);

            // User映射配置
            TypeAdapterConfig<UsersModel, Users>
                .NewConfig()
                .PreserveReference(true);

            TypeAdapterConfig<Users, UsersModel>
                .NewConfig()
                .PreserveReference(true);

            // WebInterfaceLog映射配置
            TypeAdapterConfig<WebInterfaceLogModel, WebInterfaceLog>
                .NewConfig()
                .PreserveReference(true);

            TypeAdapterConfig<WebInterfaceLog, WebInterfaceLogModel>
                .NewConfig()
                .PreserveReference(true);

            // WorkSpaceProject映射配置
            TypeAdapterConfig<WorkSpaceProjectModel, WorkSpaceProject>
                .NewConfig()
                .PreserveReference(true);

            TypeAdapterConfig<WorkSpaceProject, WorkSpaceProjectModel>
                .NewConfig()
                .PreserveReference(true);

            _isConfigured = true;
        }
    }
}
