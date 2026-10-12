namespace ExitPlan.Core.Simulation
{
    public static class SimulationSuccess
    {
        /// <summary>
        /// A simulation succeeds when the last year's ending balance is positive.
        /// </summary>
        public static bool IsSuccessful(IEnumerable<ResultRow> simRows)
        {
            if (simRows == null)
                return false;

            var last = simRows.OrderBy(r => r.Iteration).LastOrDefault();
            return last != null && last.EndingBalance > 0m;
        }
    }
}
