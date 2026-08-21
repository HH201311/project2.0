namespace SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer
{
    /// <summary>
    /// 简单 3-2-1 找点算法来源：上位机本地 OpenCV，或视觉 CTAutoCutImg.dll。
    /// </summary>
    public enum Simple321PointSource
    {
        /// <summary>上位机本地：Vision3dSectionPointsDetector + VisionTopViewVectorPointsDetector</summary>
        Local = 0,

        /// <summary>视觉 DLL：GetSectionReferencePoints + GetObjectTopBottomPoints</summary>
        VisionDll = 1,
    }
}
