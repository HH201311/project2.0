

namespace SUNWODA_SEVB.WEB.Services
{
    /// <summary>
    /// WEB服务状态管理
    /// </summary>
    public class WebStateService
    {
        private readonly object _lockObject = new object();
        private bool _isConnected;
        private bool _isDeviceBound;
        private bool _isRunning;

        public event Action<bool>? ConnectionStatusChanged;
        public event Action<bool>? DeviceBindingStatusChanged;
        public event Action<bool>? RunningStatusChanged;

        public bool IsConnected
        {
            get
            {
                lock (_lockObject)
                {
                    return _isConnected;
                }
            }
        }

        public bool IsDeviceBound
        {
            get
            {
                lock (_lockObject)
                {
                    return _isDeviceBound;
                }
            }
        }

        public bool IsRunning
        {
            get
            {
                lock (_lockObject)
                {
                    return _isRunning;
                }
            }
        }

        public void UpdateConnectionStatus(bool isConnected)
        {
            bool changed;
            lock (_lockObject)
            {
                changed = _isConnected != isConnected;
                _isConnected = isConnected;
            }
            // 锁外触发事件，避免死锁
            if (changed) ConnectionStatusChanged?.Invoke(isConnected);
        }

        public void UpdateDeviceBindingStatus(bool isBound)
        {
            bool changed;
            lock (_lockObject)
            {
                changed = _isDeviceBound != isBound;
                _isDeviceBound = isBound;
            }
            if (changed) DeviceBindingStatusChanged?.Invoke(isBound);
        }

        public void UpdateRunningStatus(bool isRunning)
        {
            bool changed;
            lock (_lockObject)
            {
                changed = _isRunning != isRunning;
                _isRunning = isRunning;
            }
            if (changed) RunningStatusChanged?.Invoke(isRunning);
        }
    }
}
