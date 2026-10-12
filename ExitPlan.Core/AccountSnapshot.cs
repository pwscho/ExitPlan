namespace ExitPlan.Core
{
    public class AccountSnapshot
    {
        public Guid AccountId { get; set; }
        public string Name { get; set; } = "";
        public string Owner { get; set; } = "";
        public Guid OwnerId { get; set; }
        public string TaxBucket { get; set; } = "";
        public string AssetClass { get; set; } = "";

        public decimal StartingValue { get; set; }
        public decimal InvestmentReturn { get; set; }
        public decimal Value { get; set; }
    }
}
