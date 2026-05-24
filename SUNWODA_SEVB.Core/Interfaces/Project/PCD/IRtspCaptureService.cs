using OpenCvSharp;

namespace SUNWODA_SEVB.Core.Interfaces.Project.PCD
{
    public interface IRtspCaptureService
    {
         event Action<Mat> FrameCaptured;
        void Start(string rtspUrl);
        void Stop();
    }
}
