using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using LuoTianyiPet.Core;
using LuoTianyiPet.Platform.Windows;
using Button = System.Windows.Controls.Button;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    private async Task RunReminderCardQaAsync(bool preview)
    {
        string path=Path.Combine(AppContext.BaseDirectory,"ReminderCardQa",DateTime.UtcNow.Ticks.ToString());
        Directory.CreateDirectory(path);
        List<string> checks=[];
        try
        {
            using ReminderService service=new(new LocalAppPaths(Path.Combine(path,"UserData")));
            await service.LoadAsync();
            DateTime at=DateTime.Now.AddMinutes(28);
            await service.ChangeAsync(b=>
            {
                b.Preferences.Sound=true;b.Preferences.Animation=true;
                var item=new ReminderItem{Calendar=true,Title="项目会议",Notes="准备演示资料，提前到会议室。",
                    Start=at,CheckedThrough=at,ReminderCreated=true,EarlyEnabled=true,EarlyMinutes=30};
                b.Items.Add(item);
                b.Occurrences.Add(new ReminderOccurrence{RuleId=item.Id,At=at,Phase=ReminderPhase.Early});
            });
            _reminders=service;
            IEnumerable<DependencyObject> Tree(DependencyObject d)
            {
                yield return d;
                for(int n=0;n<VisualTreeHelper.GetChildrenCount(d);n++)
                    foreach(var child in Tree(VisualTreeHelper.GetChild(d,n)))yield return child;
            }
            Button FindButton(string name)=>Tree(_reminderCard!).OfType<Button>().First(b=>b.Name==name);
            void Click(string name)=>FindButton(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            void Check(bool condition,string message)
            {if(!condition)throw new InvalidOperationException(message);checks.Add("PASS "+message);}
            void Shot(string name)
            {
                var card=_reminderCard!;card.UpdateLayout();
                int w=(int)Math.Ceiling(card.ActualWidth),h=(int)Math.Ceiling(card.ActualHeight);
                if(w<1||h<1)throw new InvalidOperationException("Reminder card has no visible geometry");
                RenderTargetBitmap bitmap=new(w,h,96,96,PixelFormats.Pbgra32);bitmap.Render(card);
                PngBitmapEncoder encoder=new();encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream=File.Create(Path.Combine(path,name+".png"));encoder.Save(stream);
                checks.Add($"PASS rendered {name} {w}x{h}");
            }
            RefreshReminderCardCore(true);
            Check(_reminderCard is {IsVisible:true}&&!_reminderCard.IsExpanded&&_reminderCard.ActualHeight<=85,"early starts as compact nonactivating capsule");
            Check(!ReminderAudio.IsPlaying&&_plannerAlarmReaction==null&&_plannerAlarmTopmost==null,
                "early reminder stays silent without alarm animation or transient topmost");
            await service.ChangeAsync(b=>{b.Preferences.Sound=false;b.Preferences.Animation=false;});
            Check(!_reminderCard!.ShowInTaskbar&&!_reminderCard.ShowActivated&&_reminderCard.Owner==this,"reminder is owned, nonactivating and absent from taskbar");
            double compactWidth=_reminderCard.Width;
            _reminderCard.Header.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=Mouse.MouseEnterEvent});
            Check(!_reminderCard.IsExpanded,"hover alone does not expand an early reminder");
            var headerTexts=Tree(_reminderCard.Header).OfType<TextBlock>().ToList();
            Check(headerTexts.Any(t=>t.Text=="项目会议"&&t.FontWeight==FontWeights.SemiBold)&&
                headerTexts.Any(t=>t.Text.Contains("还有")&&t.Foreground==PlannerTheme.ReminderAccent&&
                    t.FontSize<headerTexts.First(x=>x.Text=="项目会议").FontSize),
                "title and remaining time use distinct weight, color and size in the compact header");
            Shot("01-early-collapsed");
            Click("ToggleQuickReminder");await Task.Delay(240);Shot("02-early-expanded");
            Check(_reminderCard.Width>compactWidth+20,"compact capsule does not reserve the detail card width");
            Check(FindButton("KeepDueReminder").IsVisible&&FindButton("SkipOccurrence").IsVisible&&
                !Tree(_reminderCard).OfType<Button>().Any(b=>b.Name=="SnoozeReminder"),"early offers only keep-due and skip-current actions");
            var leftAction=FindButton("KeepDueReminder").TransformToAncestor(_reminderCard)
                .TransformBounds(new Rect(FindButton("KeepDueReminder").RenderSize));
            var rightAction=FindButton("SkipOccurrence").TransformToAncestor(_reminderCard)
                .TransformBounds(new Rect(FindButton("SkipOccurrence").RenderSize));
            Check(Math.Abs(leftAction.Top-rightAction.Top)<1&&rightAction.Left>leftAction.Right,
                "early actions share one row even when the collapsed capsule was narrow");
            Check(Equals(FindButton("SkipOccurrence").Content,"本次不再提醒"),
                "early full-dismissal action clearly includes the due reminder");
            int originalScale=_settings.Appearance.DisplayScalePercent;
            double priorWidth=0;
            foreach(int scale in new[]{60,95,100,150,200,300})
            {
                SetDisplayScalePercent(scale,false);RefreshReminderCardCore(true);await Task.Delay(220);
                Check(_reminderCard!.IsExpanded&&_reminderCard.ActualWidth<=GetQuickActionsWorkArea().Width&&
                    FindButton("KeepDueReminder").IsVisible&&FindButton("SkipOccurrence").IsVisible,
                    $"expanded early card fits work area at pet scale {scale}");
                Check(_reminderCard.Width>=priorWidth,$"expanded reminder width stays readable at pet scale {scale}");priorWidth=_reminderCard.Width;
                Shot($"02-early-expanded-scale-{scale}");
            }
            foreach(var (scale,width,uiScale) in new (int,double,double)[]{
                (50,220,.60),(75,220,.60),(79,220,.60),
                (80,250,.75),(95,250,.75),(120,250,.75),(124,250,.75),
                (125,285,.90),(150,285,.90),(175,285,.90),(179,285,.90),
                (180,320,1.05),(200,320,1.05),(295,320,1.05),(300,320,1.05)})
            {
                SetDisplayScalePercent(scale,false);RefreshReminderCardCore(true);
                Check(Math.Abs(_reminderCard!.ExpandedWidth-width)<1&&Math.Abs(_reminderCard.UiScale-uiScale)<.001,
                    $"pet scale {scale} uses stable reminder size tier {width:0} DIP");
            }
            SetDisplayScalePercent(originalScale,false);RefreshReminderCardCore(true);
            SetDisplayScalePercent(95,false);RefreshReminderCardCore(true);
            await service.ChangeAsync(b=>b.Items.Single().Notes=string.Concat(Enumerable.Repeat("准备演示资料并核对会议室安排。",24)));
            RefreshReminderCardCore(true);await Task.Delay(240);_reminderCard!.UpdateLayout();
            var action=FindButton("KeepDueReminder");
            var notesPreview=Tree(_reminderCard).OfType<TextBlock>().Single(t=>t.Name=="ReminderNotesPreview");
            Rect actionBounds=action.TransformToAncestor(_reminderCard).TransformBounds(new Rect(action.RenderSize));
            Shot("02-early-expanded-long-notes");
            Check(notesPreview.ActualHeight<=notesPreview.LineHeight*3+1&&notesPreview.Parent is StackPanel,
                "quick reminder notes show at most three lines without an inner scroller");
            Check(actionBounds.Bottom<=_reminderCard.ActualHeight+1&&actionBounds.Top>=0,
                $"long notes scroll inside the detail while actions remain visible ({actionBounds.Top:0}-{actionBounds.Bottom:0} of {_reminderCard.ActualHeight:0})");
            await service.ChangeAsync(b=>b.Items.Single().Notes="准备演示资料，提前到会议室。");
            SetDisplayScalePercent(originalScale,false);
            RefreshReminderCardCore(true);
            double initialLeft=Left,initialTop=Top;
            var dragWork=GetQuickActionsWorkArea();
            _isWindowDragging=true;
            Left=dragWork.Left-Width*.4;Top=dragWork.Top;
            RefreshReminderCardCore(true);PositionReminderCard();
            Check(_reminderCard!.IsVisible&&_reminderCard.Left>=dragWork.Left&&
                _reminderCard.Left+_reminderCard.Width<=dragWork.Right+1&&
                _reminderCard.Top>=dragWork.Top&&_reminderCard.Top+_reminderCard.ActualHeight<=dragWork.Bottom+1,
                "reminder remains visible and clamped while the pet is dragged toward a work-area edge");
            _isWindowDragging=false;Left=initialLeft;Top=initialTop;PositionReminderCard();
            if(preview)
            {
                _settings=_settings with {Media=_settings.Media with {ShowMusicIslands=true}};
                MediaControls.Visibility=Visibility.Visible;MediaControls.Opacity=1;MediaControlsTranslate.Y=0;
                UpdateLayout();PositionReminderCard();
                Check(TryGetMusicIslandBoundsInWindow(out Rect musicLocal),"music island has transformed bounds");
                Rect musicBounds=new(Left+musicLocal.Left,Top+musicLocal.Top,musicLocal.Width,musicLocal.Height);
                Rect reminderBounds=new(_reminderCard!.Left,_reminderCard.Top,_reminderCard.ActualWidth,_reminderCard.ActualHeight);
                var petBounds=GetPetImageAlphaBoundsInWindow();
                double reminderMidX=reminderBounds.Left+reminderBounds.Width/2;
                Check(CanShowMusicIslands&&MediaControls.IsVisible&&
                    !musicBounds.IntersectsWith(reminderBounds),"visible music island and reminder card remain separate");
                Check(reminderMidX>=Left+petBounds.Left&&reminderMidX<=Left+petBounds.Right,
                    "reminder stays horizontally attached to pet instead of moving to desktop side");
                Rect cluster=Rect.Union(reminderBounds,musicBounds);
                cluster=Rect.Union(cluster,new Rect(Left+petBounds.Left,Top+petBounds.Top,petBounds.Width,petBounds.Height));
                cluster.Inflate(12,12);
                var device=PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice??Matrix.Identity;
                Rect pixels=new(device.Transform(cluster.TopLeft),device.Transform(cluster.BottomRight));
                File.WriteAllText(Path.Combine(path,"geometry.txt"),
                    $"pet={Left+petBounds.Left:0},{Top+petBounds.Top:0},{petBounds.Width:0},{petBounds.Height:0}\n"+
                    $"music={musicBounds.Left:0},{musicBounds.Top:0},{musicBounds.Width:0},{musicBounds.Height:0}\n"+
                    $"reminder={reminderBounds.Left:0},{reminderBounds.Top:0},{reminderBounds.Width:0},{reminderBounds.Height:0}\n");
                await Task.Delay(250);
                using(var bitmap=new System.Drawing.Bitmap((int)Math.Ceiling(pixels.Width),(int)Math.Ceiling(pixels.Height)))
                using(var graphics=System.Drawing.Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen((int)Math.Floor(pixels.Left),(int)Math.Floor(pixels.Top),0,0,bitmap.Size);
                    bitmap.Save(Path.Combine(path,"desktop-cluster.png"),System.Drawing.Imaging.ImageFormat.Png);
                }
                Click("ToggleQuickReminder");await Task.Delay(250);
                Check(!_reminderCard.IsExpanded,"early reminder returns to the reference collapsed capsule");
                Shot("desktop-collapsed-card-render");
                await Task.Delay(350);
                using(var bitmap=new System.Drawing.Bitmap((int)Math.Ceiling(pixels.Width),(int)Math.Ceiling(pixels.Height)))
                using(var graphics=System.Drawing.Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen((int)Math.Floor(pixels.Left),(int)Math.Floor(pixels.Top),0,0,bitmap.Size);
                    bitmap.Save(Path.Combine(path,"desktop-collapsed.png"),System.Drawing.Imaging.ImageFormat.Png);
                }
                foreach(int petScale in new[]{50,95,150,200,300})
                {
                    SetDisplayScalePercent(petScale,false);
                    var scaleWork=GetQuickActionsWorkArea();
                    Left=scaleWork.Left+(scaleWork.Width-Width)/2;
                    Top=scaleWork.Top+(scaleWork.Height-Height)/2;
                    UpdateAccessoryLayoutForCurrentPosition();
                    RefreshReminderCardCore(true);UpdateLayout();PositionReminderCard();
                    await Task.Delay(180);
                    Check(TryGetMusicIslandBoundsInWindow(out Rect localIsland),$"music bounds exist at preview scale {petScale}");
                    var localPet=GetPetImageAlphaBoundsInWindow();
                    Rect petScreen=new(Left+localPet.Left,Top+localPet.Top,localPet.Width,localPet.Height);
                    Rect islandScreen=new(Left+localIsland.Left,Top+localIsland.Top,localIsland.Width,localIsland.Height);
                    Rect cardScreen=new(_reminderCard.Left,_reminderCard.Top,_reminderCard.ActualWidth,_reminderCard.ActualHeight);
                    Rect view=Rect.Union(Rect.Union(petScreen,islandScreen),cardScreen);
                    view.Inflate(12,12);
                    Matrix toDevice=PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice??Matrix.Identity;
                    Rect viewPixels=new(toDevice.Transform(view.TopLeft),toDevice.Transform(view.BottomRight));
                    using(var bitmap=new System.Drawing.Bitmap((int)Math.Ceiling(viewPixels.Width),(int)Math.Ceiling(viewPixels.Height)))
                    using(var graphics=System.Drawing.Graphics.FromImage(bitmap))
                    {
                        graphics.CopyFromScreen((int)Math.Floor(viewPixels.Left),(int)Math.Floor(viewPixels.Top),0,0,bitmap.Size);
                        bitmap.Save(Path.Combine(path,$"desktop-collapsed-{petScale}.png"),System.Drawing.Imaging.ImageFormat.Png);
                    }
                    if(petScale==95)
                    {
                        Click("ToggleQuickReminder");
                        await service.ChangeAsync(b=>b.Items.Single().Notes=string.Concat(Enumerable.Repeat("准备演示资料并核对会议室安排。",24)));
                        RefreshReminderCardCore(true);await Task.Delay(240);
                        Rect detail=new(_reminderCard.Left,_reminderCard.Top,_reminderCard.ActualWidth,_reminderCard.ActualHeight);
                        detail.Inflate(8,8);
                        Rect detailPixels=new(toDevice.Transform(detail.TopLeft),toDevice.Transform(detail.BottomRight));
                        using(var bitmap=new System.Drawing.Bitmap((int)Math.Ceiling(detailPixels.Width),(int)Math.Ceiling(detailPixels.Height)))
                        using(var graphics=System.Drawing.Graphics.FromImage(bitmap))
                        {
                            graphics.CopyFromScreen((int)Math.Floor(detailPixels.Left),(int)Math.Floor(detailPixels.Top),0,0,bitmap.Size);
                            bitmap.Save(Path.Combine(path,"desktop-expanded-long-notes-95.png"),System.Drawing.Imaging.ImageFormat.Png);
                        }
                        await service.ChangeAsync(b=>b.Items.Single().Notes="准备演示资料，提前到会议室。");
                        RefreshReminderCardCore(true);
                        Click("ToggleQuickReminder");
                    }
                }
                File.WriteAllLines(Path.Combine(path,"result.txt"),checks);
                File.WriteAllText(Path.Combine(path,"ready.txt"),"Collapsed early reminder is visible for a desktop screenshot.");
                await Task.Delay(TimeSpan.FromSeconds(90));
                _reminderCard?.Close();_reminderCard=null;_reminders=null;
                return;
            }
            Click("KeepDueReminder");await Task.Delay(250);RefreshReminderCardCore(true);Shot("03-early-feedback");
            Check(service.Book.Occurrences.Single().Phase==ReminderPhase.AcknowledgedEarly&&FindButton("UndoReminderAction").IsVisible,"early action persists and shows 5-second inline undo");
            Click("UndoReminderAction");await Task.Delay(250);RefreshReminderCardCore(true);
            Check(service.Book.Occurrences.Single().Phase==ReminderPhase.Early&&_reminderCard!.IsExpanded,"undo restores the expanded early state");
            Click("SkipOccurrence");await Task.Delay(250);RefreshReminderCardCore(true);Shot("04-skip-feedback");
            Check(service.Book.Occurrences.Single().Phase==ReminderPhase.Cancelled,"skip suppresses this occurrence including due");
            Click("UndoReminderAction");await Task.Delay(250);RefreshReminderCardCore(true);
            Check(service.Book.Occurrences.Single().Phase==ReminderPhase.Early,"skip undo restores the early occurrence");
            Click("KeepDueReminder");await Task.Delay(5200);RefreshReminderCardCore(true);Shot("05-due-countdown");
            Check(service.Book.Occurrences.Single().Phase==ReminderPhase.AcknowledgedEarly&&!_reminderCard!.IsExpanded,"early dismissal leaves a collapsed countdown");
            Check(_reminderFeedback==null,"inline feedback expires after five seconds without an action");
            await service.ChangeAsync(b=>{var o=b.Occurrences.Single();o.Phase=ReminderPhase.Due;o.RoundStartedAt=DateTime.Now;o.Revision++;});
            RefreshReminderCardCore(true);await Task.Delay(240);Shot("06-due-expanded");
            Check(_reminderCard!.IsExpanded&&FindButton("AcknowledgeReminder").IsVisible&&FindButton("SnoozeReminder").IsVisible,"due presents finish and ten-minute snooze");
            Click("SnoozeReminder");await Task.Delay(250);RefreshReminderCardCore(true);Shot("07-snooze-feedback");
            Check(service.Book.Occurrences.Single().Phase==ReminderPhase.DueSnoozed&&FindButton("UndoReminderAction").IsVisible,"due snooze persists with inline undo");
            _reminderFeedback=null;RefreshReminderCardCore(true);Shot("08-snooze-countdown");
            Check(_reminderCard!.IsVisible&&!_reminderCard.IsExpanded,"snoozed due remains as a compact countdown");
            await service.ChangeAsync(b=>{var o=b.Occurrences.Single();o.SnoozeAt=DateTime.Now;ReminderEngine.Advance(b,DateTime.Now);});
            RefreshReminderCardCore(true);Check(_reminderCard!.IsExpanded&&service.Book.Occurrences.Single().Phase==ReminderPhase.Due,"snooze deadline opens due card again");
            Click("AcknowledgeReminder");await Task.Delay(250);RefreshReminderCardCore(true);Shot("09-finish-feedback");
            Check(service.Book.Occurrences.Single().Phase==ReminderPhase.Done&&FindButton("UndoReminderAction").IsVisible,"finish persists and can be undone");
            Click("UndoReminderAction");await Task.Delay(250);RefreshReminderCardCore(true);
            Check(service.Book.Occurrences.Single().Phase==ReminderPhase.Due&&_reminderCard!.IsExpanded,"finish undo restores due card");
            DateTime dueWithoutEarly=DateTime.Now;
            await service.ChangeAsync(b=>
            {
                b.Items.Clear();b.Occurrences.Clear();
                var item=new ReminderItem{Title="只到点提醒",Start=dueWithoutEarly,CheckedThrough=dueWithoutEarly,
                    ReminderCreated=true,EarlyEnabled=false};
                b.Items.Add(item);
                b.Occurrences.Add(new ReminderOccurrence{RuleId=item.Id,At=dueWithoutEarly,Phase=ReminderPhase.Due});
            });
            RefreshReminderCardCore(true);await Task.Delay(240);
            Check(_reminderCard!.IsExpanded&&FindButton("SnoozeReminder").IsVisible,"alarm without early starts directly at due");
            Click("SnoozeReminder");await Task.Delay(250);_reminderFeedback=null;RefreshReminderCardCore(true);
            Shot("10-no-early-snooze-countdown");
            Check(service.Book.Occurrences.Single().Phase==ReminderPhase.DueSnoozed&&
                _reminderCard!.IsVisible&&!_reminderCard.IsExpanded,"alarm without early gains a countdown only after snooze");
            DateTime futureWithoutEarly=DateTime.Now.AddMinutes(5);
            await service.ChangeAsync(b=>
            {
                b.Items.Clear();b.Occurrences.Clear();
                var item=new ReminderItem{Title="关闭提前提醒的闹钟",Start=futureWithoutEarly,
                    CheckedThrough=futureWithoutEarly,ReminderCreated=true,EarlyEnabled=false};
                b.Items.Add(item);
                b.Occurrences.Add(new ReminderOccurrence{RuleId=item.Id,At=futureWithoutEarly,Phase=ReminderPhase.Waiting});
            });
            service.Book.Occurrences.Single().Phase=ReminderPhase.AcknowledgedEarly;
            RefreshReminderCardCore(true);
            Check(_reminderCard is null||!_reminderCard.IsVisible,
                "disabled early alarm cannot display a stale pre-due countdown card");
            await service.ChangeAsync(_=>{});
            Check(service.Book.Occurrences.Single().Phase==ReminderPhase.Waiting,
                "saving normalizes the stale early capsule to a silent due wait");
            File.WriteAllLines(Path.Combine(path,"result.txt"),checks);
            _reminderCard?.Close();_reminderCard=null;_reminders=null;
        }
        catch(Exception ex){File.WriteAllText(Path.Combine(path,"FAILED.txt"),ex.ToString());}
        finally{System.Windows.Application.Current.Shutdown();}
    }
}
