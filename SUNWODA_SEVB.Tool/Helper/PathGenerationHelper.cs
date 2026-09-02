namespace SUNWODA_SEVB.Tool.Helper
{
    public static class PathGenerationHelper
    {
        public static List<(short x, short y)> StraightLinePath(short startX, short startY, short endX, short endY)
        {
            var k = (endY - startY) * 1.0 / (endX - startX);
            var b = startY - k * startX;
            var genPathList = new List<(short x, short y)>();

            for (var i = 0; i < Math.Abs(endX - startX); i++)
            {
                var x = i * 1.0 / (Math.Abs(endX - startX) - 1) * (endX - startX) + startX;
                var y = k * x + b;
                genPathList.Add(((short)x, (short)y));
            }

            return genPathList;
        }

        public static List<(short x, short y)> CirclePath(short centerX, short centerY, short r, short startDegress, short endDegress)
        {
            var genPathList = new List<(short x, short y)>();
            if (endDegress >= startDegress)
            {
                for (var i = startDegress; i <= endDegress; i++)
                {
                    var x = r * Math.Cos((i * 1.0 / 360) * 2 * Math.PI);
                    var y = r * Math.Sin((i * 1.0 / 360) * 2 * Math.PI);
                    genPathList.Add(((short)(centerX + x), (short)(centerY - y)));
                }
            }
            else
            {
                for (var i = startDegress; i >= endDegress; i--)
                {
                    var x = r * Math.Cos((i * 1.0 / 360) * 2 * Math.PI);
                    var y = r * Math.Sin((i * 1.0 / 360) * 2 * Math.PI);
                    genPathList.Add(((short)(centerX + x), (short)(centerY - y)));
                }
            }

            return genPathList;
        }

        public static List<(short x, short y)> CirclePath(short centerX, short centerY, short r, double startDegress, double endDegress)
        {
            var genPathList = new List<(short x, short y)>();
            var amplifyStartDeg = Math.Round(startDegress * 10);
            var amplifyEndDeg = Math.Round(endDegress * 10);
            if (endDegress >= startDegress)
            {
                for (var i = amplifyStartDeg; i <= amplifyEndDeg; i++)
                {
                    var x = r * Math.Cos((i / 10 / 360) * 2 * Math.PI);
                    var y = r * Math.Sin((i / 10 / 360) * 2 * Math.PI);
                    genPathList.Add(((short)(centerX + x), (short)(centerY - y)));
                }
            }
            else
            {
                for (var i = amplifyStartDeg; i >= amplifyEndDeg; i--)
                {
                    var x = r * Math.Cos((i / 10 / 360) * 2 * Math.PI);
                    var y = r * Math.Sin((i / 10 / 360) * 2 * Math.PI);
                    genPathList.Add(((short)(centerX + x), (short)(centerY - y)));
                }
            }

            return genPathList;
        }
    }
}
