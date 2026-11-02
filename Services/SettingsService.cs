using System.IO;
using System.Text.Json;
using IslandDesk.Models;

namespace IslandDesk.Services;

public static class SettingsService
{
    public static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IslandDesk");
    public static readonly string FilePath = Path.Combine(Folder, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new AppSettings();
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
            settings.EqualizerBandsDb ??= new double[10];
            if (settings.EqualizerBandsDb.Length != 10)
                settings.EqualizerBandsDb = Enumerable.Range(0, 10)
                    .Select(i => i < settings.EqualizerBandsDb.Length ? settings.EqualizerBandsDb[i] : 0).ToArray();
            settings.ExcludedNotificationApps ??= new();
            settings.HideIslandInApps ??= new();
            settings.ExpandIslandInApps ??= new();
            settings.GamingModeApps ??= new();
            settings.WidgetOrder ??= new() { "cpu", "ram", "gpu", "cpuTemp", "gpuTemp", "fps", "clock", "music" };
            settings.Language = string.Equals(settings.Language, "ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en";
            settings.Theme = string.Equals(settings.Theme, "Light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark";
            settings.AccentColor = string.IsNullOrWhiteSpace(settings.AccentColor) ? "#A78BFA" : settings.AccentColor;
            settings.GlassTintColor = string.IsNullOrWhiteSpace(settings.GlassTintColor) ? (settings.Theme == "Light" ? "#F7F9FD" : "#171A24") : settings.GlassTintColor;
            settings.BrandText = string.IsNullOrWhiteSpace(settings.BrandText) ? "ISLAND DESK" : settings.BrandText;
            settings.GamingWidgetMetric = string.IsNullOrWhiteSpace(settings.GamingWidgetMetric) ? "Gpu" : settings.GamingWidgetMetric;
            settings.EqualizerApoConfigPath = string.IsNullOrWhiteSpace(settings.EqualizerApoConfigPath) ? @"C:\Program Files\EqualizerAPO\config\config.txt" : settings.EqualizerApoConfigPath;
            settings.IslandWidth = Math.Clamp(settings.IslandWidth, 440, 1320);
            settings.Transparency = Math.Clamp(settings.Transparency, 0, 1);
            return settings;
        }
        catch { return new AppSettings(); }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            if (File.Exists(FilePath))
                File.Replace(temp, FilePath, null, true);
            else
                File.Move(temp, FilePath);
        }
        catch
        {
            // Settings are non-critical; the app keeps running if the profile cannot be written.
        }
    }
}
