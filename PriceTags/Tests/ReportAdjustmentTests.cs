using System.Diagnostics;
using ClosedXML.Excel;
using FanShop.PriceTags.Services;
using FanShop.ReportAdjustment.Models;
using FanShop.ReportAdjustment.Services;
using Xunit;

namespace FanShop.PriceTags.Tests;

public class ReportAdjustmentTests
{
    private readonly DiscountCalculationService _calculator = new();
    private readonly RowClassificationService _classifier = new();
    private readonly AdjustmentSettings _default = new();
    private AdjustmentSearchService Engine => new(_calculator, new SolutionScoringService(), new SolutionValidator(_calculator));
    private SalesReportExcelLoader Loader => new(_classifier, _calculator);
    private SalesReportRow Row(int id, decimal price, string characteristic = "MISC", string name = "Товар", decimal quantity = 1, decimal discount = 0)
        => _classifier.Classify(new() { RowNumber = id, ProductName = name, Characteristic = characteristic,
            Quantity = quantity, BasePrice = price, CurrentManualDiscountPercent = discount,
            CurrentTotal = _calculator.Calculate(quantity, price, discount, DiscountRounding.MathematicalRound) }, _default);
    private Task<SearchResult> Search(SalesReportRow[] rows, decimal target, AdjustmentSettings? settings = null)
        => Engine.SearchAsync(rows, new(target), settings ?? _default);
    [Theory]
    [InlineData(2199, 1, 3, 2133)]
    [InlineData(2199, 1, 7, 2045)]
    [InlineData(150, 2, 7, 279)]
    [InlineData(3999, 1, 30, 2799)]
    public void MathematicalDiscountUsesQuantityAndRoundsDiscount(decimal price, decimal quantity, decimal discount, decimal total)
        => Assert.Equal(total, _calculator.Calculate(quantity, price, discount, DiscountRounding.MathematicalRound));
    [Fact] public void DecimalDiscountKeepsKopecks() => Assert.Equal(2133.03m, _calculator.Calculate(1, 2199, 3, DiscountRounding.DecimalRound2));
    [Fact] public void HalfRublesAreRoundedAwayFromZero() => Assert.Equal(48, _calculator.Calculate(1, 50, 3, DiscountRounding.MathematicalRound));
    [Fact] public void HundredPercentDiscountNeverCreatesNegativeTotalBelowOneRuble()
        => Assert.Equal(0, _calculator.Calculate(1, .6m, 100, DiscountRounding.MathematicalRound));
    [Theory]
    [InlineData("MISC")][InlineData(" misc ")][InlineData(" MiSc ")]
    public void MiscClassificationIgnoresCaseAndSpaces(string characteristic) => Assert.True(Row(1, 100, characteristic).IsMisc);
    [Fact] public void ServiceIsClassifiedByNormalizedNameWithoutMisc()
    { var row = Row(1, 100, "S", "  нанесение   ИМЯ/фамилия  "); Assert.True(row.IsService); Assert.False(row.IsSizedProduct); Assert.True(row.UseInSearch); }
    [Fact] public void AllThreeServicesAreRecognized()
    { foreach (var name in _default.ServiceNames) Assert.Equal(RowPriorityClass.Service, Row(1, 100, "", name).PriorityClass); }
    [Theory] [InlineData("S")][InlineData("XXL")][InlineData("XXXL")][InlineData("134-140")][InlineData("42")]
    public void SizedProductsAreDisabledByDefault(string size)
    { var row = Row(1, 2199, size); Assert.True(row.IsSizedProduct); Assert.False(row.UseInSearch); }
    [Fact] public void ColorIsOrdinaryAndEnabled() { var row = Row(1, 100, "Голубой"); Assert.Equal(RowPriorityClass.Ordinary, row.PriorityClass); Assert.True(row.UseInSearch); }
    [Fact] public async Task ProtectedMiscCannotChangeOrBeExcluded()
    { var row = Row(1, 1000) with { IsProtected = true, CanExclude = true }; Assert.Empty((await Search([row], 30)).Solutions); Assert.Empty(Engine.GetOptions(row, _default)); }
    [Fact] public async Task ProtectedServiceNeverAppears()
    { var result = await Search([Row(1, 1000, name: _default.ServiceNames[0]) with { IsProtected = true }, Row(2, 1000)], 30); Assert.All(result.Solutions, s => Assert.DoesNotContain(s.Changes, c => c.RowNumber == 1)); }
    [Fact] public async Task OneChangeWinsOverThreeEvenWhenDiscountPrioritiesFavorThree()
    {
        var settings = _default with { AllowedDiscounts = [3, 10], DiscountPriorities = [10, 3] };
        var result = await Search([Row(1, 1000), Row(2, 100), Row(3, 100), Row(4, 100)], 30, settings);
        Assert.Single(result.Solutions[0].Changes); Assert.Equal(1, result.Solutions[0].Changes[0].RowNumber);
        Assert.Contains(result.Solutions, s => s.Changes.Count == 3);
    }
    [Fact] public async Task ServiceWinsMiscAndSizedWhenChangeCountMatches()
    {
        var result = await Search([Row(1, 1000, "S") with { UseInSearch = true }, Row(2, 1000), Row(3, 1000, name: _default.ServiceNames[0])], 30);
        Assert.Equal(3, result.Solutions[0].Changes.Single().RowNumber);
        Assert.Equal(2, result.Solutions[1].Changes.Single().RowNumber);
    }
    [Fact] public async Task MiscWinsSizedWhenUserAllowsBoth()
    { var result = await Search([Row(1, 1000, "134-140") with { UseInSearch = true }, Row(2, 1000)], 30); Assert.Equal(2, result.Solutions[0].Changes.Single().RowNumber); }
    [Fact] public async Task ExactPartialSumsProduce180AndPreserveControlTotal()
    {
        var rows = new[] { Row(14, 150, quantity: 2, name: "Ручка"), Row(21, 1060, name: _default.ServiceNames[0]) };
        var result = await Search(rows, 180); var best = result.Solutions[0];
        Assert.Equal(180, best.Changes.Sum(c => c.Delta)); Assert.Equal(2, best.Changes.Count);
        Assert.Equal(best.ControlTotalBefore, best.ControlTotalAfter); Assert.Equal(1360, best.ControlTotalBefore);
    }
    [Fact] public async Task ApproximateSolutionIsNeverPresentedAsExact()
    { var result = await Search([Row(1, 100)], 2, _default with { ShowNearest = true }); Assert.Empty(result.Solutions); Assert.Equal(1m, result.NearestDifference); Assert.Contains("Точная комбинация не найдена", result.Message); }
    [Fact] public async Task DifferenceOfOneKopeckFailsExactness()
    { Assert.Empty((await Search([Row(1, 1000)], 30.01m)).Solutions); }
    [Fact] public async Task NonstandardDiscountAndImportedTotalArePreservedUnchanged()
    {
        var special = Row(1, 499, discount: 30.02m) with { CurrentTotal = 349.01m };
        var result = await Search([special, Row(2, 1000)], 30);
        var best = result.Solutions[0]; Assert.DoesNotContain(best.Changes, c => c.RowNumber == 1);
        Assert.Equal(1349.01m, best.ControlTotalAfter); Assert.False(best.RoundingMatchesReport);
        var zero = await Search([special], 0); Assert.Empty(zero.Solutions[0].Changes); Assert.Equal(349.01m, zero.Solutions[0].ControlTotalAfter);
    }
    [Fact] public void DecreaseIsProhibitedByDefault()
    { Assert.DoesNotContain(Engine.GetOptions(Row(1, 2199, discount: 7), _default), o => o.DiscountPercent == 3); }
    [Fact] public async Task DecreaseEnablesSignedDeltaCombination()
    {
        var settings = _default with { AllowedDiscounts = [3, 7], DiscountPriorities = [3, 7], AllowDiscountDecrease = true };
        var rows = new[] { Row(1, 2199, discount: 7), Row(2, 1000) };
        // -88 from 7→3 plus +30 from 0→3: target would be negative (unsupported).
        // +88 from 3→7 plus -40 from 7→3 on another row produces +48.
        rows = [Row(1, 2199, discount: 3), Row(2, 1000, discount: 7)];
        var result = await Search(rows, 48, settings); Assert.NotEmpty(result.Solutions);
        Assert.Contains(result.Solutions[0].Changes, c => c.OriginalDiscount == 7 && c.NewDiscount == 3);
        Assert.Empty((await Search(rows, 48, settings with { AllowDiscountDecrease = false })).Solutions);
    }
    [Fact] public async Task ExclusionRequiresPermissionAndIsExplicit()
    {
        var row = Row(1, 100) with { CanChangeDiscount = false };
        Assert.Empty((await Search([row], 100)).Solutions);
        var result = await Search([row with { CanExclude = true }], 100);
        Assert.Equal(AdjustmentAction.Exclude, result.Solutions[0].Changes.Single().Action);
    }
    [Fact] public async Task DiscountOnlySolutionsRankAboveExclusions()
    { var result = await Search([Row(1, 30) with { CanExclude = true }, Row(2, 1000)], 30); Assert.Equal(0, result.Solutions[0].Score.Exclusions); Assert.Contains(result.Solutions, s => s.Score.Exclusions == 1); }
    [Fact] public async Task DisabledRowCannotChange()
    { Assert.Empty((await Search([Row(1, 1000) with { UseInSearch = false }], 30)).Solutions); }
    [Fact] public async Task CorrectivePositionsUseQuantityAndAllowedDiscount()
    {
        var request = new AdjustmentRequest(0, [new("Ручка", "MISC", 2, 150, 7)]);
        Assert.Equal(279, request.GetRequiredAmount(_calculator, _default));
        var result = await Engine.SearchAsync([Row(1, 930)], request, _default);
        Assert.Equal(279, result.Solutions[0].RequiredAmount); Assert.Equal(930, result.Solutions[0].ControlTotalAfter);
    }
    [Fact] public async Task MultipleCorrectivePositionsSumBeforeSearch()
    { var result = await Engine.SearchAsync([Row(1, 1000)], new(0, [new("А", "", 2, 5), new("Б", "", 1, 20)]), _default); Assert.Equal(30, result.Solutions[0].RequiredAmount); }
    [Fact] public async Task ValidatorRejectsTamperedQuantityTotalsPermissionsAndUnknownRows()
    {
        var rows = new[] { Row(1, 1000) }; var solution = (await Search(rows, 30)).Solutions[0]; var change = solution.Changes[0];
        var validator = new SolutionValidator(_calculator);
        foreach (var wrong in new[] { change with { Quantity = 2 }, change with { OriginalTotal = 999 }, change with { NewTotal = 971 },
            change with { NewDiscount = 4 }, change with { RowNumber = 99 }, change with { Action = AdjustmentAction.Exclude } })
            Assert.False(validator.Validate(rows, new(30), _default, solution with { Changes = [wrong] }).IsValid);
        Assert.False(validator.Validate([rows[0] with { IsProtected = true }], new(30), _default, solution).IsValid);
        Assert.False(validator.Validate([rows[0] with { CanChangeDiscount = false }], new(30), _default, solution).IsValid);
        Assert.False(validator.Validate(rows, new(30), _default, solution with { ControlTotalAfter = 1000.01m }).IsValid);
        Assert.False(validator.Validate(rows, new(30), _default, solution with { Changes = [change, change] }).IsValid);
    }
    [Fact] public async Task ValidatorRejectsUnauthorizedDecrease()
    {
        var settings = _default with { AllowedDiscounts = [3, 7], DiscountPriorities = [3, 7], AllowDiscountDecrease = true };
        var rows = new[] { Row(1, 2199, discount: 3), Row(2, 1000, discount: 7) };
        var solution = (await Search(rows, 48, settings)).Solutions[0];
        Assert.False(new SolutionValidator(_calculator).Validate(rows, new(48), settings with { AllowDiscountDecrease = false }, solution).IsValid);
    }
    [Fact] public async Task ConfigurableDiscountPriorityOrdersEqualOneRowSolutions()
    {
        var result = await Search([Row(1, 1000), Row(2, 428.57m)], 30);
        Assert.Equal(3, result.Solutions[0].Changes.Single().NewDiscount);
        result = await Search([Row(1, 1000), Row(2, 428.57m)], 30, _default with { DiscountPriorities = [7, 3, 15, 10, 30, 0] });
        Assert.Equal(7, result.Solutions[0].Changes.Single().NewDiscount);
    }
    [Fact] public async Task SolutionCountIsBoundedAndDistinct()
    {
        var result = await Search(Enumerable.Range(1, 20).Select(i => Row(i, 1000)).ToArray(), 30, _default with { MaxSolutions = 4 });
        Assert.Equal(4, result.Solutions.Count); Assert.Equal(4, result.Solutions.Select(s => string.Join(',', s.Changes.Select(c => c.RowNumber))).Distinct().Count());
    }
    [Fact] public async Task LargeReportUsesBoundedPartialSumsAndRemainsResponsive()
    {
        var rows = Enumerable.Range(1, 300).Select(i => Row(i, 100 + i % 20 * 10)).ToArray(); var watch = Stopwatch.StartNew();
        var result = await Search(rows, 180, _default with { MaxSolutions = 3 });
        Assert.NotEmpty(result.Solutions); Assert.True(watch.Elapsed < TimeSpan.FromSeconds(15));
        Assert.True(result.Transitions < 2_000_001); Assert.All(result.Solutions, s => Assert.Equal(s.ControlTotalBefore, s.ControlTotalAfter));
    }
    [Fact] public async Task StateAndTimeLimitsAreNeverReportedAsProofOfNoSolution()
    {
        var result = await Search([Row(1, 1000), Row(2, 1500), Row(3, 1900)], 999, _default with { ShowNearest = true, MaxPartialSums = 1 });
        Assert.True(result.WasLimited); Assert.Contains("Поиск ограничен", result.Message); Assert.DoesNotContain("Точная комбинация не найдена", result.Message);
        var transitions = await Search([Row(1, 1000), Row(2, 1500)], 40, _default with { MaxTransitions = 1 }); Assert.True(transitions.WasLimited);
    }
    [Fact] public async Task CancellationIsObserved()
    { using var source = new CancellationTokenSource(); source.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Engine.SearchAsync([Row(1, 1000)], new(30), _default, cancellationToken: source.Token)); }
    [Fact] public async Task RunningSearchReportsProgressAndCanBeCancelled()
    {
        using var source = new CancellationTokenSource(); var count = 0;
        var progress = new InlineProgress<SearchProgress>(p => { count++; source.Cancel(); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Engine.SearchAsync(Enumerable.Range(1, 300).Select(i => Row(i, 1000 + i)).ToArray(),
            new(180), _default, progress, source.Token));
        Assert.True(count > 0);
    }
    [Fact] public async Task DynamicProgrammingMatchesTinyExhaustiveReferenceIncludingSignedDeltas()
    {
        var random = new Random(713); var scoring = new SolutionScoringService();
        var settings = _default with { AllowedDiscounts = [3, 7, 15], DiscountPriorities = [3, 7, 15], AllowDiscountDecrease = true, MaxSolutions = 5 };
        for (var sample = 0; sample < 12; sample++)
        {
            var rows = Enumerable.Range(1, 5).Select(i => Row(i, random.Next(100, 1500), discount: i % 2 == 0 ? 7 : 0)
                with { CanExclude = i == 1 }).ToArray();
            var target = random.Next(1, 160); var scores = new List<SolutionScore>();
            void Visit(int index, decimal sum, SolutionScore score)
            {
                if (index == rows.Length) { if (sum == target) scores.Add(score); return; }
                Visit(index + 1, sum, score);
                foreach (var option in Engine.GetOptions(rows[index], settings))
                    Visit(index + 1, sum + Money.FromMinor(option.Delta), score + scoring.Score(rows[index], option, settings));
            }
            // Deliberately tiny reference only in tests, to verify DP pruning and best-K dominance.
            Visit(0, 0, default);
            var result = await Search(rows, target, settings); Assert.False(result.WasLimited);
            Assert.Equal(scores.Order().Take(settings.MaxSolutions), result.Solutions.Select(s => s.Score));
        }
    }
    [Theory]
    [InlineData("3", "3")][InlineData("3,0", "3")][InlineData("3%", "3")][InlineData("30,02", "30.02")]
    [InlineData("2 133", "2133")][InlineData("2 133,00", "2133")][InlineData("2\u00a0133 ₽", "2133")]
    public void LocaleNumbersAreDecimal(string text, string expected)
        => Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), SalesReportExcelLoader.ParseNumber(text));
    [Fact] public void ImportFindsHeadersAndKeepsOptionalMetadataAndNativePercent()
    {
        using var fixture = new ReportFixture();
        fixture.Write([Row(14, 2199, "134-140", discount: 3)]);
        using (var book = new XLWorkbook(fixture.Path))
        {
            var sheet = book.Worksheet(1); sheet.Cell(3, 1).Value = " Продажи за день ";
            sheet.Cell(5, 6).Value = .03m; sheet.Cell(5, 6).Style.NumberFormat.Format = "0.00%";
            sheet.Cell(5, 8).Value = "2 133,00 ₽"; sheet.Cell(5, 7).Value = "66,00";
            sheet.Cell(5, 9).Value = "20%"; sheet.Cell(5, 10).Value = 355.5m; sheet.Cell(5, 11).Value = "Реализация №7";
            book.Save();
        }
        var bytes = File.ReadAllBytes(fixture.Path); var report = Loader.Load(fixture.Path, _default); var row = report.Rows.Single();
        Assert.Equal(3, row.CurrentManualDiscountPercent); Assert.Equal(2133, row.CurrentTotal); Assert.Equal(66, row.CurrentManualDiscountAmount);
        Assert.Equal("20%", row.VatRate); Assert.Equal(355.5m, row.VatAmount); Assert.Equal("Реализация №7", row.RealizationDocument);
        Assert.True(report.RoundingMatches); Assert.Equal(bytes, File.ReadAllBytes(fixture.Path));
    }
    [Fact] public void ImportWarnsOnRoundingMismatchAndStrategyCanBeSwitched()
    {
        using var fixture = new ReportFixture(); fixture.Write([Row(1, 2199, discount: 3) with { CurrentTotal = 2133.03m }]);
        var report = Loader.Load(fixture.Path, _default); Assert.False(report.RoundingMatches); Assert.Equal(1, report.MismatchedRows);
        Assert.Contains(report.Warnings, x => x.Contains(DiscountCalculationService.MismatchWarning));
        Assert.True(Loader.Load(fixture.Path, _default with { Rounding = DiscountRounding.DecimalRound2 }).RoundingMatches);
    }
    [Fact] public void InvalidImportFailsRatherThanSilentlyDroppingControlMoney()
    {
        using var fixture = new ReportFixture(); fixture.Write([Row(1, 1000), Row(2, 1000)]);
        using (var book = new XLWorkbook(fixture.Path)) { book.Worksheet(1).Cell(6, 4).Value = "не число"; book.Save(); }
        Assert.Throws<InvalidDataException>(() => Loader.Load(fixture.Path, _default));
    }
    [Fact] public void DuplicateRowNumbersAreRejected()
    { using var fixture = new ReportFixture(); fixture.Write([Row(1, 100), Row(1, 200)]); Assert.Throws<InvalidDataException>(() => Loader.Load(fixture.Path, _default)); }
    [Fact] public void MissingRequiredColumnIsRejected()
    { using var fixture = new ReportFixture(); fixture.Write([Row(1, 100)]); using (var book = new XLWorkbook(fixture.Path)) { book.Worksheet(1).Cell(4, 4).Value = "неизвестно"; book.Save(); } Assert.Throws<InvalidDataException>(() => Loader.Load(fixture.Path, _default)); }
    [Fact] public void SettingsRoundTripStoresPreferencesWithoutReportRows()
    {
        using var fixture = new ReportFixture(); using var storage = new LocalStorage(System.IO.Path.GetDirectoryName(fixture.Path)!);
        var service = new AdjustmentSettingsService(storage);
        var settings = _default with { AllowedDiscounts = [3, 7], DiscountPriorities = [7, 3], ServiceNames = ["Услуга"],
            AllowDiscountDecrease = true, MaxSolutions = 4, LastReportDirectory = "/tmp", ShowNearest = true };
        service.Save(settings); var loaded = service.Load(); Assert.Equal(settings.AllowedDiscounts, loaded.AllowedDiscounts);
        Assert.Equal(settings.DiscountPriorities, loaded.DiscountPriorities); Assert.True(loaded.AllowDiscountDecrease);
        Assert.Equal("/tmp", loaded.LastReportDirectory); Assert.Equal(4, loaded.MaxSolutions); Assert.Equal(settings.ServiceNames, loaded.ServiceNames);
    }
    [Fact] public async Task SyntheticReportsAThroughGAreImportedAndVerified()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "FanShop.csproj"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var samples = System.IO.Path.Combine(directory!.FullName, "PriceTags", "Samples", "ReportAdjustment"); Directory.CreateDirectory(samples);
        var cases = new (string Name, SalesReportRow[] Rows, decimal Amount)[]
        {
            ("A_MISC.xlsx", [Row(1, 1000)], 30),
            ("B_MISC_Clothing.xlsx", [Row(1, 1000), Row(2, 1000, "S")], 30),
            ("C_OneVsThree.xlsx", [Row(1, 1000), Row(2, 100), Row(3, 100), Row(4, 100)], 30),
            ("D_NoExact.xlsx", [Row(1, 100)], 2),
            ("E_Nonstandard.xlsx", [Row(1, 499, discount: 30.02m), Row(2, 1000)], 30),
            ("F_Quantity.xlsx", [Row(14, 150, quantity: 2), Row(21, 1060, name: _default.ServiceNames[0])], 180),
            ("G_Large.xlsx", Enumerable.Range(1, 300).Select(i => Row(i, 100 + i % 20 * 10)).ToArray(), 180)
        };
        var evidence = new List<string>();
        foreach (var test in cases)
        {
            var path = System.IO.Path.Combine(samples, test.Name); ReportFixture.WriteFile(path, test.Rows);
            var report = Loader.Load(path, _default); var result = await Engine.SearchAsync(report.Rows, new(test.Amount), _default with { MaxSolutions = 3 });
            if (test.Name.StartsWith('D')) Assert.Empty(result.Solutions);
            else { Assert.NotEmpty(result.Solutions); Assert.All(result.Solutions, s => Assert.Equal(s.ControlTotalBefore, s.ControlTotalAfter)); }
            if (test.Name.StartsWith('C')) Assert.Single(result.Solutions[0].Changes);
            if (test.Name.StartsWith('B'))
            {
                Assert.All(result.Solutions, s => Assert.DoesNotContain(s.Changes, c => c.RowNumber == 2));
                // Explicit second run permits the sized row and demonstrates both exact alternatives.
                result = await Engine.SearchAsync(report.Rows.Select(r => r with { UseInSearch = true }).ToArray(), new(test.Amount), _default);
                Assert.Equal(1, result.Solutions[0].Changes.Single().RowNumber);
                Assert.Contains(result.Solutions, s => s.Changes.Single().RowNumber == 2);
            }
            evidence.Add($"{test.Name}: корректировка={test.Amount}; строк={report.Rows.Count}; вариантов={result.Solutions.Count}; ограничен={result.WasLimited}; переходов={result.Transitions}; время={result.Elapsed.TotalMilliseconds:F0}ms; разница={(result.Solutions.Count > 0 ? result.Solutions[0].ControlTotalAfter-result.Solutions[0].ControlTotalBefore : null)}");
        }
        File.WriteAllLines(System.IO.Path.Combine(samples, "verification.txt"), evidence);
    }
}

internal sealed class ReportFixture : IDisposable
{
    private readonly string _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "fanshop-report-test-" + Guid.NewGuid());
    public string Path => System.IO.Path.Combine(_root, "report.xlsx");
    public ReportFixture() => Directory.CreateDirectory(_root);
    public void Write(IReadOnlyList<SalesReportRow> rows) => WriteFile(Path, rows);
    public static void WriteFile(string path, IReadOnlyList<SalesReportRow> rows)
    {
        using var book = new XLWorkbook(); var sheet = book.AddWorksheet("Продажи");
        string[] headers = ["N", " Номенклатура ", "Характеристика", "Продано", "Цена", "% руч.", "Сумма руч.", "Сумма", "Ставка НДС", "Сумма НДС", "Документ реализации"];
        for (var i = 0; i < headers.Length; i++) sheet.Cell(4, i + 1).Value = headers[i];
        var index = 5;
        foreach (var row in rows)
        {
            sheet.Cell(index, 1).Value = row.RowNumber; sheet.Cell(index, 2).Value = row.ProductName;
            sheet.Cell(index, 3).Value = row.Characteristic; sheet.Cell(index, 4).Value = row.Quantity;
            sheet.Cell(index, 5).Value = row.BasePrice; sheet.Cell(index, 6).Value = row.CurrentManualDiscountPercent;
            sheet.Cell(index, 7).Value = row.CurrentManualDiscountAmount ?? row.Quantity * row.BasePrice - row.CurrentTotal;
            sheet.Cell(index, 8).Value = row.CurrentTotal; index++;
        }
        sheet.Cell(index, 2).Value = "Итого"; sheet.Cell(index, 8).Value = rows.Sum(r => r.CurrentTotal);
        sheet.Row(4).Style.Font.Bold = true; sheet.Columns().AdjustToContents(); book.SaveAs(path);
    }
    public void Dispose() => Directory.Delete(_root, true);
}
internal sealed class InlineProgress<T>(Action<T> callback) : IProgress<T>
{ public void Report(T value) => callback(value); }
