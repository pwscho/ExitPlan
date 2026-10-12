public class AccountSummaryRow
{
    public int Year { get; set; }
    public int Age { get; set; }
    public Dictionary<string, decimal> Totals { get; set; } = new();
}
