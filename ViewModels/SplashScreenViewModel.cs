using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FanShop.ViewModels;

public class SplashScreenViewModel : INotifyPropertyChanged
{
    private double _progressWidth;
    private string _loadingText = "Загрузка...";

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
            PickNewLoadingText();
            
            while (!_cts.Token.IsCancellationRequested)
            {
                await Task.Delay(1000, _cts.Token); 
                
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

    private void PickNewLoadingText()
    {
        while (usedLoadingTexts.Count <= loadingTexts.Length)
        {

            var available = loadingTexts.Except(usedLoadingTexts).ToList();

            if (available.Count == 0)
                return;

            string newText = available[random.Next(available.Count)];

            curLoadingText = newText;
            usedLoadingTexts.Add(newText);

            LoadingText = curLoadingText;
        }
    }
    
    private async void OnLoaded()
    {
        await Task.Delay(100);
        
        StartLoading();
    }
    
    private async void StartLoading()
    {
        _cts = new CancellationTokenSource();
        CancellationToken ct = _cts.Token;
        for (int i = 0; i <= 80; i++)
        {
            if (ct.IsCancellationRequested)
                return;
            UpdateProgress(i);
            await Task.Delay(10);
        }
    }
    
    private void UpdateProgress(int percent)
    {
        try
        {
            ProgressWidth = 360 * percent / 100;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
    }
    
    
    
    public void StopLoading()
    {
        ProgressWidth = 360;
        StopTextRotation();
        _cts?.Cancel();
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}