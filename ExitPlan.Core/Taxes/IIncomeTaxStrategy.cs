namespace ExitPlan.Core.Taxes
{
    public interface IIncomeTaxStrategy
    {
        TaxComputationResult Compute(TaxComputationInput input, TaxPolicy policy);
    }
}
