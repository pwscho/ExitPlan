using ExitPlan.Core;
using ExitPlan.Core.MarketData;
using ExitPlan.Core.Reporting;
using ExitPlan.Core.Simulation;
using System.Collections.Generic;
using System.Linq;

namespace ExitPlan.UI.Reporting
{
    public class ReportBuilder
    {
        public ReportData BuildReport(
            Scenario scenario,
            List<Account> accounts,
            SocialSecuritySettings primary,
            SocialSecuritySettings? spouse,
            MarketHistory history)
        {
            var data = new ReportData(scenario)
            {
                Accounts = accounts.Select(a => a.DeepCopy()).ToList(),
                Primary = primary.DeepCopy(),
                Spouse = spouse?.DeepCopy()
            };

            var runner = new SimulationRunner();
            int monteCarloCount = Math.Max(1, scenario.MonteCarloSimulations);

            // ------------------------------------------------------------
            // FIXED
            // ------------------------------------------------------------
            var fixedResults = runner.RunFixed(
                scenario,
                accounts,
                primary,
                spouse);

            data.FixedResults = fixedResults;

            // ------------------------------------------------------------
            // HISTORICAL
            // ------------------------------------------------------------
            var historicalResults = runner.RunHistorical(
                scenario,
                accounts,
                primary,
                spouse,
                history);

            data.HistoricalResults = historicalResults;

            // ------------------------------------------------------------
            // MONTE CARLO
            // ------------------------------------------------------------
            var monteCarloResults = runner.RunMonteCarlo(
                scenario,
                accounts,
                primary,
                spouse,
                history,
                monteCarloCount,
                scenario.MonteCarloSeed);

            data.MonteCarloResults = monteCarloResults;

            // ------------------------------------------------------------
            // REPORT CARD
            // ------------------------------------------------------------
            data.ReportCard = ReportCardEvaluator.Evaluate(data, accounts);

            return data;
        }
    }
}


