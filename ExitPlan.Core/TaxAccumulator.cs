public class TaxYearAccumulator
{
    // Inputs accumulated during the simulation year
    public decimal OrdinaryIncome { get; set; }
    public decimal CapitalGains { get; set; }

    // Total taxable income used for federal income tax calculation
    public decimal TotalTaxableIncome => OrdinaryIncome + CapitalGains;

    // --- Tax outcomes for this year ---

    // Core federal income tax (from ordinary + capital gains, after deductions, etc.)
    public decimal FederalIncomeTax { get; set; }

    // Annual IRMAA surcharge computed from MAGI and IRMAA brackets
    public decimal IrmaaSurcharge { get; set; }

    // Convenience: total federal-related tax burden for the year
    public decimal TotalTax => FederalIncomeTax + IrmaaSurcharge;
}

