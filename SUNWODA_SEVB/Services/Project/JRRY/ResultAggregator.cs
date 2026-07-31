using SUNWODA_SEVB.Core.Models.Project.JRRY;

namespace SUNWODA_SEVB.Services.Project.JRRY
{
    public class ResultAggregator
    {
        public event Action<DetectionResult>? ResultArrived;

        public Task PublishResultAsync(DetectionResult result)
        {
            ResultArrived?.Invoke(result);
            return Task.CompletedTask;
        }
    }
}