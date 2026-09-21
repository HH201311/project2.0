using SUNWODA_SEVB.Core.Models.Project.BoxGluing;
using System.Collections.Generic;

namespace SUNWODA_SEVB.Core.Interfaces.Project.BoxGluing
{
    public interface IBoxGluingAcquisitionService
    {
        bool IsRunning { get; }

        IReadOnlyDictionary<int, bool> StationActive { get; }

        string LastStatus { get; }

        long SampleCount { get; }

        event EventHandler? StateChanged;

        Task StartAsync(IReadOnlySet<int>? simulatedStations = null);

        Task StopAsync();

        void SetSimulatedStations(IReadOnlySet<int> stations);

        /// <summary>
        /// 模拟出胶触发：该工位启动采集，随机 5～8 秒后自动结束。
        /// </summary>
        void TriggerSimulatedGlue(int station);
    }
}
