using System;
using System.IO;
using OpenCvSharp;
using SUNWODA_SEVB.Tool.Helper;

var path = @""C:\AutoSlice\cutpicture\jietu\_pre321_test\vision_3d_sample.png"";
var outDir = @""C:\AutoSlice\cutpicture\jietu\_pre321_test\_diag"";
Directory.CreateDirectory(outDir);
using var src = Cv2.ImRead(path, ImreadModes.Color);
using var gray = new Mat();
Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
using var mask = new Mat();
Cv2.Threshold(gray, mask, 12, 255, ThresholdTypes.Binary);
using var kClose = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5,5));
using var kOpen = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(3,3));
Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kClose);
Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kOpen);
Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxNone);
var c = contours.OrderByDescending(x => Cv2.ContourArea(x)).First();
var bounds = Cv2.BoundingRect(c);
Console.WriteLine($""bounds={bounds.X},{bounds.Y},{bounds.Width}x{bounds.Height} contourPts={c.Length}"");
using var edges = new Mat();
Cv2.Canny(gray, edges, 40, 120);
using var inner = new Mat();
using var kErode = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(3,3));
Cv2.Erode(mask, inner, kErode);
Cv2.BitwiseAnd(edges, inner, edges);
var minLen = Math.Max(28, bounds.Width/12);
var segs = Cv2.HoughLinesP(edges, 1, Math.PI/180, Math.Max(25, bounds.Width/35), minLen, Math.Max(8, bounds.Width/40));
Console.WriteLine($""hough segs={(segs?.Length ?? 0)} minLen={minLen}"");
using var canvas = src.Clone();
if (segs != null)
{
  foreach (var s in segs)
  {
    Cv2.Line(canvas, s.P1, s.P2, new Scalar(0,255,0), 1);
    Console.WriteLine($""  line ({s.P1.X},{s.P1.Y})-({s.P2.X},{s.P2.Y})"");
  }
}
Cv2.ImWrite(Path.Combine(outDir, ""edges.png""), edges);
Cv2.ImWrite(Path.Combine(outDir, ""lines.png""), canvas);
Cv2.ImWrite(Path.Combine(outDir, ""mask.png""), mask);
try {
  var r = Vision3dSectionPointsDetector.Detect(path, outDir);
  Console.WriteLine(""DETECT "" + r.Summary);
} catch (Exception ex) {
  Console.WriteLine(""DETECT FAIL: "" + ex.Message);
}
