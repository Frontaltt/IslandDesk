using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace IslandDesk.Services;

public sealed record IslandNotification(string AppName, string Title, string Body, DateTimeOffset Time);

public sealed class NotificationService
{
    private UserNotificationListener? _listener;
    private bool _started;

    public event Action<IslandNotification>? NotificationReceived;
    public event Action<string>? StatusChanged;

    public async Task StartAsync()
    {
        if (_started) return;
        try
        {
            _listener = UserNotificationListener.Current;
            var access = await _listener.RequestAccessAsync();
            if (access != UserNotificationListenerAccessStatus.Allowed)
            {
                StatusChanged?.Invoke("Notification permission not granted");
                return;
            }

            _listener.NotificationChanged += OnNotificationChanged;
            _started = true;
            StatusChanged?.Invoke("Notifications connected");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke("Install the MSIX package to read notifications");
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    public void Stop()
    {
        if (_listener is not null)
            _listener.NotificationChanged -= OnNotificationChanged;
        _started = false;
        StatusChanged?.Invoke("Notifications disabled in Island Desk");
    }

    private void OnNotificationChanged(UserNotificationListener sender, UserNotificationChangedEventArgs args)
    {
        if (args.ChangeKind != UserNotificationChangedKind.Added) return;
        try
        {
            var item = sender.GetNotification(args.UserNotificationId);
            if (item is null) return;

            var binding = item.Notification?.Visual?.GetBinding(KnownNotificationBindings.ToastGeneric);
            var textItems = binding?.GetTextElements();
            if (textItems is null || textItems.Count == 0) return;

            string app = item.AppInfo?.DisplayInfo?.DisplayName ?? "Notification";
            string title = textItems[0].Text ?? app;
            string body = string.Join("\n", textItems.Skip(1).Select(x => x.Text).Where(x => !string.IsNullOrWhiteSpace(x)));
            NotificationReceived?.Invoke(new IslandNotification(app, title, body, item.CreationTime));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }
}
