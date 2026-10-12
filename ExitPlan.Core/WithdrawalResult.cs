using ExitPlan.Core;

public class WithdrawalResult
{
    public Account Account { get; set; }
    public decimal Amount { get; set; }
    public decimal TaxableAmount { get; set; }
    public TaxBucket Bucket { get; set; }
}

