using System.IO;
using OpenCvSharp;
using Point = OpenCvSharp.Point;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>
    /// ä»Ž VG ç¬¬å››è±¡é™ 3D è§†å£æˆªå›¾ä¼°è®¡ä¸Šç›–å¹³é¢ä¸‰ç‚¹ + åŽŸç‚¹ã€‚
    /// é€‚é…ã€Œæ‰¾ç‚¹å‰è§†è§’å‡†å¤‡ã€åŽçš„æ–œä¿¯è§†ï¼šé¡¶é¢ä¸­ç°è¿žé€šåŸŸ + ä¸‹è¾¹ç•Œå³°ã€‚
    /// è¾“å‡ºçº¦å®šï¼š[0]è¿‘å°–ç‚¹å¹³é¢ç‚¹ã€[1]å³è§’ã€[2]è¿‘å·¦è§’(è¿‘è¾¹)ã€[3]åŽŸç‚¹=æœ€ä¸Šæ–¹å°–ç‚¹ã€‚
    /// ï¼ˆä¸Žäººå·¥æ ‡æ³¨ä¸€è‡´ï¼šåŽŸç‚¹åœ¨å°–ç‚¹ï¼Œéžæ—§ç‰ˆè¿‘ TLã€‚ï¼‰
    /// </summary>
    public static class Vision3dSectionPointsDetector
    {
        /// <summary>åŽŸç‚¹ç›¸å¯¹å°–ç‚¹å‘ä¸­å¿ƒå¾®å†…åï¼ˆå‡ ä¹Žè´´å°–ç‚¹ï¼‰ã€‚</summary>
        public const int OriginInsetPixels = 2;

        /// <summary>è¿‘å°–ç‚¹å¹³é¢ç‚¹ï¼šè‡ªå°–ç‚¹å‘ä¸­å¿ƒå†…åã€‚</summary>
        public const int NearTipPlaneInsetPx = 28;

        /// <summary>å³è§’å¹³é¢ç‚¹ï¼šè‡ªå³æžç«¯å‘ä¸­å¿ƒå†…åï¼ˆé¿å¼€å¤–å£³å¼§è¾¹ï¼‰ã€‚</summary>
        public const int RightCornerInsetPx = 36;

        /// <summary>è¿‘å·¦è§’å¹³é¢ç‚¹ï¼šè‡ªä¸‹è¾¹ç•Œå³°å‘ä¸­å¿ƒå†…åã€‚</summary>
        public const int NearLeftCornerInsetPx = 28;

        /// <summary>è§’ç‚¹ UV å†…ç¼©ï¼ˆå›žé€€è·¯å¾„ PointOnQuad ç”¨ï¼‰ã€‚</summary>
        public const double CornerUvInset = 0.04;

        /// <summary>è§’ç‚¹å¤–ç¼˜å‘ä¸­å¿ƒå†æ”¶åƒç´ ï¼ŒæŠ—å™ªç‚¹ã€‚</summary>
        public const int CornerPullInPx = 3;

        /// <summary>é¡¶é¢ä¸­ç°é˜ˆå€¼ï¼ˆæŽ’é™¤æžè€³é«˜äº®ä¸Žä¾§å£è¿‡æš—ï¼‰ã€‚</summary>
        public const int MidGrayLo = 95;
        public const int MidGrayHi = 175;
        public const int BrightExclude = 200;

        public sealed class DetectResult
        {
            /// <summary>[0..2] ä¸Šç›–ä¸‰ç‚¹ï¼›[3] åŽŸç‚¹</summary>
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
                throw new FileNotFoundException("3D æˆªå›¾ä¸å­˜åœ¨", imagePath);
            }

            using var src = Cv2.ImRead(imagePath, ImreadModes.Color);
            if (src.Empty())
            {
                throw new InvalidOperationException($"æ— æ³•è¯»å–å›¾åƒï¼š{imagePath}");
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
                throw new InvalidOperationException("æœªåˆ†å‰²åˆ°ç‰©ä½“å‰ªå½±");
            }

            var objectContour = contours.OrderByDescending(c => Cv2.ContourArea(c)).First();
            var smoothed = SmoothContour(objectContour, window: 7);
            var objectBounds = Cv2.BoundingRect(smoothed);
            if (objectBounds.Width < 40 || objectBounds.Height < 40)
            {
                throw new InvalidOperationException($"ç‰©ä½“å¤–æŽ¥æ¡†è¿‡å°ï¼š{objectBounds.Width}x{objectBounds.Height}");
            }

            var (tl, tr, sr, sl) = DetectTopFaceCornersOblique(smoothed, objectBounds, mask, gray);

            // æ–œä¿¯è§†ï¼šæœ€ä¸Šæ–¹å°–ç‚¹ = å››è§’ä¸­ Y æœ€å°è€…ï¼ˆå¹¶åˆ—å–æ›´é å³ï¼‰
            var tip = PickTopmostTip(tl, tr, sr, sl);

            var roughCenter = new Point2f(
                (tl.X + tr.X + sr.X + sl.X) / 4f,
                (tl.Y + tr.Y + sr.Y + sl.Y) / 4f);
            tl = InsetToward(tl, roughCenter, CornerPullInPx);
            tr = InsetToward(tr, roughCenter, CornerPullInPx);
            sr = InsetToward(sr, roughCenter, CornerPullInPx);
            sl = InsetToward(sl, roughCenter, CornerPullInPx);
            tip = InsetToward(tip, roughCenter, Math.Min(CornerPullInPx, 2));

            var center = new Point2f(
                (tl.X + tr.X + sr.X + sl.X) / 4f,
                (tl.Y + tr.Y + sr.Y + sl.Y) / 4f);

            // ä¸‰ç‚¹ï¼šè¿‘å°–ç‚¹ / å³è§’ / è¿‘å·¦è§’ï¼›åŽŸç‚¹ï¼šæœ€ä¸Šæ–¹å°–ç‚¹ï¼ˆå¯¹é½äººå·¥æ ‡æ³¨ï¼‰
            var p0 = InsetToward(tip, center, NearTipPlaneInsetPx);
            var p1 = InsetToward(sr, center, RightCornerInsetPx);
            // è¿‘å·¦è§’ï¼šæ²¿å›¾åƒå‘ä¸Šå†…æ”¶ï¼ˆè¿‘è¾¹åœ¨ä¸‹ï¼Œå‘é¡¶é¢å†…éƒ¨ï¼‰ï¼Œé¿å…è¢«ä¸­å¿ƒå¸å¼•ååˆ°å³ä¾§
            var p2 = new Point(sl.X + 2, Math.Max(0, sl.Y - NearLeftCornerInsetPx));
            var origin = InsetToward(tip, center, OriginInsetPixels);

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
                    $"oblique tip={Fmt(tip)} TL={Fmt(tl)}, TR={Fmt(tr)}, BR={Fmt(sr)}, BL={Fmt(sl)}, " +
                    $"P1={Fmt(points[0])}, P2={Fmt(points[1])}, P3={Fmt(points[2])}, O={Fmt(points[3])}"
            };
        }

        private static string Fmt(Point p) => $"({p.X},{p.Y})";
        private static string Fmt((int X, int Y) p) => $"({p.X},{p.Y})";

        internal static Point PickTopmostTip(params Point[] pts)
            => pts.OrderBy(p => p.Y).ThenByDescending(p => p.X).First();

        internal static Mat BuildSilhouetteMask(Mat gray)
        {
            var mask = new Mat();
            Cv2.Threshold(gray, mask, 12, 255, ThresholdTypes.Binary);
            using var kClose = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(5, 5));
            using var kOpen = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(3, 3));
            Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kClose);
            Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kOpen);
            return mask;
        }

        internal static Point[] SmoothContour(Point[] contour, int window)
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
        /// æ–œä¿¯è§†é¡¶é¢å››è§’ï¼šä¼˜å…ˆä¸­ç°é¡¶é¢åˆ†å‰²ï¼›å¤±è´¥å† FloodFill / ROI / è¾¹ç¼˜ / ç›´çº¿ / Harrisã€‚
        /// </summary>
        internal static (Point Tl, Point Tr, Point Br, Point Bl) DetectTopFaceCornersOblique(
            Point[] contour,
            Rect objectBounds,
            Mat mask,
            Mat gray)
        {
            var bodyLeft = EstimateMainBodyLeftX(mask, objectBounds);
            var faceRoi = BuildTopFaceSearchRoi(objectBounds, bodyLeft);

            var fromMid = TryDetectTopPlateByMidGray(gray, mask, objectBounds, bodyLeft);
            if (fromMid != null && IsPlausibleTopQuad(fromMid.Value, objectBounds))
            {
                // mid-gray path: corners already on top face
                return fromMid.Value;
            }

            // Keep mid-gray near-miss: often visually correct but barely fails height-span check
            var midNearMiss = fromMid;

            var fromFlood = TryDetectTopPlateByFloodFill(gray, mask, objectBounds, bodyLeft);
            if (fromFlood != null && IsPlausibleTopQuad(fromFlood.Value, objectBounds))
            {
                return SnapQuad(gray, mask, fromFlood.Value);
            }

            var fromRoi = TryDetectTopPlateInRoi(gray, mask, objectBounds, faceRoi);
            if (fromRoi != null && IsPlausibleTopQuad(fromRoi.Value, objectBounds))
            {
                return SnapQuad(gray, mask, fromRoi.Value);
            }

            var fromPoly = TryDetectTopPlateByEdgeContour(gray, mask, objectBounds, bodyLeft);
            if (fromPoly != null && IsPlausibleTopQuad(fromPoly.Value, objectBounds))
            {
                return SnapQuad(gray, mask, fromPoly.Value);
            }

            var fromLines = TryDetectTopPlateByLines(gray, mask, objectBounds, bodyLeft);
            if (fromLines != null)
            {
                var ordered = OrderQuadCorners(new[]
                {
                    fromLines.Value.Tl, fromLines.Value.Tr, fromLines.Value.Br, fromLines.Value.Bl
                });
                if (IsPlausibleTopQuad(ordered, objectBounds))
                {
                    return SnapQuad(gray, mask, ordered);
                }
            }

            var harrisQuad = TryDetectTopPlateByHarris(gray, mask, faceRoi);
            if (harrisQuad != null && IsPlausibleTopQuad(harrisQuad.Value, objectBounds))
            {
                return SnapQuad(gray, mask, harrisQuad.Value);
            }

            if (midNearMiss != null)
            {
                return midNearMiss.Value;
            }

            throw new InvalidOperationException(
                "未能在斜俯视中稳定检出顶面四角，请确认已完成「找点前视角准备」且 3D ROI 截图含清晰上平面");
        }

        /// <summary>
        /// ä¸­ç°é¡¶é¢ï¼šæŽ’é™¤æžè€³é«˜äº®ä¸Žä¸‹éƒ¨ä¾§å£ï¼Œå–è¿žé€šåŸŸï¼›
        /// å°–ç‚¹=minYï¼Œå³è§’=maxXï¼Œè¿‘å·¦è§’=ä¸‹è¾¹ç•Œå³°ï¼ŒTL=min(x+y)ã€‚
        /// </summary>
        internal static (Point Tl, Point Tr, Point Br, Point Bl)? TryDetectTopPlateByMidGray(
            Mat gray,
            Mat mask,
            Rect bounds,
            int bodyLeft)
        {
            using var mid = new Mat();
            Cv2.InRange(gray, new Scalar(MidGrayLo), new Scalar(MidGrayHi), mid);
            Cv2.BitwiseAnd(mid, mask, mid);

            using var bright = new Mat();
            Cv2.Threshold(gray, bright, BrightExclude, 255, ThresholdTypes.Binary);
            Cv2.BitwiseNot(bright, bright);
            Cv2.BitwiseAnd(mid, bright, mid);

            // åˆ‡æŽ‰æžè€³å·¦ä¾§ + ä¸»ä½“è¿‡å·¦ç¼˜
            var cutX = Math.Min(gray.Cols - 1, bodyLeft + Math.Max(16, bounds.Width / 24));
            for (var y = 0; y < gray.Rows; y++)
            {
                for (var x = 0; x < cutX; x++)
                {
                    mid.Set(y, x, (byte)0);
                }
            }

            // åªä¿ç•™ç‰©ä½“ä¸Š 60% é«˜åº¦ï¼Œé¿å…ä¸‹æ²¿è‚©é¢
            var yCut = bounds.Y + (int)(bounds.Height * 0.60);
            for (var y = Math.Max(0, yCut); y < gray.Rows; y++)
            {
                for (var x = 0; x < gray.Cols; x++)
                {
                    mid.Set(y, x, (byte)0);
                }
            }

            using var k = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(5, 5));
            Cv2.MorphologyEx(mid, mid, MorphTypes.Open, k);
            Cv2.MorphologyEx(mid, mid, MorphTypes.Close, k);

            Cv2.FindContours(
                mid,
                out var cs,
                out _,
                RetrievalModes.External,
                ContourApproximationModes.ApproxNone);
            if (cs.Length == 0)
            {
                return null;
            }

            var plate = cs.OrderByDescending(c => Cv2.ContourArea(c)).First();
            var area = Cv2.ContourArea(plate);
            var minArea = bounds.Width * bounds.Height * 0.04;
            var maxArea = bounds.Width * bounds.Height * 0.45;
            if (area < minArea || area > maxArea)
            {
                return null;
            }

            var hull = Cv2.ConvexHull(plate).ToList();
            if (hull.Count < 4)
            {
                return null;
            }

            var tip = hull.OrderBy(p => p.Y).ThenByDescending(p => p.X).First();
            var brCands = hull.Where(p => p.Y > tip.Y + 30).ToList();
            var br = brCands.Count > 0
                ? brCands.OrderByDescending(p => p.X).First()
                : hull.OrderByDescending(p => p.X).First();

            var tl = hull.OrderBy(p => p.X + p.Y).First();
            var blPt = FindBottomBoundaryPeak(mid, plate);
            if (blPt == null)
            {
                var meanY = hull.Average(p => (double)p.Y);
                var meanX = hull.Average(p => (double)p.X);
                blPt = hull.Where(p => p.Y > meanY && p.X < meanX)
                    .OrderByDescending(p => p.Y)
                    .ThenByDescending(p => p.X)
                    .DefaultIfEmpty(hull.OrderByDescending(p => p.Y).First())
                    .First();
            }

            var q = (Tl: tl, Tr: tip, Br: br, Bl: blPt.Value);
            return IsPlausibleTopQuad(q, bounds) ? q : null;
        }

        /// <summary>ä¸­ç°é¡¶é¢æ¯åˆ—æœ€å¤§ Y å½¢æˆä¸‹è¾¹ç•Œï¼Œå–å…¶å³°å€¼ï¼ˆè¿‘è¾¹æœ€ä½Žå¤„ â‰ˆ è¿‘å·¦è§’ï¼‰ã€‚</summary>
        internal static Point? FindBottomBoundaryPeak(Mat mid, Point[] plateContour)
        {
            var plateRect = Cv2.BoundingRect(plateContour);
            if (plateRect.Width < 20 || plateRect.Height < 20)
            {
                return null;
            }

            var bottom = new List<Point>(plateRect.Width);
            for (var x = plateRect.X; x < plateRect.X + plateRect.Width; x++)
            {
                var maxY = -1;
                for (var y = plateRect.Y + plateRect.Height - 1; y >= plateRect.Y; y--)
                {
                    if (mid.At<byte>(y, x) != 0)
                    {
                        maxY = y;
                        break;
                    }
                }

                if (maxY >= 0)
                {
                    bottom.Add(new Point(x, maxY));
                }
            }

            if (bottom.Count < 10)
            {
                return null;
            }

            const int w = 7;
            var sm = new List<Point>(bottom.Count);
            for (var i = 0; i < bottom.Count; i++)
            {
                var i0 = Math.Max(0, i - w);
                var i1 = Math.Min(bottom.Count - 1, i + w);
                double sy = 0;
                for (var j = i0; j <= i1; j++)
                {
                    sy += bottom[j].Y;
                }

                sm.Add(new Point(bottom[i].X, (int)Math.Round(sy / (i1 - i0 + 1))));
            }

            // æŽ’é™¤å·¦ä¾§å£ï¼šåªåœ¨é¡¶é¢ä¸­æ®µæ‰¾ä¸‹è¾¹ç•Œå³°ï¼ˆè¿‘å·¦è§’åœ¨è¿‘è¾¹ä¸­å·¦ï¼Œä¸åœ¨ä¾§å£è„šï¼‰
            var xMin = plateRect.X + (int)(plateRect.Width * 0.18);
            var xMax = plateRect.X + (int)(plateRect.Width * 0.72);
            var band = sm.Where(p => p.X >= xMin && p.X <= xMax).ToList();
            if (band.Count < 5)
            {
                band = sm.Where(p => p.X >= xMin).ToList();
            }

            if (band.Count == 0)
            {
                return null;
            }

            var peakY = band.Max(p => p.Y);
            // è¿‘å·¦è§’ï¼šé«˜å³°å¸¦ä¸Šé è¿‘ã€Œä¸­åå·¦ã€ç›®æ ‡ Xï¼ˆé¿å¼€ä¾§å£ä¸Žå³è§’ï¼‰
            var targetX = xMin + (int)((xMax - xMin) * 0.22);
            return band.Where(p => p.Y >= peakY - 5)
                .OrderBy(p => Math.Abs(p.X - targetX))
                .First();
        }

        internal static (Point Tl, Point Tr, Point Br, Point Bl) SnapQuad(
            Mat gray,
            Mat mask,
            (Point Tl, Point Tr, Point Br, Point Bl) q)
            => (
                SnapToStrongEdge(gray, mask, q.Tl),
                SnapToStrongEdge(gray, mask, q.Tr),
                SnapToStrongEdge(gray, mask, q.Br),
                SnapToStrongEdge(gray, mask, q.Bl));

        internal static bool IsPlausibleTopQuad((Point Tl, Point Tr, Point Br, Point Bl) q, Rect bounds)
        {
            var pts = new[] { q.Tl, q.Tr, q.Br, q.Bl };
            if (pts.Distinct().Count() < 4)
            {
                return false;
            }

            // 四角须落在物体外接框内（允许少量外扩）
            foreach (var p in pts)
            {
                if (p.X < bounds.X - 5 || p.X > bounds.X + bounds.Width + 5
                    || p.Y < bounds.Y - 5 || p.Y > bounds.Y + bounds.Height + 5)
                {
                    return false;
                }
            }

            var area = Math.Abs(Cv2.ContourArea(pts));
            if (area < bounds.Width * bounds.Height * 0.03)
            {
                return false;
            }

            // 顶面高度跨度：斜俯视可略大；原 0.58 过紧（真机 dy 仅超 1～2px 就被拒）
            var minY = pts.Min(p => p.Y);
            var maxY = pts.Max(p => p.Y);
            if (maxY - minY > bounds.Height * 0.66)
            {
                return false;
            }

            if (maxY > bounds.Y + bounds.Height * 0.78)
            {
                return false;
            }

            var topLen = Dist(q.Tl, q.Tr);
            var botLen = Dist(q.Bl, q.Br);
            var leftLen = Dist(q.Tl, q.Bl);
            var rightLen = Dist(q.Tr, q.Br);
            var sideRatio = Math.Min(leftLen, rightLen) / Math.Max(1.0, Math.Max(leftLen, rightLen));
            var tbRatio = Math.Min(topLen, botLen) / Math.Max(1.0, Math.Max(topLen, botLen));
            return sideRatio >= 0.28 && tbRatio >= 0.28 && topLen >= bounds.Width * 0.12;
        }

        /// <summary>
        /// ä»Žä¸Šå¹³é¢ç§å­ç‚¹åŒºåŸŸç”Ÿé•¿ï¼Œå–è¿žé€šåŸŸå¤–è½®å»“é€¼è¿‘å››è¾¹å½¢ã€‚
        /// </summary>
        internal static (Point Tl, Point Tr, Point Br, Point Bl)? TryDetectTopPlateByFloodFill(
            Mat gray,
            Mat mask,
            Rect bounds,
            int bodyLeft)
        {
            var seed = FindTopFaceSeed(gray, mask, bounds, bodyLeft);
            if (seed == null)
            {
                return null;
            }

            using var work = gray.Clone();
            // floodFill éœ€è¦ mask æ¯”å›¾åƒå¤§ 2 åƒç´ 
            using var floodMask = new Mat(gray.Rows + 2, gray.Cols + 2, MatType.CV_8UC1, Scalar.All(0));
            // ç¦æ­¢é•¿åˆ°å‰ªå½±å¤–ï¼Œå¹¶æŒ¡ä½æžè€³å°–ç«¯å·¦ä¾§
            var blockLeft = Math.Max(0, bodyLeft - 2);
            for (var y = 0; y < gray.Rows; y++)
            {
                for (var x = 0; x < gray.Cols; x++)
                {
                    if (mask.At<byte>(y, x) == 0 || x < blockLeft)
                    {
                        floodMask.Set(y + 1, x + 1, (byte)1);
                    }
                }
            }

            var flags = FloodFillFlags.FixedRange | (FloodFillFlags)(4 << 8);
            Cv2.FloodFill(
                work,
                floodMask,
                seed.Value,
                new Scalar(255),
                out var rect,
                new Scalar(22),
                new Scalar(22),
                flags);

            // floodMaskï¼šé¢„å…ˆå±è”½çš„éžç‰©ä½“=1ï¼›æ–°å¡«å……åƒç´ =(flags>>8)=4
            using var regionFull = new Mat();
            Cv2.InRange(floodMask, new Scalar(4), new Scalar(4), regionFull);
            using var regionRoi = new Mat(regionFull, new Rect(1, 1, gray.Cols, gray.Rows));
            Cv2.BitwiseAnd(regionRoi, mask, regionRoi);

            var area = Cv2.CountNonZero(regionRoi);
            var minArea = (int)(bounds.Width * bounds.Height * 0.035);
            var maxArea = (int)(bounds.Width * bounds.Height * 0.40);
            if (area < minArea || area > maxArea)
            {
                return null;
            }

            Cv2.FindContours(
                regionRoi,
                out var cs,
                out _,
                RetrievalModes.External,
                ContourApproximationModes.ApproxNone);
            if (cs.Length == 0)
            {
                return null;
            }

            var plate = cs.OrderByDescending(c => Cv2.ContourArea(c)).First();

            // ä¼˜å…ˆï¼šè½®å»“/å‡¸åŒ…æžç«¯å››ç‚¹ï¼ˆæ¯” ApproxPolyDP=4 / MinAreaRect æ›´ç¨³ï¼‰
            var hull = Cv2.ConvexHull(plate);
            var extremeSource = hull.Length >= 4 ? hull.ToList() : plate.ToList();
            var extremes = PickExtremeCorners(extremeSource);
            var qExt = OrderQuadCorners(extremes);
            if (IsPlausibleTopQuad(qExt, bounds))
            {
                return qExt;
            }

            var peri = Cv2.ArcLength(plate, true);
            foreach (var eps in new[] { 0.02, 0.025, 0.03, 0.035, 0.04, 0.05 })
            {
                var approx = Cv2.ApproxPolyDP(plate, eps * peri, true);
                if (approx.Length == 4 && IsConvexQuad(approx))
                {
                    var q = OrderQuadCorners(approx);
                    if (IsPlausibleTopQuad(q, bounds))
                    {
                        return q;
                    }
                }
            }

            return null;
        }

        private static Point? FindTopFaceSeed(Mat gray, Mat mask, Rect bounds, int bodyLeft)
        {
            var bodyRight = bounds.X + bounds.Width;
            var xs = new[]
            {
                bodyLeft + (int)((bodyRight - bodyLeft) * 0.45),
                bodyLeft + (int)((bodyRight - bodyLeft) * 0.55),
                bodyLeft + (int)((bodyRight - bodyLeft) * 0.62),
            };

            foreach (var x in xs)
            {
                if (x <= 0 || x >= gray.Cols)
                {
                    continue;
                }

                var enteredY = -1;
                for (var y = bounds.Y; y < bounds.Y + (int)(bounds.Height * 0.55); y++)
                {
                    if (mask.At<byte>(y, x) == 0)
                    {
                        if (enteredY >= 0)
                        {
                            break;
                        }

                        continue;
                    }

                    if (enteredY < 0)
                    {
                        enteredY = y;
                    }
                }

                if (enteredY < 0)
                {
                    continue;
                }

                // è¿›å…¥å‰ªå½±åŽå†å‘ä¸‹èµ°ä¸€æ®µï¼Œè½åˆ°ä¸Šå¹³é¢å†…éƒ¨ï¼ˆé¿å¼€è¿œè¾¹äº®æ£±ï¼‰
                var seedY = enteredY + Math.Max(18, bounds.Height / 18);
                if (seedY >= gray.Rows || mask.At<byte>(seedY, x) == 0)
                {
                    seedY = enteredY + 10;
                }

                if (seedY < gray.Rows && mask.At<byte>(seedY, x) != 0)
                {
                    return new Point(x, seedY);
                }
            }

            return null;
        }

        internal static Rect BuildTopFaceSearchRoi(Rect bounds, int bodyLeft)
        {
            var left = Math.Max(bounds.X, bodyLeft + (int)(bounds.Width * 0.04));
            var top = bounds.Y + (int)(bounds.Height * 0.10);
            var right = bounds.X + (int)(bounds.Width * 0.95);
            var bottom = bounds.Y + (int)(bounds.Height * 0.65);
            left = Math.Clamp(left, bounds.X, bounds.X + bounds.Width - 20);
            right = Math.Clamp(right, left + 20, bounds.X + bounds.Width);
            top = Math.Clamp(top, bounds.Y, bounds.Y + bounds.Height - 20);
            bottom = Math.Clamp(bottom, top + 20, bounds.Y + bounds.Height);
            return new Rect(left, top, right - left, bottom - top);
        }

        /// <summary>
        /// åœ¨é¡¶é¢æœç´¢ ROI å†…ï¼šä¸­å€¼æ»¤æ³¢ + Canny + å››è¾¹å½¢é€¼è¿‘ / Harrisã€‚
        /// </summary>
        internal static (Point Tl, Point Tr, Point Br, Point Bl)? TryDetectTopPlateInRoi(
            Mat gray,
            Mat mask,
            Rect bounds,
            Rect faceRoi)
        {
            using var roiGray = new Mat(gray, faceRoi);
            using var roiMask = new Mat(mask, faceRoi);
            using var med = new Mat();
            Cv2.MedianBlur(roiGray, med, 5);

            using var edges = new Mat();
            Cv2.Canny(med, edges, 30, 100);
            Cv2.BitwiseAnd(edges, roiMask, edges);
            using var kDil = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3));
            Cv2.Dilate(edges, edges, kDil);

            Cv2.FindContours(
                edges,
                out var ecs,
                out _,
                RetrievalModes.List,
                ContourApproximationModes.ApproxNone);

            // é¡¶é¢åº”å æœç´¢åŒºç›¸å½“æ¯”ä¾‹ï¼Œé¿å…è´´åˆ°ä¸Šç¼˜å°ç¢Žå—
            var minArea = faceRoi.Width * faceRoi.Height * 0.12;
            var maxArea = faceRoi.Width * faceRoi.Height * 0.92;
            (Point Tl, Point Tr, Point Br, Point Bl)? best = null;
            var bestScore = double.MinValue;

            foreach (var ec in ecs)
            {
                var peri = Cv2.ArcLength(ec, true);
                if (peri < 80)
                {
                    continue;
                }

                foreach (var eps in new[] { 0.02, 0.025, 0.03, 0.035, 0.04, 0.05, 0.06 })
                {
                    var approxLocal = Cv2.ApproxPolyDP(ec, eps * peri, true);
                    if (approxLocal.Length != 4 || !IsConvexQuad(approxLocal))
                    {
                        continue;
                    }

                    var approx = approxLocal.Select(p => new Point(p.X + faceRoi.X, p.Y + faceRoi.Y)).ToArray();
                    var area = Math.Abs(Cv2.ContourArea(approx));
                    if (area < minArea || area > maxArea)
                    {
                        continue;
                    }

                    var widthSpan = approx.Max(p => p.X) - approx.Min(p => p.X);
                    var heightSpan = approx.Max(p => p.Y) - approx.Min(p => p.Y);
                    if (widthSpan < faceRoi.Width * 0.35 || heightSpan < faceRoi.Height * 0.22)
                    {
                        continue;
                    }

                    // å·¦å³è¾¹é•¿ä¸åº”å·®å¤ªå¤šæ•°é‡çº§ï¼ˆæ‹’ç»æ¥”å½¢é€€åŒ–ï¼‰
                    var ordered = OrderQuadCorners(approx);
                    var leftLen = Dist(ordered.Tl, ordered.Bl);
                    var rightLen = Dist(ordered.Tr, ordered.Br);
                    var topLen = Dist(ordered.Tl, ordered.Tr);
                    var botLen = Dist(ordered.Bl, ordered.Br);
                    var sideRatio = Math.Min(leftLen, rightLen) / Math.Max(1.0, Math.Max(leftLen, rightLen));
                    var tbRatio = Math.Min(topLen, botLen) / Math.Max(1.0, Math.Max(topLen, botLen));
                    if (sideRatio < 0.35 || tbRatio < 0.35)
                    {
                        continue;
                    }

                    var para = ParallelogramScore(new[] { ordered.Tl, ordered.Tr, ordered.Br, ordered.Bl });
                    var meanY = (ordered.Tl.Y + ordered.Tr.Y + ordered.Br.Y + ordered.Bl.Y) / 4.0;
                    var heightBonus = 1.0 - (meanY - bounds.Y) / Math.Max(1.0, bounds.Height);
                    var score = area * (0.4 + 0.6 * para) * (0.35 + 0.65 * heightBonus) * sideRatio * tbRatio;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = ordered;
                    }
                }
            }

            return best;
        }

        private static double Dist(Point a, Point b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        internal static (Point Tl, Point Tr, Point Br, Point Bl)? TryDetectTopPlateByHarris(
            Mat gray,
            Mat mask,
            Rect faceRoi)
        {
            using var roiGray = new Mat(gray, faceRoi);
            using var roiMask = new Mat(mask, faceRoi);
            using var filtered = new Mat();
            Cv2.BilateralFilter(roiGray, filtered, 5, 40, 40);

            var corners = Cv2.GoodFeaturesToTrack(
                filtered,
                maxCorners: 20,
                qualityLevel: 0.02,
                minDistance: Math.Max(18, Math.Min(faceRoi.Width, faceRoi.Height) / 10.0),
                mask: roiMask,
                blockSize: 5,
                useHarrisDetector: true,
                k: 0.04);

            if (corners == null || corners.Length < 4)
            {
                return null;
            }

            var pts = corners
                .Select(p => new Point((int)Math.Round(p.X + faceRoi.X), (int)Math.Round(p.Y + faceRoi.Y)))
                .Distinct()
                .ToList();
            if (pts.Count < 4)
            {
                return null;
            }

            var extremes = PickExtremeCorners(pts);
            if (PointsEqual(extremes[0], extremes[1])
                || PointsEqual(extremes[1], extremes[2])
                || PointsEqual(extremes[2], extremes[3])
                || PointsEqual(extremes[0], extremes[2]))
            {
                return null;
            }

            var area = Math.Abs(Cv2.ContourArea(extremes));
            if (area < faceRoi.Width * faceRoi.Height * 0.05)
            {
                return null;
            }

            return OrderQuadCorners(extremes);
        }

        /// <summary>
        /// åŒè¾¹æ»¤æ³¢é™å™ª â†’ Canny â†’ æ‰¾è¿‘ä¼¼å››è¾¹å½¢è½®å»“ï¼ˆé¡¶é¢å¹³è¡Œå››è¾¹å½¢ï¼‰ã€‚
        /// </summary>
        internal static (Point Tl, Point Tr, Point Br, Point Bl)? TryDetectTopPlateByEdgeContour(
            Mat gray,
            Mat mask,
            Rect bounds,
            int bodyLeft)
        {
            using var filtered = new Mat();
            Cv2.BilateralFilter(gray, filtered, d: 7, sigmaColor: 50, sigmaSpace: 50);

            using var edges = new Mat();
            Cv2.Canny(filtered, edges, 50, 130);

            using var inner = new Mat();
            using var kErode = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(3, 3));
            Cv2.Erode(mask, inner, kErode);
            Cv2.BitwiseAnd(edges, inner, edges);

            // åŽ»æŽ‰æžè€³å°–ç«¯ä¸Žåº•éƒ¨
            var xCut = Math.Max(bounds.X, bodyLeft);
            var yBottom = bounds.Y + (int)(bounds.Height * 0.75);
            for (var y = 0; y < edges.Rows; y++)
            {
                for (var x = 0; x < xCut && x < edges.Cols; x++)
                {
                    edges.Set(y, x, (byte)0);
                }

                if (y >= yBottom)
                {
                    for (var x = bounds.X; x < bounds.X + bounds.Width && x < edges.Cols; x++)
                    {
                        edges.Set(y, x, (byte)0);
                    }
                }
            }

            // è¿žæŽ¥æ–­è£‚è¾¹
            using var kDilate = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(3, 3));
            Cv2.Dilate(edges, edges, kDilate);

            Cv2.FindContours(
                edges,
                out var edgeContours,
                out _,
                RetrievalModes.List,
                ContourApproximationModes.ApproxNone);

            if (edgeContours.Length == 0)
            {
                return null;
            }

            var minArea = bounds.Width * bounds.Height * 0.02;
            var maxArea = bounds.Width * bounds.Height * 0.45;
            var yMeanMax = bounds.Y + bounds.Height * 0.62;

            (Point Tl, Point Tr, Point Br, Point Bl)? best = null;
            var bestScore = double.MinValue;

            foreach (var ec in edgeContours)
            {
                var area0 = Cv2.ContourArea(ec);
                if (area0 < minArea * 0.5 || area0 > maxArea * 1.2)
                {
                    continue;
                }

                var peri = Cv2.ArcLength(ec, true);
                if (peri < 40)
                {
                    continue;
                }

                foreach (var epsFactor in new[] { 0.02, 0.025, 0.03, 0.035, 0.04 })
                {
                    var approx = Cv2.ApproxPolyDP(ec, epsFactor * peri, true);
                    if (approx.Length != 4)
                    {
                        continue;
                    }

                    if (!IsConvexQuad(approx))
                    {
                        continue;
                    }

                    var area = Math.Abs(Cv2.ContourArea(approx));
                    if (area < minArea || area > maxArea)
                    {
                        continue;
                    }

                    var meanY = approx.Average(p => p.Y);
                    if (meanY > yMeanMax)
                    {
                        continue;
                    }

                    var widthSpan = approx.Max(p => p.X) - approx.Min(p => p.X);
                    var heightSpan = approx.Max(p => p.Y) - approx.Min(p => p.Y);
                    if (widthSpan < bounds.Width * 0.12 || heightSpan < 15)
                    {
                        continue;
                    }

                    var aspect = widthSpan / (double)Math.Max(1, heightSpan);
                    if (aspect < 0.5 || aspect > 4.5)
                    {
                        continue;
                    }

                    // å››è§’é¡»éƒ½åœ¨ç‰©ä½“å†…
                    if (approx.Any(p =>
                            p.X < 0 || p.Y < 0 || p.X >= mask.Cols || p.Y >= mask.Rows
                            || mask.At<byte>(p.Y, p.X) == 0))
                    {
                        continue;
                    }

                    var para = ParallelogramScore(approx);
                    var heightBonus = 1.0 - (meanY - bounds.Y) / Math.Max(1.0, bounds.Height);
                    var score = area * (0.5 + 0.5 * para) * (0.35 + 0.65 * heightBonus);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = OrderQuadCorners(approx);
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Canny+Houghï¼šä¸¤ç»„å¹³è¡Œçº¿æ±‚äº¤ â†’ é¡¶é¢å¹³è¡Œå››è¾¹å½¢å››è§’ã€‚
        /// </summary>
        internal static (Point Tl, Point Tr, Point Br, Point Bl)? TryDetectTopPlateByLines(
            Mat gray,
            Mat mask,
            Rect bounds,
            int bodyLeft)
        {
            using var edges = new Mat();
            Cv2.Canny(gray, edges, 40, 120);
            using var inner = new Mat();
            using var kErode = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(3, 3));
            Cv2.Erode(mask, inner, kErode);
            Cv2.BitwiseAnd(edges, inner, edges);

            var xCut = Math.Max(bounds.X, bodyLeft - 2);
            var yBottom = bounds.Y + (int)(bounds.Height * 0.78);
            for (var y = bounds.Y; y < bounds.Y + bounds.Height; y++)
            {
                for (var x = bounds.X; x < xCut; x++)
                {
                    edges.Set(y, x, (byte)0);
                }

                if (y < yBottom)
                {
                    continue;
                }

                for (var x = bounds.X; x < bounds.X + bounds.Width; x++)
                {
                    edges.Set(y, x, (byte)0);
                }
            }

            var minLen = Math.Max(28, bounds.Width / 12);
            var segments = Cv2.HoughLinesP(
                edges,
                rho: 1,
                theta: Math.PI / 180.0,
                threshold: Math.Max(25, bounds.Width / 35),
                minLineLength: minLen,
                maxLineGap: Math.Max(8, bounds.Width / 40));
            if (segments == null || segments.Length < 4)
            {
                return null;
            }

            var lines = new List<(double AngleDeg, double Rho, double Len)>();
            foreach (var seg in segments)
            {
                var dx = seg.P2.X - seg.P1.X;
                var dy = seg.P2.Y - seg.P1.Y;
                var len = Math.Sqrt(dx * dx + dy * dy);
                if (len < minLen * 0.85)
                {
                    continue;
                }

                var angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                if (angle < 0)
                {
                    angle += 180.0;
                }

                if (angle >= 180.0)
                {
                    angle -= 180.0;
                }

                var cx = (seg.P1.X + seg.P2.X) / 2.0;
                var cy = (seg.P1.Y + seg.P2.Y) / 2.0;
                var angRad = angle * Math.PI / 180.0;
                var rho = cx * Math.Cos(angRad + Math.PI / 2) + cy * Math.Sin(angRad + Math.PI / 2);
                lines.Add((angle, rho, len));
            }

            if (lines.Count < 4)
            {
                return null;
            }

            const int binSize = 8;
            var bins = new double[180 / binSize];
            foreach (var ln in lines)
            {
                var bi = Math.Min(bins.Length - 1, (int)(ln.AngleDeg / binSize));
                bins[bi] += ln.Len;
            }

            var dirCandidates = new List<(double Angle, double Weight)>();
            for (var i = 0; i < bins.Length; i++)
            {
                if (bins[i] > 0)
                {
                    dirCandidates.Add(((i + 0.5) * binSize, bins[i]));
                }
            }

            dirCandidates = dirCandidates.OrderByDescending(d => d.Weight).ToList();
            if (dirCandidates.Count < 2)
            {
                return null;
            }

            var angA = dirCandidates[0].Angle;
            var angB = double.NaN;
            foreach (var d in dirCandidates.Skip(1))
            {
                var diff = Math.Abs(AngleDiffDeg(angA, d.Angle));
                if (diff is >= 35 and <= 85)
                {
                    angB = d.Angle;
                    break;
                }
            }

            if (double.IsNaN(angB))
            {
                return null;
            }

            var pairA = PickTwoParallelLines(lines, angA, binSize + 4);
            var pairB = PickTwoParallelLines(lines, angB, binSize + 4);
            if (pairA == null || pairB == null)
            {
                return null;
            }

            var corners = new List<Point>();
            foreach (var (la, lb) in new[]
                     {
                         (pairA.Value.L1, pairB.Value.L1),
                         (pairA.Value.L1, pairB.Value.L2),
                         (pairA.Value.L2, pairB.Value.L1),
                         (pairA.Value.L2, pairB.Value.L2),
                     })
            {
                if (!TryIntersectLines(la, lb, out var pt))
                {
                    return null;
                }

                if (pt.X < bounds.X - 10 || pt.X > bounds.X + bounds.Width + 10
                    || pt.Y < bounds.Y - 10 || pt.Y > bounds.Y + bounds.Height + 10)
                {
                    return null;
                }

                corners.Add(pt);
            }

            var area = Math.Abs(Cv2.ContourArea(corners.ToArray()));
            var minArea = bounds.Width * bounds.Height * 0.015;
            var maxArea = bounds.Width * bounds.Height * 0.50;
            if (area < minArea || area > maxArea)
            {
                return null;
            }

            return OrderQuadCorners(corners.ToArray());
        }

        private static double AngleDiffDeg(double a, double b)
        {
            var d = Math.Abs(a - b) % 180.0;
            return d > 90 ? 180 - d : d;
        }

        private static ((double Angle, double Rho) L1, (double Angle, double Rho) L2)? PickTwoParallelLines(
            List<(double AngleDeg, double Rho, double Len)> lines,
            double targetAngle,
            double angleTol)
        {
            var group = lines
                .Where(l => AngleDiffDeg(l.AngleDeg, targetAngle) <= angleTol)
                .OrderByDescending(l => l.Len)
                .ToList();
            if (group.Count < 2)
            {
                return null;
            }

            var ordered = group.OrderBy(l => l.Rho).ToList();
            var minRho = ordered.First().Rho;
            var maxRho = ordered.Last().Rho;
            if (Math.Abs(maxRho - minRho) < 12)
            {
                return null;
            }

            var mid = (minRho + maxRho) / 2;
            var sideLow = group.Where(l => l.Rho <= mid).OrderByDescending(l => l.Len).FirstOrDefault();
            var sideHigh = group.Where(l => l.Rho > mid).OrderByDescending(l => l.Len).FirstOrDefault();
            if (sideLow.Len <= 0 || sideHigh.Len <= 0)
            {
                return null;
            }

            return ((sideLow.AngleDeg, sideLow.Rho), (sideHigh.AngleDeg, sideHigh.Rho));
        }

        private static bool TryIntersectLines(
            (double Angle, double Rho) a,
            (double Angle, double Rho) b,
            out Point pt)
        {
            var n1 = (a.Angle + 90.0) * Math.PI / 180.0;
            var n2 = (b.Angle + 90.0) * Math.PI / 180.0;
            var c1 = Math.Cos(n1);
            var s1 = Math.Sin(n1);
            var c2 = Math.Cos(n2);
            var s2 = Math.Sin(n2);
            var det = c1 * s2 - s1 * c2;
            if (Math.Abs(det) < 1e-6)
            {
                pt = default;
                return false;
            }

            var x = (a.Rho * s2 - b.Rho * s1) / det;
            var y = (c1 * b.Rho - c2 * a.Rho) / det;
            pt = new Point((int)Math.Round(x), (int)Math.Round(y));
            return true;
        }

        private static Point SnapToStrongEdge(Mat gray, Mat mask, Point approx)
        {
            const int r = 6;
            var best = approx;
            var bestScore = -1.0;
            var h = gray.Rows;
            var w = gray.Cols;
            for (var dy = -r; dy <= r; dy++)
            for (var dx = -r; dx <= r; dx++)
            {
                var x = approx.X + dx;
                var y = approx.Y + dy;
                if (x < 1 || y < 1 || x >= w - 1 || y >= h - 1)
                {
                    continue;
                }

                if (mask.At<byte>(y, x) == 0)
                {
                    continue;
                }

                var gx = gray.At<byte>(y, x + 1) - gray.At<byte>(y, x - 1);
                var gy = gray.At<byte>(y + 1, x) - gray.At<byte>(y - 1, x);
                var score = Math.Abs(gx) + Math.Abs(gy) - (dx * dx + dy * dy) * 0.15;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = new Point(x, y);
                }
            }

            return best;
        }

        internal static int EstimateMainBodyLeftX(Mat mask, Rect bounds)
        {
            var heights = new int[bounds.Width];
            var maxH = 0;
            for (var i = 0; i < bounds.Width; i++)
            {
                var x = bounds.X + i;
                var h = 0;
                for (var y = bounds.Y; y < bounds.Y + bounds.Height; y++)
                {
                    if (mask.At<byte>(y, x) != 0)
                    {
                        h++;
                    }
                }

                heights[i] = h;
                if (h > maxH)
                {
                    maxH = h;
                }
            }

            if (maxH < 10)
            {
                return bounds.X;
            }

            // ä»Žå·¦ä¾§æ‰¾ç¬¬ä¸€æ®µã€Œè¾¾åˆ°ä¸»ä½“é«˜åº¦ã€çš„è¿žç»­åˆ—ï¼Œå…¶èµ·ç‚¹ä½œä¸ºä¸»ä½“å·¦ç•Œï¼›
            // è‹¥å·¦ä¾§å…ˆå‡ºçŽ°çŸ®å°–å†å˜é«˜ï¼Œåˆ™è·³è¿‡çŸ®å°–ï¼ˆæžè€³ï¼‰ã€‚
            var thr = (int)(maxH * 0.55);
            var runNeed = Math.Max(8, bounds.Width / 25);
            var i0 = 0;
            while (i0 < heights.Length)
            {
                while (i0 < heights.Length && heights[i0] < thr)
                {
                    i0++;
                }

                if (i0 >= heights.Length)
                {
                    break;
                }

                var run = 0;
                var j = i0;
                while (j < heights.Length && heights[j] >= thr)
                {
                    run++;
                    j++;
                }

                // å·¦ä¾§çŸ­è€Œçª„çš„é«˜æŸ±æ›´åƒæžè€³æ ¹éƒ¨å±€éƒ¨ï¼Œç»§ç»­æ‰¾ä¸‹ä¸€æ®µ
                var runWidthRatio = run / (double)bounds.Width;
                if (run >= runNeed && runWidthRatio >= 0.18)
                {
                    return bounds.X + i0;
                }

                i0 = j;
            }

            // å›žé€€ï¼šæŒ‰é¢ç§¯ä»Žå³ç´¯è®¡åˆ° 88%
            long total = heights.Sum(h => (long)h);
            long acc = 0;
            for (var i = heights.Length - 1; i >= 0; i--)
            {
                acc += heights[i];
                if (acc >= total * 0.88)
                {
                    return bounds.X + i;
                }
            }

            return bounds.X;
        }

        private readonly struct CornerScore
        {
            public Point Point { get; init; }
            public double Score { get; init; }
        }

        /// <summary>k-cosine è§’ç‚¹ï¼šå¤¹è§’è¶Šå°–åˆ†æ•°è¶Šé«˜ã€‚</summary>
        private static List<CornerScore> FindKCosineCorners(Point[] contour, int k)
        {
            var n = contour.Length;
            var raw = new double[n];
            for (var i = 0; i < n; i++)
            {
                var p = contour[i];
                var a = contour[(i - k + n) % n];
                var b = contour[(i + k) % n];
                var v1x = a.X - p.X;
                var v1y = a.Y - p.Y;
                var v2x = b.X - p.X;
                var v2y = b.Y - p.Y;
                var n1 = Math.Sqrt(v1x * v1x + v1y * v1y);
                var n2 = Math.Sqrt(v2x * v2x + v2y * v2y);
                if (n1 < 1e-3 || n2 < 1e-3)
                {
                    raw[i] = 0;
                    continue;
                }

                var cos = (v1x * v2x + v1y * v2y) / (n1 * n2);
                cos = Math.Clamp(cos, -1.0, 1.0);
                // cos è¶Šå°ï¼ˆè¶ŠæŽ¥è¿‘ -1 è¶Šå°–ï¼‰â†’ åˆ†æ•°è¶Šé«˜ï¼›è¿‡æ»¤è¿‘ä¼¼å…±çº¿
                raw[i] = cos > 0.55 ? 0 : (1.0 - cos);
            }

            // éžæžå¤§æŠ‘åˆ¶
            var minSep = Math.Max(k, n / 40);
            var result = new List<CornerScore>();
            for (var i = 0; i < n; i++)
            {
                if (raw[i] < 0.25)
                {
                    continue;
                }

                var isMax = true;
                for (var d = 1; d <= minSep / 2; d++)
                {
                    if (raw[(i - d + n) % n] > raw[i] || raw[(i + d) % n] > raw[i])
                    {
                        isMax = false;
                        break;
                    }
                }

                if (isMax)
                {
                    result.Add(new CornerScore { Point = contour[i], Score = raw[i] });
                }
            }

            return result;
        }

        /// <summary>
        /// åœ¨å€™é€‰è§’ç‚¹ä¸­é€‰æœ€ä¼˜é¡¶æ¿å››è¾¹å½¢ï¼šåä¸Šã€é¢ç§¯é€‚ä¸­ã€è¿‘ä¼¼å‡¸å¹³è¡Œå››è¾¹å½¢ã€‚
        /// </summary>
        private static Point[] SelectBestTopPlateQuad(List<Point> pool, Rect bounds)
        {
            if (pool.Count == 4)
            {
                return pool.ToArray();
            }

            // å€™é€‰è¿‡å¤šæ—¶å…ˆæŒ‰ Y å–åä¸Šçš„ç‚¹ï¼Œå†é™åˆ¶ç»„åˆæ•°
            var work = pool
                .OrderBy(p => p.Y)
                .ThenByDescending(p => p.X)
                .Take(10)
                .ToList();
            if (work.Count < 4)
            {
                work = pool.Take(10).ToList();
            }

            var bestScore = double.MinValue;
            Point[]? best = null;
            var n = work.Count;
            for (var a = 0; a < n - 3; a++)
            for (var b = a + 1; b < n - 2; b++)
            for (var c = b + 1; c < n - 1; c++)
            for (var d = c + 1; d < n; d++)
            {
                var quad = new[] { work[a], work[b], work[c], work[d] };
                var ordered = OrderQuadCorners(quad);
                var pts = new[] { ordered.Tl, ordered.Tr, ordered.Br, ordered.Bl };
                if (!IsConvexQuad(pts))
                {
                    continue;
                }

                var area = Math.Abs(Cv2.ContourArea(pts));
                var minArea = bounds.Width * bounds.Height * 0.02;
                var maxArea = bounds.Width * bounds.Height * 0.55;
                if (area < minArea || area > maxArea)
                {
                    continue;
                }

                var meanY = pts.Average(p => p.Y);
                var heightSpan = pts.Max(p => p.Y) - pts.Min(p => p.Y);
                var widthSpan = pts.Max(p => p.X) - pts.Min(p => p.X);
                if (widthSpan < bounds.Width * 0.12 || heightSpan < 8)
                {
                    continue;
                }

                // é¡¶æ¿åº”åä¸Šï¼›é«˜å®½æ¯”ä¸å®œåƒç«–æ¡
                var aspect = widthSpan / (double)Math.Max(1, heightSpan);
                if (aspect < 0.45)
                {
                    continue;
                }

                var para = ParallelogramScore(pts);
                var heightBonus = 1.0 - (meanY - bounds.Y) / Math.Max(1.0, bounds.Height);
                var score = area * (0.55 + 0.45 * para) * (0.4 + 0.6 * heightBonus);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = pts;
                }
            }

            if (best == null)
            {
                // å›žé€€ï¼šä»Žå€™é€‰ä¸­å– xÂ±y å››ä¸ªæžç«¯ç‚¹ï¼ˆåŽ»é‡åŽè‹¥ä¸è¶³åˆ™è¡¥é½ï¼‰
                var extremes = PickExtremeCorners(work);
                return extremes;
            }

            return best;
        }

        /// <summary>ä»Žå€™é€‰ç‚¹ä¸­å– TL/TR/BR/BL å››ä¸ªæžç«¯ç‚¹ã€‚</summary>
        private static Point[] PickExtremeCorners(List<Point> pts)
        {
            if (pts.Count == 0)
            {
                throw new InvalidOperationException("æ— å€™é€‰è§’ç‚¹");
            }

            if (pts.Count <= 4)
            {
                // ä¸è¶³ 4 ä¸ªæ—¶ç”¨åŒ…å›´ç›’è§’è¡¥é½
                while (pts.Count < 4)
                {
                    pts.Add(pts[^1]);
                }

                var q = OrderQuadCorners(pts.Take(4).ToArray());
                return new[] { q.Tl, q.Tr, q.Br, q.Bl };
            }

            Point tl = pts[0], tr = pts[0], br = pts[0], bl = pts[0];
            var minSum = int.MaxValue;
            var maxSum = int.MinValue;
            var minDiff = int.MaxValue;
            var maxDiff = int.MinValue;
            foreach (var p in pts)
            {
                var sum = p.X + p.Y;
                var diff = p.X - p.Y;
                if (sum < minSum)
                {
                    minSum = sum;
                    tl = p;
                }

                if (sum > maxSum)
                {
                    maxSum = sum;
                    br = p;
                }

                if (diff > maxDiff)
                {
                    maxDiff = diff;
                    tr = p;
                }

                if (diff < minDiff)
                {
                    minDiff = diff;
                    bl = p;
                }
            }

            return new[] { tl, tr, br, bl };
        }

        internal static (Point Tl, Point Tr, Point Br, Point Bl) OrderQuadCorners(Point[] pts)
        {
            if (pts.Length != 4)
            {
                throw new ArgumentException("éœ€è¦æ°å¥½ 4 ä¸ªç‚¹", nameof(pts));
            }

            // ç»å…¸åˆ†é…ï¼šTL=min(x+y), TR=min(-x+y), BR=max(x+y), BL=max(-x+y)
            Point tl = pts[0], tr = pts[0], br = pts[0], bl = pts[0];
            var minSum = int.MaxValue;
            var maxSum = int.MinValue;
            var minDiff = int.MaxValue;
            var maxDiff = int.MinValue;
            foreach (var p in pts)
            {
                var sum = p.X + p.Y;
                var diff = p.X - p.Y;
                if (sum < minSum)
                {
                    minSum = sum;
                    tl = p;
                }

                if (sum > maxSum)
                {
                    maxSum = sum;
                    br = p;
                }

                // TR: åå³ä¸Š â†’ x å¤§ y å° â†’ x-y å¤§ï¼›BL: åå·¦ä¸‹ â†’ x å° y å¤§ â†’ x-y å°
                // æ³¨æ„ï¼šå¯¹å€¾æ–œå››è¾¹å½¢ï¼ŒTR å¸¸ç”¨ max(x-y)ï¼ŒBL ç”¨ min(x-y)
                if (diff > maxDiff)
                {
                    maxDiff = diff;
                    tr = p;
                }

                if (diff < minDiff)
                {
                    minDiff = diff;
                    bl = p;
                }
            }

            // è‹¥æžç«¯å…¬å¼æ’žç‚¹ï¼ˆé€€åŒ–ï¼‰ï¼Œæ”¹ç”¨è´¨å¿ƒæžè§’æŽ’åº
            if (PointsEqual(tl, tr) || PointsEqual(tr, br) || PointsEqual(br, bl) || PointsEqual(bl, tl)
                || PointsEqual(tl, br) || PointsEqual(tr, bl))
            {
                return OrderByCentroidAngle(pts);
            }

            return (tl, tr, br, bl);
        }

        private static (Point Tl, Point Tr, Point Br, Point Bl) OrderByCentroidAngle(Point[] pts)
        {
            var cx = pts.Average(p => p.X);
            var cy = pts.Average(p => p.Y);
            var ordered = pts
                .Select(p => (P: p, Ang: Math.Atan2(p.Y - cy, p.X - cx)))
                .OrderBy(t => t.Ang)
                .Select(t => t.P)
                .ToArray();

            // æžè§’æŽ’åºåŽï¼Œå– y æœ€å°çš„ä¸ºèµ·ç‚¹é™„è¿‘çš„ä¸Šè¾¹ï¼Œå†æŒ‰é¡ºæ—¶é’ˆ/é€†æ—¶é’ˆæ‰¾ TL
            var start = 0;
            for (var i = 1; i < 4; i++)
            {
                if (ordered[i].Y < ordered[start].Y
                    || (ordered[i].Y == ordered[start].Y && ordered[i].X < ordered[start].X))
                {
                    start = i;
                }
            }

            Point At(int i) => ordered[(start + i) % 4];
            // ä»Žæœ€ä¸Šç‚¹èµ·ï¼ŒæŒ‰æžè§’é¡ºåºçš„ä¸‹ä¸€ç‚¹åº”æ›´åå³ â†’ TRï¼›å† BRï¼›å† BL
            var p0 = At(0);
            var p1 = At(1);
            var p2 = At(2);
            var p3 = At(3);
            // ä¿è¯ p0-p1 ä¸ºä¸Šè¾¹ï¼šè‹¥ p1 åœ¨ p0 å·¦ä¾§ï¼Œåˆ™åå‘
            if (p1.X < p0.X)
            {
                return (p0, p3, p2, p1);
            }

            return (p0, p1, p2, p3);
        }

        private static bool PointsEqual(Point a, Point b)
            => a.X == b.X && a.Y == b.Y;

        private static bool IsConvexQuad(Point[] pts)
        {
            if (pts.Length != 4)
            {
                return false;
            }

            var sign = 0;
            for (var i = 0; i < 4; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % 4];
                var c = pts[(i + 2) % 4];
                var cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
                if (cross == 0)
                {
                    continue;
                }

                var s = cross > 0 ? 1 : -1;
                if (sign == 0)
                {
                    sign = s;
                }
                else if (sign != s)
                {
                    return false;
                }
            }

            return sign != 0;
        }

        /// <summary>å¯¹è¾¹å‘é‡å¤¹è§’æŽ¥è¿‘ â†’ æ›´åƒå¹³è¡Œå››è¾¹å½¢ï¼ˆ0~1ï¼‰ã€‚</summary>
        private static double ParallelogramScore(Point[] pts)
        {
            static double Len(Point a, Point b)
            {
                var dx = b.X - a.X;
                var dy = b.Y - a.Y;
                return Math.Sqrt(dx * dx + dy * dy);
            }

            static double DotNorm(Point a1, Point a2, Point b1, Point b2)
            {
                var ax = a2.X - a1.X;
                var ay = a2.Y - a1.Y;
                var bx = b2.X - b1.X;
                var by = b2.Y - b1.Y;
                var na = Math.Sqrt(ax * ax + ay * ay);
                var nb = Math.Sqrt(bx * bx + by * by);
                if (na < 1e-3 || nb < 1e-3)
                {
                    return 0;
                }

                return Math.Abs((ax * bx + ay * by) / (na * nb));
            }

            // å¯¹è¾¹å¹³è¡Œåº¦ + å¯¹è¾¹é•¿åº¦æ¯”
            var parallel1 = DotNorm(pts[0], pts[1], pts[3], pts[2]);
            var parallel2 = DotNorm(pts[1], pts[2], pts[0], pts[3]);
            var len01 = Len(pts[0], pts[1]);
            var len32 = Len(pts[3], pts[2]);
            var len12 = Len(pts[1], pts[2]);
            var len03 = Len(pts[0], pts[3]);
            var ratio1 = Math.Min(len01, len32) / Math.Max(1e-3, Math.Max(len01, len32));
            var ratio2 = Math.Min(len12, len03) / Math.Max(1e-3, Math.Max(len12, len03));
            return (parallel1 + parallel2) * 0.35 + (ratio1 + ratio2) * 0.15;
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

        internal static Point InsetToward(Point corner, Point2f center, int insetPx)
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

        internal static void ValidateNonCollinear((int X, int Y) a, (int X, int Y) b, (int X, int Y) c)
        {
            var area2 = Math.Abs(
                (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X));
            if (area2 < 80)
            {
                throw new InvalidOperationException($"ä¸Šç›–ä¸‰ç‚¹è¿‘ä¼¼å…±çº¿ï¼ˆarea2={area2}ï¼‰ï¼Œæ— æ³•å®šå¹³é¢");
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
            MarkCorner(sr, "BR", new Scalar(0, 255, 255));
            MarkCorner(sl, "BL", new Scalar(0, 255, 255));
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
                Cv2.PutText(canvas, labels[i], new Point(p.X + 10, p.Y + 4),
                    HersheyFonts.HersheySimplex, 0.55, colors[i], 1);
            }

            Cv2.ImWrite(path, canvas);
        }
    }
}

