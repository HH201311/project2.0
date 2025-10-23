using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using CsvHelper;
using CsvHelper.Configuration;
using ScottPlot.MultiplotLayouts;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Enumerations.Component;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Models;
using SUNWODA_SEVB.Core.Models.Component;
using SUNWODA_SEVB.Core.Models.Data;
using TouchSocket.Core;
using TouchSocket.Sockets;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace SUNWODA_SEVB.ViewModels.Pages.CoatingOPMM
{
    [Module("CoatingOPMM", "涂布工序监测系统")]
    public class VM_CoatingOPMMPage : ViewModelBase
    {
        private readonly ILoggerService<VM_CoatingOPMMPage> _logger;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IPLCRWConfigRepository _plcRWConfigRepository;
        private FrameworkElement? line1Page;
        private FrameworkElement? line2Page;
        private Random rnd = new Random();

        public VM_CoatingSingleLinePage? Line1PageViewModel { get; set; }
        public VM_CoatingSingleLinePage? Line2PageViewModel { get; set; }
        public FrameworkElement? Line1Page
        {
            get => line1Page;
            set => SetProperty(ref line1Page, value);
        }
        public FrameworkElement? Line2Page
        {
            get => line2Page;
            set => SetProperty(ref line2Page, value);
        }

        public VM_CoatingOPMMPage(
            ILoggerService<VM_CoatingOPMMPage> logger,
            IPLCConfigRepository plcConfigRepository,
            IPLCRWConfigRepository plcRWConfigRepository
        )
        {
            _logger = logger;
            _plcConfigRepository = plcConfigRepository;
            _plcRWConfigRepository = plcRWConfigRepository;
        }

        private async Task InitPLCConfig()
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                Delimiter = ",",
                TrimOptions = TrimOptions.Trim,
                BadDataFound = null,
                MissingFieldFound = null,
                DetectDelimiter = false,
            };

            using var plcConfigReader = new StreamReader("./plc_config.csv");
            using var plcConfigCsv = new CsvReader(plcConfigReader, config);
            plcConfigCsv.Context.RegisterClassMap<PLCConfigRowMap>();

            var plcConfigList = plcConfigCsv.GetRecords<PLCConfigModel>().ToList();
            plcConfigList.ForEach(item =>
            {
                item.IsEnable = true;
                if (!_plcConfigRepository.Exists(model => model.Name == item.Name))
                    _plcConfigRepository.Add(item);
            });

            using var plcRWConfigReader = new StreamReader("./plc_rw_config.csv");
            using var plcRWConfigCsv = new CsvReader(plcRWConfigReader, config);
            plcRWConfigCsv.Context.RegisterClassMap<PLCRWConfigRowMap>();

            var plcRWConfigList = plcRWConfigCsv.GetRecords<PLCRWConfigModel>().ToList();
            plcRWConfigList.ForEach(item =>
            {
                item.IsEnable = true;
                if (!_plcRWConfigRepository.Exists(model => model.Name == item.Name))
                    _plcRWConfigRepository.Add(item);
            });

            await Task.CompletedTask;
        }

        protected override Task OnNavigatedToAsync(object? parameter)
        {
            return base.OnNavigatedToAsync(parameter);
        }

        protected override async Task OnInitializeAsync()
        {
            //_ = InitPLCConfig();

            await RunOnUIThreadAsync(() =>
            {
                Line1PageViewModel = new VM_CoatingSingleLinePage(_logger)
                {
                    Title = "L1",
                    Status = false,
                };
                Line1Page = Line1PageViewModel.UIElement;
                Line2PageViewModel = new VM_CoatingSingleLinePage(_logger)
                {
                    Title = "L2",
                    Status = true,
                };
                Line2Page = Line2PageViewModel.UIElement;
            });

            var aDryColums = new ObservableCollection<ColumnHeader>()
            {
                new ColumnHeader { Name = "时间" },
                new ColumnHeader { Name = "A面极耳1" },
                new ColumnHeader { Name = "A面陶瓷1" },
                new ColumnHeader { Name = "A面碳膜1" },
                new ColumnHeader { Name = "A面陶瓷2" },
                new ColumnHeader { Name = "A面极耳2" },
                new ColumnHeader { Name = "A面陶瓷3" },
                new ColumnHeader { Name = "A面碳膜2" },
                new ColumnHeader { Name = "A面陶瓷4" },
                new ColumnHeader { Name = "A面极耳3" },
                new ColumnHeader { Name = "A面陶瓷5" },
                new ColumnHeader { Name = "A面碳膜3" },
                new ColumnHeader { Name = "A面陶瓷6" },
                new ColumnHeader { Name = "A面极耳4" },
                new ColumnHeader { Name = "A面陶瓷7" },
                new ColumnHeader { Name = "A面碳膜4" },
                new ColumnHeader { Name = "A面陶瓷8" },
                new ColumnHeader { Name = "A面极耳5" },
            };

            var bWetColums = new ObservableCollection<ColumnHeader>()
            {
                new ColumnHeader { Name = "时间" },
                new ColumnHeader { Name = "A面极耳1" },
                new ColumnHeader { Name = "A面陶瓷1" },
                new ColumnHeader { Name = "A面碳膜1" },
                new ColumnHeader { Name = "A面陶瓷2" },
                new ColumnHeader { Name = "A面极耳2" },
                new ColumnHeader { Name = "A面陶瓷3" },
                new ColumnHeader { Name = "A面碳膜2" },
                new ColumnHeader { Name = "A面陶瓷4" },
                new ColumnHeader { Name = "A面极耳3" },
                new ColumnHeader { Name = "A面陶瓷5" },
                new ColumnHeader { Name = "A面碳膜3" },
                new ColumnHeader { Name = "A面陶瓷6" },
                new ColumnHeader { Name = "A面极耳4" },
                new ColumnHeader { Name = "A面陶瓷7" },
                new ColumnHeader { Name = "A面碳膜4" },
                new ColumnHeader { Name = "A面陶瓷8" },
                new ColumnHeader { Name = "A面极耳5" },
            };

            var bDryColums = new ObservableCollection<ColumnHeader>()
            {
                new ColumnHeader { Name = "时间" },
                new ColumnHeader { Name = "A面极耳1" },
                new ColumnHeader { Name = "A面陶瓷1" },
                new ColumnHeader { Name = "A面碳膜1" },
                new ColumnHeader { Name = "A面陶瓷2" },
                new ColumnHeader { Name = "A面极耳2" },
                new ColumnHeader { Name = "A面陶瓷3" },
                new ColumnHeader { Name = "A面碳膜2" },
                new ColumnHeader { Name = "A面陶瓷4" },
                new ColumnHeader { Name = "A面极耳3" },
                new ColumnHeader { Name = "A面陶瓷5" },
                new ColumnHeader { Name = "A面碳膜3" },
                new ColumnHeader { Name = "A面陶瓷6" },
                new ColumnHeader { Name = "A面极耳4" },
                new ColumnHeader { Name = "A面陶瓷7" },
                new ColumnHeader { Name = "A面碳膜4" },
                new ColumnHeader { Name = "A面陶瓷8" },
                new ColumnHeader { Name = "A面极耳5" },
            };

            var aDryRows = new ObservableCollection<RowData>
            {
                new RowData
                {
                    Cells = new ObservableCollection<CellData>
                    {
                        new CellData
                        {
                            Value = "08:00:00",
                            Background = Brushes.Transparent,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.DarkGreen,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.White,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.White,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                    },
                },
                new RowData
                {
                    Cells = new ObservableCollection<CellData>
                    {
                        new CellData
                        {
                            Value = "12:00:00",
                            Background = Brushes.Transparent,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightYellow,
                            Foreground = Brushes.OrangeRed,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.White,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.White,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                    },
                },
                new RowData
                {
                    Cells = new ObservableCollection<CellData>
                    {
                        new CellData
                        {
                            Value = "18:00:00",
                            Background = Brushes.Transparent,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightCoral,
                            Foreground = Brushes.DarkRed,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.White,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightPink,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                    },
                },
            };

            var bWetRows = new ObservableCollection<RowData>
            {
                new RowData
                {
                    Cells = new ObservableCollection<CellData>
                    {
                        new CellData
                        {
                            Value = "08:00:00",
                            Background = Brushes.Transparent,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.DarkGreen,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.White,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.White,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                    },
                },
                new RowData
                {
                    Cells = new ObservableCollection<CellData>
                    {
                        new CellData
                        {
                            Value = "12:00:00",
                            Background = Brushes.Transparent,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightYellow,
                            Foreground = Brushes.OrangeRed,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.White,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.White,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                    },
                },
                new RowData
                {
                    Cells = new ObservableCollection<CellData>
                    {
                        new CellData
                        {
                            Value = "18:00:00",
                            Background = Brushes.Transparent,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightCoral,
                            Foreground = Brushes.DarkRed,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.White,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightPink,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                    },
                },
            };

            var bDryRows = new ObservableCollection<RowData>
            {
                new RowData
                {
                    Cells = new ObservableCollection<CellData>
                    {
                        new CellData
                        {
                            Value = "08:00:00",
                            Background = Brushes.Transparent,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.DarkGreen,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.White,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.White,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                    },
                },
                new RowData
                {
                    Cells = new ObservableCollection<CellData>
                    {
                        new CellData
                        {
                            Value = "12:00:00",
                            Background = Brushes.Transparent,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightYellow,
                            Foreground = Brushes.OrangeRed,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.White,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.White,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Red,
                        },
                    },
                },
                new RowData
                {
                    Cells = new ObservableCollection<CellData>
                    {
                        new CellData
                        {
                            Value = "18:00:00",
                            Background = Brushes.Transparent,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightCoral,
                            Foreground = Brushes.DarkRed,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.White,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightPink,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.Red,
                            Foreground = Brushes.White,
                        },
                    },
                },
            };

            Line1PageViewModel!.FaceADryDataTableColumns = aDryColums;

            Line1PageViewModel!.FaceADryDataTableRows = aDryRows;

            Line1PageViewModel!.FaceBWetDataTableColumns = bWetColums;

            Line1PageViewModel!.FaceBWetDataTableRows = bWetRows;

            Line1PageViewModel!.FaceBDryDataTableColumns = bDryColums;

            Line1PageViewModel!.FaceBDryDataTableRows = bDryRows;

            Line1PageViewModel!.DeviceInfos = new ObservableCollection<ParameterModel>()
            {
                new ParameterModel { PName = "拉线名", PValue = "L123" },
                new ParameterModel { PName = "涂布实时速度", PValue = "36.99" },
                new ParameterModel { PName = "设备IP", PValue = "10.215.118.8" },
                new ParameterModel { PName = "产品批号", PValue = "AD36X2124105D31" },
                new ParameterModel { PName = "设备编号", PValue = "#123" },
                new ParameterModel { PName = "膜卷号", PValue = "AD36X2124105D31" },
            };

            Line1PageViewModel.GeneralParameterInfos = new ObservableCollection<ParameterModel>()
            {
                new ParameterModel { PName = "放卷_A轴卷径", PValue = "1234" },
                new ParameterModel { PName = "放卷_B轴卷径", PValue = "1234" },
                new ParameterModel { PName = "机头B_左实时GAP", PValue = "1234" },
                new ParameterModel { PName = "机头B_右实时GAP", PValue = "1234" },
                new ParameterModel { PName = "机头B_浆料缓存罐液位_R", PValue = "1234" },
                new ParameterModel { PName = "机头B_绝缘浆料缓存罐液位_R", PValue = "1234" },
                new ParameterModel { PName = "机头A_浆料泵实时泵速3", PValue = "1234" },
                new ParameterModel { PName = "机头A_浆料泵实时泵速4", PValue = "1234" },
                new ParameterModel { PName = "收卷_A轴卷径", PValue = "1234" },
                new ParameterModel { PName = "收卷_B轴卷径", PValue = "1234" },
                new ParameterModel { PName = "机头A_左实时GAP", PValue = "1234" },
                new ParameterModel { PName = "机头A_右实时GAP", PValue = "1234" },
                new ParameterModel { PName = "机头A_浆料缓存罐液位_R", PValue = "1234" },
                new ParameterModel { PName = "机头A_绝缘浆料缓存罐液位_R", PValue = "1234" },
                new ParameterModel { PName = "机头B_浆料泵实时泵速1", PValue = "1234" },
                new ParameterModel { PName = "机头B_浆料泵实时泵速2", PValue = "1234" },
                new ParameterModel { PName = "A面实际涂布米数", PValue = "1234" },
                new ParameterModel { PName = "B面实际涂布米数", PValue = "1234" },
                new ParameterModel { PName = "系统正转速度", PValue = "1234" },
                new ParameterModel { PName = "系统运行实时速度", PValue = "1234" },
                new ParameterModel { PName = "机头A_浆料泵实时泵速1", PValue = "1234" },
                new ParameterModel { PName = "机头A_浆料泵实时泵速2", PValue = "1234" },
                new ParameterModel { PName = "机头B_浆料泵实时泵速3", PValue = "1234" },
                new ParameterModel { PName = "机头B_浆料泵实时泵速4", PValue = "1234" },
            };

            var aDryAndBWetColums = new ObservableCollection<ColumnHeader>
            {
                new ColumnHeader { Name = "时间" },
                new ColumnHeader { Name = "错位1" },
                new ColumnHeader { Name = "错位2" },
                new ColumnHeader { Name = "错位3" },
                new ColumnHeader { Name = "错位4" },
                new ColumnHeader { Name = "错位5" },
                new ColumnHeader { Name = "错位6" },
                new ColumnHeader { Name = "错位7" },
                new ColumnHeader { Name = "错位8" },
            };

            var aDryAndBDryColums = new ObservableCollection<ColumnHeader>
            {
                new ColumnHeader { Name = "时间" },
                new ColumnHeader { Name = "错位1" },
                new ColumnHeader { Name = "错位2" },
                new ColumnHeader { Name = "错位3" },
                new ColumnHeader { Name = "错位4" },
                new ColumnHeader { Name = "错位5" },
                new ColumnHeader { Name = "错位6" },
                new ColumnHeader { Name = "错位7" },
                new ColumnHeader { Name = "错位8" },
            };

            var aDryAndBWetRows = new ObservableCollection<RowData>
            {
                new RowData
                {
                    Cells = new ObservableCollection<CellData>
                    {
                        new CellData
                        {
                            Value = "08:00:00",
                            Background = Brushes.Transparent,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.DarkGreen,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.White,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.White,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                    },
                },
                new RowData
                {
                    Cells = new ObservableCollection<CellData>
                    {
                        new CellData
                        {
                            Value = "12:00:00",
                            Background = Brushes.Transparent,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.DarkGreen,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.White,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.White,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                    },
                },
                new RowData
                {
                    Cells = new ObservableCollection<CellData>
                    {
                        new CellData
                        {
                            Value = "18:00:00",
                            Background = Brushes.Transparent,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.DarkGreen,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.White,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.White,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                    },
                },
            };

            var aDryAndBDryRows = new ObservableCollection<RowData>
            {
                new RowData
                {
                    Cells = new ObservableCollection<CellData>
                    {
                        new CellData
                        {
                            Value = "08:00:00",
                            Background = Brushes.Transparent,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.DarkGreen,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.White,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.White,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                    },
                },
                new RowData
                {
                    Cells = new ObservableCollection<CellData>
                    {
                        new CellData
                        {
                            Value = "12:00:00",
                            Background = Brushes.Transparent,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.DarkGreen,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.White,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.White,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                    },
                },
                new RowData
                {
                    Cells = new ObservableCollection<CellData>
                    {
                        new CellData
                        {
                            Value = "18:00:00",
                            Background = Brushes.Transparent,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.DarkGreen,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.White,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.White,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                        new CellData
                        {
                            Value = "25.30",
                            Background = Brushes.LightGreen,
                            Foreground = Brushes.Green,
                        },
                    },
                },
            };

            Line1PageViewModel.FaceADryAndBWetDataTableColumns = aDryAndBWetColums;

            Line1PageViewModel.FaceADryAndBWetDataTableRows = aDryAndBWetRows;

            Line1PageViewModel.FaceADryAndBDryDataTableColumns = aDryAndBDryColums;

            Line1PageViewModel.FaceADryAndBDryDataTableRows = aDryAndBDryRows;

            Line1PageViewModel.FaceAPageViewModel!.SpecificationInfos =
                new ObservableCollection<ParameterModel>()
                {
                    new ParameterModel { PName = "规格上限", PValue = "54.50" },
                    new ParameterModel { PName = "规格下限", PValue = "49.50" },
                    new ParameterModel { PName = "控制上限", PValue = "54.00" },
                    new ParameterModel { PName = "控制下限", PValue = "50.00" },
                };

            Line1PageViewModel.FaceAPageViewModel!.DeviceMonitorInfos =
                new ObservableCollection<ParameterModel>()
                {
                    new ParameterModel { PName = "平均值", PValue = "334.66" },
                    new ParameterModel { PName = "实时值", PValue = "0.00" },
                    new ParameterModel { PName = "温度", PValue = "25.00" },
                    new ParameterModel { PName = "湿度", PValue = "22" },
                    new ParameterModel { PName = "扫描速度", PValue = "0.00" },
                    new ParameterModel { PName = "走带速度", PValue = "0.00" },
                    new ParameterModel { PName = "卷长", PValue = "8963.9740" },
                    new ParameterModel { PName = "分区极差", PValue = "2.79" },
                    new ParameterModel { PName = "基材宽度", PValue = "939.51" },
                    new ParameterModel { PName = "外温度", PValue = "25.00" },
                };

            await RunOnUIThreadAsync(() =>
            {
                Line1PageViewModel.FaceAPageViewModel!.RawSegments =
                    new ObservableCollection<CoatingSegmentModel>
                    {
                        new CoatingSegmentModel(
                            "Tab1",
                            CoatingSegmentType.EdgeTab,
                            new SolidColorBrush(Color.FromArgb(255, 234, 234, 234))
                        )
                        {
                            WidthA = 5.5,
                            WidthB = 5.6,
                            ForegroundB = new SolidColorBrush(Colors.Red),
                        },
                        new CoatingSegmentModel(
                            "CIL1",
                            CoatingSegmentType.CIL,
                            new SolidColorBrush(Color.FromArgb(255, 101, 101, 101))
                        ),
                        new CoatingSegmentModel(
                            "Coating1",
                            CoatingSegmentType.Coating,
                            new SolidColorBrush(Color.FromArgb(255, 204, 204, 204))
                        ),
                        new CoatingSegmentModel(
                            "CIL2",
                            CoatingSegmentType.CIL,
                            new SolidColorBrush(Color.FromArgb(255, 101, 101, 101))
                        ),
                        new CoatingSegmentModel(
                            "Tab2",
                            CoatingSegmentType.MiddleTab,
                            new SolidColorBrush(Color.FromArgb(255, 234, 234, 234))
                        ),
                        new CoatingSegmentModel(
                            "CIL3",
                            CoatingSegmentType.CIL,
                            new SolidColorBrush(Color.FromArgb(255, 101, 101, 101))
                        ),
                        new CoatingSegmentModel(
                            "Coating2",
                            CoatingSegmentType.Coating,
                            new SolidColorBrush(Color.FromArgb(255, 204, 204, 204))
                        ),
                        new CoatingSegmentModel(
                            "CIL4",
                            CoatingSegmentType.CIL,
                            new SolidColorBrush(Color.FromArgb(255, 101, 101, 101))
                        ),
                        new CoatingSegmentModel(
                            "Tab3",
                            CoatingSegmentType.MiddleTab,
                            new SolidColorBrush(Color.FromArgb(255, 234, 234, 234))
                        ),
                        new CoatingSegmentModel(
                            "CIL5",
                            CoatingSegmentType.CIL,
                            new SolidColorBrush(Color.FromArgb(255, 101, 101, 101))
                        ),
                        new CoatingSegmentModel(
                            "Coating3",
                            CoatingSegmentType.Coating,
                            new SolidColorBrush(Color.FromArgb(255, 204, 204, 204))
                        ),
                        new CoatingSegmentModel(
                            "CIL6",
                            CoatingSegmentType.CIL,
                            new SolidColorBrush(Color.FromArgb(255, 101, 101, 101))
                        ),
                        new CoatingSegmentModel(
                            "Tab4",
                            CoatingSegmentType.MiddleTab,
                            new SolidColorBrush(Color.FromArgb(255, 234, 234, 234))
                        ),
                        new CoatingSegmentModel(
                            "CIL7",
                            CoatingSegmentType.CIL,
                            new SolidColorBrush(Color.FromArgb(255, 101, 101, 101))
                        ),
                        new CoatingSegmentModel(
                            "Coating4",
                            CoatingSegmentType.Coating,
                            new SolidColorBrush(Color.FromArgb(255, 204, 204, 204))
                        ),
                        new CoatingSegmentModel(
                            "CIL8",
                            CoatingSegmentType.CIL,
                            new SolidColorBrush(Color.FromArgb(255, 101, 101, 101))
                        ),
                        new CoatingSegmentModel(
                            "Tab5",
                            CoatingSegmentType.EdgeTab,
                            new SolidColorBrush(Color.FromArgb(255, 234, 234, 234))
                        ),
                    };
            });

            Line1PageViewModel.FaceBPageViewModel!.SpecificationInfos =
                new ObservableCollection<ParameterModel>()
                {
                    new ParameterModel { PName = "规格上限", PValue = "54.50" },
                    new ParameterModel { PName = "规格下限", PValue = "49.50" },
                    new ParameterModel { PName = "控制上限", PValue = "54.00" },
                    new ParameterModel { PName = "控制下限", PValue = "50.00" },
                };

            Line1PageViewModel.FaceBPageViewModel!.DeviceMonitorInfos =
                new ObservableCollection<ParameterModel>()
                {
                    new ParameterModel { PName = "平均值", PValue = "334.66" },
                    new ParameterModel { PName = "实时值", PValue = "0.00" },
                    new ParameterModel { PName = "温度", PValue = "25.00" },
                    new ParameterModel { PName = "湿度", PValue = "22" },
                    new ParameterModel { PName = "扫描速度", PValue = "0.00" },
                    new ParameterModel { PName = "走带速度", PValue = "0.00" },
                    new ParameterModel { PName = "卷长", PValue = "8963.9740" },
                    new ParameterModel { PName = "分区极差", PValue = "2.79" },
                    new ParameterModel { PName = "基材宽度", PValue = "939.51" },
                    new ParameterModel { PName = "外温度", PValue = "25.00" },
                };

            await RunOnUIThreadAsync(() =>
            {
                Line1PageViewModel.FaceBPageViewModel!.RawSegments =
                    new ObservableCollection<CoatingSegmentModel>
                    {
                        new CoatingSegmentModel(
                            "Tab1",
                            CoatingSegmentType.EdgeTab,
                            new SolidColorBrush(Color.FromArgb(255, 234, 234, 234))
                        ),
                        new CoatingSegmentModel(
                            "CIL1",
                            CoatingSegmentType.CIL,
                            new SolidColorBrush(Color.FromArgb(255, 101, 101, 101))
                        ),
                        new CoatingSegmentModel(
                            "Coating1",
                            CoatingSegmentType.Coating,
                            new SolidColorBrush(Color.FromArgb(255, 204, 204, 204))
                        ),
                        new CoatingSegmentModel(
                            "CIL2",
                            CoatingSegmentType.CIL,
                            new SolidColorBrush(Color.FromArgb(255, 101, 101, 101))
                        ),
                        new CoatingSegmentModel(
                            "Tab2",
                            CoatingSegmentType.MiddleTab,
                            new SolidColorBrush(Color.FromArgb(255, 234, 234, 234))
                        ),
                        new CoatingSegmentModel(
                            "CIL3",
                            CoatingSegmentType.CIL,
                            new SolidColorBrush(Color.FromArgb(255, 101, 101, 101))
                        ),
                        new CoatingSegmentModel(
                            "Coating2",
                            CoatingSegmentType.Coating,
                            new SolidColorBrush(Color.FromArgb(255, 204, 204, 204))
                        ),
                        new CoatingSegmentModel(
                            "CIL4",
                            CoatingSegmentType.CIL,
                            new SolidColorBrush(Color.FromArgb(255, 101, 101, 101))
                        ),
                        new CoatingSegmentModel(
                            "Tab3",
                            CoatingSegmentType.MiddleTab,
                            new SolidColorBrush(Color.FromArgb(255, 234, 234, 234))
                        ),
                        new CoatingSegmentModel(
                            "CIL5",
                            CoatingSegmentType.CIL,
                            new SolidColorBrush(Color.FromArgb(255, 101, 101, 101))
                        ),
                        new CoatingSegmentModel(
                            "Coating3",
                            CoatingSegmentType.Coating,
                            new SolidColorBrush(Color.FromArgb(255, 204, 204, 204))
                        ),
                        new CoatingSegmentModel(
                            "CIL6",
                            CoatingSegmentType.CIL,
                            new SolidColorBrush(Color.FromArgb(255, 101, 101, 101))
                        ),
                        new CoatingSegmentModel(
                            "Tab4",
                            CoatingSegmentType.MiddleTab,
                            new SolidColorBrush(Color.FromArgb(255, 234, 234, 234))
                        ),
                        new CoatingSegmentModel(
                            "CIL7",
                            CoatingSegmentType.CIL,
                            new SolidColorBrush(Color.FromArgb(255, 101, 101, 101))
                        ),
                        new CoatingSegmentModel(
                            "Coating4",
                            CoatingSegmentType.Coating,
                            new SolidColorBrush(Color.FromArgb(255, 204, 204, 204))
                        ),
                        new CoatingSegmentModel(
                            "CIL8",
                            CoatingSegmentType.CIL,
                            new SolidColorBrush(Color.FromArgb(255, 101, 101, 101))
                        ),
                        new CoatingSegmentModel(
                            "Tab5",
                            CoatingSegmentType.EdgeTab,
                            new SolidColorBrush(Color.FromArgb(255, 234, 234, 234))
                        ),
                    };
            });

            Line1PageViewModel.FaceBPageViewModel!.FilmData = new List<double[]>
            {
                ([0.5, 0.5, 1.1, -1.2, 0.58, 0.2, 3.8]),
                ([0.5, 0.5, 1.1, -1.2, 0.58, 0.2, 3.8]),
                ([0.5, 0.5, 1.1, -1.2, 0.58, 0.2, 3.8]),
                ([0.5, 0.5, 1.1, -1.2, 0.58, 0.2, 3.8]),
            };

            _ = Task.Run(L1PageDataUpateTest);

            var gaugeServer = new DeviceTcpServer(_logger);
            gaugeServer.DataTypeJsonParse += GaugeServer_DataTypeJsonParse;
            gaugeServer.AlarmTypeJsonParse += GaugeServer_AlarmTypeJsonParse;
            await gaugeServer.StartAsync();
            await base.OnInitializeAsync();
        }

        private Brush GetRandomColor()
        {
            byte r = (byte)rnd.Next(0, 256); // 生成0-255之间的随机数
            byte g = (byte)rnd.Next(0, 256); // 生成0-255之间的随机数
            byte b = (byte)rnd.Next(0, 256); // 生成0-255之间的随机数

            return new SolidColorBrush(Color.FromRgb(r, g, b)); // 返回一个随机的SolidColorBrush
        }

        protected void L1PageFaceAFilmDataUpateTestByOne()
        {
            var data = new List<double[]>();
            for (int i = 0; i < 4; i++)
            {
                var arr = new double[7];
                for (int j = 0; j < 7; j++)
                {
                    arr[j] = rnd.NextDouble() * 7.6 - 3.8;
                }
                data.Add(arr);
            }
            Line1PageViewModel!.FaceAPageViewModel!.FilmData = data;
        }

        protected void L1PageFaceBFilmDataUpateTestByOne()
        {
            var data = new List<double[]>();
            for (int i = 0; i < 4; i++)
            {
                var arr = new double[7];
                for (int j = 0; j < 7; j++)
                {
                    arr[j] = rnd.NextDouble() * 7.6 - 3.8;
                }
                data.Add(arr);
            }
            Line1PageViewModel!.FaceBPageViewModel!.FilmData = data;
        }

        protected void L1PageFaceASpecificationInfoUpateTestByOne()
        {
            foreach (var item in Line1PageViewModel!.FaceAPageViewModel!.SpecificationInfos!)
            {
                switch (item.PName)
                {
                    case "规格上限":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 54.0):F2}";
                        break;
                    case "规格下限":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 48.0):F2}";
                        break;
                    case "控制上限":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 53.0):F2}";
                        break;
                    case "控制下限":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 49.0):F2}";
                        break;
                    default:
                        break;
                }
            }
        }

        protected void L1PageFaceBSpecificationInfoUpateTestByOne()
        {
            foreach (var item in Line1PageViewModel!.FaceBPageViewModel!.SpecificationInfos!)
            {
                switch (item.PName)
                {
                    case "规格上限":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 54.0):F2}";
                        break;
                    case "规格下限":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 48.0):F2}";
                        break;
                    case "控制上限":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 53.0):F2}";
                        break;
                    case "控制下限":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 49.0):F2}";
                        break;
                    default:
                        break;
                }
            }
        }

        protected void L1PageFaceADeviceMonitorInfoUpateTestByOne()
        {
            foreach (var item in Line1PageViewModel!.FaceAPageViewModel!.DeviceMonitorInfos!)
            {
                switch (item.PName)
                {
                    case "平均值":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 299.0):F2}";
                        break;
                    case "实时值":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 299.0):F2}";
                        break;
                    case "温度":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 24.0):F2}";
                        break;
                    case "湿度":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 23.0):F2}";
                        break;
                    case "扫描速度":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 39.0):F2}";
                        break;
                    case "走带速度":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 19.0):F2}";
                        break;
                    case "卷长":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 8000.0):F2}";
                        break;
                    case "分区极差":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 2.5):F2}";
                        break;
                    case "基材宽度":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 938.0):F2}";
                        break;
                    case "外温度":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 24.0):F2}";
                        break;
                    default:
                        break;
                }
            }
        }

        protected void L1PageFaceBDeviceMonitorInfoUpateTestByOne()
        {
            foreach (var item in Line1PageViewModel!.FaceBPageViewModel!.DeviceMonitorInfos!)
            {
                switch (item.PName)
                {
                    case "平均值":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 299.0):F2}";
                        break;
                    case "实时值":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 299.0):F2}";
                        break;
                    case "温度":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 24.0):F2}";
                        break;
                    case "湿度":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 23.0):F2}";
                        break;
                    case "扫描速度":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 39.0):F2}";
                        break;
                    case "走带速度":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 19.0):F2}";
                        break;
                    case "卷长":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 8000.0):F2}";
                        break;
                    case "分区极差":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 2.5):F2}";
                        break;
                    case "基材宽度":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 938.0):F2}";
                        break;
                    case "外温度":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 24.0):F2}";
                        break;
                    default:
                        break;
                }
            }
        }

        protected void L1PageFaceACCDWidthUpdate()
        {
            foreach (var item in Line1PageViewModel!.FaceAPageViewModel!.RawSegments!)
            {
                if (item.Name.Contains("Tab"))
                {
                    item.WidthA = Math.Round(rnd.NextDouble() * 2.0 + 31.5, 2);
                    item.ForegroundA = GetRandomColor();
                    item.WidthB = Math.Round(rnd.NextDouble() * 2.0 + 31.5, 2);
                    item.ForegroundB = GetRandomColor();
                }
                else if (item.Name.Contains("CIL"))
                {
                    item.WidthA = Math.Round(rnd.NextDouble() * 2.0 + 4.0, 2);
                    item.ForegroundA = GetRandomColor();
                    item.WidthB = Math.Round(rnd.NextDouble() * 2.0 + 4.0, 2);
                    item.ForegroundB = GetRandomColor();
                }
                else if (item.Name.Contains("Coating"))
                {
                    item.WidthA = Math.Round(rnd.NextDouble() * 2.0 + 374.0, 2);
                    item.ForegroundA = GetRandomColor();
                    item.WidthB = Math.Round(rnd.NextDouble() * 2.0 + 374.0, 2);
                    item.ForegroundB = GetRandomColor();
                }
            }
        }

        protected void L1PageFaceBCCDWidthUpdate()
        {
            foreach (var item in Line1PageViewModel!.FaceBPageViewModel!.RawSegments!)
            {
                if (item.Name.Contains("Tab"))
                {
                    item.WidthA = Math.Round(rnd.NextDouble() * 2.0 + 31.5, 2);
                    item.ForegroundA = GetRandomColor();
                    item.WidthB = Math.Round(rnd.NextDouble() * 2.0 + 31.5, 2);
                    item.ForegroundB = GetRandomColor();
                }
                else if (item.Name.Contains("CIL"))
                {
                    item.WidthA = Math.Round(rnd.NextDouble() * 2.0 + 4.0, 2);
                    item.ForegroundA = GetRandomColor();
                    item.WidthB = Math.Round(rnd.NextDouble() * 2.0 + 4.0, 2);
                    item.ForegroundB = GetRandomColor();
                }
                else if (item.Name.Contains("Coating"))
                {
                    item.WidthA = Math.Round(rnd.NextDouble() * 2.0 + 374.0, 2);
                    item.ForegroundA = GetRandomColor();
                    item.WidthB = Math.Round(rnd.NextDouble() * 2.0 + 374.0, 2);
                    item.ForegroundB = GetRandomColor();
                }
            }
        }

        protected void L1PageFaceADryDataUpdateTestByOne()
        {
            Line1PageViewModel!.FaceADryDataTableRows!.RemoveAt(0);
            Line1PageViewModel!.FaceADryDataTableRows!.Add(
                new RowData
                {
                    Cells = new ObservableCollection<CellData>
                    {
                        new CellData
                        {
                            Value = $"{DateTime.Now:HH:mm:ss}",
                            Background = Brushes.Transparent,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                    },
                }
            );
        }

        protected void L1PageFaceBWetDataUpdateTestByOne()
        {
            Line1PageViewModel!.FaceBWetDataTableRows!.RemoveAt(0);
            Line1PageViewModel!.FaceBWetDataTableRows!.Add(
                new RowData
                {
                    Cells = new ObservableCollection<CellData>
                    {
                        new CellData
                        {
                            Value = $"{DateTime.Now:HH:mm:ss}",
                            Background = Brushes.Transparent,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                    },
                }
            );
        }

        protected void L1PageFaceBDryDataUpdateTestByOne()
        {
            Line1PageViewModel!.FaceBDryDataTableRows!.RemoveAt(0);
            Line1PageViewModel!.FaceBDryDataTableRows!.Add(
                new RowData
                {
                    Cells = new ObservableCollection<CellData>
                    {
                        new CellData
                        {
                            Value = $"{DateTime.Now:HH:mm:ss}",
                            Background = Brushes.Transparent,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                    },
                }
            );
        }

        protected void L1PageDeviceInfoUpdateTestByOne()
        {
            Line1PageViewModel!
                .DeviceInfos!.FirstOrDefault(item => item.PName == "涂布实时速度")!
                .PValue = $"{(rnd.NextDouble() * 2.0 + 19.0):F2}";
        }

        protected void L1PageGeneralParameterInfoUpdateTestByOne()
        {
            foreach (var item in Line1PageViewModel!.GeneralParameterInfos!)
            {
                item.PValue = $"{(rnd.NextDouble() * 2.0 + 100.0):F2}";
            }
        }

        protected void L1PageADryAndBWetMistakeDataUpdateTestByOne()
        {
            Line1PageViewModel!.FaceADryAndBWetDataTableRows!.RemoveAt(0);
            Line1PageViewModel!.FaceADryAndBWetDataTableRows!.Add(
                new RowData
                {
                    Cells = new ObservableCollection<CellData>
                    {
                        new CellData
                        {
                            Value = $"{DateTime.Now:HH:mm:ss}",
                            Background = Brushes.Transparent,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                    },
                }
            );
        }

        protected void L1PageADryAndBDryMistakeDataUpdateTestByOne()
        {
            Line1PageViewModel!.FaceADryAndBDryDataTableRows!.RemoveAt(0);
            Line1PageViewModel!.FaceADryAndBDryDataTableRows!.Add(
                new RowData
                {
                    Cells = new ObservableCollection<CellData>
                    {
                        new CellData
                        {
                            Value = $"{DateTime.Now:HH:mm:ss}",
                            Background = Brushes.Transparent,
                            Foreground = Brushes.Black,
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                        new CellData
                        {
                            Value = $"{(rnd.NextDouble() * 25):F2}",
                            Background = GetRandomColor(),
                            Foreground = GetRandomColor(),
                        },
                    },
                }
            );
        }

        protected void L1PageAlarmInfoUpdateTestByOne()
        {
            switch (rnd.Next(0, 3))
            {
                case 0:
                    Line1PageViewModel!.AddAlarmInfo(
                        new AlarmInfo
                        {
                            AlarmTime = DateTime.Now,
                            AlarmObject = $"驱动器{rnd.Next()}",
                            AlarmType = "设备异常停止",
                            AlarmDetail = "这是一条长长长长长长长长长长长长长长详情",
                            AlarmLevel = AlarmLevel.Critical,
                        }
                    );
                    break;
                case 1:
                    Line1PageViewModel!.AddAlarmInfo(
                        new AlarmInfo
                        {
                            AlarmTime = DateTime.Now,
                            AlarmObject = $"驱动器{rnd.Next()}",
                            AlarmType = "自动调节",
                            AlarmDetail = "这是一条长长长长长长长长长长长长长长详情",
                            AlarmLevel = AlarmLevel.Info,
                        }
                    );
                    break;
                default:
                    Line1PageViewModel!.AddAlarmInfo(
                        new AlarmInfo
                        {
                            AlarmTime = DateTime.Now,
                            AlarmObject = $"驱动器{rnd.Next()}",
                            AlarmType = "自动调节超过上限",
                            AlarmDetail = "这是一条长长长长长长长长长长长长长长详情",
                            AlarmLevel = AlarmLevel.Warning,
                        }
                    );
                    break;
            }
        }

        protected void L1PageDataUpateTest()
        {
            while (true)
            {
                L1PageFaceAFilmDataUpateTestByOne();
                L1PageFaceBFilmDataUpateTestByOne();

                L1PageFaceASpecificationInfoUpateTestByOne();
                L1PageFaceBSpecificationInfoUpateTestByOne();

                L1PageFaceADeviceMonitorInfoUpateTestByOne();
                L1PageFaceBDeviceMonitorInfoUpateTestByOne();

                L1PageDeviceInfoUpdateTestByOne();

                L1PageGeneralParameterInfoUpdateTestByOne();

                RunOnUIThread(() =>
                {
                    L1PageFaceACCDWidthUpdate();
                    L1PageFaceBCCDWidthUpdate();

                    L1PageFaceADryDataUpdateTestByOne();

                    L1PageFaceBWetDataUpdateTestByOne();

                    L1PageFaceBDryDataUpdateTestByOne();

                    L1PageADryAndBWetMistakeDataUpdateTestByOne();

                    L1PageADryAndBDryMistakeDataUpdateTestByOne();

                    L1PageAlarmInfoUpdateTestByOne();
                });

                GC.Collect();

                Thread.Sleep(1000);
            }
        }

        private async Task GaugeServer_AlarmTypeJsonParse(JsonElement dataJsonElement)
        {
            await Task.CompletedTask;
        }

        private async Task GaugeServer_DataTypeJsonParse(JsonElement dataJsonElement)
        {
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// 设备TCP服务器
    /// </summary>
    public class DeviceTcpServer
    {
        private readonly ILoggerService _logger;
        private TcpService _service = new();

        public delegate Task ParseJsonEventHandler(JsonElement dataJsonElement);

        public event ParseJsonEventHandler? DataTypeJsonParse;

        public event ParseJsonEventHandler? AlarmTypeJsonParse;

        public DeviceTcpServer(ILoggerService logger)
        {
            _logger = logger;
        }

        public async Task StartAsync(int port = 9000)
        {
            var config = new TouchSocketConfig()
                .SetListenIPHosts(new[] { new IPHost($"0.0.0.0:{port}") })
                // 固定包头：前4字节长度（小端），后接UTF-8 JSON
                .SetTcpDataHandlingAdapter(() => new FixedHeaderPackageAdapter());
            _service.Received += OnReceived;
            _service.Connected += OnConnected;
            _service.Closed += OnClosed;

            await _service.SetupAsync(config);
            await _service.StartAsync();
            _logger.Info($"服务启动在 {port} 端口");
        }

        private async Task OnClosed(TcpSessionClient client, ClosedEventArgs e)
        {
            _logger.Info($"{client.Id} 客户端断开连接");
            await Task.CompletedTask;
        }

        private async Task OnConnected(TcpSessionClient client, ConnectedEventArgs e)
        {
            _logger.Info($"{client.Id} 客户端连接");
            await Task.CompletedTask;
        }

        private async Task OnReceived(TcpSessionClient client, ReceivedDataEventArgs e)
        {
            try
            {
                // 拆到的是“一个完整帧”的 Payload
                var json = Encoding.UTF8.GetString(
                    e.ByteBlock.Memory.ToArray(),
                    0,
                    e.ByteBlock.Length
                );
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string msgType = root.GetProperty("msgType").GetString() ?? "";
                string sid = root.GetProperty("sid").GetString() ?? "";
                long ts = root.GetProperty("ts").GetInt64();
                var z = root.GetProperty("z").GetString();
                var dRoot = GetDataJsonElement(z, root);

                // 可选校验
                if (root.TryGetProperty("sig", out var sigProp))
                {
                    string sig = sigProp.GetString() ?? "";

                    if (!VerifyCrc32(dRoot, sig))
                    {
                        await SendAsync(
                            client,
                            new
                            {
                                msgType = "err",
                                code = 1003,
                                msg = "bad crc",
                                ts = NowMs(),
                            }
                        );
                        return;
                    }
                }

                switch (msgType)
                {
                    case "hb":
                        // 心跳：可记录在线/状态
                        // 可选响应
                        await SendAsync(client, new { msgType = "pong", ts = NowMs() });
                        break;
                    case "data":
                        if (DataTypeJsonParse != null)
                            await DataTypeJsonParse.Invoke(dRoot);
                        //var single_wet_mm = dRoot
                        //    .GetProperty("single_wet_mm")
                        //    .EnumerateArray()
                        //    .Select(v => v.GetDouble());
                        //_logger.Info($"{JsonSerializer.Serialize(dRoot)}");

                        // 可选响应
                        await SendAsync(client, new { msgType = "ok", ts = NowMs() });
                        break;
                    case "alarm":
                        if (AlarmTypeJsonParse != null)
                            await AlarmTypeJsonParse.Invoke(dRoot);
                        //var level = dRoot.GetProperty("level").GetString();
                        //var info = dRoot.GetProperty("info").GetString();
                        // 可选响应
                        await SendAsync(client, new { msgType = "ok", ts = NowMs() });
                        break;
                    default:
                        await SendAsync(
                            client,
                            new
                            {
                                msgType = "err",
                                code = 1002,
                                msg = "unknown type",
                                ts = NowMs(),
                            }
                        );
                        break;
                }
            }
            catch (Exception ex)
            {
                await SendAsync(
                    client,
                    new
                    {
                        msgType = "err",
                        code = 1999,
                        msg = ex.Message,
                        ts = NowMs(),
                    }
                );
            }
        }

        private static JsonElement GetDataJsonElement(string? z, JsonElement root)
        {
            if (z == "gzip")
            {
                byte[] gz = Convert.FromBase64String(root.GetProperty("data").GetString()!);
                byte[] raw = GzipDecompress(gz);
                string dJson = Encoding.UTF8.GetString(raw);
                return JsonDocument.Parse(dJson).RootElement;
            }
            else
            {
                return root.GetProperty("data");
            }
        }

        private static async Task SendAsync(TcpSessionClient client, object obj)
        {
            var json = JsonSerializer.Serialize(obj);
            var data = Encoding.UTF8.GetBytes(json);
            // 使用 FixedHeaderPackageAdapter：WriteAsync 会自动带4字节长度头
            await client.SendAsync(client.Id, data);
        }

        private static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>
        /// 对 data 对象做最小化JSON后CRC32校验，sig格式："crc32:XXXXXXXX"
        /// </summary>
        /// <param name="d"></param>
        /// <param name="sig"></param>
        /// <returns></returns>
        private static bool VerifyCrc32(JsonElement d, string sig)
        {
            if (!sig.StartsWith("crc32:", StringComparison.OrdinalIgnoreCase))
                return false;
            var hex = sig.AsSpan(6);
            if (hex.Length != 8)
                return false;

            // 最小化 JSON：System.Text.Json 默认序列化已无多余空格
            string dJson = JsonSerializer.Serialize(d);
            uint crc = Crc32(Encoding.UTF8.GetBytes(dJson));
            Span<char> calc = stackalloc char[8];
            ToHex8Upper(crc, calc);
            ReadOnlySpan<char> calcRead = calc;
            return calcRead.SequenceEqual(hex, new CaseInsensitiveCharComparer());
        }

        // 轻量CRC32(IEEE)
        private static uint Crc32(byte[] data)
        {
            const uint poly = 0xEDB88320u;
            uint crc = 0xFFFFFFFFu;
            for (int i = 0; i < data.Length; i++)
            {
                crc ^= data[i];
                for (int j = 0; j < 8; j++)
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ poly : (crc >> 1);
            }
            return ~crc;
        }

        private static void ToHex8Upper(uint v, Span<char> dest)
        {
            const string Hex = "0123456789ABCDEF";
            for (int i = 7; i >= 0; i--)
            {
                dest[7 - i] = Hex[(int)((v >> (i * 4)) & 0xF)];
            }
        }

        private static byte[] GzipCompress(byte[] raw)
        {
            using var ms = new MemoryStream();
            using (var gz = new GZipStream(ms, CompressionLevel.Fastest, true))
                gz.Write(raw, 0, raw.Length);
            return ms.ToArray();
        }

        private static byte[] GzipDecompress(byte[] gzBytes)
        {
            using var inMs = new MemoryStream(gzBytes);
            using var gz = new GZipStream(inMs, CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            gz.CopyTo(outMs);
            return outMs.ToArray();
        }
    }

    public class CaseInsensitiveCharComparer : IEqualityComparer<char>
    {
        public bool Equals(char x, char y)
        {
            // 使用大小写不敏感的比较
            return char.ToUpperInvariant(x) == char.ToUpperInvariant(y);
        }

        public int GetHashCode(char obj)
        {
            // 保证大小写一致性，统一转换再取哈希
            return char.ToUpperInvariant(obj).GetHashCode();
        }
    }

    public sealed class PLCConfigRowMap : ClassMap<PLCConfigModel>
    {
        public PLCConfigRowMap()
        {
            Map(m => m.Name).Name("name");
            Map(m => m.DeviceID).Name("device_id");
            Map(m => m.IP).Name("ip");
            Map(m => m.Port).Name("port");
            Map(m => m.BrandSpecificationProtocal).Name("brand_specification_protocal");
            Map(m => m.DataSortRule).Name("data_sort_rule");
            Map(m => m.CycleReadTime).Name("cycle_read_time");
            Map(m => m.CycleWriteTime).Name("cycle_write_time");
        }
    }

    public sealed class PLCRWConfigRowMap : ClassMap<PLCRWConfigModel>
    {
        public PLCRWConfigRowMap()
        {
            Map(m => m.Name).Name("name");
            Map(m => m.PLCID).Name("plc_id");
            Map(m => m.AreaName).Name("area_name");
            Map(m => m.StartAddress).Name("start_address");
            Map(m => m.Length).Name("length");
            Map(m => m.RWMode).Name("rw_mode");
            Map(m => m.Cycle).Name("cycle");
            Map(m => m.AddressType).Name("address_type");
        }
    }
}
