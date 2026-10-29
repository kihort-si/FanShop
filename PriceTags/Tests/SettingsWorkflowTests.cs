using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FanShop.Services;
using FanShop.View;
using FanShop.ViewModels;
using Xunit;
using Settings = FanShop.Models.Settings;

namespace FanShop.PriceTags.Tests;

public class SettingsWorkflowTests
{
    [AvaloniaFact]
    public async Task HeadPositionLoadsSavesAndReachesWordWithoutRefreshingWorkplaceCatalog()
    {
        using var f = new EmployeeFixture(); f.Seed();
        var settingsPath = Path.Combine(f.Root, "settings.json");
        var saved = new Settings { HeadPosition = "Директор", Head = "Руководитель", LastSeenWhatsNewVersion = "2.2.1" };
        Assert.True(saved.TrySave(out _, settingsPath));
        var observer = new CountingObserver(); WorkplaceCatalogNotifier.Register(observer);
        var main = new MainWindowViewModel();
        var vm = new SettingsViewModel(main, f.Create, settingsPath);
        var view = new SettingsControl { DataContext = vm };
        var window = new Window { Content = view, Width = 1000, Height = 800 };
        window.Show(); window.UpdateLayout();
        var editor = view.FindControl<TextBox>("HeadPositionEditor")!;
        Assert.Equal("Директор", editor.Text);
        editor.Text = "Генеральный директор";
        Assert.Equal("Генеральный директор", vm.HeadPosition);
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Empty(vm.SaveError); Assert.False(vm.IsSaving); Assert.True(vm.CanEditSettings);
        Assert.Equal(0, observer.Calls);
        var reloaded = Settings.Load(settingsPath);
        Assert.Equal("Генеральный директор", reloaded.HeadPosition);
        Assert.Equal("Руководитель", reloaded.Head);
        Assert.Equal("2.2.1", reloaded.LastSeenWhatsNewVersion);
        Assert.Equal("Генеральный директор", new SettingsViewModel(main, f.Create, settingsPath).HeadPosition);

        var template = Path.Combine(f.Root, "template.docx");
        using (var document = WordprocessingDocument.Create(template, WordprocessingDocumentType.Document))
        {
            var part = document.AddMainDocumentPart();
            part.Document = new Document(new Body(new Paragraph(new Run(new Text("{HEAD_POSITION}"))),
                new Table(new TableRow(new TableCell(new Paragraph(new Run(new Text("№п/п"))))))));
            part.Document.Save();
        }
        var paths = PassDocumentGenerator.WritePassDocuments(template, Path.Combine(f.Root, "documents"),
            new[] { new DateTime(2026,10,30) },
            new[] { new EmployeeWorkInfo { Employee = new() { Surname = "Тестовый" }, IncludeInPass = true } }, reloaded);
        using var result = WordprocessingDocument.Open(Assert.Single(paths), false);
        Assert.Contains("Генеральный директор", result.MainDocumentPart!.Document.InnerText);
        Assert.DoesNotContain("{HEAD_POSITION}", result.MainDocumentPart.Document.InnerText);
        window.Close();
        GC.KeepAlive(observer);
    }

    [AvaloniaFact]
    public async Task FailedSettingsWriteShowsErrorAndKeepsTheTabOpen()
    {
        using var f = new EmployeeFixture(); f.Seed();
        var blockingFile = Path.Combine(f.Root, "not-a-directory");
        File.WriteAllText(blockingFile, "preserve me");
        var main = new MainWindowViewModel();
        var vm = new SettingsViewModel(main, f.Create, Path.Combine(blockingFile, "settings.json"));
        main.OpenTabRequest(vm, new SettingsControl(), "Настройки");
        vm.HeadPosition = "Директор";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.NotEmpty(vm.SaveError); Assert.False(vm.IsSaving);
        Assert.Single(main.OpenWindows);
        Assert.Equal("preserve me", File.ReadAllText(blockingFile));
    }

    private sealed class CountingObserver : IWorkplaceCatalogObserver
    {
        public int Calls { get; private set; }
        public void RefreshWorkplaceCatalog() => Calls++;
    }
}
