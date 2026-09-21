using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LuoTianyiPet.Core;
using LuoTianyiPet.Platform.Windows;
using Button=System.Windows.Controls.Button;
using TextBox=System.Windows.Controls.TextBox;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    private async Task RunPlannerEditorDensityQaAsync()
    {
        string path=Path.Combine(AppContext.BaseDirectory,"PlannerEditorDensityQa",DateTime.UtcNow.Ticks.ToString());
        Directory.CreateDirectory(path);List<string> checks=[];
        PlannerWindow? window=null;
        try
        {
            using ReminderService service=new(new LocalAppPaths(Path.Combine(path,"UserData")));
            await service.LoadAsync();
            window=new PlannerWindow(service,false);window.Show();await Task.Delay(120);
            void Check(bool condition,string label){if(!condition)throw new InvalidOperationException(label);checks.Add("PASS "+label);}
            IEnumerable<DependencyObject> Tree(DependencyObject node)
            {
                yield return node;
                for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var child in Tree(VisualTreeHelper.GetChild(node,i)))yield return child;
            }
            T Named<T>(string name) where T:FrameworkElement=>Tree(window).OfType<T>().Single(x=>x.Name==name);
            void Click(string name)=>Named<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            void Edit(ReminderItem? item)
            {
                typeof(PlannerWindow).GetMethod("Edit",BindingFlags.NonPublic|BindingFlags.Instance,null,[typeof(ReminderItem),typeof(bool)],null)!
                    .Invoke(window,[item,false]);window.UpdateLayout();
            }
            void Shot(string name)
            {
                window.UpdateLayout();var bitmap=new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth),(int)Math.Ceiling(window.ActualHeight),96,96,PixelFormats.Pbgra32);
                bitmap.Render(window);PngBitmapEncoder encoder=new();encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file=File.Create(Path.Combine(path,name+".png"));encoder.Save(file);
            }
            string Summary()=>string.Concat(Tree(Named<Button>("ModifyDates").Content as DependencyObject??throw new InvalidOperationException("No date summary"))
                .OfType<TextBlock>().Select(x=>x.Text));
            void FitModal(string label)
            {
                Grid editor=Named<Grid>("PlannerEditorLayout");
                Border card=(Border)VisualTreeHelper.GetParent(editor);
                Rect bounds=card.TransformToAncestor(window).TransformBounds(new Rect(card.RenderSize));
                Check(bounds.Left>=0&&bounds.Top>=0&&bounds.Right<=window.ActualWidth+1&&bounds.Bottom<=window.ActualHeight+1,
                    $"{label} modal fits its page viewport");
                Button save=Named<Button>("SaveReminder");
                Rect action=save.TransformToAncestor(editor).TransformBounds(new Rect(save.RenderSize));
                Check(action.Bottom<=editor.ActualHeight+1&&action.Right<=editor.ActualWidth+1,$"{label} primary action stays visible");
                Rect actionInCard=save.TransformToAncestor(card).TransformBounds(new Rect(save.RenderSize));
                Check(actionInCard.Bottom<=card.ActualHeight-card.Padding.Bottom-card.BorderThickness.Bottom+1,
                    $"{label} action is not clipped by dialog padding at native size");
            }
            void CheckDates(string label,DateTime[] dates,string expected)
            {
                Edit(new ReminderItem{Title=label,Start=dates.Min().AddHours(9),Repeat=ReminderRepeat.Dates,Dates=dates.ToList()});
                Button picker=Named<Button>("ModifyDates");Border time=Named<Border>("AlarmTimeField");
                Point dateTop=picker.TransformToAncestor(window).Transform(new Point());
                Point timeTop=time.TransformToAncestor(window).Transform(new Point());
                Check(Math.Abs(dateTop.Y-timeTop.Y)<1&&picker.ActualHeight>=time.ActualHeight&&picker.ActualHeight-time.ActualHeight<=5,
                    $"{label} compact time field shares the selected-date top edge (date {dateTop.Y:0.0}/{picker.ActualHeight:0.0}; time {timeTop.Y:0.0}/{time.ActualHeight:0.0})");
                Check(Summary().Contains(dates.Min().ToString("M月d日"))||Summary().Contains(dates.Min().ToString("yyyy/M/d")),$"{label} summary begins with a selected date");
                StackPanel content=(StackPanel)picker.Content;
                Check(content.ActualWidth<=picker.ActualWidth-picker.Padding.Left-picker.Padding.Right+1,
                    $"{label} summary fits its field without horizontal clipping (content {content.ActualWidth:0.0}, field {picker.ActualWidth:0.0}, padding {picker.Padding.Left:0.0}+{picker.Padding.Right:0.0})");
                Check(Named<ScrollViewer>("EditorFormScroll").ScrollableHeight<1,$"{label} form needs no outer scroll");
                FitModal(label);Shot("alarm-"+label);Click("EditorClose");
            }
            // Regression: the logo's routed DPI event used to rebuild the whole
            // page continuously, destroying the pressed button before mouse-up.
            Button stableNavigation=Named<Button>("PlannerAlarmNavigation");
            await Task.Delay(500);window.UpdateLayout();
            Check(ReferenceEquals(stableNavigation,Named<Button>("PlannerAlarmNavigation")),"idle page retains live buttons after image DPI measurement");
            window.SetPageSize("standard");window.UpdateLayout();stableNavigation=Named<Button>("PlannerAlarmNavigation");
            for(int repeat=0;repeat<10;repeat++)window.SetPageSize("standard");
            await Task.Delay(100);window.UpdateLayout();
            Check(ReferenceEquals(stableNavigation,Named<Button>("PlannerAlarmNavigation")),"unchanged monitor geometry never rebuilds live controls");
            var nativeShell=(Grid)window.Content;
            Check(nativeShell.LayoutTransform.Value.IsIdentity,"viewport remains native resolution without whole-page scaling");
            Click("PlannerAlarmNavigation");window.UpdateLayout();stableNavigation=Named<Button>("PlannerCalendarNavigation");
            await Task.Delay(300);window.UpdateLayout();
            Check(ReferenceEquals(stableNavigation,Named<Button>("PlannerCalendarNavigation")),"alarm page remains stable after navigation");
            Click("PlannerCalendarNavigation");window.UpdateLayout();
            Edit(null);Check(Named<Grid>("PlannerEditorLayout").Width==520&&Named<Grid>("PlannerEditorLayout").Height>=300&&Named<Grid>("PlannerEditorLayout").Height<=520,$"standard alarm sizes its modal to the visible form (actual {Named<Grid>("PlannerEditorLayout").Width:0}x{Named<Grid>("PlannerEditorLayout").Height:0})");
            FitModal("alarm empty");Shot("alarm-empty");
            TextBox notes=Named<TextBox>("ReminderNotes");notes.Text=new string('备',20);
            await Task.Delay(70);window.UpdateLayout();
            Check(notes.Text.Length==20&&notes.MaxLength==20,$"standalone alarm title accepts the full 20-character limit (height {notes.ActualHeight:0})");
            var alarmScroll=Named<ScrollViewer>("EditorFormScroll");
            Check(alarmScroll.ScrollableHeight<1,$"standalone alarm title fits without an outer form scroll (scroll={alarmScroll.ScrollableHeight:0.0}, viewport={alarmScroll.ViewportHeight:0.0}, extent={alarmScroll.ExtentHeight:0.0}, editor={Named<Grid>("PlannerEditorLayout").ActualHeight:0.0})");
            Shot("alarm-three-line-notes");Click("EditorClose");
            DateTime first=new(2026,9,17);CheckDates("same-five",Enumerable.Range(0,5).Select(n=>first.AddDays(n)).Reverse().ToArray(),"9月17日9月18日9月19日9月20日9月21日");
            CheckDates("same-six",Enumerable.Range(0,6).Select(n=>first.AddDays(n)).ToArray(),"9月17日9月18日9月19日9月20日9月21日+1天");
            DateTime[] cross=[new(2027,1,5),new(2026,12,31),new(2027,1,1),new(2027,1,3),new(2027,1,4)];
            CheckDates("cross-five",cross,"2026/12/312027/1/12027/1/32027/1/42027/1/5");
            CheckDates("cross-six",[..cross,new DateTime(2027,1,6)],"2026/12/312027/1/12027/1/32027/1/42027/1/5+1天");
            foreach(string size in new[]{"mini","standard","comfortable","fullscreen"})
            {
                window.SetPageSize(size);window.FitViewport(1200,800);Edit(null);
                double expectedHeight=520;
                double expectedCountdownHeight=560;
                double expectedWidth=size=="mini"?440:size=="standard"?520:size=="comfortable"?554:588;
                Check(Named<Grid>("PlannerEditorLayout").Width==expectedWidth&&Named<Grid>("PlannerEditorLayout").Height>=300&&Named<Grid>("PlannerEditorLayout").Height<=Math.Min(expectedHeight,Math.Max(360,window.ActualHeight-100)),$"{size} alarm fits its page preset");FitModal(size+" alarm");Shot(size+"-alarm");
                double ordinaryAlarmHeight=Named<Grid>("PlannerEditorLayout").Height;
                Border modeBar=Named<Border>("ReminderModeCapsule");TextBox alarmLabel=Named<TextBox>("ReminderNotes");
                Rect modeBounds=modeBar.TransformToAncestor(window).TransformBounds(new Rect(modeBar.RenderSize));
                Rect labelBounds=alarmLabel.TransformToAncestor(window).TransformBounds(new Rect(alarmLabel.RenderSize));
                Check(labelBounds.Top-modeBounds.Bottom>=12,$"{size} alarm title field has breathing room below the mode switch");
                Check(!Tree(window).OfType<TextBox>().Any(x=>x.Name=="ReminderTitle")&&alarmLabel.MaxLength==20,$"{size} standalone alarm uses one 20-character note title field");
                if(size=="mini")Check(alarmLabel.FontSize==12&&Named<Border>("AlarmTimeField").Height==32&&Named<Button>("SaveReminder").Height==36,"mini alarm dialog scales its fields, typography and actions together");
                alarmLabel.Text=new string('闹',20);Click("AlarmCycle2");
                Named<System.Windows.Controls.CheckBox>("EarlyReminder").IsChecked=true;
                Named<System.Windows.Controls.CheckBox>("EarlyReminder").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Rect cycleBottom=Named<Button>("AlarmCycle2").TransformToAncestor(window).TransformBounds(new Rect(Named<Button>("AlarmCycle2").RenderSize));
                TextBlock earlyTitle=Tree(window).OfType<TextBlock>().Single(t=>t.Text=="提前提醒");
                Rect earlyTitleBounds=earlyTitle.TransformToAncestor(window).TransformBounds(new Rect(earlyTitle.RenderSize));
                Check(earlyTitleBounds.Top>=cycleBottom.Bottom-1,$"{size} early reminder sits below the ringing cycle controls");
                TextBox filledAlarmNotes=Named<TextBox>("ReminderNotes");filledAlarmNotes.Text=new string('备',20);await Task.Delay(100);window.UpdateLayout();
                Shot(size+"-alarm-filled");
                Check(filledAlarmNotes.VerticalScrollBarVisibility==ScrollBarVisibility.Hidden&&filledAlarmNotes.Text.Length==20,$"{size} 20-character alarm title has no internal scrollbar");
                Check(Named<ScrollViewer>("EditorFormScroll").ScrollableHeight<1,$"{size} filled weekly alarm avoids outer scrolling (scroll={Named<ScrollViewer>("EditorFormScroll").ScrollableHeight:0.0}, viewport={Named<ScrollViewer>("EditorFormScroll").ViewportHeight:0.0}, extent={Named<ScrollViewer>("EditorFormScroll").ExtentHeight:0.0})");
                FitModal(size+" filled alarm");
                Click("ReminderModeCountdown");window.UpdateLayout();
                Check(Named<Grid>("PlannerEditorLayout").Width==expectedWidth&&Named<Grid>("PlannerEditorLayout").Height<=Math.Min(expectedCountdownHeight,Math.Max(360,window.ActualHeight-100)),$"{size} countdown shares width and fits its page preset (actual {Named<Grid>("PlannerEditorLayout").Width}x{Named<Grid>("PlannerEditorLayout").Height})");
                Check(Named<Grid>("PlannerEditorLayout").Height>=ordinaryAlarmHeight,$"{size} countdown leaves room for its duration and note fields");
                Check(Named<ScrollViewer>("EditorFormScroll").ScrollableHeight<1,$"{size} countdown keeps its entire duration picker visible without an outer scroll");
                Check(Named<TextBox>("CountdownHours").FontSize<Named<TextBox>("ReminderNotes").FontSize+9,$"{size} countdown digits remain modest beside the form typography");
                Check(Named<Button>("SaveReminder").Height<=(size=="mini"?38:size=="standard"?42:44),$"{size} countdown action remains subordinate to the page action");
                FitModal(size+" countdown");Shot(size+"-countdown");Click("EditorClose");
            }
            window.SetPageSize("standard");window.FitViewport(1200,800);Edit(null);
            var standaloneTitle=Named<TextBox>("ReminderNotes");standaloneTitle.Text="吃饭";Click("SaveReminder");await Task.Delay(120);
            var savedStandalone=service.Book.Items.Last();
            Check(!savedStandalone.Calendar&&savedStandalone.Title=="吃饭"&&savedStandalone.Notes=="","standalone alarm note field persists as its 20-character title");
            Edit(savedStandalone);Check(Named<TextBox>("ReminderNotes").Text=="吃饭"&&!Tree(window).OfType<TextBox>().Any(x=>x.Name=="ReminderTitle"),"standalone alarm reopens its title in the single note field");Click("EditorClose");await service.ChangeAsync(book=>book.Items.RemoveAll(item=>item.Id==savedStandalone.Id));
            window.Navigate(false);
            foreach(string size in new[]{"mini","standard","comfortable","fullscreen"})
            {
                window.SetPageSize(size);window.FitViewport(1200,800);
                typeof(PlannerWindow).GetMethod("EditSchedule",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(window,[null,new DateTime(2026,9,1)]);window.UpdateLayout();
                FitModal(size+" schedule");
                Check(Named<ScrollViewer>("EditorFormScroll").ScrollableHeight<1,$"{size} ordinary schedule form has no outer scroll");
                if(size=="mini")Check(Named<TextBox>("ReminderTitle").FontSize==13&&Named<TextBox>("ReminderNotes").FontSize==13&&Named<Button>("ModifyDates").Height==34&&Named<Button>("SaveReminder").Height==36,"mini schedule dialog scales labels, fields and actions as one compact system");
                Shot(size+"-schedule");Click("ModifyDates");window.UpdateLayout();
                Border dates=Named<Border>("InlineDateSelector");Grid editor=Named<Grid>("PlannerEditorLayout");
                Rect selection=dates.TransformToAncestor(editor).TransformBounds(new Rect(dates.RenderSize));
                Check(selection.Left>=0&&selection.Top>=0&&selection.Right<=editor.ActualWidth+1&&selection.Bottom<=editor.ActualHeight+1,$"{size} inline date selector stays inside the editor");
                Rect pickerBounds=Named<Button>("ModifyDates").TransformToAncestor(editor).TransformBounds(new Rect(Named<Button>("ModifyDates").RenderSize));
                Check(selection.Top>=pickerBounds.Bottom,$"{size} date selector opens below its date field");
                Grid selectorRoot=(Grid)dates.Child;Grid selectorBody=(Grid)selectorRoot.Children[0];
                Check(selectorBody.ColumnDefinitions[0].ActualWidth>selectorBody.ColumnDefinitions[1].ActualWidth*1.35,$"{size} inline calendar has more room than the selected date list");
                Button clearDates=Named<Button>("InlineClearDates"),selectorConfirm=Named<Button>("InlineConfirmDates");
                Rect clearBounds=clearDates.TransformToAncestor(dates).TransformBounds(new Rect(clearDates.RenderSize));
                Check(clearBounds.Top>=0&&clearBounds.Right<=dates.ActualWidth-2&&clearBounds.Bottom<selectorConfirm.TransformToAncestor(dates).TransformBounds(new Rect(selectorConfirm.RenderSize)).Top,$"{size} clear dates stays visible above the compact footer");
                Check(selectorConfirm.ActualHeight<Named<Button>("SaveReminder").ActualHeight,$"{size} date selector footer is smaller than the schedule action");
                Check(Tree(dates).OfType<Button>().Count(b=>b.Name.StartsWith("InlineDate20",StringComparison.Ordinal))==35,$"{size} September selector omits the all-October sixth week");
                Click("InlinePreviousMonth");window.UpdateLayout();
                Check(Tree(Named<Border>("InlineDateSelector")).OfType<Button>().Count(b=>b.Name.StartsWith("InlineDate20",StringComparison.Ordinal))==42,$"{size} six-week August selector keeps all date rows");
                Click("InlineCancelDates");
                TextBox scheduleNotes=Named<TextBox>("ReminderNotes");scheduleNotes.Text="第一行\n第二行\n第三行";
                await Task.Delay(70);window.UpdateLayout();
                double expandedHeight=editor.Height;
                Click("ModifyDates");Click("InlineCancelDates");window.UpdateLayout();
                Check(editor.Height>=expandedHeight-1,$"{size} closing date selector preserves expanded note height");
                Check(Named<ScrollViewer>("EditorFormScroll").ScrollableHeight<1,$"{size} three-line schedule note avoids outer form scroll");
                Named<TextBox>("ReminderTitle").Text=new string('日',20);
                Named<System.Windows.Controls.CheckBox>("SetSpecificTime").IsChecked=true;
                Named<System.Windows.Controls.CheckBox>("SetSpecificTime").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Named<TextBox>("ScheduleHour").Text="13";Named<TextBox>("ScheduleMinute").Text="20";
                Named<System.Windows.Controls.CheckBox>("CreateAlarm").IsChecked=true;
                Named<System.Windows.Controls.CheckBox>("CreateAlarm").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Named<System.Windows.Controls.CheckBox>("EarlyReminder").IsChecked=true;
                Named<System.Windows.Controls.CheckBox>("EarlyReminder").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                scheduleNotes.Text=new string('备',100);await Task.Delay(100);window.UpdateLayout();
                Grid formGrid=(Grid)VisualTreeHelper.GetParent(Named<System.Windows.Controls.CheckBox>("SetSpecificTime"));
                Rect dateRect=Named<Button>("ModifyDates").TransformToAncestor(editor).TransformBounds(new Rect(Named<Button>("ModifyDates").RenderSize));
                Rect timeRect=formGrid.TransformToAncestor(editor).TransformBounds(new Rect(formGrid.RenderSize));
                Check(timeRect.Top-dateRect.Bottom>=10,$"{size} schedule date and time rows have clear separation");
                TextBlock notesLabel=Tree(editor).OfType<TextBlock>().Single(t=>t.Text=="备注（可选）");
                Grid earlyGrid=(Grid)VisualTreeHelper.GetParent(Named<System.Windows.Controls.CheckBox>("EarlyReminder"));
                Rect earlyRect=earlyGrid.TransformToAncestor(editor).TransformBounds(new Rect(earlyGrid.RenderSize));
                Rect labelRect=notesLabel.TransformToAncestor(editor).TransformBounds(new Rect(notesLabel.RenderSize));
                Check(labelRect.Top-earlyRect.Bottom>=10,$"{size} schedule options and note label have clear separation");
                Check(scheduleNotes.VerticalScrollBarVisibility==ScrollBarVisibility.Hidden&&scheduleNotes.LineCount>=3,$"{size} 100-character schedule note stays readable without a note scrollbar");
                Check(scheduleNotes.ActualHeight>=(size=="mini"?94:110),$"{size} 100-character schedule note grows enough to show its last line (height={scheduleNotes.ActualHeight:0.0}, lines={scheduleNotes.LineCount}, max={scheduleNotes.MaxHeight:0.0})");
                Check(Named<ScrollViewer>("EditorFormScroll").ScrollableHeight<1,$"{size} filled schedule options avoid outer scrolling (scroll={Named<ScrollViewer>("EditorFormScroll").ScrollableHeight:0.0})");
                FitModal(size+" filled schedule");Shot(size+"-schedule-filled");
                Click("EditorClose");
            }
            ReminderItem filledEdit=new(){Calendar=true,Title=new string('日',20),Notes=new string('备',100),HasTime=true,Enabled=true,ReminderCreated=true,EarlyEnabled=true,EarlyMinutes=30,Start=DateTime.Today.AddDays(1).AddHours(13)};
            foreach(string size in new[]{"mini","standard","comfortable","fullscreen"})
            {
                window.SetPageSize(size);window.FitViewport(1200,800);
                typeof(PlannerWindow).GetMethod("EditSchedule",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(window,[filledEdit,null]);window.UpdateLayout();
                Check(Named<TextBox>("ReminderNotes").Text.Length==100&&Named<ScrollViewer>("EditorFormScroll").ScrollableHeight<1,$"{size} fully populated single-date edit keeps its entire form visible (scroll={Named<ScrollViewer>("EditorFormScroll").ScrollableHeight:0.0}, viewport={Named<ScrollViewer>("EditorFormScroll").ViewportHeight:0.0}, extent={Named<ScrollViewer>("EditorFormScroll").ExtentHeight:0.0}, layout={Named<Grid>("PlannerEditorLayout").Height:0.0})");
                FitModal(size+" filled schedule edit");Shot(size+"-schedule-edit-filled");Click("EditorClose");
            }
            DateTime future=DateTime.Today.AddDays(1);
            ReminderItem multi=new(){Calendar=true,HasTime=true,Enabled=true,ReminderCreated=true,EarlyEnabled=true,EarlyMinutes=30,Title="多日编辑",Notes=new string('备',100),Start=DateTime.Today.AddDays(-1).AddHours(9),Repeat=ReminderRepeat.Dates,Dates=[DateTime.Today.AddDays(-1),future]};
            foreach(string size in new[]{"mini","standard","comfortable","fullscreen"})
            {
                window.SetPageSize(size);window.FitViewport(1200,800);
                typeof(PlannerWindow).GetField("_occurrenceDate",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(window,future);
                typeof(PlannerWindow).GetMethod("EditSchedule",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(window,[multi,null]);window.UpdateLayout();
                Grid compactEditor=Named<Grid>("PlannerEditorLayout");
                double? buttonTop=null,previousRight=0;
                foreach(string actionName in new[]{"DeleteOccurrence","DeleteFutureSchedules","DeleteGroup","CancelScheduleEdit","SaveReminder"})
                {
                    Button action=Named<Button>(actionName);
                    Rect bounds=action.TransformToAncestor(compactEditor).TransformBounds(new Rect(action.RenderSize));
                    Check(bounds.Left>=0&&bounds.Right<=compactEditor.ActualWidth+1&&bounds.Bottom<=compactEditor.ActualHeight+1,$"{size} multi-date {actionName} stays fully visible");
                    Check(buttonTop==null||Math.Abs(bounds.Top-buttonTop.Value)<1,$"{size} multi-date {actionName} shares the same footer row");
                    Check(bounds.Left>=previousRight-1,$"{size} multi-date {actionName} does not overlap its previous action");
                    TextBlock label=new(){Text=(string)action.Content,FontSize=action.FontSize};label.Measure(new System.Windows.Size(double.PositiveInfinity,double.PositiveInfinity));
                    Check(label.DesiredSize.Width<=action.ActualWidth-action.Padding.Left-action.Padding.Right-2,$"{size} multi-date {actionName} label fits its button");
                    buttonTop=bounds.Top;previousRight=bounds.Right;
                }
                Shot(size+"-multi-date-edit");var multiScroll=Named<ScrollViewer>("EditorFormScroll");Check(multiScroll.ScrollableHeight<1,$"{size} full-note multi-date edit avoids outer scrolling (scroll={multiScroll.ScrollableHeight:0.0}, viewport={multiScroll.ViewportHeight:0.0}, extent={multiScroll.ExtentHeight:0.0}, layout={compactEditor.ActualHeight:0.0}, note={Named<TextBox>("ReminderNotes").ActualHeight:0.0})");
                FitModal(size+" multi-date edit");Click("EditorClose");
            }
            DateTime dotDay=new(2026,9,22);
            await service.ChangeAsync(book=>{for(int n=0;n<4;n++)book.Items.Add(new ReminderItem{Calendar=true,Title="圆点排版"+n,Start=dotDay.AddHours(9+n),HasTime=true});});
            window.Navigate(false);
            typeof(PlannerWindow).GetField("_date",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(window,new DateTime(2026,9,21));
            foreach(string size in new[]{"mini","standard","comfortable","fullscreen"})
            {
                window.SetPageSize(size);window.FitViewport(1200,900);
                typeof(PlannerWindow).GetMethod("Render",BindingFlags.NonPublic|BindingFlags.Instance,null,Type.EmptyTypes,null)!.Invoke(window,null);window.UpdateLayout();
                Grid day=(Grid)Named<Button>("Day20260922").Content;
                int expected=size=="fullscreen"?2:1;
                Check(Tree(day).OfType<TextBlock>().Count(t=>t.Name.StartsWith("MonthPreview20260922",StringComparison.Ordinal))==expected,$"{size} month shows {expected} readable schedule previews (actual {Tree(day).OfType<TextBlock>().Count(t=>t.Name.StartsWith("MonthPreview20260922",StringComparison.Ordinal))}, window {window.Width:0}x{window.Height:0})");
                StackPanel dots=Tree(day).OfType<StackPanel>().Single(p=>p.Children.OfType<System.Windows.Shapes.Ellipse>().Any());
                Check(dots.HorizontalAlignment==System.Windows.HorizontalAlignment.Center&&dots.VerticalAlignment==System.Windows.VerticalAlignment.Center&&Grid.GetRow(dots)==1&&dots.Children.OfType<System.Windows.Shapes.Ellipse>().All(dot=>dot.Width>=8),$"{size} remaining schedule dots are larger and centered in the space below previews");
                Button dayButton=Named<Button>("Day20260922");
                Rect dotBounds=dots.TransformToAncestor(dayButton).TransformBounds(new Rect(dots.RenderSize));
                Check(dotBounds.Bottom<=dayButton.ActualHeight-3&&dotBounds.Top>=0,$"{size} month dots stay fully inside their day cell with bottom clearance ({dotBounds.Bottom:0.0}/{dayButton.ActualHeight:0.0})");
                Shot(size+"-month-dots");
            }
            await service.ChangeAsync(book=>book.Items.RemoveAll(item=>item.Title.StartsWith("圆点排版",StringComparison.Ordinal)));
            window.SetPageSize("mini");
            await service.ChangeAsync(book=>
            {
                book.WeekView=true;
                for(int n=0;n<4;n++)book.Items.Add(new ReminderItem{Calendar=true,Title="较长的日程标题用于窄列验证",Start=DateTime.Today.AddHours(8+n),HasTime=true,ReminderCreated=true,Notes="日程备注会在窄列自然换行"});
            });
            window.UpdateLayout();
            var compactCard=Tree(window).OfType<Button>().First(b=>b.Name.StartsWith("ScheduleCard",StringComparison.Ordinal));
            var compactTime=Tree(compactCard).OfType<TextBlock>().First(t=>t.Text=="08:00");
            var compactTitle=Tree(compactCard).OfType<TextBlock>().First(t=>t.Name=="ScheduleTitle");
            var compactNotes=Tree(compactCard).OfType<TextBlock>().First(t=>t.Name=="ScheduleNotes");
            var compactAlarm=Tree(compactCard).OfType<System.Windows.Shapes.Path>().First(p=>p.Name=="ScheduleAlarmIcon");
            Rect timeBounds=compactTime.TransformToAncestor(compactCard).TransformBounds(new Rect(compactTime.RenderSize));
            Check(timeBounds.Left>=0&&timeBounds.Right<=compactCard.ActualWidth+1,"mini week time stays inside a scrollable day column");
            Check(ReferenceEquals(VisualTreeHelper.GetParent(compactTime),VisualTreeHelper.GetParent(compactAlarm)),"mini week alarm icon stays beside the time instead of wrapping below it");
            Check(compactTitle.FontSize>compactTime.FontSize&&compactTitle.FontWeight==FontWeights.Bold&&compactTime.Foreground==PlannerTheme.TimeInk&&compactNotes.FontSize<=compactTime.FontSize&&compactNotes.Foreground==PlannerTheme.Muted,"week cards keep title, time and note as three distinct text levels");
            Shot("mini-week-dense");
            await service.ChangeAsync(book=>book.WeekView=false);
            foreach(string tier in new[]{"mini","standard","comfortable","fullscreen"})
            {
                window.SetPageSize(tier);window.FitViewport(1200,900);window.Navigate(false);window.UpdateLayout();typeof(PlannerWindow).GetMethod("OpenManage",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(window,null);window.UpdateLayout();
                var subtitle=Named<TextBlock>("ManageSubtitle");var search=Named<Grid>("ManageSearchFrame");
                Rect subtitleBounds=subtitle.TransformToAncestor(window).TransformBounds(new Rect(subtitle.RenderSize));
                Rect searchBounds=search.TransformToAncestor(window).TransformBounds(new Rect(search.RenderSize));
                Check(!subtitleBounds.IntersectsWith(searchBounds),$"{tier} manage subtitle and search do not overlap");
                Check(Named<Grid>("ManageScheduleGrid").ColumnDefinitions.Count==2,$"{tier} manage cards retain two columns at native preset width");
                Shot(tier+"-manage");window.Navigate(false);
            }
            await service.ChangeAsync(book=>{book.WeekView=false;book.Items.Clear();});
            await service.ChangeAsync(book=>{for(int n=0;n<3;n++)book.Items.Add(new ReminderItem{Id=Guid.NewGuid(),Relative=true,Title="并行倒计时"+(n+1),Start=DateTime.Now.AddMinutes(30+n*10),DurationSeconds=3600,Enabled=true});});
            foreach(string tier in new[]{"mini","standard","comfortable","fullscreen"})
            {
                window.SetPageSize(tier);window.FitViewport(1200,900);window.Navigate(true);window.UpdateLayout();
                Check(Tree(window).OfType<Border>().Count(b=>b.Name=="CountdownPanel")==3,$"{tier} three countdowns remain visible in one-line panels");
                Shot(tier+"-countdowns");
            }
            await service.ChangeAsync(book=>book.Items.Clear());
            ReminderItem endedAlarm=new(){Id=Guid.NewGuid(),Title="已结束闹钟",Start=DateTime.Today.AddDays(-1).AddHours(9),Repeat=ReminderRepeat.Once,Enabled=true};
            await service.ChangeAsync(book=>book.Items.Add(endedAlarm));
            window.Navigate(true);window.UpdateLayout();
            Border endedCard=Named<Border>("AlarmCard"+endedAlarm.Id.ToString("N"));
            Check(double.IsNaN(endedCard.Width)&&endedCard.ActualHeight<=96,"mini alarm cards stretch within a compact frame");
            System.Windows.Controls.CheckBox endedSwitch=Tree(endedCard).OfType<System.Windows.Controls.CheckBox>().Single(x=>x.Name=="AlarmEnabled");
            Check(endedSwitch.IsEnabled&&endedSwitch.IsChecked==false,"ended alarm shows an operable off switch");
            endedSwitch.IsChecked=true;endedSwitch.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            await Task.Delay(100);window.UpdateLayout();
            Check(service.Book.Items.Single(x=>x.Id==endedAlarm.Id).Enabled&&service.Book.Items.Single(x=>x.Id==endedAlarm.Id).Start>DateTime.Now,"ended alarm switch directly rearms a future occurrence");
            Check(!Tree(window).OfType<Grid>().Any(x=>x.Name=="PlannerEditorLayout"),"ended alarm switch does not open the editor");
            double previousCardWidth=0;
            foreach(var tier in new[]{"mini","standard","comfortable","fullscreen"})
            {
                window.SetPageSize(tier);window.FitViewport(1200,900);window.UpdateLayout();
                Border tierCard=Named<Border>("AlarmCard"+endedAlarm.Id.ToString("N"));
                Check(double.IsNaN(tierCard.Width)&&tierCard.ActualWidth>previousCardWidth&&tierCard.ActualHeight<=104,$"{tier} alarm cards fill their content column with roomier width by preset");
                previousCardWidth=tierCard.ActualWidth;
            }
            await service.ChangeAsync(book=>book.Items.RemoveAll(x=>x.Id==endedAlarm.Id));
            window.Navigate(false);
            window.SetPageSize("standard");window.FitViewport(960,516);window.UpdateLayout();
            Check(window.ActualWidth==912&&window.ActualHeight==436,"short high-DPI work area preserves independent usable width and desktop margin");
            Check(Tree(window).OfType<TextBlock>().Single(t=>t.Name=="PlannerBrandTitle").ActualHeight<35,"brand stays on one line at 200% desktop scaling");
            window.FitViewport(768,432);window.UpdateLayout();
            Check(Tree(window).OfType<TextBlock>().Single(t=>t.Name=="PlannerBrandTitle").ActualHeight<30,"brand stays on one line at narrow 250% desktop scaling");
            DateSelectionWindow compactPicker=new([],DateTime.Today){Owner=window};compactPicker.Show();compactPicker.UpdateLayout();
            var pickerImage=new RenderTargetBitmap((int)Math.Ceiling(compactPicker.ActualWidth),(int)Math.Ceiling(compactPicker.ActualHeight),96,96,PixelFormats.Pbgra32);pickerImage.Render(compactPicker);
            var pickerPng=new PngBitmapEncoder();pickerPng.Frames.Add(BitmapFrame.Create(pickerImage));using(var file=File.Create(Path.Combine(path,"short-date-picker.png")))pickerPng.Save(file);
            Button compactConfirm=Tree(compactPicker).OfType<Button>().Single(b=>b.Name=="ConfirmDates");
            Rect confirmBounds=compactConfirm.TransformToAncestor(compactPicker).TransformBounds(new Rect(compactConfirm.RenderSize));
            Check(compactPicker.ActualHeight<=window.ActualHeight-31&&compactConfirm.ActualHeight>=34&&confirmBounds.Bottom<=compactPicker.ActualHeight-3,$"short work area keeps the complete date picker action inside the parent (parent={window.ActualHeight:0}, picker={compactPicker.ActualHeight:0}, action height={compactConfirm.ActualHeight:0}, bottom={confirmBounds.Bottom:0})");
            compactPicker.Close();
            DateSelectionWindow sixWeekPicker=new([],new DateTime(2026,8,1)){Owner=window};sixWeekPicker.Show();sixWeekPicker.UpdateLayout();
            Button sixWeekConfirm=Tree(sixWeekPicker).OfType<Button>().Single(b=>b.Name=="ConfirmDates");
            Rect sixWeekAction=sixWeekConfirm.TransformToAncestor(sixWeekPicker).TransformBounds(new Rect(sixWeekConfirm.RenderSize));
            Check(sixWeekPicker.ActualHeight<=window.ActualHeight-31&&sixWeekConfirm.ActualHeight>=34&&sixWeekAction.Bottom<=sixWeekPicker.ActualHeight-3,"six-week picker keeps its complete action row on the short work area");
            sixWeekPicker.Close();
            var body=(ScrollViewer)typeof(PlannerWindow).GetField("_body",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(window)!;
            Check(body.ScrollableHeight>0,"very short calendar scrolls instead of clipping date text");Shot("short-viewport-calendar");
            window.Close();window=null;
            File.WriteAllLines(Path.Combine(path,"result.txt"),checks);
        }
        catch(Exception ex){File.WriteAllText(Path.Combine(path,"FAILED.txt"),ex.ToString());}
        finally{window?.Close();Application.Current.Shutdown();}
    }
}
