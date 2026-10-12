using ExitPlan.Core.Engine;
using ExitPlan.Core.Services;

namespace ExitPlan.Core.Tests
{
    public class IrmaaColumnReconciliationTests
    {
        [Fact]
        public void Columns_IncludeIrmaa_AndStartPlusReturnMinusWithdrawalsEqualsEnd()
        {
            var (scenario, accounts, primary, spouse) = TestHousehold.Create(
                taxDeferred: 400_000m,
                taxFreeCash: 80_000m,
                taxableCash: 0m,
                spending: 120_000m,
                years: 4,
                currentAge: 63,
                deathAge: 90);

            scenario.EnableTaxBracketInflation = false;
            scenario.InflationRate = 0m;
            scenario.RebalanceAnnually = false;
            foreach (var account in accounts)
                account.RateOfReturn = 0m;

            var output = new ComputeEngine(
                scenario,
                accounts,
                new FixedMarketService(),
                primary,
                spouse).Run(1, "fixed");

            Assert.Contains(output.Results, row => row.IrmaaPaid > 0m);

            foreach (var row in output.Results)
            {
                decimal withdrawalsIncludingIrmaa =
                    row.Withdrawal + row.TaxesPaid + row.IrmaaPaid
                    - row.SocialSecurityIncome - row.Income;

                Assert.Equal(withdrawalsIncludingIrmaa, row.AccountWithdrawals);
                Assert.Equal(
                    row.EndingBalance,
                    row.StartingBalance + row.InvestmentReturn - row.AccountWithdrawals);

                if (row.StartingBalance == 0m)
                    Assert.Equal(0m, row.PercentWithdrawals);
                else
                    Assert.Equal(row.AccountWithdrawals / row.StartingBalance, row.PercentWithdrawals);
            }

            var irmaaYear = output.Results.First(row => row.IrmaaPaid > 0m);
            decimal withoutIrmaa = irmaaYear.AccountWithdrawals - irmaaYear.IrmaaPaid;
            decimal gapWithoutIrmaa =
                irmaaYear.StartingBalance + irmaaYear.InvestmentReturn
                - withoutIrmaa - irmaaYear.EndingBalance;
            Assert.Equal(irmaaYear.IrmaaPaid, gapWithoutIrmaa);
        }
    }
}
