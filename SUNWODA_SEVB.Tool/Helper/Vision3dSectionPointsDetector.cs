using System.IO;
using OpenCvSharp;
using Point = OpenCvSharp.Point;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>
    /// 从 VG 第四象限 3D 视口截图估计上盖平面三点 + 原点。
    /// 策略：剪影外轮廓 → 顶面四角(TL/TR/SR/SL) → 四角附近 UV 取三点（避极耳）+ TL 内偏 2px 作原点。
    /// 输出约定与 CTAutoCutImg.GetSectionReferencePoints 一致：[0..2]=上盖三点，[3]=原点。
    /// </summary>
    public static class Vision3dSectionPointsDetector
    {
        /// <summary>原点相对 TL 向顶面中心内偏像素数。</summary>
        public const int OriginInsetPixels = 2;

        /// <summary>角点 UV 内缩（相对四边形），靠近四角而非中心；向内偏约 5%。</summary>
        public const double CornerUvInset = 0.05;

        /// <summary>极耳避让：候选点相对极耳中心的最小 UV 距离。</summary>
        public const double TabAvoidUvGap = 0.22;

        public sealed class DetectResult
        {
            /// <summary>[0..2] 上盖三点；[3] 原点</summary>
            public (int X, int Y)[] Points { get; init; } = Array.Empty<(int X, int Y)>();

            public Point TopLeft { get; init; }
            public Point TopRight { get; init; }
            public Point ShoulderRight { get; init; }
            public Point ShoulderLeft { get; init; }
            public Point2f QuadCenter { get; init; }
            public Rect ObjectBounds { get; init; }
            public string Summary { get; init; } = string.Empty;
        }

        public static DetectResult Detect(string imagePath, string? debugDir = null)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                throw new FileNotFoundException("3D 截图不存在", imagePath);
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
                throw new InvalidOperationException("未分割到物体剪影");
            }

            var objectContour = contours.OrderByDescending(c => Cv2.ContourArea(c)).First();
            var smoothed = SmoothContour(objectContour, window: 5);
            var objectBounds = Cv2.BoundingRect(smoothed);
            if (objectBounds.Width < 40 || objectBounds.Height < 40)
            {
                throw new InvalidOperationException($"物体外接框过小：{objectBounds.Width}x{objectBounds.Height}");
            }

            var (tl, tr, sr, sl) = DetectTopFaceCorners(smoothed, objectBounds, mask);

            // 四角轻微内收，抗外缘噪点（直方图压噪后不必过大）
            const int cornerPullInPx = 4;
            var roughCenter = new Point2f(
                (tl.X + tr.X + sr.X + sl.X) / 4f,
                (tl.Y + tr.Y + sr.Y + sl.Y) / 4f);
            tl = InsetToward(tl, roughCenter, cornerPullInPx);
            tr = InsetToward(tr, roughCenter, cornerPullInPx);
            sr = InsetToward(sr, roughCenter, cornerPullInPx);
            sl = InsetToward(sl, roughCenter, cornerPullInPx);

            var center = new Point2f(
                (tl.X + tr.X + sr.X + sl.X) / 4f,
                (tl.Y + tr.Y + sr.Y + sl.Y) / 4f);

            // 三点落在 TL/TR/SR 角附近（四边形 UV），避开中部极耳
            var tabU = EstimateTabUvU(mask, tl, tr, sr, sl);
            var (u0, v0) = (CornerUvInset, CornerUvInset);                 // 近 TL
            var (u1, v1) = (1.0 - CornerUvInset, CornerUvInset);           // 近 TR
            var (u2, v2) = (1.0 - CornerUvInset, 1.0 - CornerUvInset);     // 近 SR

            if (!double.IsNaN(tabU))
            {
                // 极耳明显偏右：近 TR 的点再往左、往下，避免落在极耳立面
                if (tabU > 0.68)
                {
                    u1 = Math.Min(u1, Math.Max(0.55, tabU - TabAvoidUvGap));
                    v1 = Math.Max(v1, CornerUvInset + 0.14);
                }
                else if (tabU < 0.30)
                {
                    u0 = Math.Max(u0, Math.Min(0.45, tabU + TabAvoidUvGap));
                    v0 = Math.Max(v0, CornerUvInset + 0.14);
                }
                else
                {
                    v0 = Math.Max(v0, CornerUvInset + 0.10);
                    v1 = Math.Max(v1, CornerUvInset + 0.10);
                }
            }

            var p0 = PointOnQuad(tl, tr, sr, sl, u0, v0);
            var p1 = PointOnQuad(tl, tr, sr, sl, u1, v1);
            var p2 = PointOnQuad(tl, tr, sr, sl, u2, v2);
            var origin = InsetToward(tl, center, OriginInsetPixels);

            var points = new (int X, int Y)[]
            {
                (p0.X, p0.Y),
                (p1.X, p1.Y),
                (p2.X, p2.Y),
                (origin.X, origin.Y),
            };

            ValidateNonCollinear(points[0], points[1], points[2]);

            if (!string.IsNullOrWhiteSpace(debugDir))
            {
                Directory.CreateDirectory(debugDir);
                var stem = Path.GetFileNameWithoutExtension(imagePath);
                SaveDebugOverlay(
                    src,
                    mask,
                    objectBounds,
                    tl, tr, sr, sl,
                    center,
                    points,
                    Path.Combine(debugDir, $"{stem}_points.png"));
                Cv2.ImWrite(Path.Combine(debugDir, $"{stem}_mask.png"), mask);
            }

            return new DetectResult
            {
                Points = points,
                TopLeft = tl,
                TopRight = tr,
                ShoulderRight = sr,
                ShoulderLeft = sl,
                QuadCenter = center,
                ObjectBounds = objectBounds,
                Summary =
                    $"TL={Fmt(tl)}, TR={Fmt(tr)}, SR={Fmt(sr)}, SL={Fmt(sl)}, " +
                    $"tabU={(double.IsNaN(tabU) ? "n/a" : tabU.ToString("F2"))}, " +
                    $"UV=({u0:F2},{v0:F2})/({u1:F2},{v1:F2})/({u2:F2},{v2:F2}), " +
                    $"P0={Fmt(points[0])}, P1={Fmt(points[1])}, P2={Fmt(points[2])}, O={Fmt(points[3])}"
            };
        }

        private static string Fmt(Point p) => $"({p.X},{p.Y})";
        private static string Fmt((int X, int Y) p) => $"({p.X},{p.Y})";

        private static Mat BuildSilhouetteMask(Mat gray)
        {
            var mask = new Mat();
            Cv2.Threshold(gray, mask, 12, 255, ThresholdTypes.Binary);
            using var kClose = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(5, 5));
            using var kOpen = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(3, 3));
            Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kClose);
            Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kOpen);
            return mask;
        }

        private static Point[] SmoothContour(Point[] contour, int window)
        {
            if (contour.Length < window * 2)
            {
                return contour;
            }

            var n = contour.Length;
            var result = new Point[n];
            var half = window / 2;
            for (var i = 0; i < n; i++)
            {
                double sx = 0, sy = 0;
                for (var k = -half; k <= half; k++)
                {
                    var p = contour[(i + k + n) % n];
                    sx += p.X;
                    sy += p.Y;
                }

                var count = half * 2 + 1;
                result[i] = new Point((int)Math.Round(sx / count), (int)Math.Round(sy / count));
            }

            return result;
        }

        /// <summary>
        /// 剪影顶面四角：
        /// 1) 上半部行宽最大处 ≈ 肩线 → SL/SR（左右内收抗噪）；
        /// 2) 自肩线向上：宽度不足 / 填充率低 / 左右同时大幅内收（进入中部焊件）则截断 → TL/TR。
        /// </summary>
        private static (Point Tl, Point Tr, Point Sr, Point Sl) DetectTopFaceCorners(
            Point[] contour,
            Rect objectBounds,
            Mat mask)
        {
            var heights = objectBounds.Height;
            var widths = new int[heights];
            var leftXs = new int[heights];
            var rightXs = new int[heights];
            var whiteCounts = new int[heights];
            var spanWidths = new int[heights]; // trim 前外跨度，用于填充率

            for (var i = 0; i < heights; i++)
            {
                var y = objectBounds.Y + i;
                var left = -1;
                var right = -1;
                var white = 0;
                for (var x = objectBounds.X; x < objectBounds.X + objectBounds.Width; x++)
                {
                    if (mask.At<byte>(y, x) == 0)
                    {
                        continue;
                    }

                    white++;
                    if (left < 0)
                    {
                        left = x;
                    }

                    right = x;
                }

                if (left < 0)
                {
                    widths[i] = 0;
                    spanWidths[i] = 0;
                    whiteCounts[i] = 0;
                    leftXs[i] = objectBounds.X;
                    rightXs[i] = objectBounds.X;
                }
                else
                {
                    var span = right - left + 1;
                    spanWidths[i] = span;
                    whiteCounts[i] = white;
                    // 左右各内收，削弱外轮廓毛刺对肩角/顶角的拉动
                    var trim = Math.Clamp(span / 70, 2, 8);
                    var l2 = Math.Min(right, left + trim);
                    var r2 = Math.Max(l2, right - trim);
                    leftXs[i] = l2;
                    rightXs[i] = r2;
                    widths[i] = r2 - l2 + 1;
                }
            }

            var searchEnd = Math.Max(1, (int)(heights * 0.42));
            var maxW = 0;
            var shoulderIdx = 0;
            for (var i = 0; i < searchEnd; i++)
            {
                if (widths[i] > maxW)
                {
                    maxW = widths[i];
                    shoulderIdx = i;
                }
            }

            if (maxW < 20)
            {
                throw new InvalidOperationException("上半部行宽过小，无法定位肩线");
            }

            var shoulderTop = shoulderIdx;
            for (var i = shoulderIdx; i >= 0; i--)
            {
                if (widths[i] >= maxW * 0.96)
                {
                    shoulderTop = i;
                }
                else
                {
                    break;
                }
            }

            var sl = new Point(leftXs[shoulderTop], objectBounds.Y + shoulderTop);
            var sr = new Point(rightXs[shoulderTop], objectBounds.Y + shoulderTop);

            // 顶边宽度下限提高，避免爬进明显更窄的中部焊件顶
            var minTopWidth = (int)(maxW * 0.58);
            const double minFillRatio = 0.72;
            var inwardJumpPx = Math.Max(10, maxW / 22);
            var topIdx = -1;

            for (var i = shoulderTop; i >= 0; i--)
            {
                if (widths[i] < minTopWidth)
                {
                    break;
                }

                var fill = spanWidths[i] <= 0 ? 0.0 : whiteCounts[i] / (double)spanWidths[i];
                if (fill < minFillRatio)
                {
                    break;
                }

                if (i < shoulderTop)
                {
                    var leftInward = leftXs[i] - leftXs[i + 1];
                    var rightInward = rightXs[i + 1] - rightXs[i];
                    var widthDrop = widths[i + 1] - widths[i];
                    // 两侧同时内收，或宽度突然掉一截 → 进入中部凸起/焊件
                    if ((leftInward > inwardJumpPx && rightInward > inwardJumpPx)
                        || widthDrop > inwardJumpPx * 2)
                    {
                        break;
                    }
                }

                topIdx = i;
            }

            if (topIdx < 0)
            {
                for (var i = shoulderTop; i >= 0; i--)
                {
                    if (widths[i] >= minTopWidth)
                    {
                        topIdx = i;
                        break;
                    }
                }
            }

            if (topIdx < 0)
            {
                topIdx = Math.Max(0, shoulderTop - Math.Max(8, heights / 20));
            }

            var band = Math.Max(1, heights / 90);
            var band1 = Math.Min(shoulderTop, topIdx + band);
            var tlX = leftXs[topIdx];
            var trX = rightXs[topIdx];
            var topY = objectBounds.Y + topIdx;
            for (var i = topIdx; i <= band1; i++)
            {
                if (widths[i] < minTopWidth)
                {
                    break;
                }

                if (leftXs[i] < tlX)
                {
                    tlX = leftXs[i];
                }

                if (rightXs[i] > trX)
                {
                    trX = rightXs[i];
                }
            }

            var yTol = Math.Max(4, heights / 50);
            var tl = SnapCornerOnBand(contour, topY, yTol, preferLeft: true, approxX: tlX);
            var tr = SnapCornerOnBand(contour, topY, yTol, preferLeft: false, approxX: trX);
            sl = SnapCornerOnBand(contour, sl.Y, yTol, preferLeft: true, approxX: sl.X);
            sr = SnapCornerOnBand(contour, sr.Y, yTol, preferLeft: false, approxX: sr.X);

            if (tl.X > tr.X)
            {
                (tl, tr) = (tr, tl);
            }

            if (sl.X > sr.X)
            {
                (sl, sr) = (sr, sl);
            }

            var minShoulderDrop = Math.Max(10, heights / 30);
            if (sl.Y < tl.Y + minShoulderDrop || sr.Y < tr.Y + minShoulderDrop)
            {
                sl = SnapCornerOnBand(
                    contour,
                    objectBounds.Y + shoulderTop,
                    yTol,
                    preferLeft: true,
                    approxX: leftXs[shoulderTop]);
                sr = SnapCornerOnBand(
                    contour,
                    objectBounds.Y + shoulderTop,
                    yTol,
                    preferLeft: false,
                    approxX: rightXs[shoulderTop]);
            }

            // TL/TR 若相对肩线过度内收，说明仍贴在中部结构上，沿侧边回退纠正
            var plateHalf = Math.Max(20, (sr.X - sl.X) / 2.0);
            if (tl.X > sl.X + plateHalf * 0.55)
            {
                var correctedTopY = (tl.Y + sl.Y) / 2;
                tl = SnapCornerOnBand(
                    contour,
                    correctedTopY,
                    yTol * 2,
                    preferLeft: true,
                    approxX: (sl.X * 2 + tl.X) / 3);
            }

            if (tr.X < sr.X - plateHalf * 0.55)
            {
                var correctedTopY = (tr.Y + sr.Y) / 2;
                tr = SnapCornerOnBand(
                    contour,
                    correctedTopY,
                    yTol * 2,
                    preferLeft: false,
                    approxX: (sr.X * 2 + tr.X) / 3);
            }

            return (tl, tr, sr, sl);
        }

        /// <summary>
        /// 在指定 Y 带内吸附角点：左侧偏最小 X，右侧偏最大 X。
        /// </summary>
        private static Point SnapCornerOnBand(
            Point[] contour,
            int centerY,
            int yTol,
            bool preferLeft,
            int approxX)
        {
            Point? best = null;
            var bestScore = double.MaxValue;
            foreach (var p in contour)
            {
                if (Math.Abs(p.Y - centerY) > yTol)
                {
                    continue;
                }

                var sideTerm = preferLeft ? p.X : -p.X;
                var score = sideTerm + Math.Abs(p.X - approxX) * 0.15 + Math.Abs(p.Y - centerY) * 0.5;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }

            if (best != null)
            {
                return best.Value;
            }

            return SnapToContour(contour, new Point(approxX, centerY));
        }

        private static Point SnapToContour(Point[] contour, Point approx)
        {
            var best = contour[0];
            var bestD = long.MaxValue;
            foreach (var p in contour)
            {
                long dx = p.X - approx.X;
                long dy = p.Y - approx.Y;
                var d = dx * dx + dy * dy;
                if (d < bestD)
                {
                    bestD = d;
                    best = p;
                }
            }

            return best;
        }

        /// <summary>
        /// 四边形双线性插值：u=0 左 / 1 右，v=0 上(TL-TR) / 1 下(SL-SR)。
        /// </summary>
        private static Point PointOnQuad(Point tl, Point tr, Point sr, Point sl, double u, double v)
        {
            u = Math.Clamp(u, 0, 1);
            v = Math.Clamp(v, 0, 1);
            var top = Lerp(tl, tr, u);
            var bot = Lerp(sl, sr, u);
            return Lerp(top, bot, v);
        }

        private static Point Lerp(Point a, Point b, double t)
        {
            return new Point(
                (int)Math.Round(a.X + (b.X - a.X) * t),
                (int)Math.Round(a.Y + (b.Y - a.Y) * t));
        }

        /// <summary>
        /// 估计极耳在顶板水平方向的 UV-u（0~1）。无显著凸起时返回 NaN。
        /// 依据：顶边 TL-TR 上方（图像更小 Y）剪影仍存在的列，取凸起最高处的 X。
        /// </summary>
        private static double EstimateTabUvU(Mat mask, Point tl, Point tr, Point sr, Point sl)
        {
            var plateLeft = Math.Min(tl.X, sl.X);
            var plateRight = Math.Max(tr.X, sr.X);
            var plateW = plateRight - plateLeft;
            if (plateW < 20)
            {
                return double.NaN;
            }

            var topY = Math.Min(tl.Y, tr.Y);
            var searchTop = Math.Max(0, topY - Math.Max(40, plateW / 3));
            var bestX = -1;
            var bestRise = 0;

            for (var x = plateLeft; x <= plateRight; x++)
            {
                var rise = 0;
                for (var y = topY - 1; y >= searchTop; y--)
                {
                    if (mask.At<byte>(y, x) == 0)
                    {
                        break;
                    }

                    rise = topY - y;
                }

                if (rise > bestRise)
                {
                    bestRise = rise;
                    bestX = x;
                }
            }

            // 凸起太矮则认为无极耳干扰
            if (bestX < 0 || bestRise < Math.Max(12, plateW / 25))
            {
                return double.NaN;
            }

            return (bestX - plateLeft) / (double)plateW;
        }

        private static Point InsetToward(Point corner, Point2f center, int insetPx)
        {
            var dx = center.X - corner.X;
            var dy = center.Y - corner.Y;
            var len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-3 || insetPx <= 0)
            {
                return corner;
            }

            var t = insetPx / len;
            return new Point(
                (int)Math.Round(corner.X + dx * t),
                (int)Math.Round(corner.Y + dy * t));
        }

        private static void ValidateNonCollinear((int X, int Y) a, (int X, int Y) b, (int X, int Y) c)
        {
            var area2 = Math.Abs(
                (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X));
            if (area2 < 80)
            {
                throw new InvalidOperationException($"上盖三点近似共线（area2={area2}），无法定平面");
            }
        }

        private static void SaveDebugOverlay(
            Mat src,
            Mat mask,
            Rect objectBounds,
            Point tl,
            Point tr,
            Point sr,
            Point sl,
            Point2f center,
            (int X, int Y)[] points,
            string path)
        {
            using var canvas = src.Clone();
            Cv2.Rectangle(canvas, objectBounds, new Scalar(60, 60, 60), 1);

            using var maskColor = new Mat();
            Cv2.CvtColor(mask, maskColor, ColorConversionCodes.GRAY2BGR);
            Cv2.AddWeighted(canvas, 0.82, maskColor, 0.18, 0, canvas);

            var quad = new[] { tl, tr, sr, sl };
            Cv2.Polylines(canvas, new[] { quad }, isClosed: true, color: new Scalar(0, 255, 255), thickness: 2);

            void MarkCorner(Point p, string label, Scalar color)
            {
                Cv2.Circle(canvas, p, 6, color, 2);
                Cv2.PutText(canvas, label, new Point(p.X + 8, p.Y - 6),
                    HersheyFonts.HersheySimplex, 0.5, color, 1);
            }

            MarkCorner(tl, "TL", new Scalar(0, 255, 255));
            MarkCorner(tr, "TR", new Scalar(0, 255, 255));
            MarkCorner(sr, "SR", new Scalar(0, 255, 255));
            MarkCorner(sl, "SL", new Scalar(0, 255, 255));
            Cv2.Circle(canvas, new Point((int)center.X, (int)center.Y), 3, new Scalar(255, 255, 0), -1);

            var colors = new[]
            {
                new Scalar(0, 0, 255),
                new Scalar(255, 100, 0),
                new Scalar(0, 200, 0),
                new Scalar(0, 165, 255),
            };
            var labels = new[] { "P1", "P2", "P3", "O" };
            for (var i = 0; i < points.Length; i++)
            {
                var p = new Point(points[i].X, points[i].Y);
                Cv2.Circle(canvas, p, 8, colors[i], 2);
                Cv2.Circle(canvas, p, 2, colors[i], -1);
                Cv2.PutText(canvas, labels[i], new Point(p.X + 10, p.Y - 8),
                    HersheyFonts.HersheySimplex, 0.55, colors[i], 2);
            }

            Cv2.ImWrite(path, canvas);
        }
    }
}
