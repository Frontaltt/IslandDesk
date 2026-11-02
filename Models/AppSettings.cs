namespace IslandDesk.Models;

public sealed class AppSettings
{
    // English is the default UI language; Studio can switch to Russian.
    public string Language { get; set; } = "en";

    public string Theme { get; set; } = "Dark";
    public string AccentColor { get; set; } = "#A78BFA";
    public string GlassTintColor { get; set; } = "#171A24";
    // 0 = fully transparent surface, 1 = opaque surface.
    public double Transparency { get; set; } = 0.78;
    public double IslandRadius { get; set; } = 28;
    public double IslandWidth { get; set; } = 760;
    public bool AnimationsEnabled { get; set; } = true;
    public bool NotificationAnimationsEnabled { get; set; } = true;
    public int AnimationDurationMs { get; set; } = 300;
    public bool AlwaysOnTop { get; set; } = true;

    public string BrandText { get; set; } = "ISLAND DESK";
    public bool BrandAnimationEnabled { get; set; } = false;

    public bool ShowCpu { get; set; } = true;
    public bool ShowRam { get; set; } = true;
    public bool ShowGpu { get; set; } = true;
    public bool ShowFps { get; set; } = true;
    public bool ShowCpuTemperature { get; set; } = true;
    public bool ShowGpuTemperature { get; set; } = true;
    public bool ShowClock { get; set; } = true;
    public bool ShowMusic { get; set; } = true;
    public bool ShowAlbumArt { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;
    public bool ShowEmojiButton { get; set; } = true;
    public bool ShowScreenshotButton { get; set; } = true;
    public bool StartExpanded { get; set; } = false;

    // Order of the compact widgets in the island header.
    public List<string> WidgetOrder { get; set; } = new()
    {
        "cpu", "ram", "gpu", "cpuTemp", "gpuTemp", "fps", "clock", "music"
    };

    public string ToggleHotkey { get; set; } = "Ctrl+Shift+Space";
    public string NotificationHotkey { get; set; } = "Ctrl+Shift+N";
    public string ScreenshotHotkey { get; set; } = "Ctrl+Shift+S";
    public string EmojiHotkey { get; set; } = "Ctrl+Shift+E";
    public string GameModeHotkey { get; set; } = "Ctrl+Shift+G";

    public List<string> ExcludedNotificationApps { get; set; } = new();
    public List<string> HideIslandInApps { get; set; } = new();
    public List<string> ExpandIslandInApps { get; set; } = new();
    public bool HideIslandInFullscreenApps { get; set; } = false;

    public bool GamingModeEnabled { get; set; } = false;
    public bool GamingModeAutoDetectFullscreen { get; set; } = false;
    public List<string> GamingModeApps { get; set; } = new();
    // None, Cpu, Gpu, CpuTemperature, GpuTemperature.
    public string GamingWidgetMetric { get; set; } = "Gpu";

    public bool LaunchOnStartup { get; set; } = false;

    public bool EqualizerEnabled { get; set; } = false;
    public string EqualizerApoConfigPath { get; set; } = @"C:\Program Files\EqualizerAPO\config\config.txt";
    public double[] EqualizerBandsDb { get; set; } = new double[10];
}
