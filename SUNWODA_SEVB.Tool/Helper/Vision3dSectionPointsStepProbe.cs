using System;
using System.Collections.Generic;
using System.IO;
using OpenCvSharp;
using Point = OpenCvSharp.Point;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>
    /// 本地 3D 斜俯视找点分步探针：每调用一次 <see cref="StepNext"/> 推进一档并返回可视化图。
    /// 供调试弹窗逐步排查「线/点画不出来」卡在哪一步。
    /// </summary>
    public sealed class Vision3dSectionPointsStepProbe : IDisposable
    {
        public sealed class StepFrame
        {
            public required int StepIndex { get; init; }
            public required int TotalSteps { get; init; }
            public required string Title { get; init; }
            public required string Detail { get; init; }
            /// <summary>BGR 图，调用方负责 Dispose。</summary>
            public required Mat BgrImage { get; init; }
            public bool IsTerminal { get; init; }
            public bool Success { get; init; }
        }

        private readonly string _imagePath;
        private Mat? _src;
        private Mat? _gray;
        private Mat? _mask;
        private Point[]? _contour;
        private Rect _bounds;
        private int _bodyLeft;
        private Rect _faceRoi;
            private (Point Tl, Point Tr, Point Br, Point Bl)? _acceptedQuad;
        private (Point Tl, Point Tr, Point Br, Point Bl)? _midNearMiss;
        private string _acceptedPath = string.Empty;
        private int _step = -1;
        private readonly List<int> _history = new();
        private int _midGrayLo = Vision3dSectionPointsDetector.MidGrayLo;
        private int _midGrayHi = Vision3dSectionPointsDetector.MidGrayHi;
        private bool _disposed;

        private static readonly string[] StepTitles =
        [
            "① 原图 / 灰度",
            "② 剪影 Mask + 外接框",
            "③ 主体左界 bodyLeft + 顶面搜索 ROI",
            "④ 中灰 InRange 原始",
            "⑤ 中灰：排除高亮",
            "⑥ 中灰：切左 + 切下 60%",
            "⑦ 中灰：形态学 + 轮廓/凸包角点",
            "⑧ 回退：FloodFill 顶面",
            "⑨ 回退：ROI 顶面",
            "⑩ 回退：边缘轮廓",
            "⑪ 回退：直线交点",
            "⑫ 回退：Harris 角点",
            "⑬ 汇总：选中路径 / 最终三点+原点",
        ];

        public Vision3dSectionPointsStepProbe(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                throw new FileNotFoundException("3D 截图不存在", imagePath);
            }

            _imagePath = imagePath;
        }

        public int CurrentStepIndex => _step;
        public int TotalSteps => StepTitles.Length;
        public bool CanStepNext => !_disposed && _step < TotalSteps - 1;
        public bool CanStepPrev => !_disposed && _history.Count >= 2;
        public string ImagePath => _imagePath;
        public int MidGrayLo => _midGrayLo;
        public int MidGrayHi => _midGrayHi;

        /// <summary>
        /// 手动调参：供分步调试界面实时预览/覆盖中灰阈值。
        /// 注意：该阈值只影响中灰路径（分步 ③~⑦），不改变剪影 mask/外接框等内容。
        /// </summary>
        public void SetMidGrayRange(int lo, int hi)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            lo = Math.Clamp(lo, 0, 255);
            hi = Math.Clamp(hi, 0, 255);
            if (lo > hi)
            {
                // 调参时允许用户越过：自动交换，避免出现“空区间”
                (lo, hi) = (hi, lo);
            }

            _midGrayLo = lo;
            _midGrayHi = hi;
        }

        /// <summary>
        /// 给灰度调参弹窗用：只显示灰度落在 [lo, hi] 的原图像素，其余置黑（无轮廓线）。
        /// 调用方负责释放返回的 Mat。
        /// </summary>
        public Mat RenderMidGrayInRangePreview(int lo, int hi)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            EnsureLoaded();

            lo = Math.Clamp(lo, 0, 255);
            hi = Math.Clamp(hi, 0, 255);
            if (lo > hi)
            {
                (lo, hi) = (hi, lo);
            }

            using var mid = new Mat();
            Cv2.InRange(
                _gray!,
                new Scalar(lo),
                new Scalar(hi),
                mid);

            // 只保留区间内像素的原图颜色，区间外一律黑
            var canvas = new Mat(_src!.Size(), _src.Type(), Scalar.All(0));
            _src.CopyTo(canvas, mid);
            return canvas;
        }

        public void Reset()
        {
            DisposeMats();
            _contour = null;
            _acceptedQuad = null;
            _midNearMiss = null;
            _acceptedPath = string.Empty;
            _step = -1;
            _history.Clear();
            _midGrayLo = Vision3dSectionPointsDetector.MidGrayLo;
            _midGrayHi = Vision3dSectionPointsDetector.MidGrayHi;
        }

        public StepFrame StepNext()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!CanStepNext && _step >= 0)
            {
                throw new InvalidOperationException("已到最后一步");
            }

            _step++;
            // 正式路径已采纳后，⑧～⑫ 回退与主算法一致：不再执行，直接跳最终汇总
            if (_acceptedQuad != null && _step is >= 7 and <= 11)
            {
                _step = 12;
            }

            var frame = _step switch
            {
                0 => Step0_Load(),
                1 => Step1_Mask(),
                2 => Step2_BodyLeftRoi(),
                3 => Step3_MidGrayRaw(),
                4 => Step4_MidGrayNoBright(),
                5 => Step5_MidGrayCuts(),
                6 => Step6_MidGrayCorners(),
                7 => Step7_Flood(),
                8 => Step8_Roi(),
                9 => Step9_Edge(),
                10 => Step10_Lines(),
                11 => Step11_Harris(),
                12 => Step12_Final(),
                _ => throw new InvalidOperationException($"未知步骤 {_step}"),
            };

            _history.Add(_step);
            return frame;
        }

        public StepFrame StepPrev()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!CanStepPrev)
            {
                throw new InvalidOperationException("已到最前一步，无法上一步。");
            }

            // 上一步应该回到“曾经实际执行并显示过”的前一档（history 解决了主链采纳后跳过回退步骤的问题）
            var targetStep = _history[^2];
            return StepTo(targetStep);
        }

        private StepFrame StepTo(int targetStep)
        {
            if (targetStep < 0 || targetStep >= TotalSteps)
            {
                throw new ArgumentOutOfRangeException(nameof(targetStep), $"targetStep={targetStep} 不在 [0,{TotalSteps - 1}]");
            }

            // 上一步属于“演示回退”：不应丢失灰度调参弹窗里用户设置的阈值覆盖
            var tunedLo = _midGrayLo;
            var tunedHi = _midGrayHi;

            Reset();
            SetMidGrayRange(tunedLo, tunedHi);
            StepFrame frame = null!;
            for (var i = 0; i <= targetStep; i++)
            {
                frame = StepNext();
            }

            return frame;
        }

        private StepFrame Step0_Load()
        {
            _src = Cv2.ImRead(_imagePath, ImreadModes.Color);
            if (_src == null || _src.Empty())
            {
                throw new InvalidOperationException($"无法读取图像：{_imagePath}");
            }

            _gray = new Mat();
            Cv2.CvtColor(_src, _gray, ColorConversionCodes.BGR2GRAY);
            var canvas = _src.Clone();
            Cv2.PutText(
                canvas,
                $"{_src.Width}x{_src.Height}",
                new Point(12, 28),
                HersheyFonts.HersheySimplex,
                0.8,
                new Scalar(0, 255, 255),
                2);
            return Frame(
                canvas,
                $"已加载原图。灰度图尺寸 {_gray.Width}x{_gray.Height}。",
                isTerminal: false,
                success: true);
        }

        private StepFrame Step1_Mask()
        {
            EnsureLoaded();
            _mask?.Dispose();
            _mask = Vision3dSectionPointsDetector.BuildSilhouetteMask(_gray!);
            Cv2.FindContours(
                _mask,
                out var contours,
                out _,
                RetrievalModes.External,
                ContourApproximationModes.ApproxNone);
            if (contours.Length == 0)
            {
                return Frame(
                    OverlayMask(_src!, _mask),
                    "失败：未分割到物体剪影（阈值 12）。后续步骤无法继续有效找点。",
                    isTerminal: true,
                    success: false);
            }

            _contour = Vision3dSectionPointsDetector.SmoothContour(
                contours.OrderByDescending(c => Cv2.ContourArea(c)).First(),
                window: 7);
            _bounds = Cv2.BoundingRect(_contour);
            var canvas = OverlayMask(_src!, _mask);
            Cv2.Rectangle(canvas, _bounds, new Scalar(0, 255, 255), 2);
            Cv2.Polylines(canvas, new[] { _contour }, true, new Scalar(255, 128, 0), 1);
            var ok = _bounds.Width >= 40 && _bounds.Height >= 40;
            return Frame(
                canvas,
                ok
                    ? $"剪影 OK。外接框=({_bounds.X},{_bounds.Y},{_bounds.Width}x{_bounds.Height})，轮廓点数={_contour.Length}。"
                    : $"失败：外接框过小 {_bounds.Width}x{_bounds.Height}。",
                isTerminal: !ok,
                success: ok);
        }

        private StepFrame Step2_BodyLeftRoi()
        {
            EnsureMask();
            _bodyLeft = Vision3dSectionPointsDetector.EstimateMainBodyLeftX(_mask!, _bounds);
            _faceRoi = Vision3dSectionPointsDetector.BuildTopFaceSearchRoi(_bounds, _bodyLeft);
            var canvas = _src!.Clone();
            Cv2.Rectangle(canvas, _bounds, new Scalar(80, 80, 80), 1);
            Cv2.Line(
                canvas,
                new Point(_bodyLeft, _bounds.Y),
                new Point(_bodyLeft, _bounds.Y + _bounds.Height),
                new Scalar(0, 0, 255),
                2);
            Cv2.Rectangle(canvas, _faceRoi, new Scalar(0, 255, 0), 2);
            Cv2.PutText(canvas, "bodyLeft", new Point(_bodyLeft + 4, _bounds.Y + 20),
                HersheyFonts.HersheySimplex, 0.55, new Scalar(0, 0, 255), 1);
            Cv2.PutText(canvas, "faceRoi", new Point(_faceRoi.X + 4, _faceRoi.Y + 20),
                HersheyFonts.HersheySimplex, 0.55, new Scalar(0, 255, 0), 1);
            return Frame(
                canvas,
                $"bodyLeft={_bodyLeft}；faceRoi=({_faceRoi.X},{_faceRoi.Y},{_faceRoi.Width}x{_faceRoi.Height})。红线=主体左界，绿框=顶面搜索区。",
                isTerminal: false,
                success: true);
        }

        private StepFrame Step3_MidGrayRaw()
        {
            EnsureMask();
            using var mid = new Mat();
            Cv2.InRange(
                _gray!,
                new Scalar(_midGrayLo),
                new Scalar(_midGrayHi),
                mid);
            Cv2.BitwiseAnd(mid, _mask!, mid);
            return Frame(
                OverlayMask(_src!, mid),
                $"中灰 InRange[{_midGrayLo},{_midGrayHi}] ∩ 剪影。白区应为上平面主体（端子高亮区可能仍在）。",
                isTerminal: false,
                success: true);
        }

        private StepFrame Step4_MidGrayNoBright()
        {
            EnsureMask();
            using var mid = BuildMidGrayAfterBrightExclude();
            return Frame(
                OverlayMask(_src!, mid),
                $"排除亮度≥{Vision3dSectionPointsDetector.BrightExclude} 后的中灰。圆端子高亮应被挖空。",
                isTerminal: false,
                success: true);
        }

        private StepFrame Step5_MidGrayCuts()
        {
            EnsureMask();
            using var mid = BuildMidGrayAfterCuts();
            var cutX = Math.Min(_gray!.Cols - 1, _bodyLeft + Math.Max(16, _bounds.Width / 24));
            var yCut = _bounds.Y + (int)(_bounds.Height * 0.60);
            var canvas = OverlayMask(_src!, mid);
            Cv2.Line(canvas, new Point(cutX, 0), new Point(cutX, canvas.Rows - 1), new Scalar(0, 0, 255), 1);
            Cv2.Line(canvas, new Point(0, yCut), new Point(canvas.Cols - 1, yCut), new Scalar(255, 0, 255), 1);
            return Frame(
                canvas,
                $"切左 cutX={cutX}（红）+ 切下 yCut={yCut}（紫，物体上 60%）。白区过碎/过小会在下一步失败。",
                isTerminal: false,
                success: true);
        }

        private StepFrame Step6_MidGrayCorners()
        {
            EnsureMask();
            using var mid = BuildMidGrayMorph();
            var canvas = OverlayMask(_src!, mid);
            Cv2.FindContours(mid, out var cs, out _, RetrievalModes.External, ContourApproximationModes.ApproxNone);
            if (cs.Length == 0)
            {
                return Frame(canvas, "中灰路径失败：形态学后无轮廓。", isTerminal: false, success: false);
            }

            var plate = cs.OrderByDescending(c => Cv2.ContourArea(c)).First();
            var area = Cv2.ContourArea(plate);
            var minArea = _bounds.Width * _bounds.Height * 0.04;
            var maxArea = _bounds.Width * _bounds.Height * 0.45;
            Cv2.DrawContours(canvas, new[] { plate }, -1, new Scalar(255, 128, 0), 2);

            if (area < minArea || area > maxArea)
            {
                return Frame(
                    canvas,
                    $"中灰路径失败：面积={area:F0} 不在 [{minArea:F0},{maxArea:F0}]。",
                    isTerminal: false,
                    success: false);
            }

            var hull = Cv2.ConvexHull(plate).ToList();
            if (hull.Count < 4)
            {
                return Frame(canvas, $"中灰路径失败：凸包点数={hull.Count}<4。", isTerminal: false, success: false);
            }

            Cv2.Polylines(canvas, new[] { hull.ToArray() }, true, new Scalar(0, 255, 255), 1);
            var tip = hull.OrderBy(p => p.Y).ThenByDescending(p => p.X).First();
            var brCands = hull.Where(p => p.Y > tip.Y + 30).ToList();
            var br = brCands.Count > 0
                ? brCands.OrderByDescending(p => p.X).First()
                : hull.OrderByDescending(p => p.X).First();
            var tl = hull.OrderBy(p => p.X + p.Y).First();
            var blPt = Vision3dSectionPointsDetector.FindBottomBoundaryPeak(mid, plate);
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
            DrawQuad(canvas, q, new Scalar(0, 255, 0));
            var plausible = Vision3dSectionPointsDetector.IsPlausibleTopQuad(q, _bounds);
            var reason = ExplainPlausible(q, _bounds);
            if (plausible)
            {
                _acceptedQuad = q;
                _acceptedPath = "MidGray";
                StampStatus(canvas, "ADOPT MidGray -> next = FINAL", new Scalar(0, 200, 0));
            }
            else
            {
                // 与主算法一致：保留近失配供最终兜底
                _midNearMiss = q;
                StampStatus(canvas, "NEAR-MISS MidGray (try fallbacks)", new Scalar(0, 165, 255));
            }

            return Frame(
                canvas,
                plausible
                    ? $"中灰路径成功，已采纳。{reason} 再点「下一步」将跳过回退，直接出最终三点+原点（与主流程一致）。"
                    : $"中灰角点已检出（绿框）但未过 IsPlausibleTopQuad：{reason}。" +
                      "未采纳，故继续⑧～⑫回退；若回退全失败，最终步会兜底采用本绿框。",
                isTerminal: false,
                success: plausible);
        }

        private StepFrame Step7_Flood()
        {
            return TryFallback(
                "FloodFill",
                () => Vision3dSectionPointsDetector.TryDetectTopPlateByFloodFill(
                    _gray!, _mask!, _bounds, _bodyLeft),
                snap: true);
        }

        private StepFrame Step8_Roi()
        {
            return TryFallback(
                "ROI",
                () => Vision3dSectionPointsDetector.TryDetectTopPlateInRoi(
                    _gray!, _mask!, _bounds, _faceRoi),
                snap: true);
        }

        private StepFrame Step9_Edge()
        {
            return TryFallback(
                "边缘轮廓",
                () => Vision3dSectionPointsDetector.TryDetectTopPlateByEdgeContour(
                    _gray!, _mask!, _bounds, _bodyLeft),
                snap: true);
        }

        private StepFrame Step10_Lines()
        {
            EnsureMask();
            var canvas = _src!.Clone();
            if (_acceptedQuad != null)
            {
                DrawQuad(canvas, _acceptedQuad.Value, new Scalar(0, 255, 0));
                StampStatus(canvas, "SKIP Lines: already adopted", new Scalar(0, 200, 0));
                return Frame(
                    canvas,
                    $"已有采纳路径「{_acceptedPath}」，跳过直线回退。",
                    isTerminal: false,
                    success: true);
            }

            if (_midNearMiss != null)
            {
                DrawQuad(canvas, _midNearMiss.Value, new Scalar(0, 255, 255));
            }

            var fromLines = Vision3dSectionPointsDetector.TryDetectTopPlateByLines(
                _gray!, _mask!, _bounds, _bodyLeft);
            if (fromLines == null)
            {
                StampStatus(canvas, "Lines: null", new Scalar(0, 0, 255));
                return Frame(canvas, "直线路径：未检出有效交点。", isTerminal: false, success: false);
            }

            var ordered = Vision3dSectionPointsDetector.OrderQuadCorners(new[]
            {
                fromLines.Value.Tl, fromLines.Value.Tr, fromLines.Value.Br, fromLines.Value.Bl
            });
            DrawQuad(canvas, ordered, new Scalar(255, 0, 255));
            var ok = Vision3dSectionPointsDetector.IsPlausibleTopQuad(ordered, _bounds);
            var reason = ExplainPlausible(ordered, _bounds);
            if (ok)
            {
                _acceptedQuad = Vision3dSectionPointsDetector.SnapQuad(_gray!, _mask!, ordered);
                _acceptedPath = "直线 Lines";
                StampStatus(canvas, "ADOPT Lines", new Scalar(0, 200, 0));
            }
            else
            {
                StampStatus(canvas, "Lines: rejected by IsPlausible", new Scalar(0, 165, 255));
            }

            return Frame(
                canvas,
                ok ? $"直线路径成功。{reason}" : $"直线路径未通过合理性：{reason}",
                isTerminal: false,
                success: ok);
        }

        private StepFrame Step11_Harris()
        {
            return TryFallback(
                "Harris",
                () => Vision3dSectionPointsDetector.TryDetectTopPlateByHarris(
                    _gray!, _mask!, _faceRoi),
                snap: true);
        }

        private StepFrame Step12_Final()
        {
            EnsureMask();
            var canvas = _src!.Clone();

            // 与主算法一致：回退全失败时，兜底采用中灰近失配
            if (_acceptedQuad == null && _midNearMiss != null)
            {
                _acceptedQuad = _midNearMiss;
                _acceptedPath = "MidGray-nearMiss-fallback";
            }

            if (_acceptedQuad == null)
            {
                StampStatus(canvas, "FAIL: no path accepted", new Scalar(0, 0, 255));
                return Frame(
                    canvas,
                    "汇总失败：中灰 + 全部回退均未给出可用四角。",
                    isTerminal: true,
                    success: false);
            }

            var q = _acceptedQuad.Value;
            DrawQuad(canvas, q, new Scalar(0, 255, 255));
            var tip = Vision3dSectionPointsDetector.PickTopmostTip(q.Tl, q.Tr, q.Br, q.Bl);
            var roughCenter = new Point2f(
                (q.Tl.X + q.Tr.X + q.Br.X + q.Bl.X) / 4f,
                (q.Tl.Y + q.Tr.Y + q.Br.Y + q.Bl.Y) / 4f);
            var tl = Vision3dSectionPointsDetector.InsetToward(q.Tl, roughCenter, Vision3dSectionPointsDetector.CornerPullInPx);
            var tr = Vision3dSectionPointsDetector.InsetToward(q.Tr, roughCenter, Vision3dSectionPointsDetector.CornerPullInPx);
            var sr = Vision3dSectionPointsDetector.InsetToward(q.Br, roughCenter, Vision3dSectionPointsDetector.CornerPullInPx);
            var sl = Vision3dSectionPointsDetector.InsetToward(q.Bl, roughCenter, Vision3dSectionPointsDetector.CornerPullInPx);
            tip = Vision3dSectionPointsDetector.InsetToward(tip, roughCenter, Math.Min(Vision3dSectionPointsDetector.CornerPullInPx, 2));
            var center = new Point2f(
                (tl.X + tr.X + sr.X + sl.X) / 4f,
                (tl.Y + tr.Y + sr.Y + sl.Y) / 4f);
            var p0 = Vision3dSectionPointsDetector.InsetToward(tip, center, Vision3dSectionPointsDetector.NearTipPlaneInsetPx);
            var p1 = Vision3dSectionPointsDetector.InsetToward(sr, center, Vision3dSectionPointsDetector.RightCornerInsetPx);
            var p2 = new Point(sl.X + 2, Math.Max(0, sl.Y - Vision3dSectionPointsDetector.NearLeftCornerInsetPx));
            var origin = Vision3dSectionPointsDetector.InsetToward(tip, center, Vision3dSectionPointsDetector.OriginInsetPixels);

            Mark(canvas, p0, "P1", new Scalar(0, 0, 255));
            Mark(canvas, p1, "P2", new Scalar(255, 100, 0));
            Mark(canvas, p2, "P3", new Scalar(0, 200, 0));
            Mark(canvas, origin, "O", new Scalar(0, 165, 255));
            Cv2.Line(canvas, p0, p1, new Scalar(0, 255, 255), 2);
            Cv2.Line(canvas, p1, p2, new Scalar(0, 255, 255), 2);
            Cv2.Line(canvas, p2, p0, new Scalar(0, 255, 255), 2);

            try
            {
                Vision3dSectionPointsDetector.ValidateNonCollinear(
                    (p0.X, p0.Y), (p1.X, p1.Y), (p2.X, p2.Y));
                return Frame(
                    canvas,
                    $"成功。采纳路径={_acceptedPath}。P1=({p0.X},{p0.Y}) P2=({p1.X},{p1.Y}) P3=({p2.X},{p2.Y}) O=({origin.X},{origin.Y})。黄线=上盖三点平面。",
                    isTerminal: true,
                    success: true);
            }
            catch (Exception ex)
            {
                return Frame(
                    canvas,
                    $"角点已出但三点共线校验失败：{ex.Message}",
                    isTerminal: true,
                    success: false);
            }
        }

        private StepFrame TryFallback(
            string name,
            Func<(Point Tl, Point Tr, Point Br, Point Bl)?> detect,
            bool snap)
        {
            EnsureMask();
            var canvas = _src!.Clone();
            if (_acceptedQuad != null)
            {
                DrawQuad(canvas, _acceptedQuad.Value, new Scalar(0, 255, 0));
                StampStatus(canvas, $"SKIP {name}: already adopted", new Scalar(0, 200, 0));
                return Frame(
                    canvas,
                    $"已有采纳路径「{_acceptedPath}」，跳过回退「{name}」。图上绿框=已采纳结果。",
                    isTerminal: false,
                    success: true);
            }

            if (_midNearMiss != null)
            {
                DrawQuad(canvas, _midNearMiss.Value, new Scalar(0, 255, 255));
            }

            var q = detect();
            if (q == null)
            {
                StampStatus(canvas, $"{name}: null (no corners)", new Scalar(0, 0, 255));
                return Frame(
                    canvas,
                    $"{name}：检测返回 null（图上无品红框）。黄框=中灰近失配（若有）。",
                    isTerminal: false,
                    success: false);
            }

            var quad = q.Value;
            DrawQuad(canvas, quad, new Scalar(255, 0, 255));
            var ok = Vision3dSectionPointsDetector.IsPlausibleTopQuad(quad, _bounds);
            var reason = ExplainPlausible(quad, _bounds);
            if (ok)
            {
                _acceptedQuad = snap
                    ? Vision3dSectionPointsDetector.SnapQuad(_gray!, _mask!, quad)
                    : quad;
                _acceptedPath = name;
                StampStatus(canvas, $"ADOPT {name}", new Scalar(0, 200, 0));
            }
            else
            {
                StampStatus(canvas, $"{name}: rejected by IsPlausible", new Scalar(0, 165, 255));
            }

            return Frame(
                canvas,
                ok
                    ? $"{name} 成功并采纳（品红→绿路径）。{reason}"
                    : $"{name} 有角点（品红）但未通过合理性：{reason}",
                isTerminal: false,
                success: ok);
        }

        private Mat BuildMidGrayAfterBrightExclude()
        {
            var mid = new Mat();
            Cv2.InRange(
                _gray!,
                new Scalar(_midGrayLo),
                new Scalar(_midGrayHi),
                mid);
            Cv2.BitwiseAnd(mid, _mask!, mid);
            using var bright = new Mat();
            Cv2.Threshold(_gray!, bright, Vision3dSectionPointsDetector.BrightExclude, 255, ThresholdTypes.Binary);
            Cv2.BitwiseNot(bright, bright);
            Cv2.BitwiseAnd(mid, bright, mid);
            return mid;
        }

        private Mat BuildMidGrayAfterCuts()
        {
            var mid = BuildMidGrayAfterBrightExclude();
            var cutX = Math.Min(_gray!.Cols - 1, _bodyLeft + Math.Max(16, _bounds.Width / 24));
            for (var y = 0; y < _gray.Rows; y++)
            {
                for (var x = 0; x < cutX; x++)
                {
                    mid.Set(y, x, (byte)0);
                }
            }

            var yCut = _bounds.Y + (int)(_bounds.Height * 0.60);
            for (var y = Math.Max(0, yCut); y < _gray.Rows; y++)
            {
                for (var x = 0; x < _gray.Cols; x++)
                {
                    mid.Set(y, x, (byte)0);
                }
            }

            return mid;
        }

        private Mat BuildMidGrayMorph()
        {
            var mid = BuildMidGrayAfterCuts();
            using var k = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(5, 5));
            Cv2.MorphologyEx(mid, mid, MorphTypes.Open, k);
            Cv2.MorphologyEx(mid, mid, MorphTypes.Close, k);
            return mid;
        }

        private static string ExplainPlausible((Point Tl, Point Tr, Point Br, Point Bl) q, Rect bounds)
        {
            var pts = new[] { q.Tl, q.Tr, q.Br, q.Bl };
            if (pts.Distinct().Count() < 4)
            {
                return "四角不互异";
            }

            foreach (var p in pts)
            {
                if (p.X < bounds.X - 5 || p.X > bounds.X + bounds.Width + 5
                    || p.Y < bounds.Y - 5 || p.Y > bounds.Y + bounds.Height + 5)
                {
                    return $"角点越界 p=({p.X},{p.Y}) bounds=({bounds.X},{bounds.Y},{bounds.Width}x{bounds.Height})";
                }
            }

            var area = Math.Abs(Cv2.ContourArea(pts));
            var minA = bounds.Width * bounds.Height * 0.03;
            if (area < minA)
            {
                return $"面积过小 area={area:F0} < {minA:F0}";
            }

            var minY = pts.Min(p => p.Y);
            var maxY = pts.Max(p => p.Y);
            if (maxY - minY > bounds.Height * 0.66)
            {
                return $"高度跨度过大 dy={maxY - minY} > {bounds.Height * 0.66:F0}（阈值 0.66*H）";
            }

            if (maxY > bounds.Y + bounds.Height * 0.78)
            {
                return $"下角过低 maxY={maxY} > {bounds.Y + bounds.Height * 0.78:F0}";
            }

            double Dist(Point a, Point b)
                => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
            var topLen = Dist(q.Tl, q.Tr);
            var botLen = Dist(q.Bl, q.Br);
            var leftLen = Dist(q.Tl, q.Bl);
            var rightLen = Dist(q.Tr, q.Br);
            var sideRatio = Math.Min(leftLen, rightLen) / Math.Max(1.0, Math.Max(leftLen, rightLen));
            var tbRatio = Math.Min(topLen, botLen) / Math.Max(1.0, Math.Max(topLen, botLen));
            if (sideRatio < 0.28 || tbRatio < 0.28 || topLen < bounds.Width * 0.12)
            {
                return $"边长比不合格 side={sideRatio:F2} tb={tbRatio:F2} topLen={topLen:F0}";
            }

            return $"合理 area={area:F0} dy={maxY - minY} side={sideRatio:F2} tb={tbRatio:F2}";
        }

        /// <summary>
        /// OpenCV PutText 仅支持 ASCII；中文会触发 Cannot marshal: unmappable character。
        /// </summary>
        private static void StampStatus(Mat canvas, string text, Scalar color)
        {
            var ascii = ToAsciiSafe(text);
            var width = Math.Min(canvas.Cols - 16, Math.Max(200, ascii.Length * 14 + 24));
            Cv2.Rectangle(canvas, new Rect(8, 8, width, 36), new Scalar(0, 0, 0), -1);
            Cv2.PutText(
                canvas,
                ascii,
                new Point(16, 34),
                HersheyFonts.HersheySimplex,
                0.7,
                color,
                2);
        }

        private static string ToAsciiSafe(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "?";
            }

            var chars = text.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (chars[i] > 0x7F)
                {
                    chars[i] = '?';
                }
            }

            return new string(chars);
        }

        private static Mat OverlayMask(Mat src, Mat mask)
        {
            var canvas = src.Clone();
            using var maskColor = new Mat();
            Cv2.CvtColor(mask, maskColor, ColorConversionCodes.GRAY2BGR);
            Cv2.AddWeighted(canvas, 0.55, maskColor, 0.45, 0, canvas);
            return canvas;
        }

        private static void DrawQuad(Mat canvas, (Point Tl, Point Tr, Point Br, Point Bl) q, Scalar color)
        {
            var quad = new[] { q.Tl, q.Tr, q.Br, q.Bl };
            Cv2.Polylines(canvas, new[] { quad }, true, color, 2);
            Mark(canvas, q.Tl, "TL", color);
            Mark(canvas, q.Tr, "TR", color);
            Mark(canvas, q.Br, "BR", color);
            Mark(canvas, q.Bl, "BL", color);
        }

        private static void Mark(Mat canvas, Point p, string label, Scalar color)
        {
            Cv2.Circle(canvas, p, 7, color, 2);
            Cv2.PutText(
                canvas,
                label,
                new Point(p.X + 8, p.Y - 6),
                HersheyFonts.HersheySimplex,
                0.55,
                color,
                1);
        }

        private StepFrame Frame(Mat bgr, string detail, bool isTerminal, bool success)
            => new()
            {
                StepIndex = _step,
                TotalSteps = TotalSteps,
                Title = StepTitles[_step],
                Detail = detail,
                BgrImage = bgr,
                IsTerminal = isTerminal,
                Success = success,
            };

        private void EnsureLoaded()
        {
            if (_src == null || _gray == null)
            {
                throw new InvalidOperationException("请先执行步骤①");
            }
        }

        private void EnsureMask()
        {
            EnsureLoaded();
            if (_mask == null || _contour == null)
            {
                throw new InvalidOperationException("请先执行步骤②");
            }
        }

        private void DisposeMats()
        {
            _src?.Dispose();
            _gray?.Dispose();
            _mask?.Dispose();
            _src = null;
            _gray = null;
            _mask = null;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            DisposeMats();
            _disposed = true;
        }
    }
}
