using System.Diagnostics;
using System.IO;
using LuoTianyiPet.Animation;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    private void PlayAnimation(
        string animationId,
        Action? completed = null,
        bool preserveVisualTransition = false,
        bool reverse = false,
        double playbackRate = 1.0)
    {
        _bodyReactionMotion.Cancel();
        if (!preserveVisualTransition) CancelVisualTransition();
        if (_animationPlayer is null || _animationCatalog is null)
        {
            ShowFallback("Animation catalog unavailable.");
            return;
        }
        try
        {
            if (animationId is FileDropPromptAnimation or FileDropSuccessAnimation)
            {
                ApplyBodyReactionMirror(false);
                SetEdgeMirror(false);
            }
            AnimationAssetManifest manifest = _animationPlayer.Play(
                animationId, completed, reverse, playbackRate);
            if (animationId == PetVisualState.MediumIdleCountdownAnimation &&
                _fishingCountdownStartedTimestamp is null)
            {
                _fishingCountdownStartedTimestamp = Stopwatch.GetTimestamp();
            }
            ApplyAnimationManifest(manifest);
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or ArgumentException or
            KeyNotFoundException or NotSupportedException)
        {
            _logger.Error("animation.play_failed", exception);
            ShowFallback("Animation playback failed.");
        }
    }

    private void PlayAnimationRange(
        string animationId,
        int startFrameIndex,
        int endFrameIndex,
        Action? completed = null,
        double playbackRate = 1.0)
    {
        _bodyReactionMotion.Cancel();
        CancelVisualTransition();
        if (_animationPlayer is null || _animationCatalog is null)
        {
            ShowFallback("Animation catalog unavailable.");
            return;
        }
        try
        {
            AnimationAssetManifest manifest = _animationPlayer.PlayRange(
                animationId, startFrameIndex, endFrameIndex, completed, playbackRate);
            ApplyAnimationManifest(manifest);
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or ArgumentException or
            KeyNotFoundException or NotSupportedException)
        {
            _logger.Error("animation.range_play_failed", exception);
            ShowFallback("Animation range playback failed.");
        }
    }

    private void ShowAnimationFrame(string animationId, int frameIndex)
    {
        _bodyReactionMotion.Cancel();
        CancelVisualTransition();
        if (_animationPlayer is null || _animationCatalog is null)
        {
            ShowFallback("Animation catalog unavailable.");
            return;
        }
        try
        {
            AnimationAssetManifest manifest = _animationPlayer.ShowFrame(animationId, frameIndex);
            ApplyAnimationManifest(manifest);
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or ArgumentException or
            KeyNotFoundException or NotSupportedException)
        {
            _logger.Error("animation.frame_show_failed", exception);
            ShowFallback("Animation frame could not be shown.");
        }
    }

    private void ApplyAnimationManifest(AnimationAssetManifest manifest)
    {
        double displayScale = _settings.Appearance.DisplayScalePercent / 100.0;
        double displayWidth = manifest.DisplayWidth * displayScale;
        double displayHeight = manifest.DisplayHeight * displayScale;
        bool preserveWindowBounds = _isWindowDragging && _classicDragExpansionStarted;
        PetImage.Width = displayWidth;
        PetImage.Height = displayHeight;
        PetImage.Visibility = System.Windows.Visibility.Visible;
        PetImage.IsHitTestVisible = true;
        FallbackSurface.Visibility = System.Windows.Visibility.Collapsed;
        if (!preserveWindowBounds) ResizeAnimationStage();
        UpdateBodyHitDebugOverlay();
    }

    private void ShowFallback(string logMessage)
    {
        _animationPlayer?.Stop();
        PetImage.Visibility = System.Windows.Visibility.Collapsed;
        FallbackSurface.Visibility = System.Windows.Visibility.Visible;
        BodyHitDebugOverlay.Visibility = System.Windows.Visibility.Collapsed;
        ResizeAnimationStage();
        _logger.Info("animation.fallback_shown", logMessage);
    }

    private void ResizeAnimationStage()
    {
        AnimationStageSizing stage = GetAnimationStageSizing();
        double width = stage.Width;
        double height = stage.Height;
        ApplyAccessorySizing(stage.Accessories);
        if (Math.Abs(Width - width) < 0.01 && Math.Abs(Height - height) < 0.01) return;
        DesktopRectangle workArea = GetCurrentWorkArea();
        double oldWidth = ActualWidth > 0 ? ActualWidth : Width;
        double oldHeight = ActualHeight > 0 ? ActualHeight : Height;
        double center = Left + oldWidth / 2;
        double bottom = Top + oldHeight;
        Width = width;
        Height = height;
        Left = Clamp(center - width / 2, workArea.Left - 8, workArea.Right - width + 8);
        double minimumTop = workArea.Top - PetVisual.Margin.Top;
        Top = Clamp(bottom - height, minimumTop, workArea.Bottom - height + PetVisual.Margin.Bottom);
        UpdateFeedbackLayout();
    }

    private void ApplyAccessorySizing(AccessorySizing sizing)
    {
        MediaControlsLayoutScale.ScaleX = sizing.MusicIslandScale;
        MediaControlsLayoutScale.ScaleY = sizing.MusicIslandScale;
    }
}
