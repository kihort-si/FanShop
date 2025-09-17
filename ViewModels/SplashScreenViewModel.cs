using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Application = System.Windows.Application;

namespace FanShop.ViewModels;

public class SplashScreenViewModel : INotifyPropertyChanged
{
    private double _progressWidth;
    private string _loadingText = "Загрузка...";
    private readonly string[] loadingTexts = new string[]
    {
        "Делаем довесы...",
        "Делаем внесение...",
        "Печатаем чеки...",
        "Собираем товары...",
        "Получаем аккредитацию...",
        "Проверяем сертификаты...",
        "Печатаем ценники...",
        "Снимаем защиту..."
    };
    private readonly List<string> usedLoadingTexts = new();
    private Random random = new();
    private List<string> available;

    public double ProgressWidth 
    {
        get => _progressWidth;
        set 
        {
            _progressWidth = value;
            OnPropertyChanged();
        }
    }

    public string LoadingText
    {
        get => _loadingText;
        set
        {
            _loadingText = value;
            OnPropertyChanged();
        }
    }

    public SplashScreenViewModel()
    {
        available = loadingTexts.Except(usedLoadingTexts).ToList();
        _loadingText = PickNewLoadingText();
        OnLoaded();
    }

    private string PickNewLoadingText()
    {
        if (available.Count == 0)
            return "Готово!";

        string newText = available[random.Next(available.Count)];

        usedLoadingTexts.Add(newText);
        available = loadingTexts.Except(usedLoadingTexts).ToList();

        return newText;
    }

    private async void OnLoaded()
    {
        await Task.Delay(100);
    }

    public void UpdateProgress(int percent)
    {
        try
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                percent = Math.Max(0, Math.Min(100, percent));
                ProgressWidth = 360 * percent / 100;
                if (percent == 30 || percent == 60)
                {
                    _loadingText = PickNewLoadingText();
                    OnPropertyChanged(nameof(LoadingText));
                }
                if (percent == 100)
                {
                    _loadingText = "Готово!";
                    OnPropertyChanged(nameof(LoadingText));
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка обновления прогресса: {ex.Message}");
        }
    }
    
    public void Stop()
    {
        usedLoadingTexts.Clear();
        
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
