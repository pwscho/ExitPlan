using System.Globalization;

namespace ExitPlan.Core.Engine
{
    /// <summary>
    /// Moves dollars between Equities and Bonds so each owner, inside each tax bucket,
    /// matches the Scenario equity and bond allocations. Cash is not part of the sleeve.
    /// </summary>
    public class RebalancingEngine
    {
        /// <summary>
        /// Allocations are fractions (0.60 = 60%). A complement written as 1 - equity
        /// still passes; 60% + 30% does not.
        /// </summary>
        public const decimal AllocationSumTolerance = 0.0000001m;

        private static readonly string[] TaxBuckets = { "Tax Free", "Tax Deferred", "Taxable" };

        public void Rebalance(List<Account> accounts, Scenario scenario)
        {
            ArgumentNullException.ThrowIfNull(accounts);
            ArgumentNullException.ThrowIfNull(scenario);

            EnsureAllocationsSumToOne(scenario);

            foreach (var ownerId in accounts.Select(a => a.OwnerId).Distinct())
            {
                foreach (var bucket in TaxBuckets)
                {
                    var equity = Find(accounts, ownerId, bucket, "Equities");
                    var bonds = Find(accounts, ownerId, bucket, "Bonds");
                    if (equity == null || bonds == null)
                        continue;

                    decimal sleeve = equity.Value + bonds.Value;
                    if (sleeve <= 0m)
                        continue;

                    decimal targetEquity = sleeve * scenario.TargetEquityPercentage;
                    decimal targetBond = sleeve * scenario.TargetBondPercentage;

                    // Keep the sleeve total unchanged if the two products drift by a fraction of a cent.
                    decimal allocated = targetEquity + targetBond;
                    if (allocated != sleeve)
                        targetBond += sleeve - allocated;

                    MoveTowardTargets(equity, bonds, targetEquity, targetBond, adjustCostBasis: bucket == "Taxable");
                }
            }
        }

        /// <summary>
        /// The Scenario tab treats the two boxes as a pair that sums to 100%.
        /// Reject any other pair so the bond box cannot be stored and then ignored.
        /// </summary>
        public static void EnsureAllocationsSumToOne(Scenario scenario)
        {
            ArgumentNullException.ThrowIfNull(scenario);

            decimal equity = scenario.TargetEquityPercentage;
            decimal bond = scenario.TargetBondPercentage;
            bool inRange = equity >= 0m && equity <= 1m && bond >= 0m && bond <= 1m;
            bool sumsToOne = Math.Abs(equity + bond - 1m) <= AllocationSumTolerance;

            if (inRange && sumsToOne)
                return;

            throw new InvalidOperationException(
                "Equity Allocation and Bond Allocation must each be between 0% and 100% and sum to 100%. " +
                "Cash is not rebalanced. " +
                $"Current equity is {FormatPercent(equity)} and bonds are {FormatPercent(bond)}.");
        }

        private static Account? Find(List<Account> accounts, Guid ownerId, string bucket, string assetClass) =>
            accounts.FirstOrDefault(a =>
                a.OwnerId == ownerId &&
                (a.TaxBucket ?? "").Trim() == bucket &&
                (a.AssetClass ?? "").Trim() == assetClass);

        private static void MoveTowardTargets(
            Account equity,
            Account bonds,
            decimal targetEquity,
            decimal targetBond,
            bool adjustCostBasis)
        {
            decimal moveToEquity = targetEquity - equity.Value;
            decimal moveToBonds = targetBond - bonds.Value;

            if (moveToEquity > 0m)
                Transfer(bonds, equity, moveToEquity, adjustCostBasis);
            else if (moveToBonds > 0m)
                Transfer(equity, bonds, moveToBonds, adjustCostBasis);
        }

        /// <summary>
        /// Taxable sleeves carry unrealized gain. Move cost basis with the dollars so a later
        /// withdrawal does not treat the whole transfer as a gain or leave basis behind on a
        /// smaller position. This is not a sale: gains stay unrealized until a withdrawal.
        /// </summary>
        private static void Transfer(Account source, Account destination, decimal amount, bool adjustCostBasis)
        {
            if (amount <= 0m || source.Value <= 0m)
                return;

            if (amount > source.Value)
                amount = source.Value;

            decimal basisMoved = 0m;
            if (adjustCostBasis && source.CostBasis > 0m)
            {
                basisMoved = amount >= source.Value
                    ? source.CostBasis
                    : amount * source.CostBasis / source.Value;
            }

            source.Value -= amount;
            destination.Value += amount;

            if (!adjustCostBasis || basisMoved == 0m)
                return;

            source.CostBasis -= basisMoved;
            destination.CostBasis += basisMoved;
        }

        private static string FormatPercent(decimal ratio) =>
            (ratio * 100m).ToString("0.##", CultureInfo.InvariantCulture) + "%";
    }
}

