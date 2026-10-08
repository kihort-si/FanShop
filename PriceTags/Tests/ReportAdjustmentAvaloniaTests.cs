using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FanShop.PriceTags.Services;
using FanShop.ReportAdjustment.Models;
using FanShop.ReportAdjustment.Services;
using FanShop.View;
using FanShop.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FanShop.PriceTags.Tests;

public class ReportAdjustmentAvaloniaTests
{
    private static SalesReportRow Row(int number, decimal price, string size = "MISC", decimal? total = null, decimal discount = 0)
        => new() { RowNumber = number, ProductName = "Товар", Characteristic = size, Quantity = 1, BasePrice = price,
            CurrentTotal = total ?? price, CurrentManualDiscountPercent = discount };
    private static ReportAdjustmentViewModel Create(LocalStorage storage)
    {
        var calculator = new DiscountCalculationService(); var classifier = new RowClassificationService();
        var validator = new SolutionValidator(calculator);
        return new(new(classifier, calculator), new(calculator, new(), validator), calculator, classifier, validator,
            new(storage), NullLogger<ReportAdjustmentViewModel>.Instance);
    }
    [AvaloniaFact]
    public async Task IntegratedReportViewImportsFindsAndDisplaysValidatedPreview()
    {
        using var fixture = new ReportFixture(); fixture.Write([Row(1, 1000), Row(2, 1000, "S")]);
        using var storage = new LocalStorage(Path.GetDirectoryName(fixture.Path)!); var vm = Create(storage);
        await vm.ImportReportAsync(fixture.Path); Assert.Equal(2, vm.Rows.Count); Assert.False(vm.Rows[1].UseInSearch);
        vm.RequiredAmount = "30,00"; await vm.FindSolutionsAsync(); Assert.NotEmpty(vm.Solutions);
        Assert.Single(vm.PreviewRows); Assert.Contains("Разница: 0,00", vm.SolutionSummary);
        Assert.Contains("Скидка: 0% → 3%", vm.SelectedSolution!.Instruction);
        var view = new ReportAdjustmentControl { DataContext = vm }; var window = new Window { Content = view, Width = 1420, Height = 1100 };
        window.Show(); window.UpdateLayout();
        Assert.True(view.Bounds.Width > 1000);
        Assert.Contains(view.GetVisualDescendants().OfType<Button>(), b => Equals(b.Content, "Найти варианты"));
        window.CaptureRenderedFrame()?.Save(Path.Combine(Path.GetTempPath(), "fanshop-report-adjustment-ui.png"));
        var scroll = view.GetVisualDescendants().OfType<ScrollViewer>().First();
        scroll.Offset = new Avalonia.Vector(0, 10000); window.UpdateLayout();
        window.CaptureRenderedFrame()?.Save(Path.Combine(Path.GetTempPath(), "fanshop-report-adjustment-preview.png"));
        scroll.Offset = new Avalonia.Vector(0, 0); window.UpdateLayout();
        var grid = view.GetVisualDescendants().OfType<DataGrid>().Single(g => g.Name == "ReportRowsGrid");
        grid.SelectedItems.Add(vm.Rows[0]); grid.SelectedItems.Add(vm.Rows[1]);
        Assert.Equal(2, vm.SelectedRows.Count); vm.ProtectSelectedCommand.Execute(null);
        Assert.All(vm.Rows, r => Assert.True(r.IsProtected)); Assert.Empty(vm.Solutions);
        vm.UnprotectSelectedCommand.Execute(null); Assert.All(vm.Rows, r => Assert.False(r.IsProtected));
        vm.ProtectSizedCommand.Execute(null); Assert.True(vm.Rows[1].IsProtected);
        window.Close(); await vm.ShutdownAsync();
    }
    [AvaloniaFact]
    public async Task SizeExpansionRequiresExplicitUserCommand()
    {
        using var fixture = new ReportFixture(); fixture.Write([Row(1, 1000, "S")]);
        using var storage = new LocalStorage(Path.GetDirectoryName(fixture.Path)!); var vm = Create(storage);
        await vm.ImportReportAsync(fixture.Path); vm.RequiredAmount = "30"; await vm.FindSolutionsAsync();
        Assert.Empty(vm.Solutions); Assert.True(vm.CanExpand); Assert.False(vm.Rows[0].UseInSearch);
        await vm.ExpandSearchCommand.ExecuteAsync(null); Assert.True(vm.Rows[0].UseInSearch); Assert.NotEmpty(vm.Solutions);
        vm.ShowFullReport = true; Assert.Single(vm.PreviewRows);
        vm.Rows[0].IsProtected = true; Assert.Empty(vm.Solutions); Assert.Null(vm.SelectedSolution);
    }
    [AvaloniaFact]
    public async Task RoundingMismatchBlocksSearchUntilExplicitPreviewConsent()
    {
        using var fixture = new ReportFixture(); fixture.Write([Row(1, 2199, total: 2133.03m, discount: 3), Row(2, 1000)]);
        using var storage = new LocalStorage(Path.GetDirectoryName(fixture.Path)!); var vm = Create(storage);
        await vm.ImportReportAsync(fixture.Path); vm.RequiredAmount = "30"; Assert.True(vm.HasRoundingWarning);
        await vm.FindSolutionsAsync(); Assert.Empty(vm.Solutions); Assert.Contains("явно разрешите", vm.Status);
        vm.AllowMismatchPreview = true; await vm.FindSolutionsAsync(); Assert.NotEmpty(vm.Solutions);
        Assert.Contains("точность в 1С не подтверждена", vm.SolutionSummary);
        vm.RequiredAmount = "31"; Assert.Empty(vm.Solutions);
        vm.Rounding = DiscountRounding.DecimalRound2; Assert.False(vm.HasRoundingWarning);
    }
    [AvaloniaFact]
    public async Task EditableCorrectivePositionsAndSettingsInvalidateOldResults()
    {
        using var fixture = new ReportFixture(); fixture.Write([Row(1, 1000)]);
        using var storage = new LocalStorage(Path.GetDirectoryName(fixture.Path)!); var vm = Create(storage);
        await vm.ImportReportAsync(fixture.Path); vm.UsePositions = true; vm.AddPositionCommand.Execute(null);
        vm.Positions[0].ProductName = "Позиция"; vm.Positions[0].Quantity = "2"; vm.Positions[0].BasePrice = "15";
        await vm.FindSolutionsAsync(); Assert.NotEmpty(vm.Solutions);
        Assert.Contains("Официально учитываемые позиции", vm.SelectedSolution!.Instruction);
        vm.Positions[0].BasePrice = "20"; Assert.Empty(vm.Solutions);
        vm.UsePositions = false; vm.RequiredAmount = "30"; await vm.FindSolutionsAsync(); Assert.NotEmpty(vm.Solutions);
        vm.AllowedDiscounts = "3; 7"; Assert.Empty(vm.Solutions);
        vm.DiscountPriorities = "7; 3"; vm.ServiceNames = "Товар"; vm.SaveSettingsCommand.Execute(null);
        Assert.True(vm.Rows[0].Original.IsService); Assert.Equal([7m, 3m], new AdjustmentSettingsService(storage).Load().DiscountPriorities);
    }
}
