using System.Linq;
using System.Collections.Generic;

namespace ExitPlan.Core.Taxes
{
    public sealed class ConfigIncomeTaxStrategy : IIncomeTaxStrategy
    {
        public TaxComputationResult Compute(TaxComputationInput input, TaxPolicy policy)
        {
            var table = input.FilingStatus == FilingStatus.Single
                ? policy.Single
                : policy.MarriedFilingJointly;

            // 1) Taxable Social Security
            var taxableSs = SocialSecurityTaxHelper.ComputeTaxableSocialSecurity(
                input.SocialSecurityBenefits,
                input.OrdinaryIncome,
                input.FilingStatus,
                policy,
                input.CapitalGains);

            // 2) Ordinary income after standard deduction (base + IRC §63(f)
            //    extra, if configured); leftover deduction can shelter capital gains.
            var ordinaryBeforeDeduction = input.OrdinaryIncome + taxableSs;
            var standardDeduction = policy.EffectiveStandardDeduction(
                input.FilingStatus,
                input.LivingOwnerAges);
            var leftoverDeduction = standardDeduction - ordinaryBeforeDeduction;
            decimal ordinaryBase;
            decimal taxableCapitalGains;

            if (leftoverDeduction > 0)
            {
                ordinaryBase = 0m;
                taxableCapitalGains = Math.Max(0m, input.CapitalGains - leftoverDeduction);
            }
            else
            {
                ordinaryBase = -leftoverDeduction;
                taxableCapitalGains = Math.Max(0m, input.CapitalGains);
            }

            // 3) Ordinary tax (and per-bracket fill / conversion headroom)
            var ordinaryAllocation = OrdinaryBracketAllocator.Allocate(
                ordinaryBase,
                table.OrdinaryBrackets);
            var ordinaryTax = ordinaryAllocation.OrdinaryTax;

            // 4) Capital gains tax (stacked on top of ordinary taxable income)
            var capitalGainsTax = ApplyCapitalGainsTax(
                ordinaryBase,
                taxableCapitalGains,
                table.CapitalGainsBrackets);

            // 5) MAGI uses taxable SS, not gross SS. IRMAA is assessed here
            //    but paid as year N+2 cash (not part of this year's federal bill).
            var magi = input.OrdinaryIncome + input.CapitalGains + taxableSs;
            var irmaaAnnual = ComputeIrmaaAnnual(magi, table.IrmaaBrackets);

            return new TaxComputationResult
            {
                FederalTax = ordinaryTax + capitalGainsTax,
                TaxableOrdinaryIncome = ordinaryBase,
                TaxableCapitalGains = taxableCapitalGains,
                TaxableSocialSecurity = taxableSs,
                IrmaaSurcharge = irmaaAnnual,
                Magi = magi,
                OrdinaryBracketFills = ordinaryAllocation.Fills,
                OrdinaryBracketHeadroom = ordinaryAllocation.Headroom
            };
        }

        private static decimal ApplyCapitalGainsTax(
            decimal ordinaryTaxable,
            decimal capitalGains,
            List<TaxBracket> cgBrackets)
        {
            if (capitalGains <= 0)
                return 0m;

            decimal tax = 0m;
            decimal remaining = capitalGains;
            decimal incomeSoFar = ordinaryTaxable;

            foreach (var bracket in cgBrackets.OrderBy(b => b.Threshold))
            {
                if (remaining <= 0)
                    break;

                var roomInBracket = bracket.Threshold - incomeSoFar;
                if (roomInBracket <= 0)
                {
                    incomeSoFar = bracket.Threshold;
                    continue;
                }

                var span = System.Math.Min(remaining, roomInBracket);
                tax += span * bracket.Rate;

                remaining -= span;
                incomeSoFar += span;
            }

            if (remaining > 0 && cgBrackets.Count > 0)
            {
                var lastRate = cgBrackets.OrderBy(b => b.Threshold).Last().Rate;
                tax += remaining * lastRate;
            }

            return tax;
        }

        private static decimal ComputeIrmaaAnnual(decimal magi, List<IrmaaBracket> irmaaBrackets)
        {
            if (irmaaBrackets == null || irmaaBrackets.Count == 0)
                return 0m;

            decimal monthly = 0m;

            foreach (var bracket in irmaaBrackets.OrderBy(b => b.Threshold))
            {
                monthly = bracket.MonthlySurcharge;

                if (magi <= bracket.Threshold)
                    break;
            }

            return monthly * 12m;
        }
    }
}
