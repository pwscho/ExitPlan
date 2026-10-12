using System.ComponentModel;

namespace ExitPlan.Core
{
    public enum TaxBucket { TaxFree, TaxDeferred, Taxable }
    public enum AssetClass { Equities, Bonds, Cash }

    public class Account : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        // ------------------------------------------------------------
        // Unique Account Identifier
        // ------------------------------------------------------------
        private Guid _accountId = Guid.NewGuid();
        public Guid AccountId
        {
            get => _accountId;
            set
            {
                if (_accountId != value)
                {
                    _accountId = value;
                    OnPropertyChanged(nameof(AccountId));
                }
            }
        }

        // ------------------------------------------------------------
        // Display Name (NEW)
        // ------------------------------------------------------------
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        private Guid _ownerId;
        public Guid OwnerId
        {
            get => _ownerId;
            set
            {
                if (_ownerId != value)
                {
                    _ownerId = value;
                    OnPropertyChanged(nameof(OwnerId));
                }
            }
        }

        private string _owner = string.Empty;
        public string Owner
        {
            get => _owner;
            set
            {
                if (_owner != value)
                {
                    _owner = value;
                    OnPropertyChanged(nameof(Owner));
                }
            }
        }

        private string _assetClass = string.Empty;
        public string AssetClass
        {
            get => _assetClass;
            set
            {
                if (_assetClass != value)
                {
                    _assetClass = value;
                    OnPropertyChanged(nameof(AssetClass));
                }
            }
        }

        private string _taxBucket = string.Empty;
        public string TaxBucket
        {
            get => _taxBucket;
            set
            {
                if (_taxBucket != value)
                {
                    _taxBucket = value;
                    OnPropertyChanged(nameof(TaxBucket));
                }
            }
        }

        private decimal _rateOfReturn = 0.1m;
        public decimal RateOfReturn
        {
            get => _rateOfReturn;
            set
            {
                if (_rateOfReturn != value)
                {
                    _rateOfReturn = value;
                    OnPropertyChanged(nameof(RateOfReturn));
                }
            }
        }

        private decimal _value;
        public decimal Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    OnPropertyChanged(nameof(Value));
                }
            }
        }

        private decimal _costBasis;
        public decimal CostBasis
        {
            get => _costBasis;
            set
            {
                if (_costBasis != value)
                {
                    _costBasis = value;
                    OnPropertyChanged(nameof(CostBasis));
                }
            }
        }

        private int _priority;
        public int Priority
        {
            get => _priority;
            set
            {
                if (_priority != value)
                {
                    _priority = value;
                    OnPropertyChanged(nameof(Priority));
                }
            }
        }

        private int _bucket = 1;
        public int Bucket
        {
            get => _bucket;
            set
            {
                if (_bucket != value)
                {
                    _bucket = value;
                    OnPropertyChanged(nameof(Bucket));
                }
            }
        }

        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // ------------------------------------------------------------
        // DeepCopy now includes AccountId and Name
        // ------------------------------------------------------------
        public Account DeepCopy()
        {
            return new Account
            {
                AccountId = this.AccountId,
                Name = this.Name,
                OwnerId = this.OwnerId,
                Owner = this.Owner,
                AssetClass = this.AssetClass,
                TaxBucket = this.TaxBucket,
                RateOfReturn = this.RateOfReturn,
                Value = this.Value,
                CostBasis = this.CostBasis,
                Priority = this.Priority,
                Bucket = this.Bucket
            };
        }
    }
}


