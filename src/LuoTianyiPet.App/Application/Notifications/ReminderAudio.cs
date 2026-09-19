using System.IO;
using System.Windows.Media;
using LuoTianyiPet.Core;
namespace LuoTianyiPet.App;

internal static class ReminderAudio
{
    private enum PlaybackKind { None, Alarm, Preview }
    private static MediaPlayer? _player;
    private static PlaybackKind _playbackKind;
    internal static event Action<bool>? PlaybackStateChanged;
    internal static bool IsPlaying { get; private set; }
    internal static double ActiveVolume => _player?.Volume ?? 0;
    internal static string SongPath => RuntimeAssetLocator.Audio("reminder-song.mp3");
    public static bool Play(ReminderPreferences preferences)
        => PlayCore(preferences, PlaybackKind.Alarm);
    internal static bool PlayPreview(double volume)
        => PlayCore(new ReminderPreferences { Sound = true, Volume = volume }, PlaybackKind.Preview);
    private static bool PlayCore(ReminderPreferences preferences, PlaybackKind kind)
    {
        Stop();
        if(!preferences.Sound || preferences.Volume<=0 || !File.Exists(SongPath))return false;
        try
        {
            MediaPlayer player=new(){Volume=preferences.Volume};
            _player=player;
            _playbackKind=kind;
            player.MediaOpened+=OnMediaOpened;
            player.MediaEnded+=OnMediaEnded;
            player.MediaFailed+=OnMediaFailed;
            // Open is asynchronous. Starting before MediaOpened can result in a
            // short transient sound and then silence on some WPF media sessions.
            player.Open(new Uri(SongPath, UriKind.Absolute));
            return true;
        }
        catch
        {
            Stop();
            return false;
        }
    }
    internal static void SetVolume(double volume)
    {
        if(_player is null)return;
        _player.Volume=Math.Max(0,Math.Min(1,volume));
    }
    private static void OnMediaOpened(object? sender, EventArgs e)
    {
        if(sender is not MediaPlayer player||!ReferenceEquals(_player,player))return;
        try
        {
            player.Play();
            SetPlaybackState(true);
        }
        catch { Stop(); }
    }
    private static void OnMediaEnded(object? sender, EventArgs e)
    {
        if(sender is MediaPlayer player&&ReferenceEquals(_player,player))Stop();
    }
    private static void OnMediaFailed(object? sender, ExceptionEventArgs e)
    {
        if(sender is MediaPlayer player&&ReferenceEquals(_player,player))Stop();
    }
    private static void SetPlaybackState(bool value)
    {
        if(IsPlaying==value)return;
        IsPlaying=value;
        try { PlaybackStateChanged?.Invoke(value); }
        catch { }
    }
    public static void Stop()
        => StopCore(null);
    internal static void StopAlarm()
        => StopCore(PlaybackKind.Alarm);
    internal static void StopPreview()
        => StopCore(PlaybackKind.Preview);
    private static void StopCore(PlaybackKind? expectedKind)
    {
        if(expectedKind is PlaybackKind expected&&_playbackKind!=expected)return;
        MediaPlayer? player=_player;
        _player=null;
        _playbackKind=PlaybackKind.None;
        if(player is not null)
        {
            player.MediaOpened-=OnMediaOpened;
            player.MediaEnded-=OnMediaEnded;
            player.MediaFailed-=OnMediaFailed;
            try { player.Stop(); } catch { }
            try { player.Close(); } catch { }
        }
        SetPlaybackState(false);
    }
}
