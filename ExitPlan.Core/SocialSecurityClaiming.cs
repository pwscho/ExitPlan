namespace ExitPlan.Core;

/// <summary>
/// Social Security can be claimed from age 62 through 70. Delayed retirement
/// credits stop at 70, so a claim age above 70 must not produce extra +8%/yr.
/// </summary>
public static class SocialSecurityClaiming
{
    public const int MinAge = 62;
    public const int MaxAge = 70;

    public static readonly IReadOnlyList<int> AllowedAges =
        Enumerable.Range(MinAge, MaxAge - MinAge + 1).ToList();

    public static int Clamp(int claimAge) => Math.Clamp(claimAge, MinAge, MaxAge);

    /// <summary>
    /// Early reduction (6%/yr before FRA 67) or delayed credits (8%/yr after FRA),
    /// with claim age clamped to [62, 70] so the adjustment never exceeds real DRC max.
    /// </summary>
    public static decimal AdjustmentFactor(int claimAge)
    {
        int ageDiff = Clamp(claimAge) - 67;

        return ageDiff switch
        {
            > 0 => 0.08m * ageDiff,
            < 0 => -0.06m * Math.Abs(ageDiff),
            _ => 0m
        };
    }
}
