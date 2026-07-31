using System.IO;
using SUNWODA_SEVB.Tool.Helper;

var inputDir = args.Length > 0
    ? args[0]
    : @"C:\AutoSlice\cutpicture\jietu\2026-07-22";
var debugDir = args.Length > 1
    ? args[1]
    : Path.Combine(inputDir, "_probe");

Directory.CreateDirectory(debugDir);
var files = Directory.GetFiles(inputDir, "vision_3d_*.png")
    .Where(f => !Path.GetFileName(f).Contains("_points", StringComparison.OrdinalIgnoreCase)
                && !Path.GetFileName(f).Contains("_mask", StringComparison.OrdinalIgnoreCase)
                && !Path.GetFileName(f).Contains("_plate", StringComparison.OrdinalIgnoreCase))
    .OrderBy(f => f)
    .ToArray();

if (files.Length == 0)
{
    Console.WriteLine($"æœªæ‰¾åˆ° vision_3d_*.pngï¼š{inputDir}");
    return 1;
}

Console.WriteLine($"è¾“å…¥ç›®å½•ï¼š{inputDir}");
Console.WriteLine($"è°ƒè¯•å›¾ï¼š{debugDir}");
Console.WriteLine($"å…± {files.Length} å¼ ");

var ok = 0;
foreach (var file in files)
{
    try
    {
        var result = Vision3dSectionPointsDetector.Detect(file, debugDir);
        Console.WriteLine($"[OK] {Path.GetFileName(file)}  {result.Summary}");
        ok++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[FAIL] {Path.GetFileName(file)}  {ex.Message}");
    }
}

Console.WriteLine($"å®Œæˆï¼š{ok}/{files.Length}");
return ok == files.Length ? 0 : 2;




