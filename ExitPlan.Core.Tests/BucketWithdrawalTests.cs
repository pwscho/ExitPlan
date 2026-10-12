using System.Text.Json;
using ExitPlan.Core;
using ExitPlan.Core.Engine;
using ExitPlan.Core.Services;
using ExitPlan.Core.Strategies;

namespace ExitPlan.Core.Tests
{
    public class BucketWithdrawalTests
    {
        [Fact]
        public void NewAccount_DefaultsBucketToOne()
        {
            Assert.Equal(1, new Account().Bucket);
            Assert.Equal(1, DefaultAccountsInitializer.CreateDefaultAccountsFor(Guid.NewGuid(), "P", 1)[0].Bucket);
        }

        [Fact]
        public void Bucket_RoundTripsThroughDeepCopyAndJson_MissingDefaultsToOne()
        {
            var account = new Account
            {
                Name = "Brokerage",
                TaxBucket = "Taxable",
                AssetClass = "Equities",
                Priority = 4,
                Bucket = 2
            };

            var copy = account.DeepCopy();
            Assert.Equal(2, copy.Bucket);
            Assert.Equal(4, copy.Priority);

            var json = JsonSerializer.Serialize(new ScenarioState
            {
                Accounts = [account]
            });
            Assert.Contains("\"Bucket\":2", json);

            var loaded = JsonSerializer.Deserialize<ScenarioState>(json);
            Assert.NotNull(loaded);
            Assert.Equal(2, loaded!.Accounts[0].Bucket);

            var legacy = JsonSerializer.Deserialize<Account>("""{"Priority":7,"TaxBucket":"Taxable"}""");
            Assert.NotNull(legacy);
            Assert.Equal(1, legacy!.Bucket);
            Assert.Equal(7, legacy.Priority);
        }

        [Fact]
        public void Spending_DefaultBucketOne_PreservesPriorityThenTaxRankOrder()
        {
            var deferred = Acct("Tax Deferred", "Equities", 10_000m, priority: 2);
            var taxable = Acct("Taxable", "Cash", 10_000m, priority: 2);
            var taxFree = Acct("Tax Free", "Cash", 10_000m, priority: 3);
            var first = Acct("Taxable", "Equities", 10_000m, priority: 1);

            var order = Order(WithdrawalPurpose.Spending, first, deferred, taxable, taxFree);

            Assert.Equal(new[] { first.AccountId, taxable.AccountId, deferred.AccountId, taxFree.AccountId }, order);
        }

        [Fact]
        public void Spending_LowerBucketUsedBeforeHigherBucket_EvenIfHigherHasBetterPriority()
        {
            var cash = Acct("Taxable", "Cash", 8_000m, priority: 99, bucket: 1);
            var brokerage = Acct("Taxable", "Equities", 8_000m, priority: 1, bucket: 2);

            var withdrawals = Withdraw(WithdrawalPurpose.Spending, 5_000m, cash, brokerage);

            Assert.Equal(5_000m, SingleAmount(withdrawals, cash));
            Assert.DoesNotContain(withdrawals, w => w.Account.AccountId == brokerage.AccountId);
            Assert.Equal(3_000m, cash.Value);
            Assert.Equal(8_000m, brokerage.Value);
        }

        [Fact]
        public void Spending_WithinBucket_LowerPriorityWins()
        {
            var later = Acct("Taxable", "Cash", 10_000m, priority: 5, bucket: 2);
            var earlier = Acct("Tax Deferred", "Equities", 10_000m, priority: 2, bucket: 2);

            var order = Order(WithdrawalPurpose.Spending, later, earlier);

            Assert.Equal(new[] { earlier.AccountId, later.AccountId }, order);
        }

        [Fact]
        public void Spending_EmptyBucketFallsThroughToNextBucket()
        {
            var empty = Acct("Taxable", "Cash", 0m, priority: 1, bucket: 1);
            var next = Acct("Taxable", "Equities", 9_000m, priority: 9, bucket: 2);

            var withdrawals = Withdraw(WithdrawalPurpose.Spending, 4_000m, empty, next);

            Assert.Equal(4_000m, SingleAmount(withdrawals, next));
            Assert.Equal(5_000m, next.Value);
        }

        [Fact]
        public void TaxAndIrmaa_SkipTaxFreeWhenAnotherAccountCanPay()
        {
            var roth = Acct("Tax Free", "Cash", 20_000m, priority: 1, bucket: 1);
            var taxable = Acct("Taxable", "Cash", 20_000m, priority: 9, bucket: 2);

            var taxOrder = Order(WithdrawalPurpose.TaxOrIrmaa, roth, taxable);
            Assert.Equal(new[] { taxable.AccountId, roth.AccountId }, taxOrder);

            var spendingOrder = Order(WithdrawalPurpose.Spending, roth, taxable);
            Assert.Equal(new[] { roth.AccountId, taxable.AccountId }, spendingOrder);

            var taxWithdrawals = Withdraw(WithdrawalPurpose.TaxOrIrmaa, 5_000m, roth, taxable);
            Assert.Equal(5_000m, SingleAmount(taxWithdrawals, taxable));
            Assert.DoesNotContain(taxWithdrawals, w => w.Account.AccountId == roth.AccountId);
            Assert.Equal(20_000m, roth.Value);
            Assert.Equal(15_000m, taxable.Value);
        }

        [Fact]
        public void TaxAndIrmaa_UseTaxFreeOnlyWhenNothingElseCanPay()
        {
            var roth = Acct("Tax Free", "Cash", 12_000m, priority: 1, bucket: 1);
            var emptyTaxable = Acct("Taxable", "Cash", 0m, priority: 2, bucket: 1);

            var withdrawals = Withdraw(WithdrawalPurpose.TaxOrIrmaa, 4_000m, roth, emptyTaxable);

            Assert.Equal(4_000m, SingleAmount(withdrawals, roth));
            Assert.Equal(8_000m, roth.Value);
        }

        [Fact]
        public void ComputeEngine_TaxPayment_DoesNotTakeRothWhenTaxableCanPay()
        {
            var (scenario, accounts, primary, spouse) = TestHousehold.Create(
                taxDeferred: 0m,
                taxFreeCash: 20_000m,
                taxableCash: 20_000m,
                spending: 0m,
                years: 1,
                currentAge: 60,
                deathAge: 90);

            foreach (var account in accounts)
                account.RateOfReturn = 0m;

            var roth = TestHousehold.Get(accounts, primary.UserId, "Tax Free", "Cash");
            var taxable = TestHousehold.Get(accounts, primary.UserId, "Taxable", "Cash");
            roth.Bucket = 1;
            roth.Priority = 1;
            taxable.Bucket = 2;
            taxable.Priority = 9;
            scenario.TaxesFromLastYear = 5_000m;

            var output = new ComputeEngine(scenario, accounts, new FixedMarketService(), primary, spouse)
                .Run(1, "fixed");

            var year1 = output.Accounts.Where(a => a.Year == 1).ToList();
            decimal rothEnd = year1
                .Where(a => a.Owner == primary.Owner && a.TaxBucket.Trim() == "Tax Free" && a.AssetClass.Trim() == "Cash")
                .Sum(a => a.EndingValue);
            decimal taxableEnd = year1
                .Where(a => a.Owner == primary.Owner && a.TaxBucket.Trim() == "Taxable" && a.AssetClass.Trim() == "Cash")
                .Sum(a => a.EndingValue);

            Assert.Equal(20_000m, rothEnd);
            Assert.Equal(15_000m, taxableEnd);
            Assert.Equal(5_000m, output.Results[0].TaxesPaid);
        }

        [Fact]
        public void ComputeEngine_Spending_TakesBucketOneCashBeforeBucketTwoBrokerage()
        {
            var (scenario, accounts, primary, spouse) = TestHousehold.Create(
                taxDeferred: 0m,
                taxFreeCash: 0m,
                taxableCash: 10_000m,
                spending: 4_000m,
                years: 1,
                currentAge: 60,
                deathAge: 90);

            foreach (var account in accounts)
                account.RateOfReturn = 0m;

            var cash = TestHousehold.Get(accounts, primary.UserId, "Taxable", "Cash");
            var brokerage = TestHousehold.Get(accounts, primary.UserId, "Taxable", "Equities");
            TestHousehold.Set(accounts, primary.UserId, "Taxable", "Equities", 10_000m);
            cash.Bucket = 1;
            cash.Priority = 9;
            brokerage.Bucket = 2;
            brokerage.Priority = 1;

            var output = new ComputeEngine(scenario, accounts, new FixedMarketService(), primary, spouse)
                .Run(1, "fixed");

            var year1 = output.Accounts.Where(a => a.Year == 1).ToList();
            decimal cashEnd = year1
                .Where(a => a.Owner == primary.Owner && a.TaxBucket.Trim() == "Taxable" && a.AssetClass.Trim() == "Cash")
                .Sum(a => a.EndingValue);
            decimal brokerageEnd = year1
                .Where(a => a.Owner == primary.Owner && a.TaxBucket.Trim() == "Taxable" && a.AssetClass.Trim() == "Equities")
                .Sum(a => a.EndingValue);

            Assert.Equal(6_000m, cashEnd);
            Assert.Equal(10_000m, brokerageEnd);
            Assert.Equal(4_000m, output.Results[0].Withdrawal);
        }

        [Fact]
        public void Rmd_DoesNotSortBySpendingBucket_AndLeftoverGoesToTaxableCash()
        {
            var (scenario, accounts, primary, spouse) = TestHousehold.Create(
                taxDeferred: 123_000m,
                taxFreeCash: 5_000m,
                taxableCash: 1_000m,
                spending: 0m,
                years: 1,
                currentAge: 75,
                deathAge: 90);

            foreach (var account in accounts)
                account.RateOfReturn = 0m;

            var deferredEq = TestHousehold.Get(accounts, primary.UserId, "Tax Deferred", "Equities");
            var deferredBonds = TestHousehold.Get(accounts, primary.UserId, "Tax Deferred", "Bonds");
            TestHousehold.Set(accounts, primary.UserId, "Tax Deferred", "Bonds", 123_000m);
            deferredEq.Bucket = 99;
            deferredEq.Priority = 9;
            deferredBonds.Bucket = 1;
            deferredBonds.Priority = 1;

            var taxable = TestHousehold.Get(accounts, primary.UserId, "Taxable", "Cash");
            taxable.Bucket = 50;

            var output = new ComputeEngine(scenario, accounts, new FixedMarketService(), primary, spouse)
                .Run(1, "fixed");

            // 246,000 / 24.6 = 10,000 RMD, split by start-of-year deferred balances, not buckets.
            Assert.Equal(10_000m, output.Results[0].Rmds);

            var year1 = output.Accounts.Where(a => a.Year == 1).ToList();
            decimal eqEnd = year1
                .Where(a => a.Owner == primary.Owner && a.TaxBucket.Trim() == "Tax Deferred" && a.AssetClass.Trim() == "Equities")
                .Sum(a => a.EndingValue);
            decimal bondsEnd = year1
                .Where(a => a.Owner == primary.Owner && a.TaxBucket.Trim() == "Tax Deferred" && a.AssetClass.Trim() == "Bonds")
                .Sum(a => a.EndingValue);
            decimal taxableCashEnd = year1
                .Where(a => a.Owner == primary.Owner && a.TaxBucket.Trim() == "Taxable" && a.AssetClass.Trim() == "Cash")
                .Sum(a => a.EndingValue);

            Assert.Equal(118_000m, eqEnd);
            Assert.Equal(118_000m, bondsEnd);
            Assert.Equal(11_000m, taxableCashEnd);
        }

        [Fact]
        public void RothConversion_IgnoresSpendingBucket_UsesPriorityOnDeferred()
        {
            var ownerId = Guid.NewGuid();
            var lowPriorityLowBucket = new Account
            {
                OwnerId = ownerId,
                Owner = "Primary",
                TaxBucket = "Tax Deferred",
                AssetClass = "Equities",
                Value = 20_000m,
                Priority = 5,
                Bucket = 1
            };
            var highPriorityHighBucket = new Account
            {
                OwnerId = ownerId,
                Owner = "Primary",
                TaxBucket = "Tax Deferred",
                AssetClass = "Bonds",
                Value = 20_000m,
                Priority = 1,
                Bucket = 9
            };

            var tax = new TaxYearAccumulator();
            var strategy = new RothConversionWithdrawalStrategy(new DefaultWithdrawalStrategy());
            decimal converted = strategy.Convert(
                ownerId,
                6_000m,
                [lowPriorityLowBucket, highPriorityHighBucket],
                tax,
                result: null!,
                yearIndex: 0);

            Assert.Equal(6_000m, converted);
            Assert.Equal(20_000m, lowPriorityLowBucket.Value);
            Assert.Equal(14_000m, highPriorityHighBucket.Value);
        }

        [Fact]
        public void ProductInfo_IsVersion23()
        {
            Assert.Equal("2.3", ProductInfo.Version);
            Assert.Equal("ExitPlan 2.3", ProductInfo.Title);
        }

        private static Account Acct(
            string taxBucket,
            string asset,
            decimal value,
            int priority,
            int bucket = 1)
        {
            return new Account
            {
                Owner = "Primary",
                TaxBucket = taxBucket,
                AssetClass = asset,
                Value = value,
                CostBasis = taxBucket == "Taxable" ? value : 0m,
                Priority = priority,
                Bucket = bucket
            };
        }

        private static Guid[] Order(WithdrawalPurpose purpose, params Account[] accounts)
        {
            var strategy = new DefaultWithdrawalStrategy();
            return strategy
                .GetWithdrawalOrder(new Scenario(), accounts.ToList(), purpose)
                .Select(a => a.AccountId)
                .ToArray();
        }

        private static List<WithdrawalResult> Withdraw(
            WithdrawalPurpose purpose,
            decimal amount,
            params Account[] accounts)
        {
            return WithdrawalEngine.Withdraw(
                amount,
                new DefaultWithdrawalStrategy(),
                new Scenario(),
                accounts.ToList(),
                new TaxYearAccumulator(),
                purpose);
        }

        private static decimal SingleAmount(List<WithdrawalResult> withdrawals, Account account)
        {
            return Assert.Single(withdrawals, w => w.Account.AccountId == account.AccountId).Amount;
        }
    }
}
