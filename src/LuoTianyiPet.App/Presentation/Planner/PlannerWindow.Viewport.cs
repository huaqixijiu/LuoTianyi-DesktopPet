using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace LuoTianyiPet.App;

internal sealed partial class PlannerWindow
{
    private string? _viewportMonitor;
    private bool _fittingViewport;
    private string _pageSize="standard";

    internal void SetPageSize(string size)
    {
        _pageSize=size is "mini" or "comfortable" or "fullscreen" ? size : "standard";
        if(new WindowInteropHelper(this).Handle==IntPtr.Zero)FitViewport(SystemParameters.WorkArea.Width,SystemParameters.WorkArea.Height);
        else FitCurrentMonitor();
    }

    private void InitializeViewport()
    {
        FitViewport(SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height);
        System.ComponentModel.PropertyChangedEventHandler workAreaChanged = (_, e) => { if (e.PropertyName == "WorkArea") Dispatcher.BeginInvoke(new Action(FitCurrentMonitor)); };
        SystemParameters.StaticPropertyChanged += workAreaChanged;
        Closed += (_, _) => SystemParameters.StaticPropertyChanged -= workAreaChanged;
        SourceInitialized += (_, _) => FitCurrentMonitor();
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(new Action(FitCurrentMonitor));
        LocationChanged += (_, _) =>
        {
            if (_fittingViewport) return;
            var handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero) return;
            var screen = System.Windows.Forms.Screen.FromHandle(handle);
            if (screen.DeviceName != _viewportMonitor) FitCurrentMonitor();
        };
    }

    private void FitCurrentMonitor()
    {
        if (_fittingViewport) return;
        var screen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle);
        _viewportMonitor = screen.DeviceName;
        var dpi = VisualTreeHelper.GetDpi(this);
        FitViewport(screen.WorkingArea.Width / dpi.DpiScaleX, screen.WorkingArea.Height / dpi.DpiScaleY);
        _fittingViewport = true;
        try
        {
            if(_pageSize=="fullscreen")
            {
                Left=screen.WorkingArea.Left/dpi.DpiScaleX;Top=screen.WorkingArea.Top/dpi.DpiScaleY;
                return;
            }
            Left = Math.Max(screen.WorkingArea.Left / dpi.DpiScaleX, Math.Min(Left, screen.WorkingArea.Right / dpi.DpiScaleX - Width));
            Top = Math.Max(screen.WorkingArea.Top / dpi.DpiScaleY, Math.Min(Top, screen.WorkingArea.Bottom / dpi.DpiScaleY - Height));
        }
        finally { _fittingViewport = false; }
    }

    // Inputs are the monitor's available DIP, never physical pixels. Also used by layout QA.
    internal void FitViewport(double availableWidth, double availableHeight)
    {
        double logicalWidth=1280,logicalHeight=860;
        bool full=_pageSize=="fullscreen";
        double preferred=_pageSize switch {"mini"=>.625,"comfortable"=>.9375,"fullscreen"=>double.PositiveInfinity,_=>.8125};
        double scale=Math.Min(preferred,Math.Min(Math.Max(1,availableWidth-(full?0:24))/logicalWidth,Math.Max(1,availableHeight-(full?0:24))/logicalHeight));
        if(full){logicalWidth=availableWidth/scale;logicalHeight=availableHeight/scale;}
        _shell.Width = logicalWidth;
        _shell.Height = logicalHeight;
        _shell.LayoutTransform = new ScaleTransform(scale, scale);
        MinWidth = MinHeight = 0;
        MaxWidth = MaxHeight = double.PositiveInfinity;
        Width = logicalWidth * scale;
        Height = logicalHeight * scale;
    }
}
