using SUNWODA_SEVB.Core.Models.Project.AIPredictive;

namespace SUNWODA_SEVB.Core.Interfaces.Project.AIPredictive
{
    /// <summary>读写本机厂家 + PLC IP（JSON；端口固定 44818；改后需保存并重连采集生效）。</summary>
    public interface IAIPredictivePlcEndpointStore
    {
        AIPredictivePlcEndpoint Load();
        void Save(AIPredictivePlcEndpoint endpoint);
        string SettingsFilePath { get; }
    }
}
