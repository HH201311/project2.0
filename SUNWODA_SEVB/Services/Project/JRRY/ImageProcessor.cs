using System.Diagnostics;
using System.IO;
using System.Reflection;
using OpenCvSharp;
using RemoteFileMonitor.Abstractions;
using RemoteFileMonitor.Models;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Models.Project.JRRY;
using SUNWODA_SEVB.Services.Project.CTOL;
using SUNWODA_SEVB.Tool.Helper;
using V11Core;
using static V11Core.BaseClass;

namespace SUNWODA_SEVB.Services.Project.JRRY
{
    public class ImageProcessor : IFileProcessor
    {
        private const string MonitorId = "JRRY";

        private readonly IMonitorManager _manager;
        private readonly ProcessingResultService _resultService;
        private readonly ResultAggregator _resultAggregator;
        private readonly ThresholdService _thresholdService;
        private readonly ILoggerService<ImageProcessor> _logger;

        private string _yolo_jierModelPath = "../Configs/极耳冗余模型/Yolo_jier.onnx";
        private string _unetpp_jierModelPath = "../Configs/极耳冗余模型/Unetpp_jier.onnx";
        private string _unetpp_forkModelPath = "../Configs/极耳冗余模型/Unetpp_fork.onnx";

        object? JRRY_AI;
        MethodInfo? ModelInit, ProcessSeg;

        // 模型实例
        private IPredictorzz? _yoloModel;          // Yolo 尺寸模型
        private IPredictorzz? _unetMeasureModel;   // Unet 尺寸模型
        private IPredictorzz? _unetDefectModel;    // Unet 缺陷模型

        // 最近一次推理的原始数据（用于绘图）
        private ProcessSegResult? _lastSegResult;

        // 模型互斥信号量
        private readonly SemaphoreSlim _modelSemaphore = new(1, 1);        

        public ImageProcessor
        (
            IMonitorManager monitorManager,
            ProcessingResultService resultService,
            ResultAggregator resultAggregator,
            ThresholdService thresholdService,
            ILoggerService<ImageProcessor> logger
        )
        {
            _manager = monitorManager;
            _resultService = resultService;
            _resultAggregator = resultAggregator;
            _thresholdService = thresholdService;
            _logger = logger;

            InitAlgorithm();
        }

        private void InitAlgorithm()
        {
            InitAlgorithmExample();
            InitModel();
        }


        private void InitAlgorithmExample()
        {
            try
            {
                if (File.Exists($"ZZJiErRongYu.dll"))
                {
                    var ass = Assembly.LoadFrom($"ZZJiErRongYu.dll");
                    var tp = ass.GetType("ZZJiErRongYuCore.JiErRongYu") ??
                        throw new NullReferenceException("[ZZJiErRongYuCore.JiErRongYu]type获取失败！");
                    JRRY_AI = Activator.CreateInstance(tp);
                    ModelInit = tp.GetMethod("ModelInit");
                    ProcessSeg = tp.GetMethod("ProcessSeg");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"创建AI算法dll实例异常：{ex.Message}", ex);
                throw new Exception($"创建AI算法dll实例异常：{ex.Message}");
            }
        }

        private void InitModel()
        {
            // 初始化 Yolo 尺寸模型
            (_yoloModel, var yoloError) = InitSingleModel(_yolo_jierModelPath, "尺寸测量Yolo");
            if (!string.IsNullOrEmpty(yoloError)) _logger.Error($"[JRRY] Yolo模型加载失败: {yoloError}");

            // 初始化 Unet 尺寸模型
            (_unetMeasureModel, var unetMeasureError) = InitSingleModel(_unetpp_jierModelPath, "尺寸测量Unet");
            if (!string.IsNullOrEmpty(unetMeasureError)) _logger.Error($"[JRRY] Unet尺寸模型加载失败: {unetMeasureError}");

            // 初始化 Unet 缺陷模型
            (_unetDefectModel, var unetDefectError) = InitSingleModel(_unetpp_forkModelPath, "缺陷检测Unet");
            if (!string.IsNullOrEmpty(unetDefectError)) _logger.Error($"[JRRY] Unet缺陷模型加载失败: {unetDefectError}");

            _logger.Info("[JRRY] 视觉模型加载完成");
        }

        private (IPredictorzz? Model, string? Error) InitSingleModel(string modelPath, string modelType)
        {
            try
            {
                var data = new object?[]
                {
                    modelPath,   // 模型文件绝对路径
                    null,        // 占位
                    modelType,   // "Yolo" 或 "Unet"
                    null,        // 模型句柄输出
                    null,        // 异常信息输出
                    true         // 启用 GPU
                };

                ModelInit?.Invoke(JRRY_AI, data);

                return (data[3] as IPredictorzz, data[4] as string);
            }
            catch (Exception ex)
            {
                _logger.Error($"初始化模型失败：{modelPath}，异常：{ex.Message}", ex);
                return (null, ex.Message);
            }
        }


        public bool CanProcess(string monitorId, string filePath) =>
            monitorId == MonitorId &&
            Path.GetExtension(filePath).Equals(".png", StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileNameWithoutExtension(filePath).Contains("cutImage_") && // 只处理含有 cutImage 的
            !Path.GetFileNameWithoutExtension(filePath).Contains("_result");    // 排除结果图


        public async Task ProcessAsync(MonitoredFile file, CancellationToken ct)
        {
            string sourcePath = file.FilePath;           

            try
            {
                // 1. 获取最新阈值
                var parameters = _thresholdService.GetParameters();

                // 2. 调用视觉推理算法
                DetectionResult detectionResult;
                await _modelSemaphore.WaitAsync(ct);
                try
                {
                    detectionResult = RunVisionInference(sourcePath, parameters);
                }
                finally
                {
                    _modelSemaphore.Release();
                }

                // 3. 构建输出路径（包含日期和 OK/NG 子目录）
                string outputPath = BuildOutputPath(sourcePath, detectionResult.IsOK);

                // 4. 生成带标注的结果图
                GenerateResultImage(sourcePath, outputPath);

                // 5. 保存CSV结果
                SaveCsvResults(sourcePath, outputPath);

                // 6. 通知 UI 更新预览图
                _resultService.NotifyProcessed(MonitorId, outputPath);

                // 7. 发布结果给统计模块
                await _resultAggregator.PublishResultAsync(detectionResult);
            }
            catch (Exception ex)
            {
                _logger.Error($"图片处理失败：{sourcePath}", ex);
                // 发送异常结果，防止UI卡死
                await _resultAggregator.PublishResultAsync(new DetectionResult
                {
                    Barcode = "ERROR",
                    IsOK = false,
                    DefectTypes = ["算法异常"]
                });
            }
        }

        
        private DetectionResult RunVisionInference(string imagePath, List<ParameterItem> parameters)
        {

            var result = new DetectionResult
            {
                Barcode = ExtractBarcode(imagePath),
                IsOK = true,
                GapResults = [],
                LeftForkResults = [],
                RightForkResults = [],
                DefectTypes = []
            };


            using var img = Cv2.ImRead(imagePath, ImreadModes.Color);
            if (img.Empty())
            {
                _logger.Error($"[JRRY] 图像读取失败：{imagePath}");
                result.IsOK = false;
                result.DefectTypes.Add("图像读取失败");
                return result;
            }

            var data = new object?[]
            {
                _yoloModel,         // 主测量模型句柄（Yolo_jier.onnx）
                _unetMeasureModel,  // 备用测量模型句柄（Unetpp_jier.onnx）
                _unetDefectModel,   // 缺陷检测模型句柄（Unetpp_fork.onnx）
                img,                // 输入原图（OpenCvSharp.Mat）
                null,               // 检测结果结构体
                null                // 接口异常返回信息
            };

            Stopwatch sw = Stopwatch.StartNew();
            ProcessSeg?.Invoke(JRRY_AI, data);
            sw.Stop();

            _logger.Info($"[JRRY] {imagePath}视觉算法处理完成，耗时：{sw.ElapsedMilliseconds} ms");

            var errorMsg = data[5] as string;

            if (data[4] is not ProcessSegResult segResult || !string.IsNullOrEmpty(errorMsg))
            {
                _logger.Error($"[JRRY] 算法错误：{errorMsg ?? "返回结果为空"}");
                result.IsOK = false;
                result.DefectTypes.Add("算法异常");
                return result;
            }

            _lastSegResult = segResult;

            // 间距测量判定
            EvaluateGap(segResult, parameters, result);

            // 缺陷（开叉）判定：左右分别判定，并收集图像级 NG 条件（如开叉时最小高度）
            EvaluateFork(segResult, true, parameters, result);   // 左侧
            EvaluateFork(segResult, false, parameters, result);  // 右侧

            // 去重缺陷类型
            result.DefectTypes = [.. result.DefectTypes.Distinct()];

            return result;
        }

        private void EvaluateGap(ProcessSegResult seg, List<ParameterItem> parameters, DetectionResult result)
        {
            var distance = seg.Distance;
            if (distance == null || distance.Length < 6)
            {
                result.IsOK = false;
                result.DefectTypes.Add("间距数据不完整");
                return;
            }

            string[] gapNames = ["左侧距离", "左侧最小高度距离", "左侧最大高度距离", "右侧距离", "右侧最小高度距离", "右侧最大高度距离"];
            for (int i = 0; i < gapNames.Length; i++)
            {
                double value = distance[i];
                bool failed = value < 0;
                string paramName = $"{gapNames[i]}标准";
                var threshold = parameters.FirstOrDefault(p => p.ParameterName == paramName);

                bool ok = true;
                if (failed) ok = false;
                else if (threshold != null) ok = value >= threshold.LowerLimit && value <= threshold.UpperLimit;

                result.GapResults.Add(new DetectionItem
                {
                    Name = gapNames[i],
                    Value = Math.Round(failed ? 0 : value, 2),
                    IsOK = ok
                });

                if (!ok)
                {
                    result.IsOK = false;
                    result.DefectTypes.Add(threshold != null ? ParamNameToDefectType(paramName) : $"{gapNames[i]}异常");
                }
            }
        }


        private void EvaluateFork(ProcessSegResult seg, bool isLeft, List<ParameterItem> parameters, DetectionResult result)
        {
            int number = isLeft ? seg.L_Number : seg.R_Number;
            if (number <= 0) return;

            string side = isLeft ? "左" : "右";
            bool hasForkDefect = false; // 该侧是否有开叉缺陷（属性超限）

            // 遍历该侧的每个缺陷
            for (int i = 0; i < number; i++)
            {
                // 提取第 i 个缺陷的属性（根据左右侧选取对应的数组）
                float area = isLeft ? GetArrayValue(seg.L_Area, i) : GetArrayValue(seg.R_Area, i);
                float width = isLeft ? GetArrayValue(seg.L_Width, i) : GetArrayValue(seg.R_Width, i);
                float height = isLeft ? GetArrayValue(seg.L_Height, i) : GetArrayValue(seg.R_Height, i);
                float angle1 = isLeft ? GetArrayValue(seg.L_Angle1, i) : GetArrayValue(seg.R_Angle1, i);
                float angle2 = isLeft ? GetArrayValue(seg.L_Angle2, i) : GetArrayValue(seg.R_Angle2, i);
                float contourLen = isLeft ? GetArrayValue(seg.L_ContourLen, i) : GetArrayValue(seg.R_ContourLen, i);
                float cil = isLeft ? GetArrayValue(seg.L_CIL_cha, i) : GetArrayValue(seg.R_CIL_cha, i);
                
                // 1. 缺陷属性阈值判定
                bool areaOk = CheckThreshold(parameters, "开叉面积", area);
                bool angle1Ok = CheckThreshold(parameters, "开叉角度1", angle1);
                bool angle2Ok = CheckThreshold(parameters, "开叉角度2", angle2);
                bool cilOk = CheckThreshold(parameters, "开叉CIL", cil);

                // 宽、高、周长仅显示，不参与缺陷判定
                var items = new List<DetectionItem>
                {
                    new() { Name = "面积", Value = Math.Round(area, 2), IsOK = areaOk },
                    new() { Name = "宽度", Value = Math.Round(width, 2), IsOK = true },
                    new() { Name = "高度", Value = Math.Round(height, 2), IsOK = true },
                    new() { Name = "角度1", Value = Math.Round(angle1, 2), IsOK = angle1Ok },
                    new() { Name = "角度2", Value = Math.Round(angle2, 2), IsOK = angle2Ok },
                    new() { Name = "周长", Value = Math.Round(contourLen, 2), IsOK = true },
                    new() { Name = "CIL", Value = Math.Round(cil, 2), IsOK = cilOk }
                };

                // 如果任一属性超限，标记该侧存在开叉缺陷
                if (!areaOk || !angle1Ok || !angle2Ok || !cilOk)
                {
                    hasForkDefect = true;
                    // 添加具体缺陷类型
                    if (!areaOk) result.DefectTypes.Add(ParamNameToDefectType("开叉面积"));
                    if (!angle1Ok) result.DefectTypes.Add(ParamNameToDefectType("开叉角度1"));
                    if (!angle2Ok) result.DefectTypes.Add(ParamNameToDefectType("开叉角度2"));
                    if (!cilOk) result.DefectTypes.Add(ParamNameToDefectType("开叉CIL"));
                }

                // 将本缺陷的测量项加入结果列表
                var targetList = isLeft ? result.LeftForkResults : result.RightForkResults;
                foreach (var item in items)
                    targetList.Add(item);
            }

            // 2. 图像级 NG 判定：开叉存在时，检查对应侧的最小高度
            if (hasForkDefect)
            {
                result.IsOK = false;

                string minHeightName = side + "侧最小高度距离";
                var minHeightItem = result.GapResults.FirstOrDefault(g => g.Name == minHeightName);
                double minHeightValue = minHeightItem?.Value ?? 0;

                string minHeightParamName = $"{side}最小高度距离标准";
                var minHeightThreshold = parameters.FirstOrDefault(p => p.ParameterName == minHeightParamName);
                bool minHeightOk = minHeightThreshold == null ||
                                   (minHeightValue >= minHeightThreshold.LowerLimit && minHeightValue <= minHeightThreshold.UpperLimit);

                if (!minHeightOk)
                {
                    result.DefectTypes.Add(ParamNameToDefectType(minHeightParamName));
                    if (minHeightItem != null) minHeightItem.IsOK = false;
                }
            }            
        }

        

        private void GenerateResultImage(string sourcePath, string destPath)
        {
            using var src = Cv2.ImRead(sourcePath, ImreadModes.Color);
            if (src.Empty())
            {
                _logger.Warn("[JRRY] 原图为空，无法绘制标注，直接复制原图。");
                File.Copy(sourcePath, destPath, overwrite: true);
                return;
            }

            if (_lastSegResult != null)
            {
                // 1. 绘制间距测量线段（6个点）
                DrawGapMeasurements(src, _lastSegResult);
                // 2. 绘制缺陷外接矩形
                DrawDefectRects(src, _lastSegResult);
            }
            
            // 保存结果图
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            src.SaveImage(destPath);
        }

        private void DrawGapMeasurements(Mat src, ProcessSegResult seg)
        {
            if (seg.RowOut == null || seg.ColOut == null || seg.RowOut.Length < 12 || seg.ColOut.Length < 12)
                return;

            // 绘制所有点，并用短线段连接
            for (int i = 0; i < seg.RowOut.Length; i++)
            {
                var pt = new Point(seg.ColOut[i], seg.RowOut[i]);
                Cv2.Circle(src, pt, 4, Scalar.Orange, -1);
            }

            // 绘制6条线段
            for (int i = 0; i < 12; i += 2)
            {
                if (i + 1 < seg.RowOut.Length)
                    Cv2.Line(src, new Point(seg.ColOut[i], seg.RowOut[i]),
                        new Point(seg.ColOut[i + 1], seg.RowOut[i + 1]), Scalar.Green, 2);
            }
        }

        private void DrawDefectRects(Mat src, ProcessSegResult seg)
        {
            if (seg.L_Number > 0 && seg.L_OutRect != default)
                Cv2.Rectangle(src, seg.L_OutRect, Scalar.Red, 2);

            if (seg.R_Number > 0 && seg.R_OutRect != default)
                Cv2.Rectangle(src, seg.R_OutRect, Scalar.Red, 2);
        }


        private void SaveCsvResults(string sourcePath, string outputImagePath)
        {
            if (_lastSegResult == null) return;

            string imageName = Path.GetFileNameWithoutExtension(sourcePath);

            // 间距数据
            float[]? dist = _lastSegResult.Distance;
            string leftDist = dist?.Length > 0 ? dist[0].ToString("F2") : "";
            string leftMinH = dist?.Length > 1 ? dist[1].ToString("F2") : "";
            string leftMaxH = dist?.Length > 2 ? dist[2].ToString("F2") : "";
            string rightDist = dist?.Length > 3 ? dist[3].ToString("F2") : "";
            string rightMinH = dist?.Length > 4 ? dist[4].ToString("F2") : "";
            string rightMaxH = dist?.Length > 5 ? dist[5].ToString("F2") : "";

            string header = "图像名称,左侧间距,左侧最小高度,左侧最大高度,右侧间距,右侧最小高度,右侧最大高度,左右侧,序号,面积,宽度,高度,开叉-水平,开叉-切线,周长,开叉深度";

            // 构建该图片的所有数据行
            var dataRows = new List<string>();
            string spacingPart = $"{imageName},{leftDist},{leftMinH},{leftMaxH},{rightDist},{rightMinH},{rightMaxH}";

            int defectNo = 1;
            // 左侧缺陷
            for (int i = 0; i < _lastSegResult.L_Number; i++)
            {
                dataRows.Add(spacingPart + "," + BuildForkRow(_lastSegResult, true, i, defectNo));
                spacingPart = ",,,,,,"; // 后续缺陷行间距留空
                defectNo++;
            }
            // 右侧缺陷
            for (int i = 0; i < _lastSegResult.R_Number; i++)
            {
                dataRows.Add(spacingPart + "," + BuildForkRow(_lastSegResult, false, i, defectNo));
                spacingPart = ",,,,,,";
                defectNo++;
            }

            // 如果没有缺陷，仍输出一行仅含间距的数据
            if (dataRows.Count == 0)
            {
                dataRows.Add(spacingPart);
            }

            // 1. 保存单张图片对应的 CSV 文件（与结果图同目录）
            string? individualDir = Path.GetDirectoryName(outputImagePath);
            if (string.IsNullOrEmpty(individualDir))
                individualDir = "."; // 回退到当前目录

            string individualFileName = Path.GetFileNameWithoutExtension(outputImagePath) + ".csv";
            foreach (string row in dataRows)
            {
                CsvSaveHelper.SaveCSVFile(individualDir, individualFileName, header, row);
            }

            // 2. 追加到汇总 CSV（OK/NG 目录下的 汇总结果.csv）
            string? summaryDir = Path.GetDirectoryName(individualDir); // 向上取一级，即 OK 或 NG 目录
            if (string.IsNullOrEmpty(summaryDir))
                summaryDir = "."; // 回退到当前目录

            lock (CsvSaveHelper.FileObj)
            {
                foreach (string row in dataRows)
                {
                    CsvSaveHelper.SaveCSVFile(summaryDir, "汇总结果.csv", header, row);
                }
            }
        }

        private static string BuildForkRow(ProcessSegResult seg, bool isLeft, int index, int defectIndex)
        {
            string side = isLeft ? "Left" : "Right";

            float GetVal(float[]? arr, int idx) => (arr != null && idx < arr.Length) ? arr[idx] : 0f;

            float area = isLeft ? GetVal(seg.L_Area, index) : GetVal(seg.R_Area, index);
            float width = isLeft ? GetVal(seg.L_Width, index) : GetVal(seg.R_Width, index);
            float height = isLeft ? GetVal(seg.L_Height, index) : GetVal(seg.R_Height, index);
            float angle1 = isLeft ? GetVal(seg.L_Angle1, index) : GetVal(seg.R_Angle1, index);
            float angle2 = isLeft ? GetVal(seg.L_Angle2, index) : GetVal(seg.R_Angle2, index);
            float contourLen = isLeft ? GetVal(seg.L_ContourLen, index) : GetVal(seg.R_ContourLen, index);
            float cil = isLeft ? GetVal(seg.L_CIL_cha, index) : GetVal(seg.R_CIL_cha, index);

            return $"{side},{defectIndex},{area:F4},{width:F4},{height:F4},{angle1:F4},{angle2:F4},{contourLen:F4},{cil:F2}";
        }

        private static string ExtractBarcode(string imagePath)
        {
            string? dir = Path.GetDirectoryName(imagePath);
            if (string.IsNullOrEmpty(dir))
                return "UNKNOWN";

            string folderName = Path.GetFileName(dir);   // 文件夹名
            string[] parts = folderName.Split('_');
            return parts.Length > 0 ? parts[^1] : folderName;
        }

        /// <summary>
        /// 构建输出路径：OutputDirectory/日期/OK或NG/原文件名/原文件名_result.png
        /// </summary>
        private string BuildOutputPath(string sourcePath, bool isOk)
        {
            string outputRoot = GetOutputDirectory();
            string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");
            string resultFolder = isOk ? "OK" : "NG";
            string fileNameFolder = ExtractBarcode(sourcePath);
            string fileName = Path.GetFileNameWithoutExtension(sourcePath) + "_result.png";

            return Path.Combine(outputRoot, dateFolder, resultFolder, fileNameFolder, fileName);
        }

        /// <summary>
        /// 从 MonitorManager 获取当前监控目标的 OutputDirectory。
        /// 若未配置则回退到源文件同目录。
        /// </summary>
        private string GetOutputDirectory()
        {
            var target = _manager.Controls.FirstOrDefault(c => c.Id == MonitorId);
            if (target != null && !string.IsNullOrWhiteSpace(target.Options.OutputDirectory))
                return target.Options.OutputDirectory;

            // 回退：使用程序目录下的默认文件夹
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "极耳冗余项目\\IMEResult");
        }

        private string ParamNameToDefectType(string paramName) =>
            paramName.EndsWith("标准") ? $"{paramName[..^2]}不良" : $"{paramName}不良";


        // 辅助方法：安全获取数组中的第 i 个元素，不足时返回 0
        private static float GetArrayValue(float[]? array, int index)
        {
            if (array == null || index >= array.Length) return 0;
            return array[index];
        }

        // 辅助方法：根据参数名查找阈值并比较，如果阈值不存在则视为合格（true）
        private static bool CheckThreshold(List<ParameterItem> parameters, string paramName, float value)
        {
            var threshold = parameters.FirstOrDefault(p => p.ParameterName == paramName);
            if (threshold == null) return true;
            return value >= threshold.LowerLimit && value <= threshold.UpperLimit;
        }
    }
}