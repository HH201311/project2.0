using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB
{
    public class WeightDataModelMap : ClassMap<WeightDataModel>
    {
        public WeightDataModelMap()
        {
            // 自动应用特性映射
            AutoMap(CultureInfo.InvariantCulture);

            // 自定义映射
            Map(m => m.Weights).Convert(args => ExtractWeights(args));
        }

        private List<double> ExtractWeights(ConvertFromStringArgs args)
        {
            var weights = new List<double>();
            var headers = args.Row.HeaderRecord;
            if (headers == null || headers.Length <= 0) return weights;

            for (int i = 0; i < headers.Length; i++)
            {
                var header = headers[i];

                var pattern = @"重量\d+$";
                var match = Regex.Match(header, pattern);
                if (!match.Success) continue;

                var value = args.Row.GetField(i);
                var success = double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double weight);
                if (success) weights.Add(weight);
                else weights.Add(0);
            }

            return weights;
        }

        public static CsvConfiguration GetCsvConfiguration()
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture);
            config.Delimiter = ",";                 // 分隔符
            config.NewLine = Environment.NewLine;   // 换行符
            config.HasHeaderRecord = true;          // 有表头
            config.PrepareHeaderForMatch = args => args.Header.Trim();
            config.TrimOptions = TrimOptions.Trim;
            config.BadDataFound = null;             // 忽略错误数据
            config.MissingFieldFound = null;        // 忽略缺失字段
            config.HeaderValidated = null;          // 不验证标题
            config.IgnoreBlankLines = true;         // 忽略空行
            config.AllowComments = false;           // 禁用注释
            config.BufferSize = 4 * 1024 * 1024;    // 增大缓冲区
            return config;
        }
    }
}
