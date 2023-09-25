using System.ComponentModel;
using System.IO;
using System.Linq;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Monitor.Model.Sessions;

namespace Monitor.ViewModel.NewSession
{
    public class NewFileSessionViewModel : ViewModelBase, INewSessionViewModel, IDataErrorInfo
    {
        private readonly ISessionService _sessionService;
        private readonly HistoriesSessionParameters _fileSessionParameters = new HistoriesSessionParameters { FileName = "" };

        public NewFileSessionViewModel(ISessionService sessionService)
        {
            _sessionService = sessionService;
            OpenCommand = new RelayCommand(Open, CanOpen);
        }

        private void Open()
        {
            _sessionService.OpenHistories(_fileSessionParameters);
        }

        private bool CanOpen()
        {
            var fieldsToValidate = new[] { nameof(FileName), };

            return fieldsToValidate.All(field => string.IsNullOrEmpty(this[field]));
        }

        public string FileName
        {
            get => _fileSessionParameters.FileName;
            set
            {
                _fileSessionParameters.FileName = value;
                RaisePropertyChanged();
                OpenCommand.RaiseCanExecuteChanged();
            }
        }

        public bool CloseAfterCompleted
        {
            get => _fileSessionParameters.CloseAfterCompleted;
            set
            {
                //_fileSessionParameters.CloseAfterCompleted = value;
                //RaisePropertyChanged();
            }
        }

        public RelayCommand OpenCommand { get; }

        public string Header { get; } = "From Histories";

        public string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(FileName):
                        if (string.IsNullOrWhiteSpace(FileName)) return "Filename is required";
                        if (!File.Exists(FileName)) return "File does not exist";
                        return string.Empty;

                    default:
                        return string.Empty;
                }
            }
        }

        public string Error { get; } = null;
    }
}