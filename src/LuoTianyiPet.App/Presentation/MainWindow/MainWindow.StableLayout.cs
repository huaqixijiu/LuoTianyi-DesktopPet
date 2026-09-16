using LuoTianyiPet.Core;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    private double GetEdgeAccessoryReservation()
    {
        if (_feedbackSlotHeight <= 0.1)
        {
            return MediaControlsReservedHeight;
        }

        return Math.Max(
            MediaControlsReservedHeight,
            FeedbackBubbleAnchorOffset + _feedbackSlotHeight);
    }

    private double GetAccessoryLayoutStageHeight(AccessoryLayout layout)
    {
        AnimationStageSizing standard = GetAnimationStageSizing();
        if (layout == AccessoryLayout.Split)
        {
            return standard.Height;
        }

        // The standard stage reserves one 86-DIP music island row below the
        // artwork. For an edge layout the same row moves above the artwork;
        // do not keep the old split-layout padding as an invisible gap.
        double artworkHeight = (_animationCatalog?.Assets.Max(asset => asset.DisplayHeight) ?? 238) *
            (_settings.Appearance.DisplayScalePercent / 100.0);
        // The pet grid keeps an 8-DIP inset on the opposite side of the
        // window, so include that inset in the edge-stage height as well.
        return artworkHeight + GetEdgeAccessoryReservation() + 8;
    }

    private AnimationStageSizing GetAnimationStageSizing()
    {
        AnimationStageSizing stage = AnimationStageSizing.Resolve(
            _animationCatalog?.Assets.Max(asset => asset.DisplayWidth) ?? 244,
            _animationCatalog?.Assets.Max(asset => asset.DisplayHeight) ?? 238,
            _settings.Appearance.DisplayScalePercent);
        return stage with { Height = stage.Height + _feedbackSlotHeight };
    }

    // Deliberately excludes per-frame alpha and animated render transforms.
    // Those remain authoritative for mouse hits and explicit edge docking only.
    private DesktopRectangle GetStableStageBoundsInWindow() => new(
        8, PetVisual.Margin.Top, Width - 16,
        Height - PetVisual.Margin.Top - PetVisual.Margin.Bottom);

    private DesktopRectangle GetStableStageDesktopBounds()
    {
        DesktopRectangle stage = GetStableStageBoundsInWindow();
        return new DesktopRectangle(Left + stage.Left, Top + stage.Top, stage.Width, stage.Height);
    }
}
