using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using IslandDesk.Models;
using IslandDesk.Services;

namespace IslandDeskStudio;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings;
    private readonly Slider[] _eqSliders;
    private bool _ready;

    private static readonly Dictionary<string, string> Russian = new(StringComparer.Ordinal)
    {
        ["Studio"] = "Студия",
        ["Preview your style here, then save to update the island."] = "Настраивай внешний вид здесь, затем сохрани, чтобы обновить островок.",
        ["LANGUAGE"] = "ЯЗЫК",
        ["LIVE PREVIEW"] = "ПРЕДПРОСМОТР",
        ["Appearance"] = "Внешний вид", ["Widgets"] = "Виджеты", ["Gaming & alerts"] = "Игры и уведомления", ["Hotkeys & audio"] = "Клавиши и звук",
        ["Island surface"] = "Внешний вид островка", ["A quiet glass capsule that stays out of your way."] = "Аккуратная стеклянная капсула, которая не мешает.",
        ["THEME"] = "ТЕМА", ["Dark"] = "Тёмная", ["Light"] = "Светлая", ["ACCENT COLOR · #RRGGBB"] = "АКЦЕНТНЫЙ ЦВЕТ · #RRGGBB",
        ["GLASS TINT · #RRGGBB"] = "ЦВЕТ СТЕКЛА · #RRGGBB", ["GLASS OPACITY"] = "ПРОЗРАЧНОСТЬ СТЕКЛА", ["78% · 0% is fully transparent"] = "78% · 0% — полностью прозрачно",
        ["CORNER RADIUS"] = "СКРУГЛЕНИЕ УГЛОВ", ["Layout & motion"] = "Размер и анимация", ["Keep the island compact, smooth and readable."] = "Компактный размер, плавность и читаемость.",
        ["ISLAND WIDTH"] = "ШИРИНА ОСТРОВКА", ["Keep island above normal windows"] = "Поверх обычных окон", ["Enable island animations"] = "Включить анимации островка",
        ["Animate notification entrances"] = "Анимировать появление уведомлений", ["Continuously animate the brand text"] = "Постоянная анимация названия",
        ["ANIMATION DURATION"] = "ДЛИТЕЛЬНОСТЬ АНИМАЦИИ", ["BRAND TEXT"] = "НАДПИСЬ НА ОСТРОВКЕ", ["Launch Island Desk with Windows"] = "Запускать Island Desk вместе с Windows",
        ["Available widgets"] = "Доступные виджеты", ["Choose what appears in the compact island and expanded view."] = "Выбери элементы для компактного и раскрытого островка.",
        ["CPU load"] = "Загрузка CPU", ["Memory"] = "Оперативная память", ["GPU load"] = "Загрузка GPU", ["FPS · RTSS"] = "FPS · RTSS",
        ["CPU temperature"] = "Температура CPU", ["GPU temperature"] = "Температура GPU", ["Clock & date"] = "Время и дата", ["Now playing"] = "Музыка",
        ["Album artwork"] = "Обложка альбома", ["Notifications"] = "Уведомления", ["Emoji shortcut"] = "Панель эмодзи", ["Snipping Tool"] = "Ножницы",
        ["Widget order"] = "Порядок виджетов", ["Select a widget and move it up or down. This order is used in the island header."] = "Выбери виджет и перемести его вверх или вниз. Этот порядок будет на островке.",
        ["↑ Move up"] = "↑ Выше", ["↓ Move down"] = "↓ Ниже", ["Gaming mode"] = "Игровой режим",
        ["A tiny glowing marker, optional single metric, and notification toasts only."] = "Только светящийся индикатор, один показатель по выбору и уведомления.",
        ["Enable gaming mode"] = "Включить игровой режим", ["Enable automatically for fullscreen apps"] = "Включать автоматически в полноэкранных приложениях",
        ["GAMING WIDGET"] = "ВИДЖЕТ В ИГРЕ", ["No widget · glow only"] = "Без виджета · только свечение",
        ["AUTO-ENABLE FOR THESE PROCESSES · EXE NAME"] = "АВТОЗАПУСК ДЛЯ ПРОЦЕССОВ · ИМЯ EXE",
        ["Example: lmu.exe, ac2-win64-shipping.exe. Use borderless windowed mode for the best overlay compatibility."] = "Например: lmu.exe, ac2-win64-shipping.exe. Для лучшей совместимости используй безрамочный режим.",
        ["Notification behavior"] = "Поведение уведомлений", ["Let notifications slide out from the top, then expand only when you ask."] = "Уведомления мягко выезжают сверху и раскрываются по нажатию.",
        ["Hide island in fullscreen apps (overrides gaming mode)"] = "Скрывать островок в полноэкранных приложениях (выше игрового режима)",
        ["MUTE NOTIFICATIONS FROM THESE APPS"] = "НЕ ПОКАЗЫВАТЬ УВЕДОМЛЕНИЯ ЭТИХ ПРИЛОЖЕНИЙ", ["ONE PROCESS NAME PER LINE · OPTIONAL"] = "ОДИН ПРОЦЕСС НА СТРОКУ · НЕОБЯЗАТЕЛЬНО",
        ["Hide island in apps"] = "Скрывать островок в приложениях", ["Auto-expand in apps"] = "Раскрывать островок в приложениях",
        ["Global hotkeys"] = "Глобальные горячие клавиши", ["Use Ctrl, Shift, Alt and a key. Hotkeys work while another app is focused."] = "Используй Ctrl, Shift, Alt и клавишу. Хоткеи работают поверх других приложений.",
        ["TOGGLE ISLAND"] = "ПОКАЗАТЬ / СКРЫТЬ ОСТРОВОК", ["SHOW LATEST NOTIFICATION"] = "ПОСЛЕДНЕЕ УВЕДОМЛЕНИЕ", ["SNIPPING TOOL"] = "НОЖНИЦЫ",
        ["EMOJI PICKER"] = "ПАНЕЛЬ ЭМОДЗИ", ["TOGGLE GAMING MODE"] = "ПЕРЕКЛЮЧИТЬ ИГРОВОЙ РЕЖИМ", ["System equalizer"] = "Системный эквалайзер",
        ["A real 10-band system EQ through Equalizer APO. It affects the selected Windows output device."] = "Настоящий 10-полосный эквалайзер через Equalizer APO. Влияет на выбранное устройство вывода Windows.",
        ["Apply Equalizer APO settings"] = "Применить настройки Equalizer APO", ["EQUALIZER APO CONFIG.TXT"] = "ФАЙЛ CONFIG.TXT EQUALIZER APO", ["Browse"] = "Обзор",
        ["Settings are stored in %AppData%\\IslandDesk"] = "Настройки хранятся в %AppData%\\IslandDesk", ["Launch island"] = "Запустить островок", ["Save settings"] = "Сохранить настройки",
        ["Compact glass, quiet motion and a clear visual hierarchy."] = "Компактное стекло, мягкая анимация и чёткая визуальная иерархия.",
        ["Choose what appears in the island and arrange the order below."] = "Выбери, что показывать на островке, и настрой порядок ниже.",
        ["Select a widget and move it up or down. The order is used in both compact and expanded system views."] = "Выбери виджет и перемести его выше или ниже. Порядок применяется в компактном и раскрытом режиме.",
        ["Equalizer APO config.txt was not found. Install Equalizer APO and choose its config.txt file."] = "Не найден config.txt Equalizer APO. Установи Equalizer APO и укажи путь к config.txt.",
        ["Equalizer settings applied. If audio does not change, check the active output device in Equalizer APO Configurator."] = "Настройки эквалайзера применены. Если звук не изменился, проверь активное устройство вывода в Configurator Equalizer APO.",
        ["Access to the Equalizer APO folder was denied. Run Studio as administrator once or adjust the folder permissions."] = "Нет доступа к папке Equalizer APO. Один раз запусти Studio от имени администратора или измени права папки.",
        ["Equalizer APO is not connected. Island Desk will continue without it."] = "Equalizer APO не подключён. Island Desk продолжит работу без него.",
        ["Island Desk equalizer disabled."] = "Эквалайзер Island Desk отключён.",
        ["Access to config.txt was denied. Permission to the Equalizer APO folder is required."] = "Нет доступа к config.txt. Нужны права на папку Equalizer APO.",
        ["Widget order changed · save settings to apply"] = "Порядок виджетов изменён · сохрани настройки для применения",
        ["Could not update startup"] = "Не удалось изменить автозапуск",
        ["startup disabled"] = "автозапуск отключён",
        ["startup enabled"] = "автозапуск включён",
        ["startup enabled (MSIX)"] = "автозапуск включён (MSIX)",
        ["IslandDesk.exe not found beside Studio"] = "IslandDesk.exe не найден рядом со Studio",
    };

    public MainWindow()
    {
        InitializeComponent();
        _settings = SettingsService.Load();
        _eqSliders = new[] { Eq31, Eq62, Eq125, Eq250, Eq500, Eq1k, Eq2k, Eq4k, Eq8k, Eq16k };
        LoadSettings();
        ApplyStudioTheme();
        RebuildWidgetOrder();
        _ready = true;
        ApplyLanguage();
        UpdatePreview();
    }

    private void ApplyStudioTheme()
    {
        bool dark = !string.Equals(_settings.Theme, "Light", StringComparison.OrdinalIgnoreCase);
        string accent = IsValidColor(_settings.AccentColor) ? _settings.AccentColor : "#A78BFA";
        SetBrush("StudioBg", dark ? "#FF0C0F15" : "#FFF2F4F8");
        SetBrush("StudioPanel", dark ? "#FF171C26" : "#FFFFFFFF");
        SetBrush("StudioControl", dark ? "#FF202735" : "#FFE9EDF4");
        SetBrush("StudioSelected", dark ? "#333A315C" : "#33A78BFA");
        SetBrush("StudioText", dark ? "#FFF4F6FA" : "#FF1D2430");
        SetBrush("StudioMuted", dark ? "#FF98A3B5" : "#FF687386");
        SetBrush("StudioStroke", dark ? "#FF30394A" : "#FFD8DEE8");
        SetBrush("StudioAccent", accent);
    }

    private static void SetBrush(string key, string colorText)
    {
        var color = (Color)ColorConverter.ConvertFromString(colorText);
        Application.Current.Resources[key] = new SolidColorBrush(color);
    }

    private void LoadSettings()
    {
        ThemeCombo.SelectedIndex = string.Equals(_settings.Theme, "Light", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        LanguageCombo.SelectedIndex = string.Equals(_settings.Language, "ru", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        AccentColorBox.Text = _settings.AccentColor;
        GlassTintColorBox.Text = _settings.GlassTintColor;
        TransparencySlider.Value = Math.Clamp(_settings.Transparency, 0, 1);
        RadiusSlider.Value = Math.Clamp(_settings.IslandRadius, 12, 42);
        IslandWidthSlider.Value = Math.Clamp(_settings.IslandWidth, 440, 1320);
        AnimationsCheck.IsChecked = _settings.AnimationsEnabled;
        NotificationAnimationsCheck.IsChecked = _settings.NotificationAnimationsEnabled;
        DurationSlider.Value = Math.Clamp(_settings.AnimationDurationMs, 120, 800);
        BrandTextBox.Text = _settings.BrandText;
        BrandAnimationCheck.IsChecked = _settings.BrandAnimationEnabled;
        AlwaysOnTopCheck.IsChecked = _settings.AlwaysOnTop;
        LaunchOnStartupCheck.IsChecked = _settings.LaunchOnStartup;

        ShowCpuCheck.IsChecked = _settings.ShowCpu;
        ShowRamCheck.IsChecked = _settings.ShowRam;
        ShowGpuCheck.IsChecked = _settings.ShowGpu;
        ShowFpsCheck.IsChecked = _settings.ShowFps;
        ShowCpuTempCheck.IsChecked = _settings.ShowCpuTemperature;
        ShowGpuTempCheck.IsChecked = _settings.ShowGpuTemperature;
        ShowClockCheck.IsChecked = _settings.ShowClock;
        ShowMusicCheck.IsChecked = _settings.ShowMusic;
        ShowAlbumArtCheck.IsChecked = _settings.ShowAlbumArt;
        ShowNotificationsCheck.IsChecked = _settings.ShowNotifications;
        ShowEmojiCheck.IsChecked = _settings.ShowEmojiButton;
        ShowScreenshotCheck.IsChecked = _settings.ShowScreenshotButton;

        ExcludedAppsBox.Text = string.Join(Environment.NewLine, _settings.ExcludedNotificationApps);
        ToggleHotkeyBox.Text = _settings.ToggleHotkey;
        NotificationHotkeyBox.Text = _settings.NotificationHotkey;
        ScreenshotHotkeyBox.Text = _settings.ScreenshotHotkey;
        EmojiHotkeyBox.Text = _settings.EmojiHotkey;
        GameModeHotkeyBox.Text = _settings.GameModeHotkey;

        HideFullscreenCheck.IsChecked = _settings.HideIslandInFullscreenApps;
        GamingModeCheck.IsChecked = _settings.GamingModeEnabled;
        GamingAutoFullscreenCheck.IsChecked = _settings.GamingModeAutoDetectFullscreen;
        GamingModeAppsBox.Text = string.Join(Environment.NewLine, _settings.GamingModeApps);
        HideAppsBox.Text = string.Join(Environment.NewLine, _settings.HideIslandInApps);
        ExpandAppsBox.Text = string.Join(Environment.NewLine, _settings.ExpandIslandInApps);
        SelectComboByTag(GamingWidgetCombo, _settings.GamingWidgetMetric);

        EqualizerEnabledCheck.IsChecked = _settings.EqualizerEnabled;
        EqualizerPathBox.Text = _settings.EqualizerApoConfigPath;
        var bands = _settings.EqualizerBandsDb ?? new double[10];
        for (int i = 0; i < _eqSliders.Length; i++)
            _eqSliders[i].Value = i < bands.Length ? Math.Clamp(bands[i], -12, 12) : 0;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!IsValidColor(AccentColorBox.Text.Trim()) || !IsValidColor(GlassTintColorBox.Text.Trim()))
        {
            MessageBox.Show("Use #RRGGBB for both colors, for example #A78BFA.", "Invalid color",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _settings.Language = (LanguageCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "en";
        string nextTheme = (ThemeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Dark";
        string previousDefaultTint = string.Equals(_settings.Theme, "Light", StringComparison.OrdinalIgnoreCase) ? "#F7F9FD" : "#171A24";
        string nextDefaultTint = string.Equals(nextTheme, "Light", StringComparison.OrdinalIgnoreCase) ? "#F7F9FD" : "#171A24";
        if (string.Equals(GlassTintColorBox.Text.Trim(), previousDefaultTint, StringComparison.OrdinalIgnoreCase))
            GlassTintColorBox.Text = nextDefaultTint;
        _settings.Theme = nextTheme;
        _settings.AccentColor = AccentColorBox.Text.Trim();
        _settings.GlassTintColor = GlassTintColorBox.Text.Trim();
        _settings.Transparency = TransparencySlider.Value;
        _settings.IslandRadius = RadiusSlider.Value;
        _settings.IslandWidth = IslandWidthSlider.Value;
        _settings.AnimationsEnabled = AnimationsCheck.IsChecked == true;
        _settings.NotificationAnimationsEnabled = NotificationAnimationsCheck.IsChecked == true;
        _settings.AnimationDurationMs = (int)DurationSlider.Value;
        _settings.BrandText = string.IsNullOrWhiteSpace(BrandTextBox.Text) ? "ISLAND DESK" : BrandTextBox.Text.Trim();
        _settings.BrandAnimationEnabled = BrandAnimationCheck.IsChecked == true;
        _settings.AlwaysOnTop = AlwaysOnTopCheck.IsChecked == true;
        _settings.LaunchOnStartup = LaunchOnStartupCheck.IsChecked == true;

        _settings.ShowCpu = ShowCpuCheck.IsChecked == true;
        _settings.ShowRam = ShowRamCheck.IsChecked == true;
        _settings.ShowGpu = ShowGpuCheck.IsChecked == true;
        _settings.ShowFps = ShowFpsCheck.IsChecked == true;
        _settings.ShowCpuTemperature = ShowCpuTempCheck.IsChecked == true;
        _settings.ShowGpuTemperature = ShowGpuTempCheck.IsChecked == true;
        _settings.ShowClock = ShowClockCheck.IsChecked == true;
        _settings.ShowMusic = ShowMusicCheck.IsChecked == true;
        _settings.ShowAlbumArt = ShowAlbumArtCheck.IsChecked == true;
        _settings.ShowNotifications = ShowNotificationsCheck.IsChecked == true;
        _settings.ShowEmojiButton = ShowEmojiCheck.IsChecked == true;
        _settings.ShowScreenshotButton = ShowScreenshotCheck.IsChecked == true;

        _settings.ExcludedNotificationApps = ParseLines(ExcludedAppsBox.Text);
        _settings.ToggleHotkey = NormalizeHotkey(ToggleHotkeyBox.Text, "Ctrl+Shift+Space");
        _settings.NotificationHotkey = NormalizeHotkey(NotificationHotkeyBox.Text, "Ctrl+Shift+N");
        _settings.ScreenshotHotkey = NormalizeHotkey(ScreenshotHotkeyBox.Text, "Ctrl+Shift+S");
        _settings.EmojiHotkey = NormalizeHotkey(EmojiHotkeyBox.Text, "Ctrl+Shift+E");
        _settings.GameModeHotkey = NormalizeHotkey(GameModeHotkeyBox.Text, "Ctrl+Shift+G");

        _settings.HideIslandInFullscreenApps = HideFullscreenCheck.IsChecked == true;
        _settings.GamingModeEnabled = GamingModeCheck.IsChecked == true;
        _settings.GamingModeAutoDetectFullscreen = GamingAutoFullscreenCheck.IsChecked == true;
        _settings.GamingModeApps = ParseLines(GamingModeAppsBox.Text);
        _settings.GamingWidgetMetric = (GamingWidgetCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Gpu";
        _settings.HideIslandInApps = ParseLines(HideAppsBox.Text);
        _settings.ExpandIslandInApps = ParseLines(ExpandAppsBox.Text);

        _settings.EqualizerEnabled = EqualizerEnabledCheck.IsChecked == true;
        _settings.EqualizerApoConfigPath = EqualizerPathBox.Text.Trim();
        _settings.EqualizerBandsDb = _eqSliders.Select(x => Math.Round(x.Value, 1)).ToArray();
        _settings.WidgetOrder = WidgetOrderList.Items.OfType<ListBoxItem>()
            .Select(x => x.Tag?.ToString() ?? "").Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

        SettingsService.Save(_settings);
        ApplyStudioTheme();
        UpdatePreview();
        SaveStatus.Text = "Saved · the island will reload settings automatically";

        string startupStatus = ApplyStartupSetting(_settings.LaunchOnStartup);
        if (!string.IsNullOrWhiteSpace(startupStatus)) SaveStatus.Text += " · " + startupStatus;

        if (_settings.EqualizerEnabled)
        {
            var result = EqualizerApoService.Apply(_settings);
            EqualizerStatus.Text = Translate(result.Message, string.Equals(_settings.Language, "ru", StringComparison.OrdinalIgnoreCase));
            if (!result.Success)
                MessageBox.Show(EqualizerStatus.Text, "Equalizer APO", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            var result = EqualizerApoService.Disable(_settings.EqualizerApoConfigPath);
            EqualizerStatus.Text = Translate(result.Message, string.Equals(_settings.Language, "ru", StringComparison.OrdinalIgnoreCase));
        }
        ApplyLanguage();
    }

    private void RebuildWidgetOrder()
    {
        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["cpu"] = "CPU load", ["ram"] = "Memory", ["gpu"] = "GPU load",
            ["cpuTemp"] = "CPU temperature", ["gpuTemp"] = "GPU temperature", ["fps"] = "FPS · RTSS",
            ["clock"] = "Clock & date", ["music"] = "Now playing"
        };
        WidgetOrderList.Items.Clear();
        var order = _settings.WidgetOrder ?? new List<string>();
        foreach (string id in order)
            if (labels.TryGetValue(id, out string? label))
                WidgetOrderList.Items.Add(new ListBoxItem { Content = label, Tag = id, Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(2) });
        foreach (var pair in labels)
            if (!order.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
                WidgetOrderList.Items.Add(new ListBoxItem { Content = pair.Value, Tag = pair.Key, Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(2) });
        if (WidgetOrderList.Items.Count > 0) WidgetOrderList.SelectedIndex = 0;
        ApplyLanguage();
    }

    private void WidgetUp_Click(object sender, RoutedEventArgs e) => MoveWidget(-1);
    private void WidgetDown_Click(object sender, RoutedEventArgs e) => MoveWidget(1);

    private void MoveWidget(int offset)
    {
        int index = WidgetOrderList.SelectedIndex;
        int destination = index + offset;
        if (index < 0 || destination < 0 || destination >= WidgetOrderList.Items.Count) return;
        object item = WidgetOrderList.Items[index];
        WidgetOrderList.Items.RemoveAt(index);
        WidgetOrderList.Items.Insert(destination, item);
        WidgetOrderList.SelectedIndex = destination;
        SaveStatus.Text = "Widget order changed · save settings to apply";
        ApplyLanguage();
    }

    private void PreviewTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_ready) UpdatePreview();
    }

    private void PreviewChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_ready) UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (IslandPreview is null) return;
        IslandWidthValue.Text = $"{IslandWidthSlider.Value:0} px";
        PreviewSizeLabel.Text = $"{IslandWidthSlider.Value:0} px";
        TransparencyValue.Text = string.Equals(_settings.Language, "ru", StringComparison.OrdinalIgnoreCase)
            ? $"{TransparencySlider.Value * 100:0}% · 0% — полностью прозрачно"
            : $"{TransparencySlider.Value * 100:0}% · 0% is fully transparent";
        IslandPreview.Width = Math.Clamp(IslandWidthSlider.Value * 0.43, 230, 560);
        IslandPreview.CornerRadius = new CornerRadius(RadiusSlider.Value * 0.72);
        try
        {
            Color tint = (Color)ColorConverter.ConvertFromString(GlassTintColorBox.Text);
            tint.A = (byte)Math.Round(228 * TransparencySlider.Value);
            IslandPreview.Background = new SolidColorBrush(tint);
            IslandPreview.BorderBrush = new SolidColorBrush(string.Equals(_settings.Theme, "Light", StringComparison.OrdinalIgnoreCase)
                ? Color.FromArgb(0x38, 28, 38, 54) : Color.FromArgb(0x55, 255, 255, 255));
            var accent = (Color)ColorConverter.ConvertFromString(AccentColorBox.Text);
            PreviewBrand.Foreground = new SolidColorBrush(accent);
            PreviewBrand.Text = string.IsNullOrWhiteSpace(BrandTextBox.Text) ? "ISLAND DESK" : BrandTextBox.Text.Trim();
        }
        catch { }
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        _settings.Language = (LanguageCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "en";
        ApplyLanguage();
        SettingsService.Save(_settings);
        SaveStatus.Text = _settings.Language == "ru" ? "Язык сохранён" : "Language saved";
    }

    private void ApplyLanguage()
    {
        bool russian = string.Equals(_settings.Language, "ru", StringComparison.OrdinalIgnoreCase);
        TranslateTree(this, russian);
        PreviewBrand.Text = string.IsNullOrWhiteSpace(BrandTextBox.Text) ? "ISLAND DESK" : BrandTextBox.Text;
        if (russian && SaveStatus.Text.StartsWith("Saved", StringComparison.OrdinalIgnoreCase))
            SaveStatus.Text = "Сохранено · островок обновит настройки автоматически";
        else if (!russian && SaveStatus.Text.StartsWith("Сохранено", StringComparison.OrdinalIgnoreCase))
            SaveStatus.Text = "Saved · the island will reload settings automatically";
    }

    private static void TranslateTree(DependencyObject root, bool russian)
    {
        if (root is TextBlock textBlock) textBlock.Text = Translate(textBlock.Text, russian);
        else if (root is Button button && button.Content is string buttonText) button.Content = Translate(buttonText, russian);
        else if (root is CheckBox checkBox && checkBox.Content is string checkText) checkBox.Content = Translate(checkText, russian);
        else if (root is TabItem tab && tab.Header is string tabText) tab.Header = Translate(tabText, russian);
        else if (root is ComboBoxItem combo && combo.Content is string comboText) combo.Content = Translate(comboText, russian);
        else if (root is ListBoxItem listItem && listItem.Content is string listText) listItem.Content = Translate(listText, russian);

        foreach (object child in LogicalTreeHelper.GetChildren(root))
            if (child is DependencyObject dependencyChild)
                TranslateTree(dependencyChild, russian);

        if (root is ComboBox comboBox)
            foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
                TranslateTree(item, russian);
    }

    private static string Translate(string value, bool russian)
    {
        if (russian) return Russian.TryGetValue(value, out string? translated) ? translated : value;
        foreach (var pair in Russian)
            if (string.Equals(pair.Value, value, StringComparison.Ordinal)) return pair.Key;
        return value;
    }

    private static void SelectComboByTag(ComboBox combo, string tag)
    {
        foreach (var item in combo.Items.OfType<ComboBoxItem>())
            if (string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                return;
            }
        combo.SelectedIndex = 0;
    }

    private static List<string> ParseLines(string text) => text
        .Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    private static string NormalizeHotkey(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static bool IsValidColor(string value)
    {
        try
        {
            _ = (Color)ColorConverter.ConvertFromString(value);
            return value.StartsWith('#') && (value.Length == 7 || value.Length == 9);
        }
        catch { return false; }
    }

    private static string ApplyStartupSetting(bool enabled)
    {
        const string runPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string appName = "IslandDesk";
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(runPath, writable: true) ??
                            Registry.CurrentUser.CreateSubKey(runPath, writable: true);
            if (key is null) return "Could not update startup";
            if (!enabled)
            {
                key.DeleteValue(appName, throwOnMissingValue: false);
                return "startup disabled";
            }
            try
            {
                string familyName = Windows.ApplicationModel.Package.Current.Id.FamilyName;
                key.SetValue(appName, $"explorer.exe shell:AppsFolder\\{familyName}!IslandDesk");
                return "startup enabled (MSIX)";
            }
            catch
            {
                string exe = Path.Combine(AppContext.BaseDirectory, "IslandDesk.exe");
                if (!File.Exists(exe)) return "IslandDesk.exe not found beside Studio";
                key.SetValue(appName, $"\"{exe}\"");
                return "startup enabled";
            }
        }
        catch (Exception ex) { return "Startup: " + ex.Message; }
    }

    private void BrowseEqualizer_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Equalizer APO config.txt",
            Filter = "Config file (config.txt)|config.txt|All files (*.*)|*.*",
            CheckFileExists = true,
            InitialDirectory = Path.GetDirectoryName(EqualizerPathBox.Text) is { } dir && Directory.Exists(dir)
                ? dir : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
        };
        if (dialog.ShowDialog(this) == true) EqualizerPathBox.Text = dialog.FileName;
    }

    private void LaunchIsland_Click(object sender, RoutedEventArgs e)
    {
        string exe = Path.Combine(AppContext.BaseDirectory, "IslandDesk.exe");
        if (!File.Exists(exe))
        {
            MessageBox.Show("IslandDesk.exe was not found. Build both projects with BuildAll.ps1 or install the MSIX package.",
                "Island Desk", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try { Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Could not launch island"); }
    }
}
