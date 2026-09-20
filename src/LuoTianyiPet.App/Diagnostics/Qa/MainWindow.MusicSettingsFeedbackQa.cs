using System.IO;
using System.Windows;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    // Uses the production state/rendering paths without controlling any external music player.
    private async Task RunMusicSettingsFeedbackQaAsync()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "MusicSettingsFeedbackQa");
        Directory.CreateDirectory(directory);
        List<string> checks = [];
        void Check(bool passed, string label)
        {
            if (!passed) throw new InvalidOperationException(label);
            checks.Add("PASS " + label);
        }
        Rect Bounds(FrameworkElement element) => element.TransformToAncestor(this)
            .TransformBounds(new Rect(element.RenderSize));
        try
        {
            await Task.Delay(700);
            _idleSceneTimer.Stop();
            _timeSceneTimer.Stop();
            _trackInfoRefreshTimer.Stop();
            _stateMachine.CancelActiveReaction();
            TrackInfoBubble.Effect = null;
            MediaControls.Effect = null;
            ApplyMediaPreferences(_settings.Media with { MusicAnimationSelection = PetVisualState.NoMusicAnimation }, save: false);
            foreach (string id in new[] { PetVisualState.CompactIdleAnimation,
                "user-chibi-crystal-full-body-idle", "user-chibi-classic-full-body-idle" })
            {
                _stateMachine.SetDisplayMode(id == PetVisualState.CompactIdleAnimation ?
                    PetDisplayMode.Compact : PetDisplayMode.FullBodyInteractive);
                _stateMachine.SetFullBodyAnimation(id);
                _stateMachine.SetContinuousState(PetContinuousState.Idle);
                PlayResolvedContinuousAnimation();
                await Task.Delay(300);
                for (int repeat = 0; repeat < 3; repeat++)
                {
                    StartMusicPlayback("qa", "洛天依Official");
                    for (int i = 0; i < 8; i++)
                    {
                        await Task.Delay(20);
                        Check(!_visualSwapTransition.IsActive && PetVisual.Opacity == 1 && MusicTransitionFlash.Opacity == 0 &&
                            _animationPlayer?.CurrentAnimationId == id, $"Music off: no start flash {id}/{repeat}/{i}");
                    }
                    StopMusicPlayback("qa");
                    await Task.Delay(80);
                    Check(!_visualSwapTransition.IsActive && PetVisual.Opacity == 1 && _animationPlayer?.CurrentAnimationId == id,
                        $"Music off: no stop flash {id}/{repeat}");
                }
            }
            ApplyMediaPreferences(_settings.Media with { MusicAnimationSelection = PetVisualState.EnjoyMusicAnimation }, save: false);
            StartMusicPlayback("qa", "测试歌手");
            Check(_visualSwapTransition.IsActive, "Different art still uses a transition");
            await Task.Delay(650);
            Check(_animationPlayer?.CurrentAnimationId == PetVisualState.EnjoyMusicAnimation, "Enabled music animation is preserved");
            StopMusicPlayback("qa");
            await Task.Delay(650);

            SetMusicIslandsVisible(true);
            string[] messages = ["设置暂时没有保存成功，请稍后再试", "正在打开网易云音乐，请稍等…",
                "网易云已打开，但还没有开始播放。请检查登录状态或选择一个可播放的歌单，再试一次。"];
            foreach (int scale in new[] { 50, 100, 200 })
            {
                SetDisplayScalePercent(scale, false);
                foreach (AccessoryLayout layout in new[] { AccessoryLayout.Split, AccessoryLayout.AbovePet, AccessoryLayout.BelowPet })
                {
                    var work = GetCurrentWorkArea();
                    Left = work.Left + (work.Width - Width) / 2;
                    ApplyAccessoryLayout(layout);
                    var stage = GetStableStageBoundsInWindow();
                    Top = layout == AccessoryLayout.BelowPet ? work.Top - stage.Top :
                        layout == AccessoryLayout.AbovePet ? work.Bottom - stage.Bottom :
                        work.Top + (work.Height - Height) / 2;
                    foreach (string message in messages)
                    {
                        HideFeedbackBubble(true);
                        ApplyAccessoryLayout(layout);
                        var before = GetStableStageDesktopBounds();
                        DesktopRectangle beforePetAlpha = GetPetImageAlphaBoundsInWindow();
                        ShowTrackInfo(new MediaTrackSnapshot(true, true, "歌名与提示各有位置", "洛天依"), true);
                        _musicIslandMotion.Show(false);
                        _trackInfoHideTimer.Stop();
                        _mediaControlsHideTimer.Stop();
                        ShowPersistentFeedbackBubble(message);
                        UpdateLayout();
                        Check(_accessoryLayout == layout, $"Actual edge layout {scale}/{layout}");
                        Rect song = Bounds(TrackInfoBubble), feedback = Bounds(FeedbackBubble), controls = Bounds(MediaControls);
                        DesktopRectangle petAlpha = GetPetImageAlphaBoundsInWindow();
                        Rect pet = new(petAlpha.Left, petAlpha.Top, petAlpha.Width, petAlpha.Height);
                        DesktopRectangle workArea = GetCurrentWorkArea();
                        bool preferredFits = Top + pet.Top - feedback.Height - FeedbackBubblePetGap >= workArea.Top &&
                            Top + pet.Top - FeedbackBubblePetGap <= workArea.Bottom;
                        bool preferredOverlapsIsland =
                            pet.Top - feedback.Height - FeedbackBubblePetGap < controls.Bottom &&
                            pet.Top - FeedbackBubblePetGap > controls.Top;
                        if (preferredFits && !preferredOverlapsIsland)
                        {
                            Check(Math.Abs(pet.Top - feedback.Bottom - FeedbackBubblePetGap) < 1,
                                $"Feedback stays above the visible pet {scale}/{layout}/{message.Length}");
                        }
                        else if (controls.Bottom <= pet.Top)
                        {
                            Check(feedback.Bottom <= controls.Top - FeedbackBubblePetGap + 1,
                                $"Feedback stays above the music island when it is above the pet {scale}/{layout}/{message.Length}");
                        }
                        else
                        {
                            Check(feedback.Top >= controls.Bottom + FeedbackBubblePetGap - 1,
                                $"Top-edge feedback stays below the music island {scale}/{layout}/{message.Length} " +
                                $"(feedbackTop={feedback.Top:0.0}, islandBottom={controls.Bottom:0.0}, petTop={pet.Top:0.0}, " +
                                $"feedbackHeight={feedback.Height:0.0}, windowHeight={ActualHeight:0.0}, slot={_feedbackSlotHeight:0.0})");
                        }
                        Check(!feedback.IntersectsWith(pet) && !feedback.IntersectsWith(controls),
                            $"Feedback does not overlap pet or controls {scale}/{layout}/{message.Length}");
                        Check(feedback.Left >= 0 && feedback.Right <= Width + 0.1 && feedback.Bottom <= Height + 0.1,
                            $"Feedback fits window {scale}/{layout}/{message.Length}");
                        Check(Top + feedback.Top >= work.Top && Top + feedback.Bottom <= work.Bottom &&
                            Top + song.Top >= work.Top && Top + controls.Bottom <= work.Bottom,
                            $"Islands remain on screen {scale}/{layout}/{message.Length}");
                        Check(Math.Abs(beforePetAlpha.Width - petAlpha.Width) < 0.1 &&
                            Math.Abs(beforePetAlpha.Height - petAlpha.Height) < 0.1,
                            $"Feedback keeps the visible pet scale unchanged {scale}/{layout}/{message.Length}");
                        Check(MediaControls.Opacity == 1 && TrackInfoBubble.IsVisible,
                            "Feedback does not hide the song island");
                        if (message == messages[0]) CaptureQuickActionsQa(this, Path.Combine(directory, $"{scale}-{layout}.png"));
                        HideFeedbackBubble(true);
                        Check(Math.Abs(GetStableStageDesktopBounds().Bottom - before.Bottom) < 0.1 && _feedbackSlotHeight == 0,
                            "Feedback dismissal releases only its own row");
                    }
                }
            }
            foreach (int scale in new[] { 50, 95, 200 })
            {
                SetDisplayScalePercent(scale,false);
                ApplyAccessoryLayout(AccessoryLayout.Split);
                var centeredWork=GetCurrentWorkArea();
                Left=centeredWork.Left+(centeredWork.Width-Width)/2;
                Top=centeredWork.Top+(centeredWork.Height-Height)/2;
                ShowTrackInfo(new MediaTrackSnapshot(true,true,"间距检查","洛天依"),true);
                _musicIslandMotion.Show(false);
                UpdateLayout();PositionMusicIslandNearPet();UpdateLayout();
                Rect centeredIsland=Bounds(MediaControls);
                DesktopRectangle centeredPet=GetPetImageAlphaBoundsInWindow();
                Check(Math.Abs(centeredIsland.Top-centeredPet.Bottom-MusicIslandPetGap)<1.5,
                    $"Music island follows visible feet at pet scale {scale} " +
                    $"(gap={centeredIsland.Top-centeredPet.Bottom:0.0})");
            }
            SetMusicIslandsVisible(false);
            ShowPersistentFeedbackBubble(messages[1]);
            Check(MediaControls.Visibility == Visibility.Collapsed, "Important feedback never re-enables disabled islands");
            SetDisplayScalePercent(50, false);
            UpdateLayout();
            Check(FeedbackBubble.ActualWidth <= Width - 10 && Bounds(FeedbackBubble).Bottom <= Height,
                "Visible feedback reflows when resizing to minimum size");
            HideFeedbackBubble(true);
            // This QA instance has its own on-disk profile, never the user's settings.
            await Task.WhenAll(Enumerable.Range(0, 32).Select(_ =>
                SaveSettingsAsync("qa.settings_saved", "QA isolated profile saved.")));
            AppSettings reloaded = await _settingsStore.LoadAsync();
            Check(reloaded.Media == _settings.Media && reloaded.Appearance == _settings.Appearance &&
                reloaded.Window == _settings.Window, "Actual app saves round-trip all preference groups");
            Check(FeedbackBubble.Visibility == Visibility.Collapsed, "Successful overlapping saves never show a failure bubble");
            File.WriteAllLines(Path.Combine(directory, "result.txt"), checks);
            Close();
        }
        catch (Exception exception)
        {
            checks.Add("FAIL " + exception);
            File.WriteAllLines(Path.Combine(directory, "result.txt"), checks);
            Application.Current.Shutdown(1);
        }
    }
}
