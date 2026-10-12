using ExitPlan.Core;
using ExitPlan.Core.Services;
using ExitPlan.Core.Strategies;
using ExitPlan.Core.Taxes;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;


namespace ExitPlan.Core.Engine
{


    public class ComputeEngine
    {
        private readonly Scenario _scenario;
        private readonly List<Account> _accounts;
        private readonly IMarketService _market;
        private readonly SocialSecuritySettings _primary;
        private readonly SocialSecuritySettings? _spouse;

        private readonly IWithdrawalStrategy _strategy = new DefaultWithdrawalStrategy();
        private readonly IIncomeTaxStrategy _taxStrategy;
        private readonly IRothConversionWithdrawalStrategy _conversionStrategy;

        private readonly GrowthEngine _growth;
        private readonly SpendingEngine _spending;
        private readonly RebalancingEngine _rebalancing = new();

        private TaxYearAccumulator _tax = new TaxYearAccumulator();
        private decimal _nextYearTaxBill = 0m;
        decimal cumulativeBracketInflation = 1m;
        private readonly TaxBracketInflationEngine _taxBracketInflation;



        public ComputeEngine(
            Scenario scenario,
            List<Account> accounts,
            IMarketService market,
            SocialSecuritySettings primary,
            SocialSecuritySettings? spouse,
            IIncomeTaxStrategy? taxStrategy = null)
        {
            _scenario = scenario;
            _accounts = accounts;
            _market = market;
            _primary = primary;
            _spouse = spouse;

            _taxStrategy = taxStrategy ?? new ConfigIncomeTaxStrategy();
            _conversionStrategy = new RothConversionWithdrawalStrategy(_strategy);

            _growth = new GrowthEngine(market);
            _spending = new SpendingEngine(market);

            
            _taxBracketInflation = new TaxBracketInflationEngine(market);
        }




        public SimulationOutput Run(int simNumber, string simulation)
        {
            int years = _scenario.YearsToCalculate;
            decimal baseSpending = _scenario.AnnualSpending;
            decimal spendingNeed = 0;

            var output = new SimulationOutput();

            // Load RMD policy once
            var rmdPolicy = RmdPolicyLoader.LoadForYear(_scenario.BaseYear);

            // Working accounts are the ONLY balances
            var workingAccounts = _accounts
                .Select(a => a.DeepCopy())
                .ToList();

            decimal nextYearTaxBill = _scenario.TaxesFromLastYear;

            // NEW: cumulative tax bracket inflation multiplier
            decimal cumulativeBracketInflation = 1m;

            decimal startingPortfolio = workingAccounts.Sum(a => a.Value);
            if (_scenario.Guardrails.InitialPortfolioValue <= 0m)
            {
                _scenario.Guardrails.InitialPortfolioValue = startingPortfolio;
            }
            if (_scenario.InitialPortfolioValue <= 0m)
            {
                _scenario.InitialPortfolioValue = _scenario.Guardrails.InitialPortfolioValue > 0m
                    ? _scenario.Guardrails.InitialPortfolioValue
                    : startingPortfolio;
            }

            decimal inflationOnlySpending = _scenario.AnnualSpending;

            // Prior years' spending-inflation rates, used only when the guardrail
            // baseline compounds. Empty unless that option is on, so an off run
            // never reads the market path.
            var baselineInflationPath = new List<decimal>();

            var irmaaAssessedByYear = new decimal[years];

            for (int year = 0; year < years; year++)
            {

                _tax = new TaxYearAccumulator();
                var deferredWithdrawnThisYear = new Dictionary<Guid, decimal>();

                int calendarYear = _scenario.BaseYear + year;

                // Capture starting values (RMD base is start-of-year Tax Deferred)
                var startingValues = workingAccounts
                    .Select(a => a.Value)
                    .ToList();

                decimal startingBalance = workingAccounts.Sum(a => a.Value);

                var startOfYearDeferredByOwner = workingAccounts
                    .Where(a => a.TaxBucket.Trim() == "Tax Deferred")
                    .GroupBy(a => a.OwnerId)
                    .ToDictionary(g => g.Key, g => g.Sum(a => a.Value));

                var startOfYearDeferredByAccount = workingAccounts
                    .Where(a => a.TaxBucket.Trim() == "Tax Deferred")
                    .ToDictionary(a => a.AccountId, a => a.Value);

                // 1. Pay last year's tax bill
                decimal taxesPaid = 0m;
                if (nextYearTaxBill > 0)
                {
                    var taxWithdrawals = WithdrawalEngine.Withdraw(
                        nextYearTaxBill,
                        _strategy,
                        _scenario,
                        workingAccounts,
                        _tax,
                        WithdrawalPurpose.TaxOrIrmaa);

                    TrackDeferredWithdrawals(taxWithdrawals, deferredWithdrawnThisYear);
                    taxesPaid = nextYearTaxBill;
                    nextYearTaxBill = 0m;
                }

                // 1b. IRMAA Paid (N+2 cash)
                decimal irmaaPaid = PayIrmaaForYear(
                    year,
                    irmaaAssessedByYear,
                    workingAccounts,
                    deferredWithdrawnThisYear);

                // 2. Deposit SS + Income
                decimal ssIncome = 0m;
                decimal otherIncome = 0m;

                DepositSocialSecurityAndIncome_Flat(
                    year,
                    workingAccounts,
                    ref ssIncome,
                    ref otherIncome);

                // 3. Spending need
                int primaryAge = _primary.CurrentAge + year;

                decimal _inflationrate = _scenario.InflationRate;
                var guardrailAction = GuardrailYearAction.None;
                decimal guardrailPortfolioPercent = PortfolioPercentOfBaseline(
                    startingBalance,
                    _scenario.Guardrails.InitialPortfolioValue);

                switch (_scenario.SpendingStrategy)
                {
                    case "Fixed":
                        _inflationrate = _scenario.InflationRate;
                        break;

                    case "Historical":
                        if (simulation == "fixed")
                        {
                            _inflationrate = _scenario.InflationRate;
                            break;
                        }
                        _inflationrate = _growth.GetInflation(year);
                        break;

                    case "Guardrails":
                        decimal baseline = GuardrailSettings.TriggerBaseline(
                            _scenario.Guardrails.InitialPortfolioValue,
                            _scenario.Guardrails.IncreaseBaselineWithInflation,
                            year,
                            baselineInflationPath);
                        var decision = ApplyGuardrails(
                            year,
                            startingBalance,
                            baseSpending,
                            _scenario,
                            baseline);
                        _inflationrate = decision.InflationRate;
                        guardrailAction = decision.Action;
                        guardrailPortfolioPercent = decision.PortfolioPercent;
                        break;
                }

                decimal addedSpending = 0;
                foreach (var w in _scenario.WithdrawalStreams)
                {
                    int ownerAge = GetAgeForOwner(w.OwnerId, year);
                    if (ownerAge >= w.StartAge && ownerAge <= w.EndAge)
                        addedSpending += w.AnnualAmount;
                }

                // 4. Withdraw for spending
                decimal withdrawals = 0m;

                spendingNeed = baseSpending + addedSpending;

                if (spendingNeed > 0)
                {
                    var w = WithdrawalEngine.Withdraw(
                        spendingNeed,
                        _strategy,
                        _scenario,
                        workingAccounts,
                        _tax,
                        WithdrawalPurpose.Spending);

                    TrackDeferredWithdrawals(w, deferredWithdrawnThisYear);
                    withdrawals = w.Sum(x => x.Amount);
                }

                // 4b. RMDs (include year 0; base is start-of-year Tax Deferred)
                decimal rmds = ApplyRmdsForYear_Flat(
                    year,
                    workingAccounts,
                    deferredWithdrawnThisYear,
                    rmdPolicy,
                    startOfYearDeferredByOwner,
                    startOfYearDeferredByAccount);

                // 4c. Roth Conversions
                decimal conversions = ApplyRothConversionsForYear_Flat(
                    year,
                    workingAccounts);

                // 4d. Inheritance
                ApplyInheritanceForYear(
                    year,
                    workingAccounts);

                // 5. Growth
                _growth.ApplyGrowth(workingAccounts, year);

                decimal endingBalance = workingAccounts.Sum(a => a.Value);

                // IRMAA is already withdrawn from the accounts. Add it back into
                // return and into net withdrawals so
                // start + return − account withdrawals = end, without changing balances.
                decimal investmentReturn = endingBalance - startingBalance
                                           + withdrawals
                                           - ssIncome
                                           - otherIncome
                                           + taxesPaid
                                           + irmaaPaid;

                decimal accountWithdrawals = withdrawals + taxesPaid + irmaaPaid
                                              - ssIncome - otherIncome;

                // 6. Rebalance (per owner, per tax bucket; cash stays put)
                if (_scenario.RebalanceAnnually)
                    _rebalancing.Rebalance(workingAccounts, _scenario);

                if (_scenario.EnableTaxBracketInflation)
                {
                    // -----------------------------------------------------
                    // 7. Compute Taxes (UPDATED WITH COMPOUNDING BRACKETS)
                    // -----------------------------------------------------

                    // Get this year's bracket inflation
                    decimal bracketInflation = _taxBracketInflation.GetBracketInflation(year, _scenario);

                    // Compound it
                    cumulativeBracketInflation *= (1 + bracketInflation);

                    //System.Diagnostics.Debug.WriteLine(
                    //    $"[DEBUG] Year={calendarYear}, RawInflation={bracketInflation:P2}, Cumulative={cumulativeBracketInflation:F6}");

                }


                // Load tax policy with inflation multiplier
                var livingOwnerAges = FilingStatusHelper.GetLivingOwnerAges(year, _primary, _spouse);
                var filingStatus = FilingStatusHelper.GetFilingStatusForYear(year, _primary, _spouse);

                var taxPolicy = TaxPolicyLoader.LoadForYear(
                    calendarYear,
                    cumulativeBracketInflation
                );

                   //             System.Diagnostics.Debug.WriteLine(
                   // $"[DEBUG] StdDeduction={taxPolicy.Single.StandardDeduction:F2}, " +
                   // $"FirstOrdBracket={taxPolicy.Single.OrdinaryBrackets[0].Threshold:F2}");


                var taxInput = new TaxComputationInput
                {
                    Year = calendarYear,
                    FilingStatus = filingStatus,
                    OrdinaryIncome = _tax.OrdinaryIncome,
                    CapitalGains = _tax.CapitalGains,
                    SocialSecurityBenefits = ssIncome,
                    LivingOwnerAges = livingOwnerAges
                };

                var taxResult = _taxStrategy.Compute(taxInput, taxPolicy);

                decimal perPersonIrmaa = taxResult.IrmaaSurcharge;
                decimal taxesDue = taxResult.FederalTax;

                nextYearTaxBill = taxesDue;
                irmaaAssessedByYear[year] = perPersonIrmaa;

                // 9. Per-account summaries
                for (int i = 0; i < workingAccounts.Count; i++)
                {
                    var acct = workingAccounts[i];
                    var start = startingValues[i];
                    var end = acct.Value;

                    output.Accounts.Add(new AccountYearSummary
                    {
                        Year = year + 1,
                        Age = _primary.CurrentAge + year,
                        Owner = acct.Owner,
                        TaxBucket = acct.TaxBucket,
                        AssetClass = acct.AssetClass,
                        StartingValue = start,
                        EndingValue = end,
                        InvestmentReturn = end - start
                    });
                }

                // 10. Add ResultRow
                output.Results.Add(new ResultRow
                {
                    SimNo = simNumber,
                    Iteration = year + 1,
                    Year = calendarYear,

                    Age = _primary.CurrentAge + year,
                    SpouseAge = _spouse == null ? 0 : _spouse.CurrentAge + year,

                    StartingBalance = startingBalance,
                    EndingBalance = endingBalance,

                    Withdrawal = withdrawals,
                    SocialSecurityIncome = ssIncome,
                    Income = otherIncome,
                    InvestmentReturn = investmentReturn,

                    TaxableIncome = taxResult.TaxableOrdinaryIncome + taxResult.TaxableCapitalGains,
                    OrdinaryIncome = taxResult.TaxableOrdinaryIncome,
                    CapitalGains = taxResult.TaxableCapitalGains,
                    FilingStatus = filingStatus,
                    OrdinaryBracketFills = taxResult.OrdinaryBracketFills?.ToList()
                        ?? new List<OrdinaryBracketFill>(),
                    OrdinaryBracketHeadroom = taxResult.OrdinaryBracketHeadroom,

                    TaxesDue = taxesDue,
                    TaxesPaid = taxesPaid,
                    IrmaaPaid = irmaaPaid,
                    Magi = taxResult.Magi,
                    Rmds = rmds,
                    Conversions = conversions,
                    BaseSpending = baseSpending,
                    AddedSpending = addedSpending,
                    InflationOnlyBaseSpending = inflationOnlySpending,
                    GuardrailPortfolioPercent = guardrailPortfolioPercent,
                    GuardrailAction = guardrailAction,

                    AccountWithdrawals = accountWithdrawals,

                    PercentWithdrawals = startingBalance == 0 ? 0 :
                        accountWithdrawals / startingBalance
                });

                baseSpending *= (1 + _inflationrate);
                inflationOnlySpending *= (1 + _scenario.InflationRate);

                if (string.Equals(_scenario.SpendingStrategy, "Guardrails", StringComparison.OrdinalIgnoreCase)
                    && _scenario.Guardrails.IncreaseBaselineWithInflation)
                {
                    baselineInflationPath.Add(InflationAlongSpendingPath(year, simulation));
                }
            }

            return output;
        }




        private void TrackDeferredWithdrawals(
            List<WithdrawalResult> withdrawals,
            Dictionary<Guid, decimal> deferredWithdrawnThisYear)
        {
            foreach (var w in withdrawals)
            {
                if (w.Bucket != TaxBucket.TaxDeferred)
                    continue;

                var ownerId = w.Account.OwnerId;

                if (!deferredWithdrawnThisYear.TryGetValue(ownerId, out var current))
                    current = 0m;
                
                deferredWithdrawnThisYear[ownerId] = current + w.Amount;

            }
        }

        private decimal PayIrmaaForYear(
            int yearIndex,
            decimal[] irmaaAssessedByYear,
            List<Account> workingAccounts,
            Dictionary<Guid, decimal> deferredWithdrawnThisYear)
        {
            if (yearIndex < 2)
                return 0m;

            decimal perPersonIrmaaFromN = irmaaAssessedByYear[yearIndex - 2];
            if (perPersonIrmaaFromN <= 0m)
                return 0m;

            int enrolleeCount = 0;

            int primaryAgeNow = _primary.CurrentAge + yearIndex;
            bool primaryAliveNow = primaryAgeNow <= _primary.DeathAge;
            if (primaryAliveNow && primaryAgeNow >= 65)
                enrolleeCount++;

            if (_spouse != null)
            {
                int spouseAgeNow = _spouse.CurrentAge + yearIndex;
                bool spouseAliveNow = spouseAgeNow <= _spouse.DeathAge;
                if (spouseAliveNow && spouseAgeNow >= 65)
                    enrolleeCount++;
            }

            decimal irmaaDueNow = perPersonIrmaaFromN * enrolleeCount;
            if (irmaaDueNow <= 0m)
                return 0m;

            var withdrawals = WithdrawalEngine.Withdraw(
                irmaaDueNow,
                _strategy,
                _scenario,
                workingAccounts,
                _tax,
                WithdrawalPurpose.TaxOrIrmaa);

            TrackDeferredWithdrawals(withdrawals, deferredWithdrawnThisYear);
            return irmaaDueNow;
        }

        private decimal ApplyRmdsForYear_Flat(
            int yearIndex,
            List<Account> workingAccounts,
            Dictionary<Guid, decimal> deferredWithdrawnThisYear,
            RmdPolicy policy,
            Dictionary<Guid, decimal> startOfYearDeferredByOwner,
            Dictionary<Guid, decimal> startOfYearDeferredByAccount)
        {
            if (policy == null)
                return 0m;

            decimal total = 0m;

            int primaryAge = _primary.CurrentAge + yearIndex;
            total += ApplyRmdForOwner_Flat(
                _primary.UserId,
                primaryAge,
                workingAccounts,
                deferredWithdrawnThisYear,
                policy,
                startOfYearDeferredByOwner,
                startOfYearDeferredByAccount);

            if (_spouse != null)
            {
                int spouseAge = _spouse.CurrentAge + yearIndex;
                total += ApplyRmdForOwner_Flat(
                    _spouse.UserId,
                    spouseAge,
                    workingAccounts,
                    deferredWithdrawnThisYear,
                    policy,
                    startOfYearDeferredByOwner,
                    startOfYearDeferredByAccount);
            }

            return total;
        }



        private decimal ApplyRmdForOwner_Flat(
            Guid ownerId,
            int ownerAgeThisYear,
            List<Account> workingAccounts,
            Dictionary<Guid, decimal> deferredWithdrawnThisYear,
            RmdPolicy policy,
            Dictionary<Guid, decimal> startOfYearDeferredByOwner,
            Dictionary<Guid, decimal> startOfYearDeferredByAccount)
        {
            if (policy == null)
                return 0m;

            int deathAge =
                ownerId == _primary.UserId ? _primary.DeathAge :
                (_spouse != null && ownerId == _spouse.UserId ? _spouse.DeathAge : int.MaxValue);

            int currentAge =
                ownerId == _primary.UserId ? _primary.CurrentAge :
                (_spouse != null && ownerId == _spouse.UserId ? _spouse.CurrentAge : 0);
            int startAge = RmdEngine.ResolveStartAge(policy, _scenario.BaseYear, currentAge);

            if (ownerAgeThisYear < startAge || ownerAgeThisYear > deathAge)
                return 0m;

            if (!policy.LifeExpectancyDivisors.TryGetValue(ownerAgeThisYear, out var divisor) ||
                divisor <= 0m)
                return 0m;

            var deferredAccounts = workingAccounts
                .Where(a =>
                    a.OwnerId == ownerId &&
                    a.TaxBucket.Trim() == "Tax Deferred")
                .ToList();

            if (deferredAccounts.Count == 0)
                return 0m;

            if (!startOfYearDeferredByOwner.TryGetValue(ownerId, out decimal startOfYearDeferred) ||
                startOfYearDeferred <= 0m)
                return 0m;

            decimal rmdAmount = startOfYearDeferred / divisor;
            if (rmdAmount <= 0m)
                return 0m;

            deferredWithdrawnThisYear.TryGetValue(ownerId, out decimal alreadyWithdrawn);

            decimal remainingRmd = Math.Max(0m, rmdAmount - alreadyWithdrawn);
            if (remainingRmd <= 0m)
                return 0m;

            var destination = FindRmdDestination(ownerId, workingAccounts);
            if (destination == null)
                return 0m;

            bool destinationIsTaxable = destination.TaxBucket.Trim() == "Taxable";
            decimal remaining = remainingRmd;

            foreach (var acct in deferredAccounts)
            {
                if (remaining <= 0m)
                    break;

                startOfYearDeferredByAccount.TryGetValue(acct.AccountId, out decimal startBalance);
                if (startBalance <= 0m)
                    continue;

                decimal share = remainingRmd * (startBalance / startOfYearDeferred);
                decimal toWithdraw = Math.Min(share, Math.Min(acct.Value, remaining));
                if (toWithdraw <= 0m)
                    continue;

                acct.Value -= toWithdraw;
                destination.Value += toWithdraw;
                if (destinationIsTaxable)
                    destination.CostBasis += toWithdraw;

                remaining -= toWithdraw;
            }

            decimal executedRmd = remainingRmd - remaining;
            if (executedRmd <= 0m)
                return 0m;

            _tax.OrdinaryIncome += executedRmd;
            return executedRmd;
        }

        private static Account? FindRmdDestination(Guid ownerId, List<Account> workingAccounts)
        {
            var taxableCash = workingAccounts.FirstOrDefault(a =>
                a.OwnerId == ownerId &&
                a.TaxBucket.Trim() == "Taxable" &&
                a.AssetClass.Trim() == "Cash");

            if (taxableCash != null)
                return taxableCash;

            return workingAccounts.FirstOrDefault(a =>
                a.OwnerId == ownerId &&
                a.TaxBucket.Trim() == "Tax Free" &&
                a.AssetClass.Trim() == "Cash");
        }



        private void DepositSocialSecurityAndIncome_Flat(
            int year,
            List<Account> workingAccounts,
            ref decimal ssIncome,
            ref decimal otherIncome)
        {
            // -----------------------------
            // PRIMARY SOCIAL SECURITY
            // -----------------------------
            int primaryAge = _primary.CurrentAge + year;
            bool primaryAlive = primaryAge <= _primary.DeathAge;

            // Compute primary benefit for this year (even if deceased)
            decimal primaryBenefitForSurvivor = 0m;
            int primaryClaimAge = SocialSecurityClaiming.Clamp(_primary.ClaimAge);
            if (primaryAge >= primaryClaimAge)
            {
                primaryBenefitForSurvivor = CalculateColaAdjustedBenefit(
                    currentAge: primaryAge,
                    claimAge: primaryClaimAge,
                    fraMonthly: _primary.BenefitAtFullRetirementAge,
                    colaRate: _primary.Cola);
            }

            // Pay primary benefit only if alive
            decimal primaryPaid = 0m;
            if (primaryAlive && primaryBenefitForSurvivor > 0)
            {
                var acct = workingAccounts.First(a =>
                    a.OwnerId == _primary.UserId &&
                    a.TaxBucket.Trim() == "Tax Free" &&
                    a.AssetClass.Trim() == "Cash");

                acct.Value += primaryBenefitForSurvivor;
                ssIncome += primaryBenefitForSurvivor;
                primaryPaid = primaryBenefitForSurvivor;
            }

            // -----------------------------
            // OTHER INCOME STREAMS
            // -----------------------------
            foreach (var income in _scenario.IncomeStreams)
            {
                int ownerAge = GetAgeForOwner(income.OwnerId, year);

                if (ownerAge >= income.StartAge && ownerAge <= income.EndAge)
                {
                    var acct = workingAccounts.First(a =>
                        a.OwnerId == income.OwnerId &&
                        a.TaxBucket.Trim() == "Tax Free" &&
                        a.AssetClass.Trim() == "Cash");

                    acct.Value += income.AnnualAmount;
                    otherIncome += income.AnnualAmount;

                    if (income.IsTaxable)
                        _tax.OrdinaryIncome += income.AnnualAmount;
                }
            }

            // -----------------------------
            // SPOUSE + SURVIVOR BENEFITS
            // -----------------------------
            if (_spouse != null)
            {
                int spouseAge = _spouse.CurrentAge + year;
                bool spouseAlive = spouseAge <= _spouse.DeathAge;

                // Compute spouse benefit for this year (even if deceased)
                decimal spouseBenefitForSurvivor = 0m;
                int spouseClaimAge = SocialSecurityClaiming.Clamp(_spouse.ClaimAge);
                if (spouseAge >= spouseClaimAge)
                {
                    spouseBenefitForSurvivor = CalculateColaAdjustedBenefit(
                        currentAge: spouseAge,
                        claimAge: spouseClaimAge,
                        fraMonthly: _spouse.BenefitAtFullRetirementAge,
                        colaRate: _spouse.Cola);
                }

                // Pay spouse benefit only if alive
                decimal spousePaid = 0m;
                if (spouseAlive && spouseBenefitForSurvivor > 0)
                {
                    var acct = workingAccounts.First(a =>
                        a.OwnerId == _spouse.UserId &&
                        a.TaxBucket.Trim() == "Tax Free" &&
                        a.AssetClass.Trim() == "Cash");

                    acct.Value += spouseBenefitForSurvivor;
                    ssIncome += spouseBenefitForSurvivor;
                    spousePaid = spouseBenefitForSurvivor;
                }

                // -----------------------------
                // SURVIVOR LOGIC
                // -----------------------------
                if (primaryAlive && !spouseAlive)
                {
                    // Primary survives → gets larger benefit
                    decimal survivorBenefit = Math.Max(primaryBenefitForSurvivor, spouseBenefitForSurvivor);

                    // Avoid double-paying if primary already got their own benefit
                    decimal extra = survivorBenefit - primaryPaid;
                    if (extra > 0)
                    {
                        var acct = workingAccounts.First(a =>
                            a.OwnerId == _primary.UserId &&
                            a.TaxBucket.Trim() == "Tax Free" &&
                            a.AssetClass.Trim() == "Cash");

                        acct.Value += extra;
                        ssIncome += extra;
                    }
                }
                else if (!primaryAlive && spouseAlive)
                {
                    // Spouse survives → gets larger benefit
                    decimal survivorBenefit = Math.Max(primaryBenefitForSurvivor, spouseBenefitForSurvivor);

                    // Avoid double-paying if spouse already got their own benefit
                    decimal extra = survivorBenefit - spousePaid;
                    if (extra > 0)
                    {
                        var acct = workingAccounts.First(a =>
                            a.OwnerId == _spouse.UserId &&
                            a.TaxBucket.Trim() == "Tax Free" &&
                            a.AssetClass.Trim() == "Cash");

                        acct.Value += extra;
                        ssIncome += extra;
                    }
                }
            }
        }


        private decimal CalculateColaAdjustedBenefit(
    int currentAge,
    int claimAge,
    decimal fraMonthly,
    decimal colaRate)
        {
            // FRA monthly → annual
            decimal fraAnnual = fraMonthly * 12m;

            // 1. COLA applies starting at age 62
            int yearsSince62 = Math.Max(0, currentAge - 62);
            decimal fraWithCola = fraAnnual * (decimal)Math.Pow((double)(1 + colaRate), yearsSince62);

            // 2. Early / Late claiming adjustment (claim age clamped to 62–70)
            decimal adjustment = SocialSecurityClaiming.AdjustmentFactor(claimAge);

            decimal adjustedAnnual = fraWithCola * (1 + adjustment);

            return Math.Max(0, adjustedAnnual);
        }




        private int GetAgeForOwner(Guid ownerId, int year)
        {
            if (ownerId == _primary.UserId)
                return _primary.CurrentAge + year;

            if (_spouse != null && ownerId == _spouse.UserId)
                return _spouse.CurrentAge + year;

            return 0;
        }

        private decimal CalculateBenefits(int claimage, decimal amount)
        {
            
            decimal fraAnnual = amount * 12m;

            decimal adjustment = SocialSecurityClaiming.AdjustmentFactor(claimage);

            decimal adjustedAnnual = fraAnnual * (1 + adjustment);

            return Math.Max(0, adjustedAnnual);
        }

        private List<AccountProjection> CreateProjectionsSkeleton(List<Account> accounts, int years)
        {
            var projections = new List<AccountProjection>();

            foreach (var acct in accounts)
            {
                var proj = new AccountProjection
                {
                    OwnerId = acct.OwnerId,
                    Name = acct.Owner,
                    TaxBucket = acct.TaxBucket.Trim(),
                    AssetClass = acct.AssetClass.Trim(),
                    StartingValue = acct.Value,
                    RateOfReturn = acct.RateOfReturn,
                    ValuesByYear = new List<decimal>(new decimal[years]),
                    InvestmentReturnByYear = new List<decimal>(new decimal[years])
                };

                projections.Add(proj);
            }

            return projections;
        }

        private decimal ApplyRothConversionsForYear_Flat(
            int yearIndex,
            List<Account> workingAccounts)
        {
            decimal total = 0m;

            foreach (var conv in _scenario.RothConversions)
            {
                int ownerAge = GetAgeForOwner(conv.OwnerId, yearIndex);
                if (ownerAge < conv.StartAge || ownerAge > conv.EndAge)
                    continue;

                if (conv.AnnualAmount <= 0)
                    continue;

                decimal actual = _conversionStrategy.Convert(
                    conv.OwnerId,
                    conv.AnnualAmount,
                    workingAccounts,
                    _tax,
                    null, // no SimulationResult
                    yearIndex);

                total += actual;
            }

            return total;
        }


        private void ApplyInheritanceForYear(
            int yearIndex,
            List<Account> workingAccounts)
        {
            int primaryAge = _primary.CurrentAge + yearIndex;
            int spouseAge = _spouse != null ? _spouse.CurrentAge + yearIndex : int.MaxValue;

            bool primaryDiedThisYear = primaryAge == _primary.DeathAge;
            bool spouseDiedThisYear = _spouse != null && spouseAge == _spouse.DeathAge;

            if (!primaryDiedThisYear && !spouseDiedThisYear)
                return;

            // Last-to-die / both-dead: do not transfer balances.
            bool spouseStillAlive = _spouse != null && spouseAge < _spouse.DeathAge;
            bool primaryStillAlive = primaryAge < _primary.DeathAge;

            if (primaryDiedThisYear && spouseDiedThisYear)
                return;

            if (primaryDiedThisYear && !spouseStillAlive)
                return;

            if (spouseDiedThisYear && !primaryStillAlive)
                return;

            Guid deceasedId;
            Guid survivorId;

            if (primaryDiedThisYear && spouseStillAlive)
            {
                deceasedId = _primary.UserId;
                survivorId = _spouse!.UserId;
            }
            else if (spouseDiedThisYear && primaryStillAlive)
            {
                deceasedId = _spouse!.UserId;
                survivorId = _primary.UserId;
            }
            else
            {
                return;
            }

            TransferAccountsOnDeath(deceasedId, survivorId, workingAccounts);
        }


        private readonly record struct GuardrailDecision(
            decimal InflationRate,
            GuardrailYearAction Action,
            decimal PortfolioPercent);

        private static decimal PortfolioPercentOfBaseline(decimal portfolioValue, decimal baseline)
        {
            if (baseline <= 0m)
                return 0m;
            return portfolioValue / baseline;
        }

        /// <summary>
        /// Inflation that grows the guardrail baseline, matching the inflation path spending uses.
        /// A fixed run uses <see cref="Scenario.InflationRate"/> every year.
        /// Historical and Monte Carlo runs use that year's rate on the market path, and the
        /// baseline compounds those actual rates rather than one rate raised to a power.
        /// Guardrail cut and bonus are not part of this path.
        /// </summary>
        private decimal InflationAlongSpendingPath(int yearIndex, string simulation)
        {
            if (string.Equals(simulation, "fixed", StringComparison.OrdinalIgnoreCase))
                return _scenario.InflationRate;

            return _market.GetInflationForYear(yearIndex);
        }

        private GuardrailDecision ApplyGuardrails(
    int year,
    decimal portfolioValue,
    decimal currentBaseSpending,
    Scenario scenario,
    decimal baseline)
        {
            var g = scenario.Guardrails;

            decimal initialSpending = scenario.AnnualSpending;
            decimal pct = PortfolioPercentOfBaseline(portfolioValue, baseline);

            if (baseline <= 0)
                return new GuardrailDecision(scenario.InflationRate, GuardrailYearAction.None, pct);

            // LOWER GUARDRAIL
            if (pct < g.LowerTrigger)
            {
                // Apply negative inflation cut to slow depletion
                decimal newSpending = currentBaseSpending * (1 + g.LowerCut);

                // Enforce minimum spending floor (nominal, or inflated with CPI)
                decimal minAllowed = initialSpending * g.MinSpendingFloor;
                if (g.InflateSpendingFloor)
                    minAllowed *= CumulativeInflationFactor(scenario.InflationRate, year);

                if (newSpending < minAllowed)
                    return new GuardrailDecision(
                        (minAllowed / currentBaseSpending) - 1m,
                        GuardrailYearAction.FloorHit,
                        pct);

                return new GuardrailDecision(g.LowerCut, GuardrailYearAction.Cut, pct);
            }

            // UPPER GUARDRAIL
            if (pct > g.UpperTrigger)
                return new GuardrailDecision(
                    scenario.InflationRate + g.UpperBonus,
                    GuardrailYearAction.Raise,
                    pct);

            // Normal inflation
            return new GuardrailDecision(scenario.InflationRate, GuardrailYearAction.None, pct);
        }

        /// <summary>
        /// Cumulative (1 + inflationRate)^year using scenario inflation, not guardrail cut/bonus.
        /// Year 0 is 1 (no growth yet), matching base-spending inflation applied at year end.
        /// </summary>
        private static decimal CumulativeInflationFactor(decimal inflationRate, int year)
        {
            decimal factor = 1m;
            for (int i = 0; i < year; i++)
                factor *= (1 + inflationRate);
            return factor;
        }


        private void TransferAccountsOnDeath(
                Guid deceasedId,
                Guid survivorId,
                List<Account> workingAccounts)
                //List<AccountProjection> projections)
        {
            foreach (var acct in workingAccounts.Where(a => a.OwnerId == deceasedId).ToList())
            {
                if (acct.Value <= 0m)
                    continue;

                // Find matching survivor account
                var survivorAcct = workingAccounts.FirstOrDefault(a =>
                    a.OwnerId == survivorId &&
                    a.TaxBucket.Trim() == acct.TaxBucket.Trim() &&
                    a.AssetClass.Trim() == acct.AssetClass.Trim());

                // Create survivor account if needed
                if (survivorAcct == null)
                {
                    survivorAcct = new Account
                    {
                        OwnerId = survivorId,
                        Owner = survivorId == _primary.UserId ? _primary.Owner : _spouse!.Owner,
                        TaxBucket = acct.TaxBucket,
                        AssetClass = acct.AssetClass,
                        Priority = acct.Priority,
                        Bucket = acct.Bucket,
                        RateOfReturn = acct.RateOfReturn,
                        Value = 0m,
                        CostBasis = 0m
                    };

                    workingAccounts.Add(survivorAcct);

                  //  projections.Add(new AccountProjection
                  //  {
                  //      OwnerId = survivorId,
                   //     Name = survivorAcct.Owner,
                   //     TaxBucket = survivorAcct.TaxBucket.Trim(),
                   //     AssetClass = survivorAcct.AssetClass.Trim(),
                   //     StartingValue = 0m,
                   //     RateOfReturn = survivorAcct.RateOfReturn,
                   //     ValuesByYear = new List<decimal>(new decimal[_scenario.YearsToCalculate]),
                  //      InvestmentReturnByYear = new List<decimal>(new decimal[_scenario.YearsToCalculate])
                  //  });
                }

                // Transfer logic by tax bucket
                if (acct.TaxBucket.Trim() == "Taxable")
                {
                    // Step-up in basis
                    survivorAcct.Value += acct.Value;
                    survivorAcct.CostBasis += acct.Value;
                }
                else
                {
                    // Tax Deferred or Tax Free
                    survivorAcct.Value += acct.Value;
                }

                // Zero out deceased account
                acct.Value = 0m;
                acct.CostBasis = 0m;
            }
        }





    }
}


