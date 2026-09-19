using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
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
        Check(window.Title.Contains("与你依起")&&Tree(window).OfType<TextBlock>().Any(t=>t.Text=="愿世界，如你我所愿~"),"new branding");
        // Input tests exercise the same routing methods as PreviewTextInput/paste, not save normalization.
        var hour=new TextBox();var minute=new TextBox();var input=new PlannerTimeInput(hour,minute);
        input.Input(hour,"123");Check(hour.Text=="12"&&minute.Text=="3","hour third digit routes immediately to minute");
        minute.CaretIndex=1;input.Input(minute,"4");Check(hour.Text=="12"&&minute.Text=="34","hour-to-minute continuation");
        minute.Text="";minute.CaretIndex=0;Check(input.Backspace(minute)&&hour.Text=="1","backspace crosses to hours");
        hour.Text="";minute.Text="";input.Input(minute,"830");Check(hour.Text=="8"&&minute.Text=="30","minute compact three digits split immediately");
        input.Input(hour,"5");Check(hour.Text=="83"&&minute.Text=="05","fourth compact digit never appears in one segment and remains subject to time validation");
        var h2=new TextBox();var m2=new TextBox();var i2=new PlannerTimeInput(h2,m2);i2.Input(m2,"1830");Check(h2.Text=="18"&&m2.Text=="30","minute four-digit paste");i2.Input(m2,"a");Check(m2.Text=="30","non-digit paste rejected");
        var alarm=new ReminderItem{Title="已有闹钟类型锁定",Start=DateTime.Today.AddDays(1).AddHours(8),Enabled=false};await service.ChangeAsync(b=>b.Items.Add(alarm));window.OpenItem(alarm.Id);window.UpdateLayout();
        Check(!Tree(window).OfType<Button>().Any(b=>b.Name is "ReminderModeAlarm" or "ReminderModeCountdown"),"existing alarm cannot switch type");Shot("r2-existing-alarm");Click("EditorClose");window.Navigate(true);window.UpdateLayout();Check(!Tree(window).OfType<Button>().Any(b=>b.Name=="AlarmMore"),"alarm overflow menus removed");
        typeof(PlannerWindow).GetField("_batch",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(window,false);window.Navigate(false);await service.ChangeAsync(b=>b.WeekView=false);window.UpdateLayout();Click("SetWorkdays");
        foreach(var size in new[]{(1920,1080),(2560,1440),(3840,2160)})
        foreach(double dpi in new[]{1d,1.25,1.5,1.75,2d,2.5,3d})
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
                Check(pickerBounds.Left>=0&&pickerBounds.Top>=0&&pickerBounds.Right<=window.ActualWidth&&pickerBounds.Bottom<=window.ActualHeight,"date popup fits scaled editor");Shot($"r2-dates-{size.Item1}x{size.Item2}-{dpi*100:0}",dpi);Click("InlineCancelDates");Click("EditorClose");
            }
        }
        window.FitViewport(SystemParameters.WorkArea.Width,SystemParameters.WorkArea.Height);Click("WorkdaysClose");
        foreach(string preset in new[]{"mini","standard","comfortable","fullscreen"})
        {
            window.SetPageSize(preset);window.FitViewport(1920,1032);window.UpdateLayout();
            Check(window.ActualWidth<=1920&&window.ActualHeight<=1032,"preset fits "+preset);
            if(preset=="standard")Check(Math.Abs(window.ActualWidth-1040)<1,"standard is compact 1040 DIP");
            if(preset=="fullscreen")Check(Math.Abs(window.ActualWidth-1920)<1&&Math.Abs(window.ActualHeight-1032)<1,"full screen fills work area");
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
        await service.ChangeAsync(b=>b.Items.RemoveAll(i=>i.Id==alarm.Id));
    }
}
