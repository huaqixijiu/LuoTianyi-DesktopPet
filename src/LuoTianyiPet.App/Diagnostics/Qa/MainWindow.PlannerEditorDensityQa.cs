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
                Check(Summary()==expected,$"{label} shows five sorted dates before a remaining-day badge");
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
            Edit(null);Check(Named<Grid>("PlannerEditorLayout").Width==608&&Named<Grid>("PlannerEditorLayout").Height==640,"alarm has compact modal dimensions");
            FitModal("alarm empty");Shot("alarm-empty");
            TextBox notes=Named<TextBox>("ReminderNotes");notes.Text="第一行备注\n第二行备注\n第三行备注";
            await Task.Delay(70);window.UpdateLayout();
            Check(notes.ActualHeight>48&&notes.ActualHeight<=121,$"three-line alarm notes grow within available dialog height (height {notes.ActualHeight:0})");
            Check(notes.ActualHeight>=95||notes.ExtentHeight>notes.ViewportHeight,"notes scroll internally when the native viewport cannot fit all three lines");
            Check(Named<ScrollViewer>("EditorFormScroll").ScrollableHeight<1,"three-line alarm notes fit without an outer form scroll");
            Shot("alarm-three-line-notes");Click("EditorClose");
            DateTime first=new(2026,9,17);CheckDates("same-five",Enumerable.Range(0,5).Select(n=>first.AddDays(n)).Reverse().ToArray(),"9月17日9月18日9月19日9月20日9月21日");
            CheckDates("same-six",Enumerable.Range(0,6).Select(n=>first.AddDays(n)).ToArray(),"9月17日9月18日9月19日9月20日9月21日+1天");
            DateTime[] cross=[new(2027,1,5),new(2026,12,31),new(2027,1,1),new(2027,1,3),new(2027,1,4)];
            CheckDates("cross-five",cross,"2026/12/312027/1/12027/1/32027/1/42027/1/5");
            CheckDates("cross-six",[..cross,new DateTime(2027,1,6)],"2026/12/312027/1/12027/1/32027/1/42027/1/5+1天");
            foreach(string size in new[]{"mini","standard","comfortable","fullscreen"})
            {
                window.SetPageSize(size);window.FitViewport(1200,800);Edit(null);
                Check(Named<Grid>("PlannerEditorLayout").Height==Math.Min(640,Math.Max(360,window.ActualHeight-(window.ActualHeight<650?80:40))),$"{size} alarm fits the native-size viewport");FitModal(size+" alarm");Shot(size+"-alarm");
                Click("ReminderModeCountdown");window.UpdateLayout();
                Check(Named<Grid>("PlannerEditorLayout").Width==608&&Named<Grid>("PlannerEditorLayout").Height<=Math.Min(640,Math.Max(360,window.ActualHeight-(window.ActualHeight<650?80:40))),$"{size} countdown shares width and fits the native-size viewport (actual {Named<Grid>("PlannerEditorLayout").Width}x{Named<Grid>("PlannerEditorLayout").Height})");
                FitModal(size+" countdown");Shot(size+"-countdown");Click("EditorClose");
            }
            window.SetPageSize("standard");window.FitViewport(960,516);window.UpdateLayout();
            Check(window.ActualWidth==936&&window.ActualHeight==492,"short high-DPI work area preserves independent usable width");
            Check(Tree(window).OfType<TextBlock>().Single(t=>t.Text=="洛天依 · 与你依起").ActualHeight<35,"brand stays on one line at 200% desktop scaling");
            window.FitViewport(768,432);window.UpdateLayout();
            Check(Tree(window).OfType<TextBlock>().Single(t=>t.Text=="洛天依 · 与你依起").ActualHeight<30,"brand stays on one line at narrow 250% desktop scaling");
            var body=(ScrollViewer)typeof(PlannerWindow).GetField("_body",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(window)!;
            Check(body.ScrollableHeight>0,"very short calendar scrolls instead of clipping date text");Shot("short-viewport-calendar");
            window.Close();window=null;
            File.WriteAllLines(Path.Combine(path,"result.txt"),checks);
        }
        catch(Exception ex){File.WriteAllText(Path.Combine(path,"FAILED.txt"),ex.ToString());}
        finally{window?.Close();Application.Current.Shutdown();}
    }
}
