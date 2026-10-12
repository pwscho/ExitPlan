using ExitPlan.Core;

public class TransferResult
{
    public AccountProjection FromAccount { get; set; } = null!;
    public AccountProjection? ToAccount { get; set; }

    public decimal RequestedAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal NetAmount { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal TaxesDue { get; set; }

    public TransferType Type { get; set; }
    public int Year { get; set; }
}


