using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using ExitPlan.Core.Taxes;
using ExitPlan.UI.Views;

namespace ExitPlan.UI.ViewModels
{
    public class TaxSettingsViewModel : ViewModelBase
    {
        private TaxPolicyDocument? _document;
        private TaxPolicyFileInfo? _selectedFile;
        private bool _suppressFileChange;
        private bool _loading;
        private bool _isDirty;
        private int _year;
        private int _additionalStandardDeductionAge = 65;
        private decimal _singleSsBaseThreshold;
        private decimal _singleSsSecondThreshold;
        private decimal _marriedSsBaseThreshold;
        private decimal _marriedSsSecondThreshold;
        private string _statusMessage = string.Empty;
        private string _validationMessage = string.Empty;
        private Window? _window;

        public TaxSettingsViewModel()
        {
            Single = new FilingStatusEditorViewModel("Single");
            MarriedFilingJointly = new FilingStatusEditorViewModel("Married filing jointly", additionalDeductionIsPerSpouse: true);
            Single.Changed += OnChildChanged;
            MarriedFilingJointly.Changed += OnChildChanged;

            SaveCommand = new RelayCommand(Save);
            SaveAsCommand = new RelayCommand(SaveAsNewYear, () => SelectedFile != null && _document != null);
            ReloadCommand = new RelayCommand(Reload, () => SelectedFile != null);
            ViewJsonCommand = new RelayCommand(ViewJson, () => SelectedFile != null);

            RefreshAvailableFiles();
            if (AvailableFiles.Count > 0)
                SelectedFile = AvailableFiles[0];
        }

        public void AttachWindow(Window window) => _window = window;

        public ObservableCollection<TaxPolicyFileInfo> AvailableFiles { get; } = new();

        public FilingStatusEditorViewModel Single { get; }
        public FilingStatusEditorViewModel MarriedFilingJointly { get; }

        public TaxPolicyFileInfo? SelectedFile
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

        public int Year
        {
            get => _year;
            set => SetScalar(ref _year, value);
        }

        public int AdditionalStandardDeductionAge
        {
            get => _additionalStandardDeductionAge;
            set => SetScalar(ref _additionalStandardDeductionAge, value);
        }

        public decimal SingleSsBaseThreshold
        {
            get => _singleSsBaseThreshold;
            set => SetScalar(ref _singleSsBaseThreshold, value);
        }

        public decimal SingleSsSecondThreshold
        {
            get => _singleSsSecondThreshold;
            set => SetScalar(ref _singleSsSecondThreshold, value);
        }

        public decimal MarriedSsBaseThreshold
        {
            get => _marriedSsBaseThreshold;
            set => SetScalar(ref _marriedSsBaseThreshold, value);
        }

        public decimal MarriedSsSecondThreshold
        {
            get => _marriedSsSecondThreshold;
            set => SetScalar(ref _marriedSsSecondThreshold, value);
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

        public string TaxDataFolder => TaxPolicyLoader.GetTaxDataFolder();

        public RelayCommand SaveCommand { get; }
        public RelayCommand SaveAsCommand { get; }
        public RelayCommand ReloadCommand { get; }
        public RelayCommand ViewJsonCommand { get; }

        public bool ConfirmClose() => ConfirmLeaveCurrentFile();

        public void RefreshAvailableFiles()
        {
            var selectedPath = SelectedFile?.FullPath;
            AvailableFiles.Clear();
            foreach (var file in TaxPolicyLoader.ListAvailableFiles())
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
                StatusMessage = "No Tax*.json files were found.";
                return;
            }

            try
            {
                _document = TaxPolicyDocument.Load(SelectedFile.FullPath);
                ApplyPolicyToForm(_document.ToPolicy());
                IsDirty = false;
                StatusMessage = $"Editing {SelectedFile.FileName}";
            }
            catch (Exception ex)
            {
                ValidationMessage = ex.Message;
                StatusMessage = "Could not load the selected tax file.";
            }

            SaveAsCommand.RaiseCanExecuteChanged();
        }

        private void ApplyPolicyToForm(TaxPolicy policy)
        {
            _loading = true;
            try
            {
                _year = policy.Year;
                _additionalStandardDeductionAge = policy.AdditionalStandardDeductionAge;
                _singleSsBaseThreshold = policy.SingleSsBaseThreshold;
                _singleSsSecondThreshold = policy.SingleSsSecondThreshold;
                _marriedSsBaseThreshold = policy.MarriedSsBaseThreshold;
                _marriedSsSecondThreshold = policy.MarriedSsSecondThreshold;

                OnPropertyChanged(nameof(Year));
                OnPropertyChanged(nameof(AdditionalStandardDeductionAge));
                OnPropertyChanged(nameof(SingleSsBaseThreshold));
                OnPropertyChanged(nameof(SingleSsSecondThreshold));
                OnPropertyChanged(nameof(MarriedSsBaseThreshold));
                OnPropertyChanged(nameof(MarriedSsSecondThreshold));

                Single.Load(policy.Single);
                MarriedFilingJointly.Load(policy.MarriedFilingJointly);
            }
            finally
            {
                _loading = false;
            }
        }

        private TaxPolicy ToPolicy()
        {
            return new TaxPolicy
            {
                Year = Year,
                AdditionalStandardDeductionAge = AdditionalStandardDeductionAge,
                Single = Single.ToTable(),
                MarriedFilingJointly = MarriedFilingJointly.ToTable(),
                SingleSsBaseThreshold = SingleSsBaseThreshold,
                SingleSsSecondThreshold = SingleSsSecondThreshold,
                MarriedSsBaseThreshold = MarriedSsBaseThreshold,
                MarriedSsSecondThreshold = MarriedSsSecondThreshold
            };
        }

        private void Save()
        {
            TrySave(showSuccess: true);
        }

        private void SaveAsNewYear()
        {
            if (_document == null || SelectedFile == null)
            {
                ValidationMessage = "No tax file is selected.";
                return;
            }

            var policy = ToPolicy();
            var errors = TaxPolicyValidator.Validate(policy);
            if (errors.Count > 0)
            {
                ValidationMessage = string.Join(Environment.NewLine, errors);
                return;
            }

            var suggestedYear = Year + 1;
            if (suggestedYear < TaxYearNaming.MinYear || suggestedYear > TaxYearNaming.MaxYear)
                suggestedYear = Math.Clamp(Year, TaxYearNaming.MinYear, TaxYearNaming.MaxYear);

            var folder = Path.GetDirectoryName(_document.FilePath);
            if (string.IsNullOrWhiteSpace(folder))
                folder = TaxDataFolder;

            var dialog = new SaveTaxYearWindow(suggestedYear, folder);
            if (_window != null)
                dialog.Owner = _window;

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                var saved = TaxPolicyLoader.SaveAsNewYear(_document, policy, dialog.ChosenYear);
                ValidationMessage = string.Empty;
                IsDirty = false;
                StatusMessage = $"Saved as {saved.FileName}";

                RefreshAvailableFiles();
                SelectFileWithoutPrompt(AvailableFiles.FirstOrDefault(f =>
                    string.Equals(f.FullPath, saved.FullPath, StringComparison.OrdinalIgnoreCase)));
                LoadSelectedFile();

                MessageBox.Show(
                    _window,
                    $"Saved as {saved.FileName}.",
                    "Tax Settings",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ValidationMessage = ex.Message;
            }
        }

        private void SelectFileWithoutPrompt(TaxPolicyFileInfo? file)
        {
            _suppressFileChange = true;
            try
            {
                _selectedFile = file;
                OnPropertyChanged(nameof(SelectedFile));
                ReloadCommand.RaiseCanExecuteChanged();
                ViewJsonCommand.RaiseCanExecuteChanged();
                SaveAsCommand.RaiseCanExecuteChanged();
            }
            finally
            {
                _suppressFileChange = false;
            }
        }

        private bool TrySave(bool showSuccess)
        {
            if (_document == null || SelectedFile == null)
            {
                ValidationMessage = "No tax file is selected.";
                return false;
            }

            var policy = ToPolicy();
            var errors = TaxPolicyValidator.Validate(policy);
            if (errors.Count > 0)
            {
                ValidationMessage = string.Join(Environment.NewLine, errors);
                return false;
            }

            try
            {
                _document.Apply(policy);
                _document.Save();

                var reloaded = TaxPolicyLoader.LoadFromPath(_document.FilePath);
                ApplyPolicyToForm(reloaded);
                IsDirty = false;
                ValidationMessage = string.Empty;
                StatusMessage = $"Saved {SelectedFile.FileName}";

                if (showSuccess)
                    MessageBox.Show(_window, $"Saved {SelectedFile.FileName}.", "Tax Settings", MessageBoxButton.OK, MessageBoxImage.Information);

                return true;
            }
            catch (Exception ex)
            {
                ValidationMessage = ex.Message;
                return false;
            }
        }

        private void Reload()
        {
            if (IsDirty)
            {
                var result = MessageBox.Show(
                    _window,
                    "Reload the file from disk and discard unsaved changes?",
                    "Tax Settings",
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
                    "Tax Settings",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Cancel)
                    return;

                if (result == MessageBoxResult.Yes && !TrySave(showSuccess: false))
                    return;
            }

            var editor = new TaxPolicyEditorWindow(SelectedFile.FullPath);
            if (_window != null)
                editor.Owner = _window;
            editor.ShowDialog();
            LoadSelectedFile();
        }

        private bool ConfirmLeaveCurrentFile()
        {
            if (!IsDirty)
                return true;

            var result = MessageBox.Show(
                _window,
                "You have unsaved tax changes. Save them first?",
                "Tax Settings",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
                return false;

            if (result == MessageBoxResult.Yes)
                return TrySave(showSuccess: false);

            return true;
        }

        private void OnChildChanged(object? sender, EventArgs e)
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
