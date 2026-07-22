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
    .OrderBy(f => f)
    .ToArray();

if (files.Length == 0)
{
    Console.WriteLine($"未找到 vision_3d_*.png：{inputDir}");
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
        var result = Vision3dSectionPointsDetector.Detect(file, debugDir);
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
