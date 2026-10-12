namespace ExitPlan.Core.Reporting
{
    public class HistogramBin
    {
        public decimal BinStart { get; set; }
        public decimal BinEnd { get; set; }
        public int Count { get; set; }

        public override string ToString()
        {
            return $"{BinStart:n0} – {BinEnd:n0}: {Count}";
        }
    }
}
