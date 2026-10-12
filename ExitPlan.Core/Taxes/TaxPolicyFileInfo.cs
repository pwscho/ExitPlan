namespace ExitPlan.Core.Taxes
{
    public sealed record TaxPolicyFileInfo(int Year, string FileName, string FullPath)
    {
        public string DisplayName => $"{FileName}  ({Year})";
    }
}
