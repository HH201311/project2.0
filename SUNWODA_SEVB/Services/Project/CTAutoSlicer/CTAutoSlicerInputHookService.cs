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
        private Action? _onCtrlX;
        private Action? _onAltP;

        public CTAutoSlicerInputHookService(ILoggerService<CTAutoSlicerInputHookService> logger)
        {
            _logger = logger;
        }

        public void Start(Action onDoubleEsc, Action onSingleEsc, Action onCtrlX, Action? onAltP = null)
        {
            Stop();
            _onDoubleEsc = onDoubleEsc;
            _onSingleEsc = onSingleEsc;
            _onCtrlX = onCtrlX;
            _onAltP = onAltP;
            _hook = new TaskPoolGlobalHook();
            _hook.KeyPressed += OnKeyPressed;
            _hookThread = new Thread(() => _hook.Run()) { IsBackground = true };
            _hookThread.Start();
            _logger.Info("CT 自动化切片全局按键 Hook 已启动（Esc 锁输入/双击中断，Ctrl+X 中断，Alt+P 暂停/继续）");
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
            _onCtrlX = null;
            _onAltP = null;
        }

        private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
        {
            if (IsAltPPressed(e))
            {
                // 暂停时需能操作鼠标；继续时由后续自动化步骤自行 Lock
                WinAPIHelper.BlockInput(false);
                _onAltP?.Invoke();
                return;
            }

            if (IsCtrlXPressed(e))
            {
                WinAPIHelper.BlockInput(false);
                _onCtrlX?.Invoke();
                return;
            }

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

        private static bool IsCtrlXPressed(KeyboardHookEventArgs e) =>
            e.Data.KeyCode == KeyCode.VcX && e.RawEvent.Mask.HasCtrl();

        private static bool IsAltPPressed(KeyboardHookEventArgs e) =>
            e.Data.KeyCode == KeyCode.VcP && e.RawEvent.Mask.HasAlt();

        public void Dispose()
        {
            Stop();
        }
    }
}
