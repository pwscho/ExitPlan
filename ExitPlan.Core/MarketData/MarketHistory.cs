namespace ExitPlan.Core.MarketData
{
    public class MarketHistory
    {
        public List<MarketYear> Years { get; set; } = new();

        private readonly Random _rng = new();

        public MarketYear GetRandomYear()
        {

            return this.Years[_rng.Next(this.Years.Count)];
        }
    }



}

