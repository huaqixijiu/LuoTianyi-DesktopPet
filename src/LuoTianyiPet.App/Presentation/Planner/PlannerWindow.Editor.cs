using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using LuoTianyiPet.Core;
using Button=System.Windows.Controls.Button;
using CheckBox=System.Windows.Controls.CheckBox;
using TextBox=System.Windows.Controls.TextBox;
using ComboBox=System.Windows.Controls.ComboBox;
using HorizontalAlignment=System.Windows.HorizontalAlignment;
namespace LuoTianyiPet.App;
internal sealed partial class PlannerWindow
{
    private void Edit(ReminderItem? original,bool calendar)
    {
        _editing=true;StackPanel panel=new(){Width=600};DockPanel head=new();
        var close=IconButton("close",()=>{_editing=false;Render();},"关闭",18,PlannerTheme.Muted);DockPanel.SetDock(close,Dock.Right);head.Children.Add(close);
        head.Children.Add(Text(original==null?calendar?"新建事项":"新建闹钟":calendar?"编辑事项":"编辑闹钟",22,FontWeights.SemiBold));panel.Children.Add(head);
        panel.Children.Add(new Border{Height=1,Background=PlannerTheme.Line,Margin=new Thickness(0,8,0,10)});
        TextBlock error=Text("",12);error.Name="EditorError";error.Foreground=PlannerTheme.Danger;error.Margin=new Thickness(3,8,3,0);
        bool relative=original?.Relative==true;
        Border modeCapsule=new(){Background=Brushes.White,CornerRadius=new CornerRadius(10),BorderBrush=PlannerTheme.Line,BorderThickness=new Thickness(1),Padding=new Thickness(3),Margin=new Thickness(3,3,3,10),HorizontalAlignment=HorizontalAlignment.Stretch};
        var mode=Row();mode.HorizontalAlignment=HorizontalAlignment.Center;
        var specified=new Button{Style=(Style)FindResource("PlannerSegment"),Content="◷ 闹钟",MinWidth=250,Height=38,Margin=new Thickness(1)};
        var countdown=new Button{Style=(Style)FindResource("PlannerSegment"),Content="⌛ 倒计时",MinWidth=250,Height=38,Margin=new Thickness(1)};
        mode.Children.Add(specified);mode.Children.Add(countdown);modeCapsule.Child=mode;if(!calendar)panel.Children.Add(modeCapsule);
        StackPanel form=new();panel.Children.Add(form);
        TextBlock Counter(string value){var t=Text(value,11,foreground:PlannerTheme.Muted);t.HorizontalAlignment=HorizontalAlignment.Right;t.Margin=new Thickness(3,4,3,0);return t;}
        StackPanel BoxWith(UIElement input,TextBlock count){StackPanel box=new(){Margin=new Thickness(3)};box.Children.Add(input);box.Children.Add(count);return box;}
        UIElement WithPlaceholder(TextBox input,string hint)
        {
            Grid g=new();
            TextBlock ph=new(){Text=hint,Foreground=PlannerTheme.Muted,Margin=new Thickness(14,11,0,0),IsHitTestVisible=false,FontSize=14};
            void Sync()=>ph.Visibility=input.Text.Length==0?Visibility.Visible:Visibility.Collapsed;
            input.TextChanged+=(_,_)=>Sync();Sync();
            g.Children.Add(input);g.Children.Add(ph);return g;
        }
        TextBox name=new(){Height=42,Name="ReminderTitle",Text=original?.Title??"",MaxLength=120};
        TextBlock nameCount=Counter($"{name.Text.Length}/120");name.TextChanged+=(_,_)=>nameCount.Text=$"{name.Text.Length}/120";
        StackPanel nameBox=BoxWith(WithPlaceholder(name,"请输入事项名称..."),nameCount);
        // Legacy combined content is kept intact in the note field until the user explicitly edits it.
        string legacyNotes=original?.Notes??"";
        if(original?.Content is string merged){var line=merged.IndexOf('\n');if(line>=0)legacyNotes=merged.Substring(line+1);}
        TextBox notes=new(){Name="ReminderNotes",Text=legacyNotes,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=64,MaxHeight=120,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        TextBlock notesCount=Counter($"{notes.Text.Length} 字");notes.TextChanged+=(_,_)=>notesCount.Text=$"{notes.Text.Length} 字";
        StackPanel notesBox=BoxWith(WithPlaceholder(notes,"写点备注..."),notesCount);
        DateTime start=original?.Start??(calendar?_date.Date.AddHours(Math.Min(23,DateTime.Now.Hour+1)):DateTime.Now.AddHours(1));
        DatePicker date=new(){Name="ReminderDate",SelectedDate=start.Date,DisplayDateStart=ReminderSchedule.MinimumDate,DisplayDateEnd=ReminderSchedule.MaximumDate,Width=260,Margin=new Thickness(3),VerticalContentAlignment=VerticalAlignment.Center};
        TextBox time=new(){Height=42,Name="ReminderTime",Text=start.ToString("HH:mm"),Width=110,Margin=new Thickness(3)};
        Button noTime=Chip("不指定时间",()=>{});noTime.Name="NoTime";bool noTimeOn=original?.HasTime==false;
        Button multiple=Chip("多日期",()=>{});multiple.Name="MultipleDates";bool multiOn=original?.Repeat==ReminderRepeat.Dates;
        void StyleToggle(Button b,bool on){b.Background=on?PlannerTheme.AccentSoft:Brushes.White;b.Foreground=on?PlannerTheme.Accent:PlannerTheme.Ink;b.BorderBrush=on?PlannerTheme.Accent:PlannerTheme.Line;b.FontWeight=on?FontWeights.SemiBold:FontWeights.Normal;}
        StyleToggle(noTime,noTimeOn);StyleToggle(multiple,multiOn);
        ComboBox repeat=new(){Name="ReminderRepeat",ItemsSource=Repeats,SelectedIndex=(int)(original?.Repeat??ReminderRepeat.Once),Margin=new Thickness(3)};
        var checks=Days.Select(d=>new CheckBox{Content="周"+"日一二三四五六"[(int)d],IsChecked=original?.Weekdays.Contains(d)==true,Margin=new Thickness(5)}).ToArray();StackPanel weekdays=Row();foreach(var c in checks)weekdays.Children.Add(c);
        List<DateTime> selectedDates=original?.Dates.Count>0?original.Dates.ToList():[start.Date];
        bool multiMode=calendar?multiOn:repeat.SelectedIndex==3;
        Button picker=null!;picker=Action("",()=>{DateSelectionWindow w=new(selectedDates,date.SelectedDate??start){Owner=this};if(w.ShowDialog()==true){selectedDates=w.Selection.OrderBy(d=>d).ToList();UpdateDateSummary();}});picker.Name="ModifyDates";picker.Width=260;picker.Height=42;picker.HorizontalContentAlignment=HorizontalAlignment.Left;picker.ToolTip="点击选择日期";
        void UpdateDateSummary()
        {
            string[] labels=selectedDates.OrderBy(d=>d).Select(d=>d.ToString("M/d")).ToArray();
            picker.Content=labels.Length switch{0=>"请选择日期",1=>labels[0],2 or 3=>$"已选 {labels.Length} 天 · {string.Join("、",labels)}",_=>$"已选 {labels.Length} 天 · {string.Join("、",labels.Take(2))}…"};
            picker.ToolTip=labels.Length==0?"点击选择日期":string.Join("、",labels);
        }
        UpdateDateSummary();
        var enabled=new CheckBox{Name="CreateAlarm",Content="创建闹钟提醒",IsChecked=original?.Enabled??false,Style=(Style)FindResource("PlannerSwitch"),Margin=new Thickness(3,12,3,12)};
        var early=new CheckBox{Name="EarlyReminder",Content="提前提醒",IsChecked=original?.EarlyEnabled??false,Style=(Style)FindResource("PlannerSwitch"),Margin=new Thickness(3,12,3,12)};
        TextBox lead=new(){Height=42,Name="EarlyMinutes",Text=(original?.EarlyMinutes??30).ToString(),Width=80,Margin=new Thickness(3)};StackPanel leadRow=Row();leadRow.Children.Add(lead);leadRow.Children.Add(Text("分钟 · 到点仍会提醒",12));
        int seconds=original?.DurationSeconds??_service.Book.LastDurationSeconds;
        PlannerNumber hours=new("CountdownHours",seconds/3600,24),minutes=new("CountdownMinutes",seconds/60%60,59),secs=new("CountdownSeconds",seconds%60,59);
        StackPanel digits=Row();digits.HorizontalAlignment=HorizontalAlignment.Center;
        foreach(var tuple in new[]{("小时",hours),("分钟",minutes),("秒",secs)})
        {
            if(digits.Children.Count>0)
            {
                // Colon column mirrors the digit column (label spacer + centered card region) so it lines up with the number.
                Grid colon=new(){Width=20,Margin=new Thickness(0,0,0,0)};colon.RowDefinitions.Add(new(){Height=GridLength.Auto});colon.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
                colon.Children.Add(new TextBlock{Text="",FontSize=13,Margin=new Thickness(3)});
                var mark=Text(":",22,FontWeights.SemiBold,PlannerTheme.Muted);mark.HorizontalAlignment=HorizontalAlignment.Center;mark.VerticalAlignment=VerticalAlignment.Center;Grid.SetRow(mark,1);colon.Children.Add(mark);
                digits.Children.Add(colon);
            }
            StackPanel column=new(){Width=186};var label=Text(tuple.Item1,13);label.TextAlignment=TextAlignment.Center;column.Children.Add(label);var digitCard=Card(tuple.Item2);digitCard.Background=PlannerTheme.Soft;column.Children.Add(digitCard);digits.Children.Add(column);
        }
        List<(int Seconds,Button Button)> presets=[];
        void HighlightPreset(){int duration=hours.Value*3600+minutes.Value*60+secs.Value;foreach(var pair in presets){bool selected=pair.Seconds==duration;pair.Button.Background=selected?PlannerTheme.Soft:Brushes.White;pair.Button.Foreground=selected?PlannerTheme.Accent:PlannerTheme.Ink;pair.Button.FontWeight=selected?FontWeights.SemiBold:FontWeights.Normal;pair.Button.BorderBrush=selected?PlannerTheme.Accent:PlannerTheme.Line;}}
        StackPanel quick=Row();quick.HorizontalAlignment=HorizontalAlignment.Center;foreach(var preset in new[]{(300,"5分钟"),(900,"15分钟"),(1800,"30分钟"),(3600,"1小时")}){var button=Action(preset.Item2,()=>{hours.Input.Text=(preset.Item1/3600).ToString("00");minutes.Input.Text=(preset.Item1/60%60).ToString("00");secs.Input.Text="00";});button.Width=140;quick.Children.Add(button);presets.Add((preset.Item1,button));}foreach(var input in new[]{hours.Input,minutes.Input,secs.Input})input.TextChanged+=(_,_)=>HighlightPreset();HighlightPreset();
        void Field(string label,UIElement input){Grid row=new(){Margin=new Thickness(0,9,0,9)};row.ColumnDefinitions.Add(new(){Width=new GridLength(116)});row.ColumnDefinitions.Add(new());var labelRow=Row();if(label.Length>0){string icon=label.Contains("日期")?"calendar":label.Contains("时间")||label.Contains("提前")?"clock":label.Contains("重复")||label.Contains("频率")?"repeat":"note";labelRow.Children.Add(PlannerTheme.Icon(icon,18));labelRow.Children.Add(Text(label,14));}labelRow.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(labelRow);Grid.SetColumn(input,1);row.Children.Add(input);form.Children.Add(row);}
        Button? save=null;
        void SetMultiMode(bool enabled)
        {
            if(enabled==multiMode)return;
            DateTime current=date.SelectedDate?.Date??start.Date;
            if(enabled)
            {
                if(selectedDates.Count<=1||!selectedDates.Contains(current))
                {
                    selectedDates.Clear();selectedDates.Add(current);
                }
            }
            else
            {
                DateTime single=selectedDates.Count>0?selectedDates.Min().Date:current;
                date.SelectedDate=single;
                selectedDates.Clear();selectedDates.Add(single);
            }
            multiMode=enabled;UpdateDateSummary();
        }
        void Update()
        {
            form.Children.Clear();error.Text="";
            specified.Background=!relative?PlannerTheme.AccentSoft:Brushes.Transparent;specified.Foreground=!relative?PlannerTheme.Accent:PlannerTheme.Ink;specified.FontWeight=!relative?FontWeights.SemiBold:FontWeights.Normal;
            countdown.Background=relative?PlannerTheme.AccentSoft:Brushes.Transparent;countdown.Foreground=relative?PlannerTheme.Accent:PlannerTheme.Ink;countdown.FontWeight=relative?FontWeights.SemiBold:FontWeights.Normal;
            // Detach reusable fields from previous rows before rebuilding their conditional layout.
            foreach(var element in new UIElement[]{nameBox,date,time,notesBox,multiple,noTime,repeat,picker,enabled,early,leadRow,weekdays,quick,digits})
            {if(LogicalTreeHelper.GetParent(element) is System.Windows.Controls.Panel parent)parent.Children.Remove(element);}
            if(relative)
            {
                var nameLabel=Text("提醒名称（可选）",14,FontWeights.SemiBold);nameLabel.Margin=new Thickness(3,4,3,6);form.Children.Add(nameLabel);form.Children.Add(nameBox);
                var quickLabel=Text("快捷选择",14,FontWeights.SemiBold,PlannerTheme.Accent);quickLabel.Margin=new Thickness(3,14,3,6);form.Children.Add(quickLabel);form.Children.Add(quick);
                StackPanel timerLabel=Row();timerLabel.Margin=new Thickness(3,14,3,6);timerLabel.Children.Add(Text("倒计时设置",14,FontWeights.SemiBold,PlannerTheme.Accent));timerLabel.Children.Add(Text("最长 24 小时",12));form.Children.Add(timerLabel);form.Children.Add(digits);
            }
            else
            {
                if(calendar)Field("事项名称 *",nameBox);
                bool multi=calendar?multiOn:repeat.SelectedIndex==3;
                StackPanel dateRow=Row();dateRow.Children.Add(multi?picker:date);if(calendar)dateRow.Children.Add(multiple);Field("日期 *",dateRow);
                StackPanel timeRow=Row();time.Visibility=noTimeOn?Visibility.Collapsed:Visibility.Visible;timeRow.Children.Add(time);if(calendar)timeRow.Children.Add(noTime);Field("时间",timeRow);
                if(!calendar)Field("名称（可选）",nameBox);
                if(!calendar || original?.Repeat is ReminderRepeat.Daily or ReminderRepeat.Weekly or ReminderRepeat.Workdays or ReminderRepeat.RestDays)
                {
                    WrapPanel choices=new();var labels=new[]{"仅一次","每天","每周","指定日期","工作日","休息日"};
                    foreach(int index in new[]{0,1,2,4,5,3})
                    {
                        int value=index;var choice=Chip(labels[index],()=>{repeat.SelectedIndex=value;Update();});
                        choice.Padding=new Thickness(10,8,10,8);choice.FontSize=12;
                        if(repeat.SelectedIndex==index){choice.Background=PlannerTheme.AccentSoft;choice.Foreground=PlannerTheme.Accent;choice.BorderBrush=PlannerTheme.Accent;choice.FontWeight=FontWeights.SemiBold;}
                        choices.Children.Add(choice);
                    }
                    Field("提醒频率",choices);repeat.Visibility=Visibility.Collapsed;form.Children.Add(repeat);
                }
                if(repeat.SelectedIndex==2)form.Children.Add(weekdays);
                time.IsEnabled=!noTimeOn;
                enabled.IsEnabled=!noTimeOn;
                if(noTimeOn)enabled.IsChecked=false;
                if(calendar)Field("",enabled);
                if(noTimeOn&&calendar)form.Children.Add(Text("设置时间后可创建闹钟提醒",12));
                if(!calendar || enabled.IsChecked==true){Field("",early);if(early.IsChecked==true)Field("提前时长",leadRow);}
                if(calendar)Field("备注（可选）",notesBox);
            }
            if(save!=null){save.Content=relative?"▶ 开始倒计时":"保存";save.Width=relative?280:150;}
        }
        specified.Click+=(_,_)=>{relative=false;Update();};countdown.Click+=(_,_)=>{relative=true;Update();};
        multiple.Click+=(_,_)=>{multiOn=!multiOn;StyleToggle(multiple,multiOn);SetMultiMode(multiOn);Update();};noTime.Click+=(_,_)=>{noTimeOn=!noTimeOn;StyleToggle(noTime,noTimeOn);Update();};enabled.Click+=(_,_)=>Update();early.Click+=(_,_)=>Update();repeat.SelectionChanged+=(_,_)=>{if(!calendar)SetMultiMode(repeat.SelectedIndex==3);Update();};
        panel.Children.Add(error);
        save=Action("保存",async()=>
        {
            try
            {
                DateTime now=DateTime.Now;int duration=hours.Value*3600+minutes.Value*60+secs.Value;
                if(relative&&(hours.Value<0||hours.Value>24||minutes.Value<0||minutes.Value>59||secs.Value<0||secs.Value>59||duration<1||duration>86400))throw new ArgumentException("请输入大于0且不超过24:00:00的时长。");
                if(calendar&&string.IsNullOrWhiteSpace(name.Text))throw new ArgumentException("请填写事项名称。");
                TimeSpan clock=TimeSpan.Zero;if(!relative&&!noTimeOn&&(!TimeSpan.TryParseExact(time.Text.Trim(),new[]{@"h\:mm",@"hh\:mm"},CultureInfo.InvariantCulture,out clock)||clock.TotalDays>=1))throw new ArgumentException("时间请输入00:00～23:59。");
                var rule=relative?ReminderRepeat.Once:calendar?(multiOn?ReminderRepeat.Dates:original?.Repeat is ReminderRepeat.Daily or ReminderRepeat.Weekly or ReminderRepeat.Workdays or ReminderRepeat.RestDays?(ReminderRepeat)repeat.SelectedIndex:ReminderRepeat.Once):(ReminderRepeat)repeat.SelectedIndex;
                if(rule==ReminderRepeat.Dates&&selectedDates.Count==0)throw new ArgumentException("请至少选择一个日期。");
                int earlyMinutes=original?.EarlyMinutes??30;if(!relative&&early.IsChecked==true&&(!int.TryParse(lead.Text,out earlyMinutes)||earlyMinutes<1||earlyMinutes>10080))throw new ArgumentException("提前时长请输入1～10080分钟。");
                var at=relative?now.AddSeconds(duration):(rule==ReminderRepeat.Dates?selectedDates.Min().Date:date.SelectedDate??throw new ArgumentException("请选择日期。"))+clock;
                bool on=relative||!calendar||enabled.IsChecked==true&&!noTimeOn;
                if(on&&rule==ReminderRepeat.Once&&at<=now)throw new ArgumentException("该时间已过去，请选择未来日期和时间。");
                ReminderItem item=new(){Id=original?.Id??Guid.NewGuid(),Calendar=calendar,Title=name.Text.Trim(),Notes=calendar?notes.Text:"",Content=null,Start=at,HasTime=!calendar||!noTimeOn,Enabled=on,ReminderCreated=on||original?.ReminderCreated==true,EarlyEnabled=!relative&&early.IsChecked==true,EarlyMinutes=earlyMinutes,Relative=relative,DurationSeconds=relative?duration:original?.DurationSeconds??1500,Repeat=rule,Dates=selectedDates,Weekdays=Days.Where((d,n)=>checks[n].IsChecked==true).ToList(),ExcludedDates=original?.ExcludedDates.ToList()??[],CheckedThrough=now};
                await Execute(b=>{b.Occurrences.RemoveAll(o=>o.RuleId==item.Id);b.Items.RemoveAll(i=>i.Id==item.Id);b.Items.Add(item);if(relative)b.LastDurationSeconds=duration;});_editing=false;Render();
            }catch(Exception ex)when(ex is ArgumentException or System.IO.IOException or UnauthorizedAccessException){error.Text=ex is ArgumentException?ex.Message:"保存失败，原数据保留，请重试。";}
        });save.Name="SaveReminder";save.Width=150;
        DockPanel buttons=new(){LastChildFill=false,Margin=new Thickness(0,18,0,0)};DockPanel.SetDock(save,Dock.Right);buttons.Children.Add(Primary(save));var cancel=Action("取消",()=>{_editing=false;Render();});cancel.Width=120;DockPanel.SetDock(cancel,Dock.Right);buttons.Children.Add(cancel);
        if(original!=null){var delete=Action(calendar?"删除事项…":"删除提醒…",()=>ConfirmDelete([original.Id],null));delete.Name="DeleteGroup";delete.Style=(Style)FindResource("PlannerDanger");DockPanel.SetDock(delete,Dock.Left);buttons.Children.Add(delete);if(calendar&&_occurrenceDate is DateTime day&&original.Repeat!=ReminderRepeat.Once){var one=Action("仅删除当天",()=>ConfirmDelete([original.Id],day));one.Name="DeleteOccurrence";buttons.Children.Add(one);}}
        cancel.Width=120;panel.Children.Add(buttons);Update();ShowOverlay(panel);name.Focus();
    }
}
