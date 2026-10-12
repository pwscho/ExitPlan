namespace ExitPlan.Core
{
    public class SimulationOutput
    {
        public List<ResultRow> Results { get; set; } = new();
        public List<AccountYearSummary> Accounts { get; set; } = new();
    }

}

