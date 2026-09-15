using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    // Isolated in-process WPF regression: never sends input to other applications.
    private async Task RunQuickActionsQaAsync()
    {
        if (_persistSettings) return;
        string directory = Path.Combine(AppContext.BaseDirectory, "QuickActionsQa");
        Directory.CreateDirectory(directory);
        List<string> checks = [];
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            checks.Add("PASS " + name);
        }
        try
        {
            await Task.Delay(700);
            SetMusicIslandsVisible(false);
            OnRootMouseEnter(this, new MouseEventArgs(Mouse.PrimaryDevice, 0));
            ShowTrackInfo(new MediaTrackSnapshot(true, true, "测试歌曲", "洛天依"), true);
            ShowTrackSwitchPending();
            Check(MediaControls.Visibility == Visibility.Collapsed && !MediaControls.IsHitTestVisible,
                "Disabled islands reject hover, track updates and track-switch feedback");
            CaptureQuickActionsQa(this, Path.Combine(directory, "01-hidden.png"));

            SetMusicIslandsVisible(true);
            OnRootMouseEnter(this, new MouseEventArgs(Mouse.PrimaryDevice, 0));
            await Task.Delay(220);
            Check(MediaControls.Visibility == Visibility.Visible && MediaControls.IsHitTestVisible &&
                PreviousTrackButton.Visibility == Visibility.Visible &&
                TogglePlayPauseButton.Visibility == Visibility.Visible &&
                NextTrackButton.Visibility == Visibility.Visible &&
                CloudMusicVolumeButton.Visibility == Visibility.Visible,
                "Enabling islands exposes the combined surface and all four controls");
            Check(TrackTitleText.Text == "未在播放", "No track uses a quiet empty state");
            Check(TrackTimelineText.Visibility == Visibility.Collapsed,
                "Missing timeline keeps the third-row time text hidden");
            byte[] sampleArtwork = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
            ShowTrackInfo(
                new MediaTrackSnapshot(
                    true,
                    true,
                    "测试歌曲",
                    "洛天依",
                    sampleArtwork,
                    new MediaTrackTimeline(
                        TimeSpan.FromSeconds(42),
                        TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(56))),
                true);
            await Task.Delay(220);
            Check(TrackArtworkImage.Visibility == Visibility.Visible &&
                TrackArtworkPlaceholder.Visibility == Visibility.Collapsed,
                "Track artwork decodes in memory and replaces the placeholder");
            Check(TrackProgressRing.Visibility == Visibility.Visible &&
                TrackProgressRingTrack.Visibility == Visibility.Visible &&
                TrackTimelineText.Visibility == Visibility.Visible &&
                TrackTimelineText.Text == "0:42 / 3:56" &&
                TogglePlayPauseButton.ToolTip?.ToString()?.Contains("0:42 / 3:56", StringComparison.Ordinal) == true,
                "Track timeline renders a progress ring, third-row time text and tooltip elapsed/total");
            CaptureQuickActionsQa(this, Path.Combine(directory, "02-visible.png"), true);
            CloudMusicVolumeButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            await Task.Delay(150);
            Check(CloudMusicVolumePopup.IsOpen, "Volume key opens the anchored volume popup");
            CloudMusicVolumePopup.IsOpen = false;
            await Task.Delay(150);
            SetMusicIslandsVisible(false);
            HideFeedbackBubble(restoreTrackInfo: true);
            // Simulate a metadata response already in flight when the switch was turned off.
            ShowTrackInfo(new MediaTrackSnapshot(true, true, "延迟返回的歌曲", "测试歌手"), true);
            Check(MediaControls.Visibility == Visibility.Collapsed && !CloudMusicVolumePopup.IsOpen,
                "Late metadata and feedback restoration cannot reopen disabled islands");

            SetPositionLocked(true);
            double left = Left, top = Top;
            BeginWindowDrag();
            Check(!_isWindowDragging && Left == left && Top == top, "Locked position rejects dragging");
            string previousStyle = _settings.Appearance.FullBodyStyle;
            bool styleChanged = false;
            for (int attempt = 0; attempt < 8 && !styleChanged; attempt++)
            {
                // An idle crystal yawn holds the display-mode toggle; end it so the check is deterministic.
                if (_stateMachine.ActiveReactionToken is Guid reactionToken)
                {
                    _stateMachine.CompleteReaction(reactionToken, DateTimeOffset.Now);
                }
                HandlePointerAction(new PointerGestureAction(PointerGestureActionType.ToggleDisplayMode));
                styleChanged = _settings.Appearance.FullBodyStyle != previousStyle;
                if (!styleChanged) await Task.Delay(150);
            }
            Check(styleChanged, "Locked position still permits double-click appearance changes");
            SetPositionLocked(false);
            BeginWindowDrag();
            Check(_isWindowDragging, "Unlock restores dragging");
            EndWindowDrag();
            await Task.Delay(600);

            AppSettings beforeHide = _settings;
            HidePetFromTray();
            Check(!IsVisible, "Tray hide hides the pet");
            ShowPetFromTray();
            Check(IsVisible && _settings.Window.AlwaysOnTop == beforeHide.Window.AlwaysOnTop,
                "Tray recall restores visibility without changing permanent topmost preference");
            ShowPetFromTray();
            Check(IsVisible, "Repeated tray left action never hides the pet");

            _petQuickPanel = new PetQuickPanel(() => _settings, SetPositionLocked,
                value => SetPermanentTopmost(value, true), SetMusicIslandsVisible,
                value => SetDisplayScalePercent(value, true));
            _petQuickPanel.ShowNearPet(new DesktopRectangle(Left, Top, ActualWidth, ActualHeight), GetQuickActionsWorkArea());
            Check(!_petQuickPanel.CalendarButton.Focusable && !_petQuickPanel.AlarmButton.Focusable,
                "Calendar and alarm quick-entry buttons do not retain a blue keyboard-focus state");
            CaptureQuickActionsQa(_petQuickPanel, Path.Combine(directory, "03-pet-menu.png"), true);
            int petSettingsRequests=0,petExitRequests=0;
            _petQuickPanel.OpenSettings=()=>petSettingsRequests++;
            _petQuickPanel.ExitPet=()=>{petExitRequests++;return Task.CompletedTask;};
            _petQuickPanel.SettingsButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Check(petSettingsRequests==1&&!_petQuickPanel.IsVisible,"Pet settings closes menu and opens existing settings action");
            _petQuickPanel.ShowNearPet(new DesktopRectangle(Left,Top,ActualWidth,ActualHeight),GetQuickActionsWorkArea());
            _petQuickPanel.ExitPetButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Check(petExitRequests==1&&!_petQuickPanel.IsVisible,"Pet exit closes menu and dispatches normal exit once");
            _petQuickPanel.Hide();
            int exitRequests = 0;
            TrayQuickPanel tray = new(ShowPetFromTray, HidePetFromTray, () => IsVisible, ShowSettingsDialog,
                () => exitRequests++);
            System.Drawing.Rectangle work = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
            System.Drawing.Point trayAnchor = new(work.Right - 40, work.Bottom - 20);
            tray.ShowNearTray(trayAnchor);
            await Task.Delay(120);
            double trayLeft = tray.Left, trayTop = tray.Top;
            double trayHeight = tray.ActualHeight;
            Check(!tray.ShowPetButton.IsKeyboardFocused, "Mouse opening does not preselect a menu action");
            tray.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            Check(tray.ShowPetButton.IsKeyboardFocused, "Tab enters the first menu action");
            tray.ShowPetButton.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            Check(tray.HidePetButton.IsKeyboardFocused, "Keyboard navigation reaches hide");
            tray.HidePetButton.IsEnabled = false;
            tray.ShowPetButton.Focus();
            tray.ShowPetButton.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            Check(tray.OpenSettingsButton.IsKeyboardFocused, "Keyboard navigation skips disabled hide");
            tray.RefreshState();
            tray.HidePanel();
            tray.ShowNearTray(trayAnchor);
            Check(!tray.ShowPetButton.IsKeyboardFocused && !tray.OpenSettingsButton.IsKeyboardFocused,
                "Mouse reopening clears the previous keyboard selection");
            CaptureQuickActionsQa(tray, Path.Combine(directory, "04-tray-menu.png"), true);
            for (int reopen = 0; reopen < 8; reopen++)
            {
                tray.HidePanel();
                tray.ShowNearTray(trayAnchor);
                await Task.Delay(30);
                Check(Math.Abs(tray.Left - trayLeft) < 1 && Math.Abs(tray.Top - trayTop) < 1 &&
                    Math.Abs(tray.ActualHeight - trayHeight) < 1,
                    $"Tray reopen {reopen + 1} keeps the original anchor and size");
            }
            CaptureQuickActionsQa(tray, Path.Combine(directory, "06-tray-reopened.png"));
            tray.ExitButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Check(exitRequests == 1 && !tray.IsVisible,
                "One exit click closes the menu and dispatches exit without confirmation");
            Check(Math.Abs(tray.Left - trayLeft) < 1 && Math.Abs(tray.Top - trayTop) < 1,
                "Exit does not reposition the panel toward the button");
            tray.ShowNearTray(trayAnchor);
            tray.RaiseEvent(new System.Windows.Input.KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(tray),
                0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Check(!tray.IsVisible && exitRequests == 1, "Escape dismisses the menu without requesting exit");
            tray.Close();
            SettingsWindow settings = new(_settings.Notifications, _settings.Window,
                _settings.FileTreats, _settings.Appearance, _settings.Media, false, null);
            settings.Show();
            await Task.Delay(250);
            CaptureQuickActionsQa(settings, Path.Combine(directory, "05-settings.png"));
            settings.Close();
            int previousScale = _settings.Appearance.DisplayScalePercent;
            SetDisplayScalePercent(AppearancePreferences.MaximumDisplayScalePercent, save: false);
            await Task.Delay(180);
            Check(_settings.Appearance.DisplayScalePercent == 300,
                "Display scale applies the 300% upper bound to the live pet window");
            _petQuickPanel?.RefreshState();
            Check(_petQuickPanel is null || !_petQuickPanel.IncreaseScaleButton.IsEnabled,
                "Quick panel disables increase at the 300% upper bound");
            CaptureQuickActionsQa(this, Path.Combine(directory, "07-scale-300.png"));
            SetDisplayScalePercent(previousScale, save: false);
            File.WriteAllLines(Path.Combine(directory, "result.txt"), checks);
            Close();
        }
        catch (Exception exception)
        {
            checks.Add("FAIL " + exception);
            File.WriteAllLines(Path.Combine(directory, "result.txt"), checks);
            Application.Current.Shutdown(1);
        }
    }

    private static void CaptureQuickActionsQa(Window window, string path, bool dpi = false)
    {
        window.UpdateLayout();
        FrameworkElement content = (FrameworkElement)window.Content;
        void Shot(string file, double scale)
        {
            RenderTargetBitmap bitmap = new((int)Math.Ceiling(window.ActualWidth * scale),
                (int)Math.Ceiling(window.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bitmap.Render(content);
            PngBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using FileStream fileStream = File.Create(file);
            encoder.Save(fileStream);
        }
        Shot(path, 1.0);
        if (dpi)
        {
            string stem = Path.ChangeExtension(path, null);
            Shot(stem + "-125.png", 1.25);
            Shot(stem + "-150.png", 1.5);
        }
    }
}
