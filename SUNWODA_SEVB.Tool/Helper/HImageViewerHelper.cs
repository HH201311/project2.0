extern alias halcondotnet;
using halcondotnet::HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Tool.Helper
{
    public class HImageViewerHelper
    {
        /// <summary>
        /// 添加矩形轮廓
        /// </summary>
        /// <param name="centerRow">矩形中心行坐标</param>
        /// <param name="centerColumn">矩形中心列坐标</param>
        /// <param name="centerPhi">矩形主轴角度(弧度制)</param>
        /// <param name="halfLength">矩形长度一半</param>
        /// <param name="halfWidth">矩形宽度一半</param>
        /// <param name="color">轮廓颜色</param>
        /// <returns>绘制图形</returns>
        public static CHObject GenRectangleContour(
            HTuple centerRow,
            HTuple centerColumn,
            HTuple centerPhi,
            HTuple halfLength,
            HTuple halfWidth,
            string color = "green"
        )
        {
            HOperatorSet.GenRectangle2ContourXld(
                out HObject rectangle,
                centerRow,
                centerColumn,
                centerPhi,
                halfLength,
                halfWidth
            );
            return new CHObject(rectangle, color);
        }

        /// <summary>
        /// 添加填充矩形
        /// </summary>
        /// <param name="centerRow">矩形中心行坐标</param>
        /// <param name="centerColumn">矩形中心列坐标</param>
        /// <param name="centerPhi">矩形主轴角度(弧度制)</param>
        /// <param name="halfLength">矩形长度一半</param>
        /// <param name="halfWidth">矩形宽度一半</param>
        /// <param name="color">轮廓颜色</param>
        /// <returns>绘制图形</returns>
        public static CHObject GenFillRectangle(
            HTuple centerRow,
            HTuple centerColumn,
            HTuple centerPhi,
            HTuple halfLength,
            HTuple halfWidth,
            string color = "green"
        )
        {
            HOperatorSet.GenRectangle2(
                out HObject rectangle,
                centerRow,
                centerColumn,
                centerPhi,
                halfLength,
                halfWidth
            );
            return new CHObject(rectangle, color);
        }

        /// <summary>
        /// 添加圆形轮廓
        /// </summary>
        /// <param name="centerRow">圆心行坐标</param>
        /// <param name="centerColumn">圆心列坐标</param>
        /// <param name="radius">半径</param>
        /// <param name="startPhi">起始角度(弧度制)</param>
        /// <param name="endPhi">结束角度(弧度制)</param>
        /// <param name="pointOrder">沿边界的点序："negative"|"positive"</param>
        /// <param name="resolution">相邻轮廓点之间的距离</param>
        /// <param name="color">轮廓颜色</param>
        /// <returns>绘制图形</returns>
        public static CHObject GenCircleContour(
            HTuple centerRow,
            HTuple centerColumn,
            HTuple radius,
            HTuple startPhi,
            HTuple endPhi,
            HTuple pointOrder,
            double resolution = 1.0,
            string color = "green"
        )
        {
            HOperatorSet.GenCircleContourXld(
                out HObject circle,
                centerRow,
                centerColumn,
                radius,
                startPhi,
                endPhi,
                pointOrder,
                resolution
            );
            return new CHObject(circle, color);
        }

        /// <summary>
        /// 添加十字轮廓
        /// </summary>
        /// <param name="centerRow">中心行坐标</param>
        /// <param name="centerColumn">中心列坐标</param>
        /// <param name="size">十字长度</param>
        /// <param name="angle">十字方向角度(弧度制)</param>
        /// <param name="color">轮廓颜色</param>
        /// <returns>绘制图形</returns>
        public static CHObject GenCrossContour(
            HTuple centerRow,
            HTuple centerColumn,
            HTuple size,
            HTuple angle,
            string color = "green"
        )
        {
            HOperatorSet.GenCrossContourXld(
                out HObject cross,
                centerRow,
                centerColumn,
                size,
                angle
            );
            return new CHObject(cross, color);
        }

        /// <summary>
        /// 添加直线
        /// </summary>
        /// <param name="startRow">开始行坐标</param>
        /// <param name="startColumn">开始列坐标</param>
        /// <param name="endRow">结束行坐标</param>
        /// <param name="endColumn">结束列坐标</param>
        /// <param name="color">直线颜色</param>
        /// <returns>绘制图形</returns>
        public static CHObject GenLine(
            HTuple startRow,
            HTuple startColumn,
            HTuple endRow,
            HTuple endColumn,
            string color = "green"
        )
        {
            HOperatorSet.GenRegionLine(
                out HObject regionLines,
                startRow,
                startColumn,
                endRow,
                endColumn
            );
            return new CHObject(regionLines, color);
        }

        /// <summary>
        /// 添加椭圆轮廓
        /// </summary>
        /// <param name="centerRow">中心行坐标</param>
        /// <param name="centerColumn">中心列坐标</param>
        /// <param name="centerPhi">主轴方向角度(弧度制)</param>
        /// <param name="longAxisRadius">长轴半径</param>
        /// <param name="shortAxisRadius">短轴半径</param>
        /// <param name="startPhi">起始角度(弧度制)</param>
        /// <param name="endPhi">结束角度(弧度制)</param>
        /// <param name="pointOrder">沿边界的点序："negative"|"positive"</param>
        /// <param name="resolution">相邻轮廓点之间的距离</param>
        /// <param name="color">轮廓颜色</param>
        /// <returns>绘制图形</returns>
        public static CHObject GenEllipseContour(
            HTuple centerRow,
            HTuple centerColumn,
            HTuple centerPhi,
            HTuple longAxisRadius,
            HTuple shortAxisRadius,
            HTuple startPhi,
            HTuple endPhi,
            HTuple pointOrder,
            double resolution = 1.0,
            string color = "green"
        )
        {
            HOperatorSet.GenEllipseContourXld(
                out HObject ellipse,
                centerRow,
                centerColumn,
                centerPhi,
                longAxisRadius,
                shortAxisRadius,
                startPhi,
                endPhi,
                pointOrder,
                resolution
            );
            return new CHObject(ellipse, color);
        }

        /// <summary>
        /// 生成区域轮廓
        /// 模式："border":边界像素的外边界作为轮廓点；"border_holes":除了输入区域的外边界，还可以得到所有孔的轮廓；"center":边界像素的中心用作轮廓点
        /// </summary>
        /// <param name="region">区域</param>
        /// <param name="mode">生成模式：border、border_holes、center</param>
        /// <param name="color">轮廓颜色</param>
        /// <returns>绘制图形</returns>
        public static CHObject GenRegionContour(HObject region, HTuple mode, string color = "green")
        {
            HOperatorSet.GenContourRegionXld(region, out HObject contour, mode);
            return new CHObject(contour, color);
        }



        /// <summary>
        /// 六角星标记
        /// </summary>
        /// <param name="centerRow">中心行坐标</param>
        /// <param name="centerColumn">中心列坐标</param>
        /// <param name="centerPhi">主轴方向角度(弧度制)</param>
        /// <param name="len"></param>
        /// <param name="color">区域颜色</param>
        /// <returns>六角星</returns>
        public static CHObject GenFillSixStar(HTuple centerRow, HTuple centerColumn, HTuple centerPhi, HTuple len, string color = "green")
        {
            var rows = new double[13];
            var cols = new double[13];
            // 点1: row = centerRow, column = cneterColumn - len
            rows[0] = centerRow;
            cols[0] = centerColumn - len;
            // 点2: row = centerRow + sqrt(3)/2 * len, column = centerColumn - 3/2 * len
            rows[1] = centerRow + Math.Sqrt(3) / 2 * len;
            cols[1] = centerColumn - 3.0 / 2 * len;
            // 点3: row = centerRow + sqrt(3)/2 * len, column = centerColumn - 1/2 * len
            rows[2] = centerRow + Math.Sqrt(3) / 2 * len;
            cols[2] = centerColumn - 1.0 / 2 * len;
            // 点4: row = centerRow + sqrt(3) * len, column = centerColumn
            rows[3] = centerRow + Math.Sqrt(3) * len;
            cols[3] = centerColumn;
            // 点5: row = centerRow + sqrt(3)/2 * len, column = centerColumn + 1/2 * len
            rows[4] = centerRow + Math.Sqrt(3) / 2 * len;
            cols[4] = centerColumn + 1.0 / 2 * len;
            // 点6: row = centerRow + sqrt(3)/2 * len, column = centerColumn + 3/2 * len
            rows[5] = centerRow + Math.Sqrt(3) / 2 * len;
            cols[5] = centerColumn + 3.0 / 2 * len;
            // 点7: row = centerRow, column = centerColumn + len
            rows[6] = centerRow;
            cols[6] = centerColumn + len;
            // 点8: row = centerRow - sqrt(3)/2 * len, column = centerColumn + 3/2 * len
            rows[7] = centerRow - Math.Sqrt(3) / 2 * len;
            cols[7] = centerColumn + 3.0 / 2 * len;
            // 点9: row = centerRow - sqrt(3)/2 * len, column = centerColumn + 1/2 * len
            rows[8] = centerRow - Math.Sqrt(3) / 2 * len;
            cols[8] = centerColumn + 1.0 / 2 * len;
            // 点10: row = centerRow - sqrt(3) * len, column = centerColumn
            rows[9] = centerRow - Math.Sqrt(3) * len;
            cols[9] = centerColumn;
            // 点11: row = centerRow - sqrt(3)/2 * len, column = centerColumn - 1/2 * len
            rows[10] = centerRow - Math.Sqrt(3) / 2 * len;
            cols[10] = centerColumn - 1.0 / 2 * len;
            // 点12: row = centerRow - sqrt(3)/2 * len, column = centerColumn - 3/2 * len
            rows[11] = centerRow - Math.Sqrt(3) / 2 * len;
            cols[11] = centerColumn - 3.0 / 2 * len;
            // 点13: row = centerRow, column = cneterColumn - len
            rows[12] = centerRow;
            cols[12] = centerColumn - len;
            HOperatorSet.GenRegionPolygonFilled(out HObject star, rows, cols);
            return new CHObject(star, color);
        }

        /// <summary>
        /// CT专用星星标记
        /// </summary>
        /// <param name="centerRow">中心行坐标</param>
        /// <param name="centerColumn">中心列坐标</param>
        /// <param name="centerPhi">主轴方向角度(弧度制)</param>
        /// <param name="color">区域颜色</param>
        /// <returns>CT专用星星</returns>
        public static CHObject GenFillCTStar(HTuple centerRow, HTuple centerColumn, HTuple centerPhi, string color = "green")
        {
            HOperatorSet.GenRectangle2(
                out HObject mainRectangle,
                centerRow,
                centerColumn,
                centerPhi,
                2,
                2
            );
            HOperatorSet.GenRectangle2(
                out HObject rectangle1,
                centerRow - 3,
                centerColumn,
                centerPhi,
                0,
                0
            );
            HOperatorSet.GenRectangle2(
                out HObject rectangle2,
                centerRow,
                centerColumn + 3,
                centerPhi,
                0,
                0
            );
            HOperatorSet.GenRectangle2(
                out HObject rectangle3,
                centerRow + 3,
                centerColumn,
                centerPhi,
                0,
                0
            );
            HOperatorSet.GenRectangle2(
                out HObject rectangle4,
                centerRow,
                centerColumn - 3,
                centerPhi,
                0,
                0
            );
            return new CHObject(mainRectangle.ConcatObj(rectangle1).ConcatObj(rectangle2).ConcatObj(rectangle3).ConcatObj(rectangle4), color);
        }

        public static HObject GenFillCTStar(HTuple centerRow, HTuple centerColumn, HTuple centerPhi)
        {
            HOperatorSet.GenRectangle2(
                out HObject mainRectangle,
                centerRow,
                centerColumn,
                centerPhi,
                2,
                2
            );
            HOperatorSet.GenRectangle2(
                out HObject rectangle1,
                centerRow - 3,
                centerColumn,
                centerPhi,
                0,
                0
            );
            HOperatorSet.GenRectangle2(
                out HObject rectangle2,
                centerRow,
                centerColumn + 3,
                centerPhi,
                0,
                0
            );
            HOperatorSet.GenRectangle2(
                out HObject rectangle3,
                centerRow + 3,
                centerColumn,
                centerPhi,
                0,
                0
            );
            HOperatorSet.GenRectangle2(
                out HObject rectangle4,
                centerRow,
                centerColumn - 3,
                centerPhi,
                0,
                0
            );
            return mainRectangle.ConcatObj(rectangle1).ConcatObj(rectangle2).ConcatObj(rectangle3).ConcatObj(rectangle4);
        }
    }
}
