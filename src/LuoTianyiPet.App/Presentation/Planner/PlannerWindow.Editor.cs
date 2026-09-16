using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Brush=System.Windows.Media.Brush;
using LuoTianyiPet.Core;
using Button=System.Windows.Controls.Button;
using CheckBox=System.Windows.Controls.CheckBox;
using TextBox=System.Windows.Controls.TextBox;
using ComboBox=System.Windows.Controls.ComboBox;
using Panel=System.Windows.Controls.Panel;
using HorizontalAlignment=System.Windows.HorizontalAlignment;
namespace LuoTianyiPet.App;
internal sealed partial class PlannerWindow
{
    private void Edit(ReminderItem? original,bool calendar) => Edit(original,calendar,null);

    private void Edit(ReminderItem? original,bool calendar,DateTime? presetDate)
    {
        _editing=true;
        StackPanel panel=new(){Width=calendar?574:620};
        DockPanel head=new();
        var close=IconButton("close",()=>{_editing=false;Render();},"关闭",18,PlannerTheme.Muted);close.Name="EditorClose";DockPanel.SetDock(close,Dock.Right);head.Children.Add(close);
        string editorTitle=original==null?(calendar?"创建日程":"新建提醒"):(calendar?"编辑日程":"编辑提醒");
        if(calendar)
        {
            StackPanel heading=new();heading.Children.Add(Text(editorTitle,22,FontWeights.SemiBold));
            var subtitle=Text(original==null?"为这一天添加一个事项":"调整这一天的日程",12,foreground:PlannerTheme.Muted);
            subtitle.Margin=new Thickness(0,2,0,0);heading.Children.Add(subtitle);head.Children.Add(heading);
        }
        panel.Children.Add(head);
        Border divider=new(){Height=1,Background=PlannerTheme.Line,Margin=calendar?new Thickness(0,16,0,12):new Thickness(0,6,0,8)};panel.Children.Add(divider);
        TextBlock error=Text("",12);error.Name="EditorError";error.Foreground=PlannerTheme.Danger;error.Margin=new Thickness(3,8,3,0);
        bool relative=original?.Relative==true;
        Border modeCapsule=new(){Background=Brushes.White,CornerRadius=new CornerRadius(10),BorderBrush=PlannerTheme.ControlLine,BorderThickness=new Thickness(1),Padding=new Thickness(3),Margin=new Thickness(0),Width=340,Height=44,HorizontalAlignment=HorizontalAlignment.Left};
        var mode=Row();mode.HorizontalAlignment=HorizontalAlignment.Center;
        StackPanel specifiedLabel=Row();specifiedLabel.Children.Add(PlannerTheme.Icon("alarm",16,PlannerTheme.Ink,7));specifiedLabel.Children.Add(Text("闹钟",14,FontWeights.SemiBold));
        StackPanel countdownLabel=Row();countdownLabel.Children.Add(PlannerTheme.Icon("hourglass",16,PlannerTheme.Ink,7));countdownLabel.Children.Add(Text("倒计时",14,FontWeights.SemiBold));
        var specified=new Button{Style=(Style)FindResource("PlannerSegment"),Content=specifiedLabel,Width=165,MinWidth=0,Height=36,Margin=new Thickness(1)};
        var countdown=new Button{Style=(Style)FindResource("PlannerSegment"),Content=countdownLabel,Width=165,MinWidth=0,Height=36,Margin=new Thickness(1)};
        mode.Children.Add(specified);mode.Children.Add(countdown);modeCapsule.Child=mode;
        if(!calendar){DockPanel.SetDock(modeCapsule,Dock.Left);head.Children.Add(modeCapsule);head.Height=44;head.Background=Brushes.Transparent;head.MouseLeftButtonDown+=(_,e)=>{DependencyObject? source=e.OriginalSource as DependencyObject;bool interactive=false;while(source is not null&&source!=head){if(source is Button or TextBox or ComboBox){interactive=true;break;}source=System.Windows.Media.VisualTreeHelper.GetParent(source);}if(!interactive&&e.LeftButton==System.Windows.Input.MouseButtonState.Pressed){DragMove();e.Handled=true;}};}
        StackPanel form=new();panel.Children.Add(form);
        TextBlock Counter(string value){var t=Text(value,11,foreground:PlannerTheme.Muted);t.HorizontalAlignment=HorizontalAlignment.Right;t.Margin=new Thickness(3,3,3,0);return t;}
        StackPanel BoxWith(UIElement input,TextBlock count){StackPanel box=new(){Margin=new Thickness(3)};box.Children.Add(input);box.Children.Add(count);return box;}
        UIElement WithPlaceholder(TextBox input,string hint)
        {
            Grid g=new();TextBlock ph=new(){Text=hint,Foreground=PlannerTheme.Muted,Margin=new Thickness(14,11,0,0),IsHitTestVisible=false,FontSize=14};
            Button? clear=hint=="请输入提醒名称"?new Button{Name="ClearReminderTitle",Content="×",Style=(Style)FindResource("PlannerClear"),Width=21,Height=21,FontSize=16,FontWeight=FontWeights.SemiBold,Foreground=Brushes.White,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,10,0),Cursor=System.Windows.Input.Cursors.Hand}:null;
            void Sync(){ph.Visibility=input.Text.Length==0?Visibility.Visible:Visibility.Collapsed;if(clear!=null)clear.Visibility=input.Text.Length==0?Visibility.Collapsed:Visibility.Visible;}input.TextChanged+=(_,_)=>Sync();Sync();g.Children.Add(input);g.Children.Add(ph);if(clear!=null){clear.Click+=(_,_)=>input.Clear();g.Children.Add(clear);}return g;
        }
        int titleLimit=30;int inputLimit=Math.Max(titleLimit,original?.Title.Length??0);TextBox name=new(){Height=42,Name="ReminderTitle",Text=original?.Title??"",MaxLength=inputLimit};TextBlock nameCount=Counter($"{name.Text.Length}/{titleLimit}");name.TextChanged+=(_,_)=>nameCount.Text=$"{name.Text.Length}/{titleLimit}";StackPanel nameBox=BoxWith(WithPlaceholder(name,calendar?"请输入日程标题":"请输入提醒名称"),nameCount);
        string legacyNotes=original?.Notes??"";if(original?.Content is string merged){var line=merged.IndexOf('\n');if(line>=0)legacyNotes=merged.Substring(line+1);}
        int notesLimit=calendar?200:300;TextBox notes=new(){Name="ReminderNotes",Text=legacyNotes,MaxLength=Math.Max(notesLimit,legacyNotes.Length),AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=calendar?42:92,MaxHeight=calendar?112:130,Height=calendar?42:double.NaN,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};TextBlock notesCount=Counter($"{notes.Text.Length}/{notesLimit}");notes.TextChanged+=(_,_)=>notesCount.Text=$"{notes.Text.Length}/{notesLimit}";StackPanel notesBox=BoxWith(WithPlaceholder(notes,"说点什么吧……"),notesCount);
        DateTime? initialDate=calendar?(presetDate?.Date??_occurrenceDate?.Date??original?.Start.Date??DateTime.Today):original?.Start.Date??_date.Date;
        DateTime start=original?.Start??(initialDate??DateTime.Today).Date.AddHours(Math.Min(23,DateTime.Now.Hour+1));
        DatePicker date=new(){Name="ReminderDate",SelectedDate=initialDate,SelectedDateFormat=DatePickerFormat.Long,DisplayDate=initialDate??_date,DisplayDateStart=ReminderSchedule.MinimumDate,DisplayDateEnd=ReminderSchedule.MaximumDate,Width=calendar?260:230,Margin=new Thickness(3),VerticalContentAlignment=VerticalAlignment.Center,Foreground=System.Windows.Media.Brushes.Transparent};
        string FormatDate(DateTime? selected)
        {
            if(selected is not DateTime value)return "选择日期";
            int weekday=(int)value.DayOfWeek==0?6:(int)value.DayOfWeek-1;
            const string weekdays="一二三四五六日";
            return $"{value:yyyy年M月d日}  周{weekdays[weekday]}";
        }
        TextBlock dateCaption=new(){Text=FormatDate(initialDate),FontSize=15,Foreground=PlannerTheme.Ink,Margin=new Thickness(14,2,40,2),VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false};
        Grid dateField=new(){Width=calendar?260:230,Margin=new Thickness(0)};dateField.Children.Add(date);dateField.Children.Add(dateCaption);
        date.SelectedDateChanged+=(_,_)=>dateCaption.Text=FormatDate(date.SelectedDate);
        TextBox time=new(){Height=42,Name="ReminderTime",Text=start.ToString("HH:mm"),Width=110,Margin=new Thickness(3)};
        Grid alarmDateTime=new(){Margin=new Thickness(3)};alarmDateTime.ColumnDefinitions.Add(new());alarmDateTime.ColumnDefinitions.Add(new(){Width=new GridLength(120)});
        bool timeOn=calendar?original?.HasTime==true:true;
        CheckBox specificTime=new(){Name="SetSpecificTime",Content="设置具体时间",IsChecked=timeOn,Style=(Style)FindResource("PlannerSwitch"),Margin=new Thickness(3,8,3,8)};
        ComboBox repeat=new(){Name="ReminderRepeat",ItemsSource=Repeats,SelectedIndex=(int)(original?.Repeat??ReminderRepeat.Once),Margin=new Thickness(3)};
        var checks=Days.Select(d=>new CheckBox{Content="周"+"日一二三四五六"[(int)d],IsChecked=original?.Weekdays.Contains(d)==true,Margin=new Thickness(5)}).ToArray();StackPanel weekdays=Row();foreach(var c in checks)weekdays.Children.Add(c);
        List<DateTime> selectedDates=original?.Dates.Count>0?original.Dates.Select(d=>d.Date).Distinct().OrderBy(d=>d).ToList():calendar&&initialDate is DateTime selected?[selected.Date]:[];
        Button picker=null!;picker=Action("",()=>{DateSelectionWindow w=new(selectedDates,date.SelectedDate??initialDate??_date,!calendar){Owner=this};if(w.ShowDialog()==true){selectedDates=w.Selection.OrderBy(d=>d).ToList();UpdateDateSummary();}});picker.Name="ModifyDates";picker.Height=calendar?42:48;picker.HorizontalContentAlignment=HorizontalAlignment.Left;picker.ToolTip="点击选择一个或多个日期";
        if(calendar)
        {
            picker.MouseEnter+=(_,_)=>{picker.Background=PlannerTheme.AccentSoft;picker.BorderBrush=PlannerTheme.Accent;};
            picker.MouseLeave+=(_,_)=>{picker.Background=Brushes.White;picker.BorderBrush=PlannerTheme.Line;};
            picker.GotKeyboardFocus+=(_,_)=>picker.BorderBrush=PlannerTheme.Accent;
            picker.LostKeyboardFocus+=(_,_)=>picker.BorderBrush=PlannerTheme.Line;
        }
        void UpdateDateSummary()
        {
            DateTime[] sorted=selectedDates.Select(d=>d.Date).Distinct().OrderBy(d=>d).ToArray();
            bool acrossYears=sorted.Length>1&&sorted.Select(d=>d.Year).Distinct().Count()>1;
            int visible=sorted.Length==1?1:acrossYears?(calendar?2:3):(calendar?3:4);
            StackPanel summary=Row();summary.Children.Add(PlannerTheme.Icon("calendar",17,PlannerTheme.Accent,9));
            if(sorted.Length==0)summary.Children.Add(Text("选择日期",14,foreground:PlannerTheme.Muted));
            else if(sorted.Length==1)
            {
                int weekday=sorted[0].DayOfWeek==DayOfWeek.Sunday?6:(int)sorted[0].DayOfWeek-1;
                summary.Children.Add(Text($"{sorted[0]:yyyy年M月d日} 星期{"一二三四五六日"[weekday]}",14,foreground:PlannerTheme.Ink));
            }
            else
            {
                foreach(DateTime day in sorted.Take(visible))
                {
                    var label=Text(acrossYears?day.ToString("yyyy/MM/dd"):day.ToString("M月d日"),14,foreground:PlannerTheme.Ink);
                    label.Margin=new Thickness(0,0,12,0);summary.Children.Add(label);
                }
                if(sorted.Length>visible)
                {
                    var more=Text($"+{sorted.Length-visible}天",12,FontWeights.SemiBold,PlannerTheme.Accent);
                    summary.Children.Add(new Border{Background=PlannerTheme.AccentSoft,CornerRadius=new CornerRadius(5),Padding=new Thickness(7,3,7,3),Child=more});
                }
            }
            picker.Content=summary;
            picker.ToolTip=sorted.Length==0?"点击选择日期":string.Join("、",sorted.Select(d=>d.ToString("yyyy年M月d日")));
        }
        UpdateDateSummary();
        var enabled=new CheckBox{Name="CreateAlarm",Content="创建闹钟提醒",IsChecked=original?.Enabled==true,Style=(Style)FindResource("PlannerSwitch"),Margin=new Thickness(3,8,3,8)};
        var early=new CheckBox{Name="EarlyReminder",Content=calendar?"提前提醒":null,IsChecked=original?.EarlyEnabled==true,Style=(Style)FindResource("PlannerSwitch"),Margin=new Thickness(3,8,3,8)};
        TextBox lead=new(){Height=42,Name="EarlyMinutes",Text=(original?.EarlyMinutes??30).ToString(),Width=80,Margin=new Thickness(3)};StackPanel leadRow=Row();leadRow.Children.Add(lead);leadRow.Children.Add(Text("分钟 · 到点仍会提醒",12));
        int[] leadOptions=[15,30,60,120];ComboBox leadPreset=new(){ItemsSource=new[]{"15分钟","30分钟","1小时","2小时","自定义"},Width=136,Margin=new Thickness(3),IsEnabled=early.IsChecked==true};int leadIndex=Array.IndexOf(leadOptions,original?.EarlyMinutes??30);leadPreset.SelectedIndex=leadIndex>=0?leadIndex:4;
        StackPanel alarmLeadRow=Row();alarmLeadRow.Children.Add(leadPreset);alarmLeadRow.Children.Add(Text("在闹钟时间前进行提醒",12,foreground:PlannerTheme.Muted));
        leadPreset.SelectionChanged+=(_,_)=>{if(leadPreset.SelectedIndex>=0&&leadPreset.SelectedIndex<4)lead.Text=leadOptions[leadPreset.SelectedIndex].ToString();lead.Visibility=leadPreset.SelectedIndex==4?Visibility.Visible:Visibility.Collapsed;};
        Button? save=null,cancel=null;
        int seconds=original?.DurationSeconds??_service.Book.LastDurationSeconds;PlannerNumber hours=new("CountdownHours",seconds/3600,24),minutes=new("CountdownMinutes",seconds/60%60,59),secs=new("CountdownSeconds",seconds%60,59);StackPanel digits=Row();digits.HorizontalAlignment=HorizontalAlignment.Center;
        foreach(var tuple in new[]{("小时",hours),("分钟",minutes),("秒",secs)})
        {
            if(digits.Children.Count>0){Grid colon=new(){Width=24,Height=138,Margin=new Thickness(0,25,0,0)};var mark=Text(":",24,FontWeights.SemiBold,PlannerTheme.Ink);mark.HorizontalAlignment=HorizontalAlignment.Center;mark.VerticalAlignment=VerticalAlignment.Center;colon.Children.Add(mark);digits.Children.Add(colon);}
            StackPanel column=new(){Width=104};var label=Text(tuple.Item1,13,foreground:PlannerTheme.Muted);label.TextAlignment=TextAlignment.Center;label.Margin=new Thickness(3,0,3,6);column.Children.Add(label);column.Children.Add(tuple.Item2);digits.Children.Add(column);
        }
        List<(int Seconds,Button Button)> presets=[];bool adjustingDuration=false;
        void RefreshDuration()
        {
            if(adjustingDuration)return;adjustingDuration=true;
            if(hours.Value==24){if(minutes.Value!=0)minutes.Input.Text="00";if(secs.Value!=0)secs.Input.Text="00";minutes.SetEnabled(false);secs.SetEnabled(false);}else{minutes.SetEnabled(true);secs.SetEnabled(true);}
            int duration=Math.Max(0,hours.Value)*3600+Math.Max(0,minutes.Value)*60+Math.Max(0,secs.Value);
            foreach(var pair in presets){bool selected=pair.Seconds==duration;pair.Button.Background=selected?PlannerTheme.AccentSoft:Brushes.White;pair.Button.Foreground=selected?PlannerTheme.Accent:PlannerTheme.Ink;pair.Button.FontWeight=selected?FontWeights.SemiBold:FontWeights.Normal;pair.Button.BorderBrush=selected?PlannerTheme.Accent:PlannerTheme.ControlLine;}
            if(save!=null)save.IsEnabled=!relative||duration>0;adjustingDuration=false;
        }
        StackPanel quick=Row();quick.HorizontalAlignment=HorizontalAlignment.Stretch;foreach(var preset in new[]{(300,"5分钟"),(900,"15分钟"),(1800,"30分钟"),(3600,"1小时"),(7200,"2小时")}){var button=Action(preset.Item2,()=>{hours.Input.Text=(preset.Item1/3600).ToString("00");minutes.Input.Text=(preset.Item1/60%60).ToString("00");secs.Input.Text="00";});button.Style=(Style)FindResource("PlannerPill");button.MinHeight=0;button.Width=108;button.Height=40;button.Margin=new Thickness(3);button.BorderBrush=PlannerTheme.ControlLine;quick.Children.Add(button);presets.Add((preset.Item1,button));}foreach(var input in new[]{hours.Input,minutes.Input,secs.Input})input.TextChanged+=(_,_)=>RefreshDuration();RefreshDuration();
        void ColorMode(StackPanel label,Brush color){foreach(var child in label.Children){if(child is System.Windows.Shapes.Path path)path.Stroke=color;if(child is TextBlock textBlock)textBlock.Foreground=color;}}
        if(calendar)notes.TextChanged+=(_,_)=>{
            int explicitLines=notes.Text.Count(c=>c=='\n')+1;
            notes.Height=Math.Min(112,42+(explicitLines-1)*22);
            notes.Dispatcher.BeginInvoke(new Action(()=>
            {
                int lines=Math.Max(explicitLines,notes.LineCount);
                notes.Height=Math.Min(112,42+(lines-1)*22);
            }),System.Windows.Threading.DispatcherPriority.Background);
        };
        void Update()
        {
            form.Children.Clear();error.Text="";specified.Background=!relative?PlannerTheme.AccentSoft:Brushes.Transparent;specified.Foreground=!relative?PlannerTheme.Accent:PlannerTheme.Muted;specified.FontWeight=!relative?FontWeights.SemiBold:FontWeights.Normal;specified.BorderBrush=!relative?PlannerTheme.Accent:PlannerTheme.Line;specified.BorderThickness=!relative?new Thickness(1):new Thickness(0);countdown.Background=relative?PlannerTheme.AccentSoft:Brushes.Transparent;countdown.Foreground=relative?PlannerTheme.Accent:PlannerTheme.Muted;countdown.FontWeight=relative?FontWeights.SemiBold:FontWeights.Normal;countdown.BorderBrush=relative?PlannerTheme.Accent:PlannerTheme.Line;countdown.BorderThickness=relative?new Thickness(1):new Thickness(0);
            ColorMode(specifiedLabel,!relative?PlannerTheme.Accent:PlannerTheme.Muted);ColorMode(countdownLabel,relative?PlannerTheme.Accent:PlannerTheme.Muted);
            foreach(var element in new UIElement[]{nameBox,dateField,time,alarmDateTime,notesBox,repeat,specificTime,enabled,early,leadRow,alarmLeadRow,weekdays,quick,digits,picker})if(LogicalTreeHelper.GetParent(element) is System.Windows.Controls.Panel parent)parent.Children.Remove(element);
            if(calendar)
            {
                foreach(var input in new UIElement[]{name,notes,lead})if(LogicalTreeHelper.GetParent(input) is Panel owner)owner.Children.Remove(input);
                StackPanel Label(string text){StackPanel label=new(){Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,6)};label.Children.Add(Text(text,14,FontWeights.SemiBold));return label;}
                Grid CountedInput(TextBox input,TextBlock count)
                {
                    Grid box=new();input.Padding=new Thickness(12,10,65,10);box.Children.Add(input);
                    count.VerticalAlignment=VerticalAlignment.Bottom;count.HorizontalAlignment=HorizontalAlignment.Right;count.Margin=new Thickness(0,0,11,10);
                    count.IsHitTestVisible=false;box.Children.Add(count);return box;
                }
                if(LogicalTreeHelper.GetParent(nameCount) is Panel nameCountOwner)nameCountOwner.Children.Remove(nameCount);
                if(LogicalTreeHelper.GetParent(notesCount) is Panel notesCountOwner)notesCountOwner.Children.Remove(notesCount);
                form.Children.Add(Label("事项标题 *"));form.Children.Add(CountedInput(name,nameCount));
                Grid dateTime=new(){Margin=new Thickness(0,16,0,12)};
                dateTime.ColumnDefinitions.Add(new(){Width=new GridLength(3,GridUnitType.Star)});
                dateTime.ColumnDefinitions.Add(new(){Width=new GridLength(10)});
                dateTime.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
                StackPanel dateColumn=new();dateColumn.Children.Add(Label("日期"));picker.HorizontalAlignment=HorizontalAlignment.Stretch;picker.Width=double.NaN;dateColumn.Children.Add(picker);
                Grid.SetColumn(dateColumn,0);dateTime.Children.Add(dateColumn);
                StackPanel timeColumn=new();timeColumn.Children.Add(Label("时间"));
                Grid timeField=new(){Height=42};
                if(timeOn)
                {
                    time.Width=double.NaN;time.Margin=new Thickness(0);time.Padding=new Thickness(12,10,28,10);
                    timeField.Children.Add(time);
                    var clearTime=Action("×",()=>{specificTime.IsChecked=false;timeOn=false;Update();});
                    clearTime.Name="ClearScheduleTime";clearTime.Width=25;clearTime.Height=30;clearTime.Padding=new Thickness(0);clearTime.BorderThickness=new Thickness(0);clearTime.Background=Brushes.Transparent;clearTime.Foreground=PlannerTheme.Muted;clearTime.HorizontalAlignment=HorizontalAlignment.Right;clearTime.Margin=new Thickness(0,0,6,0);
                    timeField.Children.Add(clearTime);
                }
                else
                {
                    var addTime=Action("添加时间",()=>{specificTime.IsChecked=true;timeOn=true;Update();});
                    addTime.Name="AddScheduleTime";addTime.Height=42;addTime.HorizontalAlignment=HorizontalAlignment.Stretch;
                    addTime.Foreground=PlannerTheme.Accent;addTime.Background=PlannerTheme.AccentSoft;addTime.BorderBrush=PlannerTheme.Accent;addTime.BorderThickness=new Thickness(1);timeField.Children.Add(addTime);
                }
                timeColumn.Children.Add(timeField);Grid.SetColumn(timeColumn,2);dateTime.Children.Add(timeColumn);form.Children.Add(dateTime);
                specificTime.IsChecked=timeOn;specificTime.Visibility=Visibility.Collapsed;form.Children.Add(specificTime);
                form.Children.Add(new Border{Height=1,Background=PlannerTheme.Line,Margin=new Thickness(0,0,0,10)});
                enabled.Content=null;enabled.Margin=new Thickness(0);enabled.IsEnabled=timeOn;enabled.IsChecked=timeOn&&enabled.IsChecked==true;
                Grid alarmRow=new(){Width=300,HorizontalAlignment=HorizontalAlignment.Left,Height=38};
                alarmRow.ColumnDefinitions.Add(new(){Width=new GridLength(168)});alarmRow.ColumnDefinitions.Add(new(){Width=new GridLength(50)});
                StackPanel alarmLabel=Row();alarmLabel.Children.Add(PlannerTheme.Icon("bell",17,PlannerTheme.Accent,9));alarmLabel.Children.Add(Text("创建闹钟提醒",14,FontWeights.SemiBold));alarmLabel.VerticalAlignment=VerticalAlignment.Center;alarmRow.Children.Add(alarmLabel);
                Grid.SetColumn(enabled,1);enabled.VerticalAlignment=VerticalAlignment.Center;alarmRow.Children.Add(enabled);form.Children.Add(alarmRow);
                if(!timeOn){var hint=Text("设置具体时间后可创建闹钟提醒",12,foreground:PlannerTheme.Muted);hint.Margin=new Thickness(27,0,0,3);form.Children.Add(hint);}
                if(enabled.IsChecked==true)
                {
                    early.Content=null;early.Margin=new Thickness(0);early.IsEnabled=true;
                    StackPanel earlyRow=Row();earlyRow.Width=330;earlyRow.HorizontalAlignment=HorizontalAlignment.Left;earlyRow.Height=44;earlyRow.Margin=new Thickness(32,0,0,0);
                    var earlyLabel=Text("提前提醒",13,foreground:PlannerTheme.Muted);earlyLabel.Width=128;earlyRow.Children.Add(earlyLabel);
                    early.VerticalAlignment=VerticalAlignment.Center;earlyRow.Children.Add(early);
                    StackPanel duration=Row();duration.Children.Add(lead);duration.Children.Add(Text("分钟",12,foreground:PlannerTheme.Muted));
                    lead.Width=70;lead.Height=36;lead.Margin=new Thickness(0,0,5,0);lead.Padding=new Thickness(8,6,8,6);lead.IsEnabled=early.IsChecked==true;
                    duration.VerticalAlignment=VerticalAlignment.Center;duration.Margin=new Thickness(8,0,0,0);earlyRow.Children.Add(duration);form.Children.Add(earlyRow);
                }
                form.Children.Add(new Border{Height=1,Background=PlannerTheme.Line,Margin=new Thickness(0,9,0,14)});
                form.Children.Add(Label("备注（可选）"));notes.Padding=new Thickness(12,10,67,10);form.Children.Add(CountedInput(notes,notesCount));
                if(save!=null){save.Content=original==null?"创建日程":"保存修改";save.Width=original==null?145:125;save.Height=42;if(cancel!=null){cancel.Width=110;cancel.Height=42;}}
                return;
            }
            foreach(var input in new UIElement[]{name,lead,leadPreset})if(LogicalTreeHelper.GetParent(input) is Panel owner)owner.Children.Remove(input);
            if(LogicalTreeHelper.GetParent(nameCount) is Panel countOwner)countOwner.Children.Remove(nameCount);
            StackPanel AlarmLabel(string value){var label=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,7)};label.Children.Add(Text(value,14,FontWeights.SemiBold));return label;}
            Grid CountedName(){Grid box=new();name.Padding=new Thickness(12,10,62,10);name.BorderBrush=PlannerTheme.ControlLine;box.Children.Add(name);nameCount.VerticalAlignment=VerticalAlignment.Center;nameCount.HorizontalAlignment=HorizontalAlignment.Right;nameCount.Margin=new Thickness(0,0,11,0);box.Children.Add(nameCount);return box;}
            form.Children.Add(AlarmLabel("名称（可选）"));form.Children.Add(CountedName());
            if(relative)
            {
                var quickLabel=AlarmLabel("快速选择");quickLabel.Margin=new Thickness(0,18,0,7);form.Children.Add(quickLabel);form.Children.Add(quick);
                DockPanel timerTitle=new(){Margin=new Thickness(0,20,0,10)};var maximum=Text("最长24小时",12,foreground:PlannerTheme.Muted);DockPanel.SetDock(maximum,Dock.Right);timerTitle.Children.Add(maximum);timerTitle.Children.Add(Text("倒计时时长",14,FontWeights.SemiBold));form.Children.Add(timerTitle);form.Children.Add(digits);
            }
            else
            {
                form.Children.Add(new Border{Height=15,Background=Brushes.Transparent});form.Children.Add(AlarmLabel("时间"));
                Grid timeControl=new(){Width=200,Height=42,HorizontalAlignment=HorizontalAlignment.Left};time.Width=200;time.Height=42;time.Margin=new Thickness(0);time.Padding=new Thickness(40,10,32,10);time.BorderBrush=PlannerTheme.ControlLine;timeControl.Children.Add(time);var clockIcon=PlannerTheme.Icon("clock",17,PlannerTheme.Accent,0);clockIcon.HorizontalAlignment=HorizontalAlignment.Left;clockIcon.Margin=new Thickness(13,0,0,0);clockIcon.IsHitTestVisible=false;timeControl.Children.Add(clockIcon);var arrow=Text("⌄",15,foreground:PlannerTheme.Muted);arrow.HorizontalAlignment=HorizontalAlignment.Right;arrow.Margin=new Thickness(0,0,13,0);arrow.IsHitTestVisible=false;timeControl.Children.Add(arrow);form.Children.Add(timeControl);
                var cycleLabel=AlarmLabel("响铃周期");cycleLabel.Margin=new Thickness(0,18,0,7);form.Children.Add(cycleLabel);
                StackPanel cycles=Row();string[] cycleLabels=["一次","每天","每周","工作日","休息日","指定日期"];int[] cycleValues=[0,1,2,4,5,3];
                foreach(var pair in cycleLabels.Zip(cycleValues,(label,value)=>(label,value)))
                {
                    int value=pair.value;var choice=Action(pair.label,()=>
                    {
                        if(value==3&&selectedDates.Count==0)
                        {
                            int previous=repeat.SelectedIndex;DateSelectionWindow window=new(selectedDates,initialDate??_date,true){Owner=this};
                            if(window.ShowDialog()==true&&window.Selection.Count>0){selectedDates=window.Selection.OrderBy(d=>d).ToList();repeat.SelectedIndex=3;UpdateDateSummary();Update();}else repeat.SelectedIndex=previous;
                        }
                        else repeat.SelectedIndex=value;Update();
                    });
                    choice.Style=(Style)FindResource("PlannerChip");choice.Width=94;choice.Height=40;choice.Margin=new Thickness(3);choice.Padding=new Thickness(5);choice.BorderBrush=repeat.SelectedIndex==value?PlannerTheme.Accent:PlannerTheme.ControlLine;choice.Background=repeat.SelectedIndex==value?PlannerTheme.AccentSoft:Brushes.White;choice.Foreground=repeat.SelectedIndex==value?PlannerTheme.Accent:PlannerTheme.Ink;choice.FontWeight=repeat.SelectedIndex==value?FontWeights.SemiBold:FontWeights.Normal;cycles.Children.Add(choice);
                }
                form.Children.Add(cycles);repeat.Visibility=Visibility.Collapsed;form.Children.Add(repeat);
                if(repeat.SelectedIndex==0&&TimeSpan.TryParse(time.Text,out TimeSpan onceTime)){DateTime next=DateTime.Today+onceTime;if(next<=DateTime.Now)next=next.AddDays(1);var hint=Text($"将于{(next.Date==DateTime.Today?"今天":"明天")} {next:HH:mm} 响铃",12,foreground:PlannerTheme.Muted);hint.Margin=new Thickness(3,6,0,0);form.Children.Add(hint);}
                if(repeat.SelectedIndex==2)
                {
                    form.Children.Add(AlarmLabel("星期"));StackPanel weekChoices=Row();for(int n=0;n<checks.Length;n++){int index=n;var weekday=Action("一二三四五六日"[n].ToString(),()=>{checks[index].IsChecked=checks[index].IsChecked!=true;Update();});weekday.Style=(Style)FindResource("PlannerChip");weekday.Width=42;weekday.Height=34;weekday.Margin=new Thickness(3);bool selected=checks[n].IsChecked==true;weekday.Background=selected?PlannerTheme.AccentSoft:Brushes.White;weekday.Foreground=selected?PlannerTheme.Accent:PlannerTheme.Ink;weekday.BorderBrush=selected?PlannerTheme.Accent:PlannerTheme.ControlLine;weekChoices.Children.Add(weekday);}form.Children.Add(weekChoices);
                }
                if(repeat.SelectedIndex==3&&selectedDates.Count>0){var datesLabel=AlarmLabel("已选日期");datesLabel.Margin=new Thickness(0,13,0,7);form.Children.Add(datesLabel);picker.Width=double.NaN;picker.HorizontalAlignment=HorizontalAlignment.Stretch;picker.BorderBrush=PlannerTheme.ControlLine;form.Children.Add(picker);}
                form.Children.Add(new Border{Height=1,Background=PlannerTheme.Line,Margin=new Thickness(0,18,0,13)});
                early.Content=null;early.Margin=new Thickness(0);StackPanel earlyLine=Row();earlyLine.Width=330;earlyLine.HorizontalAlignment=HorizontalAlignment.Left;var earlyText=Text("提前提醒",14,FontWeights.SemiBold);earlyText.Width=110;earlyLine.Children.Add(earlyText);earlyLine.Children.Add(early);UIElement leadControl=leadPreset;if(leadPreset.SelectedIndex==4){lead.Width=90;lead.Height=42;leadControl=lead;}bool earlyOn=early.IsChecked==true;leadPreset.IsEnabled=earlyOn;lead.IsEnabled=earlyOn;if(leadControl is FrameworkElement leadElement){leadElement.Margin=new Thickness(12,0,0,0);leadElement.Width=136;leadElement.Opacity=earlyOn?1:0.52;}earlyLine.Children.Add(leadControl);form.Children.Add(earlyLine);
            }
            if(save!=null){save.Content=relative?"▶ 开始倒计时":original==null?"创建闹钟":"保存修改";save.Width=relative?180:150;save.Height=42;if(cancel!=null){cancel.Width=120;cancel.Height=42;}RefreshDuration();}
        }
        specified.Click+=(_,_)=>{relative=false;Update();};countdown.Click+=(_,_)=>{relative=true;Update();};specificTime.Click+=(_,_)=>{timeOn=specificTime.IsChecked==true;Update();};enabled.Click+=(_,_)=>Update();early.Click+=(_,_)=>Update();repeat.SelectionChanged+=(_,_)=>{if(calendar)Update();};
        panel.Children.Add(error);
        save=Action("保存",async()=>
        {
            try
            {
                DateTime now=DateTime.Now;int duration=hours.Value*3600+minutes.Value*60+secs.Value;if(relative&&(hours.Value<0||hours.Value>24||minutes.Value<0||minutes.Value>59||secs.Value<0||secs.Value>59||duration<1||duration>86400))throw new ArgumentException("请输入大于0且不超过24:00:00的时长。");
                if(calendar&&string.IsNullOrWhiteSpace(name.Text))throw new ArgumentException("请填写日程标题。");if(calendar&&selectedDates.Count==0)throw new ArgumentException("请选择至少一个日期。");
                if(name.Text.Trim().Length>titleLimit&&name.Text.Trim()!=original?.Title.Trim())throw new ArgumentException(calendar?"日程标题最多30字。":"提醒名称最多30字。");
                if(calendar&&notes.Text.Length>notesLimit&&notes.Text!=legacyNotes)throw new ArgumentException("备注最多200字。");
                bool concreteTime=relative||!calendar||timeOn;TimeSpan clock=TimeSpan.Zero;if(concreteTime&&!relative&&(!TimeSpan.TryParseExact(time.Text.Trim(),new[]{@"h\:mm",@"hh\:mm"},CultureInfo.InvariantCulture,out clock)||clock.TotalDays>=1))throw new ArgumentException("时间请输入00:00～23:59。");
                ReminderRepeat rule;if(relative)rule=ReminderRepeat.Once;else if(calendar)rule=selectedDates.Count>1?ReminderRepeat.Dates:original?.Repeat is ReminderRepeat.Daily or ReminderRepeat.Weekly or ReminderRepeat.Workdays or ReminderRepeat.RestDays?(ReminderRepeat)repeat.SelectedIndex:ReminderRepeat.Once;else rule=(ReminderRepeat)repeat.SelectedIndex;if(!calendar&&!relative&&rule==ReminderRepeat.Dates&&selectedDates.Count==0)throw new ArgumentException("请至少选择一个指定日期。");
                int earlyMinutes=original?.EarlyMinutes??30;if(!relative&&early.IsChecked==true&&(!int.TryParse(lead.Text,out earlyMinutes)||earlyMinutes<1||earlyMinutes>10080))throw new ArgumentException("提前时长请输入1～10080分钟。");
                DateTime chosenDay;if(calendar)chosenDay=selectedDates.Min().Date;else if(rule==ReminderRepeat.Dates)chosenDay=selectedDates.Min().Date;else{chosenDay=DateTime.Today;if(rule==ReminderRepeat.Once&&chosenDay+clock<=now)chosenDay=chosenDay.AddDays(1);}DateTime at=relative?now.AddSeconds(duration):chosenDay+clock;bool on=relative||!calendar||enabled.IsChecked==true&&concreteTime;if(calendar&&on&&rule==ReminderRepeat.Once&&at<=now)throw new ArgumentException("该时间已过去，请选择未来日期和时间。");
                ReminderItem item=new(){Id=original?.Id??Guid.NewGuid(),Calendar=calendar,Title=name.Text.Trim(),Notes=calendar?notes.Text:"",Content=null,Start=at,HasTime=concreteTime,Enabled=on,ReminderCreated=on,EarlyEnabled=on&&!relative&&concreteTime&&early.IsChecked==true,EarlyMinutes=earlyMinutes,Relative=relative,DurationSeconds=relative?duration:original?.DurationSeconds??1500,Repeat=rule,Dates=selectedDates,Weekdays=Days.Where((d,n)=>checks[n].IsChecked==true).ToList(),ExcludedDates=original?.ExcludedDates.ToList()??[],CheckedThrough=now};
                await Execute(b=>{b.Occurrences.RemoveAll(o=>o.RuleId==item.Id);b.Items.RemoveAll(i=>i.Id==item.Id);b.Items.Add(item);if(relative)b.LastDurationSeconds=duration;});_editing=false;Render();
            }
            catch(Exception ex)when(ex is ArgumentException or System.IO.IOException or UnauthorizedAccessException){error.Text=ex is ArgumentException?ex.Message:"保存失败，原数据保留，请重试。";}
        });save.Name="SaveReminder";save.Width=150;
        DockPanel buttons=new(){LastChildFill=false,Margin=new Thickness(0,14,0,0)};DockPanel.SetDock(save,Dock.Right);buttons.Children.Add(Primary(save));cancel=Action("取消",()=>{_editing=false;Render();});cancel.Width=120;cancel.BorderBrush=PlannerTheme.ControlLine;DockPanel.SetDock(cancel,Dock.Right);buttons.Children.Add(cancel);
        if(original!=null){var delete=Action(calendar?"删除日程…":"删除提醒…",()=>ConfirmDelete([original.Id],null));delete.Name="DeleteGroup";delete.Style=(Style)FindResource("PlannerDanger");DockPanel.SetDock(delete,Dock.Left);buttons.Children.Add(delete);if(calendar&&_occurrenceDate is DateTime day&&original.Repeat!=ReminderRepeat.Once){var one=Action("仅删除当天",()=>ConfirmDelete([original.Id],day));one.Name="DeleteOccurrence";buttons.Children.Add(one);}}
        panel.Children.Add(buttons);Update();panel.Children.Clear();
        Grid editorLayout=new(){Name="PlannerEditorLayout",Width=calendar?574:620,Height=calendar?546:double.NaN,MaxHeight=Math.Min(700,Math.Max(400,ActualHeight-80)),Tag=calendar?"CalendarEditor":null};
        foreach(var height in new[]{GridLength.Auto,GridLength.Auto,GridLength.Auto,new GridLength(1,GridUnitType.Star),GridLength.Auto,GridLength.Auto})editorLayout.RowDefinitions.Add(new(){Height=height});
        void Attach(UIElement child,int row){Grid.SetRow(child,row);editorLayout.Children.Add(child);}
        Attach(head,0);Attach(divider,1);
        ScrollViewer formScroll=new(){Name="EditorFormScroll",Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Attach(formScroll,3);
        Attach(error,4);Attach(buttons,5);ShowOverlay(editorLayout,true);name.Focus();
    }
}
