using LuoTianyiPet.Core;
using LuoTianyiPet.Platform.Windows;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    private void StartMessageNotificationMonitoring()
    {
        if (!_settings.Notifications.EnableMessageReminders)
        {
            return;
        }

        _messageNotificationStatusTimer.Start();
        UpdateWeChatMonitoring();
        if (_messageNotificationSource is null) return;
        if (!_messageNotificationSubscribed)
        {
            _messageNotificationSource.NotificationReceived += OnMessageNotificationReceived;
            _messageNotificationSubscribed = true;
        }
        (_messageNotificationSource as IMessageNotificationDetailSettings)?.SetQqDetailsEnabled(
            _settings.Notifications.EnableQqDetailedReminders);
        (_messageNotificationSource as IMessageNotificationDetailSettings)?.SetWeChatDetailsEnabled(
            _settings.Notifications.EnableWeChatDetailedReminders);
        _messageNotificationSource.Start();
        _messageNotificationStatusTimer.Start();
        _logger.Info(
            "notification.monitor_status",
            _messageNotificationSource.GetAccessStatus().ToString());
    }

    private void OnMessageNotificationReceived(
        object? sender,
        MessageNotificationReceivedEventArgs e) =>
        Dispatcher.BeginInvoke(() => HandleMessageNotification(e));

    private void OnShellWindowAttentionRequested(
        object? sender,
        ShellWindowAttentionEventArgs e)
    {
        if (_isClosing || !_settings.Notifications.EnableMessageReminders ||
            !WindowsWindowProcessResolver.TryGetProcessName(
                e.WindowHandle,
                out string? processName))
        {
            return;
        }

        MessageProvider? provider = _messageProviderMatcher.IdentifyProcess(processName);
        if (provider is not MessageProvider matched)
        {
            return;
        }

        DateTimeOffset occurredAt = DateTimeOffset.Now;
        long providerSessionKey = (long)matched + 1;
        if (!_shellAttentionSessions.ShouldNotify(providerSessionKey, occurredAt))
        {
            return;
        }

        _logger.Info("notification.shell_attention_detected", matched.ToString());
        if (matched == MessageProvider.WeChat && _settings.Notifications.EnableWeChatDetailedReminders)
        {
            _ = HandleWeChatAttentionAsync(occurredAt);
            return;
        }
        HandleMessageNotification(
            new MessageNotificationReceivedEventArgs(
                new MessageNotificationSummary(matched, occurredAt)));
    }

    private void HandleMessageNotification(MessageNotificationReceivedEventArgs e)
    {
        if (_isClosing || !_settings.Notifications.EnableMessageReminders || !CanPresentWeChatReminder(e.Notification))
        {
            return;
        }

        ForegroundApplicationSnapshot foreground = _foregroundApplicationProbe?.Query() ??
            new ForegroundApplicationSnapshot(false, null, false);
        bool sourceIsForeground = foreground.Succeeded &&
            _messageProviderMatcher.IsForegroundProcess(e.Provider, foreground.ProcessName);
        if (e.Provider == MessageProvider.WeChat)
        {
            if (sourceIsForeground) _lastWeChatForegroundAt = DateTimeOffset.Now;
            if (e.Notification.ConversationDisplayName is not null) _weChatDetailRevision++;
        }
        if (sourceIsForeground) return;
        MessageNotificationSummary notification = e.Notification.ForDisplay(
            _settings.Notifications.EnableQqDetailedReminders,
            _settings.Notifications.EnableWeChatDetailedReminders);
        _inboxSafe = IsInboxDisplaySafe(foreground);
        if (_messageInbox.Add(notification)) RefreshMessageInbox();
    }

    private void OnMessageNotificationStatusTimerTick(object? sender, EventArgs e)
    {
        if (_isClosing || _foregroundApplicationProbe is null)
        {
            return;
        }

        UpdateWeChatMonitoring();
        _messageNotificationSource?.Start();

        ForegroundApplicationSnapshot foreground = _foregroundApplicationProbe.Query();
        if (_messageProviderMatcher.IdentifyProcess(foreground.ProcessName) is MessageProvider foregroundProvider)
        {
            _shellAttentionSessions.Reset();
            if (foregroundProvider == MessageProvider.WeChat) _lastWeChatForegroundAt = DateTimeOffset.Now;
        }
        _inboxSafe = IsInboxDisplaySafe(foreground);
        PruneMessageInbox();
    }

    private bool IsMessageNotificationDisplaySafe(ForegroundApplicationSnapshot foreground)
    {
        bool hasAvailablePresentationSlot =
            MessageNotificationPresentationPolicy.HasAvailablePresentationSlot(
                _stateMachine.Resolve(DateTimeOffset.Now).Source,
                _stateMachine.ActiveReactionToken,
                _messageNotificationReactionToken);
        return !_hiddenByUser && foreground.Succeeded &&
            !foreground.IsFullscreen &&
            !_systemSessionUnavailable &&
            _edgeDockSide == EdgeDockSide.None &&
            !_isWindowDragging &&
            _stateMachine.CurrentContinuousState is not
                (PetContinuousState.Sleeping or PetContinuousState.HiddenForSafety) &&
            hasAvailablePresentationSlot;
    }

    private Task BeginMessageNotificationAsync(MessageNotificationSummary notification)
    {
        if (!CanPresentWeChatReminder(notification)) return Task.CompletedTask;
        _messageInbox.Add(notification.ForDisplay(
            _settings.Notifications.EnableQqDetailedReminders,
            _settings.Notifications.EnableWeChatDetailedReminders));
        if (!_persistSettings) _inboxSafe = true;
        RefreshMessageInbox();
        return Task.CompletedTask;
    }

    private async Task BeginMessageNotificationPreviewAsync(MessageProvider provider)
    {
        await Task.Delay(700);
        if (!_isClosing)
        {
            await BeginMessageNotificationAsync(new MessageNotificationSummary(
                provider,
                DateTimeOffset.Now,
                "测试联系人"));
        }
    }

    private void ApplyMessageNotificationPreferences(MessageNotificationPreferences preferences)
    {
        bool wasEnabled = _settings.Notifications.EnableMessageReminders;
        _settings = _settings with { Notifications = preferences };
        (_messageNotificationSource as IMessageNotificationDetailSettings)?.SetQqDetailsEnabled(
            preferences.EnableQqDetailedReminders);
        (_messageNotificationSource as IMessageNotificationDetailSettings)?.SetWeChatDetailsEnabled(
            preferences.EnableWeChatDetailedReminders);
        if (!preferences.EnableMessageReminders) _messageNotificationCounter.Clear();
        if (!preferences.EnableQqDetailedReminders) _messageNotificationCounter.Reset(MessageProvider.Qq);
        if (!preferences.EnableWeChatDetailedReminders) _messageNotificationCounter.Reset(MessageProvider.WeChat);
        UpdateWeChatMonitoring();
        if (!preferences.EnableQqDetailedReminders) _messageInbox.HideDetails(MessageProvider.Qq);
        if (!preferences.EnableWeChatDetailedReminders) _messageInbox.HideDetails(MessageProvider.WeChat);
        if (!preferences.EnableMessageReminders)
        {
            _messageInbox.Clear(MessageProvider.Qq, DateTimeOffset.Now);
            _messageInbox.Clear(MessageProvider.WeChat, DateTimeOffset.Now);
        }
        RefreshMessageInbox();
        _messageNotificationCoordinator.ClearPending();
        if (_displayedMessageSummary is not null) ShowMessageNotification(_displayedMessageSummary);
        if (preferences.EnableMessageReminders)
        {
            StartMessageNotificationMonitoring();
        }
        else
        {
            _messageNotificationStatusTimer.Stop();
            _messageNotificationCoordinator.ClearPending();
            CancelMessageNotificationPresentation(restoreContinuousAnimation: true);
            _messageNotificationSource?.Stop();
        }

        if (wasEnabled != preferences.EnableMessageReminders)
        {
            _logger.Info(
                "notification.preferences_applied",
                preferences.EnableMessageReminders ? "Enabled." : "Disabled.");
        }
    }
}
