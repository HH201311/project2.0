using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace SUNWODA_SEVB.Tool.Helper.CCTBHelper
{
    public static class GraphicHelper
    {
        /// <summary>
        /// 坐标转换辅助方法：将数值转换为屏幕 Y 坐标
        /// </summary>
        /// <param name="yValue"></param>
        /// <param name="yMin"></param>
        /// <param name="yRange"></param>
        /// <param name="length"></param>
        /// <param name="isReverse"></param>
        /// <returns></returns>
        public static double Normalize(double yValue, double yMin, double yRange, double length, bool isReverse = false)
        {
            // 数值越大，Y坐标越小（因为屏幕坐标系原点在左上角）
            if (isReverse) return (yValue - yMin) / yRange * length;
            else return length - ((yValue - yMin) / yRange * length);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static StreamGeometry CreatStreamGeometry(List<Point> points)
        {
            return CreatStreamGeometry(points.ToArray());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static StreamGeometry CreatStreamGeometry(params Point[] points)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                // 移动到起点
                ctx.BeginFigure(points[0], true, false);
                for (var i = 1; i < points.Length; i++)
                {
                    ctx.LineTo(points[i], true, true);
                }
            }
            geometry.Freeze();
            return geometry;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pointLB"></param>
        /// <param name="pointLT"></param>
        /// <param name="pointRT"></param>
        /// <param name="pointRB"></param>
        /// <returns></returns>
        public static StreamGeometry CreatStreamGeometryWithCorner(Point pointLB, Point pointLT, Point pointRT, Point pointRB)
        {
            var r = 5.0; // 圆角半径
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                // 起点 左下角
                ctx.BeginFigure(pointLB, true, true);

                // 左上圆角
                var arcStartPointLT = new Point(pointLT.X, pointLT.Y + r);
                var arcEndPointLT = new Point(pointLT.X + r, pointLT.Y);
                ctx.LineTo(arcStartPointLT, true, true);
                ctx.ArcTo(arcEndPointLT, new Size(r, r), 0, false, SweepDirection.Clockwise, true, true);

                // 右上圆角
                var arcStartPointRT = new Point(pointRT.X - r, pointRT.Y);
                var arcEndPointRT = new Point(pointRT.X, pointRT.Y + r);
                ctx.LineTo(arcStartPointRT, true, true);
                ctx.ArcTo(arcEndPointRT, new Size(r, r), 0, false, SweepDirection.Clockwise, true, true);

                // 右下角
                ctx.LineTo(pointRB, true, true);
                // 终点 左下角
                ctx.LineTo(pointLB, true, true);
            }
            geometry.Freeze();
            return geometry;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pointLB"></param>
        /// <param name="pointLT"></param>
        /// <param name="pointRT"></param>
        /// <param name="pointRB"></param>
        /// <param name="isReverse"></param>
        /// <returns></returns>
        public static StreamGeometry CreatUpStreamGeometryWithCorner(Point pointLB, Point pointLT, Point pointRT, Point pointRB, bool isReverse = false)
        {
            var r = 5.0; // 圆角半径
            var geometry = new StreamGeometry();
            var sweepDirection = isReverse ? SweepDirection.Counterclockwise : SweepDirection.Clockwise;
            using (var ctx = geometry.Open())
            {
                // 起点 左下角
                ctx.BeginFigure(pointLB, true, true);

                // 左上圆角
                var r1 = r > Math.Abs(pointLB.Y - pointLT.Y) ? 0 : r;
                var arcStartPointLT_Y = isReverse ? pointLT.Y - r1 : pointLT.Y + r1;
                var arcStartPointLT = new Point(pointLT.X, arcStartPointLT_Y);
                var arcEndPointLT = new Point(pointLT.X + r1, pointLT.Y);
                ctx.LineTo(arcStartPointLT, true, true);
                ctx.ArcTo(arcEndPointLT, new Size(r1, r1), 0, false, sweepDirection, true, true);

                // 右上圆角
                var r2 = r > Math.Abs(pointRB.Y - pointRT.Y) ? 0 : r;
                var arcStartPointRT_Y = isReverse ? pointLT.Y - r2 : pointRT.Y + r2;
                var arcStartPointRT = new Point(pointRT.X - r2, pointRT.Y);
                var arcEndPointRT = new Point(pointRT.X, arcStartPointRT_Y);
                ctx.LineTo(arcStartPointRT, true, true);
                ctx.ArcTo(arcEndPointRT, new Size(r2, r2), 0, false, sweepDirection, true, true);

                // 右下角
                ctx.LineTo(pointRB, true, true);
                // 终点 左下角
                ctx.LineTo(pointLB, true, true);
            }
            geometry.Freeze();
            return geometry;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pointLB"></param>
        /// <param name="pointLT"></param>
        /// <param name="pointRT"></param>
        /// <param name="pointRB"></param>
        /// <param name="isReverse"></param>
        /// <returns></returns>
        public static StreamGeometry CreatDownStreamGeometryWithCorner(Point pointLB, Point pointLT, Point pointRT, Point pointRB, bool isReverse = false)
        {
            var r = 5.0; // 圆角半径
            var geometry = new StreamGeometry();
            var sweepDirection = isReverse ? SweepDirection.Counterclockwise : SweepDirection.Clockwise;
            using (var ctx = geometry.Open())
            {
                // 起点 左上角
                ctx.BeginFigure(pointLT, true, true);

                // 右上角
                ctx.BeginFigure(pointRT, true, true);

                // 右下圆角
                var r1 = r > Math.Abs(pointRT.Y - pointRB.Y) ? 0 : r;
                var arcStartPointRB_Y = isReverse ? pointRB.Y + r1 : pointRB.Y - r1;
                var arcStartPointRB = new Point(pointRB.X, arcStartPointRB_Y);
                var arcEndPointtRB = new Point(pointRB.X - r1, pointRB.Y);
                ctx.LineTo(arcStartPointRB, true, true);
                ctx.ArcTo(arcEndPointtRB, new Size(r1, r1), 0, false, sweepDirection, true, true);

                // 左下圆角
                var r2 = r > Math.Abs(pointLT.Y - pointLB.Y) ? 0 : r;
                var arcStartPointLB_Y = isReverse ? pointLB.Y + r : pointLB.Y - r;
                var arcStartPointLB = new Point(pointLB.X + r2, pointLB.Y);
                var arcEndPointLB = new Point(pointLB.X, arcStartPointLB_Y);
                ctx.LineTo(arcStartPointLB, true, true);
                ctx.ArcTo(arcEndPointLB, new Size(r2, r2), 0, false, sweepDirection, true, true);

                // 终点 左上角
                ctx.LineTo(pointLT, true, true);
            }
            geometry.Freeze();
            return geometry;
        }
    }
}
