using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using Dm;
using HandyControl.Controls;
using Microsoft.Win32;
using Newtonsoft.Json;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Project.HawkEye;

namespace SUNWODA_SEVB.ViewModels.Pages.HawkEye
{
    /// <summary>
    /// 
    /// </summary>
    [Module("VM_CameraConfigurationManagementPage", "相机管理配置", Order = 1, Category = "VM_MachineDamageMonitoringPage/VM_CameraConfigurationManagementPage")]
    class VM_CameraConfigurationManagementPage : ViewModelBase
    {
        private readonly ILoggerService<VM_MachineDamageMonitoringPage> _logger;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IWebApiClient _webApiClient;

        /// <summary>
        /// 构造函数
        /// </summary>
        public VM_CameraConfigurationManagementPage(
            ILoggerService<VM_MachineDamageMonitoringPage> logger,
            IGlobalSettingRepository globalSettingRepository,
            IPLCConfigRepository plcConfigRepository,
            IWebApiClient webApiClient

        )
        {
            _logger = logger;
            _globalSettingRepository = globalSettingRepository;
            _plcConfigRepository = plcConfigRepository;
            _webApiClient = webApiClient;

            SaveCommand = new RelayCommand(Save);
            DeleteCommand = new RelayCommand(Delete);
            AddCommand = new RelayCommand(Add);
            LoadCalibrationConfigCommand = new RelayCommand(LoadCalibrationConfig);            

            // 更新行列
            CameraConfig.CollectionChanged += ((s, e) =>
            {
                if (s is not ObservableCollection<CameraModel> cms) return;
                Columns = 3;
                Rows = (cms.Count + 1) / 3 >= 3 ? (CameraConfig.Count / 3 + 1) : 3;
            });
            CompositeItems =
            [
                // 添加数据源集合
                new CollectionContainer { Collection = CameraConfig },
                // 添加“新增功能”的 Button
                AddCommand,
            ];
        }

        private int columns = 3;
        private int rows = 3;

        /// <summary>
        /// 列数
        /// </summary>
        public int Columns
        {
            get => columns;
            set => SetProperty(ref columns, value);
        }
        /// <summary>
        /// 行数
        /// </summary>
        public int Rows
        {
            get => rows;
            set => SetProperty(ref rows, value);
        }

        /// <summary>
        /// 相机配置
        /// </summary>
        public static ObservableCollection<CameraModel> CameraConfig { get; set; } = [];
        public CompositeCollection CompositeItems { get; set; }

        public void AddCamera(CameraModel? cameraModel)
        {
            if (cameraModel == null) return;
            RunOnUIThread(() =>
            {
                CameraConfig?.Add(cameraModel);
            });
        }

        #region 命令
        public ICommand? SaveCommand { get; set; }
        public ICommand? DeleteCommand { get; set; }
        public ICommand? AddCommand { get; set; }
        public ICommand? LoadCalibrationConfigCommand { get; set; }

        /// <summary>
        /// 保存
        /// </summary>
        /// <param name="obj"></param>
        private void Save(object? obj)
        {
            if (obj is not CameraModel cm) return;
            if (string.IsNullOrEmpty(cm.Name) || string.IsNullOrEmpty(cm.HostIP) || string.IsNullOrEmpty(cm.WorkStation))
            {
                HandyControl.Controls.MessageBox.Show
                (
                    $"[名称、相机IP、工位名称]不可为空，请确认！",
                    "信息确认",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }
            if (!IPAddress.TryParse(cm.HostIP, out IPAddress? ip))
            {
                HandyControl.Controls.MessageBox.Show
                (
                    $"[相机IP]格式不正确，请确认！",
                    "信息确认",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }
            cm.HostIP = ip.ToString();
            
            Task.Run(async () =>
            {
                //var re = await _webApiClient.AddCameraAsync([cm]);

                //if (re.IsSuccess || re.Code == 200)
                //{
                    _logger.Info($"相机配置更新成功！");
                    VM_MachineDamageMonitoringPage.SetCameras(cm);
                    HandyControl.Controls.MessageBox.Show
                    (
                        $"[{cm.Name} Ip:{cm.HostIP}]已保存！",
                        "信息确认",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );                    
                //}
                //else
                //    _logger.Error($"相机配置更新失败：[{re.Message}]");
            });
        }


        private void LoadCalibrationConfig(object? obj)
        {
            if (obj is not CameraModel cm) return;
            OpenFileDialog openFileDialog = new()
            {
                Title = "打开 JSON 文件",
                Filter = "JSON 文件 (*.json)|*.json",
                Multiselect = false
            };

            bool? result = openFileDialog.ShowDialog();
            if (result == true)
            {
                cm.CalibrationFilePath = openFileDialog.FileName;
                _logger.Info($"[{cm.Name}]加载校准文件成功！");
                HandyControl.Controls.MessageBox.Show
                (
                    $"[{cm.CalibrationFilePath}]加载校准文件成功！",
                    "信息确认",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            
        }

        /// <summary>
        /// 删除
        /// </summary>
        /// <param name="obj"></param>
        private void Delete(object? obj)
        {
            if (obj is not CameraModel cm) return;
            var re = HandyControl.Controls.MessageBox.Show
            (
               $"请确认是否删除！",
               "删除确认",
               MessageBoxButton.YesNo,
               MessageBoxImage.Question
            );
            if (re != System.Windows.MessageBoxResult.Yes) return;
            VM_MachineDamageMonitoringPage.DeleteCameras(cm.GuidId);
            CameraConfig.Remove(cm);
        }

        /// <summary>
        /// 新增
        /// </summary>
        /// <param name="obj"></param>
        private void Add()
        {
            var cm = new CameraModel
            {
                Protocol = "RTSP"
            };
            CameraConfig.Add(cm);
        }
        #endregion

        /// <summary>
        /// 导航完成时加载相机配置
        /// </summary>
        /// <param name="parameter"></param>
        //public override void OnNavigatedTo(object? parameter)
        //{
        //    CameraConfig.Clear();
        //    // 初始化加载
        //    var cameras = VM_MachineDamageMonitoringPage.GetCameras();
        //    cameras.ForEach(o => CameraConfig.Add(o));
        //    // 导航完成后调用，初始加载一些需要绑定的数据，还有一个异步方法OnNavigatedToAsync
        //    base.OnNavigatedTo(parameter);
        //}

        /// <summary>
        /// 导航结束时判断相机配置
        /// </summary>
        /// <returns></returns>
        public override bool CanNavigateFrom()
        {
            var cameras = VM_MachineDamageMonitoringPage.GetCameras();

            // 新增未保存
            foreach (var item in CameraConfig)
            {
                if (!cameras.Exists(p => p.GuidId == item.GuidId))
                {
                    var re = HandyControl.Controls.MessageBox.Show(
                       $"新增相机未保存，请确认否退出！",
                       "退出确认",
                       MessageBoxButton.YesNo,
                       MessageBoxImage.Question
                    );
                    return re == MessageBoxResult.Yes;
                }
            }
            // 修改未保存
            foreach (var item in CameraConfig)
            {
                //if (!cameras.Exists(p => JsonConvert.SerializeObject(p) == JsonConvert.SerializeObject(item)))
                //{
                //    var re = HandyControl.Controls.MessageBox.Show(
                //       $"[{item.Name}]存在修改未保存，请确认是否退出！",
                //       "退出确认",
                //       MessageBoxButton.YesNo,
                //       MessageBoxImage.Question
                //    );
                //    return re == MessageBoxResult.Yes;
                //}
                if (!cameras.Any(p => p.BusinessEquals(item)))
                {
                    var re = HandyControl.Controls.MessageBox.Show(
                        $"[{item.Name}]存在修改未保存，请确认是否退出！",
                        "退出确认",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question
                    );

                    return re == MessageBoxResult.Yes;
                }
            }

            // 可自定义设置当前页面是否可以导航离开，还有一个异步方法CanNavigateFromAsync
            return base.CanNavigateFrom();
        }

    }
}
