using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Diagnostics;

namespace FanShop.Services;

public static class PassTemplateService
{
    public static string TemplateDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FanShop");

    public static string TemplatePath => Path.Combine(TemplateDirectory, "болванка.docx");

    public static async Task<bool> EnsureTemplateAsync(Window? owner)
    {
        if (File.Exists(TemplatePath))
        {
            return true;
        }

        return await PickAndInstallTemplateAsync(owner);
    }

    public static async Task<bool> OpenTemplateAsync(Window? owner)
    {
        if (!await EnsureTemplateAsync(owner))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(TemplatePath)
            {
                UseShellExecute = true
            });

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> PickAndInstallTemplateAsync(Window? owner)
    {
        try
        {
            var topLevel = owner ?? TopLevel.GetTopLevel(owner);
            if (topLevel?.StorageProvider == null)
            {
                return false;
            }

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Выберите шаблон пропуска (.docx)",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Документ Word")
                    {
                        Patterns = ["*.docx"],
                        AppleUniformTypeIdentifiers = ["org.openxmlformats.wordprocessingml.document"],
                        MimeTypes = ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"]
                    }
                ]
            });

            var selectedFile = files.FirstOrDefault();
            if (selectedFile == null)
            {
                return false;
            }

            Directory.CreateDirectory(TemplateDirectory);

            await using var sourceStream = await selectedFile.OpenReadAsync();
            await using var targetStream = File.Create(TemplatePath);
            await sourceStream.CopyToAsync(targetStream);

            return true;
        }
        catch
        {
            return false;
        }
    }
}
