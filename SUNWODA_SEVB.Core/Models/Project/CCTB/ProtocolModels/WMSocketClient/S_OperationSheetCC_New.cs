using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.WMSocketClient
{
    /// <summary>
    /// 操作单数据：新涂布
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct S_OperationSheetCC_New
    {
        /// <summary>
        /// //品种名称(单面S结尾，双面D结尾)
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
        /// //基材公差
        /// </summary>
        public float BaseTol;

        /// <summary>
        /// 单面公差
        /// </summary>
        public float SingleTol;

        /// <summary>
        /// //双面公差
        /// </summary>
        public float DoubleTol;

        /// <summary>
        /// //基材预警公差
        /// </summary>
        public float BaseAlarmTol;

        /// <summary>
        /// //单面预警公差
        /// </summary>
        public float SingleAlarmTol;

        /// <summary>
        /// //双面预警公差
        /// </summary>
        public float DoubleAlarmTol;

        /// <summary>
        /// //扫描起始分区
        /// </summary>
        public int ScanStartPos;

        /// <summary>
        /// //扫描终止分区
        /// </summary>
        public int ScanEndPos;

        /// <summary>
        /// 是否启用二次项
        /// </summary>
        public int IsQuadraticFit;

        /// <summary>
        /// 配方1
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string StrRecipeName1;

        /// <summary>
        /// 1架二次A
        /// </summary>
        public float CofA_Value1;

        /// <summary>
        /// 1架比例系数(B)
        /// </summary>
        public float PropCoef1;

        /// <summary>
        /// 1架平移系数(C)
        /// </summary>
        public float MoveCoef1;

        /// <summary>
        /// 配方2
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string StrRecipeName2;

        /// <summary>
        /// 2架二次A
        /// </summary>
        public float CofA_Value2;

        /// <summary>
        /// 2架比例系数(B)
        /// </summary>
        public float PropCoef2;

        /// <summary>
        /// 2架平移系数(C)
        /// </summary>
        public float MoveCoef2;

        /// <summary>
        /// 配方3
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string StrRecipeName3;

        /// <summary>
        /// 3架二次A
        /// </summary>
        public float CofA_Value3;

        /// <summary>
        /// 3架比例系数(B)
        /// </summary>
        public float PropCoef3;

        /// <summary>
        /// 3架平移系数(C)
        /// </summary>
        public float MoveCoef3;

        /// <summary>
        /// 配方4
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string StrRecipeName4;

        /// <summary>
        /// 4架二次A
        /// </summary>
        public float CofA_Value4;

        /// <summary>
        /// 4架比例系数(B)
        /// </summary>
        public float PropCoef4;

        /// <summary>
        /// 4架平移系数(C)
        /// </summary>
        public float MoveCoef4;

        /// <summary>
        /// 配方5
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string StrRecipeName5;

        /// <summary>
        /// 5架二次A
        /// </summary>
        public float CofA_Value5;

        /// <summary>
        /// 5架比例系数(B)
        /// </summary>
        public float PropCoef5;

        /// <summary>
        /// 5架平移系数(C)
        /// </summary>
        public float MoveCoef5;

        /// <summary>
        /// //备注
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string ParamRemark;
    };
}
