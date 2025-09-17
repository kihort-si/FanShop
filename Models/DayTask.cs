using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace FanShop.Models
{
    public class DayTask : INotifyPropertyChanged
    {
        public int DayTaskID { get; set; }
        
        public DateTime Date { get; set; }
        
        public int StartHour { get; set; }
        public int StartMinute { get; set; }
        
        public int EndHour { get; set; }
        public int EndMinute { get; set; }
        
        public string Title { get; set; } = "";
        
        public string? Comment { get; set; } = "";
        
        public int? TaskCategoryID { get; set; }
        public TaskCategory? Category { get; set; }
    
        [NotMapped]
        public TimeSpan StartTime
        {
            get => new TimeSpan(StartHour, StartMinute, 0);
            set
            {
                StartHour = value.Hours;
                StartMinute = value.Minutes;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StartTimeText));
            }
        }
    
        [NotMapped]
        public TimeSpan EndTime
        {
            get => new TimeSpan(EndHour, EndMinute, 0);
            set
            {
                if (new TimeSpan(EndHour, EndMinute, 0) < StartTime)
                {
                    Date = Date.AddDays(1);
                    OnPropertyChanged(nameof(Date));
                }
                EndHour = value.Hours;
                EndMinute = value.Minutes;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EndTimeText));
            }
        }
    
        [NotMapped]
        public TimeSpan Duration => EndTime > StartTime ? EndTime - StartTime : (EndTime + TimeSpan.FromDays(1)) - StartTime;
    
        [NotMapped]
        public string StartTimeText
        {
            get => $"{StartHour:D2}:{StartMinute:D2}";
            set
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                
                try
                {
                    if (value.All(char.IsDigit))
                    {
                        if (value.Length <= 2) 
                        {
                            if (int.TryParse(value, out int hour) && hour >= 0 && hour < 24)
                            {
                                StartHour = hour;
                                StartMinute = 0;
                            }
                        }
                        else if (value.Length == 3) 
                        {
                            int hour = int.Parse(value.Substring(0, 1));
                            int minute = int.Parse(value.Substring(1));
                            if (hour >= 0 && hour < 24 && minute >= 0 && minute < 60)
                            {
                                StartHour = hour;
                                StartMinute = minute;
                            }
                        }
                        else if (value.Length == 4) 
                        {
                            int hour = int.Parse(value.Substring(0, 2));
                            int minute = int.Parse(value.Substring(2));
                            if (hour >= 0 && hour < 24 && minute >= 0 && minute < 60)
                            {
                                StartHour = hour;
                                StartMinute = minute;
                            }
                        }
                    }
                    else
                    {
                        string[] parts = value.Replace('.', ':').Replace(',', ':').Split(':');
                        if (parts.Length >= 1 && int.TryParse(parts[0], out int hour) && hour >= 0 && hour < 24)
                        {
                            StartHour = hour;
                            if (parts.Length >= 2 && int.TryParse(parts[1], out int minute) && minute >= 0 && minute < 60)
                            {
                                StartMinute = minute;
                            }
                            else
                            {
                                StartMinute = 0;
                            }
                        }
                    }
                    
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StartTime));
                    OnPropertyChanged(nameof(Duration));
                }
                catch
                {
                }
            }
        }
    
        [NotMapped]
        public string EndTimeText
        {
            get => $"{EndHour:D2}:{EndMinute:D2}";
            set
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                
                try
                {
                    if (value.All(char.IsDigit))
                    {
                        if (value.Length <= 2) 
                        {
                            if (int.TryParse(value, out int hour) && hour >= 0 && hour < 24)
                            {
                                EndHour = hour;
                                EndMinute = 0;
                            }
                        }
                        else if (value.Length == 3)
                        {
                            int hour = int.Parse(value.Substring(0, 1));
                            int minute = int.Parse(value.Substring(1));
                            if (hour >= 0 && hour < 24 && minute >= 0 && minute < 60)
                            {
                                EndHour = hour;
                                EndMinute = minute;
                            }
                        }
                        else if (value.Length == 4)
                        {
                            int hour = int.Parse(value.Substring(0, 2));
                            int minute = int.Parse(value.Substring(2));
                            if (hour >= 0 && hour < 24 && minute >= 0 && minute < 60)
                            {
                                EndHour = hour;
                                EndMinute = minute;
                            }
                        }
                    }
                    else
                    {
                        string[] parts = value.Replace('.', ':').Replace(',', ':').Split(':');
                        if (parts.Length >= 1 && int.TryParse(parts[0], out int hour) && hour >= 0 && hour < 24)
                        {
                            EndHour = hour;
                            if (parts.Length >= 2 && int.TryParse(parts[1], out int minute) && minute >= 0 && minute < 60)
                            {
                                EndMinute = minute;
                            }
                            else
                            {
                                EndMinute = 0;
                            }
                        }
                    }
                    
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(EndTime));
                    OnPropertyChanged(nameof(Duration));
                }
                catch
                {
                }
            }
        }
    
        public event PropertyChangedEventHandler? PropertyChanged;
    
        public virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}