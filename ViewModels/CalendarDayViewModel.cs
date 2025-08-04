using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media.Imaging;

namespace FanShop.ViewModels

{
    public class CalendarDayViewModel : INotifyPropertyChanged
    {
        public DateTime Date { get; set; }
        public ObservableCollection<string> Employees { get; set; } = new();

        public bool HasMatch => Match != null;
        public MatchInfo Match { get; set; }

        public bool IsCurrentMonth { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class MatchInfo
    {
        public string TeamName { get; set; }
        public string MatchTime { get; set; }
        public BitmapImage Time { get; set; }
    }
}