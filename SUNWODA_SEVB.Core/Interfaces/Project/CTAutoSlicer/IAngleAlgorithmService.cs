namespace SUNWODA_SEVB.Core.Interfaces.Project.CTAutoSlicer
{
    public interface IAngleAlgorithmService
    {
        bool IsInitialized { get; }

        Task InitializeAsync(string dllDownloadBaseUrl, CancellationToken cancellationToken = default);

        (double deg, string direction) MeasureAngle(string measurementType, string imagePath);
    }
}
