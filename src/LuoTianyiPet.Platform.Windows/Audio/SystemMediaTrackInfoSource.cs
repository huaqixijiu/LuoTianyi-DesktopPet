using System.Diagnostics;
using LuoTianyiPet.Core;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace LuoTianyiPet.Platform.Windows;

public sealed class SystemMediaTrackInfoSource : IMediaTrackInfoSource
{
    private const ulong MaximumArtworkBytes = 1024 * 1024;
    private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
    private string _artworkIdentity = string.Empty;
    private byte[]? _cachedArtworkBytes;
    private bool _artworkRead;

    public async ValueTask<MediaTrackSnapshot> ReadAsync(string targetProcessName)
    {
        if (string.IsNullOrWhiteSpace(targetProcessName))
        {
            return MediaTrackSnapshot.NoSession;
        }

        try
        {
            _sessionManager ??=
                await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            GlobalSystemMediaTransportControlsSession? session = _sessionManager
                .GetSessions()
                .FirstOrDefault(candidate => MatchesSourceApplication(
                    candidate.SourceAppUserModelId,
                    targetProcessName));
            if (session is null)
            {
                ClearArtworkCache();
                return ReadFromWindowTitle(targetProcessName);
            }

            GlobalSystemMediaTransportControlsSessionMediaProperties properties =
                await session.TryGetMediaPropertiesAsync();
            MediaTrackSnapshot metadata = MediaTrackText.Normalize(new MediaTrackSnapshot(
                ProbeSucceeded: true,
                SessionFound: true,
                properties?.Title ?? string.Empty,
                properties?.Artist ?? string.Empty));
            MediaTrackSnapshot snapshot = metadata with
            {
                ArtworkBytes = metadata.HasTrack
                    ? await ReadArtworkAsync(properties, BuildIdentity(metadata.Title, metadata.Artist))
                    : null,
            };
            return SupplementArtistFromWindow(snapshot, ReadFromWindowTitle(targetProcessName));
        }
        catch (Exception)
        {
            _sessionManager = null;
            ClearArtworkCache();
            MediaTrackSnapshot fallback = ReadFromWindowTitle(targetProcessName);
            return fallback.HasTrack ? fallback : MediaTrackSnapshot.Unavailable;
        }
    }

    internal static MediaTrackSnapshot SupplementArtistFromWindow(
        MediaTrackSnapshot media,
        MediaTrackSnapshot window)
    {
        if (!media.HasTrack)
        {
            return window;
        }

        // Some CloudMusic versions publish only the first collaborator to SMTC.
        // Supplement only the same track and a compatible artist list; a stale
        // window during a track switch must not replace the current metadata.
        if (window.ProbeSucceeded && window.HasTrack &&
            string.Equals(media.Title, window.Title, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(window.Artist) &&
            (string.IsNullOrWhiteSpace(media.Artist) ||
                window.Artist.IndexOf(media.Artist, StringComparison.OrdinalIgnoreCase) >= 0))
        {
            return media with { Artist = window.Artist };
        }

        return media;
    }

    private async Task<byte[]?> ReadArtworkAsync(
        GlobalSystemMediaTransportControlsSessionMediaProperties? properties,
        string identity)
    {
        if (properties is null || string.IsNullOrWhiteSpace(identity))
        {
            ClearArtworkCache();
            return null;
        }

        if (_artworkRead && string.Equals(_artworkIdentity, identity, StringComparison.Ordinal))
        {
            return _cachedArtworkBytes;
        }

        _artworkIdentity = identity;
        _artworkRead = true;
        _cachedArtworkBytes = await TryReadArtworkAsync(properties.Thumbnail);
        return _cachedArtworkBytes;
    }

    private static async Task<byte[]?> TryReadArtworkAsync(IRandomAccessStreamReference? reference)
    {
        try
        {
            if (reference is null)
            {
                return null;
            }

            using IRandomAccessStreamWithContentType stream = await reference.OpenReadAsync();
            if (stream.Size is 0 or > MaximumArtworkBytes)
            {
                return null;
            }

            uint byteCount = checked((uint)stream.Size);
            using DataReader reader = new(stream.GetInputStreamAt(0));
            uint loaded = await reader.LoadAsync(byteCount);
            if (loaded == 0)
            {
                return null;
            }

            byte[] bytes = new byte[loaded];
            reader.ReadBytes(bytes);
            return bytes;
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or InvalidOperationException or IOException ||
            exception is ArgumentException or OverflowException ||
            exception.HResult is unchecked((int)0x800706BA) or
                unchecked((int)0x800706BE) or
                unchecked((int)0x80010108) or
                unchecked((int)0x8001010E))
        {
            return null;
        }
    }

    private static string BuildIdentity(string? title, string? artist) =>
        $"{title ?? string.Empty}\u001f{artist ?? string.Empty}";

    private void ClearArtworkCache()
    {
        _artworkIdentity = string.Empty;
        _cachedArtworkBytes = null;
        _artworkRead = false;
    }

    internal static MediaTrackSnapshot ReadFromWindowTitle(string targetProcessName)
    {
        string processName = Path.GetFileNameWithoutExtension(targetProcessName.Trim());
        if (string.IsNullOrWhiteSpace(processName))
        {
            return MediaTrackSnapshot.NoSession;
        }

        try
        {
            foreach (Process process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    if (TryParseWindowTitle(process.MainWindowTitle, out string title, out string artist))
                    {
                        return MediaTrackText.Normalize(new MediaTrackSnapshot(
                            ProbeSucceeded: true,
                            SessionFound: true,
                            title,
                            artist));
                    }
                }
            }

            return MediaTrackSnapshot.NoSession;
        }
        catch (Exception)
        {
            return MediaTrackSnapshot.Unavailable;
        }
    }

    internal static bool TryParseWindowTitle(
        string? windowTitle,
        out string title,
        out string artist)
    {
        title = string.Empty;
        artist = string.Empty;
        if (string.IsNullOrWhiteSpace(windowTitle))
        {
            return false;
        }

        string candidate = windowTitle!.Trim();
        string[] genericTitles = ["网易云音乐", "NetEase CloudMusic", "CloudMusic"];
        if (genericTitles.Contains(candidate, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (string genericTitle in genericTitles)
        {
            string suffix = $" - {genericTitle}";
            if (candidate.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                candidate = candidate.Substring(0, candidate.Length - suffix.Length).Trim();
                break;
            }
        }

        int separator = candidate.LastIndexOf(" - ", StringComparison.Ordinal);
        if (separator > 0 && separator < candidate.Length - 3)
        {
            title = candidate.Substring(0, separator).Trim();
            artist = candidate.Substring(separator + 3).Trim();
        }
        else
        {
            title = candidate;
        }

        return !string.IsNullOrWhiteSpace(title);
    }

    internal static bool MatchesSourceApplication(string? sourceAppUserModelId, string targetProcessName)
    {
        if (string.IsNullOrWhiteSpace(sourceAppUserModelId) ||
            string.IsNullOrWhiteSpace(targetProcessName))
        {
            return false;
        }

        string targetFileName = Path.GetFileName(targetProcessName.Trim());
        string targetStem = Path.GetFileNameWithoutExtension(targetFileName);
        string source = sourceAppUserModelId!.Trim();
        if (source.Equals(targetFileName, StringComparison.OrdinalIgnoreCase) ||
            source.Equals(targetStem, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string sourceFileName = Path.GetFileName(source);
        if (sourceFileName.Equals(targetFileName, StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileNameWithoutExtension(sourceFileName).Equals(
                targetStem,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        char[] separators = ['.', '!', '_', '-', '/', '\\', ':'];
        return source
            .Split(separators, StringSplitOptions.RemoveEmptyEntries)
            .Any(part => part.Equals(targetStem, StringComparison.OrdinalIgnoreCase));
    }
}
