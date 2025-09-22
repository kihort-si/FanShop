using System.ComponentModel;
using System.Windows.Input;
using FanShop.Models;

namespace FanShop.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        private readonly MainWindowViewModel _mainWindowViewModel;
        private Settings _settings;

        public string Head
        {
            get => _settings.Head;
            set
            {
                _settings.Head = value;
                OnPropertyChanged(nameof(Head));
            }
        }

        public string ResponsiblePerson
        {
            get => _settings.ResponsiblePerson;
            set
            {
                _settings.ResponsiblePerson = value;
                OnPropertyChanged(nameof(ResponsiblePerson));
            }
        }

        public string ResponsiblePhoneNumber 
        {
            get => _settings.ResponsiblePhoneNumber;
            set
            {
                _settings.ResponsiblePhoneNumber = value;
                OnPropertyChanged(nameof(ResponsiblePhoneNumber));
            }
        }
        
        public string ResponsiblePosition
        {
            get => _settings.ResponsiblePosition;
            set
            {
                _settings.ResponsiblePosition = value;
                OnPropertyChanged(nameof(ResponsiblePosition));
            }
        }

        public string VisitGoal
        {
            get => _settings.VisitGoal;
            set
            {
                _settings.VisitGoal = value;
                OnPropertyChanged(nameof(VisitGoal));
            }
        }

        public decimal DailySalary
        {
            get => _settings.DailySalary;
            set
            {
                _settings.DailySalary = value;
                OnPropertyChanged(nameof(DailySalary));
            }
        }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public event Action? CloseRequested;

        public SettingsViewModel(MainWindowViewModel mainWindowViewModel)
        {
            _mainWindowViewModel = mainWindowViewModel;
            _settings = Settings.Load();
            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(Cancel);
        }

        private void Save(object? parameter)
        {
            _settings.Save();
            _mainWindowViewModel.RefreshStatistics();
            _mainWindowViewModel.CloseTabRequest(this);
            CloseRequested?.Invoke();
        }

        private void Cancel(object? parameter)
        {
            _mainWindowViewModel.CloseTabRequest(this);
        }
    }
}