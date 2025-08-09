using System.IO;
using System.Text.Json;

namespace FanShop.Models;

public class Settings
{
    public string Head { get; set; } = "";
    public string ResponsiblePerson { get; set; } = "";
    public string ResponsiblePhoneNumber { get; set; } = "";
    public string ResponsiblePosition { get; set; } = "Управляющий магазином Фаншоп";
    public string VisitGoal { get; set; } = "";
    public decimal DailySalary { get; set; } = 0;
    
    private static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FanShop", "settings.json");
    
    public static Settings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
            }
        }
        catch { }
        return new Settings();
    }
    
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }
}