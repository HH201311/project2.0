using System.Windows.Controls;
using System.Windows.Threading;
using SUNWODA_SEVB.ViewModels.Pages.BoxGluing;

namespace SUNWODA_SEVB.Views.Pages.BoxGluing
{
    public partial class BoxGluingMonitoringPage : Page
    {
        private readonly DispatcherTimer _plotTimer;

        public BoxGluingMonitoringPage()
        {
            InitializeComponent();
            _plotTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _plotTimer.Tick += OnPlotTimerTick;
            Loaded += (_, _) => _plotTimer.Start();
            Unloaded += (_, _) => _plotTimer.Stop();
        }

        private void OnPlotTimerTick(object? sender, EventArgs e)
        {
            if (DataContext is not VM_BoxGluingMonitoringPage vm)
                return;

            var snapshot = vm.CaptureChart();
            vm.RefreshRealtime(snapshot);
            if (vm.PlotFocus == 0)
            {
                BoxGluingPlotHelper.Render(Station1Plot, snapshot, 1);
                BoxGluingPlotHelper.Render(Station2Plot, snapshot, 2);
                BoxGluingPlotHelper.Render(Station3Plot, snapshot, 3);
            }
            else
            {
                BoxGluingPlotHelper.Render(MaximizedPlot, snapshot, vm.PlotFocus);
            }
        }
    }
}
