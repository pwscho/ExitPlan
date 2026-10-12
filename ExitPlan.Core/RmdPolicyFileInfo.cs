namespace ExitPlan.Core
{
    public sealed record RmdPolicyFileInfo(int Year, string FileName, string FullPath)
    {
        public string DisplayName => $"{FileName}  ({Year})";
    }
}
