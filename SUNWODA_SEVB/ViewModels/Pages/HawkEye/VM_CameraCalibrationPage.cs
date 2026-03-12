using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using Camera.Calibration.Model;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;
using OpenCvSharp;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Models.Project.HawkEye;


namespace SUNWODA_SEVB.ViewModels.Pages.HawkEye
{
    [Module("VM_CameraCalibrationPage", "相机标定配置", Order = 2, Category = "VM_MachineDamageMonitoringPage/VM_CameraCalibrationPage")]
    class VM_CameraCalibrationPage : ViewModelBase
    {
        /// <summary>
        /// 反射的标定算法的方法
        /// </summary>
        private MethodInfo? cameraCoefficientMethod, calibrationMethod;
        /// <summary>
        /// 标定算法实例
        /// </summary>
        private object? AlgorithmExample;

        /// <summary>
        /// 标定视图模型
        /// </summary>
        public CalibrationPlateModel CalibrationPlateModel { get; set; } = new();
        /// <summary>
        /// 界面标定图片集
        /// </summary>
        public ObservableCollection<string> ImagePaths { get; } = [];
        /// <summary>
        /// 界面标定图片源
        /// </summary>
        public ObservableCollection<string> ImagePath { get; } = [];

        private string? selectedImage;
        /// <summary>
        /// 被选中的图片
        /// </summary>
        public string? SelectedImage
        {
            get => selectedImage;
            set => SetProperty(ref selectedImage, value);
        }

        /// <summary>
        /// 标定结果
        /// </summary>
        private CalibrationResult CalibrationResult { get; set; } = new();

        /// <summary>
        /// 构造函数
        /// </summary>
        public VM_CameraCalibrationPage()
        {
            InitDLL();            
        }

        /// <summary>
        /// 初始化标定算法
        /// </summary>
        private void InitDLL()
        {
            var DLLPath = "Calibration.dll";
            if (File.Exists($"{DLLPath}"))
            {
                try
                {
                    Assembly ass = Assembly.LoadFrom($"{DLLPath}");
                    Type? tp = ass.GetType("Calibration.Calibrate");
                    AlgorithmExample = Activator.CreateInstance(tp);
                    cameraCoefficientMethod = tp.GetMethod("CalibrateCamera");
                    calibrationMethod = tp.GetMethod("CalculatePixelAccuracy");
                }
                catch (Exception ex)
                {
                    HandyControl.Controls.MessageBox.Show($"[InitDLL]-标定算法初始化失败！！！\r\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #region 界面命令

        /// <summary>
        /// 加载图片文件夹中所有图片
        /// </summary>
        public ICommand LoadImageDirectory => new RelayCommand(() =>
        {
            // 选择文件夹
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                string selectedFolder = dialog.FileName;
                ImagePaths.Clear();

                // 支持的图片类型
                var extensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };

                // 遍历目录下所有图片文件
                foreach (var file in Directory.GetFiles(selectedFolder))
                {
                    if (extensions.Contains(Path.GetExtension(file).ToLower()))
                    {
                        ImagePaths.Add(file);
                    }
                }
            }
        });

        /// <summary>
        /// 移除选中的图片
        /// </summary>
        public ICommand RemoveSelectedImage => new RelayCommand(() =>
        {
            if (SelectedImage != null) ImagePaths.Remove(SelectedImage);
        });

        /// <summary>
        /// 移除所有图片
        /// </summary>
        public ICommand RemoveAllImages => new RelayCommand(() =>
        {
            ImagePaths.Clear();
        });

        /// <summary>
        /// 图片集标定
        /// </summary>
        public ICommand Calibration => new RelayCommand(() =>
        {
            try
            {
                if (ImagePaths.Count == 0)
                {
                    HandyControl.Controls.MessageBox.Show("请先加载文件目录！！！", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (ImagePaths.Count < 15)
                {
                    HandyControl.Controls.MessageBox.Show($"文件目录中图片数量不足，当前有{ImagePaths.Count}张，需要至少15张才能进行标定！！！", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // === 二、准备标定输入数据 ===
                var calibrateImages = ImagePaths.Select(path => new CalibrateImage { FileName = path }).ToList();

                if (CalibrationPlateModel.ProceedNumber <= 0 || CalibrationPlateModel.ListNumber <= 0 || CalibrationPlateModel.CheckerboardSize <= 0)
                {
                    HandyControl.Controls.MessageBox.Show($"请先输入标定板参数，不能为0、空或负数！！！", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                object[] AIParamData =
                [
                    calibrateImages,
                    CalibrationPlateModel.ProceedNumber,
                    CalibrationPlateModel.ListNumber,
                    CalibrationPlateModel.CheckerboardSize
                ];
                var calibrationResult = cameraCoefficientMethod?.Invoke(AlgorithmExample, AIParamData);
                // 获取对象类型
                Type? kkType = calibrationResult?.GetType();

                // 获取 CameraMatrix相机内参矩阵
                PropertyInfo? cameraMatrixProp = kkType?.GetProperty("CameraMatrix");
                object? cameraMatrixValue = cameraMatrixProp?.GetValue(calibrationResult);
                double[,]? cameraMatrix = (double[,]?)cameraMatrixValue;

                // 获取 DistCoeffs相机畸变系数
                PropertyInfo? distCoeffsProp = kkType?.GetProperty("DistCoeffs");
                double[]? distCoeffs = (double[]?)distCoeffsProp?.GetValue(calibrationResult);

                // 获取 Ret总体重投影误差（总均方误差）
                PropertyInfo? retProp = kkType?.GetProperty("Ret");
                double? Ret = (double?)retProp?.GetValue(calibrationResult);

                if (Ret < 0.6)
                {
                    CalibrationResult.Ret = (double)Ret;
                    CalibrationResult.CameraMatrix = cameraMatrix;
                    CalibrationResult.DistCoeffs = distCoeffs;
                    HandyControl.Controls.MessageBox.Show($"标定成功！！！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                HandyControl.Controls.MessageBox.Show($"标定失败，请重新标定！！！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"[Calibration]-图片集标定命令错误！！！\r\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });

        /// <summary>
        /// 加载一张标定图片
        /// </summary>
        public ICommand LoadImage => new RelayCommand(() =>
        {
            OpenFileDialog openFileDialog = new()
            {
                Multiselect = true,
                Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                ImagePath.Clear();
                foreach (var file in openFileDialog.FileNames)
                {
                    ImagePath.Add(file);
                }
            }
        });

        /// <summary>
        /// 移除标定图片
        /// </summary>
        public ICommand RemoveImage => new RelayCommand(() =>
        {
            ImagePath.Clear();
        });

        /// <summary>
        /// 像素精度计算
        /// </summary>
        public ICommand CalculatePixelAccuracy => new RelayCommand(() =>
        {
            try
            {
                if (ImagePath.Count == 0)
                {
                    HandyControl.Controls.MessageBox.Show("请先加载标定图片！！！", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (CalibrationResult.CameraMatrix == null || CalibrationResult.DistCoeffs == null)
                {
                    HandyControl.Controls.MessageBox.Show("请先完成标定操作或加载标定文件！！！", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Mat image = Cv2.ImRead(ImagePath[0], ImreadModes.Color); // 以彩色模式读取

                object[] AIParamData =
                [
                    image,
                    CalibrationResult.CameraMatrix,
                    CalibrationResult.DistCoeffs,
                    CalibrationPlateModel.ProceedNumber,
                    CalibrationPlateModel.ListNumber,
                    CalibrationPlateModel.CheckerboardSize
                ];

                var pixelAccuracyResult = calibrationMethod?.Invoke(AlgorithmExample, AIParamData);
                // 获取像素精度
                if (pixelAccuracyResult is double pixelPrecision)
                {
                    CalibrationPlateModel.PixelPrecision = pixelPrecision;
                }
                else
                {
                    HandyControl.Controls.MessageBox.Show($"像素精度计算结果无效，请检查标定参数或算法返回值！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                image?.Dispose();
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"[CalculatePixelAccuracy]-像素精度计算命令错误！！！\r\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });

        /// <summary>
        /// 加载标定配置文件
        /// </summary>
        public ICommand LoadCalibrationConfig => new RelayCommand(() =>
        {
            try
            {
                if (CalibrationPlateModel.ProceedNumber <= 0 || CalibrationPlateModel.ListNumber <= 0 || CalibrationPlateModel.CheckerboardSize <= 0)
                {
                    HandyControl.Controls.MessageBox.Show($"请先输入标定板参数，不能为0、空或负数！！！", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                OpenFileDialog openFileDialog = new()
                {
                    Title = "打开 JSON 文件",
                    Filter = "JSON 文件 (*.json)|*.json",
                    Multiselect = false
                };

                bool? result = openFileDialog.ShowDialog();
                if (result == true)
                {
                    var json = File.ReadAllText(openFileDialog.FileName);
                    var calibrationResult = JsonConvert.DeserializeObject<CalibrationResult>(json);
                    CalibrationResult = calibrationResult;
                    HandyControl.Controls.MessageBox.Show($"成功加载标定文件，获取到相机内外参和去畸变系数", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"[LoadCalibrationConfig]-标定配置文件加载失败！！！\r\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });

        /// <summary>
        /// 导出标定配置文件
        /// </summary>
        public ICommand ExportCalibrationConfig => new RelayCommand(() =>
        {
            try
            {
                SaveFileDialog saveFileDialog = new()
                {
                    Title = "另存为",
                    Filter = "JSON 文件 (*.json)|*.json",
                    FileName = "标定文件.json",
                    OverwritePrompt = true
                };

                bool? result = saveFileDialog.ShowDialog();
                if (result == true)
                {
                    var json = JsonConvert.SerializeObject(CalibrationResult, Formatting.Indented);
                    File.WriteAllText(saveFileDialog.FileName, json);
                    HandyControl.Controls.MessageBox.Show($"完成标定配置文件导出", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"[ExportCalibrationConfig]-标定配置文件导出失败！！！\r\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });
        #endregion


    }
}
