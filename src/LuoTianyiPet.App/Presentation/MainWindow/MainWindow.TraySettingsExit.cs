using System.IO;
using System.Windows;
using System.Windows.Interop;
using LuoTianyiPet.Core;
using LuoTianyiPet.Platform.Windows;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    private void CreateTrayIcon()
    {
        try
        {
            _trayIcon = new TrayIconController(
                () => Dispatcher.BeginInvoke(ShowPetFromTray),
                () => Dispatcher.BeginInvoke(HidePetFromTray),
                () => IsVisible,
                () => Dispatcher.BeginInvoke(ShowSettingsDialog),
                () => Dispatcher.BeginInvoke(async () => await BeginUserRequestedExitAsync()),
                () => Dispatcher.BeginInvoke(OpenReminderSettings));
            if (_previewTray)
            {
                Dispatcher.BeginInvoke(
                    _trayIcon.ShowQuickPanel,
                    System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
            _logger.Info("tray.ready", "System tray controls are available.");
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or System.ComponentModel.Win32Exception or
            ArgumentException)
        {
            _logger.Error("tray.initialization_failed", exception);
        }
    }

    private void SetPermanentTopmost(bool enabled, bool save)
    {
        _permanentTopmost = enabled;
        _settings = _settings with
        {
            Window = _settings.Window with { AlwaysOnTop = enabled },
        };
        ApplyEffectiveTopmost();
        _trayIcon?.RefreshChecks();
        _logger.Info("window.topmost_changed", enabled ? "Enabled." : "Disabled.");
        if (save && _persistSettings)
        {
            _ = SaveSettingsAsync("settings.topmost_saved", "Topmost preference saved.");
        }
    }

    private void SetStartupEnabled(bool enabled, bool save)
    {
        StartupRegistrationResult result = _startupRegistrationService?.TrySetEnabled(enabled) ??
            new StartupRegistrationResult(StartupRegistrationStatus.Unavailable, false);
        bool actual = result.IsEnabled;
        _settings = _settings with
        {
            Window = _settings.Window with { StartWithWindows = actual },
        };
        _trayIcon?.RefreshChecks();
        if (result.Status is StartupRegistrationStatus.Succeeded or StartupRegistrationStatus.Unchanged)
        {
            _logger.Info("startup.registration_changed", actual ? "Enabled." : "Disabled.");
            if (save && _persistSettings)
            {
                _ = SaveSettingsAsync("settings.startup_saved", "Startup preference saved.");
            }
        }
        else
        {
            ShowFeedbackBubble("开机自启动设置没有成功，请稍后再试");
            _logger.Info("startup.registration_rejected", result.Status.ToString());
        }
    }

    private async void ShowSettingsDialog()
    {
        if (_isClosing)
        {
            return;
        }

        if (_settingsWindow is not null)
        {
            if (_settingsWindow.WindowState == WindowState.Minimized)
            {
                _settingsWindow.WindowState = WindowState.Normal;
            }
            _settingsWindow.Activate();
            return;
        }

        _logger.Info("settings.window_opening", "Settings window requested.");
        SettingsWindow settingsWindow = new(
            _settings.Notifications,
            _settings.Window,
            _settings.FileTreats,
            _settings.Appearance,
            _settings.Media,
            _startupRegistrationService?.IsEnabled ?? false,
            _messageNotificationSource,
            _reminders?.Book.Preferences)
        {
            Owner = this,
        };
        _settingsWindow = settingsWindow;
        if (_openPlannerNotificationSettings)
        {
            settingsWindow.NavigateNotifications();
            _openPlannerNotificationSettings = false;
        }
        settingsWindow.Closed += (_, _) => _settingsWindow = null;
        if (settingsWindow.ShowDialog() == true)
        {
            ApplyMessageNotificationPreferences(settingsWindow.SelectedNotificationPreferences);
            ApplyFileTreatPreferences(settingsWindow.SelectedFileTreatPreferences, save: false);
            ApplyAppearancePreferences(settingsWindow.SelectedAppearancePreferences, save: false);
            ApplyMediaPreferences(settingsWindow.SelectedMediaPreferences, save: false);
            ApplyWindowPreferences(
                settingsWindow.SelectedWindowPreferences,
                settingsWindow.StartWithWindowsSelected,
                save: false);
            if (_persistSettings)
            {
                await SaveSettingsAsync("settings.dialog_saved", "All settings saved together.");
            }
            if (_reminders is not null)
            {
                await _reminders.ChangeAsync(book =>
                    book.Preferences = settingsWindow.SelectedReminderPreferences);
            }
        }
    }

    private async void OnExitClick(object sender, RoutedEventArgs e)
    {
        await BeginUserRequestedExitAsync();
    }

    private async Task BeginUserRequestedExitAsync()
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        PlayAnimation("resonance-cracked-shake");
        int[] offsets = [0, -8, 8, -7, 7, -5, 5, -3, 3, 0];
        foreach (int offset in offsets)
        {
            PetShakeTransform.X = offset;
            await Task.Delay(70);
        }

        PetShakeTransform.X = 0;
        Close();
    }
}
