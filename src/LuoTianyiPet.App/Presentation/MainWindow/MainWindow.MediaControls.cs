using System.Windows;
using System.Windows.Input;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    private void InterruptTimeGreetingFromAccessoryInput()
    {
        if (_timeGreetingPresentationInFlight)
        {
            CancelTimeGreetingPresentation(
                restoreContinuousAnimation: true,
                "Interrupted by accessory control input.");
        }
    }

    private void OnPreviousTrackClick(object sender, RoutedEventArgs e)
    {
        PreserveReminderCardDuringTransientInput();
        InterruptTimeGreetingFromAccessoryInput();
        TrySendMediaCommand(MediaCommand.PreviousTrack);
    }

    private void OnTogglePlayPauseClick(object sender, RoutedEventArgs e)
    {
        PreserveReminderCardDuringTransientInput();
        InterruptTimeGreetingFromAccessoryInput();
        HandleTogglePlayPauseRequest();
    }

    private void OnNextTrackClick(object sender, RoutedEventArgs e)
    {
        PreserveReminderCardDuringTransientInput();
        InterruptTimeGreetingFromAccessoryInput();
        TrySendMediaCommand(MediaCommand.NextTrack);
    }

    private void OnCloudMusicVolumeClick(object sender, RoutedEventArgs e)
    {
        PreserveReminderCardDuringTransientInput();
        InterruptTimeGreetingFromAccessoryInput();
        if (_bunChaseActive)
        {
            HideAccessorySurfacesForBunChase();
            return;
        }

        DateTimeOffset now = DateTimeOffset.Now;
        if (CloudMusicVolumePopup.IsOpen)
        {
            CloudMusicVolumePopup.IsOpen = false;
            return;
        }

        if (_cloudMusicVolumePopupClosedAt is DateTimeOffset closedAt &&
            now - closedAt <= VolumePopupSameClickSuppression)
        {
            return;
        }

        _mediaControlsHideTimer.Stop();
        RefreshCloudMusicVolumeControl();
        CloudMusicVolumePopup.IsOpen = true;
    }

    private void OnCloudMusicVolumeSliderValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingCloudMusicVolumeSlider || _applicationVolumeService is null)
        {
            return;
        }

        ApplicationVolumeAdjustmentResult result =
            _applicationVolumeService.TrySetLevel((float)(e.NewValue / 100));
        if (result.Status is ApplicationVolumeAdjustmentStatus.Succeeded or
            ApplicationVolumeAdjustmentStatus.AtLimit)
        {
            UpdateCloudMusicVolumeControl(result.Snapshot);
            return;
        }

        string message = result.Status switch
        {
            ApplicationVolumeAdjustmentStatus.TargetSessionMissing =>
                "请先让网易云播放一首歌，再调节独立音量",
            ApplicationVolumeAdjustmentStatus.ProtectedApplicationForeground =>
                "游戏安全模式：这次没有调整网易云音量",
            ApplicationVolumeAdjustmentStatus.ForegroundCheckUnavailable =>
                "暂时无法确认前台程序，没有调整音量",
            ApplicationVolumeAdjustmentStatus.SessionUnavailable =>
                "Windows 音频服务暂时不可用",
            _ => "Windows 没有接受这次音量调整",
        };
        ShowFeedbackBubble(message);
        RefreshCloudMusicVolumeControl(message);
    }

    private void OnCloudMusicVolumeSliderPreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (!CloudMusicVolumeSlider.IsEnabled)
        {
            return;
        }

        PreserveReminderCardDuringTransientInput();
        _isCloudMusicVolumeTrackDragging = CloudMusicVolumeDragSurface.CaptureMouse();
        UpdateCloudMusicVolumeFromPointer(e.GetPosition(CloudMusicVolumeSlider));
        e.Handled = true;
    }

    private void OnCloudMusicVolumeSliderPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isCloudMusicVolumeTrackDragging)
        {
            return;
        }

        PreserveReminderCardDuringTransientInput();
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            StopCloudMusicVolumeTrackDrag();
            return;
        }

        UpdateCloudMusicVolumeFromPointer(e.GetPosition(CloudMusicVolumeSlider));
        e.Handled = true;
    }

    private void OnCloudMusicVolumeSliderPreviewMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (!_isCloudMusicVolumeTrackDragging)
        {
            return;
        }

        UpdateCloudMusicVolumeFromPointer(e.GetPosition(CloudMusicVolumeSlider));
        StopCloudMusicVolumeTrackDrag();
        e.Handled = true;
    }

    private void StopCloudMusicVolumeTrackDrag()
    {
        _isCloudMusicVolumeTrackDragging = false;
        if (Mouse.Captured == CloudMusicVolumeDragSurface)
        {
            CloudMusicVolumeDragSurface.ReleaseMouseCapture();
        }
    }

    private void UpdateCloudMusicVolumeFromPointer(Point pointer)
    {
        const double trackPadding = 9;
        double value = VerticalRangeMapper.Resolve(
            pointer.Y,
            CloudMusicVolumeSlider.ActualHeight,
            trackPadding,
            CloudMusicVolumeSlider.Minimum,
            CloudMusicVolumeSlider.Maximum);
        CloudMusicVolumeSlider.Value = Math.Round(value);
    }

    private void OnCloudMusicVolumePopupClosed(object? sender, EventArgs e)
    {
        _cloudMusicVolumePopupClosedAt = DateTimeOffset.Now;
        StopCloudMusicVolumeTrackDrag();
        if (!_previewMediaControls && !IsMouseOver)
        {
            _mediaControlsHideTimer.Stop();
            _mediaControlsHideTimer.Start();
        }
    }

    private void RefreshCloudMusicVolumeControl(string? overrideStatus = null)
    {
        ApplicationVolumeSnapshot snapshot =
            _applicationVolumeService?.Read() ?? ApplicationVolumeSnapshot.Unavailable;
        UpdateCloudMusicVolumeControl(snapshot, overrideStatus);
    }

    private void UpdateCloudMusicVolumeControl(
        ApplicationVolumeSnapshot snapshot,
        string? overrideStatus = null)
    {
        _updatingCloudMusicVolumeSlider = true;
        CloudMusicVolumeSlider.IsEnabled = snapshot.IsAvailable;
        CloudMusicVolumeValueText.Text = snapshot.IsAvailable
            ? $"{snapshot.Percentage}%"
            : "--%";
        if (snapshot.IsAvailable)
        {
            CloudMusicVolumeSlider.Value = snapshot.Percentage;
        }
        _updatingCloudMusicVolumeSlider = false;

        CloudMusicVolumeButton.ToolTip = overrideStatus ?? (snapshot switch
        {
            { IsAvailable: true, SessionCount: > 1 } =>
                $"网易云独立音量 · {snapshot.Percentage}% · {snapshot.SessionCount} 个会话",
            { IsAvailable: true } => $"网易云独立音量 · {snapshot.Percentage}%",
            { ProbeSucceeded: true } => "请先让网易云播放一首歌",
            _ => "Windows 音频服务暂时不可用",
        });
    }
}
