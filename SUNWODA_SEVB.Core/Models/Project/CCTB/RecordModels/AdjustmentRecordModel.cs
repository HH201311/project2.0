using Newtonsoft.Json;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.RecordModels
{
    public class AdjustmentRecordModel : ModelBase
    {
        private int id;
        private string? adjustMode = "Unknown";
        private float tPosition;
        private float tAdjustOffset;
        private float bPosition;
        private float bAdjustOffset;
        private DateTime? recordTime = null;

        /// <summary>
        /// 排序编号
        /// </summary>
        [JsonIgnore]
        public int ID { get => id; set => SetProperty(ref id, value); }

        /// <summary>
        /// 调机模式
        /// </summary>
        public string? AdjustMode { get => adjustMode; set => SetProperty(ref adjustMode, value); }

        /// <summary>
        /// T块当前位置
        /// </summary>
        public float TPosition { get => tPosition; set => SetProperty(ref tPosition, value); }

        /// <summary>
        /// T块调整偏移量
        /// </summary>
        public float TAdjustOffset { get => tAdjustOffset; set => SetProperty(ref tAdjustOffset, value); }

        /// <summary>
        /// 泵速当前位置
        /// </summary>
        public float BPosition { get => bPosition; set => SetProperty(ref bPosition, value); }

        /// <summary>
        /// 泵速调整偏移量
        /// </summary>
        public float BAdjustOffset { get => bAdjustOffset; set => SetProperty(ref bAdjustOffset, value); }

        /// <summary>
        /// 记录时间
        /// </summary>
        [JsonIgnore]
        public DateTime? RecordTime { get => recordTime; set => SetProperty(ref recordTime, value); }
    }
}
