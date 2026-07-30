using System.Drawing;
using System.Drawing.Imaging;

static int Gray(Color c) => (c.R + c.G + c.B) / 3;

static Bitmap Load(string path)
{
    using var fs = File.OpenRead(path);
    using var tmp = new Bitmap(fs);
    return new Bitmap(tmp);
}

static Bitmap Rot90Cw(Bitmap src)
{
    var dst = new Bitmap(src.Height, src.Width);
    using var g = Graphics.FromImage(dst);
    g.TranslateTransform(src.Height, 0);
    g.RotateTransform(90);
    g.DrawImage(src, 0, 0);
    return dst;
}

static void Analyze(string tag, Bitmap bmp)
{
    int rw = Math.Min(1844, bmp.Width);
    int rh = Math.Min(1500, bmp.Height);
    int step = Math.Max(1, Math.Min(rw, rh) / 80);
    var vals = new List<double>();
    int ge200 = 0, ge150 = 0, ge120 = 0, ge60 = 0;
    double sum = 0;
    for (int y = 0; y < rh; y += step)
    for (int x = 0; x < rw; x += step)
    {
        double g = Gray(bmp.GetPixel(x, y));
        vals.Add(g); sum += g;
        if (g >= 200) ge200++;
        if (g >= 150) ge150++;
        if (g >= 120) ge120++;
        if (g >= 60) ge60++;
    }
    vals.Sort();
    int n = vals.Count;
    double Pct(int c) => 100.0 * c / n;
    double P(double p) => vals[(int)Math.Floor((n - 1) * p)];
    double mean = sum / n;

    double[] thirds = new double[3];
    int tw = rw / 3;
    for (int t = 0; t < 3; t++)
    {
        double s = 0; int c = 0;
        for (int y = 0; y < rh; y += step)
        for (int x = t * tw; x < Math.Min((t + 1) * tw, rw); x += step)
        { s += Gray(bmp.GetPixel(x, y)); c++; }
        thirds[t] = Math.Round(s / c, 1);
    }

    double[] bands = new double[3];
    int th = rh / 3;
    for (int t = 0; t < 3; t++)
    {
        double s = 0; int c = 0;
        for (int y = t * th; y < Math.Min((t + 1) * th, rh); y += step)
        for (int x = 0; x < rw; x += step)
        { s += Gray(bmp.GetPixel(x, y)); c++; }
        bands[t] = Math.Round(s / c, 1);
    }

    int gw = (rw + step - 1) / step, gh = (rh + step - 1) / step;
    var mask = new bool[gh, gw];
    double thr = Math.Max(110, mean + 15);
    for (int yi = 0, y = 0; y < rh; y += step, yi++)
    for (int xi = 0, x = 0; x < rw; x += step, xi++)
        mask[yi, xi] = Gray(bmp.GetPixel(x, y)) >= thr;

    var visited = new bool[gh, gw];
    var areas = new List<(int area, int minX, int maxX, int minY, int maxY)>();
    int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
    for (int y = 0; y < gh; y++)
    for (int x = 0; x < gw; x++)
    {
        if (!mask[y, x] || visited[y, x]) continue;
        var q = new Queue<(int x, int y)>();
        q.Enqueue((x, y)); visited[y, x] = true;
        int area = 0, minX = x, maxX = x, minY = y, maxY = y;
        while (q.Count > 0)
        {
            var (cx, cy) = q.Dequeue();
            area++;
            minX = Math.Min(minX, cx); maxX = Math.Max(maxX, cx);
            minY = Math.Min(minY, cy); maxY = Math.Max(maxY, cy);
            for (int k = 0; k < 4; k++)
            {
                int nx = cx + dx[k], ny = cy + dy[k];
                if (nx < 0 || ny < 0 || nx >= gw || ny >= gh) continue;
                if (!mask[ny, nx] || visited[ny, nx]) continue;
                visited[ny, nx] = true;
                q.Enqueue((nx, ny));
            }
        }
        if (area >= 20) areas.Add((area, minX * step, maxX * step, minY * step, maxY * step));
    }
    areas = areas.OrderByDescending(a => a.area).Take(4).ToList();

    Console.WriteLine("==== " + tag + " ====");
    Console.WriteLine($"src={bmp.Width}x{bmp.Height} crop={rw}x{rh} step={step}");
    Console.WriteLine($"mean={mean:F2} P5={P(0.05):F0} P50={P(0.5):F0} P95={P(0.95):F0}");
    Console.WriteLine($"pct>=200={Pct(ge200):F2}% >=150={Pct(ge150):F2}% >=120={Pct(ge120):F2}% >=60={Pct(ge60):F2}%");
    Console.WriteLine($"L/M/R mean={thirds[0]}/{thirds[1]}/{thirds[2]}  Top/Mid/Bot={bands[0]}/{bands[1]}/{bands[2]}");
    Console.WriteLine($"blobThr={thr:F1} blobs={areas.Count}: " + string.Join(" | ", areas.Select(a =>
        $"a={a.area} bbox=({a.minX},{a.minY})-({a.maxX},{a.maxY})")));
    Console.WriteLine();
}

var files = new[]
{
    @"C:\AutoSlice\output\test\1.jpg",
    @"C:\AutoSlice\output\test\2.jpg",
    @"C:\AutoSlice\output\test\30.jpg",
    @"C:\AutoSlice\output\test\123.png",
};

foreach (var f in files)
{
    if (!File.Exists(f)) { Console.WriteLine("MISSING " + f); continue; }
    var name = Path.GetFileName(f);
    using var src = Load(f);
    Analyze(name + "|raw", src);
    using var rot = Rot90Cw(src);
    Analyze(name + "|cw90", rot);
}
