using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.WMSocketClient
{
    /// <summary>
    /// 操作单数据：双架涂布
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct S_OperationSheetCC_2
    {
        /// <summary>
        /// 品种名称(单面S结尾，双面D结尾)
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string ModelNo;

        /// <summary>
        /// 基材重
        /// </summary>
        public float BaseWeight;

        /// <summary>
        /// A面净重
        /// </summary>
        public float NetWeightA;

        /// <summary>
        /// B面净重
        /// </summary>
        public float NetWeightB;

        /// <summary>
        /// 单面公差
        /// </summary>
        public float SingleTol;

        /// <summary>
        /// 双面公差
        /// </summary>
        public float DoubleTol;

        /// <summary>
        /// 比例系数
        /// </summary>
        public float PropCoef1;

        /// <summary>
        /// 平移系数
        /// </summary>
        public float MoveCoef1;

        /// <summary>
        /// 比例系数
        /// </summary>
        public float PropCoef2;

        /// <summary>
        /// 平移系数
        /// </summary>
        public float MoveCoef2;

        /// <summary>
        /// 比例系数
        /// </summary>
        public float PropCoef3;

        /// <summary>
        /// 平移系数
        /// </summary>
        public float MoveCoef3;

        /// <summary>
        /// 备注
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string ParamRemark;

        /// <summary>
        /// 扫描起始分区
        /// </summary>
        public int ScanStartPos;

        /// <summary>
        /// 扫描终止分区
        /// </summary>
        public int ScanEndPos;

        /// <summary>
        /// 膜片长度
        /// </summary>
        public int MebraneLength;

        /// <summary>
        /// 单面预警公差
        /// </summary>
        public float SingleAlarmTol;

        /// <summary>
        /// 双面预警公差
        /// </summary>
        public float DoubleAlarmTol;

        /// <summary>
        /// 基材公差   
        /// </summary>
        public float BaseTol;

        /// <summary>
        /// 基材预警公差
        /// </summary>
        public float BaseAlarmTol;
    };
}
