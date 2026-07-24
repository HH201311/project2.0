using System.IO;
using OpenCvSharp;
using Point = OpenCvSharp.Point;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>
    /// 从 VG 第二象限「上」视图截图估计向量两点（简单 3-2-1 的「2」）。
    /// 主策略：检测 VG 黄色对齐框 → 沿其右侧边取两点（略偏旁侧，因黄线本身不可点）→ 保证与黄框平行且基线更长。
    /// 回退：旧剪影右缘竖直段（黄框未检出时）。
    /// 输出约定：[0]=上点，[1]=下点；点击顺序必须从上到下。
    /// </summary>
    public static class VisionTopViewVectorPointsDetector
    {
        /// <summary>相对黄框右缘向旁侧偏移像素（黄线不可点；正值=向矩形内侧）。</summary>
        public const int YellowEdgeOffsetPixels = 6;

        /// <summary>黄框边上两点最小竖直间距（像素）。</summary>
        public const int MinPointSeparationPx = 40;

        /// <summary>上点在右缘上的相对位置（0=顶，1=底）。</summary>
        public const double TopPointFraction = 0.10;

        /// <summary>下点在右缘上的相对位置。</summary>
        public const double BottomPointFraction = 0.90;

        /// <summary>回退：相对剪影右缘向物体内收像素。</summary>
        public const int EdgeInsetPixels = 3;

        /// <summary>回退：竖直段判定容差。</summary>
        public const int RightEdgeTolerancePx = 4;

        /// <summary>回退：仅在物体上半部取向量段。</summary>
        public const double VerticalSearchHeightRatio = 0.55;

        public sealed class DetectResult
        {
            /// <summary>[0] 上点；[1] 下点</summary>
            public (int X, int Y)[] Points { get; init; } = Array.Empty<(int X, int Y)>();

            public Point TopPoint { get; init; }
            public Point BottomPoint { get; init; }
            public Rect ObjectBounds { get; init; }
            public int VerticalSegmentTopY { get; init; }
            public int VerticalSegmentBottomY { get; init; }
            public string Mode { get; init; } = string.Empty;
            public string Summary { get; init; } = string.Empty;
        }

        public static DetectResult Detect(string imagePath, string? debugDir = null)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                throw new FileNotFoundException("「上」视图截图不存在", imagePath);
            }

            using var src = Cv2.ImRead(imagePath, ImreadModes.Color);
            if (src.Empty())
            {
                throw new InvalidOperationException($"无法读取图像：{imagePath}");
            }

            if (TryDetectByYellowRect(src, out var yellowResult, out var yellowDebug) && yellowDebug != null)
            {
                if (!string.IsNullOrWhiteSpace(debugDir))
                {
                    Directory.CreateDirectory(debugDir);
                    var stem = Path.GetFileNameWithoutExtension(imagePath);
                    SaveYellowDebugOverlay(
                        src,
                        yellowDebug,
                        yellowResult.TopPoint,
                        yellowResult.BottomPoint,
                        Path.Combine(debugDir, $"{stem}_vector_points.png"));
                    if (yellowDebug.YellowMask != null && !yellowDebug.YellowMask.Empty())
                    {
                        Cv2.ImWrite(
                            Path.Combine(debugDir, $"{stem}_vector_yellow_mask.png"),
                            yellowDebug.YellowMask);
                    }
                }

                yellowDebug.Dispose();
                return yellowResult;
            }

            yellowDebug?.Dispose();
            return DetectBySilhouetteFallback(src, imagePath, debugDir);
        }

        /// <summary>
        /// 主路径：HSV 提取黄色对齐框 → MinAreaRect → 右侧边 → 旁侧偏移两点。
        /// </summary>
        private static bool TryDetectByYellowRect(
            Mat src,
            out DetectResult result,
            out YellowDebugContext? debug)
        {
            result = new DetectResult();
            debug = null;

            using var hsv = new Mat();
            Cv2.CvtColor(src, hsv, ColorConversionCodes.BGR2HSV);

            // VG 黄框：高饱和黄（OpenCV H: 0～180，黄约 15～40）；S 下限略放宽以覆盖抗锯齿边缘
            using var yellowMask = new Mat();
            Cv2.InRange(hsv, new Scalar(12, 50, 100), new Scalar(42, 255, 255), yellowMask);

            // 仅 Close 连通细缝；禁止 Open——黄框常为 1～2px 细线，Open 会整框抹掉
            using var k = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3));
            Cv2.MorphologyEx(yellowMask, yellowMask, MorphTypes.Close, k);

            var yellowPixels = Cv2.CountNonZero(yellowMask);
            if (yellowPixels < 60)
            {
                return false;
            }

            Cv2.FindContours(
                yellowMask,
                out var contours,
                out _,
                RetrievalModes.External,
                ContourApproximationModes.ApproxNone);

            if (contours.Length == 0)
            {
                return false;
            }

            // 黄框多为细线矩形：按周长优先（面积可能很小）
            var best = contours
                .Select(c => (Contour: c, Area: Cv2.ContourArea(c), Peri: Cv2.ArcLength(c, true)))
                .Where(x => x.Peri >= 60)
                .OrderByDescending(x => x.Peri)
                .ThenByDescending(x => x.Area)
                .FirstOrDefault();

            if (best.Contour is null || best.Contour.Length < 4)
            {
                return false;
            }

            var rotated = Cv2.MinAreaRect(best.Contour);
            var boxSize = rotated.Size;
            var longSide = Math.Max(boxSize.Width, boxSize.Height);
            var shortSide = Math.Min(boxSize.Width, boxSize.Height);
            // 细线框的 MinAreaRect「短边」可能只有几像素，但仍可用长边作向量基线
            if (longSide < MinPointSeparationPx)
            {
                return false;
            }

            var boxPts = Cv2.BoxPoints(rotated);
            if (!TryGetRightVerticalEdge(boxPts, out var edgeTop, out var edgeBottom))
            {
                return false;
            }

            var edgeLen = Distance(edgeTop, edgeBottom);
            if (edgeLen < MinPointSeparationPx)
            {
                return false;
            }

            // 沿右缘取两点（用黄框全长，基线远长于外壳短段）
            var topOnEdge = Lerp(edgeTop, edgeBottom, TopPointFraction);
            var botOnEdge = Lerp(edgeTop, edgeBottom, BottomPointFraction);

            // 向矩形中心偏移，落在黄线旁侧可点区域（黄线本身不可点）
            var rectCenter = new Point2f(
                (boxPts[0].X + boxPts[1].X + boxPts[2].X + boxPts[3].X) / 4f,
                (boxPts[0].Y + boxPts[1].Y + boxPts[2].Y + boxPts[3].Y) / 4f);
            var edgeMid = new Point2f(
                (edgeTop.X + edgeBottom.X) / 2f,
                (edgeTop.Y + edgeBottom.Y) / 2f);
            var inward = new Point2f(rectCenter.X - edgeMid.X, rectCenter.Y - edgeMid.Y);
            var inwardLen = Math.Sqrt(inward.X * inward.X + inward.Y * inward.Y);
            if (inwardLen < 1e-3)
            {
                // 退化：水平内收
                inward = new Point2f(-1, 0);
                inwardLen = 1;
            }

            var ox = (float)(inward.X / inwardLen * YellowEdgeOffsetPixels);
            var oy = (float)(inward.Y / inwardLen * YellowEdgeOffsetPixels);

            var top = ClampPoint(
                new Point(
                    (int)Math.Round(topOnEdge.X + ox),
                    (int)Math.Round(topOnEdge.Y + oy)),
                src.Width,
                src.Height);
            var bottom = ClampPoint(
                new Point(
                    (int)Math.Round(botOnEdge.X + ox),
                    (int)Math.Round(botOnEdge.Y + oy)),
                src.Width,
                src.Height);

            if (bottom.Y <= top.Y)
            {
                (top, bottom) = (bottom, top);
            }

            if (bottom.Y - top.Y < MinPointSeparationPx / 2)
            {
                return false;
            }

            var bounds = Cv2.BoundingRect(best.Contour);
            result = new DetectResult
            {
                Points = new (int X, int Y)[] { (top.X, top.Y), (bottom.X, bottom.Y) },
                TopPoint = top,
                BottomPoint = bottom,
                ObjectBounds = bounds,
                VerticalSegmentTopY = Math.Min(top.Y, bottom.Y),
                VerticalSegmentBottomY = Math.Max(top.Y, bottom.Y),
                Mode = "yellow-rect",
                Summary =
                    $"mode=yellow-rect, yellowPx={yellowPixels}, " +
                    $"box=({rotated.Center.X:F0},{rotated.Center.Y:F0}) {longSide:F0}x{shortSide:F0}∠{rotated.Angle:F1}°, " +
                    $"offset={YellowEdgeOffsetPixels}px, " +
                    $"Top=({top.X},{top.Y}), Bottom=({bottom.X},{bottom.Y}), dy={bottom.Y - top.Y}"
            };

            debug = new YellowDebugContext
            {
                YellowMask = yellowMask.Clone(),
                BoxPoints = boxPts.Select(p => new Point((int)Math.Round(p.X), (int)Math.Round(p.Y))).ToArray(),
                EdgeTop = new Point((int)Math.Round(edgeTop.X), (int)Math.Round(edgeTop.Y)),
                EdgeBottom = new Point((int)Math.Round(edgeBottom.X), (int)Math.Round(edgeBottom.Y)),
            };
            return true;
        }

        /// <summary>
        /// 从旋转矩形四顶点中取「右侧较竖直」的边，并整理为上→下。
        /// </summary>
        private static bool TryGetRightVerticalEdge(
            Point2f[] boxPts,
            out Point2f edgeTop,
            out Point2f edgeBottom)
        {
            edgeTop = default;
            edgeBottom = default;
            if (boxPts.Length != 4)
            {
                return false;
            }

            // 四条边，按「更竖直」排序，取最竖直的两条中偏右的一条
            var edges = new (Point2f A, Point2f B, float MidX, float VertScore, float Len)[4];
            for (var i = 0; i < 4; i++)
            {
                var a = boxPts[i];
                var b = boxPts[(i + 1) % 4];
                var dx = b.X - a.X;
                var dy = b.Y - a.Y;
                var len = (float)Math.Sqrt(dx * dx + dy * dy);
                var vertScore = len < 1e-3 ? 0f : Math.Abs(dy) / (Math.Abs(dx) + 1e-3f);
                edges[i] = (a, b, (a.X + b.X) / 2f, vertScore, len);
            }

            var verticalPair = edges
                .OrderByDescending(e => e.VertScore)
                .ThenByDescending(e => e.Len)
                .Take(2)
                .OrderByDescending(e => e.MidX)
                .First();

            // 至少要比水平边更竖直
            if (verticalPair.VertScore < 0.5f)
            {
                // 黄框接近水平正方形时：取四点中 X 较大的两个点连线
                var ordered = boxPts.OrderByDescending(p => p.X).Take(2).OrderBy(p => p.Y).ToArray();
                edgeTop = ordered[0];
                edgeBottom = ordered[1];
                return Distance(edgeTop, edgeBottom) >= MinPointSeparationPx * 0.5;
            }

            if (verticalPair.A.Y <= verticalPair.B.Y)
            {
                edgeTop = verticalPair.A;
                edgeBottom = verticalPair.B;
            }
            else
            {
                edgeTop = verticalPair.B;
                edgeBottom = verticalPair.A;
            }

            return true;
        }

        private static DetectResult DetectBySilhouetteFallback(Mat src, string imagePath, string? debugDir)
        {
            using var gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

            using var mask = BuildSilhouetteMask(gray);
            Cv2.FindContours(
                mask,
                out var contours,
                out _,
                RetrievalModes.External,
                ContourApproximationModes.ApproxNone);

            if (contours.Length == 0)
            {
                throw new InvalidOperationException("未检出黄框，且未分割到「上」视图物体剪影");
            }

            var objectContour = SelectObjectContour(contours, gray.Width, gray.Height);
            var objectBounds = Cv2.BoundingRect(objectContour);
            if (objectBounds.Width < 30 || objectBounds.Height < 40)
            {
                throw new InvalidOperationException(
                    $"「上」视图物体外接框过小：{objectBounds.Width}x{objectBounds.Height}");
            }

            using var objectMask = new Mat(mask.Size(), MatType.CV_8UC1, Scalar.All(0));
            Cv2.DrawContours(objectMask, new[] { objectContour }, 0, Scalar.All(255), thickness: -1);

            var (rightXs, valid) = BuildRightEdgeProfile(objectMask, objectBounds);
            var searchEnd = Math.Max(
                MinPointSeparationPx + 2,
                (int)(objectBounds.Height * VerticalSearchHeightRatio));
            searchEnd = Math.Min(searchEnd, objectBounds.Height - 1);

            var (segmentStart, segmentEnd, wallX) = FindVerticalRightSegment(rightXs, valid, searchEnd);
            if (segmentStart < 0 || segmentEnd - segmentStart < 24)
            {
                throw new InvalidOperationException(
                    $"未检出黄框，且右侧竖直外缘过短：start={segmentStart}, end={segmentEnd}, wallX={wallX}");
            }

            var segLen = segmentEnd - segmentStart;
            var topIdx = segmentStart + (int)Math.Round(segLen * 0.12);
            var bottomIdx = segmentStart + (int)Math.Round(segLen * 0.88);
            topIdx = Math.Clamp(topIdx, segmentStart, segmentEnd);
            bottomIdx = Math.Clamp(bottomIdx, segmentStart, segmentEnd);
            if (bottomIdx <= topIdx)
            {
                throw new InvalidOperationException("向量上下点顺序无效（下点未低于上点）");
            }

            var topRaw = new Point(rightXs[topIdx], objectBounds.Y + topIdx);
            var bottomRaw = new Point(rightXs[bottomIdx], objectBounds.Y + bottomIdx);
            var top = InsetLeft(topRaw, EdgeInsetPixels, objectBounds.X);
            var bottom = InsetLeft(bottomRaw, EdgeInsetPixels, objectBounds.X);
            if (bottom.Y <= top.Y)
            {
                (top, bottom) = (bottom, top);
            }

            if (!string.IsNullOrWhiteSpace(debugDir))
            {
                Directory.CreateDirectory(debugDir);
                var stem = Path.GetFileNameWithoutExtension(imagePath);
                SaveSilhouetteDebugOverlay(
                    src,
                    objectBounds,
                    segmentStart,
                    segmentEnd,
                    rightXs,
                    top,
                    bottom,
                    Path.Combine(debugDir, $"{stem}_vector_points.png"));
                Cv2.ImWrite(Path.Combine(debugDir, $"{stem}_vector_mask.png"), objectMask);
            }

            return new DetectResult
            {
                Points = new (int X, int Y)[] { (top.X, top.Y), (bottom.X, bottom.Y) },
                TopPoint = top,
                BottomPoint = bottom,
                ObjectBounds = objectBounds,
                VerticalSegmentTopY = objectBounds.Y + segmentStart,
                VerticalSegmentBottomY = objectBounds.Y + segmentEnd,
                Mode = "silhouette-fallback",
                Summary =
                    $"mode=silhouette-fallback, bounds=({objectBounds.X},{objectBounds.Y},{objectBounds.Width}x{objectBounds.Height}), " +
                    $"segY=[{objectBounds.Y + segmentStart}..{objectBounds.Y + segmentEnd}], " +
                    $"Top=({top.X},{top.Y}), Bottom=({bottom.X},{bottom.Y})"
            };
        }

        private static Mat BuildSilhouetteMask(Mat gray)
        {
            var mask = new Mat();
            var otsuThr = Cv2.Threshold(gray, mask, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
            var whiteRatio = Cv2.Mean(mask).Val0 / 255.0;

            if (whiteRatio > 0.72 || otsuThr < 50)
            {
                Cv2.Threshold(gray, mask, 70, 255, ThresholdTypes.Binary);
                whiteRatio = Cv2.Mean(mask).Val0 / 255.0;
            }

            if (whiteRatio > 0.72)
            {
                Cv2.Threshold(gray, mask, 100, 255, ThresholdTypes.Binary);
            }

            ClearBorderFrame(mask, margin: 4);

            using var kClose = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(5, 5));
            using var kOpen = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(3, 3));
            Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kClose);
            Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kOpen);
            return mask;
        }

        private static Point[] SelectObjectContour(Point[][] contours, int imageWidth, int imageHeight)
        {
            var imgArea = Math.Max(1.0, (double)imageWidth * imageHeight);
            var ranked = contours
                .Select(c =>
                {
                    var area = Cv2.ContourArea(c);
                    var bounds = Cv2.BoundingRect(c);
                    var boxFill = bounds.Width * bounds.Height / imgArea;
                    return (Contour: c, Area: area, Bounds: bounds, BoxFill: boxFill);
                })
                .Where(x => x.Area >= 500)
                .OrderByDescending(x => x.Area)
                .ToArray();

            if (ranked.Length == 0)
            {
                throw new InvalidOperationException("未找到有效面积的「上」视图物体轮廓");
            }

            foreach (var item in ranked)
            {
                if (item.BoxFill < 0.96)
                {
                    return item.Contour;
                }
            }

            return ranked[0].Contour;
        }

        private static void ClearBorderFrame(Mat mask, int margin)
        {
            if (mask.Empty() || margin <= 0)
            {
                return;
            }

            var w = mask.Width;
            var h = mask.Height;
            var m = Math.Min(margin, Math.Min(w, h) / 8);
            if (m <= 0)
            {
                return;
            }

            Cv2.Rectangle(mask, new Rect(0, 0, w, m), Scalar.All(0), -1);
            Cv2.Rectangle(mask, new Rect(0, h - m, w, m), Scalar.All(0), -1);
            Cv2.Rectangle(mask, new Rect(0, 0, m, h), Scalar.All(0), -1);
            Cv2.Rectangle(mask, new Rect(w - m, 0, m, h), Scalar.All(0), -1);
        }

        private static (int[] RightXs, bool[] Valid) BuildRightEdgeProfile(Mat mask, Rect bounds)
        {
            var rightXs = new int[bounds.Height];
            var valid = new bool[bounds.Height];
            for (var i = 0; i < bounds.Height; i++)
            {
                var y = bounds.Y + i;
                var right = -1;
                for (var x = bounds.X + bounds.Width - 1; x >= bounds.X; x--)
                {
                    if (mask.At<byte>(y, x) != 0)
                    {
                        right = x;
                        break;
                    }
                }

                if (right >= 0)
                {
                    rightXs[i] = right;
                    valid[i] = true;
                }
            }

            return (rightXs, valid);
        }

        private static (int Start, int End, int WallX) FindVerticalRightSegment(
            int[] rightXs,
            bool[] valid,
            int searchEnd)
        {
            var peakEnd = Math.Max(1, searchEnd / 2);
            var peakIdx = -1;
            var peakX = -1;
            for (var i = 0; i <= peakEnd; i++)
            {
                if (!valid[i])
                {
                    continue;
                }

                if (rightXs[i] > peakX)
                {
                    peakX = rightXs[i];
                    peakIdx = i;
                }
            }

            if (peakIdx < 0 || peakX < 0)
            {
                return (-1, -1, -1);
            }

            var wallIdx = peakIdx;
            for (var i = peakIdx + 1; i <= searchEnd; i++)
            {
                if (!valid[i])
                {
                    break;
                }

                if (rightXs[i] >= peakX - 1)
                {
                    wallIdx = i;
                    peakX = Math.Max(peakX, rightXs[i]);
                    continue;
                }

                break;
            }

            var wallX = rightXs[wallIdx];
            var tol = RightEdgeTolerancePx + 6;
            var start = wallIdx;
            var end = wallIdx;
            for (var i = wallIdx; i <= searchEnd; i++)
            {
                if (!valid[i])
                {
                    break;
                }

                if (Math.Abs(rightXs[i] - wallX) <= tol)
                {
                    end = i;
                    wallX = (wallX * 3 + rightXs[i]) / 4;
                    continue;
                }

                if (i + 1 <= searchEnd && valid[i + 1] && Math.Abs(rightXs[i + 1] - wallX) <= tol)
                {
                    end = i;
                    continue;
                }

                break;
            }

            return (start, end, wallX);
        }

        private static Point InsetLeft(Point edge, int inset, int minX)
        {
            var x = Math.Max(minX, edge.X - inset);
            return new Point(x, edge.Y);
        }

        private static Point2f Lerp(Point2f a, Point2f b, double t)
        {
            return new Point2f(
                (float)(a.X + (b.X - a.X) * t),
                (float)(a.Y + (b.Y - a.Y) * t));
        }

        private static double Distance(Point2f a, Point2f b)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static Point ClampPoint(Point p, int width, int height)
        {
            return new Point(
                Math.Clamp(p.X, 0, Math.Max(0, width - 1)),
                Math.Clamp(p.Y, 0, Math.Max(0, height - 1)));
        }

        private static void SaveYellowDebugOverlay(
            Mat src,
            YellowDebugContext debug,
            Point top,
            Point bottom,
            string path)
        {
            using var canvas = src.Clone();

            if (debug.BoxPoints is { Length: 4 })
            {
                for (var i = 0; i < 4; i++)
                {
                    Cv2.Line(
                        canvas,
                        debug.BoxPoints[i],
                        debug.BoxPoints[(i + 1) % 4],
                        new Scalar(0, 255, 255),
                        1,
                        LineTypes.AntiAlias);
                }
            }

            // 黄框右缘（绿）
            Cv2.Line(canvas, debug.EdgeTop, debug.EdgeBottom, new Scalar(0, 255, 0), 2, LineTypes.AntiAlias);

            // 点击向量（红，与黄框平行）
            Cv2.Line(canvas, top, bottom, new Scalar(0, 0, 255), 2, LineTypes.AntiAlias);
            DrawLabeledPoint(canvas, top, "V-Top", new Scalar(0, 0, 255));
            DrawLabeledPoint(canvas, bottom, "V-Bot", new Scalar(0, 165, 255));

            Cv2.PutText(
                canvas,
                "yellow-rect",
                new Point(8, 22),
                HersheyFonts.HersheySimplex,
                0.6,
                new Scalar(0, 255, 255),
                1,
                LineTypes.AntiAlias);

            Cv2.ImWrite(path, canvas);
        }

        private static void SaveSilhouetteDebugOverlay(
            Mat src,
            Rect bounds,
            int segmentStart,
            int segmentEnd,
            int[] rightXs,
            Point top,
            Point bottom,
            string path)
        {
            using var canvas = src.Clone();
            Cv2.Rectangle(canvas, bounds, new Scalar(80, 80, 80), 1);

            for (var i = segmentStart; i <= segmentEnd; i++)
            {
                if (i < 0 || i >= rightXs.Length || rightXs[i] <= 0)
                {
                    continue;
                }

                Cv2.Circle(canvas, new Point(rightXs[i], bounds.Y + i), 1, new Scalar(255, 180, 0), -1);
            }

            Cv2.Line(canvas, top, bottom, new Scalar(0, 0, 255), 2, LineTypes.AntiAlias);
            DrawLabeledPoint(canvas, top, "V-Top", new Scalar(0, 0, 255));
            DrawLabeledPoint(canvas, bottom, "V-Bot", new Scalar(0, 165, 255));
            Cv2.PutText(
                canvas,
                "silhouette-fallback",
                new Point(8, 22),
                HersheyFonts.HersheySimplex,
                0.55,
                new Scalar(200, 200, 200),
                1,
                LineTypes.AntiAlias);

            Cv2.ImWrite(path, canvas);
        }

        private static void DrawLabeledPoint(Mat canvas, Point p, string label, Scalar color)
        {
            Cv2.Circle(canvas, p, 7, color, 2, LineTypes.AntiAlias);
            Cv2.Circle(canvas, p, 2, color, -1, LineTypes.AntiAlias);
            Cv2.PutText(
                canvas,
                label,
                new Point(p.X + 10, p.Y - 8),
                HersheyFonts.HersheySimplex,
                0.55,
                color,
                1,
                LineTypes.AntiAlias);
        }

        private sealed class YellowDebugContext : IDisposable
        {
            public Mat? YellowMask { get; init; }
            public Point[]? BoxPoints { get; init; }
            public Point EdgeTop { get; init; }
            public Point EdgeBottom { get; init; }

            public void Dispose()
            {
                YellowMask?.Dispose();
            }
        }
    }
}
