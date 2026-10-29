using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FanShop.Models;
using FanShop.Services;
using FanShop.View;
using FanShop.ViewModels;
using FanShop.Windows;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Position = FanShop.Models.Position;
using Settings = FanShop.Models.Settings;

namespace FanShop.PriceTags.Tests;

public class EmployeeWorkflowTests
{
    [Fact]
    public async Task ExistingDatabaseUpgradePreservesEmployeesAndIsRepeatable()
    {
        using var f = new EmployeeFixture();
        using var db = f.Create();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE Employees (EmployeeID INTEGER PRIMARY KEY, FirstName TEXT NOT NULL,
                LastName TEXT NOT NULL, Surname TEXT NOT NULL, DateOfBirth TEXT NOT NULL,
                PlaceOfBirth TEXT NOT NULL, Passport TEXT NOT NULL);
            INSERT INTO Employees VALUES (1, 'Иван', 'Иванович', 'Иванов', '01.01.2000', 'СПб', '123');
            """);
        await EmployeeDefaultsSchema.EnsureAsync(db);
        await EmployeeDefaultsSchema.EnsureAsync(db);
        var employee = await db.Employees.SingleAsync();
        Assert.Equal("Иванов", employee.Surname);
        Assert.False(employee.HasPermanentPass);
        Assert.False(employee.ExcludeFromSalaryByDefault);
        Assert.Null(employee.DefaultShopID);
        Assert.Null(employee.DefaultPositionID);
    }

    [Theory]
    [InlineData(30, true, false)]
    [InlineData(31, true, true)]
    [InlineData(1, true, true)]
    [InlineData(30, false, true)]
    [InlineData(31, false, true)]
    [InlineData(1, false, true)]
    public void WeekendShiftNeedsPassRegardlessOfPermanentPass(int day, bool permanentPass, bool expected)
    {
        using var f = new EmployeeFixture(); f.Seed();
        using var db = f.Create();
        var employee = db.Employees.First();
        employee.HasPermanentPass = permanentPass;
        employee.ExcludeFromSalaryByDefault = true;
        var date = new DateTime(2026, day == 1 ? 11 : 10, day);
        var shift = EmployeeShiftDefaults.Create(employee, 1, date, db.Positions.First(), "Целый день", new SalaryService(db));
        Assert.Equal(expected, shift.IncludeInPass);
        Assert.False(shift.IncludeInSalary);
        Assert.Equal(4000m, shift.SalaryAtMoment);
    }

    [Fact]
    public void PersonalDefaultsAffectNewShiftsAndDayOverridesSurviveCardChanges()
    {
        using var f = new EmployeeFixture(); f.Seed();
        using var db = f.Create();
        var employee = db.Employees.First();
        employee.HasPermanentPass = true; employee.ExcludeFromSalaryByDefault = true;
        var position = db.Positions.First();
        var day = new WorkDay { Date = new DateTime(2026, 10, 30) };
        db.WorkDays.Add(day); db.SaveChanges();
        var shift = EmployeeShiftDefaults.Create(employee, day.WorkDayID, day.Date, position, "Полдня", new SalaryService(db));
        Assert.False(shift.IncludeInPass); Assert.False(shift.IncludeInSalary);
        Assert.Equal(2000m, shift.SalaryAtMoment);
        db.WorkDayEmployee.Add(shift); db.SaveChanges();
        shift.IncludeInPass = true; shift.IncludeInSalary = true; db.SaveChanges();
        employee.DefaultPositionID = null; employee.DefaultShopID = null; db.SaveChanges(); db.ChangeTracker.Clear();
        var saved = db.WorkDayEmployee.Single();
        Assert.True(saved.IncludeInPass); Assert.True(saved.IncludeInSalary);
        Assert.True(db.Employees.First().HasPermanentPass);
    }

    [Theory]
    [InlineData("№ п/п")]
    [InlineData("№п/п")]
    [InlineData("№\u00a0п/п")]
    public void GroupDocumentsIncludeExactDatesWeekendAndExplicitPassOverrides(string header)
    {
        using var f = new EmployeeFixture();
        var template = Path.Combine(f.Root, "template.docx");
        using (var document = WordprocessingDocument.Create(template, WordprocessingDocumentType.Document))
        {
            var part = document.AddMainDocumentPart();
            part.Document = new Document(new Body(
                new Paragraph(new Run(new Text("Дата: {DATE}{WEEKEND}"))),
                new Table(new TableRow(new TableCell(new Paragraph(new Run(new Text(header))))))));
            part.Document.Save();
        }
        var employees = new[]
        {
            new EmployeeWorkInfo { Employee = new Employee { Surname = "Иванов", HasPermanentPass = true }, IncludeInPass = true },
            new EmployeeWorkInfo { Employee = new Employee { Surname = "Петров" }, IncludeInPass = true },
            new EmployeeWorkInfo { Employee = new Employee { Surname = "Скрытый" }, IncludeInPass = false }
        };
        var dates = new[] { new DateTime(2026,10,29), new DateTime(2026,10,31), new DateTime(2026,11,1) };
        var paths = PassDocumentGenerator.WritePassDocuments(template, Path.Combine(f.Root, "documents"), dates, employees, new Settings());
        Assert.Equal(2, paths.Count);
        using var general = WordprocessingDocument.Open(paths[0], false);
        using var weekend = WordprocessingDocument.Open(paths[1], false);
        var text = general.MainDocumentPart!.Document.InnerText;
        var weekendText = weekend.MainDocumentPart!.Document.InnerText;
        Assert.Contains("29 октября 2026", text);
        Assert.DoesNotContain("31 октября", text); Assert.DoesNotContain("1 ноября", text);
        Assert.DoesNotContain("30 октября", text);
        Assert.DoesNotContain("в выходной день", text);
        Assert.Contains("в выходной день", weekendText); Assert.DoesNotContain("29 октября", weekendText);
        foreach (var content in new[] { text, weekendText })
        {
            Assert.Contains("Иванов", content); Assert.Contains("Петров", content); Assert.DoesNotContain("Скрытый", content);
        }
    }

    [Fact]
    public void MixedPermanentPassesProduceFridayForOneAndSaturdayForEveryoneInAlphabeticalOrder()
    {
        using var f = new EmployeeFixture();
        var template = Path.Combine(f.Root, "template.docx");
        using (var document = WordprocessingDocument.Create(template, WordprocessingDocumentType.Document))
        {
            var part = document.AddMainDocumentPart();
            part.Document = new Document(new Body(new Paragraph(new Run(new Text("{DATE}{WEEKEND}"))),
                new Table(new TableRow(new TableCell(new Paragraph(new Run(new Text("№п/п"))))))));
            part.Document.Save();
        }
        var friday = new WorkDay { Date = new DateTime(2026,10,30) };
        var saturday = new WorkDay { Date = new DateTime(2026,10,31) };
        var employees = new[]
        {
            new Employee { EmployeeID = 3, Surname = "Васильева" },
            new Employee { EmployeeID = 2, Surname = "Борисов", HasPermanentPass = true },
            new Employee { EmployeeID = 1, Surname = "Андреев", HasPermanentPass = true }
        };
        var assignments = employees.SelectMany(e => new[]
        {
            new WorkDayEmployee { WorkDayEmployeeID = e.EmployeeID * 2, EmployeeID = e.EmployeeID, Employee = e,
                WorkDay = friday, IncludeInPass = EmployeeShiftDefaults.RequiresPass(e, friday.Date) },
            new WorkDayEmployee { WorkDayEmployeeID = e.EmployeeID * 2 + 1, EmployeeID = e.EmployeeID, Employee = e,
                WorkDay = saturday, IncludeInPass = EmployeeShiftDefaults.RequiresPass(e, saturday.Date) }
        }).ToArray();
        var plans = PassDocumentGenerator.PlanAssignmentPasses(assignments);
        Assert.Equal(2, plans.Count);
        var ordinary = Assert.Single(plans, p => !p.Pass.IsWeekend);
        var weekendPlan = Assert.Single(plans, p => p.Pass.IsWeekend);
        Assert.Equal(friday.Date, Assert.Single(ordinary.Pass.Dates));
        Assert.Equal("Васильева", Assert.Single(ordinary.Employees).Surname);
        Assert.Equal(saturday.Date, Assert.Single(weekendPlan.Pass.Dates));
        Assert.Equal(new[] { "Андреев", "Борисов", "Васильева" }, weekendPlan.Employees.Select(e => e.Surname));
        Assert.Single(ordinary.AssignmentIds); Assert.Equal(3, weekendPlan.AssignmentIds.Count);
        var paths = PassDocumentGenerator.WritePassDocuments(template, Path.Combine(f.Root,"output"), plans, new Settings());
        Assert.Equal(2, paths.Count);
        using var weekend = WordprocessingDocument.Open(paths[1], false);
        var names = weekend.MainDocumentPart!.Document.Descendants<TableRow>().Skip(1)
            .Select(r => r.Elements<TableCell>().ElementAt(1).InnerText.Trim()).ToArray();
        Assert.Equal(new[] { "Андреев", "Борисов", "Васильева" }, names);
        Assert.DoesNotContain("30 октября", weekend.MainDocumentPart.Document.InnerText);
    }

    [Fact]
    public void WeekendOnlyAndWeekendEdgesDoNotCreateOverlappingOrdinaryPasses()
    {
        var weekendOnly = Assert.Single(PassDocumentGenerator.PlanPasses(new[] { new DateTime(2026,10,31), new DateTime(2026,11,1) }));
        Assert.True(weekendOnly.IsWeekend);
        var plans = PassDocumentGenerator.PlanPasses(new[] { new DateTime(2026,11,1), new DateTime(2026,11,2), new DateTime(2026,11,3) });
        var weekdays = Assert.Single(plans, p => !p.IsWeekend);
        Assert.Equal(new DateTime(2026,11,2), weekdays.Dates.First());
        Assert.Equal(new DateTime(2026,11,3), weekdays.Dates.Last());
        Assert.DoesNotContain("1 ноября", weekdays.DateText);
    }

    [Fact]
    public void PassPlanningHandlesWeekdaysSingleWeekendAndYearBoundary()
    {
        Assert.Empty(PassDocumentGenerator.PlanPasses(Array.Empty<DateTime>()));
        Assert.False(Assert.Single(PassDocumentGenerator.PlanPasses(new[] { new DateTime(2026,10,29) })).IsWeekend);
        Assert.True(Assert.Single(PassDocumentGenerator.PlanPasses(new[] { new DateTime(2026,10,31) })).IsWeekend);
        var plan = Assert.Single(PassDocumentGenerator.PlanPasses(new[] { new DateTime(2026,12,31), new DateTime(2027,1,1) }));
        Assert.Contains("2026", plan.DateText); Assert.Contains("2027", plan.DateText);
    }

    [AvaloniaFact]
    public void PickerRanksEmployeesByActivityInsteadOfAlphabet()
    {
        using var f = new EmployeeFixture(); f.Seed();
        int activeId;
        using (var db = f.Create())
        {
            activeId = db.Employees.OrderBy(e => e.EmployeeID).Last().EmployeeID;
            var day = new WorkDay { Date = DateTime.Today.AddDays(-1) };
            db.WorkDays.Add(day); db.SaveChanges();
            db.WorkDayEmployee.Add(new WorkDayEmployee { EmployeeID = activeId, WorkDayID = day.WorkDayID,
                PositionID = db.Positions.First().PositionID, WorkDuration = "Целый день" });
            db.SaveChanges();
        }
        var vm = new EmployeeViewModel(f.Create);
        Assert.NotEqual(activeId, vm.Employees.First().EmployeeID);
        var window = new SelectEmployeeWindow { DataContext = vm, SelectionOnly = true };
        window.Show(); window.UpdateLayout();
        var grid = window.FindControl<DataGrid>("EmployeesGrid")!;
        Assert.Equal(activeId, grid.ItemsSource.Cast<Employee>().First().EmployeeID);
        vm.SearchText = "Иван";
        Assert.Equal(activeId, grid.ItemsSource.Cast<Employee>().First().EmployeeID);
        vm.ExcludeEmployees(new[] { activeId });
        Assert.DoesNotContain(grid.ItemsSource.Cast<Employee>(), e => e.EmployeeID == activeId);
        window.Close();
    }

    [AvaloniaFact]
    public void EmployeeSearchUsesAllNamePartsAndAddingKeepsPickerOpen()
    {
        using var f = new EmployeeFixture(); f.Seed();
        var vm = new EmployeeViewModel(f.Create);
        vm.SearchText = "иван ёл";
        var employee = Assert.Single(vm.Employees);
        Assert.Equal("Ёлкин", employee.Surname);
        vm.SearchText = string.Empty;
        var window = new SelectEmployeeWindow { DataContext = vm, SelectionOnly = true };
        window.Show(); window.UpdateLayout();
        vm.SelectedEmployee = employee;
        Assert.Equal(employee.DefaultShopID, vm.SelectedShop?.ShopID);
        Assert.Equal(employee.DefaultPositionID, vm.SelectedPosition?.PositionID);
        window.FindControl<Button>("AddSelectedButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.True(window.IsVisible); Assert.Single(window.Selections);
        Assert.DoesNotContain(vm.Employees, e => e.EmployeeID == employee.EmployeeID);
        vm.SearchText = "ёлкин"; Assert.Empty(vm.Employees);
        vm.SearchText = string.Empty;
        vm.SelectedEmployee = Assert.Single(vm.Employees);
        window.FindControl<Button>("AddSelectedButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(2, window.Selections.Count); Assert.True(window.IsVisible);
        Assert.Empty(vm.Employees);
        window.Close();
    }

    [AvaloniaFact]
    public void LongEmployeeTablesStayInsideViewportAndScroll()
    {
        using var f = new EmployeeFixture(); f.Seed(60);
        var vm = new EmployeeViewModel(f.Create);
        var view = new EmployeeControl { DataContext = vm };
        var window = new Window { Content = view, Width = 1200, Height = 600 };
        window.Show(); window.UpdateLayout();
        var grids = view.GetVisualDescendants().OfType<DataGrid>().ToList();
        Assert.Equal(2, grids.Count);
        foreach (var grid in grids)
        {
            Assert.True(grid.Bounds.Height < window.Height - 100);
            var scroll = grid.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ScrollBar>()
                .First(x => x.Orientation == Avalonia.Layout.Orientation.Vertical);
            Assert.True(scroll.Maximum > 0);
            grid.ScrollIntoView(vm.Employees.Last(), null);
        }
        window.UpdateLayout();
        window.CaptureRenderedFrame()?.Save(Path.Combine(Path.GetTempPath(), "fanshop-employee-scroll.png"));
        window.Close();
    }
}

internal sealed class EmployeeFixture : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "fanshop-employee-test-" + Guid.NewGuid().ToString("N"));
    public EmployeeFixture() => Directory.CreateDirectory(Root);
    public AppDbContext Create() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={Path.Combine(Root, "test.db")}").Options);
    public void Seed(int count = 2)
    {
        using var db = Create(); db.Database.EnsureCreated();
        var shop = new Shop { ShopName = "Магазин" };
        var position = new Position { PositionName = "Продавец", Shop = shop };
        db.Positions.Add(position); db.SaveChanges();
        db.SalaryHistories.Add(new SalaryHistory { PositionID = position.PositionID, StartDate = new DateTime(2026,1,1), Salary = 4000m });
        for (var i = 0; i < count; i++)
            db.Employees.Add(new Employee { Surname = i == 0 ? "Ёлкин" : $"Сотрудник {i:00}", FirstName = "Иван", LastName = "Иванович",
                DefaultShopID = shop.ShopID, DefaultPositionID = position.PositionID });
        db.SaveChanges();
    }
    public void Dispose() { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(Root, true); }
}
