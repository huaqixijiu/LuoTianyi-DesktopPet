using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LuoTianyiPet.Core;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using TextBox = System.Windows.Controls.TextBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Brush = System.Windows.Media.Brush;
using Size = System.Windows.Size;

namespace LuoTianyiPet.App;

internal sealed partial class PlannerWindow
{
    private void EditSchedule(ReminderItem? original, DateTime? presetDate)
    {
        _editing=true;
        DateTime opened=DateTime.Now;
        DateTime? context=_occurrenceDate;
        HashSet<DateTime> selected=new(original!=null?CalendarEditing.Dates(original):presetDate is DateTime d?new[]{d.Date}:Array.Empty<DateTime>());
        HashSet<DateTime> locked=new(original?.Repeat==ReminderRepeat.Dates?selected.Where(d=>CalendarEditing.IsPast(original,d,opened)):Array.Empty<DateTime>());
        Grid layout=new(){Name="PlannerEditorLayout",Width=680,Height=640,Tag="CalendarEditor"};
        layout.RowDefinitions.Add(new(){Height=GridLength.Auto});layout.RowDefinitions.Add(new());layout.RowDefinitions.Add(new(){Height=GridLength.Auto});layout.RowDefinitions.Add(new(){Height=GridLength.Auto});
        DockPanel header=new(){Background=Brushes.Transparent,Margin=new Thickness(0,0,0,8)};
        var close=IconButton("close",()=>{_editing=false;Render();},"关闭编辑器",18,PlannerTheme.Muted);close.Name="EditorClose";DockPanel.SetDock(close,Dock.Right);header.Children.Add(close);
        StackPanel heading=new();heading.Children.Add(Text(original==null?"新建日程":"编辑日程",24,FontWeights.SemiBold));heading.Children.Add(Text(original==null?"创建一条新的日程安排":original.Repeat==ReminderRepeat.Dates?"调整这组多日期日程安排":"调整这一天的日程",12,foreground:PlannerTheme.Muted));
        if(original?.Repeat==ReminderRepeat.Dates)heading.Children.Add(Text($"已发生 {locked.Count} 次 · 未来 {selected.Count-locked.Count} 次",12,foreground:PlannerTheme.Muted));header.Children.Add(heading);layout.Children.Add(header);
        header.MouseLeftButtonDown+=(_,e)=>{for(var node=e.OriginalSource as DependencyObject;node!=null&&node!=header;node=VisualTreeHelper.GetParent(node))if(node is Button)return;if(e.LeftButton==MouseButtonState.Pressed){DragMove();e.Handled=true;}};
        StackPanel form=new();var scroll=new ScrollViewer{Name="EditorFormScroll",Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(scroll,1);layout.Children.Add(scroll);
        TextBlock error=Text("",12,foreground:PlannerTheme.Danger);error.Name="EditorError";error.Margin=new Thickness(0,6,0,0);Grid.SetRow(error,2);layout.Children.Add(error);
        void Label(string value,double gap=8){var label=Text(value,15,FontWeights.SemiBold);label.Margin=new Thickness(0,gap,0,6);form.Children.Add(label);}
        TextBox title=new(){Name="ReminderTitle",Text=original?.Title??"",MaxLength=Math.Max(20,original?.Title.Length??0),Height=42,Padding=new Thickness(12,9,60,9)};
        Grid Counted(TextBox input,int limit,string placeholder)
        {
            Grid host=new();host.Children.Add(input);
            var count=Text(input.Text.Length+"/"+limit,13,foreground:PlannerTheme.Muted);count.HorizontalAlignment=HorizontalAlignment.Right;count.VerticalAlignment=VerticalAlignment.Bottom;count.Margin=new Thickness(0,0,12,12);count.IsHitTestVisible=false;host.Children.Add(count);
            var hint=Text(placeholder,14,foreground:PlannerTheme.Muted);hint.Margin=new Thickness(13,12,64,0);hint.VerticalAlignment=VerticalAlignment.Top;hint.IsHitTestVisible=false;host.Children.Add(hint);
            void UpdateCount(){count.Text=input.Text.Length+"/"+limit;count.Foreground=input.Text.Length>limit?PlannerTheme.Danger:PlannerTheme.Muted;hint.Visibility=input.Text.Length==0?Visibility.Visible:Visibility.Collapsed;}
            input.TextChanged+=(_,_)=>UpdateCount();UpdateCount();return host;
        }
        Label("事项标题 *",0);form.Children.Add(Counted(title,20,"请输入事项标题"));
        Label("日期 *");var picker=Action("选择日期",()=>{});picker.Name="ModifyDates";picker.Height=42;picker.Margin=new Thickness(0);picker.HorizontalContentAlignment=HorizontalAlignment.Stretch;form.Children.Add(picker);
        InlineDateSelector? selector=null;HashSet<DateTime>? snapshot=null;
        void Summary()
        {
            DateTime[] days=selected.OrderBy(d=>d).ToArray();double available=Math.Max(150,(picker.ActualWidth>0?picker.ActualWidth:620)-80);
            string content="选择日期";
            if(days.Length==1)content=$"{days[0]:M月d日} 周{WeekdayLabel(days[0])}";
            else if(days.Length>1)
            {
                string prefix=$"已选 {days.Length} 天：";content=prefix;
                for(int n=0;n<days.Length;n++)
                {
                    string date=days.Select(d=>d.Year).Distinct().Count()>1?days[n].ToString("yyyy/M/d"):days[n].ToString("M月d日");
                    string candidate=content+(n>0?"、":"")+date;
                    string suffix=n<days.Length-1?$"、+{days.Length-n-1}天":"";
                    var measure=Text(candidate+suffix,14);measure.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
                    if(measure.DesiredSize.Width>available){content+=(n>0?"、":"")+$"+{days.Length-n}天";break;}content=candidate;
                }
            }
            Grid caption=new();caption.ColumnDefinitions.Add(new(){Width=new GridLength(25)});caption.ColumnDefinitions.Add(new());caption.ColumnDefinitions.Add(new(){Width=new GridLength(18)});
            caption.Children.Add(PlannerTheme.Icon("calendar",16,PlannerTheme.Ink,0));var label=Text(content,14,foreground:days.Length==0?PlannerTheme.Muted:PlannerTheme.Ink);label.Name="ScheduleDateSummary";label.TextWrapping=TextWrapping.NoWrap;label.TextTrimming=TextTrimming.CharacterEllipsis;Grid.SetColumn(label,1);caption.Children.Add(label);var chevron=PlannerTheme.Icon(selector==null?"chevron-down":"chevron-up",12,PlannerTheme.Ink,0);Grid.SetColumn(chevron,2);caption.Children.Add(chevron);picker.Content=caption;
        }
        void CloseDates(bool rollback)
        {
            if(selector==null)return;
            if(rollback&&snapshot!=null){selected.Clear();selected.UnionWith(snapshot);}
            layout.Children.Remove(selector);selector=null;snapshot=null;
            foreach(UIElement child in layout.Children)if(Grid.GetRow(child)==3)child.Visibility=Visibility.Visible;
            Summary();
        }
        picker.Click+=(_,_)=>
        {
            if(selector!=null){CloseDates(false);return;}
            snapshot=new HashSet<DateTime>(selected);
            selector=new InlineDateSelector(selected,locked,_service.Book,selected.Count>0?selected.Min():_date,Summary,()=>CloseDates(false),()=>CloseDates(true));
            selector.HorizontalAlignment=HorizontalAlignment.Stretch;selector.VerticalAlignment=VerticalAlignment.Top;
            double top=picker.TranslatePoint(new Point(0,picker.ActualHeight+3),layout).Y;
            selector.Margin=new Thickness(0,Math.Min(top,layout.ActualHeight-selector.Height-3),0,0);Grid.SetRow(selector,0);Grid.SetRowSpan(selector,4);System.Windows.Controls.Panel.SetZIndex(selector,10);
            foreach(UIElement child in layout.Children)if(Grid.GetRow(child)==3)child.Visibility=Visibility.Hidden;
            layout.Children.Add(selector);Summary();
        };
        void OutsideDates(object sender,MouseButtonEventArgs e){if(selector==null)return;for(var node=e.OriginalSource as DependencyObject;node!=null;node=VisualTreeHelper.GetParent(node))if(node==selector||node==picker)return;CloseDates(false);}
        layout.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape&&selector!=null){CloseDates(true);e.Handled=true;}};
        picker.SizeChanged+=(_,_)=>Summary();Summary();
        bool refreshing=false;
        CheckBox Switch(string name,bool value)=>new(){Name=name,IsChecked=value,Style=(Style)FindResource("PlannerSwitch"),Content=null,Margin=new Thickness(0),VerticalAlignment=VerticalAlignment.Center};
        var setTime=Switch("SetSpecificTime",original?.HasTime==true);var alarm=Switch("CreateAlarm",original?.Enabled==true&&original.HasTime);var early=Switch("EarlyReminder",original?.EarlyEnabled==true);
        Grid Option(string label,CheckBox toggle,UIElement? input=null,bool indent=false)
        {
            Grid row=new(){Height=44};row.ColumnDefinitions.Add(new(){Width=new GridLength(120)});row.ColumnDefinitions.Add(new(){Width=new GridLength(70)});row.ColumnDefinitions.Add(new());
            var text=Text(label,15,indent?null:FontWeights.SemiBold,indent?PlannerTheme.Muted:PlannerTheme.Ink);text.Margin=new Thickness(indent?24:0,0,0,0);row.Children.Add(text);Grid.SetColumn(toggle,1);row.Children.Add(toggle);if(input!=null){Grid.SetColumn(input,2);row.Children.Add(input);}return row;
        }
        TextBox hour=new(){Name="ScheduleHour",Text=original?.HasTime==true?original.Start.ToString("HH"):"",Width=42,MaxLength=2,Height=34,BorderThickness=new Thickness(0),Background=Brushes.Transparent,TextAlignment=TextAlignment.Center,Padding=new Thickness(4,5,4,5)};
        TextBox minute=new(){Name="ScheduleMinute",Text=original?.HasTime==true?original.Start.ToString("mm"):"",Width=42,MaxLength=2,Height=34,BorderThickness=new Thickness(0),Background=Brushes.Transparent,TextAlignment=TextAlignment.Center,Padding=new Thickness(4,5,4,5)};
        Grid Segment(TextBox input){Grid g=new();g.Children.Add(input);var hint=Text("--",14,foreground:PlannerTheme.Muted);hint.HorizontalAlignment=HorizontalAlignment.Center;hint.IsHitTestVisible=false;g.Children.Add(hint);input.TextChanged+=(_,_)=>hint.Visibility=input.Text.Length==0?Visibility.Visible:Visibility.Collapsed;hint.Visibility=input.Text.Length==0?Visibility.Visible:Visibility.Collapsed;return g;}
        StackPanel digits=Row();digits.HorizontalAlignment=HorizontalAlignment.Center;digits.Children.Add(Segment(hour));digits.Children.Add(Text(":",15));digits.Children.Add(Segment(minute));
        Border timeField=new(){Name="ScheduleTimeField",Child=digits,Width=124,Height=38,CornerRadius=new CornerRadius(18),BorderBrush=PlannerTheme.ControlLine,BorderThickness=new Thickness(1),Background=PlannerTheme.Soft,HorizontalAlignment=HorizontalAlignment.Left};
        var timeRow=Option("设置时间",setTime,timeField);timeRow.Margin=new Thickness(0,8,0,0);Grid.SetColumn(timeField,1);Grid.SetColumnSpan(timeField,2);timeField.Margin=new Thickness(72,0,0,0);form.Children.Add(timeRow);
        var alarmRow=Option("创建闹钟提醒",alarm);form.Children.Add(alarmRow);
        TextBox lead=CreateEarlyMinutesInput(original?.EarlyMinutes??30);StackPanel leadField=Row();leadField.Children.Add(lead);leadField.Children.Add(Text("分钟",12,foreground:PlannerTheme.Muted));var earlyRow=Option("提前提醒",early,leadField,true);form.Children.Add(earlyRow);
        var segmentedTime=new PlannerTimeInput(hour,minute);
        bool ValidTime(out TimeSpan value)=>CalendarEditing.TryTime(hour.Text,minute.Text,out value);
        void Refresh()
        {
            bool timed=setTime.IsChecked==true,valid=ValidTime(out _);timeField.Visibility=timed?Visibility.Visible:Visibility.Collapsed;
            alarmRow.Visibility=timed&&valid?Visibility.Visible:Visibility.Collapsed;earlyRow.Visibility=timed&&valid&&alarm.IsChecked==true?Visibility.Visible:Visibility.Collapsed;lead.IsEnabled=early.IsChecked==true;
            timeField.BorderBrush=timed&&!valid&&(hour.Text.Length>0||minute.Text.Length>0)?PlannerTheme.Danger:PlannerTheme.ControlLine;
        }
        void Normalize(){if(ValidTime(out var value)){refreshing=true;hour.Text=value.Hours.ToString("00");minute.Text=value.Minutes.ToString("00");refreshing=false;}Refresh();}
        hour.TextChanged+=(_,_)=>{if(!refreshing)Refresh();};minute.TextChanged+=(_,_)=>{if(!refreshing)Refresh();};void NormalizeOnExit(){Dispatcher.BeginInvoke(new Action(()=>{if(!timeField.IsKeyboardFocusWithin)Normalize();}));}hour.LostKeyboardFocus+=(_,_)=>NormalizeOnExit();minute.LostKeyboardFocus+=(_,_)=>NormalizeOnExit();
        hour.KeyDown+=(_,e)=>{if(e.Key==Key.Enter){Normalize();minute.Focus();e.Handled=true;}};minute.KeyDown+=(_,e)=>{if(e.Key==Key.Enter){Normalize();e.Handled=true;}};
        setTime.Click+=(_,_)=>
        {
            if(setTime.IsChecked!=true&&(original?.ReminderCreated==true||alarm.IsChecked==true))
            {
                setTime.IsChecked=true;ConfirmScheduleAction("清除日程时间？","清除日程时间后，关联的闹钟提醒也会被移除，是否继续？",()=>{setTime.IsChecked=false;alarm.IsChecked=false;early.IsChecked=false;hour.Text="";minute.Text="";Refresh();return Task.CompletedTask;});
            }
            else {if(setTime.IsChecked!=true){alarm.IsChecked=false;early.IsChecked=false;hour.Text="";minute.Text="";}Refresh();}
        };
        alarm.Click+=(_,_)=>Refresh();early.Click+=(_,_)=>Refresh();Refresh();
        Label("备注（可选）",10);TextBox notes=new(){Name="ReminderNotes",Text=original?.Notes??"",MaxLength=Math.Max(200,original?.Notes.Length??0),AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,Height=104,Padding=new Thickness(12,9,65,9),VerticalScrollBarVisibility=ScrollBarVisibility.Auto};form.Children.Add(Counted(notes,200,"写一点备注…"));
        layout.Height=672+(original?.Repeat==ReminderRepeat.Dates?22:0);
        foreach(var input in new[]{title,notes,hour,minute,lead})input.TextChanged+=(_,_)=>error.Text="";
        DockPanel actions=new(){LastChildFill=false,Margin=new Thickness(0,10,0,0),Height=54};Grid.SetRow(actions,3);layout.Children.Add(actions);
        var save=AsyncAction(original==null?"创建日程":"保存修改",async()=>
        {
            try
            {
                CloseDates(false);Normalize();error.Text="";
                if(string.IsNullOrWhiteSpace(title.Text)||title.Text.Trim().Length>20)throw new ArgumentException("事项标题必填，最多20字。");
                if(notes.Text.Length>200)throw new ArgumentException("备注最多200字。");
                if(selected.Count==0)throw new ArgumentException("请选择至少一个日期。");
                TimeSpan time=TimeSpan.Zero;if(setTime.IsChecked==true&&!ValidTime(out time))throw new ArgumentException("请填写有效时间（00:00～23:59）。");
                int advance=30;if(early.IsChecked==true&&alarm.IsChecked==true&&(!int.TryParse(lead.Text,out advance)||advance<1||advance>60))throw new ArgumentException("提前提醒请输入1～60分钟。");
                var dates=selected.OrderBy(d=>d).ToList();bool on=setTime.IsChecked==true&&alarm.IsChecked==true;
                ReminderItem item=new(){Id=original?.Id??Guid.NewGuid(),Calendar=true,Title=title.Text.Trim(),Notes=notes.Text,Start=dates[0]+time,HasTime=setTime.IsChecked==true,Dates=dates,Repeat=dates.Count>1||original?.Repeat==ReminderRepeat.Dates?ReminderRepeat.Dates:ReminderRepeat.Once,Enabled=on,ReminderCreated=on,EarlyEnabled=on&&early.IsChecked==true,EarlyMinutes=advance};
                await Execute(b=>CalendarEditing.Save(b,item,DateTime.Now));_editing=false;Render();
            }
            catch(Exception ex)when(ex is ArgumentException or System.IO.IOException or UnauthorizedAccessException){error.Text=ex is ArgumentException?ex.Message:"保存失败，原数据保留，请重试。";}
        });save.Name="SaveReminder";save.Width=original==null?160:128;save.Height=48;save.FontSize=15;DockPanel.SetDock(save,Dock.Right);actions.Children.Add(Primary(save));
        var cancel=Action("取消",()=>{_editing=false;Render();});cancel.Name="CancelScheduleEdit";cancel.Width=original==null?140:112;cancel.Height=48;cancel.FontSize=15;DockPanel.SetDock(cancel,Dock.Right);actions.Children.Add(cancel);
        if(original!=null)
        {
            Button DangerAction(string name,string label,Brush foreground,Brush background,Action action){var b=Action(label,action);b.Name=name;b.Width=name=="DeleteOccurrence"?104:130;b.FontSize=14;b.Padding=new Thickness(10,6,10,6);b.Margin=new Thickness(0,3,6,3);b.Height=48;b.Foreground=foreground;b.Background=background;b.BorderBrush=foreground;return b;}
            if(original.Repeat==ReminderRepeat.Dates&&context is DateTime current&&CalendarEditing.Dates(original).Contains(current.Date))actions.Children.Add(DangerAction("DeleteOccurrence","删除今天",PlannerTheme.Danger,Brushes.White,()=>ConfirmScheduleAction("删除今天的日程？",$"仅删除 {current:M月d日} 的日程，其他日期保持不变。",async()=>{await Execute(b=>ReminderSchedule.DeleteDate(b,original.Id,current));_editing=false;Render();})));
            if(original.Repeat==ReminderRepeat.Dates&&locked.Count>0&&selected.Count>locked.Count)actions.Children.Add(DangerAction("DeleteFutureSchedules","删除后续日程",new SolidColorBrush(Color.FromRgb(190,105,32)),new SolidColorBrush(Color.FromRgb(255,247,236)),()=>ConfirmScheduleAction("删除后续日程？",$"将删除未来 {CalendarEditing.Dates(original).Count(d=>!CalendarEditing.IsPast(original,d,DateTime.Now))} 次安排，已发生的 {CalendarEditing.Dates(original).Count(d=>CalendarEditing.IsPast(original,d,DateTime.Now))} 次记录会继续保留。",async()=>{await Execute(b=>CalendarEditing.DeleteFuture(b,original.Id,DateTime.Now));_editing=false;Render();})));
            actions.Children.Add(DangerAction("DeleteGroup",original.Repeat==ReminderRepeat.Dates?"删除全部日程":"删除日程",PlannerTheme.Danger,PlannerTheme.DangerSoft,()=>ConfirmScheduleAction("删除全部日程？",$"将删除已发生的 {CalendarEditing.Dates(original).Count(d=>CalendarEditing.IsPast(original,d,DateTime.Now))} 次记录和未来 {CalendarEditing.Dates(original).Count(d=>!CalendarEditing.IsPast(original,d,DateTime.Now))} 次安排，此操作不可恢复。",async()=>{await Execute(b=>ReminderSchedule.DeleteGroups(b,new[]{original.Id}));_editing=false;Render();})));
        }
        ShowOverlay(layout,true);((Grid)_shell.Children[_shell.Children.Count-1]).PreviewMouseDown+=OutsideDates;title.Focus();
    }

    private void ConfirmScheduleAction(string title,string explanation,Func<Task> confirmed)
    {
        StackPanel panel=new(){Width=420};panel.Children.Add(Text(title,20,FontWeights.SemiBold));var detail=Text(explanation,14,foreground:PlannerTheme.Muted);detail.TextWrapping=TextWrapping.Wrap;detail.Margin=new Thickness(0,16,0,20);panel.Children.Add(detail);
        StackPanel actions=Row();actions.HorizontalAlignment=HorizontalAlignment.Right;var cancel=Action("取消",CloseTopOverlay);cancel.Name="CancelScheduleAction";cancel.Width=112;cancel.Height=48;cancel.FontSize=15;actions.Children.Add(cancel);
        var confirm=AsyncAction("确认",async()=>{CloseTopOverlay();await confirmed();});confirm.Name="ConfirmScheduleAction";confirm.Width=112;confirm.Height=48;confirm.FontSize=15;confirm.Background=PlannerTheme.Danger;confirm.Foreground=Brushes.White;actions.Children.Add(confirm);panel.Children.Add(actions);ShowOverlay(panel);
    }
}
