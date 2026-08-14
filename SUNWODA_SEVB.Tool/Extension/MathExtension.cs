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
        /// 总体标准差/Sigma
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static float STDEVP(this IEnumerable<float> values)
        {
            if (values == null || !values.Any())
                throw new ArgumentException("输入的数据不能为空！");

            // 计算数据的平均值
            var mean = values.Average();

            // 计算每个数据点与平均值的差的平方和
            var sumOfSquaredDifferences = values.Sum(x => Math.Pow(x - mean, 2));

            // 计算方差，这里使用总体标准差的计算公式（除以数据的总数）
            var variance = sumOfSquaredDifferences / values.Count();

            // 计算标准差，即方差的平方根
            var stdevp = Math.Sqrt(variance);
            return (float)stdevp;
        }

        /// <summary>
        /// 计算CPK(过程能力指数)
        /// </summary>
        /// <param name="values"></param>
        /// <param name="targetValue">目标值</param>
        /// <param name="toleranceValue">公差值</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static float CPK(this IEnumerable<float> values, float targetValue, float toleranceValue)
        {
            if (values == null || !values.Any()) return 0;
            //throw new ArgumentException("输入的数据不能为空！");
            if (values.Count() < 2) return 0;
            //throw new InvalidOperationException("需至少2个数据点");
            // 计算均值
            var mean = values.Average();
            // 计算总体标准差
            var sigma = values.STDEVP();
            if (sigma == 0) return float.NaN;
            // 计算上限
            var upper = targetValue + toleranceValue - mean;
            // 计算下限
            var lower = mean - targetValue + toleranceValue;
            // 计算最小值
            var minValue = Math.Min(upper, lower);
            // 计算cpk
            var cpk = minValue / 3 / sigma;
            return cpk;
        }

        public static float COV(this IEnumerable<float> values)
        {
            if (values == null || !values.Any())
                throw new ArgumentException("输入的数据不能为空！");

            if (values.Count() == 0 || values.All(o => o == 0)) return 0;
            var mean = values.Average();
            var sigma = values.Sigma();
            return sigma / mean;
        }

        public static float Sigma(this IEnumerable<float> values)
        {
            return values.STDEVP();
        }
    }
}
