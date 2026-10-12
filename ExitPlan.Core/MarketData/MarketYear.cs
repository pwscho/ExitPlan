using System.Text.Json.Serialization;

namespace ExitPlan.Core.MarketData
{
    public class MarketYear
    {
        [JsonPropertyName("Year")]
        public int Year { get; set; }

        [JsonPropertyName("EquityReturn")]
        public decimal EquityReturn { get; set; }

        [JsonPropertyName("BondReturn")]
        public decimal BondReturn { get; set; }

        [JsonPropertyName("InflationRate")]
        public decimal InflationRate { get; set; }
    }
}


