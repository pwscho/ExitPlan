using ExitPlan.Core;

namespace ExitPlan.Core.Tests
{
    internal static class TestHousehold
    {
        public static (Scenario scenario, List<Account> accounts, SocialSecuritySettings primary, SocialSecuritySettings? spouse)
            Create(
                decimal taxDeferred = 0m,
                decimal taxFreeCash = 50_000m,
                decimal taxableCash = 50_000m,
                decimal spending = 0m,
                int years = 3,
                int currentAge = 70,
                int deathAge = 90,
                bool married = false,
                int spouseAge = 68,
                int spouseDeathAge = 90)
        {
            var primary = new SocialSecuritySettings
            {
                Owner = "Primary",
                CurrentAge = currentAge,
                ClaimAge = 70,
                DeathAge = deathAge,
                BenefitAtFullRetirementAge = 0m,
                Cola = 0m
            };

            SocialSecuritySettings? spouse = null;
            if (married)
            {
                spouse = new SocialSecuritySettings
                {
                    Owner = "Spouse",
                    CurrentAge = spouseAge,
                    ClaimAge = 70,
                    DeathAge = spouseDeathAge,
                    BenefitAtFullRetirementAge = 0m,
                    Cola = 0m
                };
            }

            var scenario = new Scenario
            {
                Name = "Test",
                YearsToCalculate = years,
                BaseYear = 2026,
                AnnualSpending = spending,
                SpendingStrategy = "Fixed",
                InflationRate = 0.02m,
                EnableTaxBracketInflation = false,
                TaxesFromLastYear = 0m
            };

            var accounts = DefaultAccountsInitializer.CreateDefaultAccountsFor(primary.UserId, primary.Owner, 1);
            Set(accounts, primary.UserId, "Tax Deferred", "Equities", taxDeferred);
            Set(accounts, primary.UserId, "Tax Free", "Cash", taxFreeCash);
            Set(accounts, primary.UserId, "Taxable", "Cash", taxableCash);

            if (spouse != null)
            {
                accounts.AddRange(DefaultAccountsInitializer.CreateDefaultAccountsFor(spouse.UserId, spouse.Owner, 20));
                Set(accounts, spouse.UserId, "Tax Free", "Cash", 10_000m);
            }

            return (scenario, accounts, primary, spouse);
        }

        public static void Set(List<Account> accounts, Guid ownerId, string bucket, string asset, decimal value)
        {
            var acct = accounts.First(a =>
                a.OwnerId == ownerId &&
                a.TaxBucket.Trim() == bucket &&
                a.AssetClass.Trim() == asset);
            acct.Value = value;
            if (bucket == "Taxable")
                acct.CostBasis = value;
        }

        public static Account Get(List<Account> accounts, Guid ownerId, string bucket, string asset) =>
            accounts.First(a =>
                a.OwnerId == ownerId &&
                a.TaxBucket.Trim() == bucket &&
                a.AssetClass.Trim() == asset);
    }
}
