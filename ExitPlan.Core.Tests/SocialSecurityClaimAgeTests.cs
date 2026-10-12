using System.Text.Json;
using ExitPlan.Core;
using ExitPlan.Core.Engine;
using ExitPlan.Core.Services;

namespace ExitPlan.Core.Tests
{
    public class SocialSecurityClaimAgeTests
    {
        private const decimal FraMonthly = 1_000m;
        private const decimal Cola = 0.02m;

        [Fact]
        public void Clamp_MapsOutsideRangeToBounds()
        {
            Assert.Equal(62, SocialSecurityClaiming.Clamp(61));
            Assert.Equal(62, SocialSecurityClaiming.Clamp(0));
            Assert.Equal(70, SocialSecurityClaiming.Clamp(71));
            Assert.Equal(70, SocialSecurityClaiming.Clamp(99));
            Assert.Equal(67, SocialSecurityClaiming.Clamp(67));
        }

        [Fact]
        public void Settings_ClaimAge71_ClampsTo70()
        {
            var settings = new SocialSecuritySettings { ClaimAge = 71 };
            Assert.Equal(70, settings.ClaimAge);
        }

        [Fact]
        public void Settings_ClaimAge61_ClampsTo62()
        {
            var settings = new SocialSecuritySettings { ClaimAge = 61 };
            Assert.Equal(62, settings.ClaimAge);
        }

        [Fact]
        public void JsonLoad_ClaimAge71_ClampsTo70()
        {
            var settings = JsonSerializer.Deserialize<SocialSecuritySettings>(
                """{"ClaimAge":71,"BenefitAtFullRetirementAge":1000}""");

            Assert.NotNull(settings);
            Assert.Equal(70, settings.ClaimAge);
        }

        [Fact]
        public void JsonLoad_ClaimAgeBelow62_ClampsTo62()
        {
            var settings = JsonSerializer.Deserialize<SocialSecuritySettings>(
                """{"ClaimAge":50,"BenefitAtFullRetirementAge":1000}""");

            Assert.NotNull(settings);
            Assert.Equal(62, settings.ClaimAge);
        }

        [Fact]
        public void AppStateJson_OutOfRangeClaimAges_ClampOnLoad()
        {
            var state = JsonSerializer.Deserialize<AppState>("""
                {
                  "Scenarios": [
                    {
                      "PrimarySocialSecurity": { "ClaimAge": 71 },
                      "SpouseSocialSecurity": { "ClaimAge": 55 }
                    }
                  ]
                }
                """)!;

            Assert.Equal(70, state.Scenarios[0].PrimarySocialSecurity!.ClaimAge);
            Assert.Equal(62, state.Scenarios[0].SpouseSocialSecurity!.ClaimAge);
        }

        [Fact]
        public void ClaimAge70_Gets24PercentDelayedCreditsVsFra67()
        {
            // 3 years × 8% delayed retirement credits. FRA and rates are unchanged.
            Assert.Equal(0.24m, SocialSecurityClaiming.AdjustmentFactor(70));
            Assert.Equal(0m, SocialSecurityClaiming.AdjustmentFactor(67));

            var engine = new SocialSecurityEngine();
            decimal atFra = engine.CalculateBenefits(Settings(claimAge: 67, cola: 0m));
            decimal at70 = engine.CalculateBenefits(Settings(claimAge: 70, cola: 0m));

            Assert.Equal(FraMonthly * 12m, atFra);
            Assert.Equal(atFra * 1.24m, at70);
        }

        [Fact]
        public void ClaimAge71_ProducesSameBenefitAs70_SameFraAndCola()
        {
            // Clamp-before-calc: age 71 must not earn a 4th year of +8% DRC.
            Assert.Equal(
                SocialSecurityClaiming.AdjustmentFactor(70),
                SocialSecurityClaiming.AdjustmentFactor(71));

            var engine = new SocialSecurityEngine();
            var at70 = Settings(claimAge: 70, cola: Cola);
            var at71 = Settings(claimAge: 71, cola: Cola);

            Assert.Equal(70, at71.ClaimAge);
            Assert.Equal(engine.CalculateBenefits(at70), engine.CalculateBenefits(at71));
            Assert.Equal(
                engine.CalculateBenefits(at70),
                engine.CalculateBenefits(Settings(claimAge: 99, cola: Cola)));
        }

        [Fact]
        public void ClaimAge61_Matches62_EarlyReductionPath()
        {
            // Clamp-before-calc: age 61 uses the age-62 early-reduction factor (5 × 6% = 30%).
            Assert.Equal(
                SocialSecurityClaiming.AdjustmentFactor(62),
                SocialSecurityClaiming.AdjustmentFactor(61));
            Assert.Equal(-0.30m, SocialSecurityClaiming.AdjustmentFactor(62));

            var engine = new SocialSecurityEngine();
            var at62 = Settings(claimAge: 62, cola: Cola);
            var at61 = Settings(claimAge: 61, cola: Cola);

            Assert.Equal(62, at61.ClaimAge);
            Assert.Equal(engine.CalculateBenefits(at62), engine.CalculateBenefits(at61));
        }

        [Fact]
        public void ComputeEngine_ClaimAge71_Matches70_WithCola()
        {
            decimal at70 = FirstYearBenefit(claimAge: 70, currentAge: 70, cola: Cola);
            decimal at71 = FirstYearBenefit(claimAge: 71, currentAge: 70, cola: Cola);

            Assert.Equal(at70, at71);
            Assert.True(at70 > 0m);
        }

        [Fact]
        public void ComputeEngine_ClaimAge61_Matches62_WithCola()
        {
            decimal at62 = FirstYearBenefit(claimAge: 62, currentAge: 62, cola: Cola);
            decimal at61 = FirstYearBenefit(claimAge: 61, currentAge: 62, cola: Cola);

            Assert.Equal(at62, at61);
            Assert.True(at62 > 0m);
        }

        [Fact]
        public void ComputeEngine_ClaimAge70_Is24PercentAboveFra67_ColaZero()
        {
            decimal atFra = FirstYearBenefit(claimAge: 67, currentAge: 70, cola: 0m);
            decimal at70 = FirstYearBenefit(claimAge: 70, currentAge: 70, cola: 0m);

            Assert.Equal(FraMonthly * 12m, atFra);
            Assert.Equal(atFra * 1.24m, at70);
        }

        private static SocialSecuritySettings Settings(int claimAge, decimal cola) =>
            new()
            {
                ClaimAge = claimAge,
                BenefitAtFullRetirementAge = FraMonthly,
                Cola = cola
            };

        private static decimal FirstYearBenefit(int claimAge, int currentAge, decimal cola)
        {
            var (scenario, accounts, primary, _) = TestHousehold.Create(
                taxDeferred: 0m,
                taxFreeCash: 50_000m,
                taxableCash: 50_000m,
                spending: 0m,
                years: 1,
                currentAge: currentAge,
                deathAge: 95);

            primary.ClaimAge = claimAge;
            primary.BenefitAtFullRetirementAge = FraMonthly;
            primary.Cola = cola;

            var output = new ComputeEngine(scenario, accounts, new FixedMarketService(), primary, null)
                .Run(1, "fixed");

            return output.Results[0].SocialSecurityIncome;
        }
    }
}
