using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media.Imaging;

namespace FanShop.ViewModels
{
    public class CalendarDayViewModel : INotifyPropertyChanged
    {
        public DateTime Date { get; set; }
        public ObservableCollection<string> Employees { get; set; } = new();

        private MatchInfo _match;
        public MatchInfo Match
        {
            get => _match;
            set
            {
                if (_match != value)
                {
                    _match = value;
                    HasMatch = _match != null;
                    OnPropertyChanged(nameof(Match));
                }
            }
        }

        private bool _hasMatch;
        public bool HasMatch
        {
            get => _hasMatch;
            set
            {
                if (_hasMatch != value)
                {
                    _hasMatch = value;
                    OnPropertyChanged(nameof(HasMatch));
                }
            }
        }

        private bool _isCurrentMonth;
        public bool IsCurrentMonth
        {
            get => _isCurrentMonth;
            set
            {
                if (_isCurrentMonth != value)
                {
                    _isCurrentMonth = value;
                    OnPropertyChanged(nameof(IsCurrentMonth));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class MatchInfo
    {
        public string TeamName { get; set; }
        public string Time { get; set; }
        public string SartTime { get; set; }
        public BitmapImage Logo { get; set; }
    }
}