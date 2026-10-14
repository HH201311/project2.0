namespace SUNWODA_SEVB.Core.Models.Project.ZZSIX
{
    /// <summary>产品编码档位：名称 + 前 10 位校验前缀。</summary>
    public sealed class Pack6ProductCodeProfile
    {
        public required string Name { get; init; }
        public required string Prefix10 { get; init; }

        public override string ToString() => Name;
    }

    /// <summary>现场五种产品编码（设置下拉固定枚举，不可现场增删）。</summary>
    public static class Pack6ProductCodeCatalog
    {
        public const int PrefixLength = 10;

        public static IReadOnlyList<Pack6ProductCodeProfile> All { get; } =
        [
            new() { Name = "TW国内两驱", Prefix10 = "12TPB0ALAB" },
            new() { Name = "TW国内四驱", Prefix10 = "12TPB09LAB" },
            new() { Name = "TW海外四驱", Prefix10 = "12TPB0BLAB" },
            new() { Name = "TW海外两驱", Prefix10 = "12TPB0CLAB" },
            new() { Name = "X01四驱", Prefix10 = "12TPB1ALAB" },
        ];

        public static Pack6ProductCodeProfile Default => All[0];

        public static Pack6ProductCodeProfile Resolve(string? nameOrPrefix)
        {
            var key = (nameOrPrefix ?? string.Empty).Trim();
            if (key.Length == 0)
                return Default;

            var byName = All.FirstOrDefault(p =>
                string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase));
            if (byName is not null)
                return byName;

            var byPrefix = All.FirstOrDefault(p =>
                string.Equals(p.Prefix10, key, StringComparison.OrdinalIgnoreCase));
            return byPrefix ?? Default;
        }
    }
}
