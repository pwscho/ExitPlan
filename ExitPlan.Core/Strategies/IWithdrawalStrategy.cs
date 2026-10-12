using System.Collections.Generic;

namespace ExitPlan.Core.Strategies
{
    public interface IWithdrawalStrategy
    {
        IEnumerable<Account> GetWithdrawalOrder(
            Scenario scenario,
            List<Account> accounts,
            WithdrawalPurpose purpose = WithdrawalPurpose.Spending);
    }
}
