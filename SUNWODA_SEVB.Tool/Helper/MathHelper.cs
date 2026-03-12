using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Tool.Helper
{
    public class MathHelper
    {
        /// <summary>
        /// 取最大值
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public static int Max(params int[] values)
        {
            if(values == null || values.Length == 0) return 0;
            var max = values[0];
            foreach (var value in values)
            {
                if (value > max) max = value;
            }
            return max;
        }

        /// <summary>
        /// 取最大值
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public static double Max(params double[] values)
        {
            if(values == null || values.Length == 0) return 0;
            var max = values[0];
            foreach (var value in values)
            {
                if (value > max) max = value;
            }
            return max;
        }
    }
}
