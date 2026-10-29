using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FanShop.Models;
using FanShop.Services;
using FanShop.View;
using FanShop.Windows;

namespace FanShop.ViewModels;

public partial class EmployeeViewModel : BaseViewModel, IWorkplaceCatalogObserver
{
    private readonly MainWindowViewModel? _mainWindowViewModel;
    private static readonly EmployeeNameComparer NameComparer = new();
    private List<Employee> _allEmployees = new();
    private List<Employee> _rankedEmployees = new();
    private readonly HashSet<int> _excludedEmployeeIds = new();

    [ObservableProperty] private string _searchText = string.Empty;
    partial void OnSearchTextChanged(string value) => ApplyEmployeeFilter();

    public void ExcludeEmployees(IEnumerable<int> employeeIds)
    {
        _excludedEmployeeIds.UnionWith(employeeIds);
        ApplyEmployeeFilter();
    }

    public static bool MatchesSearch(Employee employee, string? search)
    {
        static string Normalize(string value) => value.Trim().ToUpperInvariant().Replace('Ё', 'Е');
        var name = Normalize($"{employee.Surname} {employee.FirstName} {employee.LastName}");
        return Normalize(search ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .All(name.Contains);
    }

    private void ApplyEmployeeFilter()
    {
        var selectedId = SelectedEmployee?.EmployeeID;
        bool Matches(Employee employee) => !_excludedEmployeeIds.Contains(employee.EmployeeID) && MatchesSearch(employee, SearchText);
        Employees = new ObservableCollection<Employee>(_allEmployees.Where(Matches));
        EmployeesWithStats = new ObservableCollection<Employee>(_rankedEmployees.Where(Matches));
        SelectedEmployee = Employees.FirstOrDefault(e => e.EmployeeID == selectedId);
    }

    private readonly Func<AppDbContext> _createContext;

    public EmployeeViewModel() : this(() => new AppDbContext()) { }

    public EmployeeViewModel(Func<AppDbContext> createContext)
    {
        _createContext = createContext;
        LoadShops();
        RefreshEmployees();
        WorkplaceCatalogNotifier.Register(this);
    }

    [ObservableProperty]
    private ObservableCollection<Employee> _employees = new();

    [ObservableProperty]
    private ObservableCollection<Employee> _employeesWithStats = new();

    [ObservableProperty]
    private Employee? _selectedEmployee;
    
    [ObservableProperty]
    private Shop? _selectedShop;

    [ObservableProperty]
    private Position? _selectedPosition;

    [ObservableProperty]
    private ObservableCollection<Shop> _shops = new();

    [ObservableProperty]
    private ObservableCollection<Position> _availablePositions = new();

    [ObservableProperty] 
    private bool _canSelectShop;

    [ObservableProperty]
    private bool _canSelectPosition;

    public EmployeeViewModel(MainWindowViewModel mainWindowViewModel) : this()
    {
        _mainWindowViewModel = mainWindowViewModel;
    }

    public void RefreshWorkplaceCatalog()
    {
        var shopId = SelectedShop?.ShopID;
        var positionId = SelectedPosition?.PositionID;
        LoadShops(shopId, positionId);
    }

    private void LoadShops(int? preferredShopId = null, int? preferredPositionId = null)
    {
        using var context = _createContext();

        Shops.Clear();

        foreach (var shop in context.Shops.OrderByDescending(x => x.IsDefault).ThenBy(x => x.ShopName))
        {
            Shops.Add(shop);
        }

        SelectedShop = Shops.FirstOrDefault(shop => shop.ShopID == preferredShopId)
                       ?? Shops.FirstOrDefault(shop => shop.IsDefault)
                       ?? Shops.FirstOrDefault();
        CanSelectShop = Shops.Count > 1;

        if (preferredPositionId != null)
            SelectedPosition = AvailablePositions.FirstOrDefault(position => position.PositionID == preferredPositionId)
                               ?? SelectedPosition;
    }
    
    partial void OnSelectedEmployeeChanged(Employee? value)
    {
        if (value is not null)
        {
            SelectedShop = Shops.FirstOrDefault(s => s.ShopID == value.DefaultShopID)
                ?? Shops.FirstOrDefault(s => s.IsDefault) ?? Shops.FirstOrDefault();
            SelectedPosition = AvailablePositions.FirstOrDefault(p => p.PositionID == value.DefaultPositionID)
                ?? AvailablePositions.FirstOrDefault(p => p.IsDefault) ?? AvailablePositions.FirstOrDefault();
        }
        RemoveEmployeeCommand.NotifyCanExecuteChanged();
        EditEmployeeCommand.NotifyCanExecuteChanged();
    }
    
    partial void OnSelectedShopChanged(Shop? value)
    {
        AvailablePositions.Clear();

        if (value == null)
        {
            SelectedPosition = null;
            CanSelectPosition = false;
            return;
        }

        using var context = _createContext();

        var positions = context.Positions
            .Where(x => x.ShopID == value.ShopID)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.PositionName)
            .ToList();

        foreach (var position in positions)
        {
            AvailablePositions.Add(position);
        }

        if (AvailablePositions.Count == 1)
        {
            SelectedPosition = AvailablePositions[0];
            CanSelectPosition = false;
        }
        else
        {
            SelectedPosition = AvailablePositions.FirstOrDefault(x => x.IsDefault)
                               ?? AvailablePositions.FirstOrDefault();
            CanSelectPosition = true;
        }
    }

    [RelayCommand]
    private void AddEmployee()
    {
        if (_mainWindowViewModel == null)
            return;

        var editEmployeeViewModel = new EditEmployeeViewModel(_mainWindowViewModel, this);
        var editEmployeeControl = new EditEmployeeControl();
        _mainWindowViewModel.OpenTabRequest(editEmployeeViewModel, editEmployeeControl, "Новый сотрудник");
    }

    [RelayCommand(CanExecute = nameof(CanEditEmployee))]
    private void EditEmployee()
    {
        if (_mainWindowViewModel != null && SelectedEmployee != null)
        {
            var editEmployeeViewModel = new EditEmployeeViewModel(SelectedEmployee, _mainWindowViewModel, this);
            var editEmployeeControl = new EditEmployeeControl();
            _mainWindowViewModel.OpenTabRequest(editEmployeeViewModel, editEmployeeControl,
                $"{SelectedEmployee.FirstName} {SelectedEmployee.Surname}");
        }
    }

    private bool CanEditEmployee => SelectedEmployee != null;

    [RelayCommand(CanExecute = nameof(CanRemoveEmployee))]
    private async Task RemoveEmployee()
    {
        if (SelectedEmployee == null)
            return;

        var dialog = new ConfirmDialog();

        dialog.DataContext = new ConfirmDialogViewModel
        {
            Message =
                "Вы действительно хотите удалить сотрудника " +
                $"{SelectedEmployee.Surname} {SelectedEmployee.FirstName}?" + Environment.NewLine +
                Environment.NewLine +
                "Это действие нельзя отменить." + Environment.NewLine +
                Environment.NewLine +
                "Если у сотрудника были смены, то они также будут удалены. Аналита данных за период его работы также станет неактуальна."
        };

        var owner =
            (Application.Current?.ApplicationLifetime
                as IClassicDesktopStyleApplicationLifetime)?
            .MainWindow;

        bool confirmed = false;

        if (owner != null)
        {
            confirmed = await dialog.ShowDialog<bool>(owner);
        }

        if (!confirmed)
            return;

        using var context = _createContext();

        var employee = context.Employees.Find(SelectedEmployee.EmployeeID);

        if (employee != null)
        {
            context.Employees.Remove(employee);
            context.SaveChanges();
            RefreshEmployees();
        }
    }

    private void ShowConfirmDialog()
    {
        
    }

    private bool CanRemoveEmployee => SelectedEmployee != null;

    [RelayCommand]
    private void Cancel()
    {
        _mainWindowViewModel?.CloseTabRequest(this);
    }

    private int GetWorkDaysCount(int employeeId, AppDbContext context)
    {
        var thirtyDaysAgo = DateTime.Now.AddDays(-30);

        try
        {
            return context.WorkDays
                .Where(wd => wd.Date >= thirtyDaysAgo)
                .SelectMany(wd => wd.WorkDayEmployee)
                .Count(wde => wde.EmployeeID == employeeId);
        }
        catch
        {
            return 0;
        }
    }

    public (List<Employee> employeesSorted, List<Employee> employeesWithStats) RefreshEmployees()
    {
        using var context = _createContext();
        List<Employee> allEmployees = context.Employees.ToList();

        List<Employee> employeesSorted = allEmployees
            .OrderBy(employee => employee, NameComparer)
            .ToList();

        List<Employee> employeesWithStats = allEmployees.Select(emp => new
            {
                Employee = emp,
                WorkDaysCount = GetWorkDaysCount(emp.EmployeeID, context)
            })
            .OrderByDescending(x => x.WorkDaysCount)
            .ThenBy(x => x.Employee, NameComparer)
            .Select(x => x.Employee)
            .ToList();

        _allEmployees = employeesSorted;
        _rankedEmployees = employeesWithStats;
        ApplyEmployeeFilter();

        return (employeesSorted, employeesWithStats);
    }

    public void SelectEmployeeById(int employeeId)
    {
        if (employeeId == 0)
        {
            return;
        }

        SelectedEmployee = Employees.FirstOrDefault(employee => employee.EmployeeID == employeeId);
    }

    private sealed class EmployeeNameComparer : IComparer<Employee>
    {
        public int Compare(Employee? x, Employee? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;

            return StringComparer.Ordinal.Compare(GetSortKey(x), GetSortKey(y));
        }

        private static string GetSortKey(Employee employee)
        {
            return $"{Normalize(employee.Surname)}\u0000{Normalize(employee.FirstName)}\u0000{Normalize(employee.LastName)}";
        }

        private static string Normalize(string? value) =>
            (value ?? string.Empty).Trim().ToUpperInvariant().Replace('Ё', 'Е');
    }
}
