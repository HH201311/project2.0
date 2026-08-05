using Newtonsoft.Json;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
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
        private ESideType sideType;
        private int passCount;
        private List<float> tPositions = new List<float>();
        private List<float> tAdjustOffsets = new List<float>();
        private List<float> bPositions = new List<float>();
        private List<float> bAdjustOffsets = new List<float>();
        private float knifeGapLeftOffset;
        private float knifeGapRightOffset;
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
        /// 极面
        /// </summary>
        public ESideType SideType { get => sideType; set => SetProperty(ref sideType, value); }

        /// <summary>
        /// 趟号
        /// </summary>
        public int PassCount { get => passCount; set => SetProperty(ref passCount, value); }

        /// <summary>
        /// T块 当前位置
        /// </summary>
        public List<float> TPositions { get => tPositions; set => SetProperty(ref tPositions, value); }

        /// <summary>
        /// T块 调整偏移量
        /// </summary>
        public List<float> TAdjustOffsets { get => tAdjustOffsets; set => SetProperty(ref tAdjustOffsets, value); }

        /// <summary>
        /// 泵速 当前位置
        /// </summary>
        public List<float> BPositions { get => bPositions; set => SetProperty(ref bPositions, value); }

        /// <summary>
        /// 泵速 调整偏移量
        /// </summary>
        public List<float> BAdjustOffsets { get => bAdjustOffsets; set => SetProperty(ref bAdjustOffsets, value); }

        /// <summary>
        /// 刀距-左 调整偏移量
        /// </summary>
        public float KnifeGapLeftOffset { get => knifeGapLeftOffset; set => SetProperty(ref knifeGapLeftOffset, value); }

        /// <summary>
        /// 刀距-右 调整偏移量
        /// </summary>
        public float KnifeGapRightOffset { get => knifeGapRightOffset; set => SetProperty(ref knifeGapRightOffset, value); }

        /// <summary>
        /// T块位置字符串（用于显示）
        /// </summary>
        [JsonIgnore]
        public string TPositionsDisplay => TPositions?.Any() == true ? string.Join(", ", TPositions.Select(p => p.ToString("F2"))) : "-";

        /// <summary>
        /// T块调整偏移字符串（用于显示）
        /// </summary>
        [JsonIgnore]
        public string TAdjustOffsetsDisplay => TAdjustOffsets?.Any() == true ? string.Join(", ", TAdjustOffsets.Select(o => o.ToString("F3"))) : "-";

        /// <summary>
        /// B块位置字符串（用于显示）
        /// </summary>
        [JsonIgnore]
        public string BPositionsDisplay => BPositions?.Any() == true ? string.Join(", ", BPositions.Select(p => p.ToString("F2"))) : "-";

        /// <summary>
        /// B块调整偏移字符串（用于显示）
        /// </summary>
        [JsonIgnore]
        public string BAdjustOffsetsDisplay => BAdjustOffsets?.Any() == true ? string.Join(", ", BAdjustOffsets.Select(o => o.ToString("F3"))) : "-";

        /// <summary>
        /// 记录时间
        /// </summary>
        [JsonIgnore]
        public DateTime? RecordTime { get => recordTime; set => SetProperty(ref recordTime, value); }
    }
}
