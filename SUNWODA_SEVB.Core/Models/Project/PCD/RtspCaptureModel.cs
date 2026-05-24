using OpenCvSharp;
using SUNWODA_SEVB.Core.Interfaces.Project.PCD;

namespace SUNWODA_SEVB.Core.Models.Project.PCD
{
    public class RtspCaptureModel:IRtspCaptureService
    {
        private VideoCapture? _capture;
        private CancellationTokenSource? _cts;
        public event Action<Mat>? FrameCaptured;

        public void Start(string rtspUrl)
        {
            _cts = new CancellationTokenSource();
            Task.Run(() => CaptureLoop(rtspUrl, _cts.Token));
        }

        private void CaptureLoop(string url, CancellationToken ct)
        {
            _capture = new VideoCapture(url);
            if (!_capture.IsOpened())
                throw new Exception("无法打开 RTSP 流");

            var frame = new Mat();
            while (!ct.IsCancellationRequested)
            {
                if (_capture.Read(frame))
                {
                    // 克隆一份，避免后续修改影响事件订阅者
                    FrameCaptured?.Invoke(frame.Clone());
                }
                else
                {
                    break;
                }
            }
        }

        public void Stop()
        {
            _cts?.Cancel();
            _capture?.Release();
        }
    }

}
