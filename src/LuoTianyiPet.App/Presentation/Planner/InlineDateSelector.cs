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
    public InlineDateSelector(HashSet<DateTime> dates, HashSet<DateTime> locked, ReminderBook book, DateTime month, Action changed, Action accept, Action cancel)
    {
        Name="InlineDateSelector";_dates=dates;_locked=locked;_book=book;_month=new DateTime(month.Year,month.Month,1);_changed=changed;_accept=accept;_cancel=cancel;
        Height=292;Background=Brushes.White;BorderBrush=PlannerTheme.ControlLine;BorderThickness=new Thickness(1);CornerRadius=new CornerRadius(9);Padding=new Thickness(10);
        Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=10,ShadowDepth=3,Opacity=.12};Render();
    }
    private static TextBlock Text(string text,double size=12)=>new(){Text=text,FontSize=size,Foreground=PlannerTheme.Ink,VerticalAlignment=VerticalAlignment.Center};
    private Button Button(string name,string label,Action click)
    {
        var button=new Button{Name=name,Content=label,Padding=new Thickness(5,3,5,3),Margin=new Thickness(2),MinHeight=0,Height=30};button.Click+=(_,_)=>click();return button;
    }
    private void Changed(){Render();_changed();}
    private void Render()
    {
        Grid root=new();root.RowDefinitions.Add(new());root.RowDefinitions.Add(new(){Height=GridLength.Auto});
        Grid body=new();body.ColumnDefinitions.Add(new(){Width=new GridLength(1.6,GridUnitType.Star)});body.ColumnDefinitions.Add(new());root.Children.Add(body);
        Grid calendar=new(){Margin=new Thickness(0,0,10,0)};calendar.RowDefinitions.Add(new(){Height=new GridLength(36)});calendar.RowDefinitions.Add(new(){Height=new GridLength(26)});for(int r=0;r<6;r++)calendar.RowDefinitions.Add(new());for(int c=0;c<7;c++)calendar.ColumnDefinitions.Add(new());body.Children.Add(calendar);
        var prev=Button("InlinePreviousMonth","‹",()=>{_month=_month.AddMonths(-1);Render();});prev.IsEnabled=_month>ReminderSchedule.MinimumDate;calendar.Children.Add(prev);
        var caption=Text(_month.ToString("yyyy年M月"),14);caption.FontWeight=FontWeights.SemiBold;caption.HorizontalAlignment=HorizontalAlignment.Center;Grid.SetColumn(caption,1);Grid.SetColumnSpan(caption,5);calendar.Children.Add(caption);
        var next=Button("InlineNextMonth","›",()=>{_month=_month.AddMonths(1);Render();});next.IsEnabled=_month.Year<2099||_month.Month<12;Grid.SetColumn(next,6);calendar.Children.Add(next);
        for(int c=0;c<7;c++){var label=Text("一二三四五六日"[c].ToString());label.Foreground=PlannerTheme.Muted;label.HorizontalAlignment=HorizontalAlignment.Center;Grid.SetRow(label,1);Grid.SetColumn(label,c);calendar.Children.Add(label);}
        DateTime first=_month.AddDays(-((int)_month.DayOfWeek+6)%7);
        for(int i=0;i<42;i++)
        {
            DateTime day=first.AddDays(i);bool selected=_dates.Contains(day),today=day==DateTime.Today,locked=_locked.Contains(day);
            var pick=Button("InlineDate"+day.ToString("yyyyMMdd"),day.Day.ToString(),()=>{if(!_dates.Add(day))_dates.Remove(day);Changed();});
            pick.Height=double.NaN;pick.Padding=new Thickness(0);pick.Margin=new Thickness(2);pick.ToolTip=locked?"已发生的日期，仅可通过删除操作移除":day.ToString("yyyy年M月d日");pick.IsEnabled=!locked&&day>=ReminderSchedule.MinimumDate&&day<=ReminderSchedule.MaximumDate;
            pick.Background=selected?PlannerTheme.AccentSoft:Brushes.Transparent;pick.Foreground=day.Month==_month.Month?PlannerTheme.Ink:PlannerTheme.Muted;pick.BorderBrush=today?PlannerTheme.Accent:Brushes.Transparent;pick.BorderThickness=new Thickness(today?1:0);
            if(today){var label=new StackPanel();label.Children.Add(new TextBlock{Text=day.Day.ToString(),FontSize=12,HorizontalAlignment=HorizontalAlignment.Center});label.Children.Add(new TextBlock{Text="今",FontSize=8,Foreground=PlannerTheme.Accent,HorizontalAlignment=HorizontalAlignment.Center});pick.Content=label;}
            Grid.SetRow(pick,2+i/7);Grid.SetColumn(pick,i%7);calendar.Children.Add(pick);
        }
        Grid right=new(){Margin=new Thickness(5,0,0,0)};right.RowDefinitions.Add(new(){Height=new GridLength(36)});right.RowDefinitions.Add(new());Grid.SetColumn(right,1);body.Children.Add(right);
        DockPanel header=new();var clear=Button("InlineClearDates","清空选择",()=>{_dates.RemoveWhere(d=>!_locked.Contains(d));Changed();});clear.FontSize=11;clear.Padding=new Thickness(2);clear.IsEnabled=_dates.Except(_locked).Any();DockPanel.SetDock(clear,Dock.Right);header.Children.Add(clear);header.Children.Add(Text($"已选 {_dates.Count} 天",13));right.Children.Add(header);
        StackPanel list=new();bool years=_dates.Select(d=>d.Year).Distinct().Count()>1;int lastYear=0;
        foreach(var day in _dates.OrderBy(d=>d))
        {
            if(years&&lastYear!=day.Year){var year=Text(day.Year+"年",11);year.Foreground=PlannerTheme.Muted;year.Margin=new Thickness(0,7,0,4);list.Children.Add(year);lastYear=day.Year;}
            Grid row=new(){Height=32};foreach(var w in new[]{new GridLength(1,GridUnitType.Star),new GridLength(30),new GridLength(23),new GridLength(24)})row.ColumnDefinitions.Add(new(){Width=w});
            row.Children.Add(Text(day.ToString("M月d日")));var weekday=Text("周"+"日一二三四五六"[(int)day.DayOfWeek],11);weekday.Foreground=PlannerTheme.Muted;Grid.SetColumn(weekday,1);row.Children.Add(weekday);
            if(_book.RestOverrides.TryGetValue(day.ToString("yyyy-MM-dd"),out bool rest)){var badge=new Border{Child=Text(rest?"休":"班",11),Background=rest?PlannerTheme.DangerSoft:PlannerTheme.AccentSoft,Padding=new Thickness(3),CornerRadius=new CornerRadius(4),VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(badge,2);row.Children.Add(badge);}
            var remove=Button("InlineRemove"+day.ToString("yyyyMMdd"),_locked.Contains(day)?"锁":"×",()=>{_dates.Remove(day);Changed();});remove.IsEnabled=!_locked.Contains(day);remove.BorderThickness=new Thickness(0);remove.Background=Brushes.Transparent;remove.Padding=new Thickness(0);remove.ToolTip=_locked.Contains(day)?"历史日期已锁定":"移除此日期";Grid.SetColumn(remove,3);row.Children.Add(remove);list.Children.Add(row);
        }
        var scroll=new ScrollViewer{Name="InlineSelectedDates",Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(scroll,1);right.Children.Add(scroll);
        StackPanel actions=new(){Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,10,0,0)};var cancel=Button("InlineCancelDates","取消",_cancel);cancel.Width=80;actions.Children.Add(cancel);var confirm=Button("InlineConfirmDates","确定",_accept);confirm.Width=80;confirm.Background=PlannerTheme.PrimaryFill;confirm.Foreground=Brushes.White;actions.Children.Add(confirm);Grid.SetRow(actions,1);root.Children.Add(actions);Child=root;
    }
}
