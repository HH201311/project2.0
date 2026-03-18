using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB
{
    /// <summary>
    /// 毫米数据分区使用
    /// </summary>
    public class PartitionedModel : ModelBase
    {
        private string? partitionedName;
        private byte startIndex;
        private byte endIndex;
        private byte partitionedCount;

        /// <summary>
        /// 分区名称
        /// </summary>
        public string? PartitionedName { get => partitionedName; set => SetProperty(ref partitionedName, value); }

        /// <summary>
        /// 起始位置
        /// </summary>
        public byte StartIndex { get => startIndex; set => SetProperty(ref startIndex, value); }

        /// <summary>
        /// 终止位置
        /// </summary>
        public byte EndIndex { get => endIndex; set => SetProperty(ref endIndex, value); }

        /// <summary>
        /// 分区个数
        /// </summary>
        public byte PartitionedCount { get => partitionedCount; set => SetProperty(ref partitionedCount, value); }

        /// <summary>
        /// 根据EPartitionedType整理数据库中配置的膜区信息
        /// </summary>
        /// <param name="partitionedType"></param>
        /// <param name="partitionedModels"></param>
        /// <returns></returns>
        public static List<PartitionedModel> GetPartitionedModels(EPartitionedType partitionedType, List<PartitionedModel>? partitionedModels = null)
        {
            var count = (int)partitionedType + 1;
            var re = new List<PartitionedModel>(count);
            for (int i = 0; i < count; i++)
            {
                var model = new PartitionedModel();
                model.PartitionedName = $"膜区{i + 1}";
                if (partitionedModels != null && partitionedModels.Count > i && partitionedModels[i] != null)
                {
                    model.startIndex = partitionedModels[i].StartIndex;
                    model.endIndex = partitionedModels[i].EndIndex;
                    model.PartitionedCount = partitionedModels[i].PartitionedCount;
                }
                re.Add(model);
            }

            return re;
        }
    }
}
