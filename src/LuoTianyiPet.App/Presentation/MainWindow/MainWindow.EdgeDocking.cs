using System.Windows;
using System.Windows.Media;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    private void SetEdgeMirror(bool mirrored)
    {
        PetImage.RenderTransformOrigin = new Point(0.5, 0.5);
        PetImage.RenderTransform = mirrored ? new ScaleTransform(-1, 1) : Transform.Identity;
    }

    private static string GetEdgeDockAnimation(EdgeDockSide side) => side switch
    {
        EdgeDockSide.Left or EdgeDockSide.Right => "twelfth-anniversary-peek",
        EdgeDockSide.Bottom => "twelfth-anniversary-entrance",
        _ => throw new ArgumentOutOfRangeException(nameof(side)),
    };

    private static (int HiddenFrame, int HideStartFrame, int RevealEndFrame) GetEdgeDockFrames(
        EdgeDockSide side) => side switch
    {
        EdgeDockSide.Left or EdgeDockSide.Right =>
            (SideDockHiddenFrame, SideDockHideStartFrame, SideDockRevealEndFrame),
        EdgeDockSide.Bottom =>
            (BottomDockHiddenFrame, BottomDockHideStartFrame, BottomDockRevealEndFrame),
        _ => throw new ArgumentOutOfRangeException(nameof(side)),
    };

    private static double GetEdgeDockPlaybackRate(EdgeDockSide side, bool revealed) =>
        (side, revealed) switch
        {
            (EdgeDockSide.Left or EdgeDockSide.Right, true) => SideDockRevealPlaybackRate,
            (EdgeDockSide.Bottom, false) => BottomDockHidePlaybackRate,
            _ => 1.0,
        };
}
