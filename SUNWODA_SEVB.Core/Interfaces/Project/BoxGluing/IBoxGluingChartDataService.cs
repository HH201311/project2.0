using SUNWODA_SEVB.Core.Models.Project.BoxGluing;

namespace SUNWODA_SEVB.Core.Interfaces.Project.BoxGluing
{
    public interface IBoxGluingChartDataService
    {
        void Configure(BoxGluingAcquisitionConfig config);

        void ClearStation(int station);

        void ClearAll();

        void Append(BoxGluingSampleRecord record, IReadOnlyList<BoxGluingChannelConfig> channels);

        BoxGluingChartSnapshot CaptureSnapshot();
    }
}
