using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using CsvHelper;
using CsvHelper.Configuration;
using HandyControl.Controls;
using HandyControl.Tools.Extension;
using SUNWODA_SEVB.Communication.CoatingOPMM;
using SUNWODA_SEVB.Communication.Common;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Enumerations.Component;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Models;
using SUNWODA_SEVB.Core.Models.Component;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.PLC;
using SUNWODA_SEVB.Core.Models.Project.CoatingOPMM;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.ViewModels.Dialogs.CoatingOPMM;
using TouchSocket.Core;

namespace SUNWODA_SEVB.ViewModels.Pages.CoatingOPMM
{
    [Module("CoatingOPMM", "涂布工序监测系统")]
    public class VM_CoatingOPMMPage : ViewModelBase
    {
        private readonly ILoggerService<VM_CoatingOPMMPage> _logger;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IPLCRWConfigRepository _plcRWConfigRepository;
        private readonly IPLCAddressConfigRepository _plcAddressConfigRepository;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IPLCService _plcService;
        private FrameworkElement? line1Page;
        private FrameworkElement? line2Page;
        private Random rnd = new Random();
        public ObservableCollection<ColumnHeader> FaceAColumns { get; set; } =
            new ObservableCollection<ColumnHeader>()
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

        public ObservableCollection<ColumnHeader> FaceBColumns { get; set; } =
            new ObservableCollection<ColumnHeader>()
            {
                new ColumnHeader { Name = "时间" },
                new ColumnHeader { Name = "B面极耳1" },
                new ColumnHeader { Name = "B面陶瓷1" },
                new ColumnHeader { Name = "B面碳膜1" },
                new ColumnHeader { Name = "B面陶瓷2" },
                new ColumnHeader { Name = "B面极耳2" },
                new ColumnHeader { Name = "B面陶瓷3" },
                new ColumnHeader { Name = "B面碳膜2" },
                new ColumnHeader { Name = "B面陶瓷4" },
                new ColumnHeader { Name = "B面极耳3" },
                new ColumnHeader { Name = "B面陶瓷5" },
                new ColumnHeader { Name = "B面碳膜3" },
                new ColumnHeader { Name = "B面陶瓷6" },
                new ColumnHeader { Name = "B面极耳4" },
                new ColumnHeader { Name = "B面陶瓷7" },
                new ColumnHeader { Name = "B面碳膜4" },
                new ColumnHeader { Name = "B面陶瓷8" },
                new ColumnHeader { Name = "B面极耳5" },
            };

        public ObservableCollection<ColumnHeader> MisAlignColumns { get; set; } =
            new ObservableCollection<ColumnHeader>
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

        public bool IsInitPageData { get; set; } = false;

        public VM_CoatingOPMMPage(
            ILoggerService<VM_CoatingOPMMPage> logger,
            IPLCConfigRepository plcConfigRepository,
            IPLCRWConfigRepository plcRWConfigRepository,
            IPLCAddressConfigRepository plcAddressConfigRepository,
            IProjectSettingRepository projectSettingRepository,
            IPLCService plcService
        )
        {
            _logger = logger;
            _plcConfigRepository = plcConfigRepository;
            _plcRWConfigRepository = plcRWConfigRepository;
            _plcAddressConfigRepository = plcAddressConfigRepository;
            _projectSettingRepository = projectSettingRepository;
            _plcService = plcService;
        }

        private void InitPLCConfig()
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
                if (
                    !_plcRWConfigRepository.Exists(model =>
                        model.Name == item.Name && model.PLCID == item.PLCID
                    )
                )
                    _plcRWConfigRepository.Add(item);
            });

            using var plcAddressConfigReader = new StreamReader("./plc_address_config.csv");
            using var plcAddressConfigCsv = new CsvReader(plcAddressConfigReader, config);
            plcAddressConfigCsv.Context.RegisterClassMap<PLCAddressConfigRowMap>();

            var plcAddressConfigList = plcAddressConfigCsv
                .GetRecords<PLCAddressConfigModel>()
                .ToList();
            plcAddressConfigList.ForEach(item =>
            {
                item.IsMonitor = true;
                if (
                    !_plcAddressConfigRepository.Exists(model =>
                        model.ParameterName == item.ParameterName && model.PLCID == item.PLCID
                    )
                )
                    _plcAddressConfigRepository.Add(item);
            });

            _plcService
                .RWAddresses.Where(address =>
                    address.Value.CategoryId == 3 && address.Value.PlcId == 1
                )
                .Select(address => address.Value)
                .ForEach(item =>
                {
                    Line1PageViewModel!.OldPLCAddressMonitor.Add(
                        new PLCRWAddress(
                            item.ID,
                            item.PlcId,
                            item.AddressRangeId,
                            item.CategoryId,
                            item.ParameterName ?? "",
                            item.Type ?? "",
                            item.Length,
                            item.Address ?? "",
                            item.Unit ?? "",
                            item.Remark ?? "",
                            item.Index,
                            item.BitIndex
                        )
                        {
                            MonitorValue = item.MonitorValue,
                        }
                    );
                });

            _plcService
                .RWAddresses.Where(address =>
                    address.Value.CategoryId == 3 && address.Value.PlcId == 2
                )
                .Select(address => address.Value)
                .ForEach(item =>
                {
                    Line2PageViewModel!.OldPLCAddressMonitor.Add(
                        new PLCRWAddress(
                            item.ID,
                            item.PlcId,
                            item.AddressRangeId,
                            item.CategoryId,
                            item.ParameterName ?? "",
                            item.Type ?? "",
                            item.Length,
                            item.Address ?? "",
                            item.Unit ?? "",
                            item.Remark ?? "",
                            item.Index,
                            item.BitIndex
                        )
                        {
                            MonitorValue = item.MonitorValue,
                        }
                    );
                });
        }

        private void InitProjectSettings()
        {
            if (!_projectSettingRepository.Exists(model => model.Name == "IsBigScreen"))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel("IsBigScreen", "VM_CoatingOPMMPage", "True", "bool", 2)
                );
            }
            if (!_projectSettingRepository.Exists(model => model.Name == "L1MESClientDataPath"))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        "L1MESClientDataPath",
                        "VM_CoatingOPMMPage",
                        "D:/MESClientData.txt",
                        "string",
                        2
                    )
                );
            }
            if (!_projectSettingRepository.Exists(model => model.Name == "L2MESClientDataPath"))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        "L2MESClientDataPath",
                        "VM_CoatingOPMMPage",
                        "D:/MESClientData.txt",
                        "string",
                        2
                    )
                );
            }

            if (
                _projectSettingRepository.GetSettingValue("IsBigScreen", "VM_CoatingOPMMPage")
                != true
            )
            {
                Line1PageViewModel!.MainFontSize = 6;
                Line1PageViewModel!.LineTitleFontSize = 30;
                Line1PageViewModel.LittleTitleFontSize = 8;
                Line1PageViewModel.ParameterVerticalSpacing = 10;

                Line1PageViewModel!.FaceAPageViewModel!.MainFontSize = 6;
                Line1PageViewModel.FaceAPageViewModel!.LittleTitleFontSize = 8;
                Line1PageViewModel.FaceAPageViewModel!.ParameterVerticalSpacing = 40;

                Line1PageViewModel!.FaceBPageViewModel!.MainFontSize = 6;
                Line1PageViewModel.FaceBPageViewModel!.LittleTitleFontSize = 8;
                Line1PageViewModel.FaceBPageViewModel!.ParameterVerticalSpacing = 40;

                Line2PageViewModel!.MainFontSize = 6;
                Line2PageViewModel!.LineTitleFontSize = 30;
                Line2PageViewModel.LittleTitleFontSize = 8;
                Line2PageViewModel.ParameterVerticalSpacing = 10;

                Line2PageViewModel!.FaceAPageViewModel!.MainFontSize = 6;
                Line2PageViewModel.FaceAPageViewModel!.LittleTitleFontSize = 8;
                Line2PageViewModel.FaceAPageViewModel!.ParameterVerticalSpacing = 40;

                Line2PageViewModel!.FaceBPageViewModel!.MainFontSize = 6;
                Line2PageViewModel.FaceBPageViewModel!.LittleTitleFontSize = 8;
                Line2PageViewModel.FaceBPageViewModel!.ParameterVerticalSpacing = 40;
            }
        }

        protected override async Task OnNavigatedToAsync(object? parameter)
        {
            if (!IsInitPageData)
            {
                await InitPageData1();

                //Thread thread1 = new Thread(L1PageDataUpate);
                //thread1.Start();

                //Thread thread2 = new Thread(L2PageDataUpate);
                //thread2.Start();

                IsInitPageData = true;
            }

            //InitPLCConfig();
            InitProjectSettings();

            Line1PageViewModel!.StartFileWatcher(
                $"{_projectSettingRepository.GetSettingValue("L1MESClientDataPath", "VM_CoatingOPMMPage")}"
            );

            Line2PageViewModel!.StartFileWatcher(
                $"{_projectSettingRepository.GetSettingValue("L2MESClientDataPath", "VM_CoatingOPMMPage")}"
            );

            await base.OnNavigatedToAsync(parameter);
        }

        protected override async Task OnInitializeAsync()
        {
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
                    Status = false,
                };
                Line2Page = Line2PageViewModel.UIElement;
            });

            //await InitPageData1();
            await InitPageData2();
            await base.OnInitializeAsync();
        }

        private async Task InitPageData1()
        {
            await RunOnUIThreadAsync(() =>
            {
                Line1PageViewModel!.FaceAPageViewModel!.RawSegments =
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

            await RunOnUIThreadAsync(() =>
            {
                Line2PageViewModel!.FaceAPageViewModel!.RawSegments =
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

                Line2PageViewModel.FaceBPageViewModel!.RawSegments =
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
        }

        private async Task InitPageData2()
        {
            Line1PageViewModel!.FaceADryDataTableColumns = FaceAColumns;

            Line1PageViewModel!.FaceADryDataTableRows = new ObservableCollection<RowData>();

            Line1PageViewModel!.FaceBWetDataTableColumns = FaceBColumns;

            Line1PageViewModel!.FaceBWetDataTableRows = new ObservableCollection<RowData>();

            Line1PageViewModel!.FaceBDryDataTableColumns = FaceBColumns;

            Line1PageViewModel!.FaceBDryDataTableRows = new ObservableCollection<RowData>();

            Line1PageViewModel!.DeviceInfos = new ObservableCollection<ParameterModel>()
            {
                new ParameterModel("拉线名", ""),
                new ParameterModel("涂布实时速度", "0.0"),
                new ParameterModel("设备IP", "127.0.0.1"),
                new ParameterModel("产品批号", ""),
                new ParameterModel("设备编号", "#001"),
                new ParameterModel("膜卷号", ""),
            };

            Line1PageViewModel.GeneralParameterInfos = new ObservableCollection<ParameterModel>()
            {
                new ParameterModel("放卷A轴卷径", "0.0"),
                new ParameterModel("放卷B轴卷径", "0.0"),
                new ParameterModel("机头B左实时GAP", "0.0"),
                new ParameterModel("机头B右实时GAP", "0.0"),
                new ParameterModel("机头B浆料缓存罐液位R", "0.0"),
                new ParameterModel("机头B绝缘浆料缓存罐液位R", "0.0"),
                new ParameterModel("机头A浆料泵实时泵速3", "0.0"),
                new ParameterModel("机头A浆料泵实时泵速4", "0.0"),
                new ParameterModel("收卷A轴卷径", "0.0"),
                new ParameterModel("收卷B轴卷径", "0.0"),
                new ParameterModel("机头A左实时GAP", "0.0"),
                new ParameterModel("机头A右实时GAP", "0.0"),
                new ParameterModel("机头A浆料缓存罐液位R", "0.0"),
                new ParameterModel("机头A绝缘浆料缓存罐液位R", "0.0"),
                new ParameterModel("机头B浆料泵实时泵速1", "0.0"),
                new ParameterModel("机头B浆料泵实时泵速2", "0.0"),
                new ParameterModel("A面实际涂布米数", "0.0"),
                new ParameterModel("B面实际涂布米数", "0.0"),
                new ParameterModel("系统正转速度", "0.0"),
                new ParameterModel("系统运行实时速度", "0.0"),
                new ParameterModel("机头A浆料泵实时泵速1", "0.0"),
                new ParameterModel("机头A浆料泵实时泵速2", "0.0"),
                new ParameterModel("机头B浆料泵实时泵速3", "0.0"),
                new ParameterModel("机头B浆料泵实时泵速4", "0.0"),
            };

            Line1PageViewModel.FaceADryAndBWetDataTableColumns = MisAlignColumns;

            Line1PageViewModel.FaceADryAndBWetDataTableRows = new ObservableCollection<RowData>();

            Line1PageViewModel.FaceADryAndBDryDataTableColumns = MisAlignColumns;

            Line1PageViewModel.FaceADryAndBDryDataTableRows = new ObservableCollection<RowData>();

            Line1PageViewModel.FaceAPageViewModel!.SpecificationInfos =
                new ObservableCollection<ParameterModel>()
                {
                    new ParameterModel("规格上限", "0.0"),
                    new ParameterModel("规格下限", "0.0"),
                    new ParameterModel("控制上限", "0.0"),
                    new ParameterModel("控制下限", "0.0"),
                };

            Line1PageViewModel.FaceAPageViewModel!.DeviceMonitorInfos =
                new ObservableCollection<ParameterModel>()
                {
                    new ParameterModel("平均值", "0.0"),
                    new ParameterModel("标准差", "0.0"),
                    new ParameterModel("CPK", "0.0"),
                    new ParameterModel("COV", "0.0"),
                    new ParameterModel("卷长", "0.0"),
                };

            Line1PageViewModel.FaceBPageViewModel!.SpecificationInfos =
                new ObservableCollection<ParameterModel>()
                {
                    new ParameterModel("规格上限", "0.0"),
                    new ParameterModel("规格下限", "0.0"),
                    new ParameterModel("控制上限", "0.0"),
                    new ParameterModel("控制下限", "0.0"),
                };

            Line1PageViewModel.FaceBPageViewModel!.DeviceMonitorInfos =
                new ObservableCollection<ParameterModel>()
                {
                    new ParameterModel("平均值", "0.0"),
                    new ParameterModel("标准差", "0.0"),
                    new ParameterModel("CPK", "0.0"),
                    new ParameterModel("COV", "0.0"),
                    new ParameterModel("卷长", "0.0"),
                };

            Line1PageViewModel.FaceAPageViewModel!.FilmData = new List<double[]>
            {
                ([0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0]),
                ([0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0]),
                ([0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0]),
                ([0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0]),
            };

            Line1PageViewModel.FaceBPageViewModel!.FilmData = new List<double[]>
            {
                ([0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0]),
                ([0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0]),
                ([0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0]),
                ([0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0]),
            };

            Line2PageViewModel!.FaceADryDataTableColumns = FaceAColumns;

            Line2PageViewModel!.FaceADryDataTableRows = new ObservableCollection<RowData>();

            Line2PageViewModel!.FaceBWetDataTableColumns = FaceBColumns;

            Line2PageViewModel!.FaceBWetDataTableRows = new ObservableCollection<RowData>();

            Line2PageViewModel!.FaceBDryDataTableColumns = FaceBColumns;

            Line2PageViewModel!.FaceBDryDataTableRows = new ObservableCollection<RowData>();

            Line2PageViewModel!.DeviceInfos = new ObservableCollection<ParameterModel>()
            {
                new ParameterModel("拉线名", ""),
                new ParameterModel("涂布实时速度", "0"),
                new ParameterModel("设备IP", "127.0.0.1"),
                new ParameterModel("产品批号", ""),
                new ParameterModel("设备编号", "#002"),
                new ParameterModel("膜卷号", ""),
            };

            Line2PageViewModel.GeneralParameterInfos = new ObservableCollection<ParameterModel>()
            {
                new ParameterModel("放卷A轴卷径", "0.0"),
                new ParameterModel("放卷B轴卷径", "0.0"),
                new ParameterModel("机头B左实时GAP", "0.0"),
                new ParameterModel("机头B右实时GAP", "0.0"),
                new ParameterModel("机头B浆料缓存罐液位R", "0.0"),
                new ParameterModel("机头B绝缘浆料缓存罐液位R", "0.0"),
                new ParameterModel("机头A浆料泵实时泵速3", "0.0"),
                new ParameterModel("机头A浆料泵实时泵速4", "0.0"),
                new ParameterModel("收卷A轴卷径", "0.0"),
                new ParameterModel("收卷B轴卷径", "0.0"),
                new ParameterModel("机头A左实时GAP", "0.0"),
                new ParameterModel("机头A右实时GAP", "0.0"),
                new ParameterModel("机头A浆料缓存罐液位R", "0.0"),
                new ParameterModel("机头A绝缘浆料缓存罐液位R", "0.0"),
                new ParameterModel("机头B浆料泵实时泵速1", "0.0"),
                new ParameterModel("机头B浆料泵实时泵速2", "0.0"),
                new ParameterModel("A面实际涂布米数", "0.0"),
                new ParameterModel("B面实际涂布米数", "0.0"),
                new ParameterModel("系统正转速度", "0.0"),
                new ParameterModel("系统运行实时速度", "0.0"),
                new ParameterModel("机头A浆料泵实时泵速1", "0.0"),
                new ParameterModel("机头A浆料泵实时泵速2", "0.0"),
                new ParameterModel("机头B浆料泵实时泵速3", "0.0"),
                new ParameterModel("机头B浆料泵实时泵速4", "0.0"),
            };

            Line2PageViewModel!.FaceADryAndBWetDataTableColumns = MisAlignColumns;

            Line2PageViewModel!.FaceADryAndBWetDataTableRows = new ObservableCollection<RowData>();

            Line2PageViewModel!.FaceADryAndBDryDataTableColumns = MisAlignColumns;

            Line2PageViewModel!.FaceADryAndBDryDataTableRows = new ObservableCollection<RowData>();

            Line2PageViewModel.FaceAPageViewModel!.SpecificationInfos =
                new ObservableCollection<ParameterModel>()
                {
                    new ParameterModel("规格上限", "0.0"),
                    new ParameterModel("规格下限", "0.0"),
                    new ParameterModel("控制上限", "0.0"),
                    new ParameterModel("控制下限", "0.0"),
                };

            Line2PageViewModel.FaceAPageViewModel!.DeviceMonitorInfos =
                new ObservableCollection<ParameterModel>()
                {
                    new ParameterModel("平均值", "0.0"),
                    new ParameterModel("标准差", "0.0"),
                    new ParameterModel("CPK", "0.0"),
                    new ParameterModel("COV", "0.0"),
                    new ParameterModel("卷长", "0.0"),
                };

            Line2PageViewModel.FaceBPageViewModel!.SpecificationInfos =
                new ObservableCollection<ParameterModel>()
                {
                    new ParameterModel("规格上限", "0.0"),
                    new ParameterModel("规格下限", "0.0"),
                    new ParameterModel("控制上限", "0.0"),
                    new ParameterModel("控制下限", "0.0"),
                };

            Line2PageViewModel.FaceBPageViewModel!.DeviceMonitorInfos =
                new ObservableCollection<ParameterModel>()
                {
                    new ParameterModel("平均值", "0.0"),
                    new ParameterModel("标准差", "0.0"),
                    new ParameterModel("CPK", "0.0"),
                    new ParameterModel("COV", "0.0"),
                    new ParameterModel("卷长", "0.0"),
                };

            Line2PageViewModel.FaceAPageViewModel!.FilmData = new List<double[]>
            {
                ([0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0]),
                ([0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0]),
                ([0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0]),
                ([0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0]),
            };

            Line2PageViewModel.FaceBPageViewModel!.FilmData = new List<double[]>
            {
                ([0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0]),
                ([0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0]),
                ([0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0]),
                ([0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0]),
            };

            var L1WeightGaugeServer = new WeightGaugeDeviceTcpServer(_logger);
            L1WeightGaugeServer.WeightMessageParseSEvent +=
                L1WeightGaugeServer_WeightMessageParseSEvent;
            await L1WeightGaugeServer.StartAsync(9000);

            var L1CCDGaugeServer = new DeviceTcpServer(_logger);
            L1CCDGaugeServer.DeviceName = "L1 CCD";
            L1CCDGaugeServer.DataTypeJsonParse += L1CCDGaugeServer_DataTypeJsonParse;
            L1CCDGaugeServer.AlarmTypeJsonParse += L1CCDGaugeServer_AlarmTypeJsonParse;
            await L1CCDGaugeServer.StartAsync(9001);

            var L1PLCServer = new DeviceTcpServer(_logger);
            L1PLCServer.DeviceName = "L1 PLC";
            L1PLCServer.DataTypeJsonParse += L1PLCServer_DataTypeJsonParse;
            L1PLCServer.ConnectedEvent += () => { Line1PageViewModel!.Status = true; };
            L1PLCServer.ClosedEvent += () => { Line1PageViewModel!.Status = false; };
            await L1PLCServer.StartAsync(9002);

            var L2WeightGaugeServer = new WeightGaugeDeviceTcpServer(_logger);
            L2WeightGaugeServer.WeightMessageParseSEvent +=
                L2WeightGaugeServer_WeightMessageParseSEvent;
            await L2WeightGaugeServer.StartAsync(9010);

            var L2CCDGaugeServer = new DeviceTcpServer(_logger);
            L2CCDGaugeServer.DeviceName = "L2 CCD";
            L2CCDGaugeServer.DataTypeJsonParse += L2CCDGaugeServer_DataTypeJsonParse;
            L2CCDGaugeServer.AlarmTypeJsonParse += L2CCDGaugeServer_AlarmTypeJsonParse;
            await L2CCDGaugeServer.StartAsync(9011);

            var L2PLCServer = new DeviceTcpServer(_logger);
            L2PLCServer.DeviceName = "L2 PLC";
            L2PLCServer.DataTypeJsonParse += L2PLCServer_DataTypeJsonParse;
            L2PLCServer.ConnectedEvent += () => { Line2PageViewModel!.Status = true; };
            L2PLCServer.ClosedEvent += () => { Line2PageViewModel!.Status = false; };
            await L2PLCServer.StartAsync(9012);
        }

        private Brush GetRandomColor()
        {
            byte r = (byte)rnd.Next(0, 256); // 生成0-255之间的随机数
            byte g = (byte)rnd.Next(0, 256); // 生成0-255之间的随机数
            byte b = (byte)rnd.Next(0, 256); // 生成0-255之间的随机数

            //int num = rnd.Next(0, 3);

            //switch (num)
            //{
            //    case 0:
            //        return new SolidColorBrush(Color.FromRgb(41, 204, 122));
            //    case 1:
            //        return new SolidColorBrush(Color.FromRgb(255, 184, 51));
            //    case 2:
            //        return new SolidColorBrush(Color.FromRgb(255, 51, 51));
            //    default:
            //        return new SolidColorBrush(Color.FromRgb(41, 204, 122));
            //}

            return new SolidColorBrush(Color.FromRgb(r, g, b)); // 返回一个随机的SolidColorBrush
        }

        protected void L1PageStatusUpdateByOne()
        {
            Line1PageViewModel!.Status = _plcService.ConnectionStatus["L1涂布机PLC"].Status;
        }

        protected void L2PageStatusUpdateByOne()
        {
            Line2PageViewModel!.Status = _plcService.ConnectionStatus["L2涂布机PLC"].Status;
        }

        #region L1页面更新
        #region 测重仪数据更新
        #region A面
        #region 分区数据
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

        protected void L1PageFaceAFilmDataUpateByOne(
            List<TBAreaInfo> tbAreaInfos,
            List<double> weight,
            double baseValue,
            double warnLow,
            double warnHigh,
            double specLow,
            double specHigh
        )
        {
            Line1PageViewModel!.FaceAPageViewModel!.ZoneBaseValue = baseValue;
            Line1PageViewModel!.FaceAPageViewModel!.RefValue = new Dictionary<string, double>
            {
                { "dangerMaxLimit", specHigh - baseValue },
                { "dangerMinLimit", specLow - baseValue },
                { "centerValue", 0.0 },
                { "warnMaxLimit", warnHigh - baseValue },
                { "warnMinLimit", warnLow - baseValue },
            };

            var data = new List<double[]>();
            var count = 0;
            for (int i = 0; i < tbAreaInfos.Count; i++)
            {
                var arr = new double[tbAreaInfos[i].AreaCount];
                for (int j = 0; j < tbAreaInfos[i].AreaCount; j++)
                {
                    arr[j] = weight[count] - baseValue;
                    count++;
                }
                data.Add(arr);
            }
            Line1PageViewModel!.FaceAPageViewModel!.FilmData = data;
        }
        #endregion

        #region 毫米数据
        protected void L1PageFaceAMMDataUpateTestByOne()
        {
            var x = new List<double>();
            var y = new List<double>();
            for (int i = 0; i < 400; i++)
            {
                x.Add(i);
                double trend = 0.35 * Math.Sin(i * 0.05) + 0.15 * Math.Sin(i * 0.2);
                double noise = (rnd.NextDouble() - 0.5) * 0.5;
                y.Add(300.0 + trend + noise);
            }
            Line1PageViewModel!.FaceAPageViewModel!.X = x;
            Line1PageViewModel!.FaceAPageViewModel!.Y = y;
        }

        protected void L1PageFaceAMMDataUpateByOne(
            List<double> mmData,
            double baseValue,
            double warnLow,
            double warnHigh,
            double specLow,
            double specHigh
        )
        {
            Line1PageViewModel!.FaceAPageViewModel!.MMBaseline = baseValue;
            Line1PageViewModel!.FaceAPageViewModel!.WarnLow = warnLow;
            Line1PageViewModel!.FaceAPageViewModel!.WarnHigh = warnHigh;
            Line1PageViewModel!.FaceAPageViewModel!.SpecLow = specLow;
            Line1PageViewModel!.FaceAPageViewModel!.SpecHigh = specHigh;

            var x = new List<double>();
            for (int i = 0; i < mmData.Count; i++)
            {
                x.Add(i);
            }
            Line1PageViewModel!.FaceAPageViewModel!.X = x;
            Line1PageViewModel!.FaceAPageViewModel!.Y = mmData;
        }
        #endregion

        #region 规格信息
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

        protected void L1PageFaceASpecificationInfoUpateByOne(
            string? netWeightTargetUpper,
            string? netWeightTargetLower,
            string? netWeightTargetWarnUpper,
            string? netWeightTargetWarnLower
        )
        {
            foreach (var item in Line1PageViewModel!.FaceAPageViewModel!.SpecificationInfos!)
            {
                switch (item.PName)
                {
                    case "规格上限":
                        item.PValue = netWeightTargetUpper;
                        break;
                    case "规格下限":
                        item.PValue = netWeightTargetLower;
                        break;
                    case "控制上限":
                        item.PValue = netWeightTargetWarnUpper;
                        break;
                    case "控制下限":
                        item.PValue = netWeightTargetWarnLower;
                        break;
                    default:
                        break;
                }
            }
        }
        #endregion

        #region 设备监控
        protected void L1PageFaceADeviceMonitorInfoUpateTestByOne()
        {
            foreach (var item in Line1PageViewModel!.FaceAPageViewModel!.DeviceMonitorInfos!)
            {
                switch (item.PName)
                {
                    case "平均值":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 299.0):F2}";
                        break;
                    case "标准差":
                        item.PValue = $"{(rnd.NextDouble() * 0.1 + 0.3):F2}";
                        break;
                    case "CPK":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 3.6):F2}";
                        break;
                    case "COV":
                        item.PValue = $"{(rnd.NextDouble() * 0.1 + 0.3):F2}";
                        break;
                    case "卷长":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 8000.0):F2}";
                        break;
                    default:
                        break;
                }
            }
        }

        protected void L1PageFaceADeviceMonitorInfoUpateWeightByOne(
            string? mean,
            string? std,
            string? cpk,
            string? cov,
            string? volLength
        )
        {
            foreach (var item in Line1PageViewModel!.FaceAPageViewModel!.DeviceMonitorInfos!)
            {
                switch (item.PName)
                {
                    case "平均值":
                        item.PValue = mean;
                        break;
                    case "标准差":
                        item.PValue = std;
                        break;
                    case "CPK":
                        item.PValue = cpk;
                        break;
                    case "COV":
                        item.PValue = cov;
                        break;
                    case "卷长":
                        item.PValue = volLength;
                        break;
                    default:
                        break;
                }
            }
        }
        #endregion
        #endregion

        #region B面
        #region 分区数据
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

        protected void L1PageFaceBFilmDataUpateByOne(
            List<TBAreaInfo> tbAreaInfos,
            List<double> weight,
            double baseValue,
            double warnLow,
            double warnHigh,
            double specLow,
            double specHigh
        )
        {
            Line1PageViewModel!.FaceBPageViewModel!.ZoneBaseValue = baseValue;
            Line1PageViewModel!.FaceBPageViewModel!.RefValue = new Dictionary<string, double>
            {
                { "dangerMaxLimit", specHigh - baseValue },
                { "dangerMinLimit", specLow - baseValue },
                { "centerValue", 0.0 },
                { "warnMaxLimit", warnHigh - baseValue },
                { "warnMinLimit", warnLow - baseValue },
            };

            var data = new List<double[]>();
            var count = 0;
            for (int i = 0; i < tbAreaInfos.Count; i++)
            {
                var arr = new double[tbAreaInfos[i].AreaCount];
                for (int j = 0; j < tbAreaInfos[i].AreaCount; j++)
                {
                    arr[j] = weight[count] - baseValue;
                    count++;
                }
                data.Add(arr);
            }
            Line1PageViewModel!.FaceBPageViewModel!.FilmData = data;
        }
        #endregion

        #region 毫米数据
        protected void L1PageFaceBMMDataUpateTestByOne()
        {
            var x = new List<double>();
            var y = new List<double>();
            for (int i = 0; i < 400; i++)
            {
                x.Add(i);
                double trend = 0.35 * Math.Sin(i * 0.05) + 0.15 * Math.Sin(i * 0.2);
                double noise = (rnd.NextDouble() - 0.5) * 0.5;
                y.Add(300.0 + trend + noise);
            }
            Line1PageViewModel!.FaceBPageViewModel!.X = x;
            Line1PageViewModel!.FaceBPageViewModel!.Y = y;
        }

        protected void L1PageFaceBMMDataUpateByOne(
            List<double> mmData,
            double baseValue,
            double warnLow,
            double warnHigh,
            double specLow,
            double specHigh
        )
        {
            Line1PageViewModel!.FaceBPageViewModel!.MMBaseline = baseValue;
            Line1PageViewModel!.FaceBPageViewModel!.WarnLow = warnLow;
            Line1PageViewModel!.FaceBPageViewModel!.WarnHigh = warnHigh;
            Line1PageViewModel!.FaceBPageViewModel!.SpecLow = specLow;
            Line1PageViewModel!.FaceBPageViewModel!.SpecHigh = specHigh;

            var x = new List<double>();
            for (int i = 0; i < mmData.Count; i++)
            {
                x.Add(i);
            }
            Line1PageViewModel!.FaceBPageViewModel!.X = x;
            Line1PageViewModel!.FaceBPageViewModel!.Y = mmData;
        }
        #endregion

        #region 规格信息
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

        protected void L1PageFaceBSpecificationInfoUpateByOne(
            string? netWeightTargetUpper,
            string? netWeightTargetLower,
            string? netWeightTargetWarnUpper,
            string? netWeightTargetWarnLower
        )
        {
            foreach (var item in Line1PageViewModel!.FaceBPageViewModel!.SpecificationInfos!)
            {
                switch (item.PName)
                {
                    case "规格上限":
                        item.PValue = netWeightTargetUpper;
                        break;
                    case "规格下限":
                        item.PValue = netWeightTargetLower;
                        break;
                    case "控制上限":
                        item.PValue = netWeightTargetWarnUpper;
                        break;
                    case "控制下限":
                        item.PValue = netWeightTargetWarnLower;
                        break;
                    default:
                        break;
                }
            }
        }
        #endregion

        #region 设备监控
        protected void L1PageFaceBDeviceMonitorInfoUpateTestByOne()
        {
            foreach (var item in Line1PageViewModel!.FaceBPageViewModel!.DeviceMonitorInfos!)
            {
                switch (item.PName)
                {
                    case "平均值":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 299.0):F2}";
                        break;
                    case "标准差":
                        item.PValue = $"{(rnd.NextDouble() * 0.1 + 0.3):F2}";
                        break;
                    case "CPK":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 3.6):F2}";
                        break;
                    case "COV":
                        item.PValue = $"{(rnd.NextDouble() * 0.1 + 0.3):F2}";
                        break;
                    case "卷长":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 8000.0):F2}";
                        break;
                    default:
                        break;
                }
            }
        }

        protected void L1PageFaceBDeviceMonitorInfoUpateWeightByOne(
            string? mean,
            string? std,
            string? cpk,
            string? cov,
            string? volLength
        )
        {
            foreach (var item in Line1PageViewModel!.FaceBPageViewModel!.DeviceMonitorInfos!)
            {
                switch (item.PName)
                {
                    case "平均值":
                        item.PValue = mean;
                        break;
                    case "标准差":
                        item.PValue = std;
                        break;
                    case "CPK":
                        item.PValue = cpk;
                        break;
                    case "COV":
                        item.PValue = cov;
                        break;
                    case "卷长":
                        item.PValue = volLength;
                        break;
                    default:
                        break;
                }
            }
        }
        #endregion
        #endregion

        #region 报警信息
        protected void L1PageWeightAlarmInfoUpdateTestByOne()
        {
            Line1PageViewModel!.AddAlarmInfo(
                new AlarmInfo
                {
                    AlarmTime = DateTime.Now,
                    AlarmObject = "",
                    AlarmType = "",
                    AlarmDetail = $"xxxxxxxxxxxxxxxxxxxxx",
                    AlarmLevel = AlarmLevel.Warning,
                }
            );

            Dialog
                .Show(new VM_AlarmInfoDialog().UIElement)
                .Initialize<VM_AlarmInfoDialog>(vm =>
                {
                    vm.AlarmLevel = "警告";
                    vm.AlarmZone = "L1";
                    vm.AlarmInfo = $"xxxxxxxxxxxxxxxxxxxxx";
                    vm.MainFontSize = Line1PageViewModel.LineTitleFontSize;
                })
                .GetResultAsync<bool>();
        }

        protected void L1PageWeightAlarmInfoUpdateByOne(List<string> alarmInfos)
        {
            foreach (var alarmInfo in alarmInfos)
            {
                _logger.Warn($"L1测重仪报警：{alarmInfo}");

                RunOnUIThread(() =>
                {
                    try
                    {
                        Line1PageViewModel!.AddAlarmInfo(
                            new AlarmInfo
                            {
                                AlarmTime = DateTime.Now,
                                AlarmObject = "",
                                AlarmType = "",
                                AlarmDetail = $"{alarmInfo}",
                                AlarmLevel = AlarmLevel.Warning,
                            }
                        );

                        Dialog
                            .Show(new VM_AlarmInfoDialog().UIElement)
                            .Initialize<VM_AlarmInfoDialog>(vm =>
                            {
                                vm.AlarmLevel = "警告";
                                vm.AlarmZone = "L1测重仪";
                                vm.AlarmInfo = $"{alarmInfo}";
                                vm.MainFontSize = Line1PageViewModel.LineTitleFontSize;
                            })
                            .GetResultAsync<bool>();
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("更新报警异常", ex);
                    }
                });
            }
        }
        #endregion
        #endregion

        #region CCD数据更新
        protected void L1PageFaceACCDWidthUpdateTestByOne()
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

        protected void L1PageFaceBCCDWidthUpdateTestByOne()
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
            if (Line1PageViewModel!.FaceADryDataTableRows!.Count >= 3)
            {
                Line1PageViewModel!.FaceADryDataTableRows!.RemoveAt(0);
            }
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
            if (Line1PageViewModel!.FaceBWetDataTableRows!.Count >= 3)
            {
                Line1PageViewModel!.FaceBWetDataTableRows!.RemoveAt(0);
            }
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
            if (Line1PageViewModel!.FaceBDryDataTableRows!.Count >= 3)
            {
                Line1PageViewModel!.FaceBDryDataTableRows!.RemoveAt(0);
            }
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

        protected void L1PageADryAndBWetMistakeDataUpdateTestByOne()
        {
            if (Line1PageViewModel!.FaceADryAndBWetDataTableRows!.Count >= 3)
            {
                Line1PageViewModel!.FaceADryAndBWetDataTableRows!.RemoveAt(0);
            }
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
            if (Line1PageViewModel!.FaceADryAndBDryDataTableRows!.Count >= 3)
            {
                Line1PageViewModel!.FaceADryAndBDryDataTableRows!.RemoveAt(0);
            }
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
        #endregion

        #region PLC数据更新
        protected void L1PageDeviceInfoUpdateByOne()
        {
            Line1PageViewModel!
                .DeviceInfos!.FirstOrDefault(item => item.PName == "涂布实时速度")!
                .PValue =
                $"{_plcService.RWAddresses.Where(address => address.Value.CategoryId == 1).Select(address => address.Value).FirstOrDefault(address => address.ParameterName == "系统运行实时速度" && address.PlcId == 1)?.MonitorValue}";
        }

        protected void L1PageGeneralParameterInfoUpdateByOne()
        {
            foreach (var item in Line1PageViewModel!.GeneralParameterInfos!)
            {
                item.PValue =
                    $"{_plcService.RWAddresses.Where(address => address.Value.CategoryId == 1).Select(address => address.Value).FirstOrDefault(address => address.ParameterName == item.PName && address.PlcId == 1)?.MonitorValue:F1}";
            }
        }

        protected void L1PagePLCAlarmInfoUpdateByOne()
        {
            _plcService
                .RWAddresses.Where(address =>
                    address.Value.CategoryId == 3 && address.Value.PlcId == 1
                )
                .ForEach(address =>
                {
                    if (
                        Line1PageViewModel!
                            .OldPLCAddressMonitor.FirstOrDefault(item =>
                                item.ID == address.Value.ID
                            )
                            ?.MonitorValue == false
                        && address.Value.MonitorValue == true
                    )
                    {
                        RunOnUIThread(() =>
                        {
                            Line1PageViewModel!.AddAlarmInfo(
                                new AlarmInfo
                                {
                                    AlarmTime = DateTime.Now,
                                    AlarmObject = "",
                                    AlarmType = "",
                                    AlarmDetail = $"{address.Value.ParameterName}",
                                    AlarmLevel =
                                        address.Value.Remark == "高"
                                            ? AlarmLevel.Critical
                                            : AlarmLevel.Warning,
                                }
                            );
                        });

                        if (address.Value.Remark == "高")
                        {
                            _logger.Error($"L1 PLC报警：{address.Value.ParameterName}");

                            RunOnUIThread(() =>
                            {
                                Dialog
                                    .Show(new VM_AlarmInfoDialog().UIElement)
                                    .Initialize<VM_AlarmInfoDialog>(vm =>
                                    {
                                        vm.AlarmLevel = "严重";
                                        vm.AlarmZone = "L1 涂布机";
                                        vm.AlarmInfo = $"{address.Value.ParameterName}";
                                        vm.MainFontSize = Line1PageViewModel.LineTitleFontSize;
                                    })
                                    .GetResultAsync<bool>();
                            });
                        }
                        else
                        {
                            _logger.Warn($"L1 PLC报警：{address.Value.ParameterName}");
                        }
                    }
                });
            _plcService
                .RWAddresses.Where(address =>
                    address.Value.CategoryId == 3 && address.Value.PlcId == 1
                )
                .Select(address => address.Value)
                .ForEach(item =>
                {
                    var res = Line1PageViewModel!.OldPLCAddressMonitor.FirstOrDefault(address =>
                        address.ID == item.ID
                    );
                    if (res != null)
                        res.MonitorValue = item.MonitorValue;
                });
        }
        #endregion
        #endregion

        #region L2页面更新
        #region 测重仪数据更新
        #region A面
        #region 分区数据
        protected void L2PageFaceAFilmDataUpateTestByOne()
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
            Line2PageViewModel!.FaceAPageViewModel!.FilmData = data;
        }

        protected void L2PageFaceAFilmDataUpateByOne(
            List<TBAreaInfo> tbAreaInfos,
            List<double> weight,
            double baseValue,
            double warnLow,
            double warnHigh,
            double specLow,
            double specHigh
        )
        {
            Line2PageViewModel!.FaceAPageViewModel!.ZoneBaseValue = baseValue;
            Line2PageViewModel!.FaceAPageViewModel!.RefValue = new Dictionary<string, double>
            {
                { "dangerMaxLimit", specHigh - baseValue },
                { "dangerMinLimit", specLow - baseValue },
                { "centerValue", 0.0 },
                { "warnMaxLimit", warnHigh - baseValue },
                { "warnMinLimit", warnLow - baseValue },
            };

            var data = new List<double[]>();
            var count = 0;
            for (int i = 0; i < tbAreaInfos.Count; i++)
            {
                var arr = new double[tbAreaInfos[i].AreaCount];
                for (int j = 0; j < tbAreaInfos[i].AreaCount; j++)
                {
                    arr[j] = weight[count] - baseValue;
                    count++;
                }
                data.Add(arr);
            }
            Line2PageViewModel!.FaceAPageViewModel!.FilmData = data;
        }
        #endregion

        #region 毫米数据
        protected void L2PageFaceAMMDataUpateTestByOne()
        {
            var x = new List<double>();
            var y = new List<double>();
            for (int i = 0; i < 400; i++)
            {
                x.Add(i);
                double trend = 0.35 * Math.Sin(i * 0.05) + 0.15 * Math.Sin(i * 0.2);
                double noise = (rnd.NextDouble() - 0.5) * 0.5;
                y.Add(300.0 + trend + noise);
            }
            Line2PageViewModel!.FaceAPageViewModel!.X = x;
            Line2PageViewModel!.FaceAPageViewModel!.Y = y;
        }

        protected void L2PageFaceAMMDataUpateByOne(
            List<double> mmData,
            double baseValue,
            double warnLow,
            double warnHigh,
            double specLow,
            double specHigh
        )
        {
            Line2PageViewModel!.FaceAPageViewModel!.MMBaseline = baseValue;
            Line2PageViewModel!.FaceAPageViewModel!.WarnLow = warnLow;
            Line2PageViewModel!.FaceAPageViewModel!.WarnHigh = warnHigh;
            Line2PageViewModel!.FaceAPageViewModel!.SpecLow = specLow;
            Line2PageViewModel!.FaceAPageViewModel!.SpecHigh = specHigh;

            var x = new List<double>();
            for (int i = 0; i < mmData.Count; i++)
            {
                x.Add(i);
            }
            Line2PageViewModel!.FaceAPageViewModel!.X = x;
            Line2PageViewModel!.FaceAPageViewModel!.Y = mmData;
        }
        #endregion

        #region 规格信息
        protected void L2PageFaceASpecificationInfoUpateTestByOne()
        {
            foreach (var item in Line2PageViewModel!.FaceAPageViewModel!.SpecificationInfos!)
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

        protected void L2PageFaceASpecificationInfoUpateByOne(
            string? netWeightTargetUpper,
            string? netWeightTargetLower,
            string? netWeightTargetWarnUpper,
            string? netWeightTargetWarnLower
        )
        {
            foreach (var item in Line2PageViewModel!.FaceAPageViewModel!.SpecificationInfos!)
            {
                switch (item.PName)
                {
                    case "规格上限":
                        item.PValue = netWeightTargetUpper;
                        break;
                    case "规格下限":
                        item.PValue = netWeightTargetLower;
                        break;
                    case "控制上限":
                        item.PValue = netWeightTargetWarnUpper;
                        break;
                    case "控制下限":
                        item.PValue = netWeightTargetWarnLower;
                        break;
                    default:
                        break;
                }
            }
        }
        #endregion

        #region 设备监控
        protected void L2PageFaceADeviceMonitorInfoUpateTestByOne()
        {
            foreach (var item in Line2PageViewModel!.FaceAPageViewModel!.DeviceMonitorInfos!)
            {
                switch (item.PName)
                {
                    case "平均值":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 299.0):F2}";
                        break;
                    case "标准差":
                        item.PValue = $"{(rnd.NextDouble() * 0.1 + 0.3):F2}";
                        break;
                    case "CPK":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 3.6):F2}";
                        break;
                    case "COV":
                        item.PValue = $"{(rnd.NextDouble() * 0.1 + 0.3):F2}";
                        break;
                    case "卷长":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 8000.0):F2}";
                        break;
                    default:
                        break;
                }
            }
        }

        protected void L2PageFaceADeviceMonitorInfoUpateWeightByOne(
            string? mean,
            string? std,
            string? cpk,
            string? cov,
            string? volLength
        )
        {
            foreach (var item in Line2PageViewModel!.FaceAPageViewModel!.DeviceMonitorInfos!)
            {
                switch (item.PName)
                {
                    case "平均值":
                        item.PValue = mean;
                        break;
                    case "标准差":
                        item.PValue = std;
                        break;
                    case "CPK":
                        item.PValue = cpk;
                        break;
                    case "COV":
                        item.PValue = cov;
                        break;
                    case "卷长":
                        item.PValue = volLength;
                        break;
                    default:
                        break;
                }
            }
        }
        #endregion
        #endregion

        #region B面
        #region 分区数据
        protected void L2PageFaceBFilmDataUpateTestByOne()
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
            Line2PageViewModel!.FaceBPageViewModel!.FilmData = data;
        }

        protected void L2PageFaceBFilmDataUpateByOne(
            List<TBAreaInfo> tbAreaInfos,
            List<double> weight,
            double baseValue,
            double warnLow,
            double warnHigh,
            double specLow,
            double specHigh
        )
        {
            Line2PageViewModel!.FaceBPageViewModel!.ZoneBaseValue = baseValue;
            Line2PageViewModel!.FaceBPageViewModel!.RefValue = new Dictionary<string, double>
            {
                { "dangerMaxLimit", specHigh - baseValue },
                { "dangerMinLimit", specLow - baseValue },
                { "centerValue", 0.0 },
                { "warnMaxLimit", warnHigh - baseValue },
                { "warnMinLimit", warnLow - baseValue },
            };

            var data = new List<double[]>();
            var count = 0;
            for (int i = 0; i < tbAreaInfos.Count; i++)
            {
                var arr = new double[tbAreaInfos[i].AreaCount];
                for (int j = 0; j < tbAreaInfos[i].AreaCount; j++)
                {
                    arr[j] = weight[count] - baseValue;
                    count++;
                }
                data.Add(arr);
            }
            Line2PageViewModel!.FaceBPageViewModel!.FilmData = data;
        }
        #endregion

        #region 毫米数据
        protected void L2PageFaceBMMDataUpateTestByOne()
        {
            var x = new List<double>();
            var y = new List<double>();
            for (int i = 0; i < 400; i++)
            {
                x.Add(i);
                double trend = 0.35 * Math.Sin(i * 0.05) + 0.15 * Math.Sin(i * 0.2);
                double noise = (rnd.NextDouble() - 0.5) * 0.5;
                y.Add(300.0 + trend + noise);
            }
            Line2PageViewModel!.FaceBPageViewModel!.X = x;
            Line2PageViewModel!.FaceBPageViewModel!.Y = y;
        }

        protected void L2PageFaceBMMDataUpateByOne(
            List<double> mmData,
            double baseValue,
            double warnLow,
            double warnHigh,
            double specLow,
            double specHigh
        )
        {
            Line2PageViewModel!.FaceBPageViewModel!.MMBaseline = baseValue;
            Line2PageViewModel!.FaceBPageViewModel!.WarnLow = warnLow;
            Line2PageViewModel!.FaceBPageViewModel!.WarnHigh = warnHigh;
            Line2PageViewModel!.FaceBPageViewModel!.SpecLow = specLow;
            Line2PageViewModel!.FaceBPageViewModel!.SpecHigh = specHigh;

            var x = new List<double>();
            for (int i = 0; i < mmData.Count; i++)
            {
                x.Add(i);
            }
            Line2PageViewModel!.FaceBPageViewModel!.X = x;
            Line2PageViewModel!.FaceBPageViewModel!.Y = mmData;
        }
        #endregion

        #region 规格信息
        protected void L2PageFaceBSpecificationInfoUpateTestByOne()
        {
            foreach (var item in Line2PageViewModel!.FaceBPageViewModel!.SpecificationInfos!)
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

        protected void L2PageFaceBSpecificationInfoUpateByOne(
            string? netWeightTargetUpper,
            string? netWeightTargetLower,
            string? netWeightTargetWarnUpper,
            string? netWeightTargetWarnLower
        )
        {
            foreach (var item in Line2PageViewModel!.FaceBPageViewModel!.SpecificationInfos!)
            {
                switch (item.PName)
                {
                    case "规格上限":
                        item.PValue = netWeightTargetUpper;
                        break;
                    case "规格下限":
                        item.PValue = netWeightTargetLower;
                        break;
                    case "控制上限":
                        item.PValue = netWeightTargetWarnUpper;
                        break;
                    case "控制下限":
                        item.PValue = netWeightTargetWarnLower;
                        break;
                    default:
                        break;
                }
            }
        }
        #endregion

        #region 设备监控
        protected void L2PageFaceBDeviceMonitorInfoUpateTestByOne()
        {
            foreach (var item in Line2PageViewModel!.FaceBPageViewModel!.DeviceMonitorInfos!)
            {
                switch (item.PName)
                {
                    case "平均值":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 299.0):F2}";
                        break;
                    case "标准差":
                        item.PValue = $"{(rnd.NextDouble() * 0.1 + 0.3):F2}";
                        break;
                    case "CPK":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 3.6):F2}";
                        break;
                    case "COV":
                        item.PValue = $"{(rnd.NextDouble() * 0.1 + 0.3):F2}";
                        break;
                    case "卷长":
                        item.PValue = $"{(rnd.NextDouble() * 2.0 + 8000.0):F2}";
                        break;
                    default:
                        break;
                }
            }
        }

        protected void L2PageFaceBDeviceMonitorInfoUpateWeightByOne(
            string? mean,
            string? std,
            string? cpk,
            string? cov,
            string? volLength
        )
        {
            foreach (var item in Line2PageViewModel!.FaceBPageViewModel!.DeviceMonitorInfos!)
            {
                switch (item.PName)
                {
                    case "平均值":
                        item.PValue = mean;
                        break;
                    case "标准差":
                        item.PValue = std;
                        break;
                    case "CPK":
                        item.PValue = cpk;
                        break;
                    case "COV":
                        item.PValue = cov;
                        break;
                    case "卷长":
                        item.PValue = volLength;
                        break;
                    default:
                        break;
                }
            }
        }
        #endregion
        #endregion

        #region 报警信息
        protected void L2PageWeightAlarmInfoUpdateTestByOne()
        {
            Line2PageViewModel!.AddAlarmInfo(
                new AlarmInfo
                {
                    AlarmTime = DateTime.Now,
                    AlarmObject = "",
                    AlarmType = "",
                    AlarmDetail = $"xxxxxxxxxxxxxxxxxxxxx",
                    AlarmLevel = AlarmLevel.Warning,
                }
            );
        }

        protected void L2PageWeightAlarmInfoUpdateByOne(List<string> alarmInfos)
        {
            foreach (var alarmInfo in alarmInfos)
            {
                _logger.Warn($"L2测重仪报警：{alarmInfo}");

                RunOnUIThread(() =>
                {
                    Line2PageViewModel!.AddAlarmInfo(
                        new AlarmInfo
                        {
                            AlarmTime = DateTime.Now,
                            AlarmObject = "",
                            AlarmType = "",
                            AlarmDetail = $"{alarmInfo}",
                            AlarmLevel = AlarmLevel.Warning,
                        }
                    );

                    Dialog
                        .Show(new VM_AlarmInfoDialog().UIElement)
                        .Initialize<VM_AlarmInfoDialog>(vm =>
                        {
                            vm.AlarmLevel = "警告";
                            vm.AlarmZone = "L2测重仪";
                            vm.AlarmInfo = $"{alarmInfo}";
                            vm.MainFontSize = Line2PageViewModel.LineTitleFontSize;
                        })
                        .GetResultAsync<bool>();
                });
            }
        }
        #endregion
        #endregion

        #region CCD数据更新
        protected void L2PageFaceACCDWidthUpdateByOne()
        {
            foreach (var item in Line2PageViewModel!.FaceAPageViewModel!.RawSegments!)
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

        protected void L2PageFaceBCCDWidthUpdateByOne()
        {
            foreach (var item in Line2PageViewModel!.FaceBPageViewModel!.RawSegments!)
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

        protected void L2PageFaceADryDataUpdateByOne()
        {
            if (Line2PageViewModel!.FaceADryDataTableRows!.Count >= 3)
            {
                Line2PageViewModel!.FaceADryDataTableRows!.RemoveAt(0);
            }
            Line2PageViewModel!.FaceADryDataTableRows!.Add(
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

        protected void L2PageFaceBWetDataUpdateByOne()
        {
            if (Line2PageViewModel!.FaceBWetDataTableRows!.Count >= 3)
            {
                Line2PageViewModel!.FaceBWetDataTableRows!.RemoveAt(0);
            }
            Line2PageViewModel!.FaceBWetDataTableRows!.Add(
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

        protected void L2PageFaceBDryDataUpdateByOne()
        {
            if (Line2PageViewModel!.FaceBDryDataTableRows!.Count >= 3)
            {
                Line2PageViewModel!.FaceBDryDataTableRows!.RemoveAt(0);
            }
            Line2PageViewModel!.FaceBDryDataTableRows!.Add(
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

        protected void L2PageADryAndBWetMistakeDataUpdateByOne()
        {
            if (Line2PageViewModel!.FaceADryAndBWetDataTableRows!.Count >= 3)
            {
                Line2PageViewModel!.FaceADryAndBWetDataTableRows!.RemoveAt(0);
            }
            Line2PageViewModel!.FaceADryAndBWetDataTableRows!.Add(
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

        protected void L2PageADryAndBDryMistakeDataUpdateByOne()
        {
            if (Line2PageViewModel!.FaceADryAndBDryDataTableRows!.Count >= 3)
            {
                Line2PageViewModel!.FaceADryAndBDryDataTableRows!.RemoveAt(0);
            }
            Line2PageViewModel!.FaceADryAndBDryDataTableRows!.Add(
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
        #endregion

        #region PLC数据更新
        protected void L2PageDeviceInfoUpdateByOne()
        {
            Line2PageViewModel!
                .DeviceInfos!.FirstOrDefault(item => item.PName == "涂布实时速度")!
                .PValue =
                $"{_plcService.RWAddresses.Where(address => address.Value.CategoryId == 1).Select(address => address.Value).FirstOrDefault(address => address.ParameterName == "系统运行实时速度" && address.PlcId == 2)?.MonitorValue}";
        }

        protected void L2PageGeneralParameterInfoUpdateByOne()
        {
            foreach (var item in Line2PageViewModel!.GeneralParameterInfos!)
            {
                item.PValue =
                    $"{_plcService.RWAddresses.Where(address => address.Value.CategoryId == 1).Select(address => address.Value).FirstOrDefault(address => address.ParameterName == item.PName && address.PlcId == 2)?.MonitorValue:F1}";
            }
        }

        protected void L2PagePLCAlarmInfoUpdateByOne()
        {
            _plcService
                .RWAddresses.Where(address =>
                    address.Value.CategoryId == 3 && address.Value.PlcId == 2
                )
                .ForEach(address =>
                {
                    if (
                        Line2PageViewModel!
                            .OldPLCAddressMonitor.FirstOrDefault(item =>
                                item.ID == address.Value.ID
                            )
                            ?.MonitorValue == false
                        && address.Value.MonitorValue == true
                    )
                    {
                        RunOnUIThread(() =>
                        {
                            Line2PageViewModel!.AddAlarmInfo(
                                new AlarmInfo
                                {
                                    AlarmTime = DateTime.Now,
                                    AlarmObject = "",
                                    AlarmType = "",
                                    AlarmDetail = $"{address.Value.ParameterName}",
                                    AlarmLevel =
                                        address.Value.Remark == "高"
                                            ? AlarmLevel.Critical
                                            : AlarmLevel.Warning,
                                }
                            );
                        });

                        if (address.Value.Remark == "高")
                        {
                            _logger.Error($"L2 PLC报警：{address.Value.ParameterName}");

                            RunOnUIThread(() =>
                            {
                                Dialog
                                    .Show(new VM_AlarmInfoDialog().UIElement)
                                    .Initialize<VM_AlarmInfoDialog>(vm =>
                                    {
                                        vm.AlarmLevel = "严重";
                                        vm.AlarmZone = "L2 涂布机";
                                        vm.AlarmInfo = $"{address.Value.ParameterName}";
                                        vm.MainFontSize = Line2PageViewModel.LineTitleFontSize;
                                    })
                                    .GetResultAsync<bool>();
                            });
                        }
                        else
                        {
                            _logger.Warn($"L2 PLC报警：{address.Value.ParameterName}");
                        }
                    }
                });
            _plcService
                .RWAddresses.Where(address =>
                    address.Value.CategoryId == 3 && address.Value.PlcId == 2
                )
                .Select(address => address.Value)
                .ForEach(item =>
                {
                    var res = Line2PageViewModel!.OldPLCAddressMonitor.FirstOrDefault(address =>
                        address.ID == item.ID
                    );
                    if (res != null)
                        res.MonitorValue = item.MonitorValue;
                });
        }
        #endregion
        #endregion

        protected void L1PageDataUpate()
        {
            try
            {
                while (true)
                {
                    L1PageStatusUpdateByOne();

                    //L1PageFaceAFilmDataUpateTestByOne();
                    //L1PageFaceBFilmDataUpateTestByOne();

                    //L1PageFaceAMMDataUpateTestByOne();
                    //L1PageFaceBMMDataUpateTestByOne();

                    //L1PageFaceASpecificationInfoUpateTestByOne();
                    //L1PageFaceBSpecificationInfoUpateTestByOne();

                    //L1PageFaceADeviceMonitorInfoUpateTestByOne();
                    //L1PageFaceBDeviceMonitorInfoUpateTestByOne();

                    L1PageDeviceInfoUpdateByOne();

                    L1PageGeneralParameterInfoUpdateByOne();

                    RunOnUIThread(() =>
                    {
                        try
                        {
                            //L1PageFaceACCDWidthUpdateTestByOne();
                            //L1PageFaceBCCDWidthUpdateTestByOne();

                            //L1PageFaceADryDataUpdateTestByOne();

                            //L1PageFaceBWetDataUpdateTestByOne();

                            //L1PageFaceBDryDataUpdateTestByOne();

                            //L1PageADryAndBWetMistakeDataUpdateTestByOne();

                            //L1PageADryAndBDryMistakeDataUpdateTestByOne();

                            //L1PageWeightAlarmInfoUpdateTestByOne();
                            L1PagePLCAlarmInfoUpdateByOne();
                        }
                        catch (Exception ex)
                        {
                            _logger.Error("L1数据更新异常", ex);
                        }
                    });

                    //GC.Collect();

                    Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("线程调用异常", ex);
            }
        }

        protected void L2PageDataUpate()
        {
            try
            {
                while (true)
                {
                    L2PageStatusUpdateByOne();

                    //L2PageFaceAFilmDataUpateTestByOne();
                    //L2PageFaceBFilmDataUpateTestByOne();

                    //L2PageFaceAMMDataUpateTestByOne();
                    //L2PageFaceBMMDataUpateTestByOne();

                    //L2PageFaceASpecificationInfoUpateTestByOne();
                    //L2PageFaceBSpecificationInfoUpateTestByOne();

                    //L2PageFaceADeviceMonitorInfoUpateTestByOne();
                    //L2PageFaceBDeviceMonitorInfoUpateTestByOne();

                    L2PageDeviceInfoUpdateByOne();

                    L2PageGeneralParameterInfoUpdateByOne();

                    RunOnUIThread(() =>
                    {
                        try
                        {
                            //L2PageFaceACCDWidthUpdateByOne();
                            //L2PageFaceBCCDWidthUpdateByOne();

                            //L2PageFaceADryDataUpdateByOne();

                            //L2PageFaceBWetDataUpdateByOne();

                            //L2PageFaceBDryDataUpdateByOne();

                            //L2PageADryAndBWetMistakeDataUpdateByOne();

                            //L2PageADryAndBDryMistakeDataUpdateByOne();

                            //L2PageWeightAlarmInfoUpdateTestByOne();
                            L2PagePLCAlarmInfoUpdateByOne();
                        }
                        catch (Exception ex)
                        {
                            _logger.Error("L2数据更新异常", ex);
                        }
                    });

                    //GC.Collect();

                    Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("线程调用异常", ex);
            }
        }

        /// <summary>
        /// L1测重数据处理
        /// </summary>
        /// <param name="gatherTime"></param>
        /// <param name="weight"></param>
        /// <returns></returns>
        private async Task L1WeightGaugeServer_WeightMessageParseSEvent(
            DateTime gatherTime,
            CCTBWEIGHTQUEST? weight
        )
        {
            if (weight != null)
            {
                try
                {
                    _logger.Info($"数据类型：{weight.DEVICENAME}");
                    switch (weight.DEVICENAME)
                    {
                        case "A":
                            L1PageFaceAFilmDataUpateByOne(
                                weight.TBAREAINFO,
                                weight.WEIGHT.ToList().ConvertAll(double.Parse).ToList(),
                                weight.TARGET_VALUE.ToDouble(),
                                weight.NET_TARGETWARN_LOWER,
                                weight.NET_TARGETWARN_UPPER,
                                weight.SPEC_DW_LIM.ToDouble(),
                                weight.SPEC_UP_LIM.ToDouble()
                            );
                            L1PageFaceASpecificationInfoUpateByOne(
                                weight.SPEC_UP_LIM,
                                weight.SPEC_DW_LIM,
                                $"{weight.NET_TARGETWARN_UPPER:F3}",
                                $"{weight.NET_TARGETWARN_LOWER:F3}"
                            );
                            L1PageFaceADeviceMonitorInfoUpateWeightByOne(
                                weight.AVERAGE_VALUE,
                                $"{weight.STDDEV:F3}",
                                $"{weight.CPK:F3}",
                                $"{weight.COV:P3}",
                                $"{weight.VOL_LENGTH}"
                            );
                            break;
                        case "AT":
                            break;
                        case "B":
                            L1PageFaceBFilmDataUpateByOne(
                                weight.TBAREAINFO,
                                weight.WEIGHT.ToList().ConvertAll(double.Parse).ToList(),
                                weight.TARGET_VALUE.ToDouble(),
                                weight.NET_TARGETWARN_LOWER,
                                weight.NET_TARGETWARN_UPPER,
                                weight.SPEC_DW_LIM.ToDouble(),
                                weight.SPEC_UP_LIM.ToDouble()
                            );
                            L1PageFaceBSpecificationInfoUpateByOne(
                                weight.SPEC_UP_LIM,
                                weight.SPEC_DW_LIM,
                                $"{weight.NET_TARGETWARN_UPPER:F3}",
                                $"{weight.NET_TARGETWARN_LOWER:F3}"
                            );
                            L1PageFaceBDeviceMonitorInfoUpateWeightByOne(
                                weight.AVERAGE_VALUE,
                                $"{weight.STDDEV:F3}",
                                $"{weight.CPK:F3}",
                                $"{weight.COV:P3}",
                                $"{weight.VOL_LENGTH}"
                            );
                            break;
                        case "BT":
                            break;
                        case "AMM":
                            L1PageFaceAMMDataUpateByOne(
                                weight.WEIGHT.ToList().ConvertAll(double.Parse).ToList(),
                                weight.TARGET_VALUE.ToDouble(),
                                weight.NET_TARGETWARN_LOWER,
                                weight.NET_TARGETWARN_UPPER,
                                weight.SPEC_DW_LIM.ToDouble(),
                                weight.SPEC_UP_LIM.ToDouble()
                            );
                            L1PageFaceASpecificationInfoUpateByOne(
                                weight.SPEC_UP_LIM,
                                weight.SPEC_DW_LIM,
                                $"{weight.NET_TARGETWARN_UPPER:F3}",
                                $"{weight.NET_TARGETWARN_LOWER:F3}"
                            );
                            L1PageFaceADeviceMonitorInfoUpateWeightByOne(
                                weight.AVERAGE_VALUE,
                                $"{weight.STDDEV:F3}",
                                $"{weight.CPK:F3}",
                                $"{weight.COV:P3}",
                                $"{weight.VOL_LENGTH}"
                            );
                            break;
                        case "ATMM":
                            break;
                        case "BMM":
                            L1PageFaceBMMDataUpateByOne(
                                weight.WEIGHT.ToList().ConvertAll(double.Parse).ToList(),
                                weight.TARGET_VALUE.ToDouble(),
                                weight.NET_TARGETWARN_LOWER,
                                weight.NET_TARGETWARN_UPPER,
                                weight.SPEC_DW_LIM.ToDouble(),
                                weight.SPEC_UP_LIM.ToDouble()
                            );
                            L1PageFaceBSpecificationInfoUpateByOne(
                                weight.SPEC_UP_LIM,
                                weight.SPEC_DW_LIM,
                                $"{weight.NET_TARGETWARN_UPPER:F3}",
                                $"{weight.NET_TARGETWARN_LOWER:F3}"
                            );
                            L1PageFaceBDeviceMonitorInfoUpateWeightByOne(
                                weight.AVERAGE_VALUE,
                                $"{weight.STDDEV:F3}",
                                $"{weight.CPK:F3}",
                                $"{weight.COV:P3}",
                                $"{weight.VOL_LENGTH}"
                            );
                            break;
                        case "BTMM":
                            break;
                        case "AW":
                            break;
                        case "AWT":
                            break;
                        case "BW":
                            break;
                        case "BWT":
                            break;
                        case "AWMM":
                            break;
                        case "AWTMM":
                            break;
                        case "BWMM":
                            break;
                        case "BWTMM":
                            break;
                        default:
                            break;
                    }

                    L1PageWeightAlarmInfoUpdateByOne(weight.ALARMS);
                }
                catch (Exception ex)
                {
                    _logger.Error("解析错误", ex);
                }
            }
            await Task.CompletedTask;
        }

        private async Task L2WeightGaugeServer_WeightMessageParseSEvent(
            DateTime gatherTime,
            CCTBWEIGHTQUEST? weight
        )
        {
            if (weight != null)
            {
                try
                {
                    switch (weight.DEVICENAME)
                    {
                        case "A":
                            L2PageFaceAFilmDataUpateByOne(
                                weight.TBAREAINFO,
                                weight.WEIGHT.ToList().ConvertAll(double.Parse).ToList(),
                                weight.TARGET_VALUE.ToDouble(),
                                weight.NET_TARGETWARN_LOWER,
                                weight.NET_TARGETWARN_UPPER,
                                weight.SPEC_DW_LIM.ToDouble(),
                                weight.SPEC_UP_LIM.ToDouble()
                            );
                            L2PageFaceASpecificationInfoUpateByOne(
                                weight.SPEC_UP_LIM,
                                weight.SPEC_DW_LIM,
                                $"{weight.NET_TARGETWARN_UPPER:F3}",
                                $"{weight.NET_TARGETWARN_LOWER:F3}"
                            );
                            L2PageFaceADeviceMonitorInfoUpateWeightByOne(
                                weight.AVERAGE_VALUE,
                                $"{weight.STDDEV:F3}",
                                $"{weight.CPK:F3}",
                                $"{weight.COV:P3}",
                                $"{weight.VOL_LENGTH}"
                            );
                            break;
                        case "AT":
                            break;
                        case "B":
                            L2PageFaceBFilmDataUpateByOne(
                                weight.TBAREAINFO,
                                weight.WEIGHT.ToList().ConvertAll(double.Parse).ToList(),
                                weight.TARGET_VALUE.ToDouble(),
                                weight.NET_TARGETWARN_LOWER,
                                weight.NET_TARGETWARN_UPPER,
                                weight.SPEC_DW_LIM.ToDouble(),
                                weight.SPEC_UP_LIM.ToDouble()
                            );
                            L2PageFaceBSpecificationInfoUpateByOne(
                                weight.SPEC_UP_LIM,
                                weight.SPEC_DW_LIM,
                                $"{weight.NET_TARGETWARN_UPPER:F3}",
                                $"{weight.NET_TARGETWARN_LOWER:F3}"
                            );
                            L2PageFaceBDeviceMonitorInfoUpateWeightByOne(
                                weight.AVERAGE_VALUE,
                                $"{weight.STDDEV:F3}",
                                $"{weight.CPK:F3}",
                                $"{weight.COV:P3}",
                                $"{weight.VOL_LENGTH}"
                            );
                            break;
                        case "BT":
                            break;
                        case "AMM":
                            L2PageFaceAMMDataUpateByOne(
                                weight.WEIGHT.ToList().ConvertAll(double.Parse).ToList(),
                                weight.TARGET_VALUE.ToDouble(),
                                weight.NET_TARGETWARN_LOWER,
                                weight.NET_TARGETWARN_UPPER,
                                weight.SPEC_DW_LIM.ToDouble(),
                                weight.SPEC_UP_LIM.ToDouble()
                            );
                            L2PageFaceASpecificationInfoUpateByOne(
                                weight.SPEC_UP_LIM,
                                weight.SPEC_DW_LIM,
                                $"{weight.NET_TARGETWARN_UPPER:F3}",
                                $"{weight.NET_TARGETWARN_LOWER:F3}"
                            );
                            L2PageFaceADeviceMonitorInfoUpateWeightByOne(
                                weight.AVERAGE_VALUE,
                                $"{weight.STDDEV:F3}",
                                $"{weight.CPK:F3}",
                                $"{weight.COV:P3}",
                                $"{weight.VOL_LENGTH}"
                            );
                            break;
                        case "ATMM":
                            break;
                        case "BMM":
                            L2PageFaceBMMDataUpateByOne(
                                weight.WEIGHT.ToList().ConvertAll(double.Parse).ToList(),
                                weight.TARGET_VALUE.ToDouble(),
                                weight.NET_TARGETWARN_LOWER,
                                weight.NET_TARGETWARN_UPPER,
                                weight.SPEC_DW_LIM.ToDouble(),
                                weight.SPEC_UP_LIM.ToDouble()
                            );
                            L2PageFaceBSpecificationInfoUpateByOne(
                                weight.SPEC_UP_LIM,
                                weight.SPEC_DW_LIM,
                                $"{weight.NET_TARGETWARN_UPPER:F3}",
                                $"{weight.NET_TARGETWARN_LOWER:F3}"
                            );
                            L2PageFaceBDeviceMonitorInfoUpateWeightByOne(
                                weight.AVERAGE_VALUE,
                                $"{weight.STDDEV:F3}",
                                $"{weight.CPK:F3}",
                                $"{weight.COV:P3}",
                                $"{weight.VOL_LENGTH}"
                            );
                            break;
                        case "BTMM":
                            break;
                        case "AW":
                            break;
                        case "AWT":
                            break;
                        case "BW":
                            break;
                        case "BWT":
                            break;
                        case "AWMM":
                            break;
                        case "AWTMM":
                            break;
                        case "BWMM":
                            break;
                        case "BWTMM":
                            break;
                        default:
                            break;
                    }

                    L2PageWeightAlarmInfoUpdateByOne(weight.ALARMS);
                }
                catch (Exception ex)
                {
                    _logger.Error("解析错误", ex);
                }
            }
            await Task.CompletedTask;
        }

        private async Task L1CCDGaugeServer_AlarmTypeJsonParse(
            string msgType,
            DateTime getDataTime,
            JsonElement dataJsonElement
        )
        {
            var zone = dataJsonElement.GetProperty("zone").GetString();
            var info = dataJsonElement.GetProperty("info").GetString();

            _logger.Warn($"L1 CCD报警：{zone}: {info}");

            RunOnUIThread(() =>
            {
                try
                {
                    Line1PageViewModel!.AddAlarmInfo(
                        new AlarmInfo
                        {
                            AlarmTime = DateTime.Now,
                            AlarmObject = "",
                            AlarmType = "",
                            AlarmDetail = $"{zone}: {info}",
                            AlarmLevel =
                                (info != null && !info.Contains("未知缺陷"))
                                    ? AlarmLevel.Critical
                                    : AlarmLevel.Warning,
                        }
                    );

                    if (info != null && !info.Contains("未知缺陷"))
                    {
                        _ = Dialog
                            .Show(new VM_AlarmInfoDialog().UIElement)
                            .Initialize<VM_AlarmInfoDialog>(vm =>
                            {
                                vm.AlarmLevel = "严重";
                                vm.AlarmZone = $"L1 : {zone}";
                                vm.AlarmInfo = $"{info}";
                                vm.MainFontSize = Line1PageViewModel.LineTitleFontSize;
                            })
                            .GetResultAsync<bool>();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("报警更新异常", ex);
                }
            });

            await Task.CompletedTask;
        }

        private async Task L2CCDGaugeServer_AlarmTypeJsonParse(
            string msgType,
            DateTime getDataTime,
            JsonElement dataJsonElement
        )
        {
            var zone = dataJsonElement.GetProperty("zone").GetString();
            var info = dataJsonElement.GetProperty("info").GetString();

            _logger.Warn($"L2 CCD报警：{zone}: {info}");

            RunOnUIThread(() =>
            {
                try
                {
                    Line2PageViewModel!.AddAlarmInfo(
                        new AlarmInfo
                        {
                            AlarmTime = DateTime.Now,
                            AlarmObject = "",
                            AlarmType = "",
                            AlarmDetail = $"{zone}: {info}",
                            AlarmLevel =
                                (info != null && !info.Contains("未知缺陷"))
                                    ? AlarmLevel.Critical
                                    : AlarmLevel.Warning,
                        }
                    );

                    if (info != null && !info.Contains("未知缺陷"))
                    {
                        _ = Dialog
                            .Show(new VM_AlarmInfoDialog().UIElement)
                            .Initialize<VM_AlarmInfoDialog>(vm =>
                            {
                                vm.AlarmLevel = "严重";
                                vm.AlarmZone = $"L2 : {zone}";
                                vm.AlarmInfo = $"{info}";
                                vm.MainFontSize = Line2PageViewModel.LineTitleFontSize;
                            })
                            .GetResultAsync<bool>();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("报警更新异常", ex);
                }
            });

            await Task.CompletedTask;
        }

        private async Task L1CCDGaugeServer_DataTypeJsonParse(
            string msgType,
            DateTime getDataTime,
            JsonElement dataJsonElement
        )
        {
            var tab_width = dataJsonElement
                .GetProperty("tab_width")
                .Deserialize<List<ParameterModel>>();

            foreach (var item in tab_width!)
            {
                _logger.Info($"{item.PName}: {item.PValue}");
            }

            var cil_width = dataJsonElement
                .GetProperty("cil_width")
                .Deserialize<List<ParameterModel>>();

            foreach (var item in cil_width!)
            {
                _logger.Info($"{item.PName}: {item.PValue}");
            }

            var coating_width = dataJsonElement
                .GetProperty("coating_width")
                .Deserialize<List<ParameterModel>>();

            foreach (var item in coating_width!)
            {
                _logger.Info($"{item.PName}: {item.PValue}");
            }

            var coating_total_width = dataJsonElement
                .GetProperty("coating_total_width")
                .GetDouble();

            _logger.Info($"涂膜总宽：{coating_total_width}");

            var judge_res = dataJsonElement
                .GetProperty("judge_res")
                .Deserialize<List<JudgeResult>>();

            foreach (var item in judge_res!)
            {
                _logger.Info($"{item.Zone}: {item.Result}");
            }

            var tab_specification = dataJsonElement
                .GetProperty("tab_specification")
                .Deserialize<List<ParameterModel>>();

            foreach (var item in tab_specification!)
            {
                _logger.Info($"{item.PName}: {item.PValue}");
            }

            foreach (var item in tab_specification!)
            {
                if (item.PName == "边缘极耳目标值")
                {
                    Line1PageViewModel!.CoatingCCDSpecModel.EdgeTabTargetValue =
                        item.PValue!.ToDouble();
                }
                else if (item.PName == "边缘极耳上限预警公差")
                {
                    Line1PageViewModel!.CoatingCCDSpecModel.EdgeTabWarnMax =
                        item.PValue!.ToDouble();
                }
                else if (item.PName == "边缘极耳下限预警公差")
                {
                    Line1PageViewModel!.CoatingCCDSpecModel.EdgeTabWarnMin =
                        item.PValue!.ToDouble();
                }
                else if (item.PName == "边缘极耳上限公差")
                {
                    Line1PageViewModel!.CoatingCCDSpecModel.EdgeTabLimitMax =
                        item.PValue!.ToDouble();
                }
                else if (item.PName == "边缘极耳下限公差")
                {
                    Line1PageViewModel!.CoatingCCDSpecModel.EdgeTabLimitMin =
                        item.PValue!.ToDouble();
                }
                else if (item.PName == "中间极耳目标值")
                {
                    Line1PageViewModel!.CoatingCCDSpecModel.MiddleTabTargetValue =
                        item.PValue!.ToDouble();
                }
                else if (item.PName == "中间极耳上限预警公差")
                {
                    Line1PageViewModel!.CoatingCCDSpecModel.MiddleTabWarnMax =
                        item.PValue!.ToDouble();
                }
                else if (item.PName == "中间极耳下限预警公差")
                {
                    Line1PageViewModel!.CoatingCCDSpecModel.MiddleTabWarnMin =
                        item.PValue!.ToDouble();
                }
                else if (item.PName == "中间极耳上限公差")
                {
                    Line1PageViewModel!.CoatingCCDSpecModel.MiddleTabLimitMax =
                        item.PValue!.ToDouble();
                }
                else if (item.PName == "中间极耳下限公差")
                {
                    Line1PageViewModel!.CoatingCCDSpecModel.MiddleTabLimitMin =
                        item.PValue!.ToDouble();
                }
            }

            var cil_specification = dataJsonElement
                .GetProperty("cil_specification")
                .Deserialize<List<ParameterModel>>();

            foreach (var item in cil_specification!)
            {
                _logger.Info($"{item.PName}: {item.PValue}");
            }

            foreach (var item in cil_specification!)
            {
                if (item.PName == "陶瓷目标值")
                {
                    Line1PageViewModel!.CoatingCCDSpecModel.CILTargetValue =
                        item.PValue!.ToDouble();
                }
                else if (item.PName == "陶瓷上限预警公差")
                {
                    Line1PageViewModel!.CoatingCCDSpecModel.CILWarnMax = item.PValue!.ToDouble();
                }
                else if (item.PName == "陶瓷下限预警公差")
                {
                    Line1PageViewModel!.CoatingCCDSpecModel.CILWarnMin = item.PValue!.ToDouble();
                }
                else if (item.PName == "陶瓷上限公差")
                {
                    Line1PageViewModel!.CoatingCCDSpecModel.CILLimitMax = item.PValue!.ToDouble();
                }
                else if (item.PName == "陶瓷下限公差")
                {
                    Line1PageViewModel!.CoatingCCDSpecModel.CILLimitMin = item.PValue!.ToDouble();
                }
            }

            var coating_specification = dataJsonElement
                .GetProperty("coating_specification")
                .Deserialize<List<ParameterModel>>();

            foreach (var item in coating_specification!)
            {
                _logger.Info($"{item.PName}: {item.PValue}");
            }

            foreach (var item in coating_specification!)
            {
                if (item.PName == "涂膜目标值")
                {
                    Line1PageViewModel!.CoatingCCDSpecModel.CoatingTargetValue =
                        item.PValue!.ToDouble();
                }
                else if (item.PName == "涂膜上限预警公差")
                {
                    Line1PageViewModel!.CoatingCCDSpecModel.CoatingWarnMax =
                        item.PValue!.ToDouble();
                }
                else if (item.PName == "涂膜下限预警公差")
                {
                    Line1PageViewModel!.CoatingCCDSpecModel.CoatingWarnMin =
                        item.PValue!.ToDouble();
                }
                else if (item.PName == "涂膜上限公差")
                {
                    Line1PageViewModel!.CoatingCCDSpecModel.CoatingLimitMax =
                        item.PValue!.ToDouble();
                }
                else if (item.PName == "涂膜下限公差")
                {
                    Line1PageViewModel!.CoatingCCDSpecModel.CoatingLimitMin =
                        item.PValue!.ToDouble();
                }
            }

            switch (msgType)
            {
                case "data_a_wet":
                {
                    break;
                }
                case "data_b_wet":
                {
                    var misalign = dataJsonElement
                        .GetProperty("misalign")
                        .Deserialize<List<ParameterModel>>();

                    var misalign_specification = dataJsonElement
                        .GetProperty("misalign_specification")
                        .Deserialize<List<ParameterModel>>();

                    foreach (var item in misalign!)
                    {
                        _logger.Info($"{item.PName}: {item.PValue}");
                    }

                    foreach (var item in misalign_specification!)
                    {
                        _logger.Info($"{item.PName}: {item.PValue}");
                    }

                    foreach (var item in misalign_specification!)
                    {
                        if (item.PName == "错位目标值")
                        {
                            Line1PageViewModel!.CoatingCCDSpecModel.MisalignTargetValue =
                                item.PValue!.ToDouble();
                        }
                        else if (item.PName == "错位上限预警公差")
                        {
                            Line1PageViewModel!.CoatingCCDSpecModel.MisalignWarnMax =
                                item.PValue!.ToDouble();
                        }
                        else if (item.PName == "错位下限预警公差")
                        {
                            Line1PageViewModel!.CoatingCCDSpecModel.MisalignWarnMin =
                                item.PValue!.ToDouble();
                        }
                        else if (item.PName == "错位上限公差")
                        {
                            Line1PageViewModel!.CoatingCCDSpecModel.MisalignLimitMax =
                                item.PValue!.ToDouble();
                        }
                        else if (item.PName == "错位下限公差")
                        {
                            Line1PageViewModel!.CoatingCCDSpecModel.MisalignLimitMin =
                                item.PValue!.ToDouble();
                        }
                    }

                    RunOnUIThread(() =>
                    {
                        try
                        {
                            // A干B湿CCD区域显示数据解析
                            foreach (
                                var item in Line1PageViewModel!.FaceAPageViewModel!.RawSegments!
                            )
                            {
                                if (item.Name.Contains("Tab"))
                                {
                                    foreach (
                                        var p in tab_width!.Where(tab =>
                                            tab.PName!.EndsWith(item.Name.Replace("Tab", ""))
                                        )
                                    )
                                    {
                                        if (p.PName!.Contains("A"))
                                        {
                                            item.WidthA = p.PValue!.ToDouble();
                                            item.ForegroundA =
                                                item.Type == CoatingSegmentType.EdgeTab
                                                    ? GetColorFromEdgeTab(
                                                        Line1PageViewModel!.CoatingCCDSpecModel,
                                                        item.WidthA
                                                    )
                                                    : GetColorFromMiddleTab(
                                                        Line1PageViewModel!.CoatingCCDSpecModel,
                                                        item.WidthA
                                                    );
                                        }
                                        else if (p.PName!.Contains("B"))
                                        {
                                            item.WidthB = p.PValue!.ToDouble();
                                            item.ForegroundB =
                                                item.Type == CoatingSegmentType.EdgeTab
                                                    ? GetColorFromEdgeTab(
                                                        Line1PageViewModel!.CoatingCCDSpecModel,
                                                        item.WidthB
                                                    )
                                                    : GetColorFromMiddleTab(
                                                        Line1PageViewModel!.CoatingCCDSpecModel,
                                                        item.WidthB
                                                    );
                                        }
                                    }
                                }
                                else if (item.Name.Contains("CIL"))
                                {
                                    foreach (
                                        var p in cil_width!.Where(cil =>
                                            cil.PName!.EndsWith(item.Name.Replace("CIL", ""))
                                        )
                                    )
                                    {
                                        if (p.PName!.Contains("A"))
                                        {
                                            item.WidthA = p.PValue!.ToDouble();
                                            item.ForegroundA = GetColorFromCIL(
                                                Line1PageViewModel!.CoatingCCDSpecModel,
                                                item.WidthA
                                            );
                                        }
                                        else if (p.PName!.Contains("B"))
                                        {
                                            item.WidthB = p.PValue!.ToDouble();
                                            item.ForegroundB = GetColorFromCIL(
                                                Line1PageViewModel!.CoatingCCDSpecModel,
                                                item.WidthB
                                            );
                                        }
                                    }
                                }
                                else if (item.Name.Contains("Coating"))
                                {
                                    foreach (
                                        var p in coating_width!.Where(coating =>
                                            coating.PName!.EndsWith(
                                                item.Name.Replace("Coating", "")
                                            )
                                        )
                                    )
                                    {
                                        if (p.PName!.Contains("A"))
                                        {
                                            item.WidthA = p.PValue!.ToDouble();
                                            item.ForegroundA = GetColorFromCoating(
                                                Line1PageViewModel!.CoatingCCDSpecModel,
                                                item.WidthA
                                            );
                                        }
                                        else if (p.PName!.Contains("B"))
                                        {
                                            item.WidthB = p.PValue!.ToDouble();
                                            item.ForegroundB = GetColorFromCoating(
                                                Line1PageViewModel!.CoatingCCDSpecModel,
                                                item.WidthB
                                            );
                                        }
                                    }
                                }
                            }

                            // A干CCD区域显示数据解析
                            //foreach (var item in Line1PageViewModel!.FaceBPageViewModel!.RawSegments!)
                            //{
                            //    if (item.Name.Contains("Tab"))
                            //    {
                            //        foreach (
                            //            var p in tab_width!.Where(tab =>
                            //                tab.PName!.EndsWith(item.Name.Replace("Tab", ""))
                            //            )
                            //        )
                            //        {
                            //            if (p.PName!.Contains("A"))
                            //            {
                            //                item.WidthA = p.PValue!.ToDouble();
                            //                item.ForegroundA =
                            //                    item.Type == CoatingSegmentType.EdgeTab
                            //                        ? GetColorFromEdgeTab(
                            //                            Line1PageViewModel!.CoatingCCDSpecModel,
                            //                            item.WidthA
                            //                        )
                            //                        : GetColorFromMiddleTab(
                            //                            Line1PageViewModel!.CoatingCCDSpecModel,
                            //                            item.WidthA
                            //                        );
                            //            }
                            //        }
                            //    }
                            //    else if (item.Name.Contains("CIL"))
                            //    {
                            //        foreach (
                            //            var p in cil_width!.Where(cil =>
                            //                cil.PName!.EndsWith(item.Name.Replace("CIL", ""))
                            //            )
                            //        )
                            //        {
                            //            if (p.PName!.Contains("A"))
                            //            {
                            //                item.WidthA = p.PValue!.ToDouble();
                            //                item.ForegroundA = GetColorFromCIL(
                            //                    Line1PageViewModel!.CoatingCCDSpecModel,
                            //                    item.WidthA
                            //                );
                            //            }
                            //        }
                            //    }
                            //    else if (item.Name.Contains("Coating"))
                            //    {
                            //        foreach (
                            //            var p in coating_width!.Where(coating =>
                            //                coating.PName!.EndsWith(item.Name.Replace("Coating", ""))
                            //            )
                            //        )
                            //        {
                            //            if (p.PName!.Contains("A"))
                            //            {
                            //                item.WidthA = p.PValue!.ToDouble();
                            //                item.ForegroundA = GetColorFromCoating(
                            //                    Line1PageViewModel!.CoatingCCDSpecModel,
                            //                    item.WidthA
                            //                );
                            //            }
                            //        }
                            //    }
                            //}

                            // A面干膜数据解析
                            var ranksegs = RankSegments(
                                Line1PageViewModel!.CoatingCCDSpecModel,
                                tab_width!.Where(tab => tab.PName!.Contains("A干")).ToList(),
                                cil_width!.Where(cil => cil.PName!.Contains("A干")).ToList(),
                                coating_width!
                                    .Where(coating => coating.PName!.Contains("A干"))
                                    .ToList()
                            );
                            var cells = new ObservableCollection<CellData>()
                            {
                                new CellData
                                {
                                    Value = $"{getDataTime:HH:mm:ss}",
                                    Background = Brushes.Transparent,
                                    Foreground = Brushes.Black,
                                },
                            };
                            foreach (var item in ranksegs)
                            {
                                cells.Add(item);
                            }
                            if (Line1PageViewModel!.FaceADryDataTableRows!.Count >= 3)
                                Line1PageViewModel!.FaceADryDataTableRows!.RemoveAt(0);
                            Line1PageViewModel!.FaceADryDataTableRows!.Add(
                                new RowData { Cells = cells }
                            );

                            // B面湿膜数据解析
                            ranksegs = RankSegments(
                                Line1PageViewModel!.CoatingCCDSpecModel,
                                tab_width!.Where(tab => tab.PName!.Contains("B湿")).ToList(),
                                cil_width!.Where(cil => cil.PName!.Contains("B湿")).ToList(),
                                coating_width!
                                    .Where(coating => coating.PName!.Contains("B湿"))
                                    .ToList()
                            );
                            cells =
                            [
                                new CellData
                                {
                                    Value = $"{getDataTime:HH:mm:ss}",
                                    Background = Brushes.Transparent,
                                    Foreground = Brushes.Black,
                                },
                                .. ranksegs,
                            ];
                            if (Line1PageViewModel!.FaceBWetDataTableRows!.Count >= 3)
                                Line1PageViewModel!.FaceBWetDataTableRows!.RemoveAt(0);
                            Line1PageViewModel!.FaceBWetDataTableRows!.Add(
                                new RowData { Cells = cells }
                            );

                            // A干B湿错位数据解析
                            var misalignCells = new ObservableCollection<CellData>()
                            {
                                new CellData
                                {
                                    Value = $"{getDataTime:HH:mm:ss}",
                                    Background = Brushes.Transparent,
                                    Foreground = Brushes.Black,
                                },
                            };
                            foreach (var item in misalign!)
                            {
                                misalignCells.Add(
                                    new CellData
                                    {
                                        Value = $"{item.PValue!.ToDouble():F2}",
                                        Foreground = GetColorFromMisalign(
                                            Line1PageViewModel!.CoatingCCDSpecModel,
                                            item.PValue!.ToDouble()
                                        ),
                                        Background = SetColorOpacity(
                                            GetColorFromMisalign(
                                                Line1PageViewModel!.CoatingCCDSpecModel,
                                                item.PValue!.ToDouble()
                                            ),
                                            0.5
                                        ),
                                    }
                                );
                            }
                            if (Line1PageViewModel!.FaceADryAndBWetDataTableRows!.Count >= 3)
                                Line1PageViewModel!.FaceADryAndBWetDataTableRows!.RemoveAt(0);
                            Line1PageViewModel!.FaceADryAndBWetDataTableRows!.Add(
                                new RowData { Cells = misalignCells }
                            );
                        }
                        catch (Exception ex)
                        {
                            _logger.Error("A干B湿数据更新异常", ex);
                        }
                    });

                    break;
                }

                case "data_b_dry":
                {
                    var misalign = dataJsonElement
                        .GetProperty("misalign")
                        .Deserialize<List<ParameterModel>>();
                    var misalign_specification = dataJsonElement
                        .GetProperty("misalign_specification")
                        .Deserialize<List<ParameterModel>>();

                    foreach (var item in misalign!)
                    {
                        _logger.Info($"{item.PName}: {item.PValue}");
                    }

                    foreach (var item in misalign_specification!)
                    {
                        _logger.Info($"{item.PName}: {item.PValue}");
                    }

                    foreach (var item in misalign_specification!)
                    {
                        if (item.PName == "错位目标值")
                        {
                            Line1PageViewModel!.CoatingCCDSpecModel.MisalignTargetValue =
                                item.PValue!.ToDouble();
                        }
                        else if (item.PName == "错位上限预警公差")
                        {
                            Line1PageViewModel!.CoatingCCDSpecModel.MisalignWarnMax =
                                item.PValue!.ToDouble();
                        }
                        else if (item.PName == "错位下限预警公差")
                        {
                            Line1PageViewModel!.CoatingCCDSpecModel.MisalignWarnMin =
                                item.PValue!.ToDouble();
                        }
                        else if (item.PName == "错位上限公差")
                        {
                            Line1PageViewModel!.CoatingCCDSpecModel.MisalignLimitMax =
                                item.PValue!.ToDouble();
                        }
                        else if (item.PName == "错位下限公差")
                        {
                            Line1PageViewModel!.CoatingCCDSpecModel.MisalignLimitMin =
                                item.PValue!.ToDouble();
                        }
                    }

                    RunOnUIThread(() =>
                    {
                        try
                        {
                            // B干CCD区域显示数据解析
                            foreach (
                                var item in Line1PageViewModel!.FaceBPageViewModel!.RawSegments!
                            )
                            {
                                if (item.Name.Contains("Tab"))
                                {
                                    foreach (
                                        var p in tab_width!.Where(tab =>
                                            tab.PName!.EndsWith(item.Name.Replace("Tab", ""))
                                        )
                                    )
                                    {
                                        if (p.PName!.Contains("A"))
                                        {
                                            item.WidthA = p.PValue!.ToDouble();
                                            item.ForegroundA =
                                                item.Type == CoatingSegmentType.EdgeTab
                                                    ? GetColorFromEdgeTab(
                                                        Line1PageViewModel!.CoatingCCDSpecModel,
                                                        item.WidthA
                                                    )
                                                    : GetColorFromMiddleTab(
                                                        Line1PageViewModel!.CoatingCCDSpecModel,
                                                        item.WidthA
                                                    );
                                        }
                                        else if (p.PName!.Contains("B"))
                                        {
                                            item.WidthB = p.PValue!.ToDouble();
                                            item.ForegroundB =
                                                item.Type == CoatingSegmentType.EdgeTab
                                                    ? GetColorFromEdgeTab(
                                                        Line1PageViewModel!.CoatingCCDSpecModel,
                                                        item.WidthB
                                                    )
                                                    : GetColorFromMiddleTab(
                                                        Line1PageViewModel!.CoatingCCDSpecModel,
                                                        item.WidthB
                                                    );
                                        }
                                    }
                                }
                                else if (item.Name.Contains("CIL"))
                                {
                                    foreach (
                                        var p in cil_width!.Where(cil =>
                                            cil.PName!.EndsWith(item.Name.Replace("CIL", ""))
                                        )
                                    )
                                    {
                                        if (p.PName!.Contains("A"))
                                        {
                                            item.WidthA = p.PValue!.ToDouble();
                                            item.ForegroundA = GetColorFromCIL(
                                                Line1PageViewModel!.CoatingCCDSpecModel,
                                                item.WidthA
                                            );
                                        }
                                        else if (p.PName!.Contains("B"))
                                        {
                                            item.WidthB = p.PValue!.ToDouble();
                                            item.ForegroundB = GetColorFromCIL(
                                                Line1PageViewModel!.CoatingCCDSpecModel,
                                                item.WidthB
                                            );
                                        }
                                    }
                                }
                                else if (item.Name.Contains("Coating"))
                                {
                                    foreach (
                                        var p in coating_width!.Where(coating =>
                                            coating.PName!.EndsWith(
                                                item.Name.Replace("Coating", "")
                                            )
                                        )
                                    )
                                    {
                                        if (p.PName!.Contains("A"))
                                        {
                                            item.WidthA = p.PValue!.ToDouble();
                                            item.ForegroundA = GetColorFromCoating(
                                                Line1PageViewModel!.CoatingCCDSpecModel,
                                                item.WidthA
                                            );
                                        }
                                        else if (p.PName!.Contains("B"))
                                        {
                                            item.WidthB = p.PValue!.ToDouble();
                                            item.ForegroundB = GetColorFromCoating(
                                                Line1PageViewModel!.CoatingCCDSpecModel,
                                                item.WidthB
                                            );
                                        }
                                    }
                                }
                            }

                            // B面干膜数据解析
                            var ranksegs = RankSegments(
                                Line1PageViewModel!.CoatingCCDSpecModel,
                                tab_width!.Where(tab => tab.PName!.Contains("B干")).ToList(),
                                cil_width!.Where(cil => cil.PName!.Contains("B干")).ToList(),
                                coating_width!
                                    .Where(coating => coating.PName!.Contains("B干"))
                                    .ToList()
                            );
                            var cells = new ObservableCollection<CellData>()
                            {
                                new CellData
                                {
                                    Value = $"{getDataTime:HH:mm:ss}",
                                    Background = Brushes.Transparent,
                                    Foreground = Brushes.Black,
                                },
                            };
                            foreach (var item in ranksegs)
                            {
                                cells.Add(item);
                            }
                            if (Line1PageViewModel!.FaceBDryDataTableRows!.Count >= 3)
                                Line1PageViewModel!.FaceBDryDataTableRows!.RemoveAt(0);
                            Line1PageViewModel!.FaceBDryDataTableRows!.Add(
                                new RowData { Cells = cells }
                            );

                            // A干B干错位数据解析
                            var misalignCells = new ObservableCollection<CellData>()
                            {
                                new CellData
                                {
                                    Value = $"{getDataTime:HH:mm:ss}",
                                    Background = Brushes.Transparent,
                                    Foreground = Brushes.Black,
                                },
                            };
                            foreach (var item in misalign!)
                            {
                                misalignCells.Add(
                                    new CellData
                                    {
                                        Value = $"{item.PValue!.ToDouble():F2}",
                                        Foreground = GetColorFromMisalign(
                                            Line1PageViewModel!.CoatingCCDSpecModel,
                                            item.PValue!.ToDouble()
                                        ),
                                        Background = SetColorOpacity(
                                            GetColorFromMisalign(
                                                Line1PageViewModel!.CoatingCCDSpecModel,
                                                item.PValue!.ToDouble()
                                            ),
                                            0.5
                                        ),
                                    }
                                );
                            }
                            if (Line1PageViewModel!.FaceADryAndBDryDataTableRows!.Count >= 3)
                                Line1PageViewModel!.FaceADryAndBDryDataTableRows!.RemoveAt(0);
                            Line1PageViewModel!.FaceADryAndBDryDataTableRows!.Add(
                                new RowData { Cells = misalignCells }
                            );
                        }
                        catch (Exception ex)
                        {
                            _logger.Error("A干B干数据更新异常", ex);
                        }
                    });

                    break;
                }
                default:
                    break;
            }
            await Task.CompletedTask;
        }

        private async Task L2CCDGaugeServer_DataTypeJsonParse(
            string msgType,
            DateTime getDataTime,
            JsonElement dataJsonElement
        )
        {
            var tab_width = dataJsonElement
                .GetProperty("tab_width")
                .Deserialize<List<ParameterModel>>();
            var cil_width = dataJsonElement
                .GetProperty("cil_width")
                .Deserialize<List<ParameterModel>>();
            var coating_width = dataJsonElement
                .GetProperty("coating_width")
                .Deserialize<List<ParameterModel>>();

            var coating_total_width = dataJsonElement
                .GetProperty("coating_total_width")
                .GetDouble();

            var judge_res = dataJsonElement
                .GetProperty("judge_res")
                .Deserialize<List<JudgeResult>>();

            var tab_specification = dataJsonElement
                .GetProperty("tab_specification")
                .Deserialize<List<ParameterModel>>();

            foreach (var item in tab_specification!)
            {
                if (item.PName == "边缘极耳目标值")
                {
                    Line2PageViewModel!.CoatingCCDSpecModel.EdgeTabTargetValue =
                        item.PValue!.ToDouble();
                }
                else if (item.PName == "边缘极耳上限预警公差")
                {
                    Line2PageViewModel!.CoatingCCDSpecModel.EdgeTabWarnMax =
                        item.PValue!.ToDouble();
                }
                else if (item.PName == "边缘极耳下限预警公差")
                {
                    Line2PageViewModel!.CoatingCCDSpecModel.EdgeTabWarnMin =
                        item.PValue!.ToDouble();
                }
                else if (item.PName == "边缘极耳上限公差")
                {
                    Line2PageViewModel!.CoatingCCDSpecModel.EdgeTabLimitMax =
                        item.PValue!.ToDouble();
                }
                else if (item.PName == "边缘极耳下限公差")
                {
                    Line2PageViewModel!.CoatingCCDSpecModel.EdgeTabLimitMin =
                        item.PValue!.ToDouble();
                }
                else if (item.PName == "中间极耳目标值")
                {
                    Line2PageViewModel!.CoatingCCDSpecModel.MiddleTabTargetValue =
                        item.PValue!.ToDouble();
                }
                else if (item.PName == "中间极耳上限预警公差")
                {
                    Line2PageViewModel!.CoatingCCDSpecModel.MiddleTabWarnMax =
                        item.PValue!.ToDouble();
                }
                else if (item.PName == "中间极耳下限预警公差")
                {
                    Line2PageViewModel!.CoatingCCDSpecModel.MiddleTabWarnMin =
                        item.PValue!.ToDouble();
                }
                else if (item.PName == "中间极耳上限公差")
                {
                    Line2PageViewModel!.CoatingCCDSpecModel.MiddleTabLimitMax =
                        item.PValue!.ToDouble();
                }
                else if (item.PName == "中间极耳下限公差")
                {
                    Line2PageViewModel!.CoatingCCDSpecModel.MiddleTabLimitMin =
                        item.PValue!.ToDouble();
                }
            }

            var cil_specification = dataJsonElement
                .GetProperty("cil_specification")
                .Deserialize<List<ParameterModel>>();

            foreach (var item in cil_specification!)
            {
                if (item.PName == "陶瓷目标值")
                {
                    Line2PageViewModel!.CoatingCCDSpecModel.CILTargetValue =
                        item.PValue!.ToDouble();
                }
                else if (item.PName == "陶瓷上限预警公差")
                {
                    Line2PageViewModel!.CoatingCCDSpecModel.CILWarnMax = item.PValue!.ToDouble();
                }
                else if (item.PName == "陶瓷下限预警公差")
                {
                    Line2PageViewModel!.CoatingCCDSpecModel.CILWarnMin = item.PValue!.ToDouble();
                }
                else if (item.PName == "陶瓷上限公差")
                {
                    Line2PageViewModel!.CoatingCCDSpecModel.CILLimitMax = item.PValue!.ToDouble();
                }
                else if (item.PName == "陶瓷下限公差")
                {
                    Line2PageViewModel!.CoatingCCDSpecModel.CILLimitMin = item.PValue!.ToDouble();
                }
            }

            var coating_specification = dataJsonElement
                .GetProperty("coating_specification")
                .Deserialize<List<ParameterModel>>();

            foreach (var item in coating_specification!)
            {
                if (item.PName == "涂膜目标值")
                {
                    Line2PageViewModel!.CoatingCCDSpecModel.CoatingTargetValue =
                        item.PValue!.ToDouble();
                }
                else if (item.PName == "涂膜上限预警公差")
                {
                    Line2PageViewModel!.CoatingCCDSpecModel.CoatingWarnMax =
                        item.PValue!.ToDouble();
                }
                else if (item.PName == "涂膜下限预警公差")
                {
                    Line2PageViewModel!.CoatingCCDSpecModel.CoatingWarnMin =
                        item.PValue!.ToDouble();
                }
                else if (item.PName == "涂膜上限公差")
                {
                    Line2PageViewModel!.CoatingCCDSpecModel.CoatingLimitMax =
                        item.PValue!.ToDouble();
                }
                else if (item.PName == "涂膜下限公差")
                {
                    Line2PageViewModel!.CoatingCCDSpecModel.CoatingLimitMin =
                        item.PValue!.ToDouble();
                }
            }

            switch (msgType)
            {
                case "data_a_wet":
                {
                    break;
                }
                case "data_b_wet":
                {
                    var misalign = dataJsonElement
                        .GetProperty("misalign")
                        .Deserialize<List<ParameterModel>>();

                    var misalign_specification = dataJsonElement
                        .GetProperty("misalign_specification")
                        .Deserialize<List<ParameterModel>>();

                    foreach (var item in misalign_specification!)
                    {
                        if (item.PName == "错位目标值")
                        {
                            Line2PageViewModel!.CoatingCCDSpecModel.MisalignTargetValue =
                                item.PValue!.ToDouble();
                        }
                        else if (item.PName == "错位上限预警公差")
                        {
                            Line2PageViewModel!.CoatingCCDSpecModel.MisalignWarnMax =
                                item.PValue!.ToDouble();
                        }
                        else if (item.PName == "错位下限预警公差")
                        {
                            Line2PageViewModel!.CoatingCCDSpecModel.MisalignWarnMin =
                                item.PValue!.ToDouble();
                        }
                        else if (item.PName == "错位上限公差")
                        {
                            Line2PageViewModel!.CoatingCCDSpecModel.MisalignLimitMax =
                                item.PValue!.ToDouble();
                        }
                        else if (item.PName == "错位下限公差")
                        {
                            Line2PageViewModel!.CoatingCCDSpecModel.MisalignLimitMin =
                                item.PValue!.ToDouble();
                        }
                    }

                    RunOnUIThread(() =>
                    {
                        try
                        {
                            // A干B湿CCD区域显示数据解析
                            foreach (
                                var item in Line2PageViewModel!.FaceAPageViewModel!.RawSegments!
                            )
                            {
                                if (item.Name.Contains("Tab"))
                                {
                                    foreach (
                                        var p in tab_width!.Where(tab =>
                                            tab.PName!.EndsWith(item.Name.Replace("Tab", ""))
                                        )
                                    )
                                    {
                                        if (p.PName!.Contains("A"))
                                        {
                                            item.WidthA = p.PValue!.ToDouble();
                                            item.ForegroundA =
                                                item.Type == CoatingSegmentType.EdgeTab
                                                    ? GetColorFromEdgeTab(
                                                        Line2PageViewModel!.CoatingCCDSpecModel,
                                                        item.WidthA
                                                    )
                                                    : GetColorFromMiddleTab(
                                                        Line2PageViewModel!.CoatingCCDSpecModel,
                                                        item.WidthA
                                                    );
                                        }
                                        else if (p.PName!.Contains("B"))
                                        {
                                            item.WidthB = p.PValue!.ToDouble();
                                            item.ForegroundB =
                                                item.Type == CoatingSegmentType.EdgeTab
                                                    ? GetColorFromEdgeTab(
                                                        Line2PageViewModel!.CoatingCCDSpecModel,
                                                        item.WidthB
                                                    )
                                                    : GetColorFromMiddleTab(
                                                        Line2PageViewModel!.CoatingCCDSpecModel,
                                                        item.WidthB
                                                    );
                                        }
                                    }
                                }
                                else if (item.Name.Contains("CIL"))
                                {
                                    foreach (
                                        var p in cil_width!.Where(cil =>
                                            cil.PName!.EndsWith(item.Name.Replace("CIL", ""))
                                        )
                                    )
                                    {
                                        if (p.PName!.Contains("A"))
                                        {
                                            item.WidthA = p.PValue!.ToDouble();
                                            item.ForegroundA = GetColorFromCIL(
                                                Line2PageViewModel!.CoatingCCDSpecModel,
                                                item.WidthA
                                            );
                                        }
                                        else if (p.PName!.Contains("B"))
                                        {
                                            item.WidthB = p.PValue!.ToDouble();
                                            item.ForegroundB = GetColorFromCIL(
                                                Line2PageViewModel!.CoatingCCDSpecModel,
                                                item.WidthB
                                            );
                                        }
                                    }
                                }
                                else if (item.Name.Contains("Coating"))
                                {
                                    foreach (
                                        var p in coating_width!.Where(coating =>
                                            coating.PName!.EndsWith(
                                                item.Name.Replace("Coating", "")
                                            )
                                        )
                                    )
                                    {
                                        if (p.PName!.Contains("A"))
                                        {
                                            item.WidthA = p.PValue!.ToDouble();
                                            item.ForegroundA = GetColorFromCoating(
                                                Line2PageViewModel!.CoatingCCDSpecModel,
                                                item.WidthA
                                            );
                                        }
                                        else if (p.PName!.Contains("B"))
                                        {
                                            item.WidthB = p.PValue!.ToDouble();
                                            item.ForegroundB = GetColorFromCoating(
                                                Line2PageViewModel!.CoatingCCDSpecModel,
                                                item.WidthB
                                            );
                                        }
                                    }
                                }
                            }

                            // A干CCD区域显示数据解析
                            foreach (
                                var item in Line2PageViewModel!.FaceBPageViewModel!.RawSegments!
                            )
                            {
                                if (item.Name.Contains("Tab"))
                                {
                                    foreach (
                                        var p in tab_width!.Where(tab =>
                                            tab.PName!.EndsWith(item.Name.Replace("Tab", ""))
                                        )
                                    )
                                    {
                                        if (p.PName!.Contains("A"))
                                        {
                                            item.WidthA = p.PValue!.ToDouble();
                                            item.ForegroundA =
                                                item.Type == CoatingSegmentType.EdgeTab
                                                    ? GetColorFromEdgeTab(
                                                        Line2PageViewModel!.CoatingCCDSpecModel,
                                                        item.WidthA
                                                    )
                                                    : GetColorFromMiddleTab(
                                                        Line2PageViewModel!.CoatingCCDSpecModel,
                                                        item.WidthA
                                                    );
                                        }
                                    }
                                }
                                else if (item.Name.Contains("CIL"))
                                {
                                    foreach (
                                        var p in cil_width!.Where(cil =>
                                            cil.PName!.EndsWith(item.Name.Replace("CIL", ""))
                                        )
                                    )
                                    {
                                        if (p.PName!.Contains("A"))
                                        {
                                            item.WidthA = p.PValue!.ToDouble();
                                            item.ForegroundA = GetColorFromCIL(
                                                Line2PageViewModel!.CoatingCCDSpecModel,
                                                item.WidthA
                                            );
                                        }
                                    }
                                }
                                else if (item.Name.Contains("Coating"))
                                {
                                    foreach (
                                        var p in coating_width!.Where(coating =>
                                            coating.PName!.EndsWith(
                                                item.Name.Replace("Coating", "")
                                            )
                                        )
                                    )
                                    {
                                        if (p.PName!.Contains("A"))
                                        {
                                            item.WidthA = p.PValue!.ToDouble();
                                            item.ForegroundA = GetColorFromCoating(
                                                Line2PageViewModel!.CoatingCCDSpecModel,
                                                item.WidthA
                                            );
                                        }
                                    }
                                }
                            }

                            // A面干膜数据解析
                            var ranksegs = RankSegments(
                                Line2PageViewModel!.CoatingCCDSpecModel,
                                tab_width!.Where(tab => tab.PName!.Contains("A干")).ToList(),
                                cil_width!.Where(cil => cil.PName!.Contains("A干")).ToList(),
                                coating_width!
                                    .Where(coating => coating.PName!.Contains("A干"))
                                    .ToList()
                            );
                            var cells = new ObservableCollection<CellData>()
                            {
                                new CellData
                                {
                                    Value = $"{getDataTime:HH:mm:ss}",
                                    Background = Brushes.Transparent,
                                    Foreground = Brushes.Black,
                                },
                            };
                            foreach (var item in ranksegs)
                            {
                                cells.Add(item);
                            }
                            if (Line2PageViewModel!.FaceADryDataTableRows!.Count >= 3)
                                Line2PageViewModel!.FaceADryDataTableRows!.RemoveAt(0);
                            Line2PageViewModel!.FaceADryDataTableRows!.Add(
                                new RowData { Cells = cells }
                            );

                            // B面湿膜数据解析
                            ranksegs = RankSegments(
                                Line2PageViewModel!.CoatingCCDSpecModel,
                                tab_width!.Where(tab => tab.PName!.Contains("B湿")).ToList(),
                                cil_width!.Where(cil => cil.PName!.Contains("B湿")).ToList(),
                                coating_width!
                                    .Where(coating => coating.PName!.Contains("B湿"))
                                    .ToList()
                            );
                            cells =
                            [
                                new CellData
                                {
                                    Value = $"{getDataTime:HH:mm:ss}",
                                    Background = Brushes.Transparent,
                                    Foreground = Brushes.Black,
                                },
                                .. ranksegs,
                            ];
                            if (Line2PageViewModel!.FaceBWetDataTableRows!.Count >= 3)
                                Line2PageViewModel!.FaceBWetDataTableRows!.RemoveAt(0);
                            Line2PageViewModel!.FaceBWetDataTableRows!.Add(
                                new RowData { Cells = cells }
                            );

                            // A干B湿错位数据解析
                            var misalignCells = new ObservableCollection<CellData>()
                            {
                                new CellData
                                {
                                    Value = $"{getDataTime:HH:mm:ss}",
                                    Background = Brushes.Transparent,
                                    Foreground = Brushes.Black,
                                },
                            };
                            foreach (var item in misalign!)
                            {
                                misalignCells.Add(
                                    new CellData
                                    {
                                        Value = $"{item.PValue!.ToDouble():F2}",
                                        Foreground = GetColorFromMisalign(
                                            Line2PageViewModel!.CoatingCCDSpecModel,
                                            item.PValue!.ToDouble()
                                        ),
                                        Background = SetColorOpacity(
                                            GetColorFromMisalign(
                                                Line2PageViewModel!.CoatingCCDSpecModel,
                                                item.PValue!.ToDouble()
                                            ),
                                            0.5
                                        ),
                                    }
                                );
                            }
                            if (Line2PageViewModel!.FaceADryAndBWetDataTableRows!.Count >= 3)
                                Line2PageViewModel!.FaceADryAndBWetDataTableRows!.RemoveAt(0);
                            Line2PageViewModel!.FaceADryAndBWetDataTableRows!.Add(
                                new RowData { Cells = misalignCells }
                            );
                        }
                        catch (Exception ex)
                        {
                            _logger.Error("A干B湿数据更新异常", ex);
                            throw;
                        }
                    });

                    break;
                }

                case "data_b_dry":
                {
                    var misalign = dataJsonElement
                        .GetProperty("misalign")
                        .Deserialize<List<ParameterModel>>();
                    var misalign_specification = dataJsonElement
                        .GetProperty("misalign_specification")
                        .Deserialize<List<ParameterModel>>();

                    foreach (var item in misalign_specification!)
                    {
                        if (item.PName == "错位目标值")
                        {
                            Line2PageViewModel!.CoatingCCDSpecModel.MisalignTargetValue =
                                item.PValue!.ToDouble();
                        }
                        else if (item.PName == "错位上限预警公差")
                        {
                            Line2PageViewModel!.CoatingCCDSpecModel.MisalignWarnMax =
                                item.PValue!.ToDouble();
                        }
                        else if (item.PName == "错位下限预警公差")
                        {
                            Line2PageViewModel!.CoatingCCDSpecModel.MisalignWarnMin =
                                item.PValue!.ToDouble();
                        }
                        else if (item.PName == "错位上限公差")
                        {
                            Line2PageViewModel!.CoatingCCDSpecModel.MisalignLimitMax =
                                item.PValue!.ToDouble();
                        }
                        else if (item.PName == "错位下限公差")
                        {
                            Line2PageViewModel!.CoatingCCDSpecModel.MisalignLimitMin =
                                item.PValue!.ToDouble();
                        }
                    }

                    RunOnUIThread(() =>
                    {
                        try
                        {
                            // B干CCD区域显示数据解析
                            foreach (
                                var item in Line2PageViewModel!.FaceBPageViewModel!.RawSegments!
                            )
                            {
                                if (item.Name.Contains("Tab"))
                                {
                                    foreach (
                                        var p in tab_width!.Where(tab =>
                                            tab.PName!.EndsWith(item.Name.Replace("Tab", ""))
                                        )
                                    )
                                    {
                                        if (p.PName!.Contains("B"))
                                        {
                                            item.WidthB = p.PValue!.ToDouble();
                                            item.ForegroundB =
                                                item.Type == CoatingSegmentType.EdgeTab
                                                    ? GetColorFromEdgeTab(
                                                        Line2PageViewModel!.CoatingCCDSpecModel,
                                                        item.WidthB
                                                    )
                                                    : GetColorFromMiddleTab(
                                                        Line2PageViewModel!.CoatingCCDSpecModel,
                                                        item.WidthB
                                                    );
                                        }
                                    }
                                }
                                else if (item.Name.Contains("CIL"))
                                {
                                    foreach (
                                        var p in cil_width!.Where(cil =>
                                            cil.PName!.EndsWith(item.Name.Replace("CIL", ""))
                                        )
                                    )
                                    {
                                        if (p.PName!.Contains("B"))
                                        {
                                            item.WidthB = p.PValue!.ToDouble();
                                            item.ForegroundB = GetColorFromCIL(
                                                Line2PageViewModel!.CoatingCCDSpecModel,
                                                item.WidthB
                                            );
                                        }
                                    }
                                }
                                else if (item.Name.Contains("Coating"))
                                {
                                    foreach (
                                        var p in coating_width!.Where(coating =>
                                            coating.PName!.EndsWith(
                                                item.Name.Replace("Coating", "")
                                            )
                                        )
                                    )
                                    {
                                        if (p.PName!.Contains("B"))
                                        {
                                            item.WidthB = p.PValue!.ToDouble();
                                            item.ForegroundB = GetColorFromCoating(
                                                Line2PageViewModel!.CoatingCCDSpecModel,
                                                item.WidthB
                                            );
                                        }
                                    }
                                }
                            }

                            // B面干膜数据解析
                            var ranksegs = RankSegments(
                                Line2PageViewModel!.CoatingCCDSpecModel,
                                tab_width!.Where(tab => tab.PName!.Contains("B干")).ToList(),
                                cil_width!.Where(cil => cil.PName!.Contains("B干")).ToList(),
                                coating_width!
                                    .Where(coating => coating.PName!.Contains("B干"))
                                    .ToList()
                            );
                            var cells = new ObservableCollection<CellData>()
                            {
                                new CellData
                                {
                                    Value = $"{getDataTime:HH:mm:ss}",
                                    Background = Brushes.Transparent,
                                    Foreground = Brushes.Black,
                                },
                            };
                            foreach (var item in ranksegs)
                            {
                                cells.Add(item);
                            }
                            if (Line2PageViewModel!.FaceBDryDataTableRows!.Count >= 3)
                                Line2PageViewModel!.FaceBDryDataTableRows!.RemoveAt(0);
                            Line2PageViewModel!.FaceBDryDataTableRows!.Add(
                                new RowData { Cells = cells }
                            );

                            // A干B干错位数据解析
                            var misalignCells = new ObservableCollection<CellData>()
                            {
                                new CellData
                                {
                                    Value = $"{getDataTime:HH:mm:ss}",
                                    Background = Brushes.Transparent,
                                    Foreground = Brushes.Black,
                                },
                            };
                            foreach (var item in misalign!)
                            {
                                misalignCells.Add(
                                    new CellData
                                    {
                                        Value = $"{item.PValue!.ToDouble():F2}",
                                        Foreground = GetColorFromMisalign(
                                            Line2PageViewModel!.CoatingCCDSpecModel,
                                            item.PValue!.ToDouble()
                                        ),
                                        Background = SetColorOpacity(
                                            GetColorFromMisalign(
                                                Line2PageViewModel!.CoatingCCDSpecModel,
                                                item.PValue!.ToDouble()
                                            ),
                                            0.5
                                        ),
                                    }
                                );
                            }
                            if (Line2PageViewModel!.FaceADryAndBDryDataTableRows!.Count >= 3)
                                Line2PageViewModel!.FaceADryAndBDryDataTableRows!.RemoveAt(0);
                            Line2PageViewModel!.FaceADryAndBDryDataTableRows!.Add(
                                new RowData { Cells = misalignCells }
                            );
                        }
                        catch (Exception ex)
                        {
                            _logger.Error("A干B干数据更新异常", ex);
                            throw;
                        }
                    });
                    break;
                }
                default:
                    break;
            }
            await Task.CompletedTask;
        }

        private async Task L1PLCServer_DataTypeJsonParse(
            string msgType,
            DateTime getDataTime,
            JsonElement dataJsonElement
        )
        {
            var lineName = dataJsonElement.GetProperty("LineName").GetString();
            var ip = dataJsonElement.GetProperty("IP").GetString();
            var deviceSn = dataJsonElement.GetProperty("DeviceSn").GetString();
            var plcParameterModelList = dataJsonElement
                .GetProperty("PLCParameterModelList")
                .Deserialize<List<PLCParameterModel>>();
            Line1PageViewModel!
                .DeviceInfos!.FirstOrDefault(item => item.PName == "拉线名")!
                .PValue = $"{lineName}";
            Line1PageViewModel!
                .DeviceInfos!.FirstOrDefault(item => item.PName == "设备IP")!
                .PValue = $"{ip}";
            Line1PageViewModel!
                .DeviceInfos!.FirstOrDefault(item => item.PName == "设备编号")!
                .PValue = $"{deviceSn}";

            if (plcParameterModelList != null)
            {
                var systemVelocity = plcParameterModelList.FirstOrDefault(model =>
                    model.PName == "OPMM_系统运行实时速度"
                );
                if (systemVelocity != null)
                {
                    Line1PageViewModel!
                        .DeviceInfos!.FirstOrDefault(item => item.PName == "涂布实时速度")!
                        .PValue =
                        $"{DataTypeConverter.StringToValue(systemVelocity.PType!, systemVelocity.PValue!)}";
                }

                foreach (var item in Line1PageViewModel!.GeneralParameterInfos!)
                {
                    var m = plcParameterModelList.FirstOrDefault(model =>
                        model.PName == $"OPMM_{item.PName}"
                    );
                    if (m != null)
                    {
                        item.PValue = $"{DataTypeConverter.StringToValue(m.PType!, m.PValue!):F1}";
                    }
                }

                plcParameterModelList.ForEach(model =>
                {
                    var res = Line1PageViewModel!.OldPLCParameterMonitor.FirstOrDefault(m =>
                        m.PName == model.PName && m.PType == "bool"
                    );
                    if (res != null)
                    {
                        if (
                            DataTypeConverter.StringToValue(res.PType!, res.PValue!) == false
                            && DataTypeConverter.StringToValue(model.PType!, model.PValue!) == true
                        )
                        {
                            RunOnUIThread(() =>
                            {
                                Line1PageViewModel!.AddAlarmInfo(
                                    new AlarmInfo
                                    {
                                        AlarmTime = DateTime.Now,
                                        AlarmObject = "",
                                        AlarmType = "",
                                        AlarmDetail = $"{model.PName!.Replace("OPMM_", "")}",
                                        AlarmLevel = AlarmLevel.Warning,
                                    }
                                );
                            });

                            switch (model.PName)
                            {
                                case "OPMM_模头未在前进位，禁止标定、调刀":
                                case "OPMM_机头A模头未标定，禁止执行调刀":
                                case "OPMM_A1900：系统运行请求禁止，机头A基准辊压辊未压着":
                                case "OPMM_A1901：系统运行请求禁止，机头A模头推动气缸未在前进位或后退位":
                                case "OPMM_A1902：系统运行请求禁止，机头A基准辊驱动器故障":
                                case "OPMM_A1903：系统运行请求禁止，机头A纠偏系统故障":
                                case "OPMM_机头B模头未在前进位，禁止标定、调刀":
                                case "OPMM_机头B模头未标定，禁止执行调刀":
                                case "OPMM_机头B浆料缓存罐液位感应器异常":
                                case "OPMM_机头B_AT9料缓存罐液位感应器异常":
                                    _logger.Error(
                                        $"L1 PLC报警：{model.PName.Replace("OPMM_", "")}"
                                    );

                                    RunOnUIThread(() =>
                                    {
                                        Dialog
                                            .Show(new VM_AlarmInfoDialog().UIElement)
                                            .Initialize<VM_AlarmInfoDialog>(vm =>
                                            {
                                                vm.AlarmLevel = "严重";
                                                vm.AlarmZone = "L1 涂布机";
                                                vm.AlarmInfo =
                                                    $"{model.PName.Replace("OPMM_", "")}";
                                                vm.MainFontSize =
                                                    Line1PageViewModel.LineTitleFontSize;
                                            })
                                            .GetResultAsync<bool>();
                                    });
                                    break;
                                default:
                                    _logger.Warn(
                                        $"L1 PLC报警：{model.PName!.Replace("OPMM_", "")}"
                                    );
                                    break;
                            }
                        }
                    }
                    else
                    {
                        if (
                            model.PType == "bool"
                            && DataTypeConverter.StringToValue(model.PType!, model.PValue!) == true
                        )
                        {
                            RunOnUIThread(() =>
                            {
                                Line1PageViewModel!.AddAlarmInfo(
                                    new AlarmInfo
                                    {
                                        AlarmTime = DateTime.Now,
                                        AlarmObject = "",
                                        AlarmType = "",
                                        AlarmDetail = $"{model.PName!.Replace("OPMM_", "")}",
                                        AlarmLevel = AlarmLevel.Warning,
                                    }
                                );
                            });

                            switch (model.PName)
                            {
                                case "OPMM_模头未在前进位，禁止标定、调刀":
                                case "OPMM_机头A模头未标定，禁止执行调刀":
                                case "OPMM_A1900：系统运行请求禁止，机头A基准辊压辊未压着":
                                case "OPMM_A1901：系统运行请求禁止，机头A模头推动气缸未在前进位或后退位":
                                case "OPMM_A1902：系统运行请求禁止，机头A基准辊驱动器故障":
                                case "OPMM_A1903：系统运行请求禁止，机头A纠偏系统故障":
                                case "OPMM_机头B模头未在前进位，禁止标定、调刀":
                                case "OPMM_机头B模头未标定，禁止执行调刀":
                                case "OPMM_机头B浆料缓存罐液位感应器异常":
                                case "OPMM_机头B_AT9料缓存罐液位感应器异常":
                                    _logger.Error(
                                        $"L1 PLC报警：{model.PName.Replace("OPMM_", "")}"
                                    );

                                    RunOnUIThread(() =>
                                    {
                                        Dialog
                                            .Show(new VM_AlarmInfoDialog().UIElement)
                                            .Initialize<VM_AlarmInfoDialog>(vm =>
                                            {
                                                vm.AlarmLevel = "严重";
                                                vm.AlarmZone = "L1 涂布机";
                                                vm.AlarmInfo =
                                                    $"{model.PName.Replace("OPMM_", "")}";
                                                vm.MainFontSize =
                                                    Line1PageViewModel.LineTitleFontSize;
                                            })
                                            .GetResultAsync<bool>();
                                    });
                                    break;
                                default:
                                    _logger.Warn(
                                        $"L1 PLC报警：{model.PName!.Replace("OPMM_", "")}"
                                    );
                                    break;
                            }
                        }
                    }
                });

                plcParameterModelList.ForEach(model =>
                {
                    var res = Line1PageViewModel!.OldPLCParameterMonitor.FirstOrDefault(m =>
                        m.PName == model.PName
                    );
                    if (res != null)
                    {
                        res.PValue = model.PValue;
                    }
                    else
                    {
                        Line1PageViewModel!.OldPLCParameterMonitor.Add(
                            new PLCParameterModel()
                            {
                                PName = model.PName,
                                PType = model.PType,
                                PValue = model.PValue,
                            }
                        );
                    }
                });
            }

            await Task.CompletedTask;
        }

        private async Task L2PLCServer_DataTypeJsonParse(
            string msgType,
            DateTime getDataTime,
            JsonElement dataJsonElement
        )
        {
            var lineName = dataJsonElement.GetProperty("LineName").GetString();
            var ip = dataJsonElement.GetProperty("IP").GetString();
            var deviceSn = dataJsonElement.GetProperty("DeviceSn").GetString();
            var plcParameterModelList = dataJsonElement
                .GetProperty("PLCParameterModelList")
                .Deserialize<List<PLCParameterModel>>();
            Line2PageViewModel!
                .DeviceInfos!.FirstOrDefault(item => item.PName == "拉线名")!
                .PValue = $"{lineName}";
            Line2PageViewModel!
                .DeviceInfos!.FirstOrDefault(item => item.PName == "设备IP")!
                .PValue = $"{ip}";
            Line2PageViewModel!
                .DeviceInfos!.FirstOrDefault(item => item.PName == "设备编号")!
                .PValue = $"{deviceSn}";

            if (plcParameterModelList != null)
            {
                var systemVelocity = plcParameterModelList.FirstOrDefault(model =>
                    model.PName == "OPMM_系统运行实时速度"
                );
                if (systemVelocity != null)
                {
                    Line2PageViewModel!
                        .DeviceInfos!.FirstOrDefault(item => item.PName == "涂布实时速度")!
                        .PValue =
                        $"{DataTypeConverter.StringToValue(systemVelocity.PType!, systemVelocity.PValue!)}";
                }

                foreach (var item in Line2PageViewModel!.GeneralParameterInfos!)
                {
                    var m = plcParameterModelList.FirstOrDefault(model =>
                        model.PName == $"OPMM_{item.PName}"
                    );
                    if (m != null)
                    {
                        item.PValue = $"{DataTypeConverter.StringToValue(m.PType!, m.PValue!):F1}";
                    }
                }

                plcParameterModelList.ForEach(model =>
                {
                    var res = Line2PageViewModel!.OldPLCParameterMonitor.FirstOrDefault(m =>
                        m.PName == model.PName && m.PType == "bool"
                    );
                    if (res != null)
                    {
                        if (
                            DataTypeConverter.StringToValue(res.PType!, res.PValue!) == false
                            && DataTypeConverter.StringToValue(model.PType!, model.PValue!) == true
                        )
                        {
                            RunOnUIThread(() =>
                            {
                                Line2PageViewModel!.AddAlarmInfo(
                                    new AlarmInfo
                                    {
                                        AlarmTime = DateTime.Now,
                                        AlarmObject = "",
                                        AlarmType = "",
                                        AlarmDetail = $"{model.PName!.Replace("OPMM_", "")}",
                                        AlarmLevel = AlarmLevel.Warning,
                                    }
                                );
                            });

                            switch (model.PName)
                            {
                                case "OPMM_模头未在前进位，禁止标定、调刀":
                                case "OPMM_机头A模头未标定，禁止执行调刀":
                                case "OPMM_A1900：系统运行请求禁止，机头A基准辊压辊未压着":
                                case "OPMM_A1901：系统运行请求禁止，机头A模头推动气缸未在前进位或后退位":
                                case "OPMM_A1902：系统运行请求禁止，机头A基准辊驱动器故障":
                                case "OPMM_A1903：系统运行请求禁止，机头A纠偏系统故障":
                                case "OPMM_机头B模头未在前进位，禁止标定、调刀":
                                case "OPMM_机头B模头未标定，禁止执行调刀":
                                case "OPMM_机头B浆料缓存罐液位感应器异常":
                                case "OPMM_机头B_AT9料缓存罐液位感应器异常":
                                    _logger.Error(
                                        $"L2 PLC报警：{model.PName.Replace("OPMM_", "")}"
                                    );

                                    RunOnUIThread(() =>
                                    {
                                        Dialog
                                            .Show(new VM_AlarmInfoDialog().UIElement)
                                            .Initialize<VM_AlarmInfoDialog>(vm =>
                                            {
                                                vm.AlarmLevel = "严重";
                                                vm.AlarmZone = "L1 涂布机";
                                                vm.AlarmInfo =
                                                    $"{model.PName.Replace("OPMM_", "")}";
                                                vm.MainFontSize =
                                                    Line2PageViewModel.LineTitleFontSize;
                                            })
                                            .GetResultAsync<bool>();
                                    });
                                    break;
                                default:
                                    _logger.Warn(
                                        $"L2 PLC报警：{model.PName!.Replace("OPMM_", "")}"
                                    );
                                    break;
                            }
                        }
                    }
                    else
                    {
                        if (
                            model.PType == "bool"
                            && DataTypeConverter.StringToValue(model.PType!, model.PValue!) == true
                        )
                        {
                            RunOnUIThread(() =>
                            {
                                Line2PageViewModel!.AddAlarmInfo(
                                    new AlarmInfo
                                    {
                                        AlarmTime = DateTime.Now,
                                        AlarmObject = "",
                                        AlarmType = "",
                                        AlarmDetail = $"{model.PName!.Replace("OPMM_", "")}",
                                        AlarmLevel = AlarmLevel.Warning,
                                    }
                                );
                            });

                            switch (model.PName)
                            {
                                case "OPMM_模头未在前进位，禁止标定、调刀":
                                case "OPMM_机头A模头未标定，禁止执行调刀":
                                case "OPMM_A1900：系统运行请求禁止，机头A基准辊压辊未压着":
                                case "OPMM_A1901：系统运行请求禁止，机头A模头推动气缸未在前进位或后退位":
                                case "OPMM_A1902：系统运行请求禁止，机头A基准辊驱动器故障":
                                case "OPMM_A1903：系统运行请求禁止，机头A纠偏系统故障":
                                case "OPMM_机头B模头未在前进位，禁止标定、调刀":
                                case "OPMM_机头B模头未标定，禁止执行调刀":
                                case "OPMM_机头B浆料缓存罐液位感应器异常":
                                case "OPMM_机头B_AT9料缓存罐液位感应器异常":
                                    _logger.Error(
                                        $"L2 PLC报警：{model.PName.Replace("OPMM_", "")}"
                                    );

                                    RunOnUIThread(() =>
                                    {
                                        Dialog
                                            .Show(new VM_AlarmInfoDialog().UIElement)
                                            .Initialize<VM_AlarmInfoDialog>(vm =>
                                            {
                                                vm.AlarmLevel = "严重";
                                                vm.AlarmZone = "L1 涂布机";
                                                vm.AlarmInfo =
                                                    $"{model.PName.Replace("OPMM_", "")}";
                                                vm.MainFontSize =
                                                    Line2PageViewModel.LineTitleFontSize;
                                            })
                                            .GetResultAsync<bool>();
                                    });
                                    break;
                                default:
                                    _logger.Warn(
                                        $"L2 PLC报警：{model.PName!.Replace("OPMM_", "")}"
                                    );
                                    break;
                            }
                        }
                    }
                });

                plcParameterModelList.ForEach(model =>
                {
                    var res = Line2PageViewModel!.OldPLCParameterMonitor.FirstOrDefault(m =>
                        m.PName == model.PName
                    );
                    if (res != null)
                    {
                        res.PValue = model.PValue;
                    }
                    else
                    {
                        Line2PageViewModel!.OldPLCParameterMonitor.Add(
                            new PLCParameterModel()
                            {
                                PName = model.PName,
                                PType = model.PType,
                                PValue = model.PValue,
                            }
                        );
                    }
                });
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 规则：
        ///   极耳0, [陶瓷], 碳膜, [陶瓷], 极耳1, [陶瓷], 碳膜, [陶瓷], 极耳2, ...
        /// 约束：
        ///   1) 陶瓷可选：不足时跳过；多余可顺延到末尾。
        ///   2) 碳膜必须有：每个极耳间隙都必须至少 1 片碳膜；
        ///      若碳膜数量 < (极耳数-1) 则抛出异常。
        ///   3) 极耳必须有：碳膜数量 = (极耳数-1)。
        /// </summary>
        public List<CellData> RankSegments(
            CoatingCCDSpecModel coatingCCDSpecModel,
            IList<ParameterModel> tabs, // 极耳（必填）
            IList<ParameterModel> cils, // 陶瓷（可选）
            IList<ParameterModel> coatings // 碳膜（必填：每个间隙 1 片）
        )
        {
            if (tabs is null || coatings is null || cils is null)
                throw new ArgumentNullException("输入列表不能为 null。");

            if (tabs.Count == 0)
                throw new ArgumentException("极耳列表不能为空。");

            if (coatings.Count == 0)
                throw new ArgumentException("碳膜列表不能为空。");

            var result = new List<CellData>();
            int gaps = tabs.Count - 1;

            // 必须 gaps 片碳膜
            if (gaps > 0 && coatings.Count != gaps)
                throw new ArgumentException(
                    $"碳膜数量错误：需要={gaps}，实际为 {coatings.Count}。"
                );
            // 存在cils时，必须是2倍的gaps
            if (gaps > 0 && cils.Count != gaps * 2)
                throw new ArgumentException(
                    $"陶瓷数量错误：需要={gaps * 2}，实际为 {cils.Count}。"
                );

            int ci = 0; // 陶瓷索引
            int bi = 0; // 碳膜索引

            // 放第一个极耳
            result.Add(
                new CellData
                {
                    Value = tabs[0].PValue!,
                    Foreground = GetColorFromEdgeTab(
                        coatingCCDSpecModel,
                        tabs[0].PValue!.ToDouble()
                    ),
                    Background = SetColorOpacity(
                        GetColorFromEdgeTab(coatingCCDSpecModel, tabs[0].PValue!.ToDouble()),
                        0.5
                    ),
                }
            );

            // 针对每个“极耳间隙”插入：可选陶瓷 → 必需碳膜 → 可选陶瓷 → 下一个极耳
            for (int g = 0; g < gaps; g++)
            {
                if (ci < cils.Count)
                {
                    result.Add(
                        new CellData
                        {
                            Value = cils[ci].PValue!,
                            Foreground = GetColorFromCIL(
                                coatingCCDSpecModel,
                                cils[ci].PValue!.ToDouble()
                            ),
                            Background = SetColorOpacity(
                                GetColorFromCIL(coatingCCDSpecModel, cils[ci].PValue!.ToDouble()),
                                0.5
                            ),
                        }
                    ); // 可选陶瓷1
                    ci++;
                }

                result.Add(
                    new CellData
                    {
                        Value = coatings[bi].PValue!,
                        Foreground = GetColorFromCoating(
                            coatingCCDSpecModel,
                            coatings[bi].PValue!.ToDouble()
                        ),
                        Background = SetColorOpacity(
                            GetColorFromCoating(
                                coatingCCDSpecModel,
                                coatings[bi].PValue!.ToDouble()
                            ),
                            0.5
                        ),
                    }
                ); // 必需碳膜
                bi++;

                if (ci < cils.Count)
                {
                    result.Add(
                        new CellData
                        {
                            Value = cils[ci].PValue!,
                            Foreground = GetColorFromCIL(
                                coatingCCDSpecModel,
                                cils[ci].PValue!.ToDouble()
                            ),
                            Background = SetColorOpacity(
                                GetColorFromCIL(coatingCCDSpecModel, cils[ci].PValue!.ToDouble()),
                                0.5
                            ),
                        }
                    ); // 可选陶瓷2
                    ci++;
                }

                if (g + 1 == gaps)
                {
                    result.Add(
                        new CellData
                        {
                            Value = tabs[g + 1].PValue!,
                            Foreground = GetColorFromEdgeTab(
                                coatingCCDSpecModel,
                                tabs[g + 1].PValue!.ToDouble()
                            ),
                            Background = SetColorOpacity(
                                GetColorFromEdgeTab(
                                    coatingCCDSpecModel,
                                    tabs[g + 1].PValue!.ToDouble()
                                ),
                                0.5
                            ),
                        }
                    ); // 最后一个极耳
                }
                else
                {
                    result.Add(
                        new CellData
                        {
                            Value = tabs[g + 1].PValue!,
                            Foreground = GetColorFromMiddleTab(
                                coatingCCDSpecModel,
                                tabs[g + 1].PValue!.ToDouble()
                            ),
                            Background = SetColorOpacity(
                                GetColorFromMiddleTab(
                                    coatingCCDSpecModel,
                                    tabs[g + 1].PValue!.ToDouble()
                                ),
                                0.5
                            ),
                        }
                    ); // 下一个极耳
                }
            }

            return result;
        }

        private Brush SetColorOpacity(Brush color, double opacity)
        {
            color.Opacity = opacity;
            return color;
        }

        private Brush GetColorFromEdgeTab(CoatingCCDSpecModel coatingCCDSpecModel, double valueNum)
        {
            var transparent = new SolidColorBrush(Colors.Transparent);
            if (
                valueNum
                    > coatingCCDSpecModel.EdgeTabTargetValue + coatingCCDSpecModel.EdgeTabLimitMax
                || valueNum
                    <= coatingCCDSpecModel.EdgeTabTargetValue + coatingCCDSpecModel.EdgeTabLimitMin
            )
                return new SolidColorBrush(Color.FromRgb(255, 51, 51));
            if (
                (
                    valueNum
                        <= coatingCCDSpecModel.EdgeTabTargetValue
                            + coatingCCDSpecModel.EdgeTabLimitMax
                    && valueNum
                        > coatingCCDSpecModel.EdgeTabTargetValue
                            + coatingCCDSpecModel.EdgeTabWarnMax
                )
                || (
                    valueNum
                        <= coatingCCDSpecModel.EdgeTabTargetValue
                            + coatingCCDSpecModel.EdgeTabWarnMin
                    && valueNum
                        > coatingCCDSpecModel.EdgeTabTargetValue
                            + coatingCCDSpecModel.EdgeTabLimitMin
                )
            )
                return new SolidColorBrush(Color.FromRgb(255, 184, 51));
            if (
                valueNum
                    <= coatingCCDSpecModel.EdgeTabTargetValue + coatingCCDSpecModel.EdgeTabWarnMax
                && valueNum
                    > coatingCCDSpecModel.EdgeTabTargetValue + coatingCCDSpecModel.EdgeTabWarnMin
            )
                return new SolidColorBrush(Color.FromRgb(41, 204, 122));

            return transparent;
        }

        private Brush GetColorFromMiddleTab(
            CoatingCCDSpecModel coatingCCDSpecModel,
            double valueNum
        )
        {
            var transparent = new SolidColorBrush(Colors.Transparent);
            if (
                valueNum
                    > coatingCCDSpecModel.MiddleTabTargetValue
                        + coatingCCDSpecModel.MiddleTabLimitMax
                || valueNum
                    <= coatingCCDSpecModel.MiddleTabTargetValue
                        + coatingCCDSpecModel.MiddleTabLimitMin
            )
                return new SolidColorBrush(Color.FromRgb(255, 51, 51));
            if (
                (
                    valueNum
                        <= coatingCCDSpecModel.MiddleTabTargetValue
                            + coatingCCDSpecModel.MiddleTabLimitMax
                    && valueNum
                        > coatingCCDSpecModel.MiddleTabTargetValue
                            + coatingCCDSpecModel.MiddleTabWarnMax
                )
                || (
                    valueNum
                        <= coatingCCDSpecModel.MiddleTabTargetValue
                            + coatingCCDSpecModel.MiddleTabWarnMin
                    && valueNum
                        > coatingCCDSpecModel.MiddleTabTargetValue
                            + coatingCCDSpecModel.MiddleTabLimitMin
                )
            )
                return new SolidColorBrush(Color.FromRgb(255, 184, 51));
            if (
                valueNum
                    <= coatingCCDSpecModel.MiddleTabTargetValue
                        + coatingCCDSpecModel.MiddleTabWarnMax
                && valueNum
                    > coatingCCDSpecModel.MiddleTabTargetValue
                        + coatingCCDSpecModel.MiddleTabWarnMin
            )
                return new SolidColorBrush(Color.FromRgb(41, 204, 122));

            return transparent;
        }

        private Brush GetColorFromCIL(CoatingCCDSpecModel coatingCCDSpecModel, double valueNum)
        {
            var transparent = new SolidColorBrush(Colors.Transparent);
            if (
                valueNum > coatingCCDSpecModel.CILTargetValue + coatingCCDSpecModel.CILLimitMax
                || valueNum <= coatingCCDSpecModel.CILTargetValue + coatingCCDSpecModel.CILLimitMin
            )
                return new SolidColorBrush(Color.FromRgb(255, 51, 51));
            if (
                (
                    valueNum <= coatingCCDSpecModel.CILTargetValue + coatingCCDSpecModel.CILLimitMax
                    && valueNum
                        > coatingCCDSpecModel.CILTargetValue + coatingCCDSpecModel.CILWarnMax
                )
                || (
                    valueNum <= coatingCCDSpecModel.CILTargetValue + coatingCCDSpecModel.CILWarnMin
                    && valueNum
                        > coatingCCDSpecModel.CILTargetValue + coatingCCDSpecModel.CILLimitMin
                )
            )
                return new SolidColorBrush(Color.FromRgb(255, 184, 51));
            if (
                valueNum <= coatingCCDSpecModel.CILTargetValue + coatingCCDSpecModel.CILWarnMax
                && valueNum > coatingCCDSpecModel.CILTargetValue + coatingCCDSpecModel.CILWarnMin
            )
                return new SolidColorBrush(Color.FromRgb(41, 204, 122));

            return transparent;
        }

        private Brush GetColorFromCoating(CoatingCCDSpecModel coatingCCDSpecModel, double valueNum)
        {
            var transparent = new SolidColorBrush(Colors.Transparent);
            if (
                valueNum
                    > coatingCCDSpecModel.CoatingTargetValue + coatingCCDSpecModel.CoatingLimitMax
                || valueNum
                    <= coatingCCDSpecModel.CoatingTargetValue + coatingCCDSpecModel.CoatingLimitMin
            )
                return new SolidColorBrush(Color.FromRgb(255, 51, 51));
            if (
                (
                    valueNum
                        <= coatingCCDSpecModel.CoatingTargetValue
                            + coatingCCDSpecModel.CoatingLimitMax
                    && valueNum
                        > coatingCCDSpecModel.CoatingTargetValue
                            + coatingCCDSpecModel.CoatingWarnMax
                )
                || (
                    valueNum
                        <= coatingCCDSpecModel.CoatingTargetValue
                            + coatingCCDSpecModel.CoatingWarnMin
                    && valueNum
                        > coatingCCDSpecModel.CoatingTargetValue
                            + coatingCCDSpecModel.CoatingLimitMin
                )
            )
                return new SolidColorBrush(Color.FromRgb(255, 184, 51));
            if (
                valueNum
                    <= coatingCCDSpecModel.CoatingTargetValue + coatingCCDSpecModel.CoatingWarnMax
                && valueNum
                    > coatingCCDSpecModel.CoatingTargetValue + coatingCCDSpecModel.CoatingWarnMin
            )
                return new SolidColorBrush(Color.FromRgb(41, 204, 122));

            return transparent;
        }

        private Brush GetColorFromMisalign(CoatingCCDSpecModel coatingCCDSpecModel, double valueNum)
        {
            var transparent = new SolidColorBrush(Colors.Transparent);
            if (
                valueNum
                    > coatingCCDSpecModel.MisalignTargetValue + coatingCCDSpecModel.MisalignLimitMax
                || valueNum
                    <= coatingCCDSpecModel.MisalignTargetValue
                        + coatingCCDSpecModel.MisalignLimitMin
            )
                return new SolidColorBrush(Color.FromRgb(255, 51, 51));
            if (
                (
                    valueNum
                        <= coatingCCDSpecModel.MisalignTargetValue
                            + coatingCCDSpecModel.MisalignLimitMax
                    && valueNum
                        > coatingCCDSpecModel.MisalignTargetValue
                            + coatingCCDSpecModel.MisalignWarnMax
                )
                || (
                    valueNum
                        <= coatingCCDSpecModel.MisalignTargetValue
                            + coatingCCDSpecModel.MisalignWarnMin
                    && valueNum
                        > coatingCCDSpecModel.MisalignTargetValue
                            + coatingCCDSpecModel.MisalignLimitMin
                )
            )
                return new SolidColorBrush(Color.FromRgb(255, 184, 51));
            if (
                valueNum
                    <= coatingCCDSpecModel.MisalignTargetValue + coatingCCDSpecModel.MisalignWarnMax
                && valueNum
                    > coatingCCDSpecModel.MisalignTargetValue + coatingCCDSpecModel.MisalignWarnMin
            )
                return new SolidColorBrush(Color.FromRgb(41, 204, 122));

            return transparent;
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

    public sealed class PLCAddressConfigRowMap : ClassMap<PLCAddressConfigModel>
    {
        public PLCAddressConfigRowMap()
        {
            Map(m => m.PLCID).Name("plc_id");
            Map(m => m.PLCRWID).Name("plc_rw_id");
            Map(m => m.CategoryID).Name("category_id");
            Map(m => m.ParameterName).Name("parameter_name");
            Map(m => m.Type).Name("type");
            Map(m => m.Length).Name("length");
            Map(m => m.Address).Name("address");
        }
    }
}
