using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FanShop.ViewModels;

public partial class SplashScreenViewModel : BaseViewModel
{
    [ObservableProperty]
    private double _progressWidth;

    [ObservableProperty]
    private string _loadingText = "Загрузка...";

    private readonly string[] _loadingTexts = new string[]
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

    private readonly List<string> _usedLoadingTexts = new();
    private readonly Random _random = new();
    private List<string> _available = new();

    public SplashScreenViewModel()
    {
        _available = _loadingTexts.Except(_usedLoadingTexts).ToList();
        LoadingText = PickNewLoadingText();
    }

    private string PickNewLoadingText()
    {
        if (_available.Count == 0)
            return "Готово!";

        string newText = _available[_random.Next(_available.Count)];

        _usedLoadingTexts.Add(newText);
        _available = _loadingTexts.Except(_usedLoadingTexts).ToList();

        return newText;
    }

    public void UpdateProgress(int percent)
    {
        try
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                percent = Math.Max(0, Math.Min(100, percent));
                ProgressWidth = 360 * percent / 100;
                if (percent == 30 || percent == 60)
                {
                    LoadingText = PickNewLoadingText();
                }
                if (percent == 100)
                {
                    LoadingText = "Готово!";
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка обновления прогресса: {ex.Message}");
        }
    }

    public void Stop()
    {
        _usedLoadingTexts.Clear();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
}
