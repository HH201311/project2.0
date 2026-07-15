using SharpHook;
using SharpHook.Native;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Tool.Helper;

namespace SUNWODA_SEVB.Services.Project.CTAutoSlicer
{
    public class CTAutoSlicerInputHookService : IDisposable
    {
        private readonly ILoggerService<CTAutoSlicerInputHookService> _logger;
        private TaskPoolGlobalHook? _hook;
        private Thread? _hookThread;
        private DateTime _prePressedEscTime;
        private Action? _onDoubleEsc;
        private Action? _onSingleEsc;

        public CTAutoSlicerInputHookService(ILoggerService<CTAutoSlicerInputHookService> logger)
        {
            _logger = logger;
        }

        public void Start(Action onDoubleEsc, Action onSingleEsc)
        {
            Stop();
            _onDoubleEsc = onDoubleEsc;
            _onSingleEsc = onSingleEsc;
            _hook = new TaskPoolGlobalHook();
            _hook.KeyPressed += OnKeyPressed;
            _hookThread = new Thread(() => _hook.Run()) { IsBackground = true };
            _hookThread.Start();
            _logger.Info("CT 自动化切片全局按键 Hook 已启动");
        }

        public void Stop()
        {
            if (_hook != null)
            {
                _hook.KeyPressed -= OnKeyPressed;
                _hook.Dispose();
                _hook = null;
            }
            _hookThread = null;
            _onDoubleEsc = null;
            _onSingleEsc = null;
        }

        private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
        {
            if (e.Data.KeyCode != KeyCode.VcEscape)
            {
                return;
            }

            var currentPressedEscTime = DateTime.Now;
            if ((currentPressedEscTime - _prePressedEscTime).TotalMilliseconds < 500)
            {
                WinAPIHelper.BlockInput(false);
                _onDoubleEsc?.Invoke();
            }
            else
            {
                _onSingleEsc?.Invoke();
            }

            _prePressedEscTime = currentPressedEscTime;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
