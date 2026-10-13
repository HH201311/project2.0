namespace SUNWODA_SEVB.Core.Models.Project.YWSIX
{
    /// <summary>
    /// 顶面相机各张序上盖类别期望数量（必须相等）。
    /// 第 5 张拼接、第 6 张不检。
    /// </summary>
    public static class Pack6TopCoverExpectations
    {
        public const string ClassHanger = "hanger_bushing";
        public const string ClassFoam = "sealing_foam";

        public sealed record Expect(int HangerBushing, int SealingFoam);

        /// <summary>可检测的顶面张序 → 期望数量。</summary>
        public static IReadOnlyDictionary<int, Expect> ByShot { get; } =
            new Dictionary<int, Expect>
            {
                [1] = new(2, 1),
                [2] = new(1, 0),
                [3] = new(1, 0),
                [4] = new(2, 2)
            };

        public static bool TryGet(int shotIndex, out Expect expect) =>
            ByShot.TryGetValue(shotIndex, out expect!);

        /// <summary>顶面第 5 张为拼接图，跳过检测。</summary>
        public static bool IsStitchShot(int shotIndex) => shotIndex == 5;

        /// <summary>顶面第 6 张不检测。</summary>
        public static bool IsSkippedShot(int shotIndex) => shotIndex == 6 || IsStitchShot(shotIndex);
    }
}
