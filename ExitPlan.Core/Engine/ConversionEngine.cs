using ExitPlan.Core.Strategies;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExitPlan.Core.Engine
{
    public class ConversionEngine
    {
        private readonly IRothConversionWithdrawalStrategy _conversionStrategy;

        public ConversionEngine(IRothConversionWithdrawalStrategy conversionStrategy)
        {
            _conversionStrategy = conversionStrategy;
        }

        public void ApplyConversionsForYear(
            int yearIndex,
            Scenario scenario,
            List<Account> workingAccounts,
            SimulationResult result,
            TaxYearAccumulator tax,
            Func<Guid, int, int> getAgeForOwner)
        {
            foreach (var conv in scenario.RothConversions)
            {
                int ownerAge = getAgeForOwner(conv.OwnerId, yearIndex);

                if (ownerAge < conv.StartAge || ownerAge > conv.EndAge)
                    continue;

                if (conv.AnnualAmount <= 0)
                    continue;

                decimal actual = _conversionStrategy.Convert(
                    conv.OwnerId,
                    conv.AnnualAmount,
                    workingAccounts,
                    tax,
                    result,
                    yearIndex);

                result.ConversionsByYear[yearIndex] += actual;
            }
        }
    }
}


