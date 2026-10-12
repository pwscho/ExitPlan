public class AccountBalanceRow
{
    public string AccountName { get; set; }
    public string Owner { get; set; }
    public string TaxBucket { get; set; }
    public string AssetClass { get; set; }

    public int Year { get; set; }
    public int Age { get; set; }
    public decimal StartingBalance { get; set; }
    public decimal InvestmentReturn { get; set; }
    public decimal EndingBalance { get; set; }
}
