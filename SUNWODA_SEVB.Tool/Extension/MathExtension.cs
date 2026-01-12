using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Tool.Extension
{
    public static class MathExtension
    {
        /// <summary>
        /// 总体标准差
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static double STDEVP(this IEnumerable<double> values)
        {
            if (values == null || !values.Any())
                throw new ArgumentException("输入的数据不能为空！");

            // 计算数据的平均值
            double mean = values.Average();

            // 计算每个数据点与平均值的差的平方和
            double sumOfSquaredDifferences = values.Sum(x => Math.Pow(x - mean, 2));

            // 计算方差，这里使用总体标准差的计算公式（除以数据的总数）
            double variance = sumOfSquaredDifferences / values.Count();

            // 计算标准差，即方差的平方根
            double stdevp = Math.Sqrt(variance);
            return stdevp;
        }

        /// <summary>
        /// 计算CPK(过程能力指数)
        /// </summary>
        /// <param name="values"></param>
        /// <param name="targetValue">目标值</param>
        /// <param name="toleranceValue">公差值</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static double CPK(this IEnumerable<double> values, double targetValue, double toleranceValue)
        {
            if (values == null || !values.Any())
                throw new ArgumentException("输入的数据不能为空！");
            if (values.Count() < 2)
                throw new InvalidOperationException("需至少2个数据点");
            // 计算均值
            double mean = values.Average();
            // 计算总体标准差
            double sigma = values.STDEVP();
            if (sigma == 0) return double.NaN;
            // 计算上限
            double upper = targetValue + toleranceValue - mean;
            // 计算下限
            double lower = mean - targetValue + toleranceValue;
            // 计算最小值
            double minValue = Math.Min(upper, lower);
            // 计算cpk
            double cpk = minValue / 3 / sigma;
            return cpk;
        }
    }
}
