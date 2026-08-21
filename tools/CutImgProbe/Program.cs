using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using HalconDotNet;

var dllsDir = @"C:\AutoSlice\11\sunwoda2.0.0.1\SUNWODA_SEVB\bin\Debug\net8.0-windows\Dlls";
if (!Directory.Exists(dllsDir) || !File.Exists(Path.Combine(dllsDir, "CTAutoCutImg.dll")))
{
    dllsDir = @"C:\AutoSlice\11\sunwoda2.0.0.1\SUNWODA_SEVB.Core\Dlls";
}

Console.WriteLine($"DllsDir={dllsDir}");
NativeMethods.SetDllDirectory(dllsDir);
Environment.SetEnvironmentVariable(
    "PATH",
    dllsDir + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"));

var cutImgPath = Path.Combine(dllsDir, "CTAutoCutImg.dll");
Console.WriteLine($"Load {cutImgPath} exists={File.Exists(cutImgPath)}");

var alc = new AssemblyLoadContext("cut", isCollectible: true);
alc.Resolving += (_, name) =>
{
    var candidate = Path.Combine(dllsDir, name.Name + ".dll");
    if (File.Exists(candidate))
    {
        Console.WriteLine($"Resolving {name.Name} -> {candidate}");
        return alc.LoadFromAssemblyPath(candidate);
    }

    return null;
};

var asm = alc.LoadFromAssemblyPath(cutImgPath);
foreach (var t in asm.GetTypes())
{
    Console.WriteLine($"TYPE {t.FullName}");
    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
    {
        Console.WriteLine($"  METHOD {m}");
        foreach (var p in m.GetParameters())
        {
            Console.WriteLine(
                $"    PARAM {p.ParameterType.FullName} {p.Name} IsByRef={p.ParameterType.IsByRef} IsOut={p.IsOut}");
        }
    }
}

var type = asm.GetType("CTAutoCutImg.CT") ?? asm.GetType("CT")
    ?? throw new Exception("CT type not found");
var inst = Activator.CreateInstance(type)!;
var m1 = type.GetMethod("GetSectionReferencePoints")!;
var m2 = type.GetMethod("GetObjectTopBottomPoints")!;

string? FindLatest(string root, string pattern)
{
    if (!Directory.Exists(root))
    {
        return null;
    }

    return Directory.GetFiles(root, pattern, SearchOption.AllDirectories)
        .OrderByDescending(File.GetLastWriteTime)
        .FirstOrDefault();
}

var top = args.ElementAtOrDefault(0)
    ?? FindLatest(@"C:\AutoSlice\cutpicture\2Dxianglaing", "vision_top_*.png");
var threeD = args.ElementAtOrDefault(1)
    ?? FindLatest(@"C:\AutoSlice\cutpicture\jietu", "vision_3d_*.png");

Console.WriteLine($"top={top}");
Console.WriteLine($"3d={threeD}");

TryInvoke("GetObjectTopBottomPoints on TOP", m2, inst, top);
TryInvoke("GetObjectTopBottomPoints on 3D", m2, inst, threeD);
TryInvoke("GetSectionReferencePoints on 3D", m1, inst, threeD);
TryInvoke("GetSectionReferencePoints on TOP", m1, inst, top);

static void TryInvoke(string label, MethodInfo method, object inst, string? imagePath)
{
    Console.WriteLine($"---- {label} ----");
    if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
    {
        Console.WriteLine("skip: image missing");
        return;
    }

    HObject? image = null;
    try
    {
        HOperatorSet.ReadImage(out image, imagePath);
        object?[] parameters = [image, null];
        method.Invoke(inst, parameters);
        var pts = parameters[1];
        Console.WriteLine($"OK points type={pts?.GetType().FullName}");
        if (pts is Array arr)
        {
            for (var i = 0; i < arr.Length; i++)
            {
                var item = arr.GetValue(i)!;
                var x = item.GetType().GetProperty("X")!.GetValue(item);
                var y = item.GetType().GetProperty("Y")!.GetValue(item);
                Console.WriteLine($"  [{i}] ({x},{y})");
            }
        }
    }
    catch (TargetInvocationException ex)
    {
        Console.WriteLine($"TargetInvocationException Inner: {ex.InnerException}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Exception: {ex}");
    }
    finally
    {
        image?.Dispose();
    }
}

internal static class NativeMethods
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool SetDllDirectory(string lpPathName);
}
