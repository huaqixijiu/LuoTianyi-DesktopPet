using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Button=System.Windows.Controls.Button;

namespace LuoTianyiPet.App;

// A single owned, non-activating surface attached to the pet. The same border,
// header and content viewport survive collapsed/expanded transitions.
internal sealed class PetReminderCard : Window
{
    internal Border Surface { get; }
    internal Button Header { get; }
    private readonly TextBlock _summary=new(){TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center,FontSize=17,FontWeight=FontWeights.SemiBold,Foreground=PlannerTheme.Ink};
    private readonly TextBlock _status=new(){VerticalAlignment=VerticalAlignment.Center,FontSize=14,FontWeight=FontWeights.Medium,Foreground=PlannerTheme.ReminderAccent,Margin=new Thickness(8,0,6,0)};
    private readonly System.Windows.Shapes.Path _arrow=new(){Width=16,Height=16,Stretch=Stretch.Uniform,Stroke=PlannerTheme.Ink,StrokeThickness=2,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Data=Geometry.Parse("M5,3 L11,8 L5,13"),VerticalAlignment=VerticalAlignment.Center};
    private readonly System.Windows.Shapes.Path _bell=new(){Width=25,Height=28,Stretch=Stretch.Uniform,Stroke=PlannerTheme.ReminderAccent,StrokeThickness=1.8,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,Data=Geometry.Parse("M12,3 C8.5,3 6,5.7 6,9.3 L6,16 L4,19 L20,19 L18,16 L18,9.3 C18,5.7 15.5,3 12,3 Z M10,22 Q12,24 14,22 M12,1 L12,3"),Margin=new Thickness(0,0,12,0)};
    private readonly ScrollViewer _viewport=new(){HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,ClipToBounds=true};
    private bool _feedback;
    private readonly Border _scaledContent;
    private int _sizeTier = -1;
    private double _expandedWidth = 250;
    private double _compactWidthCap = 220;
    private double _availableWidth = 410;
    internal double UiScale { get; private set; } = 1;
    private bool _expanded;
    private string _title="",_remaining="";
    internal double TargetHeight { get; private set; }
    internal bool IsExpanded => _expanded;
    internal double ExpandedWidth => Math.Min(Math.Max(1,_availableWidth),_expandedWidth);
    internal event Action? ToggleRequested;
    internal event Action? GeometryChanged;

    internal PetReminderCard(Window owner)
    {
        Owner=owner;Title="天依快速提醒";ShowInTaskbar=false;ShowActivated=false;Topmost=true;
        WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;
        Background=Brushes.Transparent;SizeToContent=SizeToContent.Height;Width=340;
        UseLayoutRounding=true;SnapsToDevicePixels=true;
        FontFamily=new System.Windows.Media.FontFamily("Microsoft YaHei UI");FontSize=14;Foreground=PlannerTheme.Ink;
        PlannerTheme.Apply(this);
        DockPanel headerContent=new(){LastChildFill=true};
        DockPanel.SetDock(_arrow,Dock.Right);headerContent.Children.Add(_arrow);
        DockPanel.SetDock(_status,Dock.Right);headerContent.Children.Add(_status);
        DockPanel.SetDock(_bell,Dock.Left);headerContent.Children.Add(_bell);headerContent.Children.Add(_summary);
        Header=new(){Name="ToggleQuickReminder",Content=headerContent,Height=62,Padding=new Thickness(15,5,15,5),BorderThickness=new Thickness(0),Background=Brushes.White,HorizontalContentAlignment=System.Windows.HorizontalAlignment.Stretch};
        Header.Click+=(_,_)=>ToggleRequested?.Invoke();
        DockPanel content=new();DockPanel.SetDock(Header,Dock.Top);content.Children.Add(Header);content.Children.Add(_viewport);
        content.SizeChanged+=(_,_)=>content.Clip=new RectangleGeometry(new Rect(0,0,content.ActualWidth,content.ActualHeight),15,15);
        Surface=new(){CornerRadius=new CornerRadius(18),Background=Brushes.White,BorderBrush=PlannerTheme.ReminderLine,BorderThickness=new Thickness(1),Child=content,ClipToBounds=true,
            Effect=new DropShadowEffect{BlurRadius=10,ShadowDepth=2,Opacity=.10,Color=Color.FromRgb(41,90,135)}};
        _scaledContent=new Border{Padding=new Thickness(5),Child=Surface,Width=340,SnapsToDevicePixels=true};
        Content=_scaledContent;
        SizeChanged+=(_,_)=>GeometryChanged?.Invoke();
        DpiChanged+=(_,_)=>GeometryChanged?.Invoke();
    }

    internal void Present(string summary,UIElement body,bool quick,bool expanded)
        => Present(summary,"",body,expanded);

    internal void Present(string summary,string status,UIElement body,bool expanded)
    {
        bool initial=!IsVisible||_feedback;
        _feedback=false;_title=summary;_remaining=status;Header.Visibility=Visibility.Visible;
        _bell.Data=Geometry.Parse(status=="时间到了"
            ? "M12,3 C8.5,3 6,5.7 6,9.3 L6,16 L4,19 L20,19 L18,16 L18,9.3 C18,5.7 15.5,3 12,3 Z M10,22 Q12,24 14,22 M12,1 L12,3 M2,7 Q0,11 2,15 M22,7 Q24,11 22,15"
            : "M12,3 C8.5,3 6,5.7 6,9.3 L6,16 L4,19 L20,19 L18,16 L18,9.3 C18,5.7 15.5,3 12,3 Z M10,22 Q12,24 14,22 M12,1 L12,3");
        _viewport.Content=body;SetExpanded(expanded,!initial);
    }

    internal void PresentFeedback(UIElement body)
    {
        _feedback=true;Header.Visibility=Visibility.Collapsed;_viewport.Content=body;
        SetExpanded(true,false);
    }

    internal void SetStatus(string status)
    {
        _remaining=status;UpdateHeaderText();ApplyWidth();
    }

    internal void SetExpanded(bool expanded,bool animate=true)
    {
        _expanded=expanded;
        UpdateHeaderText();
        ApplyWidth();
        UpdateHeaderText();
        Header.ToolTip=expanded?"收起提醒":"展开提醒";
        _viewport.IsHitTestVisible=expanded;
        var body=(UIElement)_viewport.Content;
        body.Measure(new System.Windows.Size(Math.Max(1,_scaledContent.Width-16),double.PositiveInfinity));
        double desired=Math.Min(340*UiScale,body.DesiredSize.Height);
        double height=expanded?desired:0;
        double from=_viewport.ActualHeight;
        _viewport.BeginAnimation(HeightProperty,null);_viewport.Height=height;
        TargetHeight=height+(_feedback?0:Header.Height)+10;
        if(animate&&Math.Abs(from-height)>1)
            _viewport.BeginAnimation(HeightProperty,new DoubleAnimation(from,height,TimeSpan.FromMilliseconds(180)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop});
        GeometryChanged?.Invoke();
    }

    internal void LimitHeight(double available)
    {
        MaxHeight=Math.Max(18,available);
        // Constrain the scroll viewport itself, not only its transparent host;
        // otherwise a large pet can clip the lower buttons without a scrollbar.
        _viewport.MaxHeight=Math.Max(0,MaxHeight-(_feedback?0:Header.Height)-10);
    }

    internal bool ScaleForPet(double percent,double availableWidth)
    {
        // A few deliberate sizes keep this a lightweight desktop notification.
        // Moving the pet scale by 5% within a tier never reflows the card.
        int tier=percent<80?0:percent<125?1:percent<180?2:3;
        bool changed=tier!=_sizeTier||Math.Abs(availableWidth-_availableWidth)>.5;
        if(!changed)return false;
        _sizeTier=tier;
        _availableWidth=availableWidth;
        UiScale=tier switch {0=>.60,1=>.75,2=>.90,_=>1.05};
        _expandedWidth=tier switch {0=>220,1=>250,2=>285,_=>320};
        _compactWidthCap=tier switch {0=>190,1=>220,2=>255,_=>285};
        _scaledContent.Padding=new Thickness(5*UiScale);
        Surface.CornerRadius=new CornerRadius(18*UiScale);
        _summary.FontSize=Math.Max(10,17*UiScale);
        _status.FontSize=Math.Max(10,14*UiScale);
        _bell.Width=25*UiScale;_bell.Height=28*UiScale;_bell.Margin=new Thickness(0,0,12*UiScale,0);
        _arrow.Width=16*UiScale;_arrow.Height=16*UiScale;
        Header.Padding=new Thickness(15*UiScale,5*UiScale,15*UiScale,5*UiScale);
        if(_viewport.Content is UIElement)SetExpanded(_expanded,false);
        else ApplyWidth();
        return true;
    }

    private void ApplyWidth()
    {
        // The compact capsule follows its actual label instead of reserving
        // the expanded card's full width. The detail card stays readable at
        // small pet scales and keeps its two actions on one row.
        _summary.Measure(new System.Windows.Size(double.PositiveInfinity,double.PositiveInfinity));
        _status.Measure(new System.Windows.Size(double.PositiveInfinity,double.PositiveInfinity));
        double compact=_summary.DesiredSize.Width+_status.DesiredSize.Width+_status.Margin.Left+_status.Margin.Right+
            _bell.Width+_bell.Margin.Right+_arrow.Width+Header.Padding.Left+Header.Padding.Right+
            _scaledContent.Padding.Left*2+8*UiScale;
        compact=Math.Min(compact,_compactWidthCap);
        double expanded=ExpandedWidth;
        double desired=_feedback?Math.Max(260,expanded):_expanded?expanded:Math.Max(155,compact);
        double width=Math.Min(Math.Max(1,_availableWidth),desired);
        if(Math.Abs(Width-width)<.5)return;
        Width=width;
        _scaledContent.Width=width;
    }

    private void UpdateHeaderText()
    {
        _summary.Text=_title;
        _status.Text=_expanded||string.IsNullOrEmpty(_remaining)?_remaining:$"· {_remaining}";
        bool narrow=_expanded&&ExpandedWidth<200;
        DockPanel.SetDock(_status,narrow?Dock.Bottom:Dock.Right);
        _status.HorizontalAlignment=narrow?System.Windows.HorizontalAlignment.Right:System.Windows.HorizontalAlignment.Stretch;
        _status.Margin=narrow?new Thickness(0):new Thickness(8,0,6,0);
        _status.Visibility=string.IsNullOrEmpty(_remaining)?Visibility.Collapsed:Visibility.Visible;
        Header.Height=narrow?Math.Max(67,83*UiScale):Math.Max(46,56*UiScale);
        _arrow.Visibility=_expanded?Visibility.Collapsed:Visibility.Visible;
    }
}
