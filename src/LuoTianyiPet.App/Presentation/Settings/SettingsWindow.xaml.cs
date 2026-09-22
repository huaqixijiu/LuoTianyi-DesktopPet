using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using LuoTianyiPet.Core;
using LuoTianyiPet.Platform.Windows;
using TextBox = System.Windows.Controls.TextBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfRadioButton = System.Windows.Controls.RadioButton;

namespace LuoTianyiPet.App;

public partial class SettingsWindow : Window
{
    private readonly IMessageNotificationSource? _messageNotificationSource;
    private bool _isInitializing = true;
    private TextBox? _activeShortcutInput;
    private string? _shortcutBeforeCapture;
    public event Action<int>? DisplayScalePreviewChanged;
    public SettingsWindow(
        MessageNotificationPreferences notificationPreferences,
        WindowPreferences windowPreferences,
        FileTreatPreferences fileTreatPreferences,
        AppearancePreferences appearancePreferences,
        MediaPreferences mediaPreferences,
        bool startupRegistrationEnabled,
        IMessageNotificationSource? messageNotificationSource,
        ReminderPreferences? reminderPreferences = null)
    {
        Guard.NotNull(notificationPreferences, nameof(notificationPreferences));
        Guard.NotNull(windowPreferences, nameof(windowPreferences));
        Guard.NotNull(fileTreatPreferences, nameof(fileTreatPreferences));
        Guard.NotNull(appearancePreferences, nameof(appearancePreferences));
        Guard.NotNull(mediaPreferences, nameof(mediaPreferences));
        SelectedNotificationPreferences = notificationPreferences;
        SelectedWindowPreferences = windowPreferences;
        SelectedFileTreatPreferences = fileTreatPreferences;
        SelectedAppearancePreferences = AppearancePreferences.Normalize(appearancePreferences);
        SelectedMediaPreferences = MediaPreferences.Normalize(mediaPreferences);
        StartWithWindowsSelected = startupRegistrationEnabled;
        _messageNotificationSource = messageNotificationSource;
        InitializeComponent();
        InitializeShortcutInputs();
        SelectedReminderPreferences=reminderPreferences??new();
        AlarmAnimationCheckBox.IsChecked=SelectedReminderPreferences.Animation;
        AlarmSoundCheckBox.IsChecked=SelectedReminderPreferences.Sound;
        AlarmVolumeSlider.Value=SelectedReminderPreferences.Volume;
        ReminderAudio.PlaybackStateChanged+=OnReminderPlaybackStateChanged;
        Closed+=OnClosed;

        MessageReminderCheckBox.IsChecked = notificationPreferences.EnableMessageReminders;
        QqDetailedReminderCheckBox.IsChecked = notificationPreferences.EnableQqDetailedReminders;
        WeChatDetailedReminderCheckBox.IsChecked = notificationPreferences.EnableWeChatDetailedReminders;
        StartWithWindowsCheckBox.IsChecked = startupRegistrationEnabled;
        AlwaysOnTopCheckBox.IsChecked = windowPreferences.AlwaysOnTop;
        DisplayScaleSlider.Value = SelectedAppearancePreferences.DisplayScalePercent;
        PlannerSizeSelector.SelectedIndex = Array.IndexOf(new[]{"mini","standard","comfortable","fullscreen"},SelectedAppearancePreferences.PlannerSize);
        FullBodyStyleCyclingCheckBox.IsChecked =
            SelectedAppearancePreferences.EnableFullBodyStyleCycling;
        DesktopFileTreatsCheckBox.IsChecked = fileTreatPreferences.EnableDesktopFileTreats;
        bool musicAnimationEnabled = SelectedMediaPreferences.MusicAnimationSelection !=
            MusicAnimationOptions.NoneSelection;
        MusicAnimationEnabledCheckBox.IsChecked = musicAnimationEnabled;
        MusicIslandsCheckBox.IsChecked = SelectedMediaPreferences.ShowMusicIslands;
        SelectMusicAnimationCard(musicAnimationEnabled
            ? SelectedMediaPreferences.MusicAnimationSelection
            : MusicAnimationOptions.AutomaticSelection);
        UpdateMusicAnimationControlsEnabledState();
        GeneralNavigationRadioButton.IsChecked = true;
        _isInitializing = false;
    }

    public MessageNotificationPreferences SelectedNotificationPreferences { get; private set; }

    public WindowPreferences SelectedWindowPreferences { get; private set; }

    public FileTreatPreferences SelectedFileTreatPreferences { get; private set; }

    public AppearancePreferences SelectedAppearancePreferences { get; private set; }

    public MediaPreferences SelectedMediaPreferences { get; private set; }

    public bool StartWithWindowsSelected { get; private set; }
    public ReminderPreferences SelectedReminderPreferences { get; private set; } = new();
    private bool _alarmPreview;
    internal void NavigateNotifications() { NotificationNavigationRadioButton.IsChecked=true; }
    private void OnAlarmVolumeChanged(object sender,RoutedPropertyChangedEventArgs<double> e)
    {
        if(_alarmPreview)ReminderAudio.SetVolume(AlarmVolumeSlider.Value);
        OnSettingChanged(sender,e);
    }
    private void OnReminderPlaybackStateChanged(bool playing)
    {
        if(playing||!_alarmPreview)return;
        _alarmPreview=false;
        TestAlarmSoundButton.Content="试听音乐";
    }
    private void OnClosed(object? sender,EventArgs e)
    {
        ReminderAudio.PlaybackStateChanged-=OnReminderPlaybackStateChanged;
        if(_alarmPreview)
        {
            ReminderAudio.StopPreview();
            _alarmPreview=false;
        }
    }
    private void OnTestAlarmSound(object sender,RoutedEventArgs e)
    {
        if(_alarmPreview){ReminderAudio.StopPreview();_alarmPreview=false;TestAlarmSoundButton.Content="试听音乐";return;}
        if(!ReminderAudio.PlayPreview(AlarmVolumeSlider.Value))return;
        _alarmPreview=true;TestAlarmSoundButton.Content="停止试听";
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Version? version = typeof(SettingsWindow).Assembly.GetName().Version;
        VersionTextBlock.Text = version is null
            ? "版本 0.1.0.96"
            : $"版本 {version}";
        UpdateNotificationAccessDisplay();
    }

    private void OnNavigationChecked(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfRadioButton { Tag: string page })
        {
            return;
        }

        GeneralPage.Visibility = page == "General" ? Visibility.Visible : Visibility.Collapsed;
        MusicPage.Visibility = page == "Music" ? Visibility.Visible : Visibility.Collapsed;
        NotificationPage.Visibility = page == "Notification" ? Visibility.Visible : Visibility.Collapsed;
        AboutPage.Visibility = page == "About" ? Visibility.Visible : Visibility.Collapsed;

        string speech = page switch
        {
            "Music" => "要一起听歌吗？",
            "Notification" => "有新消息的话，\n我会告诉你的。",
            "About" => "谢谢你让我\n留在桌面上～",
            _ => "今天也，\n一起加油吧～",
        };
        string artwork = page switch
        {
            "Music" => "settings-sidebar-music.png",
            "Notification" => "settings-sidebar-notification.png",
            "About" => "settings-sidebar-about.png",
            _ => "settings-sidebar-general.png",
        };
        string artworkName = page switch
        {
            "Music" => "音乐页面装饰：真好",
            "Notification" => "通知页面装饰：天哪",
            "About" => "关于页面装饰：加入我们",
            _ => "常规页面装饰：我推",
        };
        UpdatePageCompanion(speech, artwork, artworkName);
    }

    private void UpdatePageCompanion(string speech, string artwork, string artworkName)
    {
        PageSpeechText.BeginAnimation(OpacityProperty, null);
        PageArtworkImage.BeginAnimation(OpacityProperty, null);
        PageSpeechText.Text = speech;
        PageArtworkImage.Source = new BitmapImage(
            RuntimeAssetLocator.PackUri($"ui/{artwork}"));
        System.Windows.Automation.AutomationProperties.SetName(PageArtworkImage, artworkName);
        if (!SystemParameters.ClientAreaAnimation)
        {
            PageSpeechText.Opacity = 1;
            PageArtworkImage.Opacity = 0.95;
            return;
        }

        PageSpeechText.Opacity = 0;
        PageArtworkImage.Opacity = 0;
        DoubleAnimation fade = new(0, 1, TimeSpan.FromMilliseconds(160))
        {
            FillBehavior = FillBehavior.Stop,
        };
        fade.Completed += (_, _) => PageSpeechText.Opacity = 1;
        PageSpeechText.BeginAnimation(OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);
        DoubleAnimation artworkFade = new(0, 0.95, TimeSpan.FromMilliseconds(160))
        {
            FillBehavior = FillBehavior.Stop,
        };
        artworkFade.Completed += (_, _) => PageArtworkImage.Opacity = 0.95;
        PageArtworkImage.BeginAnimation(
            OpacityProperty,
            artworkFade,
            HandoffBehavior.SnapshotAndReplace);
    }

    private void OnSettingChanged(object sender, RoutedEventArgs e)
    {
        if (!_isInitializing)
        {
            if (ReferenceEquals(sender, DisplayScaleSlider))
            {
                int previewScale = (int)Math.Round(DisplayScaleSlider.Value / 5d) * 5;
                DisplayScalePreviewChanged?.Invoke(previewScale);
            }
            SaveStatusText.Text = "有未保存的更改";
            SaveStatusText.Foreground = (System.Windows.Media.Brush)FindResource("PrimaryDark");
        }
    }

    private void OnMusicAnimationEnabledChanged(object sender, RoutedEventArgs e)
    {
        UpdateMusicAnimationControlsEnabledState();
        OnSettingChanged(sender, e);
    }

    private void UpdateMusicAnimationControlsEnabledState()
    {
        bool enabled = MusicAnimationEnabledCheckBox.IsChecked == true;
        MusicAnimationSelectionPanel.IsEnabled = enabled;
    }

    private void InitializeShortcutInputs()
    {
        TogglePlayPauseShortcutTextBox.Text = SelectedMediaPreferences.TogglePlayPauseShortcut;
        PreviousTrackShortcutTextBox.Text = SelectedMediaPreferences.PreviousTrackShortcut;
        NextTrackShortcutTextBox.Text = SelectedMediaPreferences.NextTrackShortcut;

        if (!AreShortcutInputsValid(out string? message))
        {
            SetShortcutStatus(message!, isError: true);
        }
    }

    private void OnShortcutInputMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox input)
        {
            return;
        }

        e.Handled = true;
        BeginShortcutCapture(input);
        input.Focus();
    }

    private void OnShortcutInputLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox input && ReferenceEquals(_activeShortcutInput, input))
        {
            CancelShortcutCapture();
        }
    }

    private void OnShortcutInputPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox input)
        {
            return;
        }

        if (!ReferenceEquals(_activeShortcutInput, input))
        {
            BeginShortcutCapture(input);
        }

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            CancelShortcutCapture();
            e.Handled = true;
            return;
        }

        if (IsModifierKey(key))
        {
            SetShortcutStatus("请继续按下字母、数字、方向键或空格。", isError: false);
            e.Handled = true;
            return;
        }

        ModifierKeys modifiers = Keyboard.Modifiers;
        if ((modifiers & ModifierKeys.Windows) != 0)
        {
            SetShortcutStatus("暂不支持 Windows 键，请使用 Ctrl、Alt 或 Shift。", isError: true);
            e.Handled = true;
            return;
        }

        ModifierKeys supportedModifiers = modifiers &
            (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift);
        if (supportedModifiers == ModifierKeys.None)
        {
            SetShortcutStatus("快捷键至少需要 Ctrl、Alt 或 Shift 中的一个。", isError: true);
            e.Handled = true;
            return;
        }

        if (!TryGetPrimaryKeyName(key, out string primaryKey))
        {
            SetShortcutStatus("不支持这个按键，请使用字母、数字、方向键或空格。", isError: true);
            e.Handled = true;
            return;
        }

        string shortcut = BuildShortcutText(supportedModifiers, primaryKey);
        if (IsShortcutUsedByAnotherInput(shortcut, input))
        {
            SetShortcutStatus("这个组合键已经分配给另一项操作，请换一个。", isError: true);
            e.Handled = true;
            return;
        }

        input.Text = shortcut;
        FinishShortcutCapture();
        SetShortcutStatus("快捷键已更新，保存后立即生效。", isError: false);
        OnSettingChanged(input, e);
        e.Handled = true;
    }

    private void OnResetCloudMusicShortcutsClick(object sender, RoutedEventArgs e)
    {
        CancelShortcutCapture();
        TogglePlayPauseShortcutTextBox.Text = MediaPreferences.DefaultTogglePlayPauseShortcut;
        PreviousTrackShortcutTextBox.Text = MediaPreferences.DefaultPreviousTrackShortcut;
        NextTrackShortcutTextBox.Text = MediaPreferences.DefaultNextTrackShortcut;
        SetShortcutStatus("已恢复默认快捷键，保存后生效。", isError: false);
        OnSettingChanged(sender, e);
    }

    private void BeginShortcutCapture(TextBox input)
    {
        if (ReferenceEquals(_activeShortcutInput, input))
        {
            return;
        }

        CancelShortcutCapture();
        _activeShortcutInput = input;
        _shortcutBeforeCapture = input.Text;
        input.Text = "请按下组合键…";
        SetShortcutStatus("正在录入，按下组合键后会自动完成；Esc 取消。", isError: false);
    }

    private void CancelShortcutCapture()
    {
        if (_activeShortcutInput is not null && _shortcutBeforeCapture is not null)
        {
            _activeShortcutInput.Text = _shortcutBeforeCapture;
        }

        FinishShortcutCapture();
    }

    private void FinishShortcutCapture()
    {
        _activeShortcutInput = null;
        _shortcutBeforeCapture = null;
    }

    private bool AreShortcutInputsValid(out string? message)
    {
        TextBox[] inputs =
        [
            TogglePlayPauseShortcutTextBox,
            PreviousTrackShortcutTextBox,
            NextTrackShortcutTextBox,
        ];
        foreach (TextBox input in inputs)
        {
            if (!ShortcutBinding.Parse(input.Text).IsValid)
            {
                message = $"“{GetShortcutLabel(input)}”的组合键无效，请重新录入或恢复默认。";
                return false;
            }
        }

        for (int i = 0; i < inputs.Length; i++)
        {
            for (int j = i + 1; j < inputs.Length; j++)
            {
                if (ShortcutBinding.Parse(inputs[i].Text).EquivalentTo(ShortcutBinding.Parse(inputs[j].Text)))
                {
                    message = $"“{GetShortcutLabel(inputs[i])}”与“{GetShortcutLabel(inputs[j])}”不能使用同一个组合键。";
                    return false;
                }
            }
        }

        message = null;
        return true;
    }

    private bool IsShortcutUsedByAnotherInput(string shortcut, TextBox current)
    {
        ShortcutBinding candidate = ShortcutBinding.Parse(shortcut);
        return new[]
        {
            TogglePlayPauseShortcutTextBox,
            PreviousTrackShortcutTextBox,
            NextTrackShortcutTextBox,
        }
            .Where(input => !ReferenceEquals(input, current))
            .Select(input => ShortcutBinding.Parse(input.Text))
            .Any(candidate.EquivalentTo);
    }

    private void SetShortcutStatus(string message, bool isError)
    {
        ShortcutStatusText.Text = message;
        ShortcutStatusText.Foreground = (System.Windows.Media.Brush)FindResource(
            isError ? "PrimaryDark" : "Muted");
    }

    private static string GetShortcutLabel(TextBox input) => input.Name switch
    {
        nameof(TogglePlayPauseShortcutTextBox) => "暂停 / 继续",
        nameof(PreviousTrackShortcutTextBox) => "上一首",
        _ => "下一首",
    };

    private static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or
        Key.LWin or Key.RWin;

    private static bool TryGetPrimaryKeyName(Key key, out string name)
    {
        if (key is >= Key.A and <= Key.Z)
        {
            name = key.ToString();
            return true;
        }

        if (key is >= Key.D0 and <= Key.D9)
        {
            name = ((int)key - (int)Key.D0).ToString();
            return true;
        }

        name = key switch
        {
            Key.Left => "Left",
            Key.Right => "Right",
            Key.Up => "Up",
            Key.Down => "Down",
            Key.Space => "Space",
            _ => string.Empty,
        };
        return name.Length > 0;
    }

    private static string BuildShortcutText(ModifierKeys modifiers, string primaryKey)
    {
        List<string> parts = [];
        if ((modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
        if ((modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
        if ((modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
        parts.Add(primaryKey);
        return string.Join("+", parts);
    }

    private async void OnRequestNotificationAccessClick(object sender, RoutedEventArgs e)
    {
        if (_messageNotificationSource is null)
        {
            UpdateNotificationAccessDisplay();
            return;
        }

        NotificationAccessButton.IsEnabled = false;
        MessageNotificationAccessStatus status =
            await _messageNotificationSource.RequestAccessAsync();
        SelectedNotificationPreferences = SelectedNotificationPreferences with
        {
            WindowsNotificationAccessGranted =
                status == MessageNotificationAccessStatus.Allowed,
        };
        OnSettingChanged(sender, e);
        UpdateNotificationAccessDisplay(status);
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        CancelShortcutCapture();
        if (!AreShortcutInputsValid(out string? shortcutMessage))
        {
            MusicNavigationRadioButton.IsChecked = true;
            SetShortcutStatus(shortcutMessage!, isError: true);
            return;
        }

        SelectedReminderPreferences=new(){Animation=AlarmAnimationCheckBox.IsChecked==true,Sound=AlarmSoundCheckBox.IsChecked==true,Volume=AlarmVolumeSlider.Value,Tone="再给我一天的时间吧QAQ"};
        SelectedNotificationPreferences = SelectedNotificationPreferences with
        {
            EnableMessageReminders = MessageReminderCheckBox.IsChecked == true,
            EnableQqDetailedReminders = QqDetailedReminderCheckBox.IsChecked == true,
            EnableWeChatDetailedReminders = WeChatDetailedReminderCheckBox.IsChecked == true,
        };
        StartWithWindowsSelected = StartWithWindowsCheckBox.IsChecked == true;
        SelectedWindowPreferences = SelectedWindowPreferences with
        {
            AlwaysOnTop = AlwaysOnTopCheckBox.IsChecked == true,
            StartWithWindows = StartWithWindowsSelected,
        };
        SelectedFileTreatPreferences = SelectedFileTreatPreferences with
        {
            EnableDesktopFileTreats = DesktopFileTreatsCheckBox.IsChecked == true,
        };
        SelectedAppearancePreferences = AppearancePreferences.Normalize(
            SelectedAppearancePreferences with
            {
                EnableFullBodyStyleCycling = FullBodyStyleCyclingCheckBox.IsChecked == true,
                DisplayScalePercent = (int)Math.Round(DisplayScaleSlider.Value / 5d) * 5,
                PlannerSize = new[]{"mini","standard","comfortable","fullscreen"}[Math.Max(0,PlannerSizeSelector.SelectedIndex)],
            });
        WpfRadioButton? selectedMusicAnimation = MusicAnimationSelectionPanel.Children
            .OfType<WpfRadioButton>()
            .FirstOrDefault(option => option.IsChecked == true);
        string selection = MusicAnimationEnabledCheckBox.IsChecked == true &&
            selectedMusicAnimation?.Tag is string selected
                ? selected
                : MusicAnimationOptions.NoneSelection;
        SelectedMediaPreferences = MediaPreferences.Normalize(
            SelectedMediaPreferences with
            {
                ShowMusicIslands = MusicIslandsCheckBox.IsChecked == true,
                MusicAnimationSelection = selection,
                // The former one-animation Easter-egg switch is retired. In
                // intelligent mode both Luo Tianyi animations form the pool;
                // fixed mode is artist-independent.
                EnableLuoTianyiSingingEasterEgg = true,
                TogglePlayPauseShortcut = TogglePlayPauseShortcutTextBox.Text.Trim(),
                PreviousTrackShortcut = PreviousTrackShortcutTextBox.Text.Trim(),
                NextTrackShortcut = NextTrackShortcutTextBox.Text.Trim(),
            });
        DialogResult = true;
    }

    private void SelectMusicAnimationCard(string? selection)
    {
        string selected = MusicAnimationOptions.NormalizeSelection(selection);
        WpfRadioButton card = MusicAnimationSelectionPanel.Children
            .OfType<WpfRadioButton>()
            .First(option => string.Equals(option.Tag as string, selected, StringComparison.Ordinal));
        card.IsChecked = true;
    }

    private void UpdateNotificationAccessDisplay(MessageNotificationAccessStatus? knownStatus = null)
    {
        MessageNotificationAccessStatus status = knownStatus ??
            _messageNotificationSource?.GetAccessStatus() ?? MessageNotificationAccessStatus.Unavailable;
        if (status == MessageNotificationAccessStatus.Allowed)
        {
            // An existing system grant can outlive the locally saved preference.
            SelectedNotificationPreferences = SelectedNotificationPreferences with { WindowsNotificationAccessGranted = true };
        }
        NotificationAccessBanner.Visibility = status == MessageNotificationAccessStatus.Allowed
            ? Visibility.Collapsed : Visibility.Visible;
        NotificationAccessBannerText.Text = status switch
        {
            MessageNotificationAccessStatus.Denied => "系统通知访问已被拒绝",
            MessageNotificationAccessStatus.PackageIdentityRequired => "系统通知访问需要安装版",
            _ => "授权系统通知访问，提醒内容更完整",
        };
        NotificationAccessHintText.Text = status switch
        {
            MessageNotificationAccessStatus.Unspecified => "授权后，可获取系统通知中的昵称和摘要。",
            MessageNotificationAccessStatus.Denied => "可在 Windows 设置中调整通知访问；仍会尝试来源提醒。",
            MessageNotificationAccessStatus.PackageIdentityRequired => "安装版支持系统通知访问；当前仍会尝试来源提醒。",
            _ => "系统通知暂不可用；仍会尝试来源提醒。",
        };
        bool actionable = _messageNotificationSource is not null &&
            status is MessageNotificationAccessStatus.Unspecified or MessageNotificationAccessStatus.Unavailable;
        NotificationAccessButton.IsEnabled = actionable;
        NotificationAccessButton.Visibility = actionable
            ? Visibility.Visible : Visibility.Collapsed;
    }

}
