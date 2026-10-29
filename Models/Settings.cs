using System.IO;
using System.Text.Json;

namespace FanShop.Models;

public class Settings
{
    public string Head { get; set; } = "";
    public string HeadPosition { get; set; } = "";
    public string ResponsiblePerson { get; set; } = "";
    public string ResponsiblePhoneNumber { get; set; } = "";
    public string ResponsiblePosition { get; set; } = "Управляющий магазином Фаншоп";
    public string VisitGoal { get; set; } = "";
    public string LastSeenWhatsNewVersion { get; set; } = "";
    
    private static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FanShop", "settings.json");
    
    public static Settings Load(string? path = null)
    {
        path ??= SettingsPath;
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
            }
        }
        catch { }
        return new Settings();
    }
    
    public void Save() => TrySave(out _);

    public bool TrySave(out string? error, string? path = null)
    {
        error = null;
        path = Path.GetFullPath(path ?? SettingsPath);
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(temporary, json);
            File.Move(temporary, path, true);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}
