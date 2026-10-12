namespace ExitPlan.Core;

public class SocialSecuritySettings
{
    public Guid UserId { get; set; } = Guid.NewGuid();

    public string Owner { get; set; } = "You";
    public int CurrentAge { get; set; }

    private int _claimAge = 67;
    public int ClaimAge
    {
        get => _claimAge;
        set => _claimAge = SocialSecurityClaiming.Clamp(value);
    }

    public int DeathAge { get; set; } = 67;
    public decimal BenefitAtFullRetirementAge { get; set; }
    public decimal Cola { get; set; } = 0.02m; // Cost of Living Adjustment, default to 2%

    // Optional future fields:
    public bool IsSurvivorBenefit { get; set; }
    public decimal SurvivorBenefitAmount { get; set; }

    public SocialSecuritySettings DeepCopy()
    {
        return new SocialSecuritySettings
        {
            UserId = this.UserId,   // preserve identity
            Owner = this.Owner,
            CurrentAge = this.CurrentAge,
            ClaimAge = this.ClaimAge,
            DeathAge = this.DeathAge,
            BenefitAtFullRetirementAge = this.BenefitAtFullRetirementAge,
            IsSurvivorBenefit = this.IsSurvivorBenefit,
            SurvivorBenefitAmount = this.SurvivorBenefitAmount,
            Cola = this.Cola
        };
    }

}

