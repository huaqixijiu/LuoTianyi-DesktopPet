using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LuoTianyiPet.Core;
using Button = System.Windows.Controls.Button;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace LuoTianyiPet.App;

/// <summary>Bounded, in-editor draft selector. The caller owns accept and rollback.</summary>
internal sealed class InlineDateSelector : Border
{
    private readonly HashSet<DateTime> _dates, _locked;
    private readonly ReminderBook _book;
    private readonly Action _changed, _accept, _cancel;
    private DateTime _month;
    private readonly bool _mini;
    private readonly double _editorWidth;
    private readonly Dictionary<DateTime,Button> _buttons=new();
    private Border? _selectedHost;
    private Grid? _calendar;
    private HashSet<DateTime>? _dragSnapshot;
    private readonly List<DateTime> _dragPath=new();
    private bool _dragMoved,_suppressClick;
    private DateTime _dragStart;
    public InlineDateSelector(HashSet<DateTime> dates, HashSet<DateTime> locked, ReminderBook book, DateTime month, Action changed, Action accept, Action cancel,double editorWidth=520)
    {
        Name="InlineDateSelector";_dates=dates;_locked=locked;_book=book;_month=new DateTime(month.Year,month.Month,1);_changed=changed;_accept=accept;_cancel=cancel;_editorWidth=editorWidth;_mini=editorWidth<480;
        Height=380;Background=Brushes.White;BorderBrush=PlannerTheme.ControlLine;BorderThickness=new Thickness(1);CornerRadius=new CornerRadius(12);Padding=new Thickness(_mini?8:10);
        Render();
    }
    private static TextBlock Text(string text,double size=14)=>new(){Text=text,FontSize=size,Foreground=PlannerTheme.Ink,VerticalAlignment=VerticalAlignment.Center};
    private Button Button(string name,string label,Action click)
    {
        var button=new Button{Name=name,Content=label,Padding=new Thickness(5,3,5,3),Margin=new Thickness(2),MinHeight=0,Height=36};button.Click+=(_,_)=>click();return button;
    }
    private void Changed(){Render();_changed();}
    private void Render()
    {
        Grid root=new();root.RowDefinitions.Add(new());root.RowDefinitions.Add(new(){Height=GridLength.Auto});
        Grid body=new();body.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});body.ColumnDefinitions.Add(new(){Width=new GridLength(Math.Min(192,Math.Max(148,_editorWidth*.34)))});root.Children.Add(body);
        DateTime first=_month.AddDays(-((int)_month.DayOfWeek+6)%7);
        int weekCount=(int)Math.Ceiling((DateTime.DaysInMonth(_month.Year,_month.Month)+(double)(_month-first).Days)/7);
        _buttons.Clear();Grid calendar=new(){Margin=new Thickness(0,0,_mini?4:7,0)};calendar.RowDefinitions.Add(new(){Height=new GridLength(_mini?36:40)});calendar.RowDefinitions.Add(new(){Height=new GridLength(_mini?22:24)});for(int r=0;r<weekCount;r++)calendar.RowDefinitions.Add(new());for(int c=0;c<7;c++)calendar.ColumnDefinitions.Add(new());body.Children.Add(calendar);_calendar=calendar;
        calendar.PreviewMouseMove+=(_,e)=>ContinueDrag(e);calendar.PreviewMouseLeftButtonUp+=(_,e)=>EndDrag(e);calendar.LostMouseCapture+=(_,_)=>{_dragSnapshot=null;};
        var prev=Button("InlinePreviousMonth","‹",()=>{_month=_month.AddMonths(-1);Render();});prev.IsEnabled=_month>ReminderSchedule.MinimumDate;calendar.Children.Add(prev);
        var caption=Text(_month.ToString("yyyy年M月"),14);caption.FontWeight=FontWeights.SemiBold;caption.HorizontalAlignment=HorizontalAlignment.Center;Grid.SetColumn(caption,1);Grid.SetColumnSpan(caption,5);calendar.Children.Add(caption);
        var next=Button("InlineNextMonth","›",()=>{_month=_month.AddMonths(1);Render();});next.IsEnabled=_month.Year<2099||_month.Month<12;Grid.SetColumn(next,6);calendar.Children.Add(next);
        for(int c=0;c<7;c++){var label=Text("一二三四五六日"[c].ToString());label.Foreground=PlannerTheme.Muted;label.HorizontalAlignment=HorizontalAlignment.Center;Grid.SetRow(label,1);Grid.SetColumn(label,c);calendar.Children.Add(label);}
        for(int i=0;i<weekCount*7;i++)
        {
            DateTime day=first.AddDays(i);bool selected=_dates.Contains(day),today=day==DateTime.Today,locked=_locked.Contains(day);
            var pick=Button("InlineDate"+day.ToString("yyyyMMdd"),day.Day.ToString(),()=>{if(_suppressClick)return;if(!_dates.Add(day))_dates.Remove(day);Changed();});
            pick.SetResourceReference(StyleProperty,"PlannerDate");pick.Width=_mini?29:32;pick.Height=_mini?29:32;pick.HorizontalAlignment=HorizontalAlignment.Center;pick.VerticalAlignment=VerticalAlignment.Center;pick.Padding=new Thickness(0);pick.Margin=new Thickness(0);pick.ToolTip=locked?"已发生的日期，仅可通过删除操作移除":day.ToString("yyyy年M月d日");pick.IsEnabled=!locked&&day>=ReminderSchedule.MinimumDate&&day<=ReminderSchedule.MaximumDate;
            pick.Background=selected?PlannerTheme.PrimaryFill:Brushes.Transparent;pick.Foreground=selected?Brushes.White:day.Month==_month.Month?PlannerTheme.Ink:PlannerTheme.Muted;pick.BorderBrush=today?PlannerTheme.Accent:Brushes.Transparent;pick.BorderThickness=new Thickness(today?1:0);

            _buttons[day]=pick;pick.PreviewMouseLeftButtonDown+=(_,e)=>BeginDrag(day,e);Grid.SetRow(pick,2+i/7);Grid.SetColumn(pick,i%7);calendar.Children.Add(pick);
        }
        _selectedHost=new Border{Child=SelectionList()};Grid.SetColumn(_selectedHost,1);body.Children.Add(_selectedHost);
        StackPanel actions=new(){Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,_mini?5:7,0,0)};var cancel=Button("InlineCancelDates","取消",_cancel);cancel.Width=_mini?68:76;cancel.Height=_mini?30:34;actions.Children.Add(cancel);var confirm=Button("InlineConfirmDates","确定",_accept);confirm.Width=_mini?68:76;confirm.Height=_mini?30:34;confirm.Background=PlannerTheme.PrimaryFill;confirm.Foreground=Brushes.White;actions.Children.Add(confirm);Grid.SetRow(actions,1);root.Children.Add(actions);Child=root;
    }
    private void BeginDrag(DateTime day,System.Windows.Input.MouseButtonEventArgs e)
    {
        if(e.ChangedButton!=System.Windows.Input.MouseButton.Left||_locked.Contains(day)||_calendar==null)return;
        _dragSnapshot=new HashSet<DateTime>(_dates);_dragPath.Clear();_dragStart=day;_dragMoved=false;
        System.Windows.Input.Mouse.Capture(_calendar,System.Windows.Input.CaptureMode.SubTree);
        e.Handled=true;
    }
    private void ContinueDrag(System.Windows.Input.MouseEventArgs e)
    {
        if(_dragSnapshot==null||_calendar==null||e.LeftButton!=System.Windows.Input.MouseButtonState.Pressed)return;
        Point point=e.GetPosition(_calendar);
        int column=(int)(point.X/(_calendar.ActualWidth/7));
        double header=_calendar.RowDefinitions[0].ActualHeight+_calendar.RowDefinitions[1].ActualHeight,rowHeight=(_calendar.ActualHeight-header)/Math.Max(1,_calendar.RowDefinitions.Count-2);
        int row=(int)Math.Floor((point.Y-header)/rowHeight);
        if(column<0||column>6||row<0||row>=_calendar.RowDefinitions.Count-2)return;
        DateTime day=_month.AddDays(-((int)_month.DayOfWeek+6)%7).AddDays(row*7+column);
        if(day==_dragStart&&!_dragMoved)return;
        _dragMoved=true;ExtendDrag(day);e.Handled=true;
    }
    // A path is reversible against the pre-drag snapshot, including mixed selections.
    internal void ExtendDrag(DateTime target)
    {
        if(_dragSnapshot==null)return;
        DateTime from=_dragPath.Count==0?_dragStart:_dragPath[_dragPath.Count-1];
        int step=from<=target?1:-1;
        for(DateTime day=from;;day=day.AddDays(step))
        {
            int existing=_dragPath.IndexOf(day);
            if(existing>=0)
            {
                for(int n=_dragPath.Count-1;n>existing;n--){DateTime restored=_dragPath[n];if(_dragSnapshot.Contains(restored))_dates.Add(restored);else _dates.Remove(restored);_dragPath.RemoveAt(n);}
            }
            else if(day>=ReminderSchedule.MinimumDate&&day<=ReminderSchedule.MaximumDate)
            {
                _dragPath.Add(day);
                if(!_locked.Contains(day)){if(_dragSnapshot.Contains(day))_dates.Remove(day);else _dates.Add(day);}
            }
            if(day==target)break;
        }
        foreach(var pair in _buttons){bool selected=_dates.Contains(pair.Key);pair.Value.Background=selected?PlannerTheme.PrimaryFill:Brushes.Transparent;pair.Value.Foreground=selected?Brushes.White:pair.Key.Month==_month.Month?PlannerTheme.Ink:PlannerTheme.Muted;}
        if(_selectedHost!=null)_selectedHost.Child=SelectionList();_changed();
    }
    private void EndDrag(System.Windows.Input.MouseButtonEventArgs e)
    {
        if(_dragSnapshot==null||e.ChangedButton!=System.Windows.Input.MouseButton.Left)return;
        if(!_dragMoved){if(!_dates.Add(_dragStart))_dates.Remove(_dragStart);}
        _dragSnapshot=null;System.Windows.Input.Mouse.Capture(null);_suppressClick=true;
        Changed();Dispatcher.BeginInvoke(new Action(()=>_suppressClick=false),System.Windows.Threading.DispatcherPriority.Input);e.Handled=true;
    }
    private Grid SelectionList()
    {
        Grid right=new(){Margin=new Thickness(_mini?2:4,0,0,0)};right.RowDefinitions.Add(new(){Height=new GridLength(_mini?32:36)});right.RowDefinitions.Add(new());
        DockPanel header=new(){LastChildFill=true};var clear=Button("InlineClearDates","清空选择",()=>{_dates.RemoveWhere(d=>!_locked.Contains(d));Changed();});clear.FontSize=_mini?10:11;clear.Width=_mini?52:60;clear.Height=_mini?26:29;clear.Margin=new Thickness(0);clear.Padding=new Thickness(1);clear.IsEnabled=_dates.Except(_locked).Any();DockPanel.SetDock(clear,Dock.Right);header.Children.Add(clear);header.Children.Add(Text($"已选 {_dates.Count} 天",_mini?11:12));right.Children.Add(header);
        StackPanel list=new();bool years=_dates.Select(d=>d.Year).Distinct().Count()>1;int lastYear=0;
        foreach(var day in _dates.OrderBy(d=>d))
        {
            if(years&&lastYear!=day.Year){var year=Text(day.Year+"年",11);year.Foreground=PlannerTheme.Muted;year.Margin=new Thickness(0,7,0,4);list.Children.Add(year);lastYear=day.Year;}
            Grid row=new(){Height=_mini?29:32};foreach(var w in new[]{new GridLength(_mini?54:66),new GridLength(_mini?29:36),new GridLength(22),new GridLength(24)})row.ColumnDefinitions.Add(new(){Width=w});
            row.Children.Add(Text(day.ToString("M月d日")));var weekday=Text("周"+"日一二三四五六"[(int)day.DayOfWeek],11);weekday.Foreground=PlannerTheme.Muted;Grid.SetColumn(weekday,1);row.Children.Add(weekday);
            if(_book.RestOverrides.TryGetValue(day.ToString("yyyy-MM-dd"),out bool rest)){var label=Text(rest?"休":"班",11);label.Foreground=rest?PlannerTheme.RestForeground:PlannerTheme.WorkForeground;var badge=new Border{Child=label,Background=rest?PlannerTheme.DangerSoft:PlannerTheme.WorkBadgeBackground,Padding=new Thickness(3),CornerRadius=new CornerRadius(4),VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(badge,2);row.Children.Add(badge);}
            var remove=Button("InlineRemove"+day.ToString("yyyyMMdd"),_locked.Contains(day)?"锁":"×",()=>{_dates.Remove(day);Changed();});remove.IsEnabled=!_locked.Contains(day);remove.BorderThickness=new Thickness(0);remove.Background=Brushes.Transparent;remove.Padding=new Thickness(0);remove.ToolTip=_locked.Contains(day)?"历史日期已锁定":"移除此日期";Grid.SetColumn(remove,3);row.Children.Add(remove);list.Children.Add(row);
        }
        var scroll=new ScrollViewer{Name="InlineSelectedDates",Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(scroll,1);right.Children.Add(scroll);
        return right;
    }

}
