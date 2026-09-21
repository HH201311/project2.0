using System.Reflection;
using System.Runtime.InteropServices;

var baseDir = args.Length > 0 ? args[0] : AppContext.BaseDirectory;
var cudaDir = Path.Combine(baseDir, "Dlls", "CUDA12");
var nativeDir = Path.Combine(baseDir, "runtimes", "win-x64", "native");
var njctDir = Path.Combine(baseDir, "Dlls", "NCLCCT");
var modelPath = Path.Combine(baseDir, "model", "NCLCCT", "model_hw.onnx");

AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
{
    var simple = new AssemblyName(e.Name).Name;
    if (string.IsNullOrEmpty(simple))
        return null;
    foreach (var dir in new[] { baseDir, njctDir, Path.Combine(baseDir, "Dlls") })
    {
        var candidate = Path.Combine(dir, simple + ".dll");
        if (File.Exists(candidate))
            return Assembly.LoadFrom(candidate);
    }
    return null;
};

Console.WriteLine($"base={baseDir}");
Console.WriteLine($"cudaExists={Directory.Exists(cudaDir)} nativeExists={Directory.Exists(nativeDir)}");
Console.WriteLine($"modelExists={File.Exists(modelPath)}");

var path = Environment.GetEnvironmentVariable("PATH") ?? "";
Environment.SetEnvironmentVariable("PATH", $"{cudaDir};{nativeDir};{njctDir};{path}");
try
{
    Native.SetDefaultDllDirectories(0x00001000);
    if (Directory.Exists(cudaDir)) Native.AddDllDirectory(cudaDir);
    if (Directory.Exists(nativeDir)) Native.AddDllDirectory(nativeDir);
    if (Directory.Exists(njctDir)) Native.AddDllDirectory(njctDir);
}
catch (Exception ex)
{
    Console.WriteLine($"AddDllDirectory warn: {ex.Message}");
}

var cfPath = Path.Combine(njctDir, "CF.dll");
var njctPath = Path.Combine(njctDir, "NJCT.dll");
Console.WriteLine($"Load CF {cfPath}");
_ = Assembly.LoadFrom(cfPath);
Console.WriteLine($"Load NJCT {njctPath}");
var njctAsm = Assembly.LoadFrom(njctPath);
var type = njctAsm.GetType("NJCT.NChwSwdCT") ?? throw new Exception("type missing");
var inst = Activator.CreateInstance(type)!;
var init = type.GetMethod("ModelInit")!;
object? handle = null;
string err = "";
var pars = new object?[] { modelPath, handle, err, true };
Console.WriteLine("Invoke ModelInit CUDA=true ...");
try
{
    init.Invoke(inst, pars);
    handle = pars[1];
    err = pars[2] as string ?? "";
    Console.WriteLine($"handleNull={handle is null}");
    Console.WriteLine($"err={err}");
}
catch (Exception ex)
{
    Console.WriteLine($"EX: {ex}");
}

internal static class Native
{
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool SetDllDirectory(string? lpPathName);

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr AddDllDirectory(string NewDirectory);

    [DllImport("kernel32", SetLastError = true)]
    public static extern bool SetDefaultDllDirectories(uint DirectoryFlags);
}
