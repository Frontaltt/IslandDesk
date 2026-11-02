using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Globalization;
using IslandDesk.Models;
using IslandDesk.Services;

namespace IslandDesk;

public partial class MainWindow : Window
{
    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const int WmMouseActivate = 0x0021;
    private const int MaNoActivate = 3;

    private HwndSource? _windowSource;
    private readonly SystemMetricsService _metricsService = new();
    private readonly HardwareMonitorService _hardwareService = new();
    private readonly MediaSessionService _mediaService = new();
    private readonly NotificationService _notificationService = new();
    private readonly HotkeyService _hotkeys = new();
    private readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _peekTimer = new() { Interval = TimeSpan.FromSeconds(7) };

    private bool _expanded;
    private bool _settingsOpen;
    private bool _peeking;
    private bool _showNotificationDetails;
    private bool _metricsBusy;
    private bool _mediaBusy;
    private bool _hardwareBusy;
    private bool _loadingControls;
    private bool _hiddenByAutomation;
    private bool _autoExpanded;
    private bool _gamingModeActive;
    private bool _gameNotificationVisible;
    private bool _gameNotificationExpanded;
    private int _tick;
    private int _brandHoverVersion;
    private int _musicHoverVersion;
    private DateTime _lastSettingsWriteUtc;
    private IslandNotification? _lastNotification;

    public MainWindow()
    {
        InitializeComponent();
        Width = Math.Clamp(App.Settings.IslandWidth, 440, 1320);
        Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
        Top = 0;

        LoadSettingsIntoControls();
        App.ApplyTheme(App.Settings.Theme);
        ApplyVisibilitySettings();
        ApplyAppearanceSettings();
        RebuildWidgetOrder();
        UpdateEqualizerPreview();
        _lastSettingsWriteUtc = GetSettingsWriteTime();

        _updateTimer.Tick += UpdateTimer_Tick;
        _peekTimer.Tick += (_, _) =>
        {
            _peekTimer.Stop();
            if (_gamingModeActive)
            {
                if (!_gameNotificationExpanded)
                {
                    _gameNotificationVisible = false;
                    UpdateLayoutState();
                }
            }
            else if (!_expanded)
            {
                _peeking = false;
                UpdateLayoutState();
            }
        };

        _hotkeys.ToggleIsland += () => Dispatcher.Invoke(ToggleExpanded);
        _hotkeys.PeekNotification += () => Dispatcher.Invoke(ShowLastNotification);
        _hotkeys.Screenshot += () => Dispatcher.Invoke(HotkeyService.LaunchSnippingTool);
        _hotkeys.EmojiPicker += () => Dispatcher.Invoke(HotkeyService.LaunchEmojiPicker);
        _hotkeys.ToggleGameMode += () => Dispatcher.Invoke(ToggleGamingMode);

        _notificationService.NotificationReceived += notification => Dispatcher.Invoke(() => ShowNotification(notification));
        _notificationService.StatusChanged += status => Dispatcher.Invoke(() => NotificationStatus.Text = LocalizeNotificationStatus(status));

        Loaded += MainWindow_Loaded;
        SourceInitialized += MainWindow_SourceInitialized;
        Closing += MainWindow_Closing;

        _expanded = App.Settings.StartExpanded;
        _gamingModeActive = App.Settings.GamingModeEnabled;
        UpdateClock();
        UpdateLayoutState();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _updateTimer.Start();
        ApplyBrandAnimation();
        EnsureTopmost();
        await RefreshMediaAsync();
        if (App.Settings.ShowNotifications)
            await _notificationService.StartAsync();
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _windowSource = HwndSource.FromHwnd(handle);
        _windowSource?.AddHook(WindowMessageHook);
        _hotkeys.Attach(handle, App.Settings);
        EnsureTopmost();
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _updateTimer.Stop();
        _peekTimer.Stop();
        _notificationService.Stop();
        _hotkeys.Dispose();
        _windowSource?.RemoveHook(WindowMessageHook);
        _windowSource = null;
        _hardwareService.Dispose();
        SettingsService.Save(App.Settings);
    }

    private async void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        _tick++;
        ReloadSettingsChangedByStudio();
        ApplyForegroundAutomation();
        if (_gamingModeActive) PositionGamingIslandOnGameMonitor();
        EnsureTopmost();

        if (!_metricsBusy)
        {
            _metricsBusy = true;
            try
            {
                var metrics = await _metricsService.ReadAsync();
                CpuTiny.Text = CpuLarge.Text = $"{metrics.CpuPercent:0}%";
                RamTiny.Text = RamLarge.Text = $"{metrics.RamPercent:0}%";
                RamDetail.Text = $"{metrics.UsedRamGb:0.0} / {metrics.TotalRamGb:0.0} {LanguageText("GB", "ГБ")}";
                string gpuText = metrics.GpuPercent.HasValue ? $"{metrics.GpuPercent.Value:0}%" : "—";
                GpuTiny.Text = GpuLarge.Text = gpuText;
                string fpsText = metrics.Fps.HasValue ? $"{metrics.Fps.Value:0}" : "—";
                FpsTiny.Text = FpsLarge.Text = fpsText;
            }
            catch (Exception ex) { Debug.WriteLine(ex); }
            finally { _metricsBusy = false; }
        }

        if (!_hardwareBusy && _tick % 2 == 0)
        {
            _hardwareBusy = true;
            try
            {
                var temps = await _hardwareService.ReadAsync();
                string cpu = temps.CpuCelsius.HasValue ? $"{temps.CpuCelsius.Value:0}°C" : "—°C";
                string gpu = temps.GpuCelsius.HasValue ? $"{temps.GpuCelsius.Value:0}°C" : "—°C";
                CpuTempTiny.Text = CpuTempLarge.Text = cpu;
                GpuTempTiny.Text = GpuTempLarge.Text = gpu;
                CpuTempChip.ToolTip = CpuTempCard.ToolTip = temps.CpuCelsius.HasValue
                    ? $"{LanguageText("CPU sensor", "Датчик CPU")}: {temps.CpuSensor}"
                    : LanguageText("CPU temperature sensor unavailable. Check LibreHardwareMonitor and try running Island Desk as administrator.", "Датчик температуры CPU недоступен. Проверь LibreHardwareMonitor и попробуй запустить Island Desk от имени администратора.");
                GpuTempChip.ToolTip = GpuTempCard.ToolTip = temps.GpuCelsius.HasValue
                    ? $"{LanguageText("GPU sensor", "Датчик GPU")}: {temps.GpuSensor}"
                    : LanguageText("GPU temperature sensor unavailable. Check GPU driver and LibreHardwareMonitor support.", "Датчик температуры GPU недоступен. Проверь драйвер видеокарты и поддержку LibreHardwareMonitor.");
            }
            catch (Exception ex) { Debug.WriteLine(ex); }
            finally { _hardwareBusy = false; }
        }

        UpdateClock();
        UpdateGamingWidgetValues();
        if (_tick % 3 == 0)
            await RefreshMediaAsync();
    }

    private async Task RefreshMediaAsync()
    {
        if (_mediaBusy) return;
        _mediaBusy = true;
        try
        {
            var media = await _mediaService.ReadAsync();
            if (media is null)
            {
                TrackTitle.Text = MediaEmptyTitle();
                TrackArtist.Text = MediaEmptySubtitle();
                MediaLargeTitle.Text = MediaEmptyTitle();
                MediaLargeArtist.Text = LanguageText("Start Spotify or another player", "Запусти Spotify или другой плеер");
                MediaAppName.Text = LanguageText("WINDOWS MEDIA SESSION", "МЕДИАСЕССИЯ WINDOWS");
                HeaderPlayButton.Content = MediaPlayButton.Content = "▶";
                HeaderAlbumArt.Source = MediaAlbumArt.Source = null;
                HeaderAlbumArt.Visibility = MediaAlbumArt.Visibility = Visibility.Collapsed;
                return;
            }

            string mediaTitle = media.Title == "Unknown track" ? LanguageText("Unknown track", "Неизвестный трек") : media.Title;
            string mediaApp = media.AppName == "Media player" ? MediaEmptySubtitle() : media.AppName;
            string mediaArtist = media.Artist == "Media player" ? mediaApp : media.Artist;
            TrackTitle.Text = mediaTitle;
            TrackArtist.Text = $"{mediaArtist} · {mediaApp}";
            MediaLargeTitle.Text = mediaTitle;
            MediaLargeArtist.Text = mediaArtist;
            MediaAppName.Text = mediaApp.ToUpperInvariant();
            HeaderPlayButton.Content = MediaPlayButton.Content = media.IsPlaying ? "Ⅱ" : "▶";

            try
            {
                if (App.Settings.ShowAlbumArt && media.ThumbnailBytes is { Length: > 0 })
                {
                    var bitmap = DecodeImage(media.ThumbnailBytes);
                    HeaderAlbumArt.Source = MediaAlbumArt.Source = bitmap;
                    HeaderAlbumArt.Visibility = MediaAlbumArt.Visibility = Visibility.Visible;
                }
                else
                {
                    HeaderAlbumArt.Source = MediaAlbumArt.Source = null;
                    HeaderAlbumArt.Visibility = MediaAlbumArt.Visibility = Visibility.Collapsed;
                }
            }
            catch
            {
                HeaderAlbumArt.Source = MediaAlbumArt.Source = null;
                HeaderAlbumArt.Visibility = MediaAlbumArt.Visibility = Visibility.Collapsed;
            }
        }
        finally { _mediaBusy = false; }
    }

    private static BitmapImage DecodeImage(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.DecodePixelWidth = 256;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private void LoadSettingsIntoControls()
    {
        _loadingControls = true;
        ShowCpuCheck.IsChecked = App.Settings.ShowCpu;
        ShowRamCheck.IsChecked = App.Settings.ShowRam;
        ShowGpuCheck.IsChecked = App.Settings.ShowGpu;
        ShowFpsCheck.IsChecked = App.Settings.ShowFps;
        ShowMusicCheck.IsChecked = App.Settings.ShowMusic;
        ShowNotificationsCheck.IsChecked = App.Settings.ShowNotifications;
        _loadingControls = false;
    }

    private void SaveSettingsFromControls()
    {
        App.Settings.ShowCpu = ShowCpuCheck.IsChecked == true;
        App.Settings.ShowRam = ShowRamCheck.IsChecked == true;
        App.Settings.ShowGpu = ShowGpuCheck.IsChecked == true;
        App.Settings.ShowFps = ShowFpsCheck.IsChecked == true;
        App.Settings.ShowMusic = ShowMusicCheck.IsChecked == true;
        App.Settings.ShowNotifications = ShowNotificationsCheck.IsChecked == true;
        SettingsService.Save(App.Settings);
        _lastSettingsWriteUtc = GetSettingsWriteTime();
    }

    private void ApplyVisibilitySettings()
    {
        CpuChip.Visibility = CpuCard.Visibility = App.Settings.ShowCpu ? Visibility.Visible : Visibility.Collapsed;
        RamChip.Visibility = RamCard.Visibility = App.Settings.ShowRam ? Visibility.Visible : Visibility.Collapsed;
        GpuChip.Visibility = GpuCard.Visibility = App.Settings.ShowGpu ? Visibility.Visible : Visibility.Collapsed;
        FpsChip.Visibility = FpsCard.Visibility = App.Settings.ShowFps ? Visibility.Visible : Visibility.Collapsed;
        CpuTempChip.Visibility = CpuTempCard.Visibility = App.Settings.ShowCpuTemperature ? Visibility.Visible : Visibility.Collapsed;
        GpuTempChip.Visibility = GpuTempCard.Visibility = App.Settings.ShowGpuTemperature ? Visibility.Visible : Visibility.Collapsed;
        ClockChip.Visibility = ClockCard.Visibility = App.Settings.ShowClock ? Visibility.Visible : Visibility.Collapsed;
        HeaderMusic.Visibility = App.Settings.ShowMusic ? Visibility.Visible : Visibility.Collapsed;
        MediaHeading.Visibility = MediaCard.Visibility = App.Settings.ShowMusic ? Visibility.Visible : Visibility.Collapsed;
        NotificationSectionTitle.Visibility = NotificationPanel.Visibility = App.Settings.ShowNotifications ? Visibility.Visible : Visibility.Collapsed;
        ScreenshotButton.Visibility = App.Settings.ShowScreenshotButton ? Visibility.Visible : Visibility.Collapsed;
        EmojiButton.Visibility = App.Settings.ShowEmojiButton ? Visibility.Visible : Visibility.Collapsed;
        if (!App.Settings.ShowNotifications) NotificationStatus.Text = LanguageText("Disabled in settings", "Отключено в настройках");
    }

    private void ApplyAppearanceSettings()
    {
        IslandCard.CornerRadius = new CornerRadius(Math.Clamp(App.Settings.IslandRadius, 12, 40));
        GlassHighlightOverlay.CornerRadius = IslandCard.CornerRadius;
        BrandTextBlock.Text = string.IsNullOrWhiteSpace(App.Settings.BrandText) ? "ISLAND DESK" : App.Settings.BrandText.Trim();
        Width = Math.Clamp(App.Settings.IslandWidth, 440, 1320);
        Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
        Top = 0;
        App.ApplyTheme(App.Settings.Theme);
        ApplyBrandAnimation();
        ApplyLocalization();
    }

    private void ApplyBrandAnimation()
    {
        BrandTextBlock.BeginAnimation(UIElement.OpacityProperty, null);
        BrandTextBlock.Opacity = 1;
        if (App.Settings.BrandAnimationEnabled && App.Settings.AnimationsEnabled && IsLoaded)
        {
            BrandTextBlock.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.62, 1.0,
                new Duration(TimeSpan.FromSeconds(1.7)))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            });
        }

        GamingGlowDot.BeginAnimation(UIElement.OpacityProperty, null);
        GamingGlowDot.Opacity = 1;
        if (App.Settings.AnimationsEnabled && IsLoaded)
        {
            GamingGlowDot.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.35, 1.0,
                new Duration(TimeSpan.FromSeconds(1.15)))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            });
        }
    }

    private void RebuildWidgetOrder()
    {
        var widgets = new Dictionary<string, UIElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["cpu"] = CpuChip,
            ["ram"] = RamChip,
            ["gpu"] = GpuChip,
            ["cpuTemp"] = CpuTempChip,
            ["gpuTemp"] = GpuTempChip,
            ["fps"] = FpsChip,
            ["clock"] = ClockChip,
            ["music"] = HeaderMusic
        };

        HeaderWidgetPanel.Children.Clear();
        var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string id in App.Settings.WidgetOrder ?? new List<string>())
        {
            if (widgets.TryGetValue(id, out var widget) && added.Add(id))
                HeaderWidgetPanel.Children.Add(widget);
        }
        foreach (var pair in widgets)
            if (added.Add(pair.Key)) HeaderWidgetPanel.Children.Add(pair.Value);

        var cards = new Dictionary<string, UIElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["cpu"] = CpuCard, ["ram"] = RamCard, ["gpu"] = GpuCard,
            ["cpuTemp"] = CpuTempCard, ["gpuTemp"] = GpuTempCard,
            ["fps"] = FpsCard, ["clock"] = ClockCard
        };
        SystemCardsPanel.Children.Clear();
        var cardAdded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string id in App.Settings.WidgetOrder ?? new List<string>())
            if (cards.TryGetValue(id, out var card) && cardAdded.Add(id))
                SystemCardsPanel.Children.Add(card);
        foreach (var pair in cards)
            if (cardAdded.Add(pair.Key)) SystemCardsPanel.Children.Add(pair.Value);
    }

    private static string LanguageText(string english, string russian) =>
        string.Equals(App.Settings.Language, "ru", StringComparison.OrdinalIgnoreCase) ? russian : english;

    private string MediaEmptyTitle() => LanguageText("Nothing playing", "Ничего не играет");
    private string MediaEmptySubtitle() => LanguageText("Media session", "Медиаплеер");

    private static readonly Dictionary<string, string> MainRussian = new(StringComparer.Ordinal)
    {
        ["SYSTEM"] = "СИСТЕМА", ["PROCESSOR"] = "ПРОЦЕССОР", ["Overall usage"] = "Общая загрузка",
        ["MEMORY"] = "ОПЕРАТИВНАЯ ПАМЯТЬ", ["GRAPHICS"] = "ВИДЕОКАРТА", ["Windows 3D engine"] = "Движок 3D Windows",
        ["CPU TEMPERATURE"] = "ТЕМПЕРАТУРА CPU", ["CPU sensor"] = "Датчик процессора",
        ["GPU TEMPERATURE"] = "ТЕМПЕРАТУРА GPU", ["GPU sensor"] = "Датчик видеокарты",
        ["FRAME RATE"] = "ЧАСТОТА КАДРОВ", ["RTSS must be running"] = "RTSS должен быть запущен",
        ["LOCAL TIME"] = "МЕСТНОЕ ВРЕМЯ", ["Today"] = "Сегодня", ["MEDIA"] = "МЕДИА",
        ["LATEST NOTIFICATION"] = "ПОСЛЕДНЕЕ УВЕДОМЛЕНИЕ", ["Waiting for Windows notifications"] = "Ожидание уведомлений Windows",
        ["Windows notifications will appear here"] = "Здесь появятся сообщения из Windows", ["Connecting…"] = "Подключение…",
        ["WIDGET VISIBILITY"] = "ВИДИМОСТЬ ВИДЖЕТОВ", ["CPU usage"] = "Загрузка процессора",
        ["RAM usage"] = "Использование RAM", ["GPU usage"] = "Загрузка видеокарты", ["FPS (via RTSS)"] = "FPS (через RTSS)",
        ["Clock & date"] = "Время и дата", ["Music player"] = "Музыкальный плеер", ["Windows notifications"] = "Уведомления Windows",
        ["APPEARANCE"] = "ВНЕШНИЙ ВИД", ["Dark theme"] = "Тёмная тема", ["Light theme"] = "Светлая тема",
        ["HOTKEYS"] = "ГОРЯЧИЕ КЛАВИШИ", ["Expand / collapse island"] = "Развернуть / свернуть островок",
        ["Open latest notification"] = "Открыть последнее уведомление", ["Settings are saved automatically."] = "Настройки сохраняются автоматически.",
        ["Nothing playing"] = "Ничего не играет", ["Media session"] = "Медиаплеер", ["Media player"] = "Медиаплеер", ["Unknown track"] = "Неизвестный трек", ["Start Spotify or another player"] = "Запусти Spotify или другой плеер",
        ["WINDOWS MEDIA SESSION"] = "МЕДИАСЕССИЯ WINDOWS", ["New notification"] = "Новое уведомление", ["Notification title"] = "Заголовок уведомления",
        ["Message preview"] = "Текст сообщения", ["Details"] = "Подробнее", ["APPLICATION"] = "ПРИЛОЖЕНИЕ",
        ["Notification"] = "Уведомление", ["No additional text."] = "Нет дополнительного текста.", ["Close"] = "Закрыть",
        ["Capture screen (Snipping Tool)"] = "Снимок экрана (Ножницы)", ["Windows emoji picker"] = "Панель эмодзи Windows",
        ["Open Island Desk Studio"] = "Открыть Island Desk Studio", ["Quick settings"] = "Быстрые настройки", ["Expand"] = "Развернуть",
        ["Show notification"] = "Показать уведомление", ["Previous track"] = "Предыдущий трек", ["Next track"] = "Следующий трек",
        ["Play / pause"] = "Воспроизведение / пауза", ["Press Ctrl + Shift + N to reopen the latest notification"] = "Нажми Ctrl + Shift + N, чтобы открыть последнее уведомление снова",
        ["Waiting for notifications"] = "Ожидание уведомлений",
        ["Disabled in settings"] = "Отключено в настройках", ["Notifications connected"] = "Уведомления подключены",
        ["Notification permission not granted"] = "Разрешение на уведомления не выдано",
        ["Install the MSIX package to read notifications"] = "Для чтения уведомлений установи пакет MSIX",
        ["Notifications disabled in Island Desk"] = "Уведомления отключены в Island Desk",
        ["CPU °"] = "CPU °", ["GPU °"] = "GPU °", ["GB"] = "ГБ"
    };

    private void ApplyLocalization()
    {
        bool ru = string.Equals(App.Settings.Language, "ru", StringComparison.OrdinalIgnoreCase);
        TranslateMainTree(this, ru);
        NotificationStatus.Text = TranslateMainText(NotificationStatus.Text, ru);
        if (TrackTitle.Text is "Nothing playing" or "Ничего не играет") TrackTitle.Text = MediaEmptyTitle();
        if (TrackArtist.Text is "Media session" or "Медиаплеер") TrackArtist.Text = MediaEmptySubtitle();
        if (MediaLargeTitle.Text is "Nothing playing" or "Ничего не играет") MediaLargeTitle.Text = MediaEmptyTitle();
        if (NotificationTitle.Text is "Windows notifications will appear here" or "Здесь появятся сообщения из Windows")
            NotificationTitle.Text = ru ? "Здесь появятся сообщения из Windows" : "Windows notifications will appear here";
        UpdateClock();
    }

    private static void TranslateMainTree(DependencyObject root, bool russian)
    {
        if (root is TextBlock textBlock) textBlock.Text = TranslateMainText(textBlock.Text, russian);
        else if (root is Button button && button.Content is string content) button.Content = TranslateMainText(content, russian);
        else if (root is CheckBox checkBox && checkBox.Content is string checkContent) checkBox.Content = TranslateMainText(checkContent, russian);
        else if (root is FrameworkElement element && element.ToolTip is string tip) element.ToolTip = TranslateMainText(tip, russian);

        foreach (object child in LogicalTreeHelper.GetChildren(root))
            if (child is DependencyObject dependencyChild)
                TranslateMainTree(dependencyChild, russian);
    }

    private static string TranslateMainText(string value, bool russian)
    {
        if (russian) return MainRussian.TryGetValue(value, out string? translated) ? translated : value;
        foreach (var pair in MainRussian)
            if (string.Equals(pair.Value, value, StringComparison.Ordinal)) return pair.Key;
        return value;
    }

    private static string LocalizeNotificationStatus(string status)
    {
        bool russian = string.Equals(App.Settings.Language, "ru", StringComparison.OrdinalIgnoreCase);
        return TranslateMainText(status, russian);
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        ClockTiny.Text = now.ToString("HH:mm", CultureInfo.InvariantCulture);
        ClockLarge.Text = now.ToString("HH:mm", CultureInfo.InvariantCulture);
        CultureInfo culture = string.Equals(App.Settings.Language, "ru", StringComparison.OrdinalIgnoreCase)
            ? CultureInfo.GetCultureInfo("ru-RU") : CultureInfo.GetCultureInfo("en-US");
        ClockDate.Text = now.ToString("ddd, d MMM", culture);
    }

    private void UpdateGamingWidgetValues()
    {
        string metric = (App.Settings.GamingWidgetMetric ?? "Gpu").Trim();
        (string label, string value) = metric.ToLowerInvariant() switch
        {
            "cpu" => ("CPU", CpuTiny.Text),
            "gpu" => ("GPU", GpuTiny.Text),
            "cputemperature" => ("CPU", CpuTempTiny.Text),
            "gputemperature" => ("GPU", GpuTempTiny.Text),
            _ => ("", "")
        };
        GamingWidgetLabel.Text = metric.EndsWith("Temperature", StringComparison.OrdinalIgnoreCase)
            ? $"{label} °" : label;
        GamingWidgetValue.Text = value;
        GamingWidgetPanel.Visibility = metric.Equals("None", StringComparison.OrdinalIgnoreCase)
            ? Visibility.Collapsed : Visibility.Visible;
    }

    private void AnimateNotificationCard(FrameworkElement card)
    {
        if (!App.Settings.AnimationsEnabled || !App.Settings.NotificationAnimationsEnabled || !IsLoaded)
        {
            card.Opacity = 1;
            card.RenderTransform = Transform.Identity;
            return;
        }

        double seconds = Math.Clamp(App.Settings.AnimationDurationMs, 140, 700) / 1000d;
        var slide = new TranslateTransform(0, -14);
        card.RenderTransform = slide;
        card.Opacity = 0;
        card.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromSeconds(seconds))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-14, 0, TimeSpan.FromSeconds(seconds))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private void ReloadSettingsChangedByStudio()
    {
        DateTime writeTime = GetSettingsWriteTime();
        if (writeTime <= _lastSettingsWriteUtc || writeTime == DateTime.MinValue) return;
        _lastSettingsWriteUtc = writeTime;
        App.ReplaceSettings(SettingsService.Load());
        LoadSettingsIntoControls();
        ApplyVisibilitySettings();
        ApplyAppearanceSettings();
        RebuildWidgetOrder();
        UpdateEqualizerPreview();
        _hotkeys.Rebind(App.Settings);
        if (App.Settings.ShowNotifications) _ = _notificationService.StartAsync();
        else _notificationService.Stop();
        _ = RefreshMediaAsync();
    }

    private static DateTime GetSettingsWriteTime()
    {
        try { return File.Exists(SettingsService.FilePath) ? File.GetLastWriteTimeUtc(SettingsService.FilePath) : DateTime.MinValue; }
        catch { return DateTime.MinValue; }
    }

    private void ApplyForegroundAutomation()
    {
        string? process = ForegroundAppService.GetProcessName();
        bool Matches(IEnumerable<string> list) => process is not null && list.Any(x =>
            string.Equals(Path.GetFileNameWithoutExtension(x.Trim()), process, StringComparison.OrdinalIgnoreCase));

        bool fullscreen = ForegroundAppService.IsForegroundWindowFullscreen();
        bool shouldGameMode = App.Settings.GamingModeEnabled || Matches(App.Settings.GamingModeApps) ||
                              (App.Settings.GamingModeAutoDetectFullscreen && fullscreen);
        if (shouldGameMode != _gamingModeActive)
        {
            _gamingModeActive = shouldGameMode;
            _gameNotificationExpanded = false;
            if (!_gamingModeActive) _gameNotificationVisible = false;
            ApplyBrandAnimation();
            UpdateLayoutState();
        }

        bool shouldHide = Matches(App.Settings.HideIslandInApps) ||
                          (App.Settings.HideIslandInFullscreenApps && fullscreen && !_gamingModeActive);
        if (shouldHide && !_hiddenByAutomation)
        {
            _hiddenByAutomation = true;
            Hide();
        }
        else if (!shouldHide && _hiddenByAutomation)
        {
            _hiddenByAutomation = false;
            Show();
            EnsureTopmost();
        }

        bool shouldExpand = !_gamingModeActive && Matches(App.Settings.ExpandIslandInApps);
        if (shouldExpand && !_expanded && !_settingsOpen)
        {
            _autoExpanded = true;
            _expanded = true;
            _showNotificationDetails = false;
            UpdateLayoutState();
        }
        else if (!shouldExpand && _autoExpanded)
        {
            _autoExpanded = false;
            _expanded = false;
            _settingsOpen = false;
            _showNotificationDetails = false;
            UpdateLayoutState();
        }
    }


    private void PositionGamingIslandOnGameMonitor()
    {
        var monitor = ForegroundAppService.GetForegroundMonitor();
        if (monitor is { } target)
        {
            double monitorLeft = target.Bounds.Left / target.ScaleX;
            double monitorTop = target.Bounds.Top / target.ScaleY;
            double monitorWidth = target.Bounds.Width / target.ScaleX;
            Left = monitorLeft + (monitorWidth - Width) / 2;
            Top = monitorTop + 6;
        }
        else
        {
            Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
            Top = 5;
        }
    }

    private void EnsureTopmost()
    {
        if (!App.Settings.AlwaysOnTop)
        {
            Topmost = false;
            return;
        }

        Topmost = true;
        if (!IsVisible) return;
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
            SetWindowPos(handle, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
    }

    private void UpdateLayoutState()
    {
        if (_gamingModeActive)
        {
            NormalIslandContent.Visibility = Visibility.Collapsed;
            GamingModeContent.Visibility = Visibility.Visible;
            MainBody.Visibility = Visibility.Collapsed;
            UpdateGamingWidgetValues();
            GameNotificationToast.Visibility = _gameNotificationVisible && !_gameNotificationExpanded
                ? Visibility.Visible : Visibility.Collapsed;
            GameNotificationDetails.Visibility = _gameNotificationVisible && _gameNotificationExpanded
                ? Visibility.Visible : Visibility.Collapsed;

            bool showGameWidget = !string.Equals(App.Settings.GamingWidgetMetric, "None", StringComparison.OrdinalIgnoreCase);
            GamingWidgetPanel.Visibility = showGameWidget ? Visibility.Visible : Visibility.Collapsed;
            Width = _gameNotificationVisible ? 430 : (showGameWidget ? 150 : 96);
            Height = _gameNotificationVisible ? (_gameNotificationExpanded ? 236 : 146) : (showGameWidget ? 98 : 62);
            PositionGamingIslandOnGameMonitor();
            IslandCard.CornerRadius = new CornerRadius(28);
            GlassHighlightOverlay.CornerRadius = IslandCard.CornerRadius;
            EnsureTopmost();
            AnimateIsland();
            return;
        }

        GamingModeContent.Visibility = Visibility.Collapsed;
        NormalIslandContent.Visibility = Visibility.Visible;
        bool showBody = _expanded || _peeking || _showNotificationDetails;
        Width = Math.Clamp(App.Settings.IslandWidth, 440, 1320);
        Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
        Top = 0;

        OverviewPanel.Visibility = showBody && !_settingsOpen && !_peeking && !_showNotificationDetails ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = showBody && _settingsOpen ? Visibility.Visible : Visibility.Collapsed;
        PeekPanel.Visibility = _peeking && !_expanded ? Visibility.Visible : Visibility.Collapsed;
        NotificationDetailPanel.Visibility = _showNotificationDetails && _expanded && !_settingsOpen
            ? Visibility.Visible : Visibility.Collapsed;
        ExpandButton.Content = _expanded ? "⌃" : "⌄";

        if (showBody)
        {
            MainBody.BeginAnimation(UIElement.OpacityProperty, null);
            MainBody.Visibility = Visibility.Visible;
            if (IsLoaded && App.Settings.AnimationsEnabled)
            {
                double fadeSeconds = Math.Clamp(App.Settings.AnimationDurationMs, 100, 700) / 1000d;
                MainBody.Opacity = 0;
                MainBody.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromSeconds(fadeSeconds))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
            }
            else MainBody.Opacity = 1;
        }
        else if (MainBody.Visibility == Visibility.Visible && IsLoaded && App.Settings.AnimationsEnabled)
        {
            double seconds = Math.Clamp(App.Settings.AnimationDurationMs, 100, 700) / 1000d;
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(seconds))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            fadeOut.Completed += (_, _) =>
            {
                if (!_expanded && !_peeking && !_showNotificationDetails)
                {
                    MainBody.Visibility = Visibility.Collapsed;
                    MainBody.Opacity = 1;
                }
            };
            MainBody.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }
        else
        {
            MainBody.Visibility = Visibility.Collapsed;
            MainBody.Opacity = 1;
        }

        double targetHeight = _expanded ? (_showNotificationDetails ? 360 : 455) : (_peeking ? 160 : 76);
        AnimateWindowHeight(targetHeight);
        IslandCard.CornerRadius = new CornerRadius(Math.Clamp(App.Settings.IslandRadius, 12, 40));
        GlassHighlightOverlay.CornerRadius = IslandCard.CornerRadius;
        EnsureTopmost();
        AnimateIsland();
    }

    private void AnimateWindowHeight(double targetHeight)
    {
        BeginAnimation(FrameworkElement.HeightProperty, null);
        if (!IsLoaded || !App.Settings.AnimationsEnabled)
        {
            Height = targetHeight;
            return;
        }

        double startHeight = ActualHeight > 0 ? ActualHeight : Height;
        Height = targetHeight;
        if (Math.Abs(startHeight - targetHeight) < 1) return;
        double seconds = Math.Clamp(App.Settings.AnimationDurationMs, 100, 700) / 1000d;
        BeginAnimation(FrameworkElement.HeightProperty, new DoubleAnimation(startHeight, targetHeight, TimeSpan.FromSeconds(seconds))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        });
    }

    private void AnimateIsland()
    {
        if (!IsLoaded || !App.Settings.AnimationsEnabled)
        {
            IslandCard.BeginAnimation(UIElement.OpacityProperty, null);
            IslandCard.Opacity = 1;
            return;
        }
        double seconds = Math.Clamp(App.Settings.AnimationDurationMs, 100, 700) / 1000d;
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        IslandCard.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.78, 1.0, TimeSpan.FromSeconds(seconds)) { EasingFunction = easing });
        if (IslandCard.RenderTransform is ScaleTransform scale)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.985, 1, TimeSpan.FromSeconds(seconds)) { EasingFunction = easing });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.985, 1, TimeSpan.FromSeconds(seconds)) { EasingFunction = easing });
        }
    }

    private void ToggleExpanded()
    {
        if (_gamingModeActive)
        {
            if (_gameNotificationVisible) ShowLastNotification();
            return;
        }
        _expanded = !_expanded;
        _peeking = false;
        _showNotificationDetails = false;
        _settingsOpen = false;
        _peekTimer.Stop();
        UpdateLayoutState();
    }

    private void ShowLastNotification()
    {
        if (_lastNotification is null)
        {
            _expanded = true;
            _settingsOpen = false;
            _peeking = false;
            _showNotificationDetails = false;
            UpdateLayoutState();
            return;
        }

        FillNotificationDetails(_lastNotification);
        if (_gamingModeActive)
        {
            _gameNotificationVisible = true;
            _gameNotificationExpanded = true;
            _peekTimer.Stop();
            UpdateLayoutState();
            AnimateNotificationCard(GameNotificationDetails);
            return;
        }

        _expanded = true;
        _settingsOpen = false;
        _peeking = false;
        _showNotificationDetails = true;
        _peekTimer.Stop();
        UpdateLayoutState();
        AnimateNotificationCard(NotificationDetailPanel);
    }

    private void FillNotificationDetails(IslandNotification notification)
    {
        NotificationApp.Text = notification.AppName;
        NotificationTitle.Text = notification.Title;
        NotificationBody.Text = notification.Body;
        DetailNotificationApp.Text = notification.AppName.ToUpperInvariant();
        DetailNotificationTitle.Text = notification.Title;
        DetailNotificationBody.Text = string.IsNullOrWhiteSpace(notification.Body) ? LanguageText("No additional text.", "Нет дополнительного текста.") : notification.Body;
        DetailNotificationTime.Text = notification.Time.ToLocalTime().ToString(string.Equals(App.Settings.Language, "ru", StringComparison.OrdinalIgnoreCase) ? "dd.MM.yyyy · HH:mm" : "MMM d · HH:mm", CultureInfo.GetCultureInfo(string.Equals(App.Settings.Language, "ru", StringComparison.OrdinalIgnoreCase) ? "ru-RU" : "en-US"));
        PeekApp.Text = notification.AppName;
        PeekIcon.Text = string.IsNullOrWhiteSpace(notification.AppName) ? "✉" : notification.AppName.Trim()[0].ToString().ToUpperInvariant();
        PeekTitle.Text = notification.Title;
        PeekBody.Text = notification.Body;

        GameNotificationIcon.Text = string.IsNullOrWhiteSpace(notification.AppName) ? "✉" : notification.AppName.Trim()[0].ToString().ToUpperInvariant();
        GameNotificationApp.Text = notification.AppName;
        GameNotificationTitle.Text = notification.Title;
        GameNotificationBody.Text = notification.Body;
        GameDetailApp.Text = notification.AppName.ToUpperInvariant();
        GameDetailTitle.Text = notification.Title;
        GameDetailBody.Text = string.IsNullOrWhiteSpace(notification.Body) ? LanguageText("No additional text.", "Нет дополнительного текста.") : notification.Body;
        GameDetailTime.Text = notification.Time.ToLocalTime().ToString(string.Equals(App.Settings.Language, "ru", StringComparison.OrdinalIgnoreCase) ? "dd.MM.yyyy · HH:mm" : "MMM d · HH:mm", CultureInfo.GetCultureInfo(string.Equals(App.Settings.Language, "ru", StringComparison.OrdinalIgnoreCase) ? "ru-RU" : "en-US"));
    }

    private void ShowNotification(IslandNotification notification)
    {
        if (!App.Settings.ShowNotifications) return;
        if (App.Settings.ExcludedNotificationApps.Any(x =>
            !string.IsNullOrWhiteSpace(x) && notification.AppName.Contains(x.Trim(), StringComparison.OrdinalIgnoreCase))) return;

        _lastNotification = notification;
        FillNotificationDetails(notification);

        if (_gamingModeActive)
        {
            _gameNotificationVisible = true;
            _gameNotificationExpanded = false;
            UpdateLayoutState();
            AnimateNotificationCard(GameNotificationToast);
            _peekTimer.Stop();
            _peekTimer.Interval = TimeSpan.FromSeconds(7);
            _peekTimer.Start();
            return;
        }

        if (!_expanded)
        {
            _peeking = true;
            _showNotificationDetails = false;
            UpdateLayoutState();
            AnimateNotificationCard(PeekPanel);
            _peekTimer.Stop();
            _peekTimer.Interval = TimeSpan.FromSeconds(7);
            _peekTimer.Start();
        }
    }

    private void BrandHoverArea_MouseEnter(object sender, MouseEventArgs e) => SetBrandActionsVisible(true);

    private void BrandHoverArea_MouseLeave(object sender, MouseEventArgs e) => SetBrandActionsVisible(false);

    private void SetBrandActionsVisible(bool visible)
    {
        int version = ++_brandHoverVersion;
        HeaderActionsPanel.BeginAnimation(UIElement.OpacityProperty, null);
        BrandDisplay.BeginAnimation(UIElement.OpacityProperty, null);

        if (!App.Settings.AnimationsEnabled)
        {
            HeaderActionsPanel.Opacity = visible ? 1 : 0;
            HeaderActionsPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            HeaderActionsPanel.IsHitTestVisible = visible;
            BrandDisplay.Opacity = visible ? 0 : 1;
            BrandDisplay.IsHitTestVisible = !visible;
            return;
        }

        if (visible)
        {
            HeaderActionsPanel.Visibility = Visibility.Visible;
            HeaderActionsPanel.IsHitTestVisible = true;
            BrandDisplay.IsHitTestVisible = false;
            var showActions = new DoubleAnimation(HeaderActionsPanel.Opacity, 1, TimeSpan.FromMilliseconds(190))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var hideBrand = new DoubleAnimation(BrandDisplay.Opacity, 0, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            HeaderActionsPanel.BeginAnimation(UIElement.OpacityProperty, showActions);
            BrandDisplay.BeginAnimation(UIElement.OpacityProperty, hideBrand);
            return;
        }

        HeaderActionsPanel.IsHitTestVisible = false;
        var hideActions = new DoubleAnimation(HeaderActionsPanel.Opacity, 0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        hideActions.Completed += (_, _) =>
        {
            if (version != _brandHoverVersion || BrandHoverArea.IsMouseOver) return;
            HeaderActionsPanel.Visibility = Visibility.Collapsed;
            BrandDisplay.IsHitTestVisible = true;
        };
        var showBrand = new DoubleAnimation(BrandDisplay.Opacity, 1, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        HeaderActionsPanel.BeginAnimation(UIElement.OpacityProperty, hideActions);
        BrandDisplay.BeginAnimation(UIElement.OpacityProperty, showBrand);
    }

    private void HeaderMusic_MouseEnter(object sender, MouseEventArgs e) => SetMusicControlsVisible(true);

    private void HeaderMusic_MouseLeave(object sender, MouseEventArgs e) => SetMusicControlsVisible(false);

    private void SetMusicControlsVisible(bool visible)
    {
        int version = ++_musicHoverVersion;
        HeaderMusicControls.BeginAnimation(UIElement.OpacityProperty, null);
        if (visible)
        {
            HeaderMusicControls.IsHitTestVisible = true;
            if (!App.Settings.AnimationsEnabled)
            {
                HeaderMusicControls.Opacity = 1;
                return;
            }
            HeaderMusicControls.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(HeaderMusicControls.Opacity, 1, TimeSpan.FromMilliseconds(170))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
            return;
        }

        HeaderMusicControls.IsHitTestVisible = false;
        if (!App.Settings.AnimationsEnabled)
        {
            HeaderMusicControls.Opacity = 0;
            return;
        }
        var fade = new DoubleAnimation(HeaderMusicControls.Opacity, 0, TimeSpan.FromMilliseconds(190))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        fade.Completed += (_, _) =>
        {
            if (version != _musicHoverVersion || HeaderMusic.IsMouseOver) return;
            HeaderMusicControls.Opacity = 0;
        };
        HeaderMusicControls.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    private void UpdateEqualizerPreview()
    {
        var values = App.Settings.EqualizerBandsDb ?? Array.Empty<double>();
        Border[] bars =
        {
            EqPreviewBand0, EqPreviewBand1, EqPreviewBand2, EqPreviewBand3, EqPreviewBand4,
            EqPreviewBand5, EqPreviewBand6, EqPreviewBand7, EqPreviewBand8, EqPreviewBand9
        };

        for (int i = 0; i < bars.Length; i++)
        {
            double db = i < values.Length ? Math.Clamp(values[i], -12, 12) : 0;
            bars[i].Height = 5 + ((db + 12) / 24d * 14d);
            bars[i].Opacity = App.Settings.EqualizerEnabled ? 1 : 0.48;
        }

        EqualizerPreviewStatus.Text = App.Settings.EqualizerEnabled
            ? LanguageText("EQUALIZER APO · ON", "EQUALIZER APO · ВКЛ")
            : LanguageText("10-BAND · STUDIO", "10 ПОЛОС · STUDIO");
    }

    private void EqualizerPreview_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        Studio_Click(sender, new RoutedEventArgs());
        e.Handled = true;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        if (e.ButtonState == MouseButtonState.Pressed &&
            !IsInsideButton(source) && !IsWithinElement(source, HeaderMusic))
        {
            try { DragMove(); } catch { }
        }
    }

    private void ToggleGamingMode()
    {
        App.Settings.GamingModeEnabled = !App.Settings.GamingModeEnabled;
        SettingsService.Save(App.Settings);
        _lastSettingsWriteUtc = GetSettingsWriteTime();
        _gamingModeActive = App.Settings.GamingModeEnabled;
        _gameNotificationExpanded = false;
        if (!_gamingModeActive) _gameNotificationVisible = false;
        UpdateLayoutState();
    }

    private void Expand_Click(object sender, RoutedEventArgs e) => ToggleExpanded();

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (_gamingModeActive) return;
        _expanded = true;
        _peeking = false;
        _showNotificationDetails = false;
        _settingsOpen = !_settingsOpen;
        UpdateLayoutState();
    }

    private void Studio_Click(object sender, RoutedEventArgs e)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "IslandDesk.Studio.exe");
        if (!File.Exists(path))
        {
            MessageBox.Show(LanguageText("Place IslandDesk.Studio.exe beside IslandDesk.exe or install the shared MSIX package.", "Положи IslandDesk.Studio.exe рядом с IslandDesk.exe или установи общий MSIX-пакет."),
                "Island Desk Studio", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Не удалось открыть Studio"); }
    }

    private void Screenshot_Click(object sender, RoutedEventArgs e) => HotkeyService.LaunchSnippingTool();
    private void Emoji_Click(object sender, RoutedEventArgs e) => HotkeyService.LaunchEmojiPicker();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private async void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        await _mediaService.TogglePlaybackAsync();
        await RefreshMediaAsync();
    }

    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        await _mediaService.NextAsync();
        await RefreshMediaAsync();
    }

    private async void Previous_Click(object sender, RoutedEventArgs e)
    {
        await _mediaService.PreviousAsync();
        await RefreshMediaAsync();
    }

    private void NotificationPeek_Click(object sender, RoutedEventArgs e) => ShowLastNotification();

    private void NotificationPeek_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (IsInsideButton(e.OriginalSource as DependencyObject)) return;
        ShowLastNotification();
        e.Handled = true;
    }

    private void GameNotificationPreview_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (IsInsideButton(e.OriginalSource as DependencyObject)) return;
        GameNotificationDetails_Click(sender, e);
        e.Handled = true;
    }

    private static bool IsInsideButton(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is Button) return true;
            try { source = System.Windows.Media.VisualTreeHelper.GetParent(source); }
            catch { source = LogicalTreeHelper.GetParent(source); }
        }
        return false;
    }

    private static bool IsWithinElement(DependencyObject? source, DependencyObject parent)
    {
        while (source is not null)
        {
            if (ReferenceEquals(source, parent)) return true;
            try { source = System.Windows.Media.VisualTreeHelper.GetParent(source); }
            catch { source = LogicalTreeHelper.GetParent(source); }
        }
        return false;
    }

    private void GameNotificationDetails_Click(object sender, RoutedEventArgs e)
    {
        if (_lastNotification is null) return;
        _gameNotificationVisible = true;
        _gameNotificationExpanded = true;
        _peekTimer.Stop();
        UpdateLayoutState();
        AnimateNotificationCard(GameNotificationDetails);
    }

    private void CloseGameNotification_Click(object sender, RoutedEventArgs e)
    {
        _gameNotificationExpanded = false;
        _gameNotificationVisible = false;
        UpdateLayoutState();
    }

    private void CloseNotificationDetails_Click(object sender, RoutedEventArgs e)
    {
        _showNotificationDetails = false;
        _expanded = false;
        UpdateLayoutState();
    }

    private void DarkTheme_Click(object sender, RoutedEventArgs e) => SetTheme("Dark");
    private void LightTheme_Click(object sender, RoutedEventArgs e) => SetTheme("Light");

    private void SetTheme(string theme)
    {
        string previousDefaultTint = string.Equals(App.Settings.Theme, "Light", StringComparison.OrdinalIgnoreCase) ? "#F7F9FD" : "#171A24";
        string nextDefaultTint = string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase) ? "#F7F9FD" : "#171A24";
        if (string.Equals(App.Settings.GlassTintColor, previousDefaultTint, StringComparison.OrdinalIgnoreCase))
            App.Settings.GlassTintColor = nextDefaultTint;
        App.Settings.Theme = theme;
        ApplyAppearanceSettings();
        SettingsService.Save(App.Settings);
        _lastSettingsWriteUtc = GetSettingsWriteTime();
    }

    private void SettingChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _loadingControls) return;
        SaveSettingsFromControls();
        ApplyVisibilitySettings();
        if (App.Settings.ShowNotifications && (NotificationStatus.Text == "Disabled in settings" || NotificationStatus.Text == "Отключено в настройках"))
            _ = _notificationService.StartAsync();
        else if (!App.Settings.ShowNotifications)
        {
            _notificationService.Stop();
            NotificationStatus.Text = LanguageText("Disabled in settings", "Отключено в настройках");
        }
    }

    // Keep the game/app under the pointer as the foreground window when the user clicks the island.
    // The island still receives mouse input, but does not steal keyboard focus from a game.
    private IntPtr WindowMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmMouseActivate)
        {
            handled = true;
            return new IntPtr(MaNoActivate);
        }
        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
}
