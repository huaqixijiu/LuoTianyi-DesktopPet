using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace LuoTianyiPet.App;

internal sealed partial class PlannerWindow
{
    private string? _viewportMonitor;
    private bool _fittingViewport;
    private bool _viewportFitQueued;
    private bool _viewportClosed;
    private string _pageSize="standard";

    internal void SetPageSize(string size)
    {
        string previousSize=_pageSize;
        _pageSize=size is "mini" or "comfortable" or "fullscreen" ? size : "standard";
        if(new WindowInteropHelper(this).Handle==IntPtr.Zero)FitViewport(SystemParameters.WorkArea.Width,SystemParameters.WorkArea.Height);
        else FitCurrentMonitor();
        // The first render may have happened in the constructor at the default
        // size. Before Show(), FitViewport only resizes; rebuild at the chosen
        // size so compact layouts do not retain the standard-width columns.
        if(!IsLoaded && !_editing && previousSize!=_pageSize)Render();
    }

    private void InitializeViewport()
    {
        FitViewport(SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height);
        System.ComponentModel.PropertyChangedEventHandler workAreaChanged = (_, e) => { if (e.PropertyName == "WorkArea") QueueViewportFit(); };
        SystemParameters.StaticPropertyChanged += workAreaChanged;
        Closed += (_, _) => { _viewportClosed = true; SystemParameters.StaticPropertyChanged -= workAreaChanged; };
        SourceInitialized += (_, _) => FitCurrentMonitor();
        DpiChanged += (_, e) =>
        {
            // Image also raises this routed event when its bitmap DPI is first
            // measured. Rebuilding for a child's event recreates that Image and
            // causes an endless render loop, replacing buttons during a click.
            if (ReferenceEquals(e.OriginalSource, this)) QueueViewportFit();
        };
        LocationChanged += (_, _) =>
        {
            if (_fittingViewport) return;
            var handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero) return;
            var screen = System.Windows.Forms.Screen.FromHandle(handle);
            if (screen.DeviceName != _viewportMonitor) FitCurrentMonitor();
        };
    }

    private void QueueViewportFit()
    {
        if (_viewportClosed || _viewportFitQueued) return;
        _viewportFitQueued = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _viewportFitQueued = false;
            if (!_viewportClosed) FitCurrentMonitor();
        }), DispatcherPriority.Background);
    }

    private void FitCurrentMonitor()
    {
        if (_fittingViewport) return;
        _fittingViewport = true;
        try
        {
            var screen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle);
            _viewportMonitor = screen.DeviceName;
            var dpi = VisualTreeHelper.GetDpi(this);
            FitViewport(screen.WorkingArea.Width / dpi.DpiScaleX, screen.WorkingArea.Height / dpi.DpiScaleY);
            Left = Math.Max(screen.WorkingArea.Left / dpi.DpiScaleX, Math.Min(Left, screen.WorkingArea.Right / dpi.DpiScaleX - Width));
            Top = Math.Max(screen.WorkingArea.Top / dpi.DpiScaleY, Math.Min(Top, screen.WorkingArea.Bottom / dpi.DpiScaleY - Height));
        }
        finally { _fittingViewport = false; }
    }

    // Inputs are the monitor's available DIP, never physical pixels. Also used by layout QA.
    internal void FitViewport(double availableWidth, double availableHeight)
    {
        // The saved "fullscreen" value is kept for compatibility, but its
        // fourth choice is now a roomy desktop tool rather than a screen fill.
        // Width and height fit independently on narrow or short DPI work areas.
        (double desiredWidth,double desiredHeight)=_pageSize switch
        {
            "mini"=>(840,620),
            "comfortable"=>(1020,710),
            "fullscreen"=>(1100,760),
            _=>(940,660)
        };
        double nativeWidth=Math.Min(desiredWidth,Math.Max(1,availableWidth-48));
        double nativeHeight=Math.Min(desiredHeight,Math.Max(1,availableHeight-80));
        bool sizeChanged = Math.Abs(Width - nativeWidth) > .01 || Math.Abs(Height - nativeHeight) > .01;
        // Keep the four window-size presets, but lay out and rasterize WPF text
        // at the final DIP size. Scaling the whole shell softened glyphs and
        // one-pixel rules, especially at the fractional standard preset.
        _shell.LayoutTransform = Transform.Identity;
        _shell.Width = nativeWidth;
        _shell.Height = nativeHeight;
        MinWidth = MinHeight = 0;
        MaxWidth = MaxHeight = double.PositiveInfinity;
        Width = nativeWidth;
        Height = nativeHeight;
        // Repeated monitor notifications with identical bounds must preserve
        // the live controls, their mouse capture, focus and editor state.
        if(sizeChanged&&IsLoaded&&!_editing)Render();
    }
}
