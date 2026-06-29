using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FanShop.Models;

namespace FanShop.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly MainWindowViewModel _mainWindowViewModel;
    private readonly Settings _settings;

    [ObservableProperty]
    private string _head = string.Empty;

    [ObservableProperty]
    private string _responsiblePerson = string.Empty;

    [ObservableProperty]
    private string _responsiblePhoneNumber = string.Empty;

    [ObservableProperty]
    private string _responsiblePosition = string.Empty;

    [ObservableProperty]
    private string _visitGoal = string.Empty;

    [ObservableProperty]
    private decimal _dailySalary;

    public SettingsViewModel(MainWindowViewModel mainWindowViewModel)
    {
        _mainWindowViewModel = mainWindowViewModel;
        _settings = Settings.Load();

        Head = _settings.Head;
        ResponsiblePerson = _settings.ResponsiblePerson;
        ResponsiblePhoneNumber = _settings.ResponsiblePhoneNumber;
        ResponsiblePosition = _settings.ResponsiblePosition;
        VisitGoal = _settings.VisitGoal;
        DailySalary = _settings.DailySalary;
    }

    [RelayCommand]
    private void Save()
    {
        _settings.Head = Head;
        _settings.ResponsiblePerson = ResponsiblePerson;
        _settings.ResponsiblePhoneNumber = ResponsiblePhoneNumber;
        _settings.ResponsiblePosition = ResponsiblePosition;
        _settings.VisitGoal = VisitGoal;
        _settings.DailySalary = DailySalary;
        _settings.Save();

        _mainWindowViewModel.RefreshStatistics();
        _mainWindowViewModel.CloseTabRequest(this);
    }

    [RelayCommand]
    private void Cancel()
    {
        _mainWindowViewModel.CloseTabRequest(this);
    }
}
