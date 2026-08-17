using HalconDotNet;
using RemoteFileMonitor.Abstractions;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Models.Project.CTOL;
using System.IO;

namespace SUNWODA_SEVB.Services.Project.CTOL.Processing
{
    public class RCDImageProcessor : ImageProcessorBase
    {
        protected override string TargetMonitorId => "RCD";
        protected override int WorkerStationId => 3;

        public RCDImageProcessor(
            IMonitorManager manager,
            ProcessingResultService resultService,
            BarcodeGroupingService groupingService,
            GapThresholdService gapThresholds,
            GapCsvService gapCsv,
            ILoggerService<RCDImageProcessor> logger
        )
            : base(manager, resultService, groupingService, gapThresholds, gapCsv, logger)
        {
            // 模型不再在本进程加载：算法由基类独占的 C01OnlineCT 子进程执行。
            // 如需提前预热子进程（加载模型），可在宿主启动时调用 WarmupAsync()。
            WarmupAsync();
        }

        protected override (
            bool isOk,
            string defectType,
            string outputPath,
            double[]? yOhValues,
            double[]? gapValues
        ) ProcessImage(string imagePath, CtAlgorithmResult algorithm)
        {
            // ===== STEP 1：消费子进程返回的检测原始数据 =====
            // 算法已在独立 C01OnlineCT 子进程内完成：
            //   - 分割结果以 .hobj 文件落盘，路径见 algorithm.RecNEPath / RecPEPath / RecMiddlePEPath；
            //   - OH/间隙等标量与数组结果直接来自 algorithm 字段。
            // 本方法只负责读取这些结果并完成判定、绘图与输出。

            HObject? originImg = null;
            HObject? recNE = null;
            HObject? recPE = null;
            HObject? recMiddlePE = null;
            HTuple? distOHInt = null;
            HTuple? distOHOut = null;
            HTuple? peRows = null;
            HTuple? peCols = null;
            HTuple? neRows = null;
            HTuple? neCols = null;
            HTuple? middlePERows = null;
            HTuple? middlePECols = null;
            HTuple? width = null;
            HTuple? height = null;

            try
            {
                // 子进程算法内部异常：errorReport 非空表示算法内部异常，跳过后续判断。
                if (!string.IsNullOrEmpty(algorithm.ErrorReport))
                    return (false, "算法异常", $"{algorithm.ErrorReport}", null, null);

                // 校验子进程是否输出了完整的分割结果。
                if (!algorithm.HasHObject
                    || string.IsNullOrWhiteSpace(algorithm.RecNEPath)
                    || string.IsNullOrWhiteSpace(algorithm.RecPEPath)
                    || string.IsNullOrWhiteSpace(algorithm.RecMiddlePEPath)
                    || !File.Exists(algorithm.RecNEPath)
                    || !File.Exists(algorithm.RecPEPath)
                    || !File.Exists(algorithm.RecMiddlePEPath))
                {
                    return (false, "算法异常", "子进程未输出完整的分割结果(HObject)", null, null);
                }

                // 读取原图与子进程产出的分割结果。
                HOperatorSet.ReadImage(out originImg, imagePath);
                HOperatorSet.ReadObject(out recNE, algorithm.RecNEPath); // 负极区域分割结果
                HOperatorSet.ReadObject(out recPE, algorithm.RecPEPath); // 正极区域分割结果
                HOperatorSet.ReadObject(out recMiddlePE, algorithm.RecMiddlePEPath); // 正极中间区域分割结果

                // --- 子进程返回的标量/数组结果 ---
                double[] ohInner = algorithm.DistOHInt; // 正极 OH 内侧值
                double[] ohOuter = algorithm.DistOHOut; // 负极 OH 外侧值
                double[] gapMaxArr = algorithm.GapMax; // [neg边缘, pos边缘, pos中间]
                float hDNe = algorithm.HDNe; // 负极差值
                int neNGflag = algorithm.NeNGFlag; // 负极压伤标志（0=正常，1=压伤）
                int neTQflag = algorithm.NeTQFlag; // 负极突起标志（0=正常，1=突起）

                // 重建绘图所需的 HTuple（由 double[] 构造）。
                distOHInt = new HTuple(ohInner);
                distOHOut = new HTuple(ohOuter);

                HOperatorSet.AreaCenter(recPE, out _, out peRows, out peCols);
                HOperatorSet.AreaCenter(recNE, out _, out neRows, out neCols);
                HOperatorSet.AreaCenter(
                    recMiddlePE,
                    out _,
                    out middlePERows,
                    out middlePECols
                );
                int positiveCount = peRows.DArr.Length; // 正极实测层数
                int negativeCount = neRows.DArr.Length; // 负极实测层数

                // 从文件名第二段提取视野类型，如 "1-X"、"2-Y"（文件命名规则：条码_视野_...）
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
                var outputPath = BuildOutputPath(imagePath, FindTarget(TargetMonitorId), judgeResult);

                // ===== STEP 3：生成标注结果图 =====

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

                string resultCode = judgeResult.StartsWith("OK") ? "OK" : judgeResult;
                // ctCode：AI 对 OH 偏大/偏小的分类描述，无则传 null
                string? ctCode = judgeResult.Contains("偏") ? judgeResult : null;

                DrawingThresholds thresholds = ResolveDrawingThresholds(imageType);

                HOperatorSet.GetImageSize(originImg, out width, out height);
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

                // ===== STEP 4：映射并返回结果 =====
                return (isOk, defectType, outputPath, yOhValues, gapMaxArr);
            }
            catch (Exception ex)
            {
                Logger.Error("[算法异常]", ex);
                return (false, "算法异常", $"{ex}", null, null);
            }
            finally
            {
                originImg?.Dispose();
                recNE?.Dispose();
                recPE?.Dispose();
                recMiddlePE?.Dispose();
                distOHInt?.Dispose();
                distOHOut?.Dispose();
                peRows?.Dispose();
                peCols?.Dispose();
                neRows?.Dispose();
                neCols?.Dispose();
                middlePERows?.Dispose();
                middlePECols?.Dispose();
                width?.Dispose();
                height?.Dispose();
            }
        }
    }
}
