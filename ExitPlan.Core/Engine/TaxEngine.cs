using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExitPlan.Core.Engine
{
    public class TaxEngine
    {
        public TransferTaxInfo CalculateTransferTax(
            AccountProjection from,
            AccountProjection? to,
            decimal amount,
            TransferType type,
            int year)
        {
            var info = new TransferTaxInfo();

            bool fullyTaxable =
                type == TransferType.Rmd ||
                type == TransferType.RothConversion;

            if (fullyTaxable && from.TaxBucket == "TaxDeferred")
            {
                info.TaxableAmount = amount;
                info.TaxesDue = 0m; // integrate with your tax pipeline later
            }

            return info;
        }
    }
}
