using System.IO;
using SUNWODA_SEVB.Tool.Helper;

var inputDir = args.Length > 0
    ? args[0]
    : @"C:\AutoSlice\cutpicture\2Dxianglaing\_samples";
var debugDir = args.Length > 1
    ? args[1]
    : Path.Combine(inputDir, "_probe");

Directory.CreateDirectory(debugDir);
var files = Directory.GetFiles(inputDir, "*.png")
    .Where(f =>
    {
        var name = Path.GetFileName(f);
        return !name.Contains("_vector_", StringComparison.OrdinalIgnoreCase)
               && !name.Contains("_points", StringComparison.OrdinalIgnoreCase)
               && !name.Contains("_mask", StringComparison.OrdinalIgnoreCase)
               && !name.Contains("_gray", StringComparison.OrdinalIgnoreCase);
    })
    .OrderBy(f => f)
    .ToArray();

if (files.Length == 0)
{
    Console.WriteLine($"未找到样图：{inputDir}");
    return 1;
}

Console.WriteLine($"输入目录：{inputDir}");
Console.WriteLine($"调试图：{debugDir}");
Console.WriteLine($"共 {files.Length} 张");

var ok = 0;
foreach (var file in files)
{
    try
    {
        var result = VisionTopViewVectorPointsDetector.Detect(file, debugDir);
        Console.WriteLine($"[OK] {Path.GetFileName(file)}  {result.Summary}");
        ok++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[FAIL] {Path.GetFileName(file)}  {ex.Message}");
    }
}

Console.WriteLine($"完成：{ok}/{files.Length}");
return ok == files.Length ? 0 : 2;
