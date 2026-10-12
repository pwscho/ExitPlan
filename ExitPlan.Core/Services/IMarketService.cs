namespace ExitPlan.Core.Services
{
    public interface IMarketService
    {
        decimal GetEquityReturnForYear(int yearIndex);
        decimal GetBondReturnForYear(int yearIndex);
        decimal GetInflationForYear(int yearIndex);
    }

}


