namespace ExitPlan.Core.Strategies
{
    public interface IRothConversionWithdrawalStrategy
    {
        decimal Convert(
            Guid ownerId,
            decimal requestedAmount,
            List<Account> workingAccounts,
            TaxYearAccumulator tax,
            SimulationResult result,
            int yearIndex);
    }
}
