using System.IO;
using ExitPlan.Core.Taxes;

namespace ExitPlan.UI.ViewModels
{
    public class SaveTaxYearViewModel : ViewModelBase
    {
        private readonly string _destinationFolder;
        private readonly Action _confirm;
        private string _yearText;
        private string _fileNamePreview = "TaxYYYY.json";
        private string _errorMessage = string.Empty;
        private int _year;

        public SaveTaxYearViewModel(int suggestedYear, string destinationFolder, Action confirm)
        {
            _destinationFolder = destinationFolder;
            _confirm = confirm;
            _yearText = suggestedYear.ToString();
            ConfirmCommand = new RelayCommand(Confirm, CanConfirm);
            RefreshState();
        }

        public string YearText
        {
            get => _yearText;
            set
            {
                if (_yearText == value)
                    return;
                _yearText = value;
                OnPropertyChanged();
                RefreshState();
            }
        }

        public string FileNamePreview
        {
            get => _fileNamePreview;
            private set
            {
                if (_fileNamePreview == value)
                    return;
                _fileNamePreview = value;
                OnPropertyChanged();
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set
            {
                if (_errorMessage == value)
                    return;
                _errorMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasError));
            }
        }

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

        public int ChosenYear => _year;

        public RelayCommand ConfirmCommand { get; }

        private bool CanConfirm() =>
            TaxYearNaming.TryParseYear(YearText, out _, out _) && string.IsNullOrEmpty(DuplicateMessage());

        private void Confirm()
        {
            RefreshState();
            if (!CanConfirm())
                return;

            _confirm();
        }

        private void RefreshState()
        {
            if (!TaxYearNaming.TryParseYear(YearText, out _year, out var error))
            {
                FileNamePreview = "TaxYYYY.json";
                ErrorMessage = error;
                ConfirmCommand.RaiseCanExecuteChanged();
                return;
            }

            FileNamePreview = TaxYearNaming.FileNameForYear(_year);
            ErrorMessage = DuplicateMessage() ?? string.Empty;
            ConfirmCommand.RaiseCanExecuteChanged();
        }

        private string? DuplicateMessage()
        {
            if (_year == 0)
                return null;

            var dest = TaxYearNaming.GetPath(_destinationFolder, _year);
            if (!File.Exists(dest))
                return null;

            return $"{TaxYearNaming.FileNameForYear(_year)} already exists. Choose a different year — existing tax files are not overwritten.";
        }
    }
}
