using System.Windows;
using SUNWODA_SEVB.ViewModels.Windows.Project.CTAutoSlicer;

namespace SUNWODA_SEVB.Views.Windows.Project.CTAutoSlicer
{
    public partial class Vision3dPointsStepDebugWindow : Window
    {
        private readonly VM_Vision3dPointsStepDebugWindow _vm;

        public Vision3dPointsStepDebugWindow(string? initialImagePath = null)
        {
            InitializeComponent();
            _vm = new VM_Vision3dPointsStepDebugWindow(initialImagePath);
            DataContext = _vm;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _vm.Dispose();
        }
    }
}
