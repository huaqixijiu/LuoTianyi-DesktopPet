using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LuoTianyiPet.Core;
using Button=System.Windows.Controls.Button;
using TextBox=System.Windows.Controls.TextBox;

namespace LuoTianyiPet.App;
public partial class MainWindow
{
    private static async Task RunPlannerRevisionQa(PlannerWindow window,ReminderService service,string path,List<string> checks)
    {
        void Check(bool value,string label){if(!value)throw new InvalidOperationException("revision: "+label);checks.Add("PASS revision: "+label);}
        IEnumerable<DependencyObject> Tree(DependencyObject x){yield return x;for(int i=0;i<VisualTreeHelper.GetChildrenCount(x);i++)foreach(var c in Tree(VisualTreeHelper.GetChild(x,i)))yield return c;}
        FrameworkElement Named(string name)=>Tree(window).OfType<FrameworkElement>().FirstOrDefault(x=>x.Name==name)??throw new InvalidOperationException("Missing revision control: "+name);
        void Click(string name){((Button)Named(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));window.UpdateLayout();}
        void Shot(string label,double dpi=1)
        {
            window.UpdateLayout();var bitmap=new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth*dpi),(int)Math.Ceiling(window.ActualHeight*dpi),96*dpi,96*dpi,PixelFormats.Pbgra32);bitmap.Render(window);
            var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var output=File.Create(Path.Combine(path,label+".png"));png.Save(output);
        }
        window.Navigate(false);await service.ChangeAsync(b=>b.WeekView=false);window.UpdateLayout();
        Check(window.Title.Contains("与你相依")&&Tree(window).OfType<TextBlock>().Any(t=>t.Name=="PlannerBrandTitle"&&t.Inlines.OfType<Run>().Any(r=>r.Text.Contains("与你相依")))&&Tree(window).OfType<TextBlock>().Any(t=>t.Text=="愿世界，如你我所愿~"),"new branding");
        // Input tests exercise the same routing methods as PreviewTextInput/paste, not save normalization.
        var hour=new TextBox();var minute=new TextBox();var input=new PlannerTimeInput(hour,minute);
        input.Input(hour,"123");Check(hour.Text=="12"&&minute.Text=="3","hour third digit routes immediately to minute");
        minute.CaretIndex=1;input.Input(minute,"4");Check(hour.Text=="12"&&minute.Text=="34","hour-to-minute continuation");
        minute.Text="";minute.CaretIndex=0;Check(input.Backspace(minute)&&hour.Text=="1","backspace crosses to hours");
        hour.Text="";minute.Text="";input.Input(minute,"830");Check(hour.Text=="8"&&minute.Text=="30","minute compact three digits split immediately");
        input.Input(hour,"5");Check(hour.Text=="83"&&minute.Text=="05","fourth compact digit never appears in one segment and remains subject to time validation");
        var h2=new TextBox();var m2=new TextBox();var i2=new PlannerTimeInput(h2,m2);i2.Input(m2,"1830");Check(h2.Text=="18"&&m2.Text=="30","minute four-digit paste");i2.Input(m2,"a");Check(m2.Text=="30","non-digit paste rejected");
        var durationHour=new TextBox();var durationMinute=new TextBox();var durationSecond=new TextBox();var durationInput=new PlannerDurationInput(durationHour,durationMinute,durationSecond);
        durationInput.Input(durationHour,"235040");Check(durationHour.Text=="23"&&durationMinute.Text=="50"&&durationSecond.Text=="40","six-digit countdown entry from hours routes 23:50:40");
        durationHour.Text="";durationMinute.Text="";durationSecond.Text="";var fromMinute=new PlannerDurationInput(durationHour,durationMinute,durationSecond);fromMinute.Input(durationMinute,"1234");Check(durationMinute.Text=="12"&&durationSecond.Text=="34","countdown entry from minutes routes to seconds");
        durationHour.Text="";durationMinute.Text="";durationSecond.Text="";var fromSecond=new PlannerDurationInput(durationHour,durationMinute,durationSecond);fromSecond.Input(durationSecond,"235040");Check(durationHour.Text=="23"&&durationMinute.Text=="50"&&durationSecond.Text=="40","countdown entry from seconds carries left through minutes to hours");
        Check(fromSecond.Backspace(durationSecond)&&durationHour.Text=="2"&&durationMinute.Text=="35"&&durationSecond.Text=="04","countdown backspace reverses seconds carry");
        var alarm=new ReminderItem{Title="已有闹钟类型锁定",Notes=new string('测',200),Start=DateTime.Today.AddDays(1).AddHours(8),Enabled=false};await service.ChangeAsync(b=>b.Items.Add(alarm));window.OpenItem(alarm.Id);window.UpdateLayout();
        Check(!Tree(window).OfType<Button>().Any(b=>b.Name is "ReminderModeAlarm" or "ReminderModeCountdown"),"existing alarm cannot switch type");
        var savedNotes=(TextBox)Named("ReminderNotes");Check(savedNotes.Text.Length==200&&savedNotes.MaxLength==200&&savedNotes.ActualHeight>=66&&savedNotes.LineCount>1,"legacy 200-character alarm notes reopen as multiline without truncation");
        var deleteAlarm=(Button)Named("DeleteGroup");Check(deleteAlarm.ActualWidth>=128&&deleteAlarm.ActualHeight>=48,"alarm delete action retains a readable compact target");
        Shot("r2-existing-alarm");Click("EditorClose");window.Navigate(true);window.UpdateLayout();Check(!Tree(window).OfType<Button>().Any(b=>b.Name=="AlarmMore"),"alarm overflow menus removed");
        typeof(PlannerWindow).GetField("_batch",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(window,false);window.Navigate(false);await service.ChangeAsync(b=>b.WeekView=false);window.UpdateLayout();Click("SetWorkdays");
        foreach(var size in new[]{(1920,1080)})
        foreach(double dpi in new[]{1d,1.5d,2d})
        {
            double width=size.Item1/dpi,height=(size.Item2-48*dpi)/dpi;
            window.FitViewport(width,height);window.UpdateLayout();
            Check(window.ActualWidth<=width&&window.ActualHeight<=height,$"viewport {size.Item1}x{size.Item2} at {dpi:P0} fits work area");
            var bottom=Named("ResetSelectedRestDays");Rect bounds=bottom.TransformToAncestor(window).TransformBounds(new Rect(0,0,bottom.ActualWidth,bottom.ActualHeight));
            Check(bounds.Bottom<=window.ActualHeight&&bounds.Right<=window.ActualWidth,"sidebar actions remain within scaled viewport");
            Shot($"r2-layout-{size.Item1}x{size.Item2}-{dpi*100:0}",dpi);
            if(dpi is 1.5 or 2)
            {
                typeof(PlannerWindow).GetMethod("Edit",BindingFlags.NonPublic|BindingFlags.Instance,null,new[]{typeof(ReminderItem),typeof(bool),typeof(DateTime?)},null)!.Invoke(window,new object?[]{null,true,DateTime.Today});window.UpdateLayout();Click("ModifyDates");
                var picker=Named("InlineDateSelector");Rect pickerBounds=picker.TransformToAncestor(window).TransformBounds(new Rect(0,0,picker.ActualWidth,picker.ActualHeight));
                Check(pickerBounds.Left>=-1&&pickerBounds.Top>=-1&&pickerBounds.Right<=window.ActualWidth+1&&pickerBounds.Bottom<=window.ActualHeight+1,$"date popup fits scaled editor ({pickerBounds}, window={window.ActualWidth:0}x{window.ActualHeight:0})");Shot($"r2-dates-{size.Item1}x{size.Item2}-{dpi*100:0}",dpi);Click("InlineCancelDates");Click("EditorClose");
            }
        }
        window.FitViewport(SystemParameters.WorkArea.Width,SystemParameters.WorkArea.Height);window.UpdateLayout();if(Tree(window).OfType<Button>().Any(b=>b.Name=="WorkdaysClose"))Click("WorkdaysClose");
        foreach(string preset in new[]{"mini","standard","comfortable","fullscreen"})
        {
            window.SetPageSize(preset);window.FitViewport(1920,1032);window.UpdateLayout();
            Check(window.ActualWidth<=1920&&window.ActualHeight<=1032,"preset fits "+preset);
            double expectedWidth=preset switch{"mini"=>840,"comfortable"=>1020,"fullscreen"=>1100,_=>940};
            double expectedHeight=preset switch{"mini"=>620,"comfortable"=>710,"fullscreen"=>760,_=>660};
            Check(Math.Abs(window.ActualWidth-expectedWidth)<1&&Math.Abs(window.ActualHeight-expectedHeight)<1,"desktop tool bounds for "+preset);
            Shot("r4-size-"+preset);
        }
        window.SetPageSize("standard");
        foreach(var month in new[]{new DateTime(2026,9,1),new DateTime(2026,8,1),new DateTime(2027,2,1)})
        {
            var picker=new DateSelectionWindow(Array.Empty<DateTime>(),month,false){Owner=window};picker.Show();picker.UpdateLayout();
            int weeks=(((int)month.DayOfWeek+6)%7+DateTime.DaysInMonth(month.Year,month.Month)+6)/7;
            Check(Tree(picker).OfType<Button>().Count(b=>b.Name.StartsWith("Date")&&b.Name.Length==12)==weeks*7,"date picker uses required weeks "+month.ToString("yyyyMM"));
            picker.Close();
        }
        // Drag reversal restores the pre-drag values, respecting locked history.
        var date=DateTime.Today.AddDays(1);var selected=new HashSet<DateTime>{date.AddDays(2)};var selector=new InlineDateSelector(selected,new HashSet<DateTime>{date.AddDays(2)},service.Book,date,()=>{},()=>{},()=>{});
        typeof(InlineDateSelector).GetField("_dragSnapshot",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(selector,new HashSet<DateTime>(selected));typeof(InlineDateSelector).GetField("_dragStart",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(selector,date);
        selector.ExtendDrag(date.AddDays(4));Check(selected.Count==5,"drag traverses intermediate dates and preserves locked day");selector.ExtendDrag(date.AddDays(1));Check(selected.SetEquals(new[]{date,date.AddDays(1),date.AddDays(2)}),"drag backtracking restores original selection");
        foreach(var max in new[]{24,59})
        {
            var roller=new PlannerNumber("CycleTest",0,max);roller.ApplyDragOffset(0,18);
            Check(roller.Value==(max==24?23:59),"upward drag crosses zero "+max);
            roller.ApplyDragOffset(0,126);Check(roller.Value==(max==24?17:53),"drag reaches previous values without full loop "+max);
            roller.ApplyDragOffset(0,0);Check(roller.Value==0,"drag reversal returns to start "+max);
            roller.Step(-1);roller.Step(1);Check(roller.Value==0,"wheel and keyboard step wrap in both directions "+max);
            typeof(PlannerNumber).GetField("_dragging",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(roller,true);
            roller.Input.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0){RoutedEvent=System.Windows.Input.Mouse.LostMouseCaptureEvent});
            Check((bool)typeof(PlannerNumber).GetField("_dragging",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(roller)!,"child capture transfer does not terminate roller drag "+max);
        }
        window.Navigate(true);window.UpdateLayout();Click("NewAlarm");
        Check(Named("AlarmTimeField").ActualWidth==110&&Named("AlarmHour") is TextBox&&Named("AlarmMinute") is TextBox,"alarm uses a compact segmented time field");
        Check(Named("ReminderNotes") is TextBox alarmNotes&&alarmNotes.MaxLength==100&&alarmNotes.ActualHeight>=38&&Named("AlarmNotesHint") is TextBlock,"alarm editor starts with one-line notes and a 100-character limit");
        var emptyNotes=(TextBox)Named("ReminderNotes");var emptyHint=(TextBlock)Named("AlarmNotesHint");
        var notesCenter=emptyNotes.TransformToAncestor(window).Transform(new Point(0,emptyNotes.ActualHeight/2)).Y;
        var hintCenter=emptyHint.TransformToAncestor(window).Transform(new Point(0,emptyHint.ActualHeight/2)).Y;
        Check(emptyHint.Visibility==Visibility.Visible&&Math.Abs(notesCenter-hintCenter)<5,"empty alarm note hint is vertically centered");
        ((TextBox)Named("AlarmHour")).Text="18";((TextBox)Named("AlarmMinute")).Text="30";
        Check(Tree(window).OfType<TextBlock>().Any(t=>t.Text.EndsWith("18:30 响铃")),"single alarm next-time hint follows segmented edits");
        var alarmMode=(Button)Named("ReminderModeAlarm");var countdownMode=(Button)Named("ReminderModeCountdown");
        Check(!Equals(alarmMode.Background,countdownMode.Background),"mode capsule visibly distinguishes alarm selection");
        Shot("r8-alarm-editor");
        Click("ReminderModeCountdown");Check(!Equals(((Button)Named("ReminderModeAlarm")).Background,((Button)Named("ReminderModeCountdown")).Background),"mode capsule visibly distinguishes countdown selection");
        ((TextBox)Named("CountdownHours")).Text="23";((TextBox)Named("CountdownMinutes")).Text="50";((TextBox)Named("CountdownSeconds")).Text="40";Shot("r8-countdown-editor");Click("ReminderModeAlarm");
        Tree(window).OfType<Button>().Single(b=>Equals(b.Content,"每周")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));window.UpdateLayout();
        for(int n=0;n<7;n++){var button=(Button)Named("AlarmWeekday"+n);Check(button.ActualWidth-button.Padding.Left-button.Padding.Right>button.FontSize+2,"weekday glyph has full width "+n);}
        var advance=(TextBox)Named("EarlyMinutes");Check(!advance.IsEnabled&&advance.Text=="30"&&advance.Width>=56&&advance.Width<=76,"alarm lead field matches schedule default and active preset");
        var early=(System.Windows.Controls.CheckBox)Named("EarlyReminder");early.IsChecked=true;early.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));window.UpdateLayout();
        Click("AlarmWeekday0");advance=(TextBox)Named("EarlyMinutes");int before=service.Book.Items.Count;
        foreach(string invalid in new[]{"0","61","abc"}){advance.Text=invalid;Click("SaveReminder");await Task.Delay(60);Check(service.Book.Items.Count==before&&((TextBlock)Named("EditorError")).Text.Contains("1～60"),"alarm rejects early minutes "+invalid);}
        advance.Text="60";((TextBox)Named("ReminderNotes")).Text="闹钟备注测试";Shot("r5-weekly-alarm-60");Click("SaveReminder");await Task.Delay(180);Check(service.Book.Items.Count==before+1&&service.Book.Items.Last().EarlyMinutes==60&&service.Book.Items.Last().Notes=="闹钟备注测试","alarm saves 60 minute limit and notes");
        var added=service.Book.Items.Last().Id;await service.ChangeAsync(b=>b.Items.RemoveAll(i=>i.Id==added||i.Id==alarm.Id));
        await service.ChangeAsync(b=>b.WeekView=true);
        double NextWeekX(DateTime date){typeof(PlannerWindow).GetField("_date",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(window,date);window.Navigate(false);window.UpdateLayout();var button=Tree(window).OfType<Button>().Single(b=>Equals(b.ToolTip,"下一周"));return button.TransformToAncestor(window).Transform(new Point()).X;}
        Check(Math.Abs(NextWeekX(new DateTime(2026,12,7))-NextWeekX(new DateTime(2026,12,21)))<.5,"week navigation arrows stay fixed for one- and two-digit dates");
        await service.ChangeAsync(b=>b.WeekView=false);
    }
}
