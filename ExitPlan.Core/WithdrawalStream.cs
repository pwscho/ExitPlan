public class WithdrawalStream
{
    public Guid OwnerId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int StartAge { get; set; }

    public int EndAge { get; set; }

    public decimal AnnualAmount { get; set; } = 1000;
}

