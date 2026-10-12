using ExitPlan.Core.Engine;
using ExitPlan.Core.MarketData;
using ExitPlan.Core.Services;
using System.Diagnostics;

namespace ExitPlan.Core.Simulation
{
    public class SimulationRunner
    {
        public List<ResultRow> RunFixed(
            Scenario scenario,
            List<Account> accounts,
            SocialSecuritySettings primary,
            SocialSecuritySettings? spouse)
        {
            var market = new FixedMarketService();

            var engine = new ComputeEngine(
                scenario,
                accounts.Select(a => a.DeepCopy()).ToList(),
                market,
                primary,
                spouse);

            var output = engine.Run(simNumber: 1, "fixed");
            return output.Results;
        }

        public List<ResultRow> RunHistorical(
            Scenario scenario,
            List<Account> accounts,
            SocialSecuritySettings primary,
            SocialSecuritySettings? spouse,
            MarketHistory history)
        {
            var list = new List<ResultRow>();

            int yearsNeeded = scenario.YearsToCalculate;
            int maxStartIndex = history.Years.Count - yearsNeeded;

            int simNumber = 1;

            for (int startIndex = 0; startIndex <= maxStartIndex; startIndex++)
            {
                var market = new HistoricalMarketService(history, startIndex);

                var engine = new ComputeEngine(
                    scenario,
                    accounts.Select(a => a.DeepCopy()).ToList(),
                    market,
                    primary,
                    spouse);

                var output = engine.Run(simNumber, "historical");
                list.AddRange(output.Results);

                simNumber++;
            }

            int successes = list
                .GroupBy(r => r.SimNo)
                .Count(g => SimulationSuccess.IsSuccessful(g));
            Debug.Print($"Successes: {successes}");
            return list;
        }

        /// <param name="seed">
        /// Optional override for <see cref="Scenario.MonteCarloSeed"/>.
        /// Simulation N derives its path from (seed, N) so the batch is reproducible
        /// without storing a seed per simulation. Year draws are independent of
        /// spending and balances (see <see cref="MonteCarloMarketService"/>).
        /// </param>
        public List<ResultRow> RunMonteCarlo(
            Scenario scenario,
            List<Account> accounts,
            SocialSecuritySettings primary,
            SocialSecuritySettings? spouse,
            MarketHistory history,
            int simulations,
            int? seed = null)
        {
            var list = new List<ResultRow>();
            int batchSeed = seed ?? scenario.MonteCarloSeed;
            int count = Math.Max(1, simulations);

            for (int sim = 1; sim <= count; sim++)
            {
                int simSeed = unchecked(batchSeed + sim);
                var market = new MonteCarloMarketService(history, simSeed);

                var engine = new ComputeEngine(
                    scenario,
                    accounts.Select(a => a.DeepCopy()).ToList(),
                    market,
                    primary,
                    spouse);

                var output = engine.Run(sim, "random");
                list.AddRange(output.Results);
            }

            return list;
        }
    }
}
