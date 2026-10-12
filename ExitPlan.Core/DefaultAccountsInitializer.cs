namespace ExitPlan.Core
{
    public static class DefaultAccountsInitializer
    {
        public static List<Account> CreateDefaultAccountsFor(Guid ownerId, string ownerName, int startingPriority)
        {
            var accounts = new List<Account>();

            string[] taxBuckets = { "Tax Free", "Tax Deferred", "Taxable" };
            string[] assetClasses = { "Equities", "Bonds", "Cash" };

            int priority = startingPriority;

            foreach (var bucket in taxBuckets)
            {
                foreach (var asset in assetClasses)
                {
                    accounts.Add(new Account
                    {
                        OwnerId = ownerId,
                        Owner = ownerName,
                        TaxBucket = bucket,
                        AssetClass = asset,
                        Value = 0m,
                        CostBasis = 0m,
                        RateOfReturn = asset switch
                        {
                            "Equities" => 0.07m,
                            "Bonds" => 0.03m,
                            "Cash" => 0.00m,
                            _ => 0.00m
                        },
                        Priority = priority++,
                        Bucket = 1
                    });
                }
            }

            return accounts;
        }
    }


}

