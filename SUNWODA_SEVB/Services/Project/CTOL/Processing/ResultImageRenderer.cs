#define C06

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HalconDotNet;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Tool.Helper;

namespace SUNWODA_SEVB.Services.Project.CTOL.Processing
{
    // ─── 阈值参数包 ───────────────────────────────────────────────────────────────
    // 由 ImageProcessorBase.ResolveDrawingThresholds(imageType) 填充，
    // 再传入 ResultImageRenderer，避免渲染器持有状态。

    /// <summary>
    /// 当前视野类型对应的判断阈值，供标注渲染使用。
    /// 由 <see cref="ImageProcessorBase.ResolveDrawingThresholds"/> 根据图片文件名生成。
    /// </summary>
    internal record DrawingThresholds(
        double OhLower, // OH 下限（用于 ohBz 标准范围字符串显示）
        double OhUpper, // OH 上限
        double FjEndThreshold, // 负极边缘间隙阈值
        double PosEndThreshold, // 正极边缘间隙阈值
        double PosMiddleThreshold, // 正极中间间隙阈值
        double NegativeDiffThreshold, // 负极差阈值（仅 1-X/1-Y/4-X/4-Y 有效）
        bool ShowNegativeDiffThreshold // 是否显示负极差行（2-X/2-Y/3-X/3-Y 不显示）
    );

    // ─── 标注渲染器 ───────────────────────────────────────────────────────────────

    /// <summary>
    /// 结果图标注渲染器 — 在原始图像上叠加极片层数、OH 值、间隙、标记点等可视化信息。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>GenerateResultImage / DrawTopInfo / DrawRegionMarkers / DrawTextInfo / DrawOhValueTable</c>
    /// 五个方法。
    /// </para>
    /// <para>
    /// 所有 HalconDotNet API 调用均以 <c>// TODO [HalconDotNet]</c> 注释形式保留，
    /// 待依赖引入后逐块取消注释并将占位 <c>object</c> 参数改回对应 Halcon 类型。
    /// </para>
    /// </remarks>
    internal static class ResultImageRenderer
    {
        // =========================================================================
        // 公共入口
        // =========================================================================

        /// <summary>
        /// 在原图上叠加检测标注，将结果图写入 <paramref name="outputPath"/>。
        /// </summary>
        /// <param name="imageFilePath">原图完整路径（用于解析条码、视野编号）</param>
        /// <param name="outputPath">结果图保存路径（PNG）</param>
        /// <param name="resultCode">判断结果，"OK" 或 "NG/…" 等描述性字符串</param>
        /// <param name="ctCode">
        /// AI 输出的分类编码，含 "偏大"/"偏小" 时影响 OH 最大/最小值的标注颜色；
        /// 无分类结论时传 <see langword="null"/>
        /// </param>
        /// <param name="thresholds">由 PngProcessorBase.ResolveDrawingThresholds 解析的阈值</param>
        /// <returns><see langword="true"/> 表示渲染成功并已写入 <paramref name="outputPath"/>；
        /// <see langword="false"/> 表示渲染失败，调用方应回退为保存无标注原图。</returns>
        public static bool GenerateResultImage(
            HObject originImg, // HObject — 原图（由 ZZC6CTModel.Algorithm_Action 输出）
            int imgWidth, // 原图像素宽度
            int imgHeight, // 原图像素高度
            HObject recPE, // HObject — 正极分割区域
            HObject recNE, // HObject — 负极分割区域
            HObject recMiddlePE, // HObject — 正极中间区域
            HTuple peRows, // HTuple  — 正极区域行中心坐标
            HTuple peCols, // HTuple  — 正极区域列中心坐标
            HTuple neRows, // HTuple  — 负极区域行中心坐标
            HTuple neCols, // HTuple  — 负极区域列中心坐标
            HTuple middlePERows, // HTuple  — 正极中间区域行中心坐标
            HTuple middlePECols, // HTuple  — 正极中间区域列中心坐标
            HTuple distOHInt, // HTuple  — 正极 OH 内侧值数组（SArr = string[]）
            HTuple distOHOut, // HTuple  — 负极 OH 外侧值数组（SArr = string[]）
            HTuple? distSortPEColMiddle, // HTuple  — 正极排序后的中间列坐标（当前渲染未直接使用，预留）
            double hDNe, // 负极差值
            string? ctCode,
            string resultCode,
            List<int> ohAbnormalPositions1, // 正极 OH 异常层位置索引
            List<int> ohAbnormalPositions2, // 负极 OH 异常层位置索引
            double[] arr1, // 正极 OH 内侧 double 数组（由 distOHInt.SArr 转换）
            double[] arr2, // 负极 OH 外侧 double 数组（由 distOHOut.SArr 转换）
            double[] distGapMax, // 间隙最大值数组：[负极边缘, 正极边缘, 正极中间]
            string imageFilePath,
            string outputPath,
            DrawingThresholds thresholds,
            ILoggerService logger
        )
        {
            HWindow? tempWindow = null;
            try
            {
                // ① 创建离屏不可见渲染窗口
                //    宽度至少 800；高度在原图基础上增加 720 行用于文字标注区域
                tempWindow = new HWindow();
                int windowWidth = Math.Max(imgWidth, 800);
                int windowHeight = imgHeight + 720;
                tempWindow.OpenWindow(
                    0,
                    0,
                    windowWidth,
                    windowHeight,
                    new HTuple(0),
                    "invisible",
                    ""
                );

                // ② 设置显示区域：顶部留 60 行用于文字，底部延伸 660 行用于数值标注
                tempWindow.SetPart(-60, 0, imgHeight + 660, imgWidth - 1);

                // ③ 显示原图
                tempWindow.DispObj(originImg);

                // ④ 绘制顶部文本：条码 + 视野编号 + 检测结果
                DrawTopInfo(imageFilePath, resultCode, tempWindow);

                // ⑤ 绘制正极标记点（橙色；异常位置改红色）
                DrawRegionMarkers(
                    (HTuple)peRows,
                    (HTuple)peCols,
                    ohAbnormalPositions1,
                    tempWindow,
                    normalColor: "orange",
                    abnormalColor: "red"
                );

                // ⑥ 绘制负极标记点（黄色；异常位置改红色）
                DrawRegionMarkers(
                    (HTuple)neRows,
                    (HTuple)neCols,
                    ohAbnormalPositions2,
                    tempWindow,
                    normalColor: "yellow",
                    abnormalColor: "red"
                );

                // ⑦ 绘制正极中间标记点（蓝色；无异常，两个颜色参数相同）
                DrawRegionMarkers(
                    (HTuple)middlePERows,
                    (HTuple)middlePECols,
                    new List<int>(),
                    tempWindow,
                    normalColor: "blue",
                    abnormalColor: "blue"
                );

                // ⑧ 绘制数值标注区域（层数、OH 值、间隙、逐层表格）
                DrawTextInfo(
                    ctCode,
                    imageFilePath,
                    imgHeight,
                    peCount: ((HTuple)peRows).Length,
                    neCount: ((HTuple)neRows).Length,
                    hDNe,
                    distOHInt,
                    distOHOut,
                    arr1,
                    arr2,
                    ohAbnormalPositions1,
                    ohAbnormalPositions2,
                    tempWindow,
                    distGapMax,
                    thresholds
                );

                // ⑨ 导出标注图像：DumpWindow → 临时 PNG → 复制到 outputPath
                string tempFile = Path.GetTempFileName() + ".png";
                try
                {
                    tempWindow.DumpWindow("png", tempFile);
                    File.Copy(tempFile, outputPath, overwrite: true);
                    logger.Info(
                        $"[结果图渲染] 导出标注图像成功, 文件: {Path.GetFileName(imageFilePath)}"
                    );
                    return true;
                }
                catch (Exception ex)
                {
                    logger.Error(
                        $"[结果图渲染] 导出标注图像失败: {ex.Message}, 文件: {Path.GetFileName(imageFilePath)}"
                    );
                    return false;
                }
                finally
                {
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }
            }
            catch (Exception ex)
            {
                // Halcon 运行时异常：降级，由调用方保存无标注原图
                logger.Error($"[结果图渲染]: {ex.Message}");
                return false;
            }
            finally
            {
                tempWindow?.CloseWindow();
                tempWindow?.Dispose();
            }
        }

        // =========================================================================
        // 私有绘制方法
        // =========================================================================

        /// <summary>
        /// 绘制顶部信息栏：电芯条码、图片编号、检测结果（OK 绿色 / NG 红色）。
        /// </summary>
        private static void DrawTopInfo(string imageFilePath, string resultCode, HWindow hWindow)
        {
            string fileName = Path.GetFileNameWithoutExtension(imageFilePath);
            // 从文件名解析条码和图片编号
            string barcode = "未知条码";
            string imgNum = "?";

            if (fileName.EndsWith("_origin", StringComparison.OrdinalIgnoreCase))
            {
                // 去掉末尾 "_origin"
                string mainPart = fileName.Substring(0, fileName.Length - "_origin".Length);

                // 从最后一个 "_" 切分，避免 barcode 中本身包含下划线时解析错误
                int lastUnderscoreIndex = mainPart.LastIndexOf('_');
                if (lastUnderscoreIndex > 0 && lastUnderscoreIndex < mainPart.Length - 1)
                {
                    barcode = mainPart.Substring(0, lastUnderscoreIndex);
                    imgNum = mainPart.Substring(lastUnderscoreIndex + 1);
                }
                else
                {
                    barcode = mainPart;
                }
            }

            
            //string[] parts = fileName.Split('_');
            //string barcode = parts.Length > 0 ? parts[0] : "未知条码";
            //string imgNum =
            //    parts.Length > 1
            //        ? (parts[1] == "auto" && parts.Length > 2 ? parts[2] : parts[1])
            //        : "?";

            // 行 0，列 0：白色主文字（条码 + 视野编号 + 结果标签）
            hWindow.SetFont("Times New Roman-35");
            hWindow.SetColor("white");
            string headerText = $"电芯条码:{barcode}\t\t\t图片编号:{imgNum}\t\t\t检测结果:";
            var len = headerText.Length;
            hWindow.DispText(headerText, "window", 0, 0, "white", "box", "false");

            // 行 5，列 1180：结果值（OK=绿色 / NG=红色）
            bool isOk = resultCode.StartsWith("OK", StringComparison.OrdinalIgnoreCase);
            string resColor = isOk ? "green" : "red";
            hWindow.SetColor(resColor);
            hWindow.DispText(resultCode, "window", 5, len * 23, resColor, "box", "false");
        }

        /// <summary>
        /// 绘制极片电极标记点（五角星形状）。
        /// 正常位置使用 <paramref name="normalColor"/>，异常位置使用 <paramref name="abnormalColor"/>。
        /// 移植自 README.md <c>DrawRegionMarkers</c> 方法。
        /// </summary>
        private static void DrawRegionMarkers(
            HTuple hRows, // HTuple — 各电极区域行中心坐标
            HTuple hCols, // HTuple — 各电极区域列中心坐标
            List<int> abnormalPositions, // 异常位置索引列表（标红）
            HWindow hWindow, // HWindow — Halcon 渲染窗口
            string normalColor,
            string abnormalColor
        )
        {
            HObject normalMark;
            HObject abnormalMark;
            HOperatorSet.GenEmptyObj(out normalMark);
            HOperatorSet.GenEmptyObj(out abnormalMark);
            try
            {
                for (int i = 0; i < hRows.Length; i++)
                {
                    double drawRow = hRows.DArr[i];
                    double drawCol = hCols.DArr[i];

                    HObject star = HImageViewerHelper.GenFillCTStar(drawRow, drawCol, 0);

                    bool isAbnormal = abnormalPositions.Contains(i);
                    if (isAbnormal)
                    {
                        HOperatorSet.ConcatObj(abnormalMark, star, out HObject merged);
                        abnormalMark?.Dispose();
                        abnormalMark = merged;
                    }
                    else
                    {
                        HOperatorSet.ConcatObj(normalMark, star, out HObject merged);
                        normalMark?.Dispose();
                        normalMark = merged;
                    }
                    star.Dispose();
                }
                if (normalMark != null)
                {
                    hWindow.SetColor(normalColor);
                    hWindow.DispObj(normalMark);
                }
                if (abnormalMark != null)
                {
                    hWindow.SetColor(abnormalColor);
                    hWindow.DispObj(abnormalMark);
                }
            }
            finally
            {
                normalMark?.Dispose();
                abnormalMark?.Dispose();
            }
        }

        /// <summary>
        /// 绘制图像底部扩展区域的数值标注（层数、OH 值、间隙值及对应阈值）。
        /// </summary>
        private static void DrawTextInfo(
            string? ctCode,
            string imageFilePath,
            int imgHeight,
            int peCount, // 正极实测层数（C06 期望 76, C01 期望 107）
            int neCount, // 负极实测层数（C06 期望 77, C01 期望 108）
            double hDNe, // 负极差值
            HTuple ohInt, // HTuple — 正极 OH 内侧值（.SArr = string[]）
            HTuple ohOut, // HTuple — 负极 OH 外侧值（.SArr = string[]）
            double[] arr1, // 正极 OH 内侧 double 数组
            double[] arr2, // 负极 OH 外侧 double 数组
            List<int> ohAbnormalPositions1,
            List<int> ohAbnormalPositions2,
            HWindow hWindow, // HWindow — Halcon 渲染窗口
            double[] distGapMax, // [负极边缘最大值, 正极边缘最大值, 正极中间最大值]
            DrawingThresholds thresholds
        )
        {
            hWindow.SetFont("Times New Roman-23");

            // ── OH 标准范围字符串（来自当前视野阈值）────────────────────────────
            string ohBz = $"[{thresholds.OhLower:F2}, {thresholds.OhUpper:F2}]";

            // ── OH 综合极值（正极内侧 + 负极外侧合并求 min/max）──────────────────
            double[] ohMaxMin = GapMax(ohInt.SArr, ohOut.SArr);
            // ohMaxMin[0] = 综合最小值，ohMaxMin[1] = 综合最大值

            // ── 间隙最大值（AI 输出 distGapMax[] 数组）───────────────────────────
            double fjEndMax = distGapMax.Length > 0 ? distGapMax[0] : 0; // 负极边缘
            double posEndMax = distGapMax.Length > 1 ? distGapMax[1] : 0; // 正极边缘
            double posMiddleMax = distGapMax.Length > 2 ? distGapMax[2] : 0; // 正极中间

            // ── 布局常量 ──────────────────────────────────────────────────────────
            const int titleX = 5; // 标签列 X 起始
            const int valueX = 230; // 数值列 X
            const int threshX = 230; // 阈值列 X
            const int lineHeight = 28; // 行间距（像素）
            int currentRow = imgHeight + 60; // 标注区起始行（原图底部下方 60 行）

#if C06

            // ── 正极层数 ──────────────────────────────────────────────────────────
            bool peOk = (peCount == 76);
            DrawLabelValue(
                hWindow,
                currentRow,
                titleX,
                "正极层数:",
                "orange",
                $"{peCount} (标准76)",
                peOk ? "green" : "red",
                valueX
            );
            currentRow += lineHeight;

            // ── 负极层数 ──────────────────────────────────────────────────────────
            bool neOk = (neCount == 77);
            DrawLabelValue(
                hWindow,
                currentRow,
                titleX,
                "负极层数:",
                "orange",
                $"{neCount} (标准77)",
                neOk ? "green" : "red",
                valueX
            );
            currentRow += lineHeight;
#else
            // ── 正极层数 ──────────────────────────────────────────────────────────
            bool peOk = (peCount == 107);
            DrawLabelValue(
                hWindow,
                currentRow,
                titleX,
                "正极层数:",
                "orange",
                $"{peCount} (标准107)",
                peOk ? "green" : "red",
                valueX
            );
            currentRow += lineHeight;

            // ── 负极层数 ──────────────────────────────────────────────────────────
            bool neOk = (neCount == 108);
            DrawLabelValue(
                hWindow,
                currentRow,
                titleX,
                "负极层数:",
                "orange",
                $"{neCount} (标准108)",
                neOk ? "green" : "red",
                valueX
            );
            currentRow += lineHeight;
#endif

            // ── OH 最大值（ctCode 含 "偏大" 时标红）──────────────────────────────
            bool ohMaxOver = ctCode != null && ctCode.Contains("偏大");
            DrawLabelValue(
                hWindow,
                currentRow,
                titleX,
                "OH最大值:",
                "orange",
                $"{ohMaxMin[1]:F3}",
                ohMaxOver ? "red" : "green",
                valueX
            );
            currentRow += lineHeight;

            // ── OH 最小值（ctCode 含 "偏小" 时标红）──────────────────────────────
            bool ohMinOver = ctCode != null && ctCode.Contains("偏小");
            DrawLabelValue(
                hWindow,
                currentRow,
                titleX,
                "OH最小值:",
                "orange",
                $"{ohMaxMin[0]:F3}",
                ohMinOver ? "red" : "green",
                valueX
            );
            currentRow += lineHeight;

            // ── OH 标准范围（橙色提示）────────────────────────────────────────────
            DrawLabelValue(
                hWindow,
                currentRow,
                titleX,
                "OH标准:",
                "orange",
                ohBz,
                "orange",
                valueX
            );
            currentRow += lineHeight;

            // ── 负极差（仅 1-X / 1-Y / 4-X / 4-Y 显示）──────────────────────────
            if (thresholds.ShowNegativeDiffThreshold)
            {
                bool negDiffOver = hDNe > thresholds.NegativeDiffThreshold;
                DrawLabelValue(
                    hWindow,
                    currentRow,
                    titleX,
                    "负极差值:",
                    "orange",
                    $"{hDNe:F3} {(negDiffOver ? "(异常)" : "")}",
                    negDiffOver ? "red" : "green",
                    valueX
                );
                currentRow += lineHeight;

                DrawLabelValue(
                    hWindow,
                    currentRow,
                    titleX,
                    "负极差阈值:",
                    "orange",
                    $"{thresholds.NegativeDiffThreshold:F3}",
                    "green",
                    threshX
                );
                currentRow += lineHeight;
            }

            // ── 负极边缘间隙 ──────────────────────────────────────────────────────
            bool fjEndOver = fjEndMax > thresholds.FjEndThreshold;
            DrawLabelValue(
                hWindow,
                currentRow,
                titleX,
                "负极边缘间隙最大值:",
                "orange",
                $"{fjEndMax:F3} {(fjEndOver ? "(异常)" : "")}",
                fjEndOver ? "red" : "green",
                valueX
            );
            currentRow += lineHeight;

            DrawLabelValue(
                hWindow,
                currentRow,
                titleX,
                "负极边缘间隙阈值:",
                "orange",
                $"{thresholds.FjEndThreshold:F3}",
                "green",
                threshX
            );
            currentRow += lineHeight;

            // ── 正极边缘间隙 ──────────────────────────────────────────────────────
            bool posEndOver = posEndMax > thresholds.PosEndThreshold;
            DrawLabelValue(
                hWindow,
                currentRow,
                titleX,
                "正极边缘间隙最大值:",
                "orange",
                $"{posEndMax:F3} {(posEndOver ? "(异常)" : "")}",
                posEndOver ? "red" : "green",
                valueX
            );
            currentRow += lineHeight;

            DrawLabelValue(
                hWindow,
                currentRow,
                titleX,
                "正极边缘间隙阈值:",
                "orange",
                $"{thresholds.PosEndThreshold:F3}",
                "green",
                threshX
            );
            currentRow += lineHeight;

            // ── 正极中间间隙 ──────────────────────────────────────────────────────
            bool posMidOver = posMiddleMax > thresholds.PosMiddleThreshold;
            DrawLabelValue(
                hWindow,
                currentRow,
                titleX,
                "正极中间间隙最大值:",
                "orange",
                $"{posMiddleMax:F3} {(posMidOver ? "(异常)" : "")}",
                posMidOver ? "red" : "green",
                valueX
            );
            currentRow += lineHeight;

            DrawLabelValue(
                hWindow,
                currentRow,
                titleX,
                "正极中间间隙阈值:",
                "orange",
                $"{thresholds.PosMiddleThreshold:F3}",
                "green",
                threshX
            );

            // ── OH 逐层数值表格（最多 5 列 × 22 行）──────────────────────────────
            if (arr1 != null && arr2 != null)
                DrawOhValueTable(
                    arr1,
                    arr2,
                    ohAbnormalPositions1,
                    ohAbnormalPositions2,
                    imgHeight,
                    hWindow
                );
        }

        /// <summary>
        /// 绘制 OH 逐层数值表格（5 列 × 最多 22 行）。
        /// </summary>
        private static void DrawOhValueTable(
            double[] arr1,
            double[] arr2,
            List<int> abnormal1,
            List<int> abnormal2,
            int imgHeight,
            HWindow hWindow
        )
        {
            const int columns = 5; // 横向分 5 列
            const int rowsPerCol = 22; // 每列最多 22 行
            const int lineSpacing = 28; // 行间距
            int tableStartRow = imgHeight + 60; // 表格起始行与 DrawTextInfo 标注区对齐

            hWindow.SetFont("Times New Roman-23");

            for (int col = 0; col < columns; col++)
            {
                int colOffset = 400 + col * 200; // 每列 X 起始偏移
                for (int row = 0; row < rowsPerCol; row++)
                {
                    int index = row + col * rowsPerCol;
                    if (index >= arr1.Length || index >= arr2.Length)
                        break;

                    int currentRow = tableStartRow + row * lineSpacing;

                    // 行标签（L001、L022…）
                    hWindow.SetColor("orange");
                    hWindow.DispText(
                        $"L{(index + 1):D3}:",
                        "window",
                        currentRow,
                        colOffset,
                        "orange",
                        "box",
                        "false"
                    );

                    // arr1（正极 OH 内侧）
                    string c1 = abnormal1.Contains(index) ? "red" : "green";
                    hWindow.SetColor(c1);
                    hWindow.DispText(
                        $"{arr1[index]:F3}",
                        "window",
                        currentRow,
                        colOffset + 60,
                        c1,
                        "box",
                        "false"
                    );

                    // arr2（负极 OH 外侧）
                    string c2 = abnormal2.Contains(index) ? "red" : "green";
                    hWindow.SetColor(c2);
                    hWindow.DispText(
                        $"{arr2[index]:F3}",
                        "window",
                        currentRow,
                        colOffset + 120,
                        c2,
                        "box",
                        "false"
                    );
                }
            }
        }

        /// <summary>
        /// 辅助：在指定行绘制标签文本（<paramref name="labelColor"/>）和数值文本（<paramref name="valueColor"/>）。
        /// </summary>
        private static void DrawLabelValue(
            HWindow hWindow,
            int row,
            int labelX,
            string label,
            string labelColor,
            string value,
            string valueColor,
            int valueX
        )
        {
            hWindow.SetColor(labelColor);
            hWindow.DispText(label, "window", row, labelX, labelColor, "box", "false");
            hWindow.SetColor(valueColor);
            hWindow.DispText(value, "window", row, valueX, valueColor, "box", "false");
        }

        // =========================================================================
        // 内部工具
        // =========================================================================

        /// <summary>
        /// 合并两组字符串数值，返回 <c>[min, max]</c> 组合极值。
        /// </summary>
        private static double[] GapMax(IEnumerable<string> sa1, IEnumerable<string> sa2)
        {
            var values = sa1.Concat(sa2)
                .Select(s => double.TryParse(s, out double v) ? (double?)v : null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();

            return values.Count == 0 ? [0.0, 0.0] : [values.Min(), values.Max()];
        }
    }
}
