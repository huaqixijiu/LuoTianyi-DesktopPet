using System.Windows;
using FontFamily = System.Windows.Media.FontFamily;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using LuoTianyiPet.Core;
using Button = System.Windows.Controls.Button;
using Size = System.Windows.Size;
namespace LuoTianyiPet.App;
internal sealed class DateSelectionWindow : Window
{
    public HashSet<DateTime> Selection { get; }
    private DateTime _month;
    private readonly StackPanel _panel = new() { Margin = new Thickness(10) };
    private readonly Border _shell;
    private readonly Dictionary<DateTime, Button> _dayButtons = [];
    private TextBlock? _selectionSummary;
    private WrapPanel? _selectedChips;
    private ScrollViewer? _selectedScroll;
    private StackPanel? _selectedArea;
    private Button? _collapseDates;
    private Button? _clearDates;
    private Button? _clearExpandedDates;
    private bool _expanded;
    private bool _adjustingLayout;
    private double _lastContainerWidth;
    private bool _dragging;
    private bool _dragMoved;
    private Point _dragLastPoint;
    private DateTime? _dragLastDay;
    private Vector? _dragLastDirection;
    private bool _suppressNextClick;
    private bool _compact;
    public DateSelectionWindow(IEnumerable<DateTime> dates, DateTime month, bool seedWhenEmpty=true)
    {
        Selection = new(dates.Select(d => d.Date)); if (seedWhenEmpty && Selection.Count == 0) Selection.Add(DateTime.Today); _month = new(month.Year, month.Month, 1);
        Title = "选择日期"; Width = 430; FontSize = 14; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        FontFamily = new FontFamily("Microsoft YaHei UI");
        _shell = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(14), BorderBrush = PlannerTheme.Line, BorderThickness = new Thickness(1), Padding = new Thickness(14), Margin = new Thickness(8) };
        _shell.Child = _panel;
        _shell.MouseLeftButtonDown += (_, e) => { if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) DragMove(); };
        Content = _shell;
        PlannerTheme.Apply(this); Render();
        Loaded += (_, _) =>
        {
            _compact=Owner?.ActualHeight<500;
            if(_compact){_shell.Padding=new Thickness(8);_shell.Margin=new Thickness(4);_panel.Margin=new Thickness(6);Render();}
            MaxHeight=SafeModalHeight();UpdateSelectionVisuals();
        };
    }
    private Button Make(string text, Action click) { Button b = new() { Content = text, Margin = new Thickness(2), Padding = new Thickness(7) }; b.Click += (_, _) => click(); return b; }
    private Button NavButton(string kind, Action click, string tip) { Button b = new() { Style = (Style)FindResource("PlannerIconBtn"), Content = PlannerTheme.Icon(kind, 16, PlannerTheme.Ink, 0), ToolTip = tip }; b.Click += (_, _) => click(); return b; }
    private Button FooterChip(string text, Action click, bool primary) { Button b = new() { Content = text, Style = (Style)FindResource("PlannerChip"), Margin = new Thickness(6, 0, 0, 0), Height = 34 }; b.Click += (_, _) => click(); if (primary) { b.Background = PlannerTheme.PrimaryFill; b.Foreground = Brushes.White; b.BorderThickness = new Thickness(0); b.FontWeight = FontWeights.SemiBold; } return b; }
    private Button Link(string text,Action click,string name){Button b=new(){Name=name,Content=text,Style=(Style)FindResource("PlannerLink"),FontSize=12,Foreground=PlannerTheme.Accent,Margin=new Thickness(4,0,0,0)};b.Click+=(_,_)=>click();return b;}
    private static ResourceDictionary LightScrollbars()=>(ResourceDictionary)XamlReader.Parse("""
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
 <Style TargetType="ScrollBar"><Setter Property="Width" Value="6"/><Setter Property="Background" Value="Transparent"/>
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ScrollBar"><Grid Width="6" Background="Transparent">
   <Track x:Name="PART_Track" IsDirectionReversed="True" Focusable="False">
    <Track.DecreaseRepeatButton><RepeatButton Command="ScrollBar.PageUpCommand" Opacity="0"/></Track.DecreaseRepeatButton>
    <Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType="Thumb"><Border x:Name="ThumbSurface" Width="4" Background="#C7D9E5" CornerRadius="2" HorizontalAlignment="Center"/><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="ThumbSurface" Property="Background" Value="#8EADC1"/></Trigger><Trigger Property="IsDragging" Value="True"><Setter TargetName="ThumbSurface" Property="Background" Value="#6D96AF"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
    <Track.IncreaseRepeatButton><RepeatButton Command="ScrollBar.PageDownCommand" Opacity="0"/></Track.IncreaseRepeatButton>
   </Track></Grid></ControlTemplate></Setter.Value></Setter>
 </Style>
</ResourceDictionary>
""");
    private double SafeModalHeight()
    {
        // Ordinary selections still grow naturally. Extreme selections reserve
        // roughly three compact chip rows so the picker stays inside Planner.
        double reserve=Selection.Count>=30?(_compact?100:130):(_compact?24:80);
        double ownerHeight=Owner?.ActualHeight>0?Owner.ActualHeight-reserve:Selection.Count>=30?690:740;
        double ownerCap=Owner?.ActualHeight>0?Owner.ActualHeight-32:double.PositiveInfinity;
        return Math.Max(1,Math.Min(Math.Max(_compact?280:360,ownerHeight),Math.Min(ownerCap,SystemParameters.WorkArea.Height-24)));
    }
    private void Render()
    {
        _dragging = false;
        _dragMoved = false;
        _dragLastDay = null;
        _dragLastDirection = null;
        _dayButtons.Clear();
        System.Windows.Input.Mouse.Capture(null);
        _panel.Children.Clear();
        DockPanel title = new() { Margin = new Thickness(2, 0, 2, _compact?2:8) }; var close = NavButton("close", () => DialogResult = false, "关闭"); DockPanel.SetDock(close, Dock.Right); title.Children.Add(close); title.Children.Add(new TextBlock { Text = "选择日期", FontSize = _compact?18:20, FontWeight = FontWeights.SemiBold, Foreground = PlannerTheme.Ink, VerticalAlignment = VerticalAlignment.Center }); _panel.Children.Add(title);
        DockPanel nav = new() { Margin = new Thickness(2, 2, 2, _compact?2:6) }; var prev = NavButton("chevron-left", () => Move(-1), "上个月");prev.Name="PreviousDateMonth"; var next = NavButton("chevron-right", () => Move(1), "下个月");next.Name="NextDateMonth";
        DockPanel.SetDock(prev, Dock.Left); DockPanel.SetDock(next, Dock.Right); nav.Children.Add(prev); nav.Children.Add(next);
        nav.Children.Add(new TextBlock { Text = _month.ToString("yyyy年M月"), TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = PlannerTheme.Ink }); _panel.Children.Add(nav);
        int weeks = (((int)_month.DayOfWeek + 6) % 7 + DateTime.DaysInMonth(_month.Year,_month.Month) + 6) / 7;
        Grid grid = new(); for (int c = 0; c < 7; c++) grid.ColumnDefinitions.Add(new()); for (int r = 0; r < weeks+1; r++) grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        grid.AddHandler(UIElement.PreviewMouseMoveEvent, new System.Windows.Input.MouseEventHandler((_, e) => ContinueDrag(grid, e)), true);
        grid.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, new System.Windows.Input.MouseButtonEventHandler((_, e) => EndDrag(e)), true);
        for (int c = 0; c < 7; c++) { TextBlock t = new() { Text = "一二三四五六日"[c].ToString(), TextAlignment = TextAlignment.Center, Margin = _compact?new Thickness(4,3,4,3):new Thickness(4,10,4,8), FontSize = 12.5, Foreground = PlannerTheme.Muted }; Grid.SetColumn(t,c); grid.Children.Add(t); }
        DateTime start = _month.AddDays(-((int)_month.DayOfWeek + 6) % 7);
        for (int i = 0; i < weeks*7; i++)
        {
            DateTime day = start.AddDays(i); Button b = Make(day.Day.ToString(), () => { if (_suppressNextClick) { _suppressNextClick = false; return; } Toggle(day); });
            b.Name = "Date" + day.ToString("yyyyMMdd"); b.ToolTip = day.ToString("yyyy年M月d日") + " " + CalendarLabels.FullLunar(day);
            b.Width=48;b.Height=_compact?(weeks>=6?20:22):42;b.Margin=new Thickness(_compact?0:2);b.Padding=new Thickness(2);b.FontSize=_compact?13:14;b.BorderThickness=new Thickness(0);
            b.IsEnabled = day >= ReminderSchedule.MinimumDate && day <= ReminderSchedule.MaximumDate;
            b.Tag = day;
            if (day == DateTime.Today)
            {
                StackPanel todayLabel=new(){VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=System.Windows.HorizontalAlignment.Center};
                todayLabel.Children.Add(new TextBlock{Text=day.Day.ToString(),TextAlignment=TextAlignment.Center,FontSize=14});
                b.Content=todayLabel;
            }
            b.MouseEnter+=(_,_)=>{if(!Selection.Contains(day))b.Background=PlannerTheme.HoverBackground;};
            b.MouseLeave+=(_,_)=>ApplyDayVisual(day,b);
            b.PreviewMouseLeftButtonDown += (_, e) => BeginDrag(b, grid, day, e);
            _dayButtons[day] = b;
            Grid.SetColumn(b,i%7); Grid.SetRow(b,i/7+1); grid.Children.Add(b);
        }
        _panel.Children.Add(grid);
        _selectedArea=new StackPanel{Margin=new Thickness(2,_compact?0:12,2,2)};
        _selectedArea.Children.Add(new Border{Height=1,Background=PlannerTheme.Line,Margin=new Thickness(2,0,2,_compact?4:10)});
        DockPanel selectedHeader=new(){Name="SelectedDateHeader",Margin=new Thickness(2,0,2,_compact?2:6)};
        _selectionSummary=TextBlockSummary();_selectionSummary.VerticalAlignment=VerticalAlignment.Center;selectedHeader.Children.Add(_selectionSummary);
        var headerActions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=System.Windows.HorizontalAlignment.Right};DockPanel.SetDock(headerActions,Dock.Right);selectedHeader.Children.Insert(0,headerActions);
        _collapseDates=Link("收起⌃",()=>{_expanded=false;UpdateSelectionVisuals();},"CollapseDates");_collapseDates.Visibility=_expanded?Visibility.Visible:Visibility.Collapsed;headerActions.Children.Add(_collapseDates);
        _clearDates=Link("清空选择",()=>{Selection.Clear();UpdateSelectionVisuals();},"ClearDates");_clearDates.Visibility=_expanded?Visibility.Collapsed:Visibility.Visible;headerActions.Children.Add(_clearDates);
        _selectedArea.Children.Add(selectedHeader);
        _selectedChips=new WrapPanel{Margin=new Thickness(0)};
        _selectedScroll=new ScrollViewer{Name="SelectedDateScroll",Content=_selectedChips,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        _selectedScroll.Resources.MergedDictionaries.Add(LightScrollbars());_selectedArea.Children.Add(_selectedScroll);
        _clearExpandedDates=Link("清空选择",()=>{Selection.Clear();UpdateSelectionVisuals();},"ClearExpandedDates");_clearExpandedDates.HorizontalAlignment=System.Windows.HorizontalAlignment.Right;_clearExpandedDates.Margin=new Thickness(0,6,2,0);_clearExpandedDates.Visibility=_expanded?Visibility.Visible:Visibility.Collapsed;_selectedArea.Children.Add(_clearExpandedDates);
        _selectedScroll.SizeChanged+=(_,e)=>{if(e.NewSize.Width>0&&Math.Abs(e.NewSize.Width-_lastContainerWidth)>1&&!_adjustingLayout){_lastContainerWidth=e.NewSize.Width;UpdateSelectionVisuals();}};
        _panel.Children.Add(_selectedArea);
        DockPanel footer = new() { Margin = new Thickness(2, _compact?2:12, 2, 2) };
        StackPanel right = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        var cancel=FooterChip("取消", () => DialogResult = false, false);cancel.Name="CancelDates";right.Children.Add(cancel); var ok = FooterChip("确定", () => DialogResult = true, true); ok.Name = "ConfirmDates"; right.Children.Add(ok);
        DockPanel.SetDock(right, Dock.Right); footer.Children.Add(right); _panel.Children.Add(footer);
        UpdateSelectionVisuals();
    }
    private TextBlock TextBlockSummary()=>new(){FontSize=14,FontWeight=FontWeights.SemiBold,Foreground=PlannerTheme.Ink,Margin=new Thickness(2,0,0,0)};
    private void Move(int delta) { DateTime next = _month.AddMonths(delta); if(next >= ReminderSchedule.MinimumDate && next <= ReminderSchedule.MaximumDate) { _month=next; Render(); } }

    private void Toggle(DateTime day)
    {
        if (!Selection.Add(day)) Selection.Remove(day);
        UpdateSelectionVisuals();
    }

    private void BeginDrag(Button button, Grid grid, DateTime day, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton != System.Windows.Input.MouseButton.Left || !button.IsEnabled) return;
        _dragging = true;
        _dragMoved = false;
        _dragLastPoint = System.Windows.Input.Mouse.GetPosition(grid);
        _dragLastDay = null;
        _dragLastDirection = null;
        System.Windows.Input.Mouse.Capture(button, System.Windows.Input.CaptureMode.Element);
    }

    private void ContinueDrag(Grid grid, System.Windows.Input.MouseEventArgs e)
    {
        if (!_dragging || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;
        Point point = System.Windows.Input.Mouse.GetPosition(grid);
        if ((point - _dragLastPoint).Length < 1) return;
        Vector direction = point - _dragLastPoint;
        if (_dragLastDirection is Vector previousDirection && previousDirection.X * direction.X + previousDirection.Y * direction.Y < 0) _dragLastDay = null;
        _dragLastDirection = direction;
        ApplyDragPath(grid, _dragLastPoint, point);
        _dragLastPoint = point;
    }

    private void EndDrag(System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_dragging || e.ChangedButton != System.Windows.Input.MouseButton.Left) return;
        bool moved = _dragMoved;
        _dragging = false;
        _dragMoved = false;
        _dragLastDay = null;
        _dragLastDirection = null;
        if (!moved)
        {
            return;
        }

        System.Windows.Input.Mouse.Capture(null);
        _suppressNextClick = true;
        Dispatcher.BeginInvoke(new Action(() => _suppressNextClick = false), System.Windows.Threading.DispatcherPriority.Input);
        e.Handled = true;
    }

    private void ApplyDragPath(Grid grid, Point from, Point to)
    {
        void ToggleOnPath(DateTime day)
        {
            if (_dragLastDay is DateTime previous && previous != day) _dragMoved = true;
            if (_dragLastDay == day) return;
            _dragLastDay = day;
            if (!Selection.Add(day)) Selection.Remove(day);
        }
        // A straight pointer movement across a week boundary need not touch the
        // Saturday/Sunday boxes. Include the intervening dates rather than only
        // the geometrical diagonal across the grid.
        if (TryGetDate(grid, from, out DateTime start) && TryGetDate(grid, to, out DateTime end)
            && Math.Abs((end-start).TotalDays) <= 42)
        {
            int step = start <= end ? 1 : -1;
            for (DateTime day = start; ; day = day.AddDays(step))
            {
                ToggleOnPath(day);
                if (day == end) break;
            }
            UpdateSelectionVisuals();
            return;
        }
        double distance = (to - from).Length;
        int samples = Math.Max(1, (int)Math.Ceiling(distance / 6));
        for (int i = 0; i <= samples; i++)
        {
            double ratio = i / (double)samples;
            Point point = new(from.X + (to.X - from.X) * ratio, from.Y + (to.Y - from.Y) * ratio);
            if (!TryGetDate(grid, point, out DateTime day)) continue;
            ToggleOnPath(day);
        }
        UpdateSelectionVisuals();
    }

    private bool TryGetDate(Grid grid, Point point, out DateTime day)
    {
        DependencyObject? current = VisualTreeHelper.HitTest(grid, point)?.VisualHit;
        while (current is not null && current != grid)
        {
            if (current is Button button && button.IsEnabled && button.Tag is DateTime value)
            {
                day = value.Date;
                return true;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        DateTime? nearest = null;
        double nearestDistance = double.MaxValue;
        foreach (var pair in _dayButtons)
        {
            if (!pair.Value.IsEnabled || pair.Value.ActualWidth <= 0 || pair.Value.ActualHeight <= 0) continue;
            Rect bounds = pair.Value.TransformToAncestor(grid).TransformBounds(new Rect(0,0,pair.Value.ActualWidth,pair.Value.ActualHeight));
            bounds.Inflate(7,7);
            if (!bounds.Contains(point)) continue;
            Point center = new(bounds.X+bounds.Width/2,bounds.Y+bounds.Height/2);
            double distance = (center-point).Length;
            if (distance < nearestDistance) { nearest = pair.Key; nearestDistance = distance; }
        }
        if (nearest is DateTime valueDay) { day = valueDay; return true; }
        day = default;
        return false;
    }

    private void UpdateSelectionVisuals()
    {
        foreach(var pair in _dayButtons)ApplyDayVisual(pair.Key,pair.Value);
        if(_selectionSummary is null||_selectedChips is null||_selectedScroll is null)return;
        _adjustingLayout=true;
        try
        {
            if(Selection.Count==0)_expanded=false;
            _selectionSummary.Text=$"已选 {Selection.Count} 天";
            // On very short work areas the count and selected day in the
            // calendar already identify a single selection. Keep the action
            // row fully visible instead of reserving a second chip row.
            _selectedScroll.Visibility=_compact&&!_expanded&&Selection.Count==1?Visibility.Collapsed:Visibility.Visible;
            if(_collapseDates is not null)_collapseDates.Visibility=_expanded?Visibility.Visible:Visibility.Collapsed;
            if(_clearDates is not null){_clearDates.Visibility=_expanded?Visibility.Collapsed:Visibility.Visible;_clearDates.IsEnabled=Selection.Count>0;}
            if(_clearExpandedDates is not null){_clearExpandedDates.Visibility=_expanded?Visibility.Visible:Visibility.Collapsed;_clearExpandedDates.IsEnabled=Selection.Count>0;}
            double width=Math.Max(120,_selectedScroll.ActualWidth>0?_selectedScroll.ActualWidth-10:Width-72);
            _selectedChips.Width=width;_lastContainerWidth=_selectedScroll.ActualWidth;
            _selectedChips.Children.Clear();
            DateTime[] ordered=Selection.OrderBy(d=>d).ToArray();
            var chips=ordered.Select(d=>DateChip(d,_expanded)).ToArray();
            foreach(var chip in chips)chip.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
            if(_expanded)
            {
                foreach(var chip in chips)_selectedChips.Children.Add(chip);
            }
            else
            {
                double allWidth=chips.Sum(c=>c.DesiredSize.Width);
                if(allWidth<=width+0.5)foreach(var chip in chips)_selectedChips.Children.Add(chip);
                else
                {
                    int visible=0;double used=0;
                    for(int n=0;n<chips.Length;n++)
                    {
                        Button fold=FoldButton(chips.Length-n-1);fold.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
                        if(used+chips[n].DesiredSize.Width+fold.DesiredSize.Width>width)break;
                        used+=chips[n].DesiredSize.Width;visible=n+1;
                    }
                    for(int n=0;n<visible;n++)_selectedChips.Children.Add(chips[n]);
                    _selectedChips.Children.Add(FoldButton(chips.Length-visible));
                }
            }
            LimitSelectedHeight();
        }
        finally{_adjustingLayout=false;}
    }
    private void ApplyDayVisual(DateTime day,Button button)
    {
        bool selected=Selection.Contains(day),today=day==DateTime.Today,other=day.Year!=_month.Year||day.Month!=_month.Month;
        button.Background=selected?PlannerTheme.PrimaryFill:Brushes.Transparent;
        button.Foreground=selected?Brushes.White:other?PlannerTheme.Muted:today?PlannerTheme.Accent:PlannerTheme.Ink;
        button.BorderBrush=today&&!selected?PlannerTheme.Accent:Brushes.Transparent;
        button.BorderThickness=new Thickness(today&&!selected?1:0);
        button.FontWeight=selected?FontWeights.SemiBold:FontWeights.Normal;
    }
    private Border DateChip(DateTime day,bool compact)
    {
        StackPanel content=new(){Orientation=Orientation.Horizontal};
        content.Children.Add(new TextBlock{Text=day.ToString("M月d日")+(day==DateTime.Today?"（今天）":""),FontSize=compact?11:12,Foreground=PlannerTheme.Ink,VerticalAlignment=VerticalAlignment.Center});
        Button remove=new(){Name="RemoveDate"+day.ToString("yyyyMMdd"),Content="×",Style=(Style)FindResource("PlannerLink"),Width=17,Height=19,Padding=new Thickness(0),Margin=new Thickness(3,0,0,0),FontSize=13,Foreground=PlannerTheme.Muted,ToolTip="移除"+day.ToString("M月d日")};
        remove.Click+=(_,_)=>{Selection.Remove(day);UpdateSelectionVisuals();};content.Children.Add(remove);
        return new Border{Child=content,Background=PlannerTheme.ChipBackground,CornerRadius=new CornerRadius(compact?6:8),Padding=compact?new Thickness(5,2,3,2):new Thickness(7,3,5,3),Margin=compact?new Thickness(2,2,2,2):new Thickness(2,3,2,3)};
    }
    private Button FoldButton(int remaining)
    {
        Button fold=new(){Name="ExpandDates",Content=$"+{remaining}⌄",Style=(Style)FindResource("PlannerChip"),Height=26,Padding=new Thickness(7,2,7,2),Margin=new Thickness(2,3,2,3),Background=PlannerTheme.AccentSoft,Foreground=PlannerTheme.Accent,BorderThickness=new Thickness(0),FontSize=12};
        fold.Click+=(_,_)=>{_expanded=true;UpdateSelectionVisuals();};return fold;
    }
    private void LimitSelectedHeight()
    {
        if(_selectedScroll is null||_selectedChips is null||_selectedArea is null)return;
        _selectedScroll.MaxHeight=double.PositiveInfinity;
        double contentWidth=Width-_shell.Margin.Left-_shell.Margin.Right-_shell.Padding.Left-_shell.Padding.Right-_shell.BorderThickness.Left-_shell.BorderThickness.Right-_panel.Margin.Left-_panel.Margin.Right;
        Size measureSize=new(contentWidth,double.PositiveInfinity);
        double fixedHeight=_shell.Margin.Top+_shell.Margin.Bottom+_shell.Padding.Top+_shell.Padding.Bottom+_shell.BorderThickness.Top+_shell.BorderThickness.Bottom+_panel.Margin.Top+_panel.Margin.Bottom+_selectedArea.Margin.Top+_selectedArea.Margin.Bottom;
        foreach(UIElement child in _panel.Children)
        {
            if(child==_selectedArea)continue;
            child.InvalidateMeasure();child.Measure(measureSize);fixedHeight+=child.DesiredSize.Height;
        }
        foreach(UIElement child in _selectedArea.Children)
        {
            if(child==_selectedScroll)continue;
            child.InvalidateMeasure();child.Measure(measureSize);fixedHeight+=child.DesiredSize.Height;
        }
        _selectedChips.InvalidateMeasure();_selectedChips.Measure(measureSize);
        double safe=SafeModalHeight(),available=Math.Max(_compact?0:28,safe-fixedHeight-8);
        if(_selectedChips.DesiredSize.Height>available+1)_selectedScroll.MaxHeight=available;
        MaxHeight=safe;
        _selectedScroll.InvalidateMeasure();_shell.InvalidateMeasure();InvalidateMeasure();
    }
}
