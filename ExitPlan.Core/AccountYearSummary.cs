namespace ExitPlan.Core
{
    public class AccountYearSummary
    {
        public int Year { get; set; }

        public string Owner { get; set; } = "";
        public int Age { get; set; } = 0;
        public string TaxBucket { get; set; } = "";
        public string AssetClass { get; set; } = "";

        public decimal StartingValue { get; set; }
        public decimal EndingValue { get; set; }
        public decimal InvestmentReturn { get; set; }
    }
}
