using System.IO;
using OpenCvSharp;
using Point = OpenCvSharp.Point;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>
    /// 从 VG 第四象限 3D 视口截图中估计上盖平面三点 + 原点（像素坐标）。
    /// 输入是二维渲染截图（非真点云）；输出约定与 CTAutoCutImg.GetSectionReferencePoints 一致：
    /// [0..2]=上盖三点，[3]=原点（顶面“左上”角）。
    /// </summary>
    public static class Vision3dSectionPointsDetector
    {
        public sealed class DetectResult
        {
            /// <summary>[0..2] 上盖三点；[3] 原点</summary>
            public (int X, int Y)[] Points { get; init; } = Array.Empty<(int X, int Y)>();

            public Rect ObjectBounds { get; init; }
            public Rect PlateBounds { get; init; }
            public Point2f PlateCenter { get; init; }
            public string Summary { get; init; } = string.Empty;
        }

        private readonly struct HubCircle
        {
            public Point2f Center { get; init; }
            public float Radius { get; init; }
        }

        /// <summary>
        /// 检测四点。可选写出调试图（mask / plate / annotated）。
        /// </summary>
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

            using var mask = new Mat();
            Cv2.Threshold(gray, mask, 12, 255, ThresholdTypes.Binary);
            using var kernelClose = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(5, 5));
            using var kernelOpen = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(3, 3));
            Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernelClose);
            Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kernelOpen);

            Cv2.FindContours(
                mask,
                out var contours,
                out _,
                RetrievalModes.External,
                ContourApproximationModes.ApproxSimple);

            if (contours.Length == 0)
            {
                throw new InvalidOperationException("未分割到物体前景，请检查截图是否为黑底 3D 视图");
            }

            var objectContour = contours.OrderByDescending(c => Cv2.ContourArea(c)).First();
            var objectBounds = Cv2.BoundingRect(objectContour);
            if (objectBounds.Width < 40 || objectBounds.Height < 40)
            {
                throw new InvalidOperationException($"物体外接框过小：{objectBounds.Width}x{objectBounds.Height}");
            }

            var widths = BuildRowWidths(mask, objectBounds);
            var (plateTopY, plateBottomY, maxWidth) = FindPlateBand(widths, objectBounds);
            if (plateBottomY <= plateTopY || maxWidth < 20)
            {
                throw new InvalidOperationException("未能从行宽轮廓定位顶板带");
            }

            var plateBounds = new Rect(
                objectBounds.X,
                plateTopY,
                objectBounds.Width,
                Math.Max(8, plateBottomY - plateTopY + 1));

            using var plateMask = ExtractPlateMask(mask, objectContour, plateBounds);
            TightenPlateMaskHorizontally(plateMask, plateBounds);

            var platePts = CollectMaskPoints(plateMask);
            if (platePts.Count < 50)
            {
                throw new InvalidOperationException("顶板区域前景像素过少");
            }

            var plateCenter = ComputeCentroid(platePts);
            var hub = TryDetectHubCircle(gray, plateBounds, plateCenter);
            // 仅当圆靠近质心时才采纳，避免把侧壁弧误当成圆台
            if (hub.HasValue)
            {
                var dx = hub.Value.Center.X - plateCenter.X;
                var dy = hub.Value.Center.Y - plateCenter.Y;
                var maxAllow = Math.Max(20, plateBounds.Width * 0.18);
                if (dx * dx + dy * dy <= maxAllow * maxAllow)
                {
                    plateCenter = new Point2f(hub.Value.Center.X, hub.Value.Center.Y);
                }
                else
                {
                    hub = null;
                }
            }

            var cornerOrigin = FindTopLeftCorner(platePts, plateCenter);
            var planePoints = SampleMaxAreaTriangle(
                plateMask,
                plateCenter,
                plateBounds,
                cornerOrigin,
                hubRadius: hub?.Radius ?? 0);

            var points = new (int X, int Y)[]
            {
                (planePoints[0].X, planePoints[0].Y),
                (planePoints[1].X, planePoints[1].Y),
                (planePoints[2].X, planePoints[2].Y),
                (cornerOrigin.X, cornerOrigin.Y),
            };

            ValidateNonCollinear(points[0], points[1], points[2]);

            if (!string.IsNullOrWhiteSpace(debugDir))
            {
                Directory.CreateDirectory(debugDir);
                var stem = Path.GetFileNameWithoutExtension(imagePath);
                SaveDebugOverlay(src, plateMask, plateBounds, objectBounds, points, plateCenter, hub,
                    Path.Combine(debugDir, $"{stem}_points.png"));
                Cv2.ImWrite(Path.Combine(debugDir, $"{stem}_mask.png"), mask);
                Cv2.ImWrite(Path.Combine(debugDir, $"{stem}_plate.png"), plateMask);
            }

            return new DetectResult
            {
                Points = points,
                ObjectBounds = objectBounds,
                PlateBounds = plateBounds,
                PlateCenter = plateCenter,
                Summary =
                    $"object={objectBounds.Width}x{objectBounds.Height}, " +
                    $"plateY=[{plateTopY},{plateBottomY}], maxW={maxWidth}, hubR={(hub?.Radius ?? 0):F1}, " +
                    $"P0={Fmt(points[0])}, P1={Fmt(points[1])}, P2={Fmt(points[2])}, O={Fmt(points[3])}"
            };
        }

        private static string Fmt((int X, int Y) p) => $"({p.X},{p.Y})";

        private static int[] BuildRowWidths(Mat mask, Rect bounds)
        {
            var widths = new int[bounds.Height];
            for (var y = 0; y < bounds.Height; y++)
            {
                var row = y + bounds.Y;
                var count = 0;
                for (var x = bounds.X; x < bounds.X + bounds.Width; x++)
                {
                    if (mask.At<byte>(row, x) > 0)
                    {
                        count++;
                    }
                }

                widths[y] = count;
            }

            return widths;
        }

        /// <summary>
        /// 定位顶板薄带：上半部首次接近最大宽度处，再向下只扩展有限高度，避免吃进筒身侧壁。
        /// </summary>
        private static (int TopY, int BottomY, int MaxWidth) FindPlateBand(int[] widths, Rect objectBounds)
        {
            var maxWidth = widths.Max();
            if (maxWidth <= 0)
            {
                return (objectBounds.Y, objectBounds.Y, 0);
            }

            var searchEnd = Math.Max(1, (int)(widths.Length * 0.45));
            var threshold = (int)(maxWidth * 0.88);

            var firstWide = -1;
            for (var y = 0; y < searchEnd; y++)
            {
                if (widths[y] >= threshold)
                {
                    firstWide = y;
                    break;
                }
            }

            if (firstWide < 0)
            {
                var localMax = 0;
                firstWide = 0;
                for (var y = 0; y < searchEnd; y++)
                {
                    if (widths[y] > localMax)
                    {
                        localMax = widths[y];
                        firstWide = y;
                    }
                }
            }

            var plateThickness = Math.Clamp((int)(widths.Length * 0.14), 28, 72);
            var upPad = Math.Max(4, plateThickness / 5);
            var bestStart = Math.Max(0, firstWide - upPad);
            var bestEnd = Math.Min(widths.Length - 1, firstWide + plateThickness);

            var narrowLimit = (int)(maxWidth * 0.72);
            for (var y = firstWide + 8; y <= bestEnd; y++)
            {
                if (widths[y] < narrowLimit)
                {
                    bestEnd = Math.Max(firstWide + 8, y - 1);
                    break;
                }
            }

            return (objectBounds.Y + bestStart, objectBounds.Y + bestEnd, maxWidth);
        }

        private static Mat ExtractPlateMask(Mat fullMask, Point[] objectContour, Rect plateBounds)
        {
            var plateMask = new Mat(fullMask.Size(), MatType.CV_8UC1, Scalar.All(0));
            using var roiFull = new Mat(fullMask, plateBounds);
            using var roiPlate = new Mat(plateMask, plateBounds);
            roiFull.CopyTo(roiPlate);

            using var objMask = new Mat(fullMask.Size(), MatType.CV_8UC1, Scalar.All(0));
            Cv2.DrawContours(objMask, new[] { objectContour }, 0, Scalar.All(255), -1);
            Cv2.BitwiseAnd(plateMask, objMask, plateMask);

            using var k = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(3, 3));
            Cv2.MorphologyEx(plateMask, plateMask, MorphTypes.Open, k);
            return plateMask;
        }

        /// <summary>按行去掉左右最外缘，削弱侧壁对顶面采样的污染。</summary>
        private static void TightenPlateMaskHorizontally(Mat plateMask, Rect plateBounds)
        {
            for (var y = plateBounds.Y; y < plateBounds.Y + plateBounds.Height; y++)
            {
                var xs = new List<int>();
                for (var x = plateBounds.X; x < plateBounds.X + plateBounds.Width; x++)
                {
                    if (plateMask.At<byte>(y, x) > 0)
                    {
                        xs.Add(x);
                    }
                }

                if (xs.Count < 10)
                {
                    continue;
                }

                var trim = Math.Max(2, xs.Count / 12);
                var keepL = xs[trim];
                var keepR = xs[xs.Count - 1 - trim];
                for (var x = plateBounds.X; x < plateBounds.X + plateBounds.Width; x++)
                {
                    if (x < keepL || x > keepR)
                    {
                        plateMask.Set(y, x, (byte)0);
                    }
                }
            }
        }

        private static HubCircle? TryDetectHubCircle(Mat gray, Rect plateBounds, Point2f fallbackCenter)
        {
            using var roi = new Mat(gray, plateBounds);
            using var blur = new Mat();
            Cv2.GaussianBlur(roi, blur, new OpenCvSharp.Size(5, 5), 1.2);
            var minR = Math.Max(8, plateBounds.Width / 18);
            var maxR = Math.Max(minR + 4, plateBounds.Width / 5);
            var circles = Cv2.HoughCircles(
                blur,
                HoughModes.Gradient,
                dp: 1.2,
                minDist: plateBounds.Width / 3.0,
                param1: 120,
                param2: 28,
                minRadius: minR,
                maxRadius: maxR);

            if (circles == null || circles.Length == 0)
            {
                return null;
            }

            CircleSegment best = circles[0];
            var bestDist = double.MaxValue;
            foreach (var c in circles)
            {
                var cx = c.Center.X + plateBounds.X;
                var cy = c.Center.Y + plateBounds.Y;
                var dx = cx - fallbackCenter.X;
                var dy = cy - fallbackCenter.Y;
                var d = dx * dx + dy * dy;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = c;
                }
            }

            return new HubCircle
            {
                Center = new Point2f(best.Center.X + plateBounds.X, best.Center.Y + plateBounds.Y),
                Radius = best.Radius
            };
        }

        private static List<Point> CollectMaskPoints(Mat mask)
        {
            var pts = new List<Point>(4096);
            for (var y = 0; y < mask.Rows; y++)
            {
                for (var x = 0; x < mask.Cols; x++)
                {
                    if (mask.At<byte>(y, x) > 0)
                    {
                        pts.Add(new Point(x, y));
                    }
                }
            }

            return pts;
        }

        private static Point2f ComputeCentroid(List<Point> pts)
        {
            double sx = 0, sy = 0;
            foreach (var p in pts)
            {
                sx += p.X;
                sy += p.Y;
            }

            return new Point2f((float)(sx / pts.Count), (float)(sy / pts.Count));
        }

        /// <summary>
        /// 顶面“左上”角：PCA 局部坐标中取 u+v 最小点，再向中心内缩。
        /// </summary>
        private static Point FindTopLeftCorner(List<Point> platePts, Point2f center)
        {
            double cxx = 0, cxy = 0, cyy = 0;
            foreach (var p in platePts)
            {
                var dx = p.X - center.X;
                var dy = p.Y - center.Y;
                cxx += dx * dx;
                cxy += dx * dy;
                cyy += dy * dy;
            }

            var n = (double)platePts.Count;
            cxx /= n;
            cxy /= n;
            cyy /= n;

            var trace = cxx + cyy;
            var det = cxx * cyy - cxy * cxy;
            var tmp = Math.Sqrt(Math.Max(0, trace * trace / 4 - det));
            var l1 = trace / 2 + tmp;
            double ux, uy;
            if (Math.Abs(cxy) > 1e-6)
            {
                ux = l1 - cyy;
                uy = cxy;
            }
            else if (cxx >= cyy)
            {
                ux = 1;
                uy = 0;
            }
            else
            {
                ux = 0;
                uy = 1;
            }

            var norm = Math.Sqrt(ux * ux + uy * uy);
            ux /= norm;
            uy /= norm;
            var vx = -uy;
            var vy = ux;

            // 保证 u 大致指向图像右侧，使 -u 为“左”
            if (ux < 0)
            {
                ux = -ux;
                uy = -uy;
                vx = -vx;
                vy = -vy;
            }

            // 保证 v 大致指向图像下方，使 -v 为“上”
            if (vy < 0)
            {
                vx = -vx;
                vy = -vy;
            }

            Point best = platePts[0];
            var bestScore = double.MaxValue;
            foreach (var p in platePts)
            {
                var dx = p.X - center.X;
                var dy = p.Y - center.Y;
                var u = dx * ux + dy * uy;
                var v = dx * vx + dy * vy;
                var score = u + v;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }

            var ox = (int)Math.Round(best.X * 0.86 + center.X * 0.14);
            var oy = (int)Math.Round(best.Y * 0.86 + center.Y * 0.14);
            return new Point(ox, oy);
        }

        /// <summary>
        /// 在顶板“内缩”区域中选面积最大的三角形三点，保证分散且互异。
        /// </summary>
        private static Point[] SampleMaxAreaTriangle(
            Mat plateMask,
            Point2f center,
            Rect plateBounds,
            Point origin,
            float hubRadius)
        {
            var halfW = plateBounds.Width / 2.0;
            var excludeR = hubRadius > 0
                ? Math.Max(hubRadius * 1.08, 10)
                : Math.Max(10, halfW * 0.16);

            // 距离变换：只保留离边缘足够远的内点，避免贴前缘/侧壁
            using var dist = new Mat();
            Cv2.DistanceTransform(plateMask, dist, DistanceTypes.L2, DistanceTransformMasks.Mask3);
            var minDist = Math.Max(3.0, Math.Min(plateBounds.Height, plateBounds.Width) * 0.04);

            var candidates = new List<Point>(512);
            for (var y = plateBounds.Y; y < plateBounds.Y + plateBounds.Height; y += 2)
            {
                for (var x = plateBounds.X; x < plateBounds.X + plateBounds.Width; x += 2)
                {
                    if (!IsValidPlanePoint(plateMask, x, y, center, excludeR, origin))
                    {
                        continue;
                    }

                    if (dist.At<float>(y, x) < minDist)
                    {
                        continue;
                    }

                    candidates.Add(new Point(x, y));
                }
            }

            if (candidates.Count < 3)
            {
                // 放宽距离阈值再采一次
                candidates.Clear();
                for (var y = plateBounds.Y; y < plateBounds.Y + plateBounds.Height; y += 2)
                {
                    for (var x = plateBounds.X; x < plateBounds.X + plateBounds.Width; x += 2)
                    {
                        if (IsValidPlanePoint(plateMask, x, y, center, excludeR * 0.7, origin))
                        {
                            candidates.Add(new Point(x, y));
                        }
                    }
                }
            }

            if (candidates.Count < 3)
            {
                throw new InvalidOperationException("顶板内有效候选点不足 3 个");
            }

            // 先做均匀抽稀，再暴力/贪心找大三角形
            var sampled = Decimate(candidates, 120);
            return FindLargestTriangle(sampled);
        }

        private static List<Point> Decimate(List<Point> pts, int maxKeep)
        {
            if (pts.Count <= maxKeep)
            {
                return pts;
            }

            var step = Math.Max(1, pts.Count / maxKeep);
            var list = new List<Point>(maxKeep);
            for (var i = 0; i < pts.Count && list.Count < maxKeep; i += step)
            {
                list.Add(pts[i]);
            }

            return list;
        }

        private static Point[] FindLargestTriangle(List<Point> pts)
        {
            // 1) 取距质心最远点 A
            double cx = 0, cy = 0;
            foreach (var p in pts)
            {
                cx += p.X;
                cy += p.Y;
            }

            cx /= pts.Count;
            cy /= pts.Count;

            Point a = pts[0];
            var bestD = -1.0;
            foreach (var p in pts)
            {
                var d = (p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy);
                if (d > bestD)
                {
                    bestD = d;
                    a = p;
                }
            }

            // 2) 距 A 最远点 B
            Point b = pts[0];
            bestD = -1.0;
            foreach (var p in pts)
            {
                var d = (p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y);
                if (d > bestD)
                {
                    bestD = d;
                    b = p;
                }
            }

            // 3) 使面积最大的 C
            Point c = pts[0];
            var bestArea = -1;
            foreach (var p in pts)
            {
                var area = Math.Abs((b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X));
                if (area > bestArea)
                {
                    bestArea = area;
                    c = p;
                }
            }

            if (bestArea < 80)
            {
                throw new InvalidOperationException($"上盖三点近似共线（area2={bestArea}），无法定平面");
            }

            // 去重保护
            if ((a.X == b.X && a.Y == b.Y) || (a.X == c.X && a.Y == c.Y) || (b.X == c.X && b.Y == c.Y))
            {
                throw new InvalidOperationException("上盖三点存在重复像素");
            }

            return new[] { a, b, c };
        }

        private static bool IsValidPlanePoint(
            Mat mask,
            int x,
            int y,
            Point2f center,
            double excludeR,
            Point origin)
        {
            if (x < 1 || y < 1 || x >= mask.Cols - 1 || y >= mask.Rows - 1)
            {
                return false;
            }

            if (mask.At<byte>(y, x) == 0)
            {
                return false;
            }

            var dx = x - center.X;
            var dy = y - center.Y;
            if (dx * dx + dy * dy < excludeR * excludeR)
            {
                return false;
            }

            var ox = x - origin.X;
            var oy = y - origin.Y;
            if (ox * ox + oy * oy < 20 * 20)
            {
                return false;
            }

            var fg = 0;
            for (var yy = y - 1; yy <= y + 1; yy++)
            {
                for (var xx = x - 1; xx <= x + 1; xx++)
                {
                    if (mask.At<byte>(yy, xx) > 0)
                    {
                        fg++;
                    }
                }
            }

            return fg >= 7;
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
            Mat plateMask,
            Rect plateBounds,
            Rect objectBounds,
            (int X, int Y)[] points,
            Point2f center,
            HubCircle? hub,
            string path)
        {
            using var canvas = src.Clone();
            Cv2.Rectangle(canvas, objectBounds, new Scalar(80, 80, 80), 1);
            Cv2.Rectangle(canvas, plateBounds, new Scalar(0, 255, 255), 1);

            using var plateColor = new Mat();
            Cv2.CvtColor(plateMask, plateColor, ColorConversionCodes.GRAY2BGR);
            Cv2.AddWeighted(canvas, 0.75, plateColor, 0.25, 0, canvas);

            Cv2.Circle(canvas, new Point((int)center.X, (int)center.Y), 3, new Scalar(255, 255, 0), -1);
            if (hub.HasValue)
            {
                Cv2.Circle(
                    canvas,
                    new Point((int)hub.Value.Center.X, (int)hub.Value.Center.Y),
                    (int)hub.Value.Radius,
                    new Scalar(255, 0, 255),
                    1);
            }

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
                Cv2.PutText(
                    canvas,
                    labels[i],
                    new Point(p.X + 10, p.Y - 8),
                    HersheyFonts.HersheySimplex,
                    0.55,
                    colors[i],
                    2);
            }

            Cv2.ImWrite(path, canvas);
        }
    }
}
