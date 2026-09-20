using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LuoTianyiPet.Core;
using Size = System.Windows.Size;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    private async Task RunMessageInboxQaAsync()
    {
        string directory=Path.Combine(AppContext.BaseDirectory,"MessageInboxQa"); Directory.CreateDirectory(directory);
        List<string> checks=[]; int serial=0;
        void Check(bool ok,string text) { if(!ok)throw new InvalidOperationException(text);checks.Add("PASS "+text); }
        MessageNotificationSummary Sample(MessageProvider p,string name="小雨",string? preview=null) => new(p,DateTimeOffset.Now,
            name,MessagePreview:preview??"周末见吗？",NotificationKey:$"qa:{++serial}");
        try
        {
            await Task.Delay(1200);
            _messageNotificationStatusTimer.Stop(); _idleSceneTimer.Stop(); _timeSceneTimer.Stop(); _musicDetectionTimer.Stop();
            _stateMachine.CancelActiveReaction(); _stateMachine.SetContinuousState(PetContinuousState.Idle);
            _stateMachine.SetDisplayMode(PetDisplayMode.FullBodyInteractive);
            _settings=_settings with { Notifications=_settings.Notifications with {EnableMessageReminders=true,EnableQqDetailedReminders=true,EnableWeChatDetailedReminders=true} };
            _inboxSafe=true; RefreshMessageInbox(); _inboxWindow!.AutomationMode=true;
            void ResetPreview()
            {
                _messageInbox.Clear(MessageProvider.WeChat,DateTimeOffset.MinValue);
                _messageInbox.Clear(MessageProvider.Qq,DateTimeOffset.MinValue);
                _inboxWindow.Interaction.Close();RefreshMessageInbox();
            }
            void ShowPreview(MessageProvider provider,string? sender,string? body,int? unread)
            {
                _messageInbox.Add(new(provider,DateTimeOffset.Now,sender,UnreadCount:unread,
                    MessagePreview:body,NotificationKey:$"hover:{++serial}"));
                _inboxWindow.Interaction.Close();_inboxWindow.Interaction.Click(provider);RefreshMessageInbox();
            }
            ResetPreview();ShowPreview(MessageProvider.WeChat,"栀","文档我已经发你了",1);
            Check(_inboxWindow.QaPreviewContent()==("栀","文档我已经发你了","来自微信 · 1 条未读")&&
                _inboxWindow.QaBadge(MessageProvider.WeChat)=="1","WeChat full detail uses sender, latest body and verified unread count");
            CaptureInboxQa(directory,"hover-wechat-full.png");
            ResetPreview();ShowPreview(MessageProvider.Qq,"产品讨论群","今晚版本需要再确认一下",3);
            Check(_inboxWindow.QaPreviewContent()==("产品讨论群","今晚版本需要再确认一下","来自 QQ · 3 条未读")&&
                _inboxWindow.QaBadge(MessageProvider.Qq)=="3","QQ full detail shows the available conversation summary");
            CaptureInboxQa(directory,"hover-qq-full.png");
            ResetPreview();ShowPreview(MessageProvider.WeChat,"栀","宝宝不敢",null);
            Check(_inboxWindow.QaPreviewContent()==("栀","宝宝不敢","来自微信 · 1 条提醒")&&
                _inboxWindow.QaBadge(MessageProvider.WeChat)==string.Empty,
                "Detailed WeChat event without an exact unread badge labels the local reminder count honestly");
            CaptureInboxQa(directory,"hover-wechat-count-unknown.png");
            ResetPreview();ShowPreview(MessageProvider.Qq,"好友","稍后聊",null);
            Check(_inboxWindow.QaPreviewContent()==("好友","稍后聊","来自 QQ · 1 条提醒")&&
                _inboxWindow.QaBadge(MessageProvider.Qq)==string.Empty,
                "QQ Toast without client unread count distinguishes local reminders from unread messages");
            CaptureInboxQa(directory,"hover-qq-count-unknown.png");
            ResetPreview();
            for(int i=0;i<7;i++) _messageInbox.Add(new(MessageProvider.WeChat,DateTimeOffset.Now.AddSeconds(i),
                $"会话{i}",UnreadCount:i+1,MessagePreview:$"第{i}条最新摘要",NotificationKey:$"multi:{++serial}",
                WeChatSessionKey:$"session-{i}"));
            _inboxWindow.Interaction.Click(MessageProvider.WeChat);RefreshMessageInbox();_inboxWindow.UpdateLayout();
            var summaries=_inboxWindow.QaConversationSummaries();
            Check(summaries.Count==7&&summaries[0]==("会话6","第6条最新摘要","7 条未读")&&
                summaries[6]==("会话0","第0条最新摘要","1 条未读"),
                "Hover lists each unread WeChat conversation newest first with its reliable session count");
            Check(_inboxWindow.QaScrollExtent>_inboxWindow.QaScrollViewport&&
                _inboxWindow.PanelRectangle.Height<=MessageRailPlacement.PanelHeight+1,
                "Many conversations scroll inside a bounded hover panel");
            CaptureInboxQa(directory,"hover-wechat-multiple-top.png");
            _inboxWindow.QaScrollToEnd();_inboxWindow.UpdateLayout();
            Check(_inboxWindow.QaScrollOffset>0,"Older conversations remain reachable by scrolling");
            CaptureInboxQa(directory,"hover-wechat-multiple-bottom.png");
            _inboxWindow.Interaction.Close();RefreshMessageInbox();
            _inboxWindow.Interaction.Click(MessageProvider.WeChat);RefreshMessageInbox();_inboxWindow.UpdateLayout();
            Check(_inboxWindow.QaScrollOffset==0&&_inboxWindow.QaConversationSummaries()[0].Title=="会话6",
                "Reopening a platform preview starts at its newest conversation");
            ResetPreview();
            for(int i=0;i<3;i++) _messageInbox.Add(new(MessageProvider.Qq,DateTimeOffset.Now.AddSeconds(i),
                $"QQ群{i}",UnreadCount:3,MessagePreview:$"QQ群摘要{i}",NotificationKey:$"qq-multi:{++serial}"));
            _inboxWindow.Interaction.Click(MessageProvider.Qq);RefreshMessageInbox();
            Check(_inboxWindow.QaConversationSummaries().Count==3&&
                _inboxWindow.QaConversationSummaries().All(row=>row.Count=="1 条提醒"),
                "QQ multi-conversation tray count is not misrepresented as a per-conversation unread count");
            CaptureInboxQa(directory,"hover-qq-multiple.png");
            bool requested=false;
            Func<MessageProvider,bool> activation=provider=>{requested=provider==MessageProvider.Qq;return true;};
            _inboxWindow.ProviderActivationRequested+=activation;_inboxWindow.QaClickIcon(MessageProvider.Qq);
            Check(requested&&_inboxWindow.Interaction.OpenProvider is null,
                "Clicking a platform icon requests its application instead of requiring click for preview");
            requested=false;_inboxWindow.Interaction.Click(MessageProvider.Qq);RefreshMessageInbox();
            _inboxWindow.QaClickPreviewCard();
            Check(requested&&_inboxWindow.Interaction.OpenProvider is null,
                "Clicking its preview card requests the same platform application");
            _inboxWindow.ProviderActivationRequested-=activation;
            foreach(MessageProvider provider in new[]{MessageProvider.Qq,MessageProvider.WeChat})
            {
                ResetPreview();ShowPreview(provider,null,null,null);
                string source=MessageProviderMatcher.GetDisplayName(provider);
                Check(_inboxWindow.QaPreviewContent()==(source+"提醒","有新消息",string.Empty)&&
                    _inboxWindow.QaBadge(provider)==string.Empty,$"{source} source-only fallback has no invented sender, body or count");
                CaptureInboxQa(directory,$"hover-{provider}-source-only.png");
            }
            ResetPreview();ShowPreview(MessageProvider.WeChat,"栀",null,1);
            Check(_inboxWindow.QaPreviewContent()==("栀","有新消息","来自微信 · 1 条未读"),
                "Sender-only detail uses a readable body fallback");
            ResetPreview();ShowPreview(MessageProvider.WeChat,null,"文档已发送",1);
            Check(_inboxWindow.QaPreviewContent()==("微信提醒","文档已发送","来自微信 · 1 条未读"),
                "Body-only detail keeps the platform fallback title");
            ResetPreview();ShowPreview(MessageProvider.WeChat,"栀",new string('文',100),1);
            _inboxWindow.UpdateLayout();
            Check(_inboxWindow.QaPreviewBodyHeight<=36.5,"Long body remains inside two preview lines");
            CaptureInboxQa(directory,"hover-long-body.png");
            ResetPreview();Check(!_inboxWindow.IsVisible,"No unread reminder hides the entire platform rail");
            ShowPreview(MessageProvider.Qq,null,null,null);
            Check(_inboxWindow.IsVisible&&_inboxWindow.VisibleOrder.Count==1&&_messageInbox.Total(MessageProvider.WeChat)==0,
                "QQ-only state does not create a WeChat icon");
            _messageInbox.Add(new(MessageProvider.WeChat,DateTimeOffset.Now,"栀",UnreadCount:1,
                MessagePreview:"新消息",NotificationKey:$"hover:{++serial}"));RefreshMessageInbox();
            Check(_inboxWindow.IsVisible&&_messageInbox.Total(MessageProvider.Qq)>0&&_messageInbox.Total(MessageProvider.WeChat)>0,
                "Both platforms retain independent reminders");
            _messageInbox.Prune(message=>message.Provider==MessageProvider.WeChat);RefreshMessageInbox();
            Check(_messageInbox.Total(MessageProvider.WeChat)==0&&_messageInbox.Total(MessageProvider.Qq)>0,
                "Read transition removes only the cleared platform icon");
            ResetPreview();
            foreach(string style in new[]{"full-body-long-hair","full-body-crystal-dress","full-body-classic-cat-ears"})
            {
                ApplyAppearancePreferences(_settings.Appearance with {FullBodyStyle=style,DisplayScalePercent=100},false);
                await Task.Delay(400); _stateMachine.SetContinuousState(PetContinuousState.Idle);
                PlayResolvedContinuousAnimation(); await Task.Delay(180);
                Check(_animationPlayer?.CurrentAnimationId==AppearanceOptionIds.ResolveFullBodyAnimation(style),$"{style}: correct selected idle artwork");
                foreach(string side in new[]{"left","right"})
                {
                    List<BitmapSource> previews=[];
                    for(int state=1;state<=8;state++)
                    {
                        _messageInbox.Clear(MessageProvider.WeChat,DateTimeOffset.MinValue); _messageInbox.Clear(MessageProvider.Qq,DateTimeOffset.MinValue);
                        _inboxWindow.Interaction.Close(); _inboxSide=null;
                        var work=GetCurrentWorkArea();
                        Left=work.Left+(side=="left" ? 70 : work.Width-Width-70); Top=work.Top+(work.Height-Height)/2;
                        string? animation=_animationPlayer?.CurrentAnimationId; var plan=_stateMachine.Resolve(DateTimeOffset.Now).Source;
                        if(state<=5) { await BeginMessageNotificationAsync(Sample(MessageProvider.WeChat)); await BeginMessageNotificationAsync(Sample(MessageProvider.WeChat)); await BeginMessageNotificationAsync(Sample(MessageProvider.WeChat,"林同学","文件发你了")); }
                        if(state<=3 || state==6 || state==7)
                        { await BeginMessageNotificationAsync(Sample(MessageProvider.Qq,"小洛","一起听歌吗？")); await BeginMessageNotificationAsync(Sample(MessageProvider.Qq,"小洛")); await BeginMessageNotificationAsync(Sample(MessageProvider.Qq,"阿言","我到啦")); }
                        if(state==2 || state==5) _inboxWindow.Interaction.Click(MessageProvider.WeChat);
                        if(state==3 || state==7) _inboxWindow.Interaction.Click(MessageProvider.Qq);
                        nint foreground=NotificationQaGetForegroundWindow();RefreshMessageInbox();
                        Check(NotificationQaGetForegroundWindow()==foreground,$"{style}/{side}/{state}: passive rail refresh preserves foreground focus");
                        await Task.Delay(70);
                        Check(_animationPlayer?.CurrentAnimationId==animation && _stateMachine.Resolve(DateTimeOffset.Now).Source==plan,
                            $"{style}/{side}/{state}: incoming reminders preserve current animation");
                        Check(_inboxWindow.IsVisible==(state!=8),$"{style}/{side}/{state}: zero reminders hide whole group");
                        if(state!=8) Check(_inboxSide==(side=="left"?MessageRailSide.Right:MessageRailSide.Left),$"{style}/{side}/{state}: rail faces available side");
                        Check(_messageInbox.Total(MessageProvider.WeChat)==_messageInbox.Rows(MessageProvider.WeChat).Sum(r=>(long)r.Count) &&
                            _messageInbox.Total(MessageProvider.Qq)==_messageInbox.Rows(MessageProvider.Qq).Sum(r=>(long)r.Count),$"{style}/{side}/{state}: badge totals equal row totals");
                        var image=CaptureInboxQa(directory,$"{style}-{side}-{state}.png");previews.Add(image);
                    }
                    SaveInboxSheet(directory,$"{style}-{side}-overview.png",previews);
                }
            }
            _messageInbox.Clear(MessageProvider.WeChat,DateTimeOffset.MinValue); _messageInbox.Clear(MessageProvider.Qq,DateTimeOffset.MinValue);
            for(int i=0;i<105;i++) _messageInbox.Add(Sample(MessageProvider.WeChat,"很长很长很长很长的联系人名称用于检查省略号","很长很长的消息摘要，必须在单行末尾省略，不能撑宽面板或影响滚动。"));
            for(int i=0;i<6;i++) _messageInbox.Add(Sample(MessageProvider.WeChat,$"测试会话{i}"));
            _messageInbox.Add(Sample(MessageProvider.Qq,"QQ好友")); _inboxWindow.Interaction.Close(); RefreshMessageInbox();
            Point icon=_inboxWindow.IconCenter(MessageProvider.WeChat); var now=DateTimeOffset.Now;
            _inboxWindow.ProcessPointer(icon,false,now); _inboxWindow.ProcessPointer(icon,false,now.AddMilliseconds(299));
            Check(_inboxWindow.Interaction.OpenProvider is null,"Hover waits 300 ms");
            _inboxWindow.ProcessPointer(icon,false,now.AddMilliseconds(301));
            Check(_inboxWindow.Interaction.OpenProvider==MessageProvider.WeChat,"Hover opens WeChat");
            var panel=_inboxWindow.PanelRectangle; icon=_inboxWindow.IconCenter(MessageProvider.WeChat);
            var gap=new Point((icon.X+(panel.Right<icon.X?panel.Right:panel.Left))/2,Numeric.Clamp(icon.Y,panel.Top,panel.Bottom));
            _inboxWindow.ProcessPointer(gap,false,now.AddMilliseconds(350));
            _inboxWindow.ProcessPointer(new(panel.Left+30,panel.Top+25),false,now.AddMilliseconds(950));
            Check(_inboxWindow.Interaction.OpenProvider==MessageProvider.WeChat,"Icon to bridge to list stays continuously open");
            Check(_inboxWindow.QaNativeHitTest(icon) && _inboxWindow.QaNativeHitTest(gap) &&
                !_inboxWindow.QaNativeHitTest(new(-5,-5)),"Native HWND hit testing accepts icons and bridge and passes empty space through");
            Check(_inboxWindow.VisibleOrder.Count==_messageInbox.Rows(MessageProvider.WeChat).Count,
                "Hover preview retains every available conversation summary");
            _messageInbox.Add(Sample(MessageProvider.WeChat,"测试会话0","更新后的摘要")); RefreshMessageInbox(); await Task.Delay(70);
            Check(_inboxWindow.VisibleOrder.Count==_messageInbox.Rows(MessageProvider.WeChat).Count &&
                _inboxWindow.VisibleOrder[0]==_messageInbox.Rows(MessageProvider.WeChat)[0].Key,
                "New readable content moves its conversation to the top without dropping other sessions");
            Check(MessageInbox.Badge(_messageInbox.Total(MessageProvider.WeChat))=="99+","App badge supports 99+");
            CaptureInboxQa(directory,"many-unread-scrollable-preview.png");
            _inboxWindow.ProcessPointer(new(-100,-100),false,now.AddSeconds(2));
            _inboxWindow.ProcessPointer(new(-100,-100),false,now.AddMilliseconds(2499)); Check(_inboxWindow.Interaction.OpenProvider is not null,"Leave grace period is 500 ms");
            _inboxWindow.ProcessPointer(new(-100,-100),false,now.AddMilliseconds(2501)); Check(_inboxWindow.Interaction.OpenProvider is null,"Leaving closes without clearing reminders");
            _inboxWindow.Interaction.Click(MessageProvider.WeChat);RefreshMessageInbox();
            _inboxWindow.ProcessPointer(new(-100,-100),true,now.AddSeconds(4));Check(_inboxWindow.Interaction.OpenProvider is null,"Outside click closes pinned list");
            Check(_messageInbox.Total(MessageProvider.WeChat)>100,"Collapsing preserves pending counts");
            _inboxWindow.QaClickIcon(MessageProvider.WeChat);Check(_inboxWindow.Interaction.Pinned,"Actual icon mouse route pins the list");
            var ignored=_messageInbox.Rows(MessageProvider.WeChat)[0];long totalBefore=_messageInbox.Total(MessageProvider.WeChat);
            _inboxWindow.QaIgnoreConversation(ignored.Key);
            Check(_messageInbox.Total(MessageProvider.WeChat)==totalBefore-ignored.Count && !_messageInbox.Add(ignored.Latest),"Attached conversation menu ignores locally and rejects replay");
            _inboxWindow.QaClickIcon(MessageProvider.WeChat);Check(_inboxWindow.Interaction.OpenProvider is null,"Second icon click closes pinned list");
            Check(!_inboxWindow.QaIconHasContextMenu(MessageProvider.Qq) &&
                !_inboxWindow.QaIconHasContextMenu(MessageProvider.WeChat),
                "Platform icons do not open a clear-reminders context menu");
            CaptureInboxQa(directory,"right-click-before.png");
            _inboxWindow.QaRightClickIcon(MessageProvider.Qq);
            Check(_messageInbox.Total(MessageProvider.Qq)==0 && _messageInbox.Total(MessageProvider.WeChat)>0,
                "Right-clicking QQ directly clears only QQ's local pending reminders");
            CaptureInboxQa(directory,"right-click-after-qq.png");
            _inboxWindow.QaRightClickIcon(MessageProvider.WeChat);
            Check(_messageInbox.Total(MessageProvider.WeChat)==0 && !_inboxWindow.IsVisible,
                "Right-clicking WeChat directly clears its local reminders and hides an empty rail");
            CaptureInboxQa(directory,"right-click-after-both.png");
            _messageInbox.Add(new(MessageProvider.WeChat,DateTimeOffset.Now.AddSeconds(1),"后续布局样例",
                MessagePreview:"新提醒仍可出现",NotificationKey:$"after-clear:{++serial}"));
            RefreshMessageInbox();
            var beforeSide=_inboxSide; _isWindowDragging=true;
            Left=GetCurrentWorkArea().Left+50;PositionMessageInbox();Check(_inboxSide==beforeSide,"Dragging freezes rail side");
            _isWindowDragging=false;PositionMessageInbox();Check(_inboxSide==MessageRailSide.Right,"Drag end selects new side");
            foreach(int scale in new[]{50,150,200})
            {
                SetDisplayScalePercent(scale,false);PlayResolvedContinuousAnimation();await Task.Delay(150);
                foreach(bool bottom in new[]{false,true})
                {
                    Top=bottom?GetCurrentWorkArea().Bottom-Height:GetCurrentWorkArea().Top;
                    var petBounds=GetPetImageAlphaBoundsInWindow();
                    Point actualEdge=PointToScreen(new(petBounds.Left,bottom?petBounds.Bottom:petBounds.Top));
                    double dpi=PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11??1;
                    var physicalWork=_windowWorkAreaProvider.GetForWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle);
                    Top+=((bottom?physicalWork.Bottom:physicalWork.Top)-actualEdge.Y)/dpi;
                    _inboxWindow.Interaction.Close();_inboxWindow.Interaction.Click(MessageProvider.WeChat);RefreshMessageInbox();
                    var pr=_inboxWindow.PanelRectangle;
                    Point panelTop=_inboxWindow.PointToScreen(pr.TopLeft), panelBottom=_inboxWindow.PointToScreen(pr.BottomRight);
                    Check(panelTop.X>=physicalWork.Left && panelBottom.X<=physicalWork.Right+1 &&
                        panelTop.Y>=physicalWork.Top && panelBottom.Y<=physicalWork.Bottom+1,$"Scale {scale}, edge {bottom}: full panel stays inside physical work area");
                    CaptureInboxQa(directory,$"edge-{scale}-{bottom}.png");
                }
            }
            Check(!IsInboxDisplaySafe(new(false,null,false)) && !IsInboxDisplaySafe(new(true,"other",true)),"Unknown foreground and fullscreen suppress interactive rail");
            _inboxSafe=false;PositionMessageInbox();Check(!_inboxWindow.IsVisible,"Safety suspension hides rail and panel");
            _inboxSafe=true;SetDisplayScalePercent(100,false);Top=GetCurrentWorkArea().Top+(GetCurrentWorkArea().Height-Height)/2;
            _stateMachine.SetMusicAnimation("resonance-enjoy-music");_stateMachine.SetContinuousState(PetContinuousState.MusicPlaying);
            PlayResolvedContinuousAnimation();await Task.Delay(150);string? music=_animationPlayer?.CurrentAnimationId;var mediaSettings=_settings.Media;
            await BeginMessageNotificationAsync(Sample(MessageProvider.Qq,"音乐中收到的测试提醒"));
            Check(_animationPlayer?.CurrentAnimationId==music && _stateMachine.VisualState.ContinuousState==PetContinuousState.MusicPlaying && _settings.Media==mediaSettings,
                "Message arrival preserves music animation and media settings without media input");
            _inboxWindow.Interaction.Close();RefreshMessageInbox();CaptureInboxQa(directory,"music-reminder.png");
            foreach(string animation in new[]{PetVisualState.EnjoyMusicAnimation,
                PetVisualState.MusicSwayAnimation,PetVisualState.OneClickSingingAnimation})
            {
                _stateMachine.SetMusicAnimation(animation);
                _stateMachine.SetContinuousState(PetContinuousState.MusicPlaying);
                var asset=_animationCatalog!.GetRequired(animation);
                ShowAnimationFrame(animation,0);
                RefreshMessageInbox();
                Point fixedPosition=_inboxWindow.PointToScreen(_inboxWindow.IconCenter(MessageProvider.Qq));
                CaptureInboxQa(directory,$"music-anchor-{animation}-first.png");
                foreach(int frame in new[]{asset.FrameDurationsMilliseconds.Count/2,
                    asset.FrameDurationsMilliseconds.Count-1})
                {
                    ShowAnimationFrame(animation,frame);
                    PositionMessageInbox();
                    Point position=_inboxWindow.PointToScreen(_inboxWindow.IconCenter(MessageProvider.Qq));
                    Check(Math.Abs(position.X-fixedPosition.X)<=1 && Math.Abs(position.Y-fixedPosition.Y)<=1,
                        $"{animation}: QQ/WeChat rail stays fixed across frame {frame}");
                }
                CaptureInboxQa(directory,$"music-anchor-{animation}-last.png");
                double startLeft=Left;
                Left+=24;PositionMessageInbox();
                Point moved=_inboxWindow.PointToScreen(_inboxWindow.IconCenter(MessageProvider.Qq));
                double pixelScale=PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11??1;
                Check(Math.Abs(moved.X-fixedPosition.X-24*pixelScale)<=1 &&
                    Math.Abs(moved.Y-fixedPosition.Y)<=1,
                    $"{animation}: fixed rail still follows a 24-DIP pet move");
                Left=startLeft;PositionMessageInbox();
            }

            _stateMachine.SetContinuousState(PetContinuousState.Idle);
            PlayResolvedContinuousAnimation();
            PositionMessageInbox();
            Point idleIcon=_inboxWindow.PointToScreen(_inboxWindow.IconCenter(MessageProvider.Qq));
            foreach(string reaction in new[]{"resonance-kiss",BodyInteractionResolver.SoftHeartAnimation})
            {
                var heartAsset=_animationCatalog!.GetRequired(reaction);
                foreach(int frame in new[]{0,heartAsset.FrameDurationsMilliseconds.Count/2,
                    heartAsset.FrameDurationsMilliseconds.Count-1})
                {
                    ShowAnimationFrame(reaction,frame);
                    PositionMessageInbox();
                    Point heartIcon=_inboxWindow.PointToScreen(_inboxWindow.IconCenter(MessageProvider.Qq));
                    Check(Math.Abs(heartIcon.X-idleIcon.X)<=1 && Math.Abs(heartIcon.Y-idleIcon.Y)<=1,
                        $"{reaction}: QQ/WeChat rail stays fixed across frame {frame}");
                    if(frame==0)CaptureInboxQa(directory,$"{reaction}-anchor-first.png");
                    if(frame==heartAsset.FrameDurationsMilliseconds.Count-1)
                        CaptureInboxQa(directory,$"{reaction}-anchor-last.png");
                }
            }
            PlayResolvedContinuousAnimation();PositionMessageInbox();
            Point returnedIcon=_inboxWindow.PointToScreen(_inboxWindow.IconCenter(MessageProvider.Qq));
            Check(Math.Abs(returnedIcon.X-idleIcon.X)<=1 && Math.Abs(returnedIcon.Y-idleIcon.Y)<=1,
                "Heart reaction: returning to idle preserves the rail position");

            // Every registered artwork can change the animation stage. The rail
            // must keep the same offset from the stable stage, including spin.
            DesktopRectangle stageAtStart=GetStableStageBoundsInWindow();
            Point stageScreen=PointToScreen(new(stageAtStart.Left+stageAtStart.Width/2,stageAtStart.Bottom));
            Point iconScreen=_inboxWindow.PointToScreen(_inboxWindow.IconCenter(MessageProvider.Qq));
            Point railOffset=new(iconScreen.X-stageScreen.X,iconScreen.Y-stageScreen.Y);
            int auditedAnimations=0;
            foreach(var artwork in _animationCatalog!.Assets)
            {
                int frames=artwork.FrameDurationsMilliseconds.Count;
                if(frames==0)continue;
                foreach(int frame in new[]{0,frames/2,frames-1}.Distinct())
                {
                    ShowAnimationFrame(artwork.Id,frame);
                    PositionMessageInbox();
                    DesktopRectangle currentStage=GetStableStageBoundsInWindow();
                    Point stagePoint=PointToScreen(new(currentStage.Left+currentStage.Width/2,currentStage.Bottom));
                    Point currentIcon=_inboxWindow.PointToScreen(_inboxWindow.IconCenter(MessageProvider.Qq));
                    Check(Math.Abs(currentIcon.X-stagePoint.X-railOffset.X)<=1 &&
                        Math.Abs(currentIcon.Y-stagePoint.Y-railOffset.Y)<=1,
                        $"{artwork.Id} frame {frame}: rail remains stage anchored");
                    if(artwork.Id==ClassicSpinDanceAnimation)
                        CaptureInboxQa(directory,$"spin-anchor-{frame}.png");
                }
                auditedAnimations++;
            }
            checks.Add($"PASS Audited all {auditedAnimations} registered animations");
            PlayResolvedContinuousAnimation();PositionMessageInbox();

            // Drag updates must happen synchronously on the pointer move, without
            // relying on the message window's 40 ms hover timer.
            var dragWork=GetCurrentWorkArea();
            Left=dragWork.Left+(dragWork.Width-Width)/2;
            Top=dragWork.Top+(dragWork.Height-Height)/2;
            PositionMessageInbox();
            Point beforeDrag=_inboxWindow.PointToScreen(_inboxWindow.IconCenter(MessageProvider.Qq));
            double beforeLeft=Left,beforeTop=Top;
            _isWindowDragging=true;
            _dragStartLeft=Left;_dragStartTop=Top;
            _dragIntentPetBoundsInWindow=GetPetImageAlphaBoundsInWindow();
            _dragPressScreenPoint=new(Left+Width/2,Top+Height/2);
            MoveWindowWithPointer(new(_dragPressScreenPoint.X+36,_dragPressScreenPoint.Y+18),DateTimeOffset.Now);
            Point afterDrag=_inboxWindow.PointToScreen(_inboxWindow.IconCenter(MessageProvider.Qq));
            double dragScale=PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11??1;
            Check(Math.Abs(afterDrag.X-beforeDrag.X-36*dragScale)<=1 &&
                Math.Abs(afterDrag.Y-beforeDrag.Y-18*dragScale)<=1,
                "Pointer move immediately repositions QQ/WeChat rail with pet");
            _isWindowDragging=false;_dragIntentPetBoundsInWindow=null;
            Left=beforeLeft;Top=beforeTop;PositionMessageInbox();

            // Verify the special sleeping pose uses its visible body bounds for
            // the ear rail, and that ZZZ remains visible after mist cleanup.
            _messageInbox.Clear(MessageProvider.WeChat,DateTimeOffset.MinValue);
            _messageInbox.Clear(MessageProvider.Qq,DateTimeOffset.MinValue);
            ApplyAppearancePreferences(_settings.Appearance with {FullBodyStyle="full-body-crystal-dress",DisplayScalePercent=100},false);
            _stateMachine.SetContinuousState(PetContinuousState.Sleeping);
            BeginCrystalLongIdle(CrystalLongIdleVariant.Sleep);
            await Task.Delay(12000);
            Check(IsHeldCrystalSleep,"Sleeping rail uses the audited crystal sleep hold frame");
            await BeginMessageNotificationAsync(Sample(MessageProvider.WeChat,"睡眠微信"));
            await BeginMessageNotificationAsync(Sample(MessageProvider.Qq,"睡眠QQ"));
            _inboxSide=null; _inboxWindow.Interaction.Close();
            foreach(string side in new[]{"left","right"})
            {
                var work=GetCurrentWorkArea();
                Left=work.Left+(side=="left" ? 70 : work.Width-Width-70);
                Top=work.Top+(work.Height-Height)/2;
                _inboxSide=null; RefreshMessageInbox(); await Task.Delay(180);
                Check(_inboxWindow.IsVisible,$"sleep/{side}: pending reminders remain visible during sleep hold");
                CaptureInboxQa(directory,$"sleep-crystal-{side}.png",includePetClip:false);
            }
            CancelCrystalLongIdle();
            _stateMachine.SetContinuousState(PetContinuousState.Idle);
            PlayResolvedContinuousAnimation();
            _messageInbox.Clear(MessageProvider.WeChat,DateTimeOffset.MinValue);
            _messageInbox.Clear(MessageProvider.Qq,DateTimeOffset.MinValue);
            RefreshMessageInbox();
            File.WriteAllLines(Path.Combine(directory,"result.txt"),checks);
            if(Environment.GetCommandLineArgs().Contains("--qa-inbox-interactive"))
            {
                SetDisplayScalePercent(100,false);_stateMachine.SetContinuousState(PetContinuousState.Idle);PlayResolvedContinuousAnimation();
                Left=GetCurrentWorkArea().Left+180;Top=GetCurrentWorkArea().Top+(GetCurrentWorkArea().Height-Height)/2;
                for(int i=0;i<7;i++) _messageInbox.Add(new(MessageProvider.WeChat,DateTimeOffset.Now.AddSeconds(i),
                    $"预览会话{i+1}",UnreadCount:i+1,MessagePreview:$"这是一条可悬浮查看的消息摘要 {i+1}",
                    NotificationKey:$"interactive:{++serial}",WeChatSessionKey:$"interactive-session-{i}"));
                _messageInbox.Add(new(MessageProvider.Qq,DateTimeOffset.Now,"产品讨论群",UnreadCount:3,
                    MessagePreview:"今晚版本需要再确认一下",NotificationKey:$"interactive:{++serial}"));
                Topmost=true;_inboxSafe=true;_inboxSide=null;_inboxWindow.AutomationMode=true;
                _inboxWindow.Interaction.Click(MessageProvider.WeChat);RefreshMessageInbox();
                _inboxWindow.ExposeAutomationWindow();
            }
            else Close();
        }
        catch(Exception e){checks.Add("FAIL "+e);File.WriteAllLines(Path.Combine(directory,"result.txt"),checks);Application.Current.Shutdown(1);}
    }

    private BitmapSource CaptureInboxQa(string directory,string name,bool includePetClip=true)
    {
        UpdateLayout();_inboxWindow!.UpdateLayout();
        var alpha=GetPetImageAlphaBoundsInWindow(); var a=PointToScreen(new(alpha.Left,alpha.Top));var b=PointToScreen(new(alpha.Right,alpha.Bottom));
        double scale=PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11??1;
        var origin=PointToScreen(new());
        Rect bounds=new(a,b);
        Point railOrigin=default;
        if(_inboxWindow.IsVisible){railOrigin=_inboxWindow.PointToScreen(new());bounds.Union(new Rect(railOrigin,new Size(_inboxWindow.ActualWidth*scale,_inboxWindow.ActualHeight*scale)));}
        if(!includePetClip && CrystalLongIdleDecorationLayer.Visibility==Visibility.Visible)
        {
            Rect decoration=CrystalLongIdleDecorationImage.TransformToAncestor(this).TransformBounds(
                new Rect(0,0,CrystalLongIdleDecorationImage.ActualWidth,CrystalLongIdleDecorationImage.ActualHeight));
            Point decorationOrigin=PointToScreen(new(decoration.Left,decoration.Top));
            bounds.Union(new Rect(decorationOrigin,new Size(decoration.Width*scale,decoration.Height*scale)));
        }
        bounds.Inflate(12,12);
        DrawingVisual drawing=new();
        using(var dc=drawing.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xED,0xF5,0xFC)),null,new Rect(0,0,bounds.Width,bounds.Height));
            if(includePetClip) dc.PushClip(new RectangleGeometry(new Rect(a.X-bounds.Left,a.Y-bounds.Top,b.X-a.X,b.Y-a.Y)));
            dc.DrawImage(RenderWindow(this),new Rect(origin.X-bounds.Left,origin.Y-bounds.Top,ActualWidth*scale,ActualHeight*scale));
            if(includePetClip) dc.Pop();
            if(_inboxWindow.IsVisible)dc.DrawImage(RenderWindow(_inboxWindow),new Rect(railOrigin.X-bounds.Left,railOrigin.Y-bounds.Top,_inboxWindow.ActualWidth*scale,_inboxWindow.ActualHeight*scale));
        }
        RenderTargetBitmap bitmap=new((int)Math.Ceiling(bounds.Width),(int)Math.Ceiling(bounds.Height),96,96,PixelFormats.Pbgra32);bitmap.Render(drawing);
        PngBitmapEncoder png=new();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(directory,name));png.Save(file);return bitmap;
    }
    private static void SaveInboxSheet(string directory,string name,List<BitmapSource> images)
    {
        DrawingVisual visual=new();using(var dc=visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White,null,new Rect(0,0,1040,1240));
            double factor=Math.Min(490.0/images.Max(image=>image.PixelWidth),275.0/images.Max(image=>image.PixelHeight));
            for(int i=0;i<images.Count;i++)
            {
                var image=images[i];
                double x=(i%2)*520+15,y=(i/2)*310+25;
                dc.DrawText(new FormattedText($"{i+1:00}",System.Globalization.CultureInfo.InvariantCulture,System.Windows.FlowDirection.LeftToRight,new Typeface("Segoe UI"),14,Brushes.SlateGray,1),new(x,y-20));
                dc.DrawImage(image,new Rect(x,y,image.PixelWidth*factor,image.PixelHeight*factor));
            }
        }
        RenderTargetBitmap bitmap=new(1040,1240,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
        PngBitmapEncoder png=new();png.Frames.Add(BitmapFrame.Create(bitmap));using var output=File.Create(Path.Combine(directory,name));png.Save(output);
    }
}
