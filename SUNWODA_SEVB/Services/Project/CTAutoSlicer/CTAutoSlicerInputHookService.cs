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
        private Action? _onAltX;
        private Action? _onAltP;
        private Action? _onAltO;

        public CTAutoSlicerInputHookService(ILoggerService<CTAutoSlicerInputHookService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 启动全局热键：Alt+X 中断、Alt+P 暂停/继续、Alt+O 锁定/解锁键鼠。
        /// 已移除 Esc（易误触）。
        /// </summary>
        public void Start(Action onAltX, Action? onAltP = null, Action? onAltO = null)
        {
            Stop();
            _onAltX = onAltX;
            _onAltP = onAltP;
            _onAltO = onAltO;
            _hook = new TaskPoolGlobalHook();
            _hook.KeyPressed += OnKeyPressed;
            _hookThread = new Thread(() => _hook.Run()) { IsBackground = true };
            _hookThread.Start();
            _logger.Info(
                "CT 自动化切片全局按键 Hook 已启动（Alt+X 中断，Alt+P 暂停/继续，Alt+O 锁定/解锁键鼠）");
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
            _onAltX = null;
            _onAltP = null;
            _onAltO = null;
        }

        private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
        {
            if (IsAltOPressed(e))
            {
                // 由 ToggleInputBlock 决定加锁或解锁，此处不先强行 Unlock
                _onAltO?.Invoke();
                return;
            }

            if (IsAltPPressed(e))
            {
                // 暂停时需能操作鼠标；继续时由后续自动化步骤自行 Lock
                WinAPIHelper.BlockInput(false);
                _onAltP?.Invoke();
                return;
            }

            if (IsAltXPressed(e))
            {
                WinAPIHelper.BlockInput(false);
                _onAltX?.Invoke();
            }
        }

        private static bool IsAltXPressed(KeyboardHookEventArgs e) =>
            e.Data.KeyCode == KeyCode.VcX && e.RawEvent.Mask.HasAlt();

        private static bool IsAltPPressed(KeyboardHookEventArgs e) =>
            e.Data.KeyCode == KeyCode.VcP && e.RawEvent.Mask.HasAlt();

        private static bool IsAltOPressed(KeyboardHookEventArgs e) =>
            e.Data.KeyCode == KeyCode.VcO && e.RawEvent.Mask.HasAlt();

        public void Dispose()
        {
            Stop();
        }
    }
}
