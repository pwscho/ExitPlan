using ExitPlan.Core;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace ExitPlan.UI.ViewModels
{
    public class RothConversionViewModel : ViewModelBase
    {
        private readonly Scenario _scenario;

        public ObservableCollection<RothConversionStream> Conversions { get; }

        public RelayCommand AddConversionCommand { get; }
        public RelayCommand<RothConversionStream> RemoveConversionCommand { get; }

        private RothConversionStream? _selectedConversion;
        public RothConversionStream? SelectedConversion
        {
            get => _selectedConversion;
            set
            {
                _selectedConversion = value;
                OnPropertyChanged();
            }
        }

        public RothConversionViewModel(Scenario scenario)
        {
            _scenario = scenario;

            Conversions = new ObservableCollection<RothConversionStream>(
                _scenario.RothConversions ?? new List<RothConversionStream>());

            AddConversionCommand = new RelayCommand(AddConversion);
            RemoveConversionCommand = new RelayCommand<RothConversionStream>(RemoveConversion);
        }

        private void AddConversion()
        {
            var conv = new RothConversionStream
            {
                Name = "New Conversion",
                StartAge = 60,
                EndAge = 70,
                AnnualAmount = 10000m
            };

            Conversions.Add(conv);
            SyncToScenario();
        }

        private void RemoveConversion(RothConversionStream? conv)
        {
            if (conv == null)
                return;

            Conversions.Remove(conv);
            SyncToScenario();
        }

        public void SyncToScenario()
        {
            _scenario.RothConversions = Conversions.ToList();
        }

        // -----------------------------
        // Owner Options
        // -----------------------------
        public ObservableCollection<OwnerOption> OwnerOptions { get; } = new();

        public void RefreshOwnerOptions(
            string primaryName,
            Guid primaryId,
            string? spouseName,
            Guid? spouseId)
        {
            OwnerOptions.Clear();
            OwnerOptions.Add(new OwnerOption { Id = primaryId, Name = primaryName });

            if (spouseId.HasValue && spouseName != null)
                OwnerOptions.Add(new OwnerOption { Id = spouseId.Value, Name = spouseName });

            OnPropertyChanged(nameof(OwnerOptions));
        }
    }
}

