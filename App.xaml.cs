using System.Windows;
using System.Windows.Media;
using IslandDesk.Models;
using IslandDesk.Services;

namespace IslandDesk;

public partial class App : Application
{
    public static AppSettings Settings { get; private set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        Settings = SettingsService.Load();
        ApplyTheme(Settings.Theme);
        base.OnStartup(e);
    }

    public static void ReplaceSettings(AppSettings settings)
    {
        Settings = settings;
        ApplyTheme(Settings.Theme);
    }

    public static void ApplyTheme(string theme)
    {
        bool dark = !string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase);
        double opacity = Math.Clamp(Settings.Transparency, 0, 1);
        Color tint;
        try { tint = (Color)ColorConverter.ConvertFromString(Settings.GlassTintColor); }
        catch { tint = dark ? Color.FromRgb(23, 26, 36) : Color.FromRgb(247, 249, 253); }

        Color panelAltBase = dark ? Color.FromRgb(39, 44, 58) : Color.FromRgb(235, 240, 246);
        Color surfaceBase = dark ? Color.FromRgb(54, 60, 75) : Color.FromRgb(226, 234, 244);
        Color panelAltTint = Blend(tint, panelAltBase, 0.30);
        Color surfaceTint = Blend(tint, surfaceBase, 0.52);

        SetBrush("PanelBrush", Color.FromArgb((byte)Math.Round(228 * opacity), tint.R, tint.G, tint.B));
        SetBrush("PanelAltBrush", Color.FromArgb((byte)Math.Round(196 * opacity), panelAltTint.R, panelAltTint.G, panelAltTint.B));
        SetBrush("SurfaceBrush", Color.FromArgb((byte)Math.Round(166 * opacity), surfaceTint.R, surfaceTint.G, surfaceTint.B));
        SetBrush("TextBrush", dark ? "#FFF5F6FA" : "#FF1D2430");
        SetBrush("MutedBrush", dark ? "#FFADB5C4" : "#FF687386");
        SetBrush("StrokeBrush", dark ? "#55FFFFFF" : "#3A202A3B");
        SetBrush("GlassHighlightBrush", dark
            ? Color.FromArgb(0x30, 255, 255, 255)
            : Color.FromArgb(0x22, 28, 38, 54));

        try
        {
            var accentColor = (Color)ColorConverter.ConvertFromString(Settings.AccentColor);
            Current.Resources["AccentColorValue"] = accentColor;
            SetBrush("AccentBrush", accentColor);
            accentColor.A = 0x35;
            SetBrush("AccentSoftBrush", accentColor);
        }
        catch
        {
            Current.Resources["AccentColorValue"] = Color.FromRgb(167, 139, 250);
            SetBrush("AccentBrush", "#FFA78BFA");
            SetBrush("AccentSoftBrush", "#35A78BFA");
        }
    }

    private static Color Blend(Color source, Color target, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        byte Mix(byte a, byte b) => (byte)Math.Clamp(Math.Round(a + (b - a) * amount), 0, 255);
        return Color.FromRgb(Mix(source.R, target.R), Mix(source.G, target.G), Mix(source.B, target.B));
    }

    private static void SetBrush(string key, string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        SetBrush(key, color);
    }

    private static void SetBrush(string key, Color color)
    {
        Current.Resources[key] = new SolidColorBrush(color);
    }
}
