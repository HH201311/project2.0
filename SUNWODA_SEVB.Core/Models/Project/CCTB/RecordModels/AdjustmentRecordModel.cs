using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Data;
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
        private ESideType sideType;
        private EClosedLoopType closedLoopType;
        private int adjustModel = 0;
        private float passCount;
        private List<int> tPositions = new List<int>();
        private List<int> tAdjustOffsets = new List<int>();
        private List<float> bPositions = new List<float>();
        private List<float> bAdjustOffsets = new List<float>();
        private float knifeGapLeftOffset;
        private float knifeGapRightOffset;

        /// <summary>
        /// 排序编号
        /// </summary>
        [JsonIgnore]
        public int ID { get => id; set => SetProperty(ref id, value); }

        /// <summary>
        /// 极面
        /// </summary>
        public ESideType SideType { get => sideType; set => SetProperty(ref sideType, value); }

        /// <summary>
        /// 闭环类型
        /// </summary>
        public EClosedLoopType ClosedLoopType { get => closedLoopType; set => SetProperty(ref closedLoopType, value); }

        /// <summary>
        /// 调机模式（0：推荐，1：调机成功，2：调机失败）
        /// </summary>
        public int AdjustModel { get => adjustModel; set => SetProperty(ref adjustModel, value); }

        /// <summary>
        /// 趟号
        /// </summary>
        public float PassCount { get => passCount; set => SetProperty(ref passCount, value); }

        /// <summary>
        /// T块 当前位置
        /// </summary>
        public List<int> TPositions { get => tPositions; set => SetProperty(ref tPositions, value); }

        /// <summary>
        /// T块 调整偏移量
        /// </summary>
        public List<int> TAdjustOffsets { get => tAdjustOffsets; set => SetProperty(ref tAdjustOffsets, value); }

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

        #region 用于界面绑定显示的辅助属性
        private string adjustDescripation = string.Empty;
        private DateTime? recordTime = null;

        /// <summary>
        /// 调机模式描述
        /// </summary>
        [JsonIgnore]
        public string AdjustDescripation { get => adjustDescripation; set => SetProperty(ref adjustDescripation, value); }

        /// <summary>
        /// T块位置字符串（用于显示）
        /// </summary>
        [JsonIgnore]
        public string TPositionsDisplay => string.Join(", ", TPositions.Select(p => p.ToString("F2")));

        /// <summary>
        /// T块调整偏移字符串（用于显示）
        /// </summary>
        [JsonIgnore]
        public string TAdjustOffsetsDisplay => string.Join(", ", TAdjustOffsets.Select(o => o.ToString("F3")));

        /// <summary>
        /// B块位置字符串（用于显示）
        /// </summary>
        [JsonIgnore]
        public string BPositionsDisplay => string.Join(", ", BPositions.Select(p => p.ToString("F2")));

        /// <summary>
        /// B块调整偏移字符串（用于显示）
        /// </summary>
        [JsonIgnore]
        public string BAdjustOffsetsDisplay => string.Join(", ", BAdjustOffsets.Select(o => o.ToString("F3")));

        /// <summary>
        /// 记录时间
        /// </summary>
        [JsonIgnore]
        public DateTime? RecordTime { get => recordTime; set => SetProperty(ref recordTime, value); }

        /// <summary>
        /// 转换
        /// </summary>
        /// <param name="log"></param>
        /// <returns></returns>
        public static AdjustmentRecordModel ConvertToRecord(AppLogModel log)
        {
            AdjustmentRecordModel? record;
            try
            {
                record = JsonConvert.DeserializeObject<AdjustmentRecordModel>(log.Message);
                if (record != null) record.AdjustDescripation = $"{record.ClosedLoopType}{record.SideType}{(GetAdjustMode(record.AdjustModel))}";
            }
            catch
            {
                record = null;
            }

            if (record == null)
            {
                record = new AdjustmentRecordModel();
                record.AdjustDescripation = log.Message;
            }
            record.RecordTime = log.LogTime;

            return record;
        }

        /// <summary>
        /// 获取调机模式描述
        /// </summary>
        /// <param name="adjustModel"></param>
        /// <returns></returns>
        private static string GetAdjustMode(int adjustModel)
        {
            return adjustModel switch
            {
                0 => "调机推荐",
                1 => "调机成功",
                2 => "调机失败",
                _ => $"未知[{adjustModel}]"
            };
        }
        #endregion
    }
}
