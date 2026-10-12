public class SimResults
{
    public int Sim { get; set; }
    public int Iteration { get; set; }

    // Mirror ResultRow
    public int Year { get; set; }
    public decimal Income { get; set; }
    public decimal SocialSecurity { get; set; }
    public decimal OrdinaryIncome { get; set; }
    public decimal CapitalGains { get; set; }
    public decimal Withdrawals { get; set; }
    public decimal TaxableIncome { get; set; }
    public decimal Taxes { get; set; }
    public decimal TaxesPaid { get; set; }
    public decimal Irmaa { get; set; }
    public decimal IrmaaPaid { get; set; }
    public decimal EndingBalance { get; set; }
}

