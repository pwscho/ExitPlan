using ExitPlan.Core;
using ExitPlan.Core.Engine;
using ExitPlan.Core.Services;
using ExitPlan.Core.Strategies;

namespace ExitPlan.Core.Tests
{
    public class RothAndRmdTests
    {
        [Fact]
        public void RothConversion_CreatesMatchingTaxFreeAccount_AndIsOrdinaryIncome()
        {
            var ownerId = Guid.NewGuid();
            var accounts = new List<Account>
            {
                new Account
                {
                    OwnerId = ownerId,
                    Owner = "Primary",
                    TaxBucket = "Tax Deferred",
                    AssetClass = "Equities",
                    Value = 25_000m,
                    Priority = 1
                }
            };

            var tax = new TaxYearAccumulator();
            var strategy = new ExitPlan.Core.Strategies.RothConversionWithdrawalStrategy(new DefaultWithdrawalStrategy());

            decimal converted = strategy.Convert(ownerId, 10_000m, accounts, tax, result: null!, yearIndex: 0);

            Assert.Equal(10_000m, converted);
            Assert.Equal(10_000m, tax.OrdinaryIncome);
            Assert.Equal(15_000m, accounts.Single(a => a.TaxBucket == "Tax Deferred").Value);

            var roth = accounts.Single(a => a.TaxBucket == "Tax Free" && a.AssetClass == "Equities");
            Assert.Equal(10_000m, roth.Value);
            Assert.Equal(ownerId, roth.OwnerId);
        }

        [Fact]
        public void Rmd_DoesNotSkipYearZero_AndUsesStartOfYearDeferred()
        {
            var (scenario, accounts, primary, spouse) = TestHousehold.Create(
                taxDeferred: 246_000m,
                taxFreeCash: 5_000m,
                taxableCash: 1_000m,
                spending: 0m,
                years: 1,
                currentAge: 75,
                deathAge: 90);

            var taxable = TestHousehold.Get(accounts, primary.UserId, "Taxable", "Cash");
            taxable.CostBasis = 1_000m;

            var engine = new ComputeEngine(scenario, accounts, new FixedMarketService(), primary, spouse);
            var output = engine.Run(1, "fixed");

            // Uniform lifetime divisor at 75 is 24.6 → 246,000 / 24.6 = 10,000.
            Assert.Equal(10_000m, output.Results[0].Rmds);

            var year1 = output.Accounts.Where(a => a.Year == 1).ToList();
            decimal deferredEnd = year1
                .Where(a => a.Owner == primary.Owner && a.TaxBucket.Trim() == "Tax Deferred")
                .Sum(a => a.EndingValue);
            decimal taxableCashEnd = year1
                .Where(a => a.Owner == primary.Owner && a.TaxBucket.Trim() == "Taxable" && a.AssetClass.Trim() == "Cash")
                .Sum(a => a.EndingValue);

            // Fixed-market growth still applies each account's RateOfReturn after the RMD.
            Assert.Equal(236_000m * 1.07m, deferredEnd);
            Assert.Equal(11_000m, taxableCashEnd);
        }
    }
}
