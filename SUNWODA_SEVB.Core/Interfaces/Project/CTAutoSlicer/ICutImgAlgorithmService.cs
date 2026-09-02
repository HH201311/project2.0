namespace SUNWODA_SEVB.Core.Interfaces.Project.CTAutoSlicer
{
    /// <summary>
    /// 惠州离线 CT 自动切图算法（CTAutoCutImg.dll）反射调用封装。
    /// </summary>
    public interface ICutImgAlgorithmService
    {
        bool IsInitialized { get; }

        /// <summary>从程序目录 Dlls\CTAutoCutImg.dll 加载类型 CT。</summary>
        void Initialize();

        /// <summary>
        /// 获取上盖平面三点 + 原点（相对截图像素坐标）。
        /// [0..2]=上盖三点（步骤1），[3]=原点（步骤3）。
        /// </summary>
        /// <param name="imagePath">截图文件路径（内部 ReadImage 为 HObject）</param>
        (int X, int Y)[] GetSectionReferencePoints(string imagePath);

        /// <summary>
        /// 获取切面方向两点（相对截图像素坐标）。步骤2 使用。
        /// [0]=起点，[1]=终点。
        /// </summary>
        (int X, int Y)[] GetObjectTopBottomPoints(string imagePath);
    }
}
