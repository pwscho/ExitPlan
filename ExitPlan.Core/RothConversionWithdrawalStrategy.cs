using ExitPlan.Core.Strategies;

namespace ExitPlan.Core
{
    public class RothConversionWithdrawalStrategy : IRothConversionWithdrawalStrategy
    {
        private readonly IWithdrawalStrategy _priorityStrategy;

        public RothConversionWithdrawalStrategy(IWithdrawalStrategy priorityStrategy)
        {
            _priorityStrategy = priorityStrategy;
        }

        public decimal Convert(
            Guid ownerId,
            decimal requestedAmount,
            List<Account> workingAccounts,
            TaxYearAccumulator tax,
            SimulationResult result,
            int yearIndex)
        {
            return new Strategies.RothConversionWithdrawalStrategy(_priorityStrategy)
                .Convert(ownerId, requestedAmount, workingAccounts, tax, result, yearIndex);
        }
    }
}
