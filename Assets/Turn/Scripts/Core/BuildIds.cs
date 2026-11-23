namespace Turn.Core
{
    /// <summary>战斗侧词条稳定编号。100 起避免与 A 侧保留的五个旧 UpgradeId 数值冲突。</summary>
    public enum BuildId
    {
        B01 = 100, B02, B03, B04, B05, B06, B07, B08, B09, B10,
        B11, B12, B13, B14, B15, B16, B17, B18, B19
    }

    public static class BuildIds
    {
        public const int Count = 19;
        public static int Index(BuildId id) { return (int)id - (int)BuildId.B01; }
        // 现有 IProgressionSystem.GetLevel(UpgradeId) 不能改签名；只传入值，旧枚举成员不变。
        public static UpgradeId Query(BuildId id) { return (UpgradeId)(int)id; }
        public static bool IsValid(BuildId id) { return Index(id) >= 0 && Index(id) < Count; }
    }
}
