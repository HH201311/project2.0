using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SUNWODA_SEVB.ViewModels.Pages.CTAutoSlicer;

namespace SUNWODA_SEVB.Views.Pages.CTAutoSlicer
{
    public partial class CTAutoSlicerPage : Page
    {
        private ScrollViewer? _importLogScrollViewer;
        private bool _isImportLogHooked;
        private VM_CTAutoSlicerPage? _themeViewModel;

        public CTAutoSlicerPage()
        {
            InitializeComponent();
            Loaded += OnPageLoaded;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            HookImportLogViewModel();
            ApplyThemeColorsFromViewModel();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is VM_CTAutoSlicerPage oldViewModel)
            {
                oldViewModel.ImportLogScrollToEndRequested -= OnImportLogScrollToEndRequested;
                oldViewModel.ThemeColorsChanged -= OnThemeColorsChanged;
            }

            HookImportLogViewModel();
            ApplyThemeColorsFromViewModel();
        }

        private void HookImportLogViewModel()
        {
            if (DataContext is not VM_CTAutoSlicerPage viewModel)
            {
                return;
            }

            if (!ReferenceEquals(_themeViewModel, viewModel))
            {
                if (_themeViewModel != null)
                {
                    _themeViewModel.ThemeColorsChanged -= OnThemeColorsChanged;
                }

                _themeViewModel = viewModel;
                _themeViewModel.ThemeColorsChanged += OnThemeColorsChanged;
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

        private void OnThemeColorsChanged(object? sender, EventArgs e)
        {
            ApplyThemeColorsFromViewModel();
        }

        private void ApplyThemeColorsFromViewModel()
        {
            if (DataContext is not VM_CTAutoSlicerPage vm)
            {
                return;
            }

            var dict = FindThemeDictionary();
            if (dict == null)
            {
                return;
            }

            SetBrush(dict, "CasBgBrush", vm.ThemePageBgColor);
            SetBrush(dict, "CasOuterCardBrush", vm.ThemeOuterCardColor);
            SetBrush(dict, "CasSurfaceBrush", vm.ThemeInnerCardColor);
            SetBrush(dict, "CasPrimaryBrush", vm.ThemeSidebarColor);
            // 兼容旧键：主色映射到 Terracotta
            SetBrush(dict, "CasTerracottaBrush", vm.ThemeSidebarColor);
            SetBrush(dict, "CasHeroGradient", vm.ThemeInnerCardColor);
            SetBrush(dict, "CasPeachBrush", vm.ThemeInnerCardColor);
            SetBrush(dict, "CasApricotBrush", vm.ThemeInnerCardColor);
        }

        private ResourceDictionary? FindThemeDictionary()
        {
            foreach (var d in Resources.MergedDictionaries)
            {
                if (d.Contains("CasBgBrush"))
                {
                    return d;
                }
            }

            return Resources.Contains("CasBgBrush") ? Resources : null;
        }

        private static void SetBrush(ResourceDictionary dict, string key, string hex)
        {
            try
            {
                var converted = ColorConverter.ConvertFromString(hex);
                if (converted is not Color color)
                {
                    return;
                }

                dict[key] = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
            }
            catch
            {
                // ignore invalid color
            }
        }

        private void OnImportLogTextBoxLoaded(object sender, RoutedEventArgs e)
        {
            _importLogScrollViewer = FindScrollViewer(ImportLogTextBox);
            if (_importLogScrollViewer == null)
            {
                return;
            }

            _importLogScrollViewer.ScrollChanged -= OnImportLogScrollChanged;
            _importLogScrollViewer.ScrollChanged += OnImportLogScrollChanged;
        }

        private void OnImportLogScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (DataContext is not VM_CTAutoSlicerPage viewModel || _importLogScrollViewer == null)
            {
                return;
            }

            var atBottom = _importLogScrollViewer.VerticalOffset
                >= _importLogScrollViewer.ScrollableHeight - 2;
            viewModel.NotifyImportLogScrollChanged(atBottom);
        }

        private void OnImportLogScrollToEndRequested(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                _importLogScrollViewer ??= FindScrollViewer(ImportLogTextBox);
                _importLogScrollViewer?.ScrollToEnd();
            });
        }

        private static ScrollViewer? FindScrollViewer(DependencyObject root)
        {
            if (root is ScrollViewer sv)
            {
                return sv;
            }

            for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                var found = FindScrollViewer(child);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
