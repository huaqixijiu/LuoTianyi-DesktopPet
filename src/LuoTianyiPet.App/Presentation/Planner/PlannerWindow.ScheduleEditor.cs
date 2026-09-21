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
        bool extendedActions=original?.Repeat==ReminderRepeat.Dates&&context is DateTime currentDay&&CalendarEditing.Dates(original).Contains(currentDay.Date)&&locked.Count>0&&selected.Count>locked.Count;
        bool mini=_pageSize=="mini";
        double editorWidth=_pageSize switch{"mini"=>440,"standard"=>520,"comfortable"=>560,_=>600};
        Grid layout=new(){Name="PlannerEditorLayout",Width=editorWidth,Height=558,Tag="CalendarEditor"};
        layout.RowDefinitions.Add(new(){Height=GridLength.Auto});layout.RowDefinitions.Add(new());layout.RowDefinitions.Add(new(){Height=GridLength.Auto});layout.RowDefinitions.Add(new(){Height=GridLength.Auto});
        DockPanel header=new(){Background=Brushes.Transparent,Margin=new Thickness(0,0,0,4)};
        var close=IconButton("close",()=>{_editing=false;Render();},"关闭编辑器",18,PlannerTheme.Muted);close.Name="EditorClose";DockPanel.SetDock(close,Dock.Right);header.Children.Add(close);
        StackPanel heading=new();heading.Children.Add(Text(original==null?"新建日程":"编辑日程",mini?19:24,FontWeights.SemiBold));heading.Children.Add(Text(original==null?"创建一条新的日程安排":original.Repeat==ReminderRepeat.Dates?"调整这组多日期日程安排":"调整这一天的日程",mini?10:12,foreground:PlannerTheme.Muted));
        if(original?.Repeat==ReminderRepeat.Dates)heading.Children.Add(Text($"已发生 {locked.Count} 次 · 未来 {selected.Count-locked.Count} 次",mini?10:12,foreground:PlannerTheme.Muted));header.Children.Add(heading);layout.Children.Add(header);
        header.MouseLeftButtonDown+=(_,e)=>{for(var node=e.OriginalSource as DependencyObject;node!=null&&node!=header;node=VisualTreeHelper.GetParent(node))if(node is Button)return;if(e.LeftButton==MouseButtonState.Pressed){DragMove();e.Handled=true;}};
        StackPanel form=new();var scroll=new ScrollViewer{Name="EditorFormScroll",Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(scroll,1);layout.Children.Add(scroll);
        TextBlock error=Text("",12,foreground:PlannerTheme.Danger);error.Name="EditorError";error.Margin=new Thickness(0,6,0,0);Grid.SetRow(error,2);layout.Children.Add(error);
        void Label(string value,double gap=3){var label=Text(value,mini?12:15,FontWeights.SemiBold);label.Margin=new Thickness(0,gap,0,mini?2:3);form.Children.Add(label);}
        TextBox title=new(){Name="ReminderTitle",Text=original?.Title??"",MaxLength=Math.Max(20,original?.Title.Length??0),Height=mini?38:42,FontSize=mini?13:15,Padding=new Thickness(12,mini?7:9,60,mini?7:9)};
        Grid Counted(TextBox input,int limit,string placeholder)
        {
            Grid host=new();host.Children.Add(input);
            var count=Text(input.Text.Length+"/"+limit,mini?10:13,foreground:PlannerTheme.Muted);count.HorizontalAlignment=HorizontalAlignment.Right;count.VerticalAlignment=VerticalAlignment.Bottom;count.Margin=new Thickness(0,0,12,mini?9:12);count.IsHitTestVisible=false;host.Children.Add(count);
            var hint=Text(placeholder,mini?12:14,foreground:PlannerTheme.Muted);hint.Margin=new Thickness(13,mini?9:12,64,0);hint.VerticalAlignment=VerticalAlignment.Top;hint.IsHitTestVisible=false;host.Children.Add(hint);
            void UpdateCount(){count.Text=input.Text.Length+"/"+limit;count.Foreground=input.Text.Length>limit?PlannerTheme.Danger:PlannerTheme.Muted;hint.Visibility=input.Text.Length==0?Visibility.Visible:Visibility.Collapsed;}
            input.TextChanged+=(_,_)=>UpdateCount();UpdateCount();return host;
        }
        Label("事项标题 *",0);form.Children.Add(Counted(title,20,"请输入事项标题"));
        Label("日期 *");var picker=Action("选择日期",()=>{});picker.Name="ModifyDates";picker.Height=mini?38:42;picker.FontSize=mini?12:15;picker.Margin=new Thickness(0);picker.HorizontalContentAlignment=HorizontalAlignment.Stretch;form.Children.Add(picker);
        InlineDateSelector? selector=null;HashSet<DateTime>? snapshot=null;double baseEditorHeight=mini?380:430,notesExtraHeight=0;
        void Summary()
        {
            DateTime[] days=selected.OrderBy(d=>d).ToArray();double available=Math.Max(150,(picker.ActualWidth>0?picker.ActualWidth:editorWidth)-80);
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
            layout.Height=Math.Min(_shell.Height-54,baseEditorHeight+notesExtraHeight+(notesExtraHeight>.5?26:0));
            Summary();
        }
        picker.Click+=(_,_)=>
        {
            if(selector!=null){CloseDates(false);return;}
            snapshot=new HashSet<DateTime>(selected);
            InlineDateSelector openedSelector=new(selected,locked,_service.Book,selected.Count>0?selected.Min():_date,Summary,()=>CloseDates(false),()=>CloseDates(true));selector=openedSelector;
            openedSelector.HorizontalAlignment=HorizontalAlignment.Stretch;openedSelector.VerticalAlignment=VerticalAlignment.Top;
            double top=picker.TranslatePoint(new Point(0,picker.ActualHeight+3),layout).Y;
            double availableEditorHeight=Math.Max(1,_shell.Height-82);
            double preferredSelectorHeight=_pageSize=="mini"?300:_pageSize=="standard"?330:_pageSize=="comfortable"?350:370;
            openedSelector.Height=Math.Min(preferredSelectorHeight,Math.Max(80,availableEditorHeight-top-4));
            layout.Height=Math.Min(availableEditorHeight,Math.Max(layout.Height,top+openedSelector.Height+4));
            openedSelector.Margin=new Thickness(0,top,0,0);Grid.SetRow(openedSelector,0);Grid.SetRowSpan(openedSelector,4);System.Windows.Controls.Panel.SetZIndex(openedSelector,10);
            foreach(UIElement child in layout.Children)if(Grid.GetRow(child)==3)child.Visibility=Visibility.Hidden;
            layout.Children.Add(openedSelector);Summary();
        };
        void OutsideDates(object sender,MouseButtonEventArgs e){if(selector==null)return;for(var node=e.OriginalSource as DependencyObject;node!=null;node=VisualTreeHelper.GetParent(node))if(node==selector||node==picker)return;CloseDates(false);}
        layout.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape&&selector!=null){CloseDates(true);e.Handled=true;}};
        picker.SizeChanged+=(_,_)=>Summary();Summary();
        bool refreshing=false;
        CheckBox Switch(string name,bool value)=>new(){Name=name,IsChecked=value,Style=(Style)FindResource("PlannerSwitch"),Content=null,Margin=new Thickness(0),VerticalAlignment=VerticalAlignment.Center};
        var setTime=Switch("SetSpecificTime",original?.HasTime==true);var alarm=Switch("CreateAlarm",original?.Enabled==true&&original.HasTime);var early=Switch("EarlyReminder",original?.EarlyEnabled==true);
        Grid Option(string label,CheckBox toggle,UIElement? input=null,bool indent=false)
        {
            Grid row=new(){Height=mini?34:38};row.ColumnDefinitions.Add(new(){Width=new GridLength(mini?104:120)});row.ColumnDefinitions.Add(new(){Width=new GridLength(mini?62:70)});row.ColumnDefinitions.Add(new());
            var text=Text(label,mini?12:15,indent?null:FontWeights.SemiBold,indent?PlannerTheme.Muted:PlannerTheme.Ink);text.Margin=new Thickness(indent?(mini?18:24):0,0,0,0);row.Children.Add(text);Grid.SetColumn(toggle,1);row.Children.Add(toggle);if(input!=null){Grid.SetColumn(input,2);row.Children.Add(input);}return row;
        }
        TextBox hour=new(){Name="ScheduleHour",Text=original?.HasTime==true?original.Start.ToString("HH"):"",Width=mini?36:42,FontSize=mini?13:15,MaxLength=2,Height=mini?31:34,BorderThickness=new Thickness(0),Background=Brushes.Transparent,TextAlignment=TextAlignment.Center,Padding=new Thickness(4,4,4,4)};
        TextBox minute=new(){Name="ScheduleMinute",Text=original?.HasTime==true?original.Start.ToString("mm"):"",Width=mini?36:42,FontSize=mini?13:15,MaxLength=2,Height=mini?31:34,BorderThickness=new Thickness(0),Background=Brushes.Transparent,TextAlignment=TextAlignment.Center,Padding=new Thickness(4,4,4,4)};
        Grid Segment(TextBox input){Grid g=new();g.Children.Add(input);var hint=Text("--",14,foreground:PlannerTheme.Muted);hint.HorizontalAlignment=HorizontalAlignment.Center;hint.IsHitTestVisible=false;g.Children.Add(hint);input.TextChanged+=(_,_)=>hint.Visibility=input.Text.Length==0?Visibility.Visible:Visibility.Collapsed;hint.Visibility=input.Text.Length==0?Visibility.Visible:Visibility.Collapsed;return g;}
        StackPanel digits=Row();digits.HorizontalAlignment=HorizontalAlignment.Center;digits.Children.Add(Segment(hour));digits.Children.Add(Text(":",15));digits.Children.Add(Segment(minute));
        Border timeField=new(){Name="ScheduleTimeField",Child=digits,Width=mini?108:124,Height=mini?34:38,CornerRadius=new CornerRadius(18),BorderBrush=PlannerTheme.ControlLine,BorderThickness=new Thickness(1),Background=PlannerTheme.Soft,HorizontalAlignment=HorizontalAlignment.Left};
        var timeRow=Option("设置时间",setTime,timeField);timeRow.Margin=new Thickness(0,2,0,0);Grid.SetColumn(timeField,1);Grid.SetColumnSpan(timeField,2);timeField.Margin=new Thickness(72,0,0,0);form.Children.Add(timeRow);
        var alarmRow=Option("创建闹钟提醒",alarm);form.Children.Add(alarmRow);
        TextBox lead=CreateEarlyMinutesInput(original?.EarlyMinutes??30);StackPanel leadField=Row();leadField.Children.Add(lead);leadField.Children.Add(Text("分钟",12,foreground:PlannerTheme.Muted));var earlyRow=Option("提前提醒",early,leadField,true);form.Children.Add(earlyRow);
        var segmentedTime=new PlannerTimeInput(hour,minute);
        bool ValidTime(out TimeSpan value)=>CalendarEditing.TryTime(hour.Text,minute.Text,out value);
        void Refresh()
        {
            bool timed=setTime.IsChecked==true,valid=ValidTime(out _);timeField.Visibility=timed?Visibility.Visible:Visibility.Collapsed;
            alarmRow.Visibility=timed&&valid?Visibility.Visible:Visibility.Collapsed;earlyRow.Visibility=timed&&valid&&alarm.IsChecked==true?Visibility.Visible:Visibility.Collapsed;lead.IsEnabled=early.IsChecked==true;
            timeField.BorderBrush=timed&&!valid&&(hour.Text.Length>0||minute.Text.Length>0)?PlannerTheme.Danger:PlannerTheme.ControlLine;
            double naturalHeight=original?.Repeat==ReminderRepeat.Dates?(mini?510:560):timed&&valid&&alarm.IsChecked==true?(mini?465:515):timed?(mini?420:465):(mini?380:430);
            baseEditorHeight=naturalHeight;
            double contentHeight=naturalHeight+notesExtraHeight+(notesExtraHeight>.5?26:0);
            layout.Height=Math.Min(_shell.Height-54,selector==null?contentHeight:Math.Max(contentHeight,selector.Margin.Top+selector.Height+4));
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
        Label("备注（可选）",4);double notesBase=mini?38:42,notesMaximum=mini?82:90;TextBox notes=new(){Name="ReminderNotes",Text=original?.Notes??"",MaxLength=Math.Max(100,original?.Notes.Length??0),AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,Height=notesBase,MinHeight=notesBase,MaxHeight=notesMaximum,FontSize=mini?13:15,Padding=new Thickness(12,mini?7:9,65,mini?7:9),VerticalScrollBarVisibility=ScrollBarVisibility.Hidden};form.Children.Add(Counted(notes,100,"写一点备注…"));
        bool sizingNotes=false;
        void ResizeNotes(){if(sizingNotes)return;sizingNotes=true;try{int lines=Math.Max(notes.Text.Count(c=>c=='\n')+1,notes.LineCount);notes.Height=Math.Min(notesMaximum,notesBase+(mini?22:24)*(Math.Min(lines,3)-1));notesExtraHeight=notes.Height-notesBase;notes.VerticalScrollBarVisibility=lines>3?ScrollBarVisibility.Auto:ScrollBarVisibility.Hidden;if(selector==null)layout.Height=Math.Min(_shell.Height-54,baseEditorHeight+notesExtraHeight+(notesExtraHeight>.5?26:0));}finally{sizingNotes=false;}}
        notes.TextChanged+=(_,_)=>Dispatcher.BeginInvoke(new Action(ResizeNotes),System.Windows.Threading.DispatcherPriority.Background);
        notes.Loaded+=(_,_)=>ResizeNotes();
        foreach(var input in new[]{title,notes,hour,minute,lead})input.TextChanged+=(_,_)=>error.Text="";
        StackPanel actions=new(){Margin=new Thickness(0,6,0,0)};Grid.SetRow(actions,3);layout.Children.Add(actions);
        StackPanel primaryActions=Row();primaryActions.HorizontalAlignment=HorizontalAlignment.Right;
        var save=AsyncAction(original==null?"创建日程":"保存修改",async()=>
        {
            try
            {
                CloseDates(false);Normalize();error.Text="";
                if(string.IsNullOrWhiteSpace(title.Text)||title.Text.Trim().Length>20)throw new ArgumentException("事项标题必填，最多20字。");
                if(notes.Text.Length>100&&notes.Text!=original?.Notes)throw new ArgumentException("备注最多100字。");
                if(selected.Count==0)throw new ArgumentException("请选择至少一个日期。");
                TimeSpan time=TimeSpan.Zero;if(setTime.IsChecked==true&&!ValidTime(out time))throw new ArgumentException("请填写有效时间（00:00～23:59）。");
                int advance=30;if(early.IsChecked==true&&alarm.IsChecked==true&&(!int.TryParse(lead.Text,out advance)||advance<1||advance>60))throw new ArgumentException("提前提醒请输入1～60分钟。");
                var dates=selected.OrderBy(d=>d).ToList();bool on=setTime.IsChecked==true&&alarm.IsChecked==true;
                ReminderItem item=new(){Id=original?.Id??Guid.NewGuid(),Calendar=true,Title=title.Text.Trim(),Notes=notes.Text,Start=dates[0]+time,HasTime=setTime.IsChecked==true,Dates=dates,Repeat=dates.Count>1||original?.Repeat==ReminderRepeat.Dates?ReminderRepeat.Dates:ReminderRepeat.Once,Enabled=on,ReminderCreated=on,EarlyEnabled=on&&early.IsChecked==true,EarlyMinutes=advance};
                await Execute(b=>CalendarEditing.Save(b,item,DateTime.Now));_editing=false;Render();
            }
            catch(Exception ex)when(ex is ArgumentException or System.IO.IOException or UnauthorizedAccessException){error.Text=ex is ArgumentException?ex.Message:"保存失败，原数据保留，请重试。";}
        });save.Name="SaveReminder";save.Width=mini?(original==null?138:110):original==null?160:128;save.Height=mini?43:48;save.FontSize=mini?13:15;
        var cancel=Action("取消",()=>{_editing=false;Render();});cancel.Name="CancelScheduleEdit";cancel.Width=mini?(original==null?116:96):original==null?140:112;cancel.Height=mini?43:48;cancel.FontSize=mini?13:15;primaryActions.Children.Add(cancel);primaryActions.Children.Add(Primary(save));
        if(original!=null)
        {
            StackPanel dangerActions=Row();dangerActions.Margin=new Thickness(0,0,0,4);
            Button DangerAction(string name,string label,Brush foreground,Brush background,Action action){var b=Action(label,action);b.Name=name;b.Width=mini?(name=="DeleteOccurrence"?90:112):name=="DeleteOccurrence"?104:130;b.FontSize=mini?12:14;b.Padding=new Thickness(6,5,6,5);b.Margin=new Thickness(0,3,mini?4:6,3);b.Height=mini?38:42;b.Foreground=foreground;b.Background=background;b.BorderBrush=foreground;return b;}
            if(original.Repeat==ReminderRepeat.Dates&&context is DateTime current&&CalendarEditing.Dates(original).Contains(current.Date))dangerActions.Children.Add(DangerAction("DeleteOccurrence","删除今天",PlannerTheme.Danger,Brushes.White,()=>ConfirmScheduleAction("删除今天的日程？",$"仅删除 {current:M月d日} 的日程，其他日期保持不变。",async()=>{await Execute(b=>ReminderSchedule.DeleteDate(b,original.Id,current));_editing=false;Render();})));
            if(original.Repeat==ReminderRepeat.Dates&&locked.Count>0&&selected.Count>locked.Count)dangerActions.Children.Add(DangerAction("DeleteFutureSchedules","删除后续日程",new SolidColorBrush(Color.FromRgb(190,105,32)),new SolidColorBrush(Color.FromRgb(255,247,236)),()=>ConfirmScheduleAction("删除后续日程？",$"将删除未来 {CalendarEditing.Dates(original).Count(d=>!CalendarEditing.IsPast(original,d,DateTime.Now))} 次安排，已发生的 {CalendarEditing.Dates(original).Count(d=>CalendarEditing.IsPast(original,d,DateTime.Now))} 次记录会继续保留。",async()=>{await Execute(b=>CalendarEditing.DeleteFuture(b,original.Id,DateTime.Now));_editing=false;Render();})));
            dangerActions.Children.Add(DangerAction("DeleteGroup",original.Repeat==ReminderRepeat.Dates?"删除全部日程":"删除日程",PlannerTheme.Danger,PlannerTheme.DangerSoft,()=>ConfirmScheduleAction("删除全部日程？",$"将删除已发生的 {CalendarEditing.Dates(original).Count(d=>CalendarEditing.IsPast(original,d,DateTime.Now))} 次记录和未来 {CalendarEditing.Dates(original).Count(d=>!CalendarEditing.IsPast(original,d,DateTime.Now))} 次安排，此操作不可恢复。",async()=>{await Execute(b=>ReminderSchedule.DeleteGroups(b,new[]{original.Id}));_editing=false;Render();})));
            actions.Children.Add(dangerActions);
        }
        actions.Children.Add(primaryActions);
        ShowOverlay(layout,true);layout.UpdateLayout();ResizeNotes();((Grid)_shell.Children[_shell.Children.Count-1]).PreviewMouseDown+=OutsideDates;title.Focus();
    }

    private void ConfirmScheduleAction(string title,string explanation,Func<Task> confirmed)
    {
        StackPanel panel=new(){Width=420};panel.Children.Add(Text(title,20,FontWeights.SemiBold));var detail=Text(explanation,14,foreground:PlannerTheme.Muted);detail.TextWrapping=TextWrapping.Wrap;detail.Margin=new Thickness(0,16,0,20);panel.Children.Add(detail);
        StackPanel actions=Row();actions.HorizontalAlignment=HorizontalAlignment.Right;var cancel=Action("取消",CloseTopOverlay);cancel.Name="CancelScheduleAction";cancel.Width=112;cancel.Height=48;cancel.FontSize=15;actions.Children.Add(cancel);
        var confirm=AsyncAction("确认",async()=>{CloseTopOverlay();await confirmed();});confirm.Name="ConfirmScheduleAction";confirm.Width=112;confirm.Height=48;confirm.FontSize=15;confirm.Background=PlannerTheme.Danger;confirm.Foreground=Brushes.White;actions.Children.Add(confirm);panel.Children.Add(actions);ShowOverlay(panel);
    }
}
