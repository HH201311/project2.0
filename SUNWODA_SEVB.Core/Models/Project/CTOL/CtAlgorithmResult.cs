using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CTOL
{
    /// <summary>
    /// Worker 返回给主进程的算法结果（每行一条 JSON）。
    /// </summary>
    public sealed class CtAlgorithmResult
    {
        /// <summary>
        /// 与请求对应的唯一标识；ready 握手消息为 "__READY__"。
        /// </summary>
        public string RequestId { get; set; } = "";

        /// <summary>
        /// 产出该结果的 Worker 所绑定的工位编号。
        /// </summary>
        public int StationId { get; set; } = -1;

        public bool Success { get; set; }

        public string ImagePath { get; set; } = "";
        public string OutputDir { get; set; } = "";

        public bool HasHObject { get; set; }

        /// <summary>
        /// 负极区域 HObject 文件路径（.hobj）。
        /// </summary>
        public string RecNEPath { get; set; } = "";

        /// <summary>
        /// 正极区域 HObject 文件路径（.hobj）。
        /// </summary>
        public string RecPEPath { get; set; } = "";

        /// <summary>
        /// 正极中间区域 HObject 文件路径（.hobj）。
        /// </summary>
        public string RecMiddlePEPath { get; set; } = "";

        /// <summary>
        /// 正极 OH 内侧值数组。
        /// </summary>
        public double[] DistOHInt { get; set; } = Array.Empty<double>();

        /// <summary>
        /// 负极 OH 外侧值数组。
        /// </summary>
        public double[] DistOHOut { get; set; } = Array.Empty<double>();

        /// <summary>
        /// 间隙最大值数组 [0]=负极边缘, [1]=正极边缘, [2]=正极中间。
        /// </summary>
        public double[] GapMax { get; set; } = Array.Empty<double>();

        /// <summary>
        /// 负极差值（hDNe）。
        /// </summary>
        public float HDNe { get; set; }

        /// <summary>
        /// 负极压伤标志（0=正常，1=压伤）。
        /// </summary>
        public int NeNGFlag { get; set; }

        /// <summary>
        /// 【旧方案，2026-08 起不再用于判级】负极突起布尔标志（0=正常，1=突起）。
        /// 旧逻辑：算法侧直接给出 flag，上位机 <c>neTQflag==1</c> 即 NG，无比面积阈值。
        /// 新逻辑改由 <see cref="NeFoldArea"/> 与界面阈值比较，本字段仅保留兼容旧 Worker JSON。
        /// </summary>
        public int NeTQFlag { get; set; }

        /// <summary>
        /// 【新方案，待算法接口交付】翻折距离正极的面积，单位 mm²。
        /// <para>
        /// 上位机比较规则（见 <c>ImageProcessorBase.NgOkJudge</c>）：
        /// 面积为 0 → 打日志并 NG；面积 ≥ 界面 <c>Gap48Limit</c> → NG_负极突起。
        /// </para>
        /// <para>
        /// 默认 <c>null</c> 表示 Worker JSON 尚未输出该字段（接口未到），判级跳过本条，避免误报。
        /// 算法到位后，请保证 stdout JSON 属性名为 <c>NeFoldArea</c>（System.Text.Json 默认区分大小写）；
        /// 若对方字段名不同，在本属性上补 <c>[JsonPropertyName("对方字段名")]</c>。
        /// </para>
        /// </summary>
        public double? NeFoldArea { get; set; }

        public string ErrorReport { get; set; } = "";
    }
}
