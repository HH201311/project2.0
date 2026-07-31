using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Models.Project.JRRY;

namespace SUNWODA_SEVB.Services.Project.JRRY
{
    public class ThresholdService(IProjectSettingRepository repo)
    {
        private const string ViewModelKey = "VM_JRRedundancyPage";

        public List<ParameterItem> GetParameters()
        {
            var models = repo.GetList(m =>m.BelongToVM == ViewModelKey && m.Remark.StartsWith("JRRY_Param_"));
            if (models == null || models.Count == 0)
                return GetDefaultParameters();

            return models
                .OrderBy(m => m.Name)
                .Select(m =>
                {
                    try
                    {
                        return System.Text.Json.JsonSerializer.Deserialize<ParameterItem>(m.Value ?? "");
                    }
                    catch { return null; }
                })
                .Where(p => p != null)
                .ToList()!;
        }

        private List<ParameterItem> GetDefaultParameters()
        {
            return
            [
                new() { ParameterName = "左侧距离标准", UpperLimit = -1.00, LowerLimit = 10.00 },
                new() { ParameterName = "右侧距离标准", UpperLimit = -1.00, LowerLimit = 10.00 },
                new() { ParameterName = "左最小高度距离标准", UpperLimit = -1.00, LowerLimit = 10.00 },
                new() { ParameterName = "右最小高度距离标准", UpperLimit = -1.00, LowerLimit = 10.00 },
                new() { ParameterName = "左最大高度距离标准", UpperLimit = -1.00, LowerLimit = 10.00 },
                new() { ParameterName = "右最大高度距离标准", UpperLimit = -1.00, LowerLimit = 10.00 },
                new() { ParameterName = "开叉面积", UpperLimit = -1.00, LowerLimit = 10.00 },
                new() { ParameterName = "开叉角度1", UpperLimit = -1.00, LowerLimit = 10.00 },
                new() { ParameterName = "开叉角度2", UpperLimit = -1.00, LowerLimit = 10.00 },
                new() { ParameterName = "开叉CIL", UpperLimit = -1.00, LowerLimit = 10.00 }
            ];
        }
    }
}