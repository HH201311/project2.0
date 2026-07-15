using System.Drawing;
using System.Drawing.Imaging;

namespace SUNWODA_SEVB.Tool.Helper
{
    public static class ScreenCaptureHelper
    {
        public static Bitmap CaptureRegion(Rectangle region)
        {
            var bmp = new Bitmap(region.Width, region.Height);
            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(region.Location, System.Drawing.Point.Empty, region.Size);
            return bmp;
        }

        public static void CaptureRegionAndSave(string filePath, int x, int y, int width, int height)
        {
            using var bmp = CaptureRegion(new Rectangle(x, y, width, height));
            bmp.Save(filePath, ImageFormat.Png);
        }
    }
}
