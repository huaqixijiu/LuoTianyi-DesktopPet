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
        check(_reminderCard?.IsVisible==true&&!CanShowMusicIslands&&MediaControls.Visibility==Visibility.Collapsed,"visible reminder suppresses music island without changing preference");
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
        foreach(var layout in new[]{AccessoryLayout.AbovePet,AccessoryLayout.BelowPet,AccessoryLayout.Split})
        {
            PrepareFeedbackBubble("网易云已打开，正在等待音乐开始播放…");ApplyAccessoryLayout(layout,force:true);UpdateLayout();PositionFeedbackNearPet();UpdateLayout();
            var pet=GetPetImageAlphaBoundsInWindow();double top=FeedbackBubble.TranslatePoint(new Point(),this).Y;
            double gap=layout==AccessoryLayout.BelowPet?top-pet.Bottom:pet.Top-top-FeedbackBubble.ActualHeight;
            shot(this,"r3-feedback-"+layout);
            check(Math.Abs(gap-6)<1,$"feedback attaches to visible pet with six DIP gap: {layout}, gap={gap}, pet={pet.Top}/{pet.Bottom}, bubble={top}/{FeedbackBubble.ActualHeight}");
        }
        HideFeedbackBubble(false);_settings=settings;ApplyCurrentDisplayScale();
    }
}
