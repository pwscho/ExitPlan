using System.Collections.Generic;

namespace ExitPlan.Core.Taxes
{
    public static class FilingStatusHelper
    {
        public static FilingStatus GetFilingStatusForYear(
            int yearOffset,
            SocialSecuritySettings primary,
            SocialSecuritySettings? spouse)
        {
            return GetLivingOwnerAges(yearOffset, primary, spouse).Count >= 2
                ? FilingStatus.MarriedFilingJointly
                : FilingStatus.Single;
        }

        /// <summary>
        /// Integer ages of owners still alive this year: CurrentAge + yearOffset,
        /// living when that age is &lt;= DeathAge (including the year of death).
        /// </summary>
        public static IReadOnlyList<int> GetLivingOwnerAges(
            int yearOffset,
            SocialSecuritySettings primary,
            SocialSecuritySettings? spouse)
        {
            var ages = new List<int>();

            int primaryAge = primary.CurrentAge + yearOffset;
            if (primaryAge <= primary.DeathAge)
                ages.Add(primaryAge);

            if (spouse != null)
            {
                int spouseAge = spouse.CurrentAge + yearOffset;
                if (spouseAge <= spouse.DeathAge)
                    ages.Add(spouseAge);
            }

            return ages;
        }
    }
}

