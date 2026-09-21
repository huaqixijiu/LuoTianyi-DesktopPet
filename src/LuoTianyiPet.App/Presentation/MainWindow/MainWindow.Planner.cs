using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LuoTianyiPet.Core;
using LuoTianyiPet.Platform.Windows;
using Button = System.Windows.Controls.Button;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    private ReminderService? _reminders;
    private PlannerWindow? _plannerWindow;
    private PetReminderCard? _reminderCard;
    private readonly List<(TextBlock Text,DateTime At,bool Snoozed)> _capsuleRemaining=[];
    private readonly DispatcherTimer _reminderDisplayTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private readonly HashSet<string> _shownReminders = [];
    private bool _quickReminderExpanded;
    private bool _restoreExpandedOnce;
    private ReminderFeedbackState? _reminderFeedback;
    private DateTime? _headerReminderTarget;
    private bool _headerReminderSnoozed;
    private sealed class ReminderFeedbackState
    {
        internal string Message { get; }
        internal ReminderUndoSnapshot Before { get; }
        internal ReminderPhase After { get; }
        internal DateTime Until { get; }
        internal ReminderFeedbackState(string message,ReminderUndoSnapshot before,ReminderPhase after,DateTime until)
        { Message=message;Before=before;After=after;Until=until; }
    }
    private string _reminderCardKey = "";
    private string _reminderCardOccurrenceKey = "";
    private string? _reminderStorageNotice;
    private bool _plannerReady;
    private async void InitializePlanner()
    {
        if (!_persistSettings)
        {
            if (Environment.GetCommandLineArgs().Contains("--qa-planner-editor-density"))
            { await RunPlannerEditorDensityQaAsync();return; }
            if (Environment.GetCommandLineArgs().Any(a=>a is "--qa-reminder-card" or "--qa-reminder-card-preview"))
            { await RunReminderCardQaAsync(Environment.GetCommandLineArgs().Contains("--qa-reminder-card-preview"));return; }
            if (Environment.GetCommandLineArgs().Contains("--qa-planner")) await RunPlannerQaAsync();
            return;
        }
        _reminders = new ReminderService(((App)System.Windows.Application.Current).ReminderPaths);
        _reminders.Failed += message => { _reminderStorageNotice = message; _logger.Info("reminder.storage", "Reminder storage needs attention."); };
        try
        {
            await _reminders.LoadAsync();
            if (_isClosing) { _reminders.Dispose(); return; }
            _plannerReady = true;
            _reminders.Changed += RefreshReminderCard;
            _reminderDisplayTimer.Tick += OnReminderDisplayTick;
            _reminderDisplayTimer.Start();
            LocationChanged += (_,_) => QueuePlannerPosition();
            SizeChanged += (_,_) => QueuePlannerPosition();
            DpiChanged += (_,_) => QueuePlannerPosition();
            PreviewMouseUp += (_,_) => QueuePlannerPosition();
        }
        catch
        {
            _reminders.Dispose(); _reminders = null;
            _logger.Info("reminder.load_failed", "Reminder data retained; planner unavailable until recovery.");
        }
    }
    private bool _plannerPositionQueued;
    private void QueuePlannerPosition()
    {
        if(_plannerPositionQueued || _reminderCard==null || _isClosing)return;
        _plannerPositionQueued=true;
        Dispatcher.BeginInvoke(new Action(()=>{_plannerPositionQueued=false;RefreshReminderCard();}),DispatcherPriority.Background);
    }
    private void OpenPlanner(bool alarm)
    {
        _petQuickPanel?.Hide();
        if (_reminders == null || !_plannerReady)
        { System.Windows.MessageBox.Show("日历数据尚未就绪或无法读取，原文件已保留。请检查本地 reminders.json 与备份。", "天依日历"); return; }
        if (_plannerWindow == null)
        {
            _plannerWindow = new PlannerWindow(_reminders, alarm);
            _plannerWindow.SetPageSize(_settings.Appearance.PlannerSize);
            _plannerWindow.Closed += (_, _) => _plannerWindow = null;
        }
        _plannerWindow.Navigate(alarm);
        if (_reminderStorageNotice != null)
        { System.Windows.MessageBox.Show(_plannerWindow, _reminderStorageNotice, "提醒数据"); _reminderStorageNotice = null; }
    }
    private void OnReminderDisplayTick(object? sender, EventArgs e) => RefreshReminderCard();
    private bool PlannerPresentationSafe(ForegroundApplicationSnapshot foreground)
    {
        return !_isClosing && !_systemSessionUnavailable && !_hiddenByUser &&
            _edgeDockSide == EdgeDockSide.None && foreground is { Succeeded: true, IsFullscreen: false } &&
            _stateMachine.VisualState.ContinuousState != PetContinuousState.HiddenForSafety &&
            foreground.ProcessName is not ("YuanShen" or "GenshinImpact" or "YuanShen.exe" or "GenshinImpact.exe");
    }
    private void RefreshReminderCard()
        => RefreshReminderCardCore(PlannerPresentationSafe(_foregroundApplicationProbe?.Query() ?? new(false, null, false)));
    private void RefreshReminderCardCore(bool safe)
    {
        if(_reminders==null||_isClosing)return;
        var book=_reminders.Book;DateTime now=DateTime.Now;
        bool CanPresent(ReminderOccurrence occurrence)
        {
            var item=book.Items.FirstOrDefault(i=>i.Id==occurrence.RuleId);
            return item!=null&&ReminderEngine.Active(item)&&
                (occurrence.Phase is ReminderPhase.Due or ReminderPhase.DueSnoozed || item.EarlyEnabled==true);
        }
        var pending=book.Occurrences.Where(o=>(o.Phase is ReminderPhase.Early or ReminderPhase.Due)&&CanPresent(o))
            .OrderBy(o=>o.Phase==ReminderPhase.Due?0:1).ThenBy(o=>o.At).ToList();
        var capsules=book.Occurrences.Where(o=>(o.Phase is ReminderPhase.AcknowledgedEarly or ReminderPhase.EarlySnoozed or ReminderPhase.DueSnoozed)&&CanPresent(o)&&
            (o.SnoozeAt??o.At)>now).OrderBy(o=>o.SnoozeAt??o.At).ToList();
        if(!safe)
        { _reminderCard?.Hide();StopPlannerPresentation();return; }
        if(_reminderFeedback is { } feedback)
        {
            if(now>=feedback.Until||pending.Any(o=>o.Phase==ReminderPhase.Due))
                _reminderFeedback=null;
            else
            {
                StopPlannerPresentation();_reminderDisplayTimer.Interval=TimeSpan.FromSeconds(1);
                ShowReminderFeedback(feedback);return;
            }
        }
        if(pending.Count+capsules.Count==0)
        { _reminderCard?.Hide();StopPlannerPresentation();_quickReminderExpanded=false;_reminderCardOccurrenceKey="";return; }
        bool quick=pending.Count==0;
        bool hasDue=pending.Any(o=>o.Phase==ReminderPhase.Due);
        // An early notice is visual only. Starting the alarm reaction here also
        // interrupts music/dance before the actual due time.
        if(!hasDue)StopPlannerPresentation();
        _reminderDisplayTimer.Interval=TimeSpan.FromSeconds(1);
        string key=string.Join("|",pending.Concat(capsules).Select(o=>
        {
            var item=book.Items.FirstOrDefault(i=>i.Id==o.RuleId);
            return $"{o.RuleId}:{o.At.Ticks}:{o.Phase}:{o.Revision}:{o.SnoozeAt?.Ticks}:{item?.Title}:{item?.Notes}";
        }));
        string occurrenceKey=string.Join("|",pending.Concat(capsules).Select(o=>$"{o.RuleId}:{o.At.Ticks}:{o.Phase}"));
        if(_reminderCard==null)
        {
            _reminderCard=new PetReminderCard(this);
            _reminderCard.ToggleRequested+=()=>
            {
                _quickReminderExpanded=!_quickReminderExpanded;
                _reminderCard.SetExpanded(_quickReminderExpanded);
                UpdateQuickReminderTimes();PositionReminderCard();
            };
            _reminderCard.GeometryChanged+=PositionReminderCard;
        }
        var work=GetQuickActionsWorkArea();
        bool resized=_reminderCard.ScaleForPet(_settings.Appearance.DisplayScalePercent,work.Width);
        bool fresh=false;
        var validKeys=new HashSet<string>(pending.Where(o=>o.Phase==ReminderPhase.Due).Select(o=>$"{o.RuleId}:{o.At.Ticks}:{o.Phase}:{o.Revision}"));_shownReminders.IntersectWith(validKeys);
        foreach(var k in validKeys)fresh|=_shownReminders.Add(k);
        bool changed=key!=_reminderCardKey;
        if(changed||resized)
        {
            if(changed)
            {
                _reminderCardKey=key;
                if(_restoreExpandedOnce)
                {
                    _restoreExpandedOnce=false;
                    _reminderCardOccurrenceKey=occurrenceKey;
                }
                else if(occurrenceKey!=_reminderCardOccurrenceKey)
                {
                    _reminderCardOccurrenceKey=occurrenceKey;
                    _quickReminderExpanded=pending.Count>1||pending.Any(o=>o.Phase==ReminderPhase.Due);
                }
            }
            RenderReminderCard(book,quick?capsules:pending);
        }
        UpdateQuickReminderTimes();
        PositionReminderCard();_reminderCard.Show();_reminderCard.UpdateLayout();PositionReminderCard();
        if(hasDue)
        {
            _plannerAlarmTopmost ??= AcquireTransientTopmost();
            if(fresh)ReminderAudio.Play(book.Preferences);
            if(!book.Preferences.Sound)ReminderAudio.StopAlarm();
            if(book.Preferences.Animation)PlayPlannerAnimation();else StopPlannerAnimation();
        }
    }
    private void UpdateQuickReminderTimes()
    {
        foreach(var entry in _capsuleRemaining)
        {
            double minutes=Math.Max(0,Math.Ceiling((entry.At-DateTime.Now).TotalMinutes));
            string remaining=minutes<=0?"时间到了":$"还有{minutes:0}分钟";
            entry.Text.Text=entry.Snoozed?$"{remaining}后再提醒":remaining;
        }
        if(_headerReminderTarget is DateTime at && _reminderCard!=null)
            _reminderCard.SetStatus(_headerReminderSnoozed?SnoozeRemaining(at):RemainingUntil(at));
    }
    private bool _positioningReminder;
    private void PositionReminderCard()
    {
        if(_reminderCard==null||_isClosing||_positioningReminder)return;
        _positioningReminder=true;
        try
        {
            var work=GetQuickActionsWorkArea();var alpha=GetPetImageAlphaBoundsInWindow();
            _reminderCard.ScaleForPet(_settings.Appearance.DisplayScalePercent,work.Width);
            var pet=new DesktopRectangle(Left+alpha.Left,Top+alpha.Top,alpha.Width,alpha.Height);
            var anchor=pet;
            if(TryGetMusicIslandBoundsInWindow(out Rect music))
            {
                Rect island=new(Left+music.Left,Top+music.Top,music.Width,music.Height);
                // Keep the pet, music island and reminder on one vertical axis.
                // The island extends the pet's occupied height instead of pushing
                // the reminder to an unrelated side of the desktop.
                anchor=new DesktopRectangle(Math.Min(pet.Left,island.Left),Math.Min(pet.Top,island.Top),
                    Math.Max(pet.Right,island.Right)-Math.Min(pet.Left,island.Left),
                    Math.Max(pet.Bottom,island.Bottom)-Math.Min(pet.Top,island.Top));
            }
            var target=ReminderPlacement.Resolve(anchor,work,_reminderCard.Width,Math.Max(18,_reminderCard.TargetHeight),2);
            bool above=target.Bottom<=anchor.Top;
            double available=above?anchor.Top-work.Top-2:work.Bottom-anchor.Bottom-2;
            double minimumVisible=Math.Min(work.Height,Math.Max(48,_reminderCard.Header.Height+10));
            bool fallback=available<minimumVisible;
            _reminderCard.LimitHeight(fallback?Math.Max(minimumVisible,work.Height*.55):available);
            double actual=Math.Min(_reminderCard.MaxHeight,Math.Max(minimumVisible,_reminderCard.ActualHeight));
            _reminderCard.Left=target.Left;
            double top=fallback
                ? (anchor.Top+anchor.Height/2<work.Top+work.Height/2?work.Bottom-actual:work.Top)
                : above?anchor.Top-2-actual:anchor.Bottom+2;
            _reminderCard.Top=Math.Max(work.Top,Math.Min(work.Bottom-actual,top));
        }
        finally { _positioningReminder=false; }
    }
    private void PlayPlannerAnimation()
    {
        // Called only from the already safety-checked presentation branch.
        if(_plannerAlarmReaction is Guid token && _stateMachine.ActiveReactionToken==token)return;
        CancelBunChase(restorePosition:true,restoreContinuousAnimation:false);
        CancelTimeGreetingPresentation(false,"Alarm is ringing.");
        StopClassicSpinDance(false,"planner.alarm");
        CancelCrystalLongIdle();_bodyReactionMotion.Cancel();ResetBodyReactionMirror();
        DateTimeOffset now=DateTimeOffset.Now;
        var outcome=_stateMachine.TryStartReaction(new ReactionRequest("alarm-tenth-birthday-coming",ReactionPriority.Alarm,now.AddSeconds(ReminderEngine.MaximumRoundSeconds),"planner:reminder",BlocksDisplayModeToggle:true,InterruptibleByDrag:false),now);
        if(outcome.Result is ReactionStartResult.Started or ReactionStartResult.Replaced){_plannerAlarmReaction=outcome.Token;PlayAnimation("alarm-tenth-birthday-coming");}
    }
    private Guid? _plannerAlarmReaction,_plannerAlarmTopmost;
    private void StopPlannerAnimation()
    {
        if(_plannerAlarmReaction is Guid token){_plannerAlarmReaction=null;if(_stateMachine.CompleteReaction(token,DateTimeOffset.Now)&&!_isClosing)PlayResolvedContinuousAnimation();}
    }
    private void StopPlannerPresentation(){ReminderAudio.StopAlarm();StopPlannerAnimation();ReleaseTransientTopmost(_plannerAlarmTopmost);_plannerAlarmTopmost=null;_shownReminders.Clear();}
    private bool _openPlannerNotificationSettings;
    private void OpenReminderSettings()
    {
        if(_settingsWindow!=null){_settingsWindow.NavigateNotifications();_settingsWindow.Activate();return;}
        _openPlannerNotificationSettings=true;ShowSettingsDialog();
    }
    private void ClosePlanner()
    {
        _reminderDisplayTimer.Stop(); _reminderDisplayTimer.Tick -= OnReminderDisplayTick;
        StopPlannerPresentation(); _plannerWindow?.Close(); _reminderCard?.Close(); _reminders?.Dispose();
    }
}
