using System.Windows;
using System.Windows.Controls;
using SUNWODA_SEVB.ViewModels.Pages.CTAutoSlicer;

namespace SUNWODA_SEVB.Views.Pages.CTAutoSlicer
{
    public partial class CTAutoSlicerPage : Page
    {
        private ScrollViewer? _importLogScrollViewer;
        private bool _isImportLogHooked;

        public CTAutoSlicerPage()
        {
            InitializeComponent();
            Loaded += OnPageLoaded;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            HookImportLogViewModel();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is VM_CTAutoSlicerPage oldViewModel)
            {
                oldViewModel.ImportLogScrollToEndRequested -= OnImportLogScrollToEndRequested;
            }

            HookImportLogViewModel();
        }

        private void HookImportLogViewModel()
        {
            if (DataContext is not VM_CTAutoSlicerPage viewModel)
            {
                return;
            }

            viewModel.ImportLogScrollToEndRequested -= OnImportLogScrollToEndRequested;
            viewModel.ImportLogScrollToEndRequested += OnImportLogScrollToEndRequested;

            if (_isImportLogHooked)
            {
                return;
            }

            ImportLogTextBox.Loaded += OnImportLogTextBoxLoaded;
            _isImportLogHooked = true;
        }

        private void OnImportLogTextBoxLoaded(object sender, RoutedEventArgs e)
        {
            _importLogScrollViewer = FindScrollViewer(ImportLogTextBox);
            if (_importLogScrollViewer != null)
            {
                _importLogScrollViewer.ScrollChanged += OnImportLogScrollChanged;
            }
        }

        private void OnImportLogScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_importLogScrollViewer == null || DataContext is not VM_CTAutoSlicerPage viewModel)
            {
                return;
            }

            const double threshold = 2.0;
            var isAtBottom = _importLogScrollViewer.VerticalOffset
                >= _importLogScrollViewer.ScrollableHeight - threshold;
            viewModel.NotifyImportLogScrollChanged(isAtBottom);
        }

        private void OnImportLogScrollToEndRequested(object? sender, EventArgs e)
        {
            if (_importLogScrollViewer == null)
            {
                _importLogScrollViewer = FindScrollViewer(ImportLogTextBox);
                if (_importLogScrollViewer != null)
                {
                    _importLogScrollViewer.ScrollChanged += OnImportLogScrollChanged;
                }
            }

            ImportLogTextBox.ScrollToEnd();
            if (_importLogScrollViewer != null)
            {
                _importLogScrollViewer.ScrollToEnd();
            }
        }

        private static ScrollViewer? FindScrollViewer(DependencyObject element)
        {
            if (element is ScrollViewer scrollViewer)
            {
                return scrollViewer;
            }

            for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(element); index++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(element, index);
                var result = FindScrollViewer(child);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }
}
