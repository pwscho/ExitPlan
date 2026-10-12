namespace ExitPlan.Core.Taxes
{
    public sealed class TaxBracket
    {
        public decimal Threshold { get; set; }   // upper bound
        public decimal Rate { get; set; }        // e.g. 0.22m
    }
}

