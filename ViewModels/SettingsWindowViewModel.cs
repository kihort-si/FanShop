using System.ComponentModel;
using System.Windows.Input;
using FanShop.Models;

namespace FanShop.ViewModels
{
    public class SettingsWindowViewModel : INotifyPropertyChanged
    {
        private Settings _settings;

        public string ResponsiblePerson
        {
            get => _settings.ResponsiblePerson;
            set
            {
                _settings.ResponsiblePerson = value;
                OnPropertyChanged(nameof(ResponsiblePerson));
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

        public SettingsWindowViewModel()
        {
            _settings = Settings.Load();
            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(Cancel);
        }

        private void Save(object? parameter)
        {
            _settings.Save();
            CloseRequested?.Invoke();
        }

        private void Cancel(object? parameter)
        {
            CloseRequested?.Invoke();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}