using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using ExitPlan.Core;
using ExitPlan.UI.Views;

namespace ExitPlan.UI.ViewModels
{
    public class RmdSettingsViewModel : ViewModelBase
    {
        private RmdPolicyDocument? _document;
        private RmdPolicyFileInfo? _selectedFile;
        private bool _suppressFileChange;
        private bool _loading;
        private bool _isDirty;
        private int _rmdStartAge;
        private RmdStartAgeBandRowViewModel? _selectedBand;
        private RmdDivisorRowViewModel? _selectedDivisor;
        private string _statusMessage = string.Empty;
        private string _validationMessage = string.Empty;
        private Window? _window;

        public RmdSettingsViewModel()
        {
            StartAgeBands.CollectionChanged += OnBandsCollectionChanged;
            Divisors.CollectionChanged += OnDivisorsCollectionChanged;

            SaveCommand = new RelayCommand(Save);
            SaveAsCommand = new RelayCommand(SaveAs, () => _document != null && SelectedFile != null);
            ReloadCommand = new RelayCommand(Reload, () => SelectedFile != null);
            ViewJsonCommand = new RelayCommand(ViewJson, () => SelectedFile != null);
            AddBandCommand = new RelayCommand(AddBand);
            RemoveBandCommand = new RelayCommand(RemoveBand, () => SelectedBand != null);
            AddRowCommand = new RelayCommand(AddRow);
            RemoveRowCommand = new RelayCommand(RemoveRow, () => SelectedDivisor != null);

            RefreshAvailableFiles();
            if (AvailableFiles.Count > 0)
                SelectedFile = AvailableFiles[0];
        }

        public void AttachWindow(Window window) => _window = window;

        public ObservableCollection<RmdPolicyFileInfo> AvailableFiles { get; } = new();
        public ObservableCollection<RmdStartAgeBandRowViewModel> StartAgeBands { get; } = new();
        public ObservableCollection<RmdDivisorRowViewModel> Divisors { get; } = new();

        public RmdPolicyFileInfo? SelectedFile
        {
            get => _selectedFile;
            set
            {
                if (ReferenceEquals(_selectedFile, value))
                    return;

                if (!_suppressFileChange && !ConfirmLeaveCurrentFile())
                {
                    OnPropertyChanged();
                    return;
                }

                _selectedFile = value;
                OnPropertyChanged();
                ReloadCommand.RaiseCanExecuteChanged();
                ViewJsonCommand.RaiseCanExecuteChanged();
                SaveAsCommand.RaiseCanExecuteChanged();
                LoadSelectedFile();
            }
        }

        public RmdStartAgeBandRowViewModel? SelectedBand
        {
            get => _selectedBand;
            set
            {
                _selectedBand = value;
                OnPropertyChanged();
                RemoveBandCommand.RaiseCanExecuteChanged();
            }
        }

        public RmdDivisorRowViewModel? SelectedDivisor
        {
            get => _selectedDivisor;
            set
            {
                _selectedDivisor = value;
                OnPropertyChanged();
                RemoveRowCommand.RaiseCanExecuteChanged();
            }
        }

        public int RmdStartAge
        {
            get => _rmdStartAge;
            set => SetScalar(ref _rmdStartAge, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public string ValidationMessage
        {
            get => _validationMessage;
            private set
            {
                _validationMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasValidationErrors));
            }
        }

        public bool HasValidationErrors => !string.IsNullOrWhiteSpace(ValidationMessage);

        public bool IsDirty
        {
            get => _isDirty;
            private set
            {
                if (_isDirty == value)
                    return;
                _isDirty = value;
                OnPropertyChanged();
            }
        }

        public string RmdDataFolder => RmdPolicyLoader.GetRmdDataFolder();

        public RelayCommand SaveCommand { get; }
        public RelayCommand SaveAsCommand { get; }
        public RelayCommand ReloadCommand { get; }
        public RelayCommand ViewJsonCommand { get; }
        public RelayCommand AddBandCommand { get; }
        public RelayCommand RemoveBandCommand { get; }
        public RelayCommand AddRowCommand { get; }
        public RelayCommand RemoveRowCommand { get; }

        public bool ConfirmClose() => ConfirmLeaveCurrentFile();

        public void RefreshAvailableFiles()
        {
            var selectedPath = SelectedFile?.FullPath;
            AvailableFiles.Clear();
            foreach (var file in RmdPolicyLoader.ListAvailableFiles())
                AvailableFiles.Add(file);

            if (selectedPath != null)
            {
                _suppressFileChange = true;
                try
                {
                    _selectedFile = AvailableFiles.FirstOrDefault(f =>
                        string.Equals(f.FullPath, selectedPath, StringComparison.OrdinalIgnoreCase));
                    OnPropertyChanged(nameof(SelectedFile));
                }
                finally
                {
                    _suppressFileChange = false;
                }
            }
        }

        private void LoadSelectedFile()
        {
            ValidationMessage = string.Empty;

            if (SelectedFile == null)
            {
                _document = null;
                SaveAsCommand.RaiseCanExecuteChanged();
                StatusMessage = "No RMD*.json files were found.";
                return;
            }

            try
            {
                _document = RmdPolicyDocument.Load(SelectedFile.FullPath);
                ApplyPolicyToForm(_document.ToPolicy());
                IsDirty = false;
                SaveAsCommand.RaiseCanExecuteChanged();
                StatusMessage = $"Editing {SelectedFile.FileName}";
            }
            catch (Exception ex)
            {
                ValidationMessage = ex.Message;
                StatusMessage = "Could not load the selected RMD file.";
            }
        }

        private void ApplyPolicyToForm(RmdPolicy policy)
        {
            _loading = true;
            try
            {
                _rmdStartAge = policy.RmdStartAge;
                OnPropertyChanged(nameof(RmdStartAge));

                foreach (var band in StartAgeBands)
                    band.Changed -= OnRowChanged;
                StartAgeBands.Clear();

                foreach (var band in policy.StartAgeBands ?? new List<RmdStartAgeBand>())
                {
                    var row = new RmdStartAgeBandRowViewModel(band.BornFrom, band.BornThrough, band.StartAge);
                    row.Changed += OnRowChanged;
                    StartAgeBands.Add(row);
                }

                foreach (var row in Divisors)
                    row.Changed -= OnRowChanged;
                Divisors.Clear();

                foreach (var pair in policy.LifeExpectancyDivisors.OrderBy(p => p.Key))
                {
                    var row = new RmdDivisorRowViewModel(pair.Key, pair.Value);
                    row.Changed += OnRowChanged;
                    Divisors.Add(row);
                }
            }
            finally
            {
                _loading = false;
            }
        }

        private RmdPolicy ToPolicy()
        {
            var divisors = new Dictionary<int, decimal>();
            foreach (var row in Divisors)
            {
                if (!divisors.ContainsKey(row.Age))
                    divisors[row.Age] = row.DistributionPeriod;
            }

            return new RmdPolicy
            {
                RmdStartAge = RmdStartAge,
                StartAgeBands = StartAgeBands.Select(row => row.ToBand()).ToList(),
                LifeExpectancyDivisors = divisors
            };
        }

        private IReadOnlyList<(int Age, decimal Period)> CurrentRows() =>
            Divisors.Select(row => (row.Age, row.DistributionPeriod)).ToList();

        private IReadOnlyList<RmdStartAgeBand> CurrentBands() =>
            StartAgeBands.Select(row => row.ToBand()).ToList();

        private void Save()
        {
            TrySave(showSuccess: true);
        }

        private bool TrySave(bool showSuccess)
        {
            if (_document == null || SelectedFile == null)
            {
                ValidationMessage = "No RMD file is selected.";
                return false;
            }

            var errors = RmdPolicyValidator.Validate(RmdStartAge, CurrentRows(), CurrentBands());
            if (errors.Count > 0)
            {
                ValidationMessage = string.Join(Environment.NewLine, errors);
                return false;
            }

            try
            {
                var policy = ToPolicy();
                _document.Apply(policy);
                _document.Save();

                var reloaded = RmdPolicyLoader.LoadFromPath(_document.FilePath);
                ApplyPolicyToForm(reloaded);
                IsDirty = false;
                ValidationMessage = string.Empty;
                StatusMessage = $"Saved {SelectedFile.FileName}";

                if (showSuccess)
                    MessageBox.Show(_window, $"Saved {SelectedFile.FileName}.", "RMD Settings", MessageBoxButton.OK, MessageBoxImage.Information);

                return true;
            }
            catch (Exception ex)
            {
                ValidationMessage = ex.Message;
                return false;
            }
        }

        private void SaveAs()
        {
            if (_document == null || SelectedFile == null)
            {
                ValidationMessage = "No RMD file is selected.";
                return;
            }

            var errors = RmdPolicyValidator.Validate(RmdStartAge, CurrentRows(), CurrentBands());
            if (errors.Count > 0)
            {
                ValidationMessage = string.Join(Environment.NewLine, errors);
                return;
            }

            int suggestedYear = SelectedFile.Year > 0 ? SelectedFile.Year + 1 : DateTime.Now.Year;
            var dialog = new RmdSaveAsWindow(suggestedYear, AvailableFiles.Select(f => f.Year))
            {
                Owner = _window
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                var path = RmdPolicyLoader.CreateYearFile(
                    RmdPolicyLoader.GetRmdDataFolder(),
                    dialog.SelectedYear,
                    _document,
                    ToPolicy());

                IsDirty = false;
                ValidationMessage = string.Empty;
                RefreshAvailableFiles();

                _suppressFileChange = true;
                try
                {
                    _selectedFile = AvailableFiles.FirstOrDefault(f =>
                        string.Equals(f.FullPath, path, StringComparison.OrdinalIgnoreCase));
                    OnPropertyChanged(nameof(SelectedFile));
                }
                finally
                {
                    _suppressFileChange = false;
                }

                LoadSelectedFile();
                StatusMessage = $"Saved {System.IO.Path.GetFileName(path)}";
                MessageBox.Show(_window, $"Saved {System.IO.Path.GetFileName(path)}.", "RMD Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ValidationMessage = ex.Message;
            }
        }

        private void Reload()
        {
            if (IsDirty)
            {
                var result = MessageBox.Show(
                    _window,
                    "Reload the file from disk and discard unsaved changes?",
                    "RMD Settings",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            LoadSelectedFile();
        }

        private void ViewJson()
        {
            if (SelectedFile == null)
                return;

            if (IsDirty)
            {
                var result = MessageBox.Show(
                    _window,
                    "Save your form changes before opening the JSON view?",
                    "RMD Settings",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Cancel)
                    return;

                if (result == MessageBoxResult.Yes && !TrySave(showSuccess: false))
                    return;
            }

            var editor = new JsonFallbackWindow("RMD Policy JSON", SelectedFile.FullPath);
            if (_window != null)
                editor.Owner = _window;
            editor.ShowDialog();
            LoadSelectedFile();
        }

        private void AddBand()
        {
            int bornFrom = 1900;
            int? bornThrough = null;
            int startAge = RmdStartAge;

            if (StartAgeBands.Count > 0)
            {
                var last = StartAgeBands[StartAgeBands.Count - 1];
                bornFrom = last.BornThrough.HasValue ? last.BornThrough.Value + 1 : last.BornFrom + 1;
                bornThrough = null;
                startAge = last.StartAge;
            }

            var row = new RmdStartAgeBandRowViewModel(bornFrom, bornThrough, startAge);
            row.Changed += OnRowChanged;
            StartAgeBands.Add(row);
            SelectedBand = row;
        }

        private void RemoveBand()
        {
            if (SelectedBand == null)
                return;
            SelectedBand.Changed -= OnRowChanged;
            StartAgeBands.Remove(SelectedBand);
            SelectedBand = StartAgeBands.LastOrDefault();
        }

        private void AddRow()
        {
            int age = Divisors.Count == 0 ? RmdStartAge : Divisors.Max(r => r.Age) + 1;
            decimal period = Divisors.LastOrDefault()?.DistributionPeriod ?? 1m;
            var row = new RmdDivisorRowViewModel(age, period);
            row.Changed += OnRowChanged;
            Divisors.Add(row);
            SelectedDivisor = row;
        }

        private void RemoveRow()
        {
            if (SelectedDivisor == null)
                return;
            SelectedDivisor.Changed -= OnRowChanged;
            Divisors.Remove(SelectedDivisor);
            SelectedDivisor = Divisors.LastOrDefault();
        }

        private bool ConfirmLeaveCurrentFile()
        {
            if (!IsDirty)
                return true;

            var result = MessageBox.Show(
                _window,
                "You have unsaved RMD changes. Save them first?",
                "RMD Settings",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
                return false;

            if (result == MessageBoxResult.Yes)
                return TrySave(showSuccess: false);

            return true;
        }

        private void OnBandsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (!_loading)
                IsDirty = true;
        }

        private void OnDivisorsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (!_loading)
                IsDirty = true;
        }

        private void OnRowChanged(object? sender, EventArgs e)
        {
            if (!_loading)
                IsDirty = true;
        }

        private void SetScalar<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
            where T : struct, IEquatable<T>
        {
            if (field.Equals(value))
                return;
            field = value;
            OnPropertyChanged(name);
            if (!_loading)
                IsDirty = true;
        }
    }
}
