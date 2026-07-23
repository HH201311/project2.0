using System.IO;
using OpenCvSharp;
using Point = OpenCvSharp.Point;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>
    /// 从 VG 第二象限「上」视图截图估计右侧外缘两点（简单 3-2-1 的「2」向量标点）。
    /// 策略：剪影 → 逐行最右缘 → 取近似竖直段 → 从上到下取两点（略向物体内收）。
    /// 输出约定：[0]=上点，[1]=下点；点击顺序必须从上到下。
    /// </summary>
    public static class VisionTopViewVectorPointsDetector
    {
        /// <summary>相对右缘向物体内收像素（避免点在背景上）。</summary>
        public const int EdgeInsetPixels = 3;

        /// <summary>竖直段判定：行右缘允许比最大右缘偏左的容差。</summary>
        public const int RightEdgeTolerancePx = 4;

        /// <summary>两点最小竖直间距（像素）。</summary>
        public const int MinPointSeparationPx = 24;

        /// <summary>仅在物体上半部（含肩线以下一段）取向量段，对应人工红线范围。</summary>
        public const double VerticalSearchHeightRatio = 0.55;

        /// <summary>上点在竖直段上的相对位置（0=段顶，1=段底）。</summary>
        public const double TopPointFraction = 0.12;

        /// <summary>下点在竖直段上的相对位置。</summary>
        public const double BottomPointFraction = 0.88;

        public sealed class DetectResult
        {
            /// <summary>[0] 上点；[1] 下点</summary>
            public (int X, int Y)[] Points { get; init; } = Array.Empty<(int X, int Y)>();

            public Point TopPoint { get; init; }
            public Point BottomPoint { get; init; }
            public Rect ObjectBounds { get; init; }
            public int VerticalSegmentTopY { get; init; }
            public int VerticalSegmentBottomY { get; init; }
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
                throw new InvalidOperationException("未分割到「上」视图物体剪影");
            }

            var objectContour = SelectObjectContour(contours, gray.Width, gray.Height);
            var objectBounds = Cv2.BoundingRect(objectContour);
            if (objectBounds.Width < 30 || objectBounds.Height < 40)
            {
                throw new InvalidOperationException(
                    $"「上」视图物体外接框过小：{objectBounds.Width}x{objectBounds.Height}");
            }

            // 仅用选中轮廓的填充 mask 取右缘，避免 ROI 内其它亮斑干扰
            using var objectMask = new Mat(mask.Size(), MatType.CV_8UC1, Scalar.All(0));
            Cv2.DrawContours(objectMask, new[] { objectContour }, 0, Scalar.All(255), thickness: -1);

            var (rightXs, valid) = BuildRightEdgeProfile(objectMask, objectBounds);
            var searchEnd = Math.Max(
                MinPointSeparationPx + 2,
                (int)(objectBounds.Height * VerticalSearchHeightRatio));
            searchEnd = Math.Min(searchEnd, objectBounds.Height - 1);

            var (segmentStart, segmentEnd, wallX) = FindVerticalRightSegment(
                rightXs,
                valid,
                searchEnd);

            if (segmentStart < 0 || segmentEnd - segmentStart < MinPointSeparationPx)
            {
                throw new InvalidOperationException(
                    $"右侧竖直外缘过短：start={segmentStart}, end={segmentEnd}, wallX={wallX}");
            }

            var maxRight = wallX;
            for (var i = 0; i < rightXs.Length; i++)
            {
                if (valid[i] && rightXs[i] > maxRight)
                {
                    maxRight = rightXs[i];
                }
            }

            var segLen = segmentEnd - segmentStart;
            var topIdx = segmentStart + (int)Math.Round(segLen * TopPointFraction);
            var bottomIdx = segmentStart + (int)Math.Round(segLen * BottomPointFraction);
            if (bottomIdx - topIdx < MinPointSeparationPx)
            {
                topIdx = segmentStart;
                bottomIdx = Math.Min(segmentEnd, topIdx + MinPointSeparationPx);
                if (bottomIdx > segmentEnd)
                {
                    bottomIdx = segmentEnd;
                    topIdx = Math.Max(segmentStart, bottomIdx - MinPointSeparationPx);
                }
            }

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

            // 再次保证从上到下
            if (bottom.Y <= top.Y)
            {
                (top, bottom) = (bottom, top);
            }

            var points = new (int X, int Y)[]
            {
                (top.X, top.Y),
                (bottom.X, bottom.Y),
            };

            if (!string.IsNullOrWhiteSpace(debugDir))
            {
                Directory.CreateDirectory(debugDir);
                var stem = Path.GetFileNameWithoutExtension(imagePath);
                SaveDebugOverlay(
                    src,
                    objectMask,
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
                Points = points,
                TopPoint = top,
                BottomPoint = bottom,
                ObjectBounds = objectBounds,
                VerticalSegmentTopY = objectBounds.Y + segmentStart,
                VerticalSegmentBottomY = objectBounds.Y + segmentEnd,
                Summary =
                    $"bounds=({objectBounds.X},{objectBounds.Y},{objectBounds.Width}x{objectBounds.Height}), " +
                    $"segY=[{objectBounds.Y + segmentStart}..{objectBounds.Y + segmentEnd}], " +
                    $"maxRight={maxRight}, " +
                    $"Top=({top.X},{top.Y}), Bottom=({bottom.X},{bottom.Y})"
            };
        }

        private static Mat BuildSilhouetteMask(Mat gray)
        {
            var mask = new Mat();
            // 「上」视图背景多为深灰；Otsu 通常可用，失败时抬高固定阈值
            var otsuThr = Cv2.Threshold(gray, mask, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
            var whiteRatio = Cv2.Mean(mask).Val0 / 255.0;

            // 过曝式全白，或阈值过低（把深灰背景也算进去）时改用更高固定阈值
            if (whiteRatio > 0.72 || otsuThr < 50)
            {
                Cv2.Threshold(gray, mask, 70, 255, ThresholdTypes.Binary);
                whiteRatio = Cv2.Mean(mask).Val0 / 255.0;
            }

            if (whiteRatio > 0.72)
            {
                Cv2.Threshold(gray, mask, 100, 255, ThresholdTypes.Binary);
            }

            // 去掉贴边细框/视场圆环，避免外接框膨胀到整图
            ClearBorderFrame(mask, margin: 4);

            using var kClose = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(5, 5));
            using var kOpen = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(3, 3));
            Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kClose);
            Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kOpen);
            return mask;
        }

        /// <summary>
        /// 选取双拱主体：面积优先，但若最大轮廓几乎铺满整图则改选次大「内缩」轮廓。
        /// </summary>
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
                // 紧裁剪样图可能接近满幅，放宽到 0.98；宽 ROI 中排除铺满假轮廓
                if (item.BoxFill < 0.96)
                {
                    return item.Contour;
                }
            }

            // 全部接近满幅（紧裁剪）：取面积最大者
            return ranked[0].Contour;
        }

        /// <summary>清除贴图像边缘的细线框，避免把视场外框当作物体。</summary>
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

        /// <summary>
        /// 找右侧竖直外缘：先在上半部定位肩部墙坐标 wallX，再沿墙向下延伸。
        /// 避免拱顶局部更靠右的尖点把竖直段截得过短。
        /// </summary>
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

            // 肩线：从峰值往下，右缘不再明显右移后视为进入竖直墙
            var wallIdx = peakIdx;
            for (var i = peakIdx + 1; i <= searchEnd; i++)
            {
                if (!valid[i])
                {
                    break;
                }

                // 仍在外扩（弧顶）则继续；开始回缩/持平则定肩
                if (rightXs[i] >= peakX - 1)
                {
                    wallIdx = i;
                    peakX = Math.Max(peakX, rightXs[i]);
                    continue;
                }

                break;
            }

            var wallX = rightXs[wallIdx];
            // 竖直段容差放宽：壳体边缘可能有 1～2px 抖动，且略内收
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
                    // 轻微跟踪墙坐标，适应缓慢内收
                    wallX = (wallX * 3 + rightXs[i]) / 4;
                    continue;
                }

                // 允许偶尔单行毛刺
                if (i + 1 <= searchEnd && valid[i + 1] && Math.Abs(rightXs[i + 1] - wallX) <= tol)
                {
                    end = i;
                    continue;
                }

                break;
            }

            if (end - start < MinPointSeparationPx)
            {
                var (fallbackStart, fallbackEnd) = FindWidestRightSegment(
                    rightXs,
                    valid,
                    searchEnd,
                    peakX,
                    tolerance: tol + 4);
                return (fallbackStart, fallbackEnd, peakX);
            }

            return (start, end, wallX);
        }

        private static (int Start, int End) FindWidestRightSegment(
            int[] rightXs,
            bool[] valid,
            int searchEnd,
            int maxRight,
            int tolerance)
        {
            var bestStart = -1;
            var bestEnd = -1;
            var bestLen = -1;
            var curStart = -1;
            var curEnd = -1;

            for (var i = 0; i <= searchEnd; i++)
            {
                var ok = valid[i] && rightXs[i] >= maxRight - tolerance;
                if (ok)
                {
                    if (curStart < 0)
                    {
                        curStart = i;
                    }

                    curEnd = i;
                }
                else if (curStart >= 0)
                {
                    var len = curEnd - curStart;
                    if (len > bestLen)
                    {
                        bestLen = len;
                        bestStart = curStart;
                        bestEnd = curEnd;
                    }

                    curStart = -1;
                    curEnd = -1;
                }
            }

            if (curStart >= 0)
            {
                var len = curEnd - curStart;
                if (len > bestLen)
                {
                    bestStart = curStart;
                    bestEnd = curEnd;
                }
            }

            return (bestStart, bestEnd);
        }

        private static Point InsetLeft(Point edge, int inset, int minX)
        {
            var x = Math.Max(minX, edge.X - inset);
            return new Point(x, edge.Y);
        }

        private static void SaveDebugOverlay(
            Mat src,
            Mat mask,
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

            // 竖直段右缘轨迹（蓝）
            for (var i = segmentStart; i <= segmentEnd; i++)
            {
                if (i < 0 || i >= rightXs.Length || rightXs[i] <= 0)
                {
                    continue;
                }

                Cv2.Circle(
                    canvas,
                    new Point(rightXs[i], bounds.Y + i),
                    1,
                    new Scalar(255, 180, 0),
                    -1);
            }

            // 向量线（红，对应人工示意）；标签用 ASCII（Hershey 不支持中文）
            Cv2.Line(canvas, top, bottom, new Scalar(0, 0, 255), 2, LineTypes.AntiAlias);
            DrawLabeledPoint(canvas, top, "V-Top", new Scalar(0, 0, 255));
            DrawLabeledPoint(canvas, bottom, "V-Bot", new Scalar(0, 165, 255));

            Cv2.ImWrite(path, canvas);
            _ = mask; // 保留参数对称于 3D 检测器；mask 另存为 *_vector_mask.png
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
    }
}
