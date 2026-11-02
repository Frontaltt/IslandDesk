using Windows.Media.Control;
using Windows.Storage.Streams;

namespace IslandDesk.Services;

public sealed record MediaInfo(string Title, string Artist, string AppName, bool IsPlaying, byte[]? ThumbnailBytes);

public sealed class MediaSessionService
{
    public async Task<MediaInfo?> ReadAsync()
    {
        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var session = manager.GetCurrentSession();
            if (session is null) return null;

            var properties = await session.TryGetMediaPropertiesAsync();
            var status = session.GetPlaybackInfo()?.PlaybackStatus;
            string source = session.SourceAppUserModelId ?? "Media player";
            string appName = source.Contains("spotify", StringComparison.OrdinalIgnoreCase)
                ? "Spotify"
                : source.Contains("chrome", StringComparison.OrdinalIgnoreCase)
                    ? "Chrome"
                    : source.Contains("firefox", StringComparison.OrdinalIgnoreCase)
                        ? "Firefox"
                        : source.Split('!')[0].Split('.').LastOrDefault() ?? "Media player";

            string title = string.IsNullOrWhiteSpace(properties?.Title) ? "Unknown track" : properties!.Title;
            string artist = string.IsNullOrWhiteSpace(properties?.Artist) ? appName : properties!.Artist;
            byte[]? thumbnail = await ReadThumbnailAsync(properties?.Thumbnail);
            return new MediaInfo(title, artist, appName,
                status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing, thumbnail);
        }
        catch { return null; }
    }

    private static async Task<byte[]?> ReadThumbnailAsync(IRandomAccessStreamReference? thumbnail)
    {
        try
        {
            if (thumbnail is null) return null;
            using IRandomAccessStreamWithContentType stream = await thumbnail.OpenReadAsync();
            if (stream.Size == 0 || stream.Size > 8 * 1024 * 1024) return null;
            using var reader = new DataReader(stream.GetInputStreamAt(0));
            await reader.LoadAsync((uint)stream.Size);
            var bytes = new byte[(int)stream.Size];
            reader.ReadBytes(bytes);
            return bytes;
        }
        catch { return null; }
    }

    public async Task TogglePlaybackAsync()
    {
        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var session = manager.GetCurrentSession();
            if (session is null) return;
            if (session.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                await session.TryPauseAsync();
            else
                await session.TryPlayAsync();
        }
        catch { }
    }

    public async Task NextAsync()
    {
        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var session = manager.GetCurrentSession();
            if (session is not null) await session.TrySkipNextAsync();
        }
        catch { }
    }

    public async Task PreviousAsync()
    {
        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var session = manager.GetCurrentSession();
            if (session is not null) await session.TrySkipPreviousAsync();
        }
        catch { }
    }
}
