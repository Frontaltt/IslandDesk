using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Input;
using IslandDesk.Models;

namespace IslandDesk.Services;

public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int ToggleIslandId = 0x5101;
    private const int PeekNotificationId = 0x5102;
    private const int ScreenshotId = 0x5103;
    private const int EmojiId = 0x5104;
    private const int GameModeId = 0x5105;
    private readonly List<int> _registered = new();
    private HwndSource? _source;
    private IntPtr _handle;

    public event Action? ToggleIsland;
    public event Action? PeekNotification;
    public event Action? Screenshot;
    public event Action? EmojiPicker;
    public event Action? ToggleGameMode;

    public void Attach(IntPtr handle, AppSettings settings)
    {
        _handle = handle;
        _source = HwndSource.FromHwnd(handle);
        _source?.AddHook(WndProc);
        Register(ToggleIslandId, settings.ToggleHotkey);
        Register(PeekNotificationId, settings.NotificationHotkey);
        Register(ScreenshotId, settings.ScreenshotHotkey);
        Register(EmojiId, settings.EmojiHotkey);
        Register(GameModeId, settings.GameModeHotkey);
    }

    public void Rebind(AppSettings settings)
    {
        foreach (int id in _registered) UnregisterHotKey(_handle, id);
        _registered.Clear();
        Register(ToggleIslandId, settings.ToggleHotkey);
        Register(PeekNotificationId, settings.NotificationHotkey);
        Register(ScreenshotId, settings.ScreenshotHotkey);
        Register(EmojiId, settings.EmojiHotkey);
        Register(GameModeId, settings.GameModeHotkey);
    }

    private void Register(int id, string chord)
    {
        if (_handle == IntPtr.Zero || !TryParseChord(chord, out uint modifiers, out uint key)) return;
        if (RegisterHotKey(_handle, id, modifiers, key)) _registered.Add(id);
    }

    private static bool TryParseChord(string chord, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        if (string.IsNullOrWhiteSpace(chord)) return false;
        var pieces = chord.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (pieces.Length < 2) return false;
        string keyName = pieces[^1];
        foreach (string mod in pieces[..^1])
        {
            switch (mod.ToLowerInvariant())
            {
                case "ctrl": case "control": modifiers |= 0x0002; break;
                case "shift": modifiers |= 0x0004; break;
                case "alt": modifiers |= 0x0001; break;
                case "win": case "windows": modifiers |= 0x0008; break;
                default: return false;
            }
        }
        try
        {
            Key key = keyName.Equals("Space", StringComparison.OrdinalIgnoreCase)
                ? Key.Space : Enum.Parse<Key>(keyName, true);
            virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
            return virtualKey != 0;
        }
        catch { return false; }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey)
        {
            switch (wParam.ToInt32())
            {
                case ToggleIslandId: ToggleIsland?.Invoke(); break;
                case PeekNotificationId: PeekNotification?.Invoke(); break;
                case ScreenshotId: Screenshot?.Invoke(); break;
                case EmojiId: EmojiPicker?.Invoke(); break;
                case GameModeId: ToggleGameMode?.Invoke(); break;
            }
            handled = true;
        }
        return IntPtr.Zero;
    }

    public static void LaunchSnippingTool()
    {
        try { Process.Start(new ProcessStartInfo("ms-screenclip:") { UseShellExecute = true }); }
        catch { }
    }

    public static void LaunchEmojiPicker()
    {
        const byte KeyUp = 0x02;
        const byte VkLwin = 0x5B;
        const byte VkOemPeriod = 0xBE;
        keybd_event(VkLwin, 0, 0, UIntPtr.Zero);
        keybd_event(VkOemPeriod, 0, 0, UIntPtr.Zero);
        keybd_event(VkOemPeriod, 0, KeyUp, UIntPtr.Zero);
        keybd_event(VkLwin, 0, KeyUp, UIntPtr.Zero);
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
            foreach (int id in _registered) UnregisterHotKey(_handle, id);
        _registered.Clear();
        _source?.RemoveHook(WndProc);
        _source = null;
        _handle = IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
}
