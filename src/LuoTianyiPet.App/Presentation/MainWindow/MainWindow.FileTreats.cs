using System.IO;
using System.Windows;
using LuoTianyiPet.Core;
using LuoTianyiPet.Platform.Windows;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    private void ApplyFileTreatPreferences(FileTreatPreferences preferences, bool save = true)
    {
        bool wasEnabled = _settings.FileTreats.EnableDesktopFileTreats;
        _settings = _settings with { FileTreats = preferences };
        if (_desktopItemDisappearanceSource is not null)
        {
            if (preferences.EnableDesktopFileTreats) _desktopItemDisappearanceSource.Start();
            else _desktopItemDisappearanceSource.Stop();
        }
        if (!preferences.EnableDesktopFileTreats)
        {
            CancelBunChase(restorePosition: true, restoreContinuousAnimation: true);
        }
        if (wasEnabled != preferences.EnableDesktopFileTreats)
        {
            _logger.Info(
                "file_treat.preferences_applied",
                preferences.EnableDesktopFileTreats ? "Enabled." : "Disabled.");
        }
        if (_persistSettings && save)
        {
            _ = SaveSettingsAsync("settings.file_treat_saved", "File treat preferences saved.");
        }
    }

    private void OnDesktopItemDisappeared(object? sender, DesktopItemDisappearedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_isClosing || !_settings.FileTreats.EnableDesktopFileTreats ||
                _fileDropInProgress || DateTimeOffset.Now < _suppressDesktopTreatUntil ||
                !IsBunChaseEnvironmentSafe())
            {
                _logger.Info(
                    "file_treat.desktop_event_suppressed",
                    "Desktop disappearance event was suppressed by the foreground safety policy.");
                return;
            }
            Point target = ConvertScreenPixelsToDips(e.ScreenPositionPixels);
            QueueBunTreat(target);
            _logger.Info(
                "file_treat.bun_created",
                e.UsedCachedIconPosition
                    ? "Bun created at the cached desktop icon position."
                    : "Bun created at the safe cursor-position fallback.");
        });
    }

    private void QueueBunTreat(Point screenPosition)
    {
        int maximum = Numeric.Clamp(_settings.FileTreats.MaximumQueuedBuns, 1, 12);
        if (_bunTargets.Count >= maximum)
        {
            ShowFeedbackBubble("包子太多啦，先吃完这些吧");
            return;
        }
        string bunPath = RuntimeAssetLocator.Object("xiaolongbao.png");
        if (!File.Exists(bunPath))
        {
            _logger.Info("file_treat.bun_asset_missing", "The runtime bun image is unavailable.");
            return;
        }
        BunTargetWindow bun = new(bunPath)
        {
            Left = screenPosition.X - 32,
            Top = screenPosition.Y - 32,
        };
        bun.DragReleased += OnBunDragReleased;
        _bunTargets.Add(bun);
        bun.Show();
        if (!_bunChaseActive) BeginBunChase();
        else if (_bunReturning) RedirectBunReturnToQueuedTreat();
        else if (_bunWaitingForManualFeed) ResumeBunChaseFromManualFeedWait();
    }
}
