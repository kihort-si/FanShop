using System.ComponentModel;
using System.Runtime.CompilerServices;
using Application = System.Windows.Application;

namespace FanShop.ViewModels;

public class SplashScreenViewModel : INotifyPropertyChanged
{
    private double _progressWidth;
    private string _loadingText = "Загрузка...";
    private string curLoadingText;
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
    private CancellationTokenSource _cts;

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
        StartTextRotation();
        OnLoaded();
    }

    private async void StartTextRotation()
    {
        _cts = new CancellationTokenSource();

        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                await Task.Delay(1500, _cts.Token);
                PickNewLoadingText();
            }
        }
        catch (TaskCanceledException)
        {
        }
    }

    public void StopTextRotation()
    {
        _cts?.Cancel();
    }

    private string PickNewLoadingText()
    {
        for (int i = 0; i < 2; i++)
        {
            var available = loadingTexts.Except(usedLoadingTexts).ToList();

            if (available.Count == 0)
                return "Готово!";

            string newText = available[random.Next(available.Count)];

            curLoadingText = newText;
            usedLoadingTexts.Add(newText);

            Application.Current.Dispatcher.Invoke(() => 
            {
                LoadingText = curLoadingText;
            });
            return curLoadingText;
        }
        return "Готово!";
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
                if (percent == 100)
                {
                    _cts.Cancel();
                    _loadingText = "Готово!";
                    OnPropertyChanged(nameof(LoadingText));
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка обновления прогресса: {ex.Message}");
        }
    }

    public void StopLoading()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ProgressWidth = 360;
        });

        StopTextRotation();
        _cts?.Cancel();
    }
    
    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        
        Application.Current.Dispatcher.Invoke(() =>
        {
            ProgressWidth = 360;
        });
        
        usedLoadingTexts.Clear();
        
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
