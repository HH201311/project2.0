using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using HalconDotNet;

/// 南昌华为 NChwSwdCT 单图探针：定位「当前图像暂不支持检测」真实原因。

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
        var candidate = Path.Combine(dir.FullName, "SUNWODA_SEVB.Core", "Dlls");
        if (Directory.Exists(candidate))
            return dir.FullName;
        dir = dir.Parent;
    }
    return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}

var repo = FindRepoRoot();
var sevbOut = Path.Combine(repo, "SUNWODA_SEVB", "bin", "Debug", "net8.0-windows");
var baseDir = Directory.Exists(Path.Combine(sevbOut, "Dlls", "NCLCCT"))
    ? sevbOut
    : Path.Combine(repo, "SUNWODA_SEVB.Core");

var dllsDir = Path.Combine(baseDir, "Dlls");
var cudaDir = Path.Combine(dllsDir, "CUDA12");
var njctDir = Path.Combine(dllsDir, "NCLCCT");
var nativeDir = Path.Combine(baseDir, "runtimes", "win-x64", "native");
var modelPath = Path.Combine(
    Directory.Exists(Path.Combine(sevbOut, "model", "NCLCCT"))
        ? sevbOut
        : Path.Combine(repo, "SUNWODA_SEVB"),
    "model",
    "NCLCCT",
    "model_hw.onnx"
);

var defaultImg =
    @"E:\WorkSpace\SWD\NCLCCT\视觉提供信息\右A测试图片\manualA20260911101229_1-R_origin.png";
var imgPath = args.Length > 0 ? args[0] : defaultImg;

Console.WriteLine("==== NCLCCT NChwSwdCT Action Probe ====");
Console.WriteLine($"repo={repo}");
Console.WriteLine($"baseDir={baseDir}");
Console.WriteLine($"img={imgPath}");
Console.WriteLine($"imgExists={File.Exists(imgPath)}");
Console.WriteLine($"modelExists={File.Exists(modelPath)} model={modelPath}");

AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
{
    var simple = new AssemblyName(e.Name).Name;
    if (string.IsNullOrEmpty(simple))
        return null;
    foreach (var dir in new[] { baseDir, njctDir, dllsDir, AppContext.BaseDirectory })
    {
        var candidate = Path.Combine(dir, simple + ".dll");
        if (File.Exists(candidate))
            return Assembly.LoadFrom(candidate);
    }
    return null;
};

var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
Environment.SetEnvironmentVariable(
    "PATH",
    $"{dllsDir};{cudaDir};{njctDir};{nativeDir};{pathEnv}"
);

var license = File.Exists(Path.Combine(dllsDir, "license.dat"))
    ? Path.Combine(dllsDir, "license.dat")
    : Path.Combine(dllsDir, "license", "license.dat");
if (File.Exists(license))
{
    Environment.SetEnvironmentVariable("HALCON_LICENSE_FILE", license);
    Console.WriteLine($"HALCON_LICENSE_FILE={license}");
}

try
{
    Native.SetDefaultDllDirectories(0x00001000);
    foreach (var d in new[] { dllsDir, cudaDir, njctDir, nativeDir })
    {
        if (Directory.Exists(d))
            Native.AddDllDirectory(d);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"AddDllDirectory warn: {ex.Message}");
}

Console.WriteLine();
Console.WriteLine("---- Precheck D:\\TEST.png (NJCT 内部硬编码) ----");
Console.WriteLine($"D:\\ exists={Directory.Exists(@"D:\")}");
try
{
    File.WriteAllText(@"D:\TEST.png", "probe");
    Console.WriteLine("D:\\TEST.png WRITE: OK");
    File.Delete(@"D:\TEST.png");
}
catch (Exception ex)
{
    Console.WriteLine($"D:\\TEST.png WRITE: FAIL -> {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine(
        ">>> 根因候选：DLL 内部写 D:\\TEST.png 被拒绝后 catch 成「当前图像暂不支持检测」。"
    );
}

if (!File.Exists(imgPath))
{
    Console.WriteLine("图片不存在，退出");
    return 2;
}

HOperatorSet.ReadImage(out HObject src, imgPath);
HOperatorSet.GetImageSize(src, out HTuple w, out HTuple h);
Console.WriteLine($"Halcon ReadImage OK size={w.I}x{h.I}");
src.Dispose();

_ = Assembly.LoadFrom(Path.Combine(njctDir, "CF.dll"));
var njctAsm = Assembly.LoadFrom(Path.Combine(njctDir, "NJCT.dll"));
var type =
    njctAsm.GetType("NJCT.NChwSwdCT") ?? throw new Exception("NJCT.NChwSwdCT missing");
var inst = Activator.CreateInstance(type)!;
var init = type.GetMethod("ModelInit")!;
var action = type.GetMethod("ModelAction")!;
Console.WriteLine($"ModelAction params={action.GetParameters().Length}");

object? handle = null;
string err = "";
var initPars = new object?[] { modelPath, handle, err, true };
var swInit = Stopwatch.StartNew();
init.Invoke(inst, initPars);
swInit.Stop();
handle = initPars[1];
err = initPars[2] as string ?? "";
Console.WriteLine(
    $"ModelInit {swInit.Elapsed.TotalSeconds:F2}s handleNull={handle is null} err='{err}'"
);
if (handle is null)
{
    Console.WriteLine("ModelInit 失败，无法继续 ModelAction");
    return 3;
}

int[] blackHeights =
    args.Length > 1 ? new[] { int.Parse(args[1]) } : new[] { 400 };

foreach (var bh in blackHeights)
{
    Console.WriteLine();
    Console.WriteLine($"---- ModelAction BlackHeight={bh} ----");
    var resultImg = new HObject();
    var regions = new List<HObject>();
    var rows = new List<HTuple>();
    var cols = new List<HTuple>();
    var gaps = new List<HTuple>();
    var prints = new List<HTuple>();
    string runInfo = "";
    var pars = new object[]
    {
        imgPath,
        handle!,
        bh,
        resultImg,
        regions,
        rows,
        cols,
        gaps,
        prints,
        runInfo,
    };

    var sw = Stopwatch.StartNew();
    try
    {
        action.Invoke(inst, pars);
        sw.Stop();
        runInfo = pars[9] as string ?? "";
        resultImg = (HObject)pars[3];
        regions = (List<HObject>)pars[4];
        rows = (List<HTuple>)pars[5];
        cols = (List<HTuple>)pars[6];

        var resultOk = resultImg != null && resultImg.IsInitialized();
        Console.WriteLine($"elapsed={sw.Elapsed.TotalSeconds:F2}s");
        Console.WriteLine($"RunInfo='{runInfo}'");
        Console.WriteLine($"ResultImgInitialized={resultOk}");
        regions = (List<HObject>)pars[4];
        rows = (List<HTuple>)pars[5];
        cols = (List<HTuple>)pars[6];
        gaps = (List<HTuple>)pars[7];
        prints = (List<HTuple>)pars[8];
        Console.WriteLine(
            $"regions={regions?.Count ?? -1} rows={rows?.Count ?? -1} cols={cols?.Count ?? -1}"
        );
        DumpTupleList("AllRegionRow", rows);
        DumpTupleList("AllRegionCol", cols);
        DumpTupleList("AllJianXiValue", gaps);
        DumpTupleList("AllPrintfValue", prints);
        if (rows is { Count: > 0 } && cols is { Count: > 0 })
        {
            var r = rows[0];
            var c = cols[0];
            var n = Math.Min(r.Length, c.Length);
            Console.WriteLine($"points[0] count={n}");
            for (int i = 0; i < n; i++)
                Console.WriteLine($"  pt{i}= row={r[i].D:F3} col={c[i].D:F3}");
        }

        if (File.Exists(@"D:\TEST.png"))
        {
            var fi = new FileInfo(@"D:\TEST.png");
            Console.WriteLine($"D:\\TEST.png exists after call, size={fi.Length}");
        }
        else
        {
            Console.WriteLine("D:\\TEST.png still missing after call");
        }

        if (runInfo.Contains("暂不支持") || runInfo.Contains("非自动切图"))
        {
            Console.WriteLine("ANALYSIS:");
            Console.WriteLine(
                "  - DLL 含「非自动切图分辨率」与兜底「当前图像暂不支持检测」"
            );
            Console.WriteLine("  - IL 中存在 1440/1000 常量，该分辨率本身在支持列表");
            Console.WriteLine("  - 优先检查 D:\\TEST.png 是否可写");
        }
    }
    catch (TargetInvocationException ex)
    {
        sw.Stop();
        Console.WriteLine($"elapsed={sw.Elapsed.TotalSeconds:F2}s INVOKE EX");
        Console.WriteLine(ex.InnerException?.ToString() ?? ex.ToString());
    }
    finally
    {
        try
        {
            resultImg?.Dispose();
        }
        catch
        {
            // ignore
        }
    }
}

Console.WriteLine();
Console.WriteLine("==== done ====");
return 0;

static void DumpTupleList(string name, List<HTuple>? list)
{
    Console.WriteLine($"{name} count={list?.Count ?? -1}");
    if (list == null)
        return;
    for (int i = 0; i < list.Count; i++)
    {
        var t = list[i];
        if (t == null)
        {
            Console.WriteLine($"  [{i}]=null");
            continue;
        }
        var vals = string.Join(",", Enumerable.Range(0, t.Length).Select(j => t[j].D.ToString("G6")));
        Console.WriteLine($"  [{i}] len={t.Length} vals={vals}");
    }
}

internal static class Native
{
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr AddDllDirectory(string NewDirectory);

    [DllImport("kernel32", SetLastError = true)]
    public static extern bool SetDefaultDllDirectories(uint DirectoryFlags);
}
