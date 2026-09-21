using System.Reflection;
using System.Runtime.InteropServices;
using HalconDotNet;

var sevb = @"E:\WorkSpace\SWD\NCLCCT\sunwoda2.0.0.1\SUNWODA_SEVB\bin\Debug\net8.0-windows";
var dlls = Path.Combine(sevb, "Dlls");
var njctDir = Path.Combine(dlls, "NCLCCT");
var cuda = Path.Combine(dlls, "CUDA12");
var model = Path.Combine(sevb, "model", "NCLCCT", "model_hw.onnx");
var img = @"E:\WorkSpace\SWD\NCLCCT\视觉提供信息\右A测试图片\manualA20260911101229_1-R_origin.png";
Environment.SetEnvironmentVariable(
    "PATH",
    dlls + ";" + cuda + ";" + njctDir + ";" + (Environment.GetEnvironmentVariable("PATH") ?? "")
);
Environment.SetEnvironmentVariable("HALCON_LICENSE_FILE", Path.Combine(dlls, "license.dat"));
Native.SetDefaultDllDirectories(0x1000);
foreach (var d in new[] { dlls, cuda, njctDir })
    if (Directory.Exists(d))
        Native.AddDllDirectory(d);

AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
{
    var n = new AssemblyName(e.Name).Name;
    foreach (var d in new[] { njctDir, dlls })
    {
        var c = Path.Combine(d, n + ".dll");
        if (File.Exists(c))
            return Assembly.LoadFrom(c);
    }
    return null;
};

Assembly.LoadFrom(Path.Combine(njctDir, "CF.dll"));
var t = Assembly.LoadFrom(Path.Combine(njctDir, "NJCT.dll")).GetType("NJCT.NChwSwdCT")!;
var inst = Activator.CreateInstance(t)!;
object? h = null;
string err = "";
var ip = new object?[] { model, h, err, true };
t.GetMethod("ModelInit")!.Invoke(inst, ip);
h = ip[1];
Console.WriteLine("handleNull=" + (h is null));

var resultImg = new HObject();
var regions = new List<HObject>();
var rows = new List<HTuple>();
var cols = new List<HTuple>();
var gaps = new List<HTuple>();
var prints = new List<HTuple>();
string run = "";
var p = new object[] { img, h!, 400, resultImg, regions, rows, cols, gaps, prints, run };
t.GetMethod("ModelAction")!.Invoke(inst, p);
run = (string)p[9];
regions = (List<HObject>)p[4];
rows = (List<HTuple>)p[5];
cols = (List<HTuple>)p[6];
gaps = (List<HTuple>)p[7];
prints = (List<HTuple>)p[8];
Console.WriteLine("RunInfo='" + run + "'");

void Dump(string name, List<HTuple> list)
{
    Console.WriteLine(name + " count=" + list.Count);
    for (int i = 0; i < list.Count; i++)
    {
        var t2 = list[i];
        if (t2 == null)
        {
            Console.WriteLine("  [" + i + "]=null");
            continue;
        }
        var sb = new System.Text.StringBuilder();
        sb.Append("  [" + i + "] len=" + t2.Length + " vals=");
        for (int j = 0; j < t2.Length; j++)
            sb.Append(t2[j].D.ToString("G6") + (j + 1 < t2.Length ? "," : ""));
        Console.WriteLine(sb.ToString());
    }
}

Dump("AllRegionRow", rows);
Dump("AllRegionCol", cols);
Dump("AllJianXiValue", gaps);
Dump("AllPrintfValue", prints);
Console.WriteLine("regions=" + regions.Count);
if (rows.Count > 0 && cols.Count > 0)
{
    var r = rows[0];
    var c = cols[0];
    var n = Math.Min(r.Length, c.Length);
    Console.WriteLine("pair count=" + n);
    for (int i = 0; i < n; i++)
        Console.WriteLine($"  pt{i}=({r[i].D:F2},{c[i].D:F2})");
}

internal static class Native
{
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr AddDllDirectory(string p);

    [DllImport("kernel32", SetLastError = true)]
    public static extern bool SetDefaultDllDirectories(uint f);
}
