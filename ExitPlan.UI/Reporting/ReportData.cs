using System.Collections.Generic;
using ExitPlan.Core;
using ExitPlan.Core.Reporting;

namespace ExitPlan.UI.Reporting
{
    public class ReportData
    {
        public Scenario Scenario { get; set; }

        // Snapshot of inputs used for this Generate Report run
        public List<Account> Accounts { get; set; } = new();
        public SocialSecuritySettings? Primary { get; set; }
        public SocialSecuritySettings? Spouse { get; set; }

        // These are the year‑level results from the flat engine
        public List<ResultRow> FixedResults { get; set; } = new();
        public List<ResultRow> HistoricalResults { get; set; } = new();
        public List<ResultRow> MonteCarloResults { get; set; } = new();

        // Report card computed from simulation results
        public ReportCard? ReportCard { get; set; }

        public ReportData(Scenario scenario)
        {
            Scenario = scenario;
        }
    }
}

