using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;

namespace SUNWODA_SEVB.Core.Converters.Project.CCTB
{
    public class SliderValidationRule : ValidationRule
    {
        public double? Minimum { get; set; }
        public double? Maximum { get; set; }
        public double? TickFrequency { get; set; }
        public bool IsSnapToTickEnabled { get; set; }

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            if (value is string stringValue)
            {
                // 检查是否为有效数字
                if (!double.TryParse(stringValue, NumberStyles.Any, cultureInfo, out double number))
                {
                    return new ValidationResult(false, "请输入有效的数字");
                }

                // 检查是否在范围内
                if (Minimum.HasValue && number < Minimum.Value)
                {
                    return new ValidationResult(false, $"值不能小于 {Minimum.Value}");
                }

                if (Maximum.HasValue && number > Maximum.Value)
                {
                    return new ValidationResult(false, $"值不能大于 {Maximum.Value}");
                }

                // 如果启用了对齐刻度，检查是否符合刻度间隔
                if (IsSnapToTickEnabled && TickFrequency.HasValue && TickFrequency.Value > 0)
                {
                    double remainder = (number - (Minimum ?? 0)) % TickFrequency.Value;
                    if (Math.Abs(remainder) > 0.0001 && Math.Abs(remainder - TickFrequency.Value) > 0.0001)
                    {
                        return new ValidationResult(false, $"值必须符合刻度间隔: {TickFrequency.Value}");
                    }
                }

                return ValidationResult.ValidResult;
            }

            return new ValidationResult(false, "请输入有效的数字");
        }
    }
}
