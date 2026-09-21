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
                Check(Math.Abs(dateTop.Y-timeTop.Y)<1&&Math.Abs(picker.ActualHeight-time.ActualHeight)<1,
                    $"{label} selected dates and time share top and bottom edges (date {dateTop.Y:0.0}/{picker.ActualHeight:0.0}; time {timeTop.Y:0.0}/{time.ActualHeight:0.0})");
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
            Edit(null);Check(Named<Grid>("PlannerEditorLayout").Width==520&&Named<Grid>("PlannerEditorLayout").Height==525,"standard alarm has scaled modal dimensions");
            FitModal("alarm empty");Shot("alarm-empty");
            TextBox notes=Named<TextBox>("ReminderNotes");notes.Text="第一行备注\n第二行备注\n第三行备注";
            await Task.Delay(70);window.UpdateLayout();
            Check(notes.ActualHeight>42&&notes.ActualHeight<=90,$"three-line alarm notes grow within available dialog height (height {notes.ActualHeight:0})");
            Check(notes.ActualHeight>=85||notes.ExtentHeight>notes.ViewportHeight,"notes scroll internally when the native viewport cannot fit all three lines");
            var alarmScroll=Named<ScrollViewer>("EditorFormScroll");
            Check(alarmScroll.ScrollableHeight<1,$"three-line alarm notes fit without an outer form scroll (scroll={alarmScroll.ScrollableHeight:0.0}, viewport={alarmScroll.ViewportHeight:0.0}, extent={alarmScroll.ExtentHeight:0.0}, editor={Named<Grid>("PlannerEditorLayout").ActualHeight:0.0})");
            Shot("alarm-three-line-notes");Click("EditorClose");
            DateTime first=new(2026,9,17);CheckDates("same-five",Enumerable.Range(0,5).Select(n=>first.AddDays(n)).Reverse().ToArray(),"9月17日9月18日9月19日9月20日9月21日");
            CheckDates("same-six",Enumerable.Range(0,6).Select(n=>first.AddDays(n)).ToArray(),"9月17日9月18日9月19日9月20日9月21日+1天");
            DateTime[] cross=[new(2027,1,5),new(2026,12,31),new(2027,1,1),new(2027,1,3),new(2027,1,4)];
            CheckDates("cross-five",cross,"2026/12/312027/1/12027/1/32027/1/42027/1/5");
            CheckDates("cross-six",[..cross,new DateTime(2027,1,6)],"2026/12/312027/1/12027/1/32027/1/42027/1/5+1天");
            foreach(string size in new[]{"mini","standard","comfortable","fullscreen"})
            {
                window.SetPageSize(size);window.FitViewport(1200,800);Edit(null);
                double expectedHeight=size=="mini"?485:size=="standard"?525:540;
                double expectedWidth=size=="mini"?440:size=="standard"?520:size=="comfortable"?554:588;
                Check(Named<Grid>("PlannerEditorLayout").Width==expectedWidth&&Named<Grid>("PlannerEditorLayout").Height==Math.Min(expectedHeight,Math.Max(360,window.ActualHeight-100)),$"{size} alarm fits its page preset");FitModal(size+" alarm");Shot(size+"-alarm");
                if(size=="mini")Check(Named<TextBox>("ReminderTitle").FontSize==13&&Named<TextBox>("ReminderNotes").FontSize==13&&Named<Border>("AlarmTimeField").Height==36&&Named<Button>("SaveReminder").Height==43,"mini alarm dialog scales its fields, typography and actions together");
                Click("ReminderModeCountdown");window.UpdateLayout();
                Check(Named<Grid>("PlannerEditorLayout").Width==expectedWidth&&Named<Grid>("PlannerEditorLayout").Height<=Math.Min(expectedHeight,Math.Max(360,window.ActualHeight-100)),$"{size} countdown shares width and fits its page preset (actual {Named<Grid>("PlannerEditorLayout").Width}x{Named<Grid>("PlannerEditorLayout").Height})");
                FitModal(size+" countdown");Shot(size+"-countdown");Click("EditorClose");
            }
            window.Navigate(false);
            foreach(string size in new[]{"mini","standard","comfortable","fullscreen"})
            {
                window.SetPageSize(size);window.FitViewport(1200,800);
                typeof(PlannerWindow).GetMethod("EditSchedule",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(window,[null,new DateTime(2026,9,1)]);window.UpdateLayout();
                FitModal(size+" schedule");
                Check(Named<ScrollViewer>("EditorFormScroll").ScrollableHeight<1,$"{size} ordinary schedule form has no outer scroll");
                if(size=="mini")Check(Named<TextBox>("ReminderTitle").FontSize==13&&Named<TextBox>("ReminderNotes").FontSize==13&&Named<Button>("ModifyDates").Height==38&&Named<Button>("SaveReminder").Height==43,"mini schedule dialog scales labels, fields and actions as one compact system");
                Shot(size+"-schedule");Click("ModifyDates");window.UpdateLayout();
                Border dates=Named<Border>("InlineDateSelector");Grid editor=Named<Grid>("PlannerEditorLayout");
                Rect selection=dates.TransformToAncestor(editor).TransformBounds(new Rect(dates.RenderSize));
                Check(selection.Left>=0&&selection.Top>=0&&selection.Right<=editor.ActualWidth+1&&selection.Bottom<=editor.ActualHeight+1,$"{size} inline date selector stays inside the editor");
                Rect pickerBounds=Named<Button>("ModifyDates").TransformToAncestor(editor).TransformBounds(new Rect(Named<Button>("ModifyDates").RenderSize));
                Check(selection.Top>=pickerBounds.Bottom,$"{size} date selector opens below its date field");
                Check(Tree(dates).OfType<Button>().Count(b=>b.Name.StartsWith("InlineDate20",StringComparison.Ordinal))==35,$"{size} September selector omits the all-October sixth week");
                Click("InlineCancelDates");
                TextBox scheduleNotes=Named<TextBox>("ReminderNotes");scheduleNotes.Text="第一行\n第二行\n第三行";
                await Task.Delay(70);window.UpdateLayout();
                double expandedHeight=editor.Height;
                Click("ModifyDates");Click("InlineCancelDates");window.UpdateLayout();
                Check(editor.Height>=expandedHeight-1,$"{size} closing date selector preserves expanded note height");
                Check(Named<ScrollViewer>("EditorFormScroll").ScrollableHeight<1,$"{size} three-line schedule note avoids outer form scroll");
                Click("EditorClose");
            }
            window.SetPageSize("mini");window.FitViewport(1200,800);
            DateTime future=DateTime.Today.AddDays(1);
            ReminderItem multi=new(){Calendar=true,HasTime=true,Title="多日编辑",Start=DateTime.Today.AddDays(-1).AddHours(9),Repeat=ReminderRepeat.Dates,Dates=[DateTime.Today.AddDays(-1),future]};
            typeof(PlannerWindow).GetField("_occurrenceDate",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(window,future);
            typeof(PlannerWindow).GetMethod("EditSchedule",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(window,[multi,null]);window.UpdateLayout();
            Grid compactEditor=Named<Grid>("PlannerEditorLayout");
            foreach(string actionName in new[]{"DeleteOccurrence","DeleteFutureSchedules","DeleteGroup","SaveReminder"})
            {
                Button action=Named<Button>(actionName);
                Rect bounds=action.TransformToAncestor(compactEditor).TransformBounds(new Rect(action.RenderSize));
                Check(bounds.Left>=0&&bounds.Right<=compactEditor.ActualWidth+1&&bounds.Bottom<=compactEditor.ActualHeight+1,$"mini multi-date {actionName} stays fully visible");
            }
            FitModal("mini multi-date edit");Shot("mini-multi-date-edit");Click("EditorClose");
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
                Check(Tree(day).OfType<TextBlock>().Count(t=>t.Name.StartsWith("MonthPreview20260922",StringComparison.Ordinal))==expected,$"{size} month shows {expected} readable schedule previews");
                StackPanel dots=Tree(day).OfType<StackPanel>().Single(p=>p.Children.OfType<System.Windows.Shapes.Ellipse>().Any());
                Check(dots.HorizontalAlignment==System.Windows.HorizontalAlignment.Center&&dots.VerticalAlignment==System.Windows.VerticalAlignment.Center&&Grid.GetRow(dots)==1&&dots.Children.OfType<System.Windows.Shapes.Ellipse>().All(dot=>dot.Width>=8),$"{size} remaining schedule dots are larger and centered in the space below previews");
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
            await service.ChangeAsync(book=>{book.WeekView=false;book.Items.Clear();});
            ReminderItem endedAlarm=new(){Id=Guid.NewGuid(),Title="已结束闹钟",Start=DateTime.Today.AddDays(-1).AddHours(9),Repeat=ReminderRepeat.Once,Enabled=true};
            await service.ChangeAsync(book=>book.Items.Add(endedAlarm));
            window.Navigate(true);window.UpdateLayout();
            Border endedCard=Named<Border>("AlarmCard"+endedAlarm.Id.ToString("N"));
            Check(endedCard.Width==330&&endedCard.ActualHeight<=90,"mini alarm cards use a narrower compact frame with breathing room around the grid");
            System.Windows.Controls.CheckBox endedSwitch=Tree(endedCard).OfType<System.Windows.Controls.CheckBox>().Single(x=>x.Name=="AlarmEnabled");
            Check(endedSwitch.IsEnabled&&endedSwitch.IsChecked==false,"ended alarm shows an operable off switch");
            endedSwitch.IsChecked=true;endedSwitch.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            await Task.Delay(100);window.UpdateLayout();
            Check(service.Book.Items.Single(x=>x.Id==endedAlarm.Id).Enabled&&service.Book.Items.Single(x=>x.Id==endedAlarm.Id).Start>DateTime.Now,"ended alarm switch directly rearms a future occurrence");
            Check(!Tree(window).OfType<Grid>().Any(x=>x.Name=="PlannerEditorLayout"),"ended alarm switch does not open the editor");
            foreach(var tier in new[]{("mini",330d),("standard",370d),("comfortable",395d),("fullscreen",420d)})
            {
                window.SetPageSize(tier.Item1);window.FitViewport(1200,900);window.UpdateLayout();
                Border tierCard=Named<Border>("AlarmCard"+endedAlarm.Id.ToString("N"));
                Check(Math.Abs(tierCard.Width-tier.Item2)<1&&tierCard.Margin.Left>=4,$"{tier.Item1} alarm cards keep a compact width and visible outer gap");
            }
            await service.ChangeAsync(book=>book.Items.RemoveAll(x=>x.Id==endedAlarm.Id));
            window.Navigate(false);
            window.SetPageSize("standard");window.FitViewport(960,516);window.UpdateLayout();
            Check(window.ActualWidth==900&&window.ActualHeight==436,"short high-DPI work area preserves independent usable width and desktop margin");
            Check(Tree(window).OfType<TextBlock>().Single(t=>t.Text=="洛天依 · 与你依起").ActualHeight<35,"brand stays on one line at 200% desktop scaling");
            window.FitViewport(768,432);window.UpdateLayout();
            Check(Tree(window).OfType<TextBlock>().Single(t=>t.Text=="洛天依 · 与你依起").ActualHeight<30,"brand stays on one line at narrow 250% desktop scaling");
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
