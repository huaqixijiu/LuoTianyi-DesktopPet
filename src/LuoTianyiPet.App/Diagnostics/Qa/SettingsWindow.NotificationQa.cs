using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.App;

public partial class SettingsWindow
{
    // Isolated UI-only QA: no platform source, user configuration, or notification requests.
    internal static async Task<int> RunNotificationSettingsQaAsync()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "NotificationSettingsQa");
        Directory.CreateDirectory(directory);
        List<string> checks = [];
        void Check(bool ok, string label)
        {
            if (!ok) throw new InvalidOperationException(label);
            checks.Add("PASS " + label);
        }
        SettingsWindow? window = null;
        SettingsWindow Create(IMessageNotificationSource? source, MediaPreferences? media = null, ReminderPreferences? reminder = null) => new(
            new MessageNotificationPreferences { EnableMessageReminders = true, EnableQqDetailedReminders = true,
                EnableWeChatDetailedReminders = true, WindowsNotificationAccessGranted = false },
            new WindowPreferences(), new FileTreatPreferences(), new AppearancePreferences(), media ?? new MediaPreferences(), false, source,
            reminder);
        try
        {
            foreach (MessageNotificationAccessStatus status in Enum.GetValues(typeof(MessageNotificationAccessStatus)))
            {
                var source = new NotificationSettingsQaSource { Status = status };
                window = Create(source);
                window.Show();
                window.NotificationNavigationRadioButton.IsChecked = true;
                await Task.Delay(180);
                window.UpdateLayout();
                string prefix = status + ": ";
                Check(source.RequestCount == 0, prefix + "Opening settings never requests system permission");
                Check(!window.NotificationRulesExpander.IsExpanded, prefix + "Rules start collapsed");
                double collapsedExtent=window.NotificationPage.ExtentHeight;
                Check(window.AlarmSoundCheckBox.IsVisible && window.AlarmVolumeSlider.IsVisible, prefix + "Music on keeps the volume row visible");
                window.AlarmSoundCheckBox.IsChecked = false;
                window.UpdateLayout();
                Check(!window.AlarmVolumeSlider.IsVisible, prefix + "Music off hides the volume row");
                window.AlarmSoundCheckBox.IsChecked = true;
                window.UpdateLayout();
                Check(window.NotificationPage.ScrollableWidth < 1, prefix + "Page never scrolls horizontally");
                Check((window.NotificationAccessBanner.Visibility == Visibility.Collapsed) == (status == MessageNotificationAccessStatus.Allowed), prefix + "Access banner appears only while access needs attention");
                Check(window.NotificationAccessButton.IsEnabled == (status is MessageNotificationAccessStatus.Unspecified or MessageNotificationAccessStatus.Unavailable) && window.NotificationAccessButton.IsVisible == window.NotificationAccessButton.IsEnabled, prefix + "Only actionable permission states show an enabled authorize button");
                Check(window.NotificationQqIcon.Source is BitmapImage qq && qq.UriSource.ToString().EndsWith("notification-qq.png") &&
                    window.NotificationWeChatIcon.Source is BitmapImage wc && wc.UriSource.ToString().EndsWith("notification-wechat.png"), prefix + "Both icons use the existing user-derived PNG assets");
                CaptureNotificationSettings(window, Path.Combine(directory, status + ".png"), 1);
                window.MessageReminderCheckBox.Focus();
                window.MessageReminderCheckBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                Check(window.QqDetailedReminderCheckBox.IsKeyboardFocused, prefix + "Tab reaches QQ");
                window.QqDetailedReminderCheckBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                Check(window.WeChatDetailedReminderCheckBox.IsKeyboardFocused, prefix + "Tab reaches WeChat");
                ((IToggleProvider)new ToggleButtonAutomationPeer(window.QqDetailedReminderCheckBox)).Toggle();
                Check(window.QqDetailedReminderCheckBox.IsChecked == false && window.WeChatDetailedReminderCheckBox.IsChecked == true,
                    prefix + "QQ and WeChat detail choices remain independent");
                window.MessageReminderCheckBox.IsChecked = false;
                window.UpdateLayout();
                Check(!window.QqDetailedReminderCheckBox.IsVisible && !window.WeChatDetailedReminderCheckBox.IsVisible,
                    prefix + "Master off collapses the detail section");
                Check(window.WeChatDetailedReminderCheckBox.IsChecked == true, prefix + "Master off preserves detail preference");
                window.MessageReminderCheckBox.IsChecked = true;
                window.UpdateLayout();
                Check(window.QqDetailedReminderCheckBox.IsVisible && window.WeChatDetailedReminderCheckBox.IsVisible,
                    prefix + "Master on restores the detail section");
                window.QqDetailedReminderCheckBox.IsChecked = true;
                Check(window.SaveStatusText.Text == "有未保存的更改", prefix + "Changes are marked unsaved");
                CaptureNotificationSettings(window, Path.Combine(directory, status + ".png"), 1);
                ToggleButton disclosure = FindNotificationVisual<ToggleButton>(window.NotificationRulesExpander)!;
                ((IToggleProvider)new ToggleButtonAutomationPeer(disclosure)).Toggle();
                window.UpdateLayout();
                Check(window.NotificationRulesExpander.IsExpanded && window.NotificationRulesFirstParagraph.IsVisible,
                    prefix + "Disclosure opens the rules inline");
                window.NotificationPage.ScrollToEnd();
                window.UpdateLayout();
                Check(window.SaveStatusText.IsVisible, prefix + "Footer remains visible while reading rules");
                if (status == MessageNotificationAccessStatus.Allowed)
                    CaptureNotificationSettings(window, Path.Combine(directory, "rules-expanded.png"), 1);
                ((IToggleProvider)new ToggleButtonAutomationPeer(disclosure)).Toggle();
                window.UpdateLayout();
                Check(!window.NotificationRulesExpander.IsExpanded && Math.Abs(window.NotificationPage.ExtentHeight-collapsedExtent)<1,
                    prefix + "Closing rules restores the original page extent");
                if (status == MessageNotificationAccessStatus.Unspecified)
                {
                    source.Status = MessageNotificationAccessStatus.Allowed;
                    window.NotificationAccessButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                    await Task.Delay(30);
                    Check(source.RequestCount == 1 && window.SelectedNotificationPreferences.WindowsNotificationAccessGranted,
                        "Grant button updates the pending preference exactly once");
                    Check(window.NotificationAccessBanner.Visibility == Visibility.Collapsed && !window.NotificationAccessButton.IsVisible,
                        "Granted state hides the whole access banner");
                }
                if (status == MessageNotificationAccessStatus.Allowed)
                {
                    Check(window.SelectedNotificationPreferences.WindowsNotificationAccessGranted,
                        "Existing system grant is preserved when saving stale local preferences");
                    CaptureNotificationSettings(window, Path.Combine(directory, "allowed-125.png"), 1.25);
                    CaptureNotificationSettings(window, Path.Combine(directory, "allowed-150.png"), 1.5);
                }
                window.Close(); window = null;
            }
            window = Create(null);
            window.Show(); window.NotificationNavigationRadioButton.IsChecked = true;
            Check(!window.NotificationAccessButton.IsEnabled && window.NotificationAccessButton.Visibility == Visibility.Collapsed && window.NotificationAccessBanner.Visibility == Visibility.Visible,
                "Absent platform source shows guidance without a dead action");
            window.Close(); window = Create(new NotificationSettingsQaSource { Status = MessageNotificationAccessStatus.Allowed });
            int previewScale = 0;
            window.DisplayScalePreviewChanged += value => previewScale = value;
            window.Show();
            window.DisplayScaleSlider.Value = AppearancePreferences.MaximumDisplayScalePercent;
            window.UpdateLayout();
            Check(window.DisplayScaleSlider.Maximum == 300 && previewScale == 300,
                "Display scale settings expose and preview the 300% upper bound");
            window.Close();
            window = Create(new NotificationSettingsQaSource { Status = MessageNotificationAccessStatus.Allowed });
            SettingsWindow modal = window;
            _ = modal.Dispatcher.BeginInvoke(new Action(() =>
            {
                modal.MessageReminderCheckBox.IsChecked = false;
                modal.QqDetailedReminderCheckBox.IsChecked = false;
                modal.WeChatDetailedReminderCheckBox.IsChecked = true;
                modal.AlarmAnimationCheckBox.IsChecked=true;
                modal.AlarmSoundCheckBox.IsChecked=false;
                modal.AlarmVolumeSlider.Value=0.35;
                modal.PlannerSizeSelector.SelectedIndex=2;
                modal.OnSaveClick(modal, new RoutedEventArgs());
                Check(modal.SelectedAppearancePreferences.PlannerSize=="comfortable","Planner size setting saves independently");
            }));
            Check(modal.ShowDialog() == true, "Save accepts the settings dialog");
            Check(modal.SelectedReminderPreferences.Animation&&!modal.SelectedReminderPreferences.Sound&&Math.Abs(modal.SelectedReminderPreferences.Volume-0.35)<0.001,"Save commits independent alarm animation sound and volume");
            Check(!modal.SelectedNotificationPreferences.EnableMessageReminders && !modal.SelectedNotificationPreferences.EnableQqDetailedReminders &&
                modal.SelectedNotificationPreferences.EnableWeChatDetailedReminders && modal.SelectedNotificationPreferences.WindowsNotificationAccessGranted,
                "Save commits all notification choices and the current grant");
            window = Create(null, new MediaPreferences { ShowMusicIslands = true });
            SettingsWindow islandsModal = window;
            _ = islandsModal.Dispatcher.BeginInvoke(new Action(() =>
            {
                islandsModal.MusicNavigationRadioButton.IsChecked = true;
                Check(islandsModal.MusicIslandsCheckBox.IsChecked == true,
                    "Islands switch loads the saved media preference");
                islandsModal.MusicIslandsCheckBox.IsChecked = false;
                islandsModal.OnSaveClick(islandsModal, new RoutedEventArgs());
            }));
            Check(islandsModal.ShowDialog() == true, "Islands switch save accepts the dialog");
            Check(!islandsModal.SelectedMediaPreferences.ShowMusicIslands,
                "Save commits the islands switch through the existing media path");
            window = Create(null);
            window.Show();
            foreach ((string tag, string name) in new[] { ("General", "general"), ("Music", "music"), ("Notification", "notification"), ("About", "about") })
            {
                ((System.Windows.Controls.RadioButton)window.FindName(tag + "NavigationRadioButton")).IsChecked = true;
                await Task.Delay(120);
                window.UpdateLayout();
                ScrollViewer page = (ScrollViewer)window.FindName(tag + "Page");
                Check(page.ScrollableWidth < 1, name + " page never scrolls horizontally");
                CaptureNotificationSettings(window, Path.Combine(directory, name + "-125.png"), 1.25);
                CaptureNotificationSettings(window, Path.Combine(directory, name + "-150.png"), 1.5);
                if (tag == "Music")
                {
                    Check(window.TogglePlayPauseShortcutTextBox.Text == MediaPreferences.DefaultTogglePlayPauseShortcut &&
                        window.PreviousTrackShortcutTextBox.Text == MediaPreferences.DefaultPreviousTrackShortcut &&
                        window.NextTrackShortcutTextBox.Text == MediaPreferences.DefaultNextTrackShortcut,
                        "Music page loads the three existing default shortcuts");
                    Check(window.TogglePlayPauseShortcutTextBox.IsVisible &&
                        window.PreviousTrackShortcutTextBox.IsVisible &&
                        window.NextTrackShortcutTextBox.IsVisible,
                        "Music page exposes all three shortcut capture fields");
                    page.ScrollToEnd();
                    window.UpdateLayout();
                    CaptureNotificationSettings(window, Path.Combine(directory, "music-shortcuts-bottom-150.png"), 1.5);
                }
            }
            window.Close();
            window = null;
            window = Create(null, reminder: new ReminderPreferences { Sound = true, Volume = 0.35 });
            window.Show();
            TaskCompletionSource<bool> firstPlayback = new();
            void ObservePlayback(bool playing)
            {
                if (playing) firstPlayback.TrySetResult(true);
            }
            ReminderAudio.PlaybackStateChanged += ObservePlayback;
            try
            {
                window.OnTestAlarmSound(window, new RoutedEventArgs());
                await Task.WhenAny(firstPlayback.Task, Task.Delay(5000));
                Check(firstPlayback.Task.Status == TaskStatus.RanToCompletion && ReminderAudio.IsPlaying &&
                    Equals(window.TestAlarmSoundButton.Content, "停止试听"),
                    "First preview waits for MediaOpened and enters the playing state");
                window.AlarmVolumeSlider.Value=0.72;
                Check(Math.Abs(ReminderAudio.ActiveVolume-0.72)<0.001,
                    "Changing volume while previewing updates the active player");
                ReminderAudio.StopAlarm();
                Check(ReminderAudio.IsPlaying,
                    "Planner alarm cleanup does not stop the settings preview");
                window.OnTestAlarmSound(window, new RoutedEventArgs());
                Check(!ReminderAudio.IsPlaying && Equals(window.TestAlarmSoundButton.Content, "试听音乐"),
                    "Stopping preview releases the player and resets the button");
                firstPlayback = new();
                window.OnTestAlarmSound(window, new RoutedEventArgs());
                await Task.WhenAny(firstPlayback.Task, Task.Delay(5000));
                Check(firstPlayback.Task.Status == TaskStatus.RanToCompletion && ReminderAudio.IsPlaying,
                    "A second preview can open the same MP3 after the first one stops");
            }
            finally
            {
                ReminderAudio.PlaybackStateChanged -= ObservePlayback;
                ReminderAudio.Stop();
                window.Close();
            }
            window = null;
        }
        catch (Exception error) { checks.Add("FAIL " + error); }
        finally { window?.Close(); }
        File.WriteAllLines(Path.Combine(directory, "result.txt"), checks);
        return checks.Any(line => line.StartsWith("FAIL", StringComparison.Ordinal)) ? 1 : 0;
    }

    private static T? FindNotificationVisual<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) return match;
            if (FindNotificationVisual<T>(child) is T nested) return nested;
        }
        return null;
    }

    private static void CaptureNotificationSettings(SettingsWindow window, string path, double scale)
    {
        FrameworkElement content = (FrameworkElement)window.Content;
        window.UpdateLayout();
        RenderTargetBitmap bitmap = new((int)Math.Ceiling(content.ActualWidth * scale),
            (int)Math.Ceiling(content.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(content);
        PngBitmapEncoder encoder = new(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream file = File.Create(path); encoder.Save(file);
    }

    private sealed class NotificationSettingsQaSource : IMessageNotificationSource
    {
        public MessageNotificationAccessStatus Status { get; set; }
        public int RequestCount { get; private set; }
        public event EventHandler<MessageNotificationReceivedEventArgs>? NotificationReceived { add { } remove { } }
        public MessageNotificationAccessStatus GetAccessStatus() => Status;
        public ValueTask<MessageNotificationAccessStatus> RequestAccessAsync() { RequestCount++; return new(Status); }
        public void Start() { }
        public void Stop() { }
        public void Dispose() { }
    }
}
