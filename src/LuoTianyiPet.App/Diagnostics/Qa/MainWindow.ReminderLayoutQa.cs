using System.Windows;
using System.Windows.Media;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    private async Task VerifyReminderLayoutQaAsync(ReminderService service,Action<bool,string> check,Action<Window,string> shot)
    {
        var settings=_settings;
        await service.ChangeAsync(b=>{b.Preferences.Sound=false;b.Preferences.Animation=false;});
        _settings=_settings with{Media=_settings.Media with{ShowMusicIslands=true}};
        _reminderCard?.Hide();
        _musicIslandMotion.Show();await Task.Delay(220);
        RefreshReminderCardCore(true);await Task.Delay(100);
        check(_reminderCard?.IsVisible==true&&CanShowMusicIslands&&_settings.Media.ShowMusicIslands,"visible reminder preserves music island eligibility and preference");
        double previous=0;
        foreach(int percent in new[]{60,100,150})
        {
            SetDisplayScalePercent(percent,false);RefreshReminderCardCore(true);UpdateLayout();_reminderCard!.UpdateLayout();PositionReminderCard();
            check(_reminderCard.Width>previous,"reminder grows with pet scale "+percent);previous=_reminderCard.Width;
            check(_reminderCard.Width<440||percent>100,"default reminder no longer fixed at 440");
            shot(_reminderCard,"r3-reminder-"+percent);
        }
        _reminderCard!.Hide();
        check(CanShowMusicIslands&&_settings.Media.ShowMusicIslands,"hiding reminder restores music eligibility and preserves preference");
        SetDisplayScalePercent(100,false);
        PrepareFeedbackBubble("已暂停");UpdateLayout();double shortWidth=FeedbackBubble.ActualWidth;
        shot(this,"r5-feedback-short");
        PrepareFeedbackBubble("正在打开网易云音乐，请稍等…");UpdateLayout();double mediumWidth=FeedbackBubble.ActualWidth;
        shot(this,"r5-feedback-medium");
        check(shortWidth<mediumWidth&&shortWidth<150&&mediumWidth<=250,"feedback width follows text instead of fixed 250 DIP");
        PrepareFeedbackBubble(new string('长',100));UpdateLayout();
        check(FeedbackBubble.ActualWidth<=250&&FeedbackBubble.ActualHeight>40,"long feedback wraps inside maximum width");
        shot(this,"r5-feedback-long");
        foreach(var layout in new[]{AccessoryLayout.AbovePet,AccessoryLayout.BelowPet,AccessoryLayout.Split})
        {
            PrepareFeedbackBubble("网易云已打开，正在等待音乐开始播放…");ApplyAccessoryLayout(layout,force:true);UpdateLayout();PositionFeedbackNearPet();UpdateLayout();
            var pet=GetPetImageAlphaBoundsInWindow();double top=FeedbackBubble.TranslatePoint(new Point(),this).Y;
            var work=GetCurrentWorkArea();
            bool preferredFits=Top+pet.Top-FeedbackBubble.ActualHeight-FeedbackBubblePetGap>=work.Top&&
                Top+pet.Top-FeedbackBubblePetGap<=work.Bottom;
            bool hasIsland=TryGetMusicIslandBoundsInWindow(out Rect islandForLayout);
            bool preferredOverlapsIsland=hasIsland&&
                pet.Top-FeedbackBubble.ActualHeight-FeedbackBubblePetGap<islandForLayout.Bottom&&
                pet.Top-FeedbackBubblePetGap>islandForLayout.Top;
            double gap=preferredFits&&!preferredOverlapsIsland?pet.Top-top-FeedbackBubble.ActualHeight:double.NaN;
            shot(this,"r3-feedback-"+layout);
            if(preferredFits&&!preferredOverlapsIsland)
                check(Math.Abs(gap-FeedbackBubblePetGap)<1,$"feedback stays above visible pet with six DIP gap: {layout}, gap={gap}, pet={pet.Top}/{pet.Bottom}, bubble={top}/{FeedbackBubble.ActualHeight}");
            else if(hasIsland&&islandForLayout.Bottom<=pet.Top)
                check(top+FeedbackBubble.ActualHeight<=islandForLayout.Top-FeedbackBubblePetGap+1,$"feedback stays above the music island when it is above the pet: {layout}, islandTop={islandForLayout.Top}, bubbleBottom={top+FeedbackBubble.ActualHeight}");
            else if(hasIsland)
                check(top>=islandForLayout.Bottom+FeedbackBubblePetGap-1,$"top-edge feedback stays below the visible music island: {layout}, islandBottom={islandForLayout.Bottom}, bubbleTop={top}");
            else
                check(top>=pet.Bottom+FeedbackBubblePetGap-1,$"top-edge feedback falls below the pet when music island is hidden: {layout}, petBottom={pet.Bottom}, bubbleTop={top}");
        }

        // Exercise the actual top-edge fallback explicitly. A normal planner
        // window position can leave enough headroom and would not cover the
        // collision that caused the reported pet/island gap.
        var originalLeft=Left;
        var originalTop=Top;
        try
        {
            ApplyAccessoryLayout(AccessoryLayout.BelowPet,force:true);
            UpdateLayout();
            DesktopRectangle edgeWork=GetCurrentWorkArea();
            DesktopRectangle edgePet=GetPetImageAlphaBoundsInWindow();
            Top=edgeWork.Top-edgePet.Top+1;
            UpdateLayout();
            PositionFeedbackNearPet();
            UpdateLayout();
            var edgePetAfter=GetPetImageAlphaBoundsInWindow();
            double edgeBubbleTop=FeedbackBubble.TranslatePoint(new Point(),this).Y;
            bool edgeHasNoHeadroom=Top+edgePetAfter.Top-FeedbackBubble.ActualHeight-FeedbackBubblePetGap<edgeWork.Top;
            check(edgeHasNoHeadroom,"top-edge layout leaves no room above the pet for the feedback bubble");
            check(TryGetMusicIslandBoundsInWindow(out Rect edgeIsland),"top-edge layout keeps the music island measurable");
            if(TryGetMusicIslandBoundsInWindow(out edgeIsland))
                check(edgeBubbleTop>=edgeIsland.Bottom+FeedbackBubblePetGap-1,$"top-edge feedback is below the music island instead of in the pet/island gap: islandBottom={edgeIsland.Bottom}, bubbleTop={edgeBubbleTop}");
            shot(this,"r5-feedback-top-edge");
        }
        finally
        {
            Left=originalLeft;
            Top=originalTop;
        }
        HideFeedbackBubble(false);_settings=settings;ApplyCurrentDisplayScale();
    }
}
