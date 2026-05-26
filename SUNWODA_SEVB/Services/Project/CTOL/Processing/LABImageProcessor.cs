#define C06

using System.IO;
using HalconDotNet;
using RemoteFileMonitor.Abstractions;
using SUNWODA_SEVB.Core.Interfaces;

namespace SUNWODA_SEVB.Services.Project.CTOL.Processing
{
    public class LABImageProcessor : ImageProcessorBase
    {
        protected override string TargetMonitorId => "LAB";

        public LABImageProcessor(
            IMonitorManager manager,
            ProcessingResultService resultService,
            BarcodeGroupingService groupingService,
            GapThresholdService gapThresholds,
            GapCsvService gapCsv,
            ILoggerService<LABImageProcessor> logger
        )
            : base(manager, resultService, groupingService, gapThresholds, gapCsv, logger)
        {
            GetModel();
        }

        protected override (
            bool isOk,
            string defectType,
            string outputPath,
            double[]? yOhValues,
            double[]? gapValues
        ) ProcessImage(string imagePath)
        {
            // ===== STEP 1：调用 AI 算法，获取检测原始数据 =====

            // --- 每张图调用算法 ---
            try
            {
#if C06
                //AI.Algorithm_Action(
                //    imagePath, // 原图完整路径
                //    Model!, // LAB 专用模型实例（各通道独立，勿共享）
                //    out HObject originImg,
                //    out HObject recNE, // 负极区域分割结果
                //    out HObject recPE, // 正极区域分割结果
                //    out HObject recMiddlePE, // 正极中间区域分割结果
                //    out HTuple distOHInt, // 正极 OH 内侧值数组（SArr = string[]）
                //    out HTuple distOHOut, // 负极 OH 外侧值数组（SArr = string[]）
                //    out HTuple gapMax, // 间隙最大值 [负极边缘, 正极边缘, 正极中间]（SArr = string[3]）
                //    out float hDNe, // 负极差值
                //    out int neNGflag, // 负极压伤标志（0=正常，1=压伤）
                //    out string errorReport
                //); // 非空表示算法内部异常，应跳过后续判断

                //int neTQflag = 0;

                AI.Algorithm_Action(
                    imagePath, // 原图完整路径
                    Model!, // LAB 专用模型实例（各通道独立，勿共享）
                    out HObject originImg,
                    out HObject recNE, // 负极区域分割结果
                    out HObject recPE, // 正极区域分割结果
                    out HObject recMiddlePE, // 正极中间区域分割结果
                    out HTuple distOHInt, // 正极 OH 内侧值数组（SArr = string[]）
                    out HTuple distOHOut, // 负极 OH 外侧值数组（SArr = string[]）
                    out HTuple gapMax, // 间隙最大值 [负极边缘, 正极边缘, 正极中间]（SArr = string[3]）
                    out float hDNe, // 负极差值
                    out int neNGflag, // 负极压伤标志（0=正常，1=压伤）
                    out int neTQflag, // 负极突起标志（0=正常，1=突起）
                    out string errorReport
                ); // 非空表示算法内部异常，应跳过后续判断
#else
            AI.Algorithm_Action(
                imagePath,          // 原图完整路径
                Model!,            // LAB 专用模型实例（各通道独立，勿共享）
                out HObject recNE,        // 负极区域分割结果
                out HObject recPE,        // 正极区域分割结果
                out HObject recMiddlePE,  // 正极中间区域分割结果
                out HTuple distOHInt,     // 正极 OH 内侧值数组（SArr = string[]）
                out HTuple distOHOut,     // 负极 OH 外侧值数组（SArr = string[]）
                out HTuple gapMax,        // 间隙最大值 [负极边缘, 正极边缘, 正极中间]（SArr = string[3]）
                out float hDNe,           // 负极差值
                out int neNGflag,         // 负极压伤标志（0=正常，1=压伤）
                out string errorReport);  // 非空表示算法内部异常，应跳过后续判断
            HOperatorSet.ReadImage(out HObject originImg, imagePath);
            int neTQflag = 0;
#endif

                if (!string.IsNullOrEmpty(errorReport))
                    return (false, "算法异常", $"{errorReport}", null, null);

                // --- 将算法输出转换为判断方法所需格式 ---

                double[] ohInner = Array.ConvertAll(distOHInt.SArr, double.Parse); // 正极 OH 内侧值
                double[] ohOuter = Array.ConvertAll(distOHOut.SArr, double.Parse); // 负极 OH 外侧值
                double[] gapMaxArr = Array.ConvertAll(gapMax.SArr, double.Parse); // [neg边缘, pos边缘, pos中间]

                HOperatorSet.AreaCenter(recPE, out _, out HTuple peRows, out HTuple peCols);
                HOperatorSet.AreaCenter(recNE, out _, out HTuple neRows, out HTuple neCols);
                HOperatorSet.AreaCenter(
                    recMiddlePE,
                    out _,
                    out HTuple middlePERows,
                    out HTuple middlePECols
                );
                int positiveCount = peRows.DArr.Length; // 正极实测层数
                int negativeCount = neRows.DArr.Length; // 负极实测层数

                // 从文件名第二段提取视野类型，如 "1-X"、"2-Y"（文件命名规则：条码_视野_...）
                //string imageType = System.IO.Path.GetFileName(imagePath).Split('_')[1];
                var (_, imageType) = ParseImageFileName(imagePath);

                // ===== STEP 2：执行判断规则 =====

                string judgeResult = NgOkJudge(
                    positiveCount, // 正极层数
                    negativeCount, // 负极层数
                    ohInner, // 正极 OH 内侧值数组（用于 OH 越限 + Y 方向掉片判断）
                    ohOuter, // 负极 OH 外侧值数组（预留，当前判断未直接用到）
                    hDNe, // 负极差值（用于 1-X / 1-Y / 4-X / 4-Y 负极差判断）
                    imageType, // 视野类型标识（路由阈值分支）
                    gapMaxArr, // 间隙最大值数组（用于边缘 / 中间间隙判断）
                    neNGflag, // 负极压伤标志
                    neTQflag // 负极突起标志
                );

                // 结果编码嵌入文件名：OK → "_result_OK"；NG → "_result_NG_{缺陷类型}"
                // 输出示例：355125701G160046_1-Y_result_OK.png / 355125701G160046_1-Y_result_NG_OH不良.png
                //var resultCode = isOk ? "OK" : $"NG_{defectType}";
                var outputPath = BuildOutputPath(imagePath, FindTarget(TargetMonitorId), judgeResult);

                // ===== STEP 3：生成标注结果图 =====

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

                string resultCode = judgeResult.StartsWith("OK") ? "OK" : judgeResult;
                // ctCode：AI 对 OH 偏大/偏小的分类描述，无则传 null
                string? ctCode = judgeResult.Contains("偏") ? judgeResult : null;

                DrawingThresholds thresholds = ResolveDrawingThresholds(imageType);

                HOperatorSet.GetImageSize(originImg, out HTuple width, out HTuple height);
                var imgWidth = (int)width.I;
                var imgHeight = (int)height.I;
                Logger.Info($"[图像处理] 获取图片尺寸 [宽：{imgWidth}，高：{imgHeight}]");

                bool rendered = ResultImageRenderer.GenerateResultImage(
                    originImg,
                    imgWidth,
                    imgHeight,
                    recPE,
                    recNE,
                    recMiddlePE,
                    peRows,
                    peCols,
                    neRows,
                    neCols,
                    middlePERows,
                    middlePECols,
                    distOHInt,
                    distOHOut,
                    distSortPEColMiddle: null,
                    hDNe,
                    ctCode,
                    resultCode,
                    GetAbnormalPositions(ohInner, imageType),
                    ohAbnormalPositions2: new List<int>(),
                    arr1: ohInner,
                    arr2: ohOuter,
                    distGapMax: gapMaxArr,
                    imagePath,
                    outputPath,
                    thresholds,
                    Logger
                );

                if (!rendered)
                {
                    outputPath = imagePath; // 结果图渲染失败则返回原图路径
                }

                var (isOk, defectType) = NormalizeResult(judgeResult); // NormalizeResult 将 "NG_正极多片" → (false, "正极多片")，"OK" → (true, "OK")
                double[]? yOhValues = imageType.Contains("-Y") ? ohInner : null;

                originImg.Dispose();
                recNE.Dispose();
                recPE.Dispose();
                recMiddlePE.Dispose();
                distOHInt.Dispose();
                distOHOut.Dispose();
                gapMax.Dispose();
                peRows.Dispose();
                peCols.Dispose();
                neRows.Dispose();
                neCols.Dispose();
                middlePERows.Dispose();
                middlePECols.Dispose();
                width.Dispose();
                height.Dispose();

                // ===== STEP 3：映射并返回结果 =====
                return (isOk, defectType, outputPath, yOhValues, gapMaxArr);
            }
            catch (Exception ex)
            {
                Logger.Error("[算法异常]", ex);
                return (false, "算法异常", $"{ex}", null, null);
            }
        }
    }
}
