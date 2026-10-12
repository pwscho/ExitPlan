using System;

namespace ExitPlan.Core
{
    public class TransferEngine
    {
        private readonly TaxEngine _taxEngine;

        public TransferEngine(TaxEngine taxEngine)
        {
            _taxEngine = taxEngine;
        }

        public TransferResult Transfer(
            AccountProjection from,
            AccountProjection? to,
            decimal amount,
            int year,
            TransferType type)
        {
            if (amount <= 0m)
                throw new ArgumentOutOfRangeException(nameof(amount));

            // 1. Withdraw from source projection
            decimal withdrawn = from.Withdraw(amount, year);

            // 2. Determine tax impact
            var taxInfo = _taxEngine.CalculateTransferTax(from, to, withdrawn, type, year);

            decimal taxes = taxInfo.TaxesDue;
            decimal net = withdrawn - taxes;
            if (net < 0m)
                net = 0m;

            // 3. Deposit into destination projection
            if (to != null && net > 0m)
                to.Deposit(net, year);

            // 4. Return a unified result
            return new TransferResult
            {
                FromAccount = from,
                ToAccount = to,
                RequestedAmount = amount,
                GrossAmount = withdrawn,
                NetAmount = net,
                TaxableAmount = taxInfo.TaxableAmount,
                TaxesDue = taxes,
                Type = type,
                Year = year
            };
        }
    }
}


