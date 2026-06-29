using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace FanShop.Services;

public class FileDialogService
{
    public static async Task<string?> SaveExcelFileAsync(
        TopLevel topLevel,
        string suggestedFileName)
    {
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                SuggestedFileName = suggestedFileName,
                DefaultExtension = "xlsx",
                FileTypeChoices =
                [
                    new FilePickerFileType("Excel")
                    {
                        Patterns = ["*.xlsx"]
                    }
                ]
            });

        return file?.Path.LocalPath;
    }
}