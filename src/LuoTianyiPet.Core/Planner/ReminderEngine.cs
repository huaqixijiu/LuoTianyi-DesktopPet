namespace LuoTianyiPet.Core;

public enum ReminderPhase { Waiting, Early, EarlySnoozed, AcknowledgedEarly, Due, DueSnoozed, Done, Cancelled }
public sealed class ReminderOccurrence
{
    public Guid RuleId { get; set; }
    public DateTime At { get; set; }
    public ReminderPhase Phase { get; set; }
    public DateTime? SnoozeAt { get; set; }
    public int Revision { get; set; }
    public DateTime? RoundStartedAt { get; set; }
}
public readonly record struct ReminderUndoSnapshot(
    Guid RuleId, DateTime At, ReminderPhase Phase, DateTime? SnoozeAt,
    DateTime? RoundStartedAt, int Revision, bool ItemEnabled,
    DateTime ItemStart, DateTime ItemCheckedThrough);
public sealed class ReminderPreferences
{
    public bool Sound { get; set; } = true;
    public bool Animation { get; set; } = false;
    public string Tone { get; set; } = "轻提示";
    public double Volume { get; set; } = 0.5;
}
public static class ReminderEngine
{
    public const int MaximumRoundSeconds = 222;
    public const int RetryMinutes = 10;
    public static string Label(ReminderItem item) => string.IsNullOrWhiteSpace(item.Title) ? item.Relative ? "倒计时" : item.Start.ToString("HH:mm") + " 提醒" : item.Title;
    public static bool Active(ReminderItem i) => i.Enabled && i.HasTime && i.PausedSeconds == null;
    public static void Initialize(ReminderBook book, DateTime now)
    {
        foreach(var i in book.Items)
        {
            i.ReminderCreated ??= !i.Calendar || i.Enabled;
            // A legacy "show upcoming" preference is not consent to an early alarm.
            i.EarlyEnabled ??= false;
        }
        if(book.EngineVersion == 1) return;
        foreach(var i in book.Items)
        {
            if(i.PendingAt is DateTime due) book.Occurrences.Add(new() { RuleId=i.Id, At=due, Phase=ReminderPhase.Due });
            else if(i.SnoozeUntil is DateTime snooze) book.Occurrences.Add(new() { RuleId=i.Id, At=i.Start, Phase=ReminderPhase.DueSnoozed, SnoozeAt=snooze });
            if(i.SkippedAt is DateTime skipped) book.Occurrences.Add(new() { RuleId=i.Id, At=skipped, Phase=ReminderPhase.Cancelled });
            i.PendingAt=null; i.SnoozeUntil=null;
        }
        book.EngineVersion=1;
    }
    public static void PrepareStartup(ReminderBook book, DateTime now)
    {
        Initialize(book, now);
        foreach (var occurrence in book.Occurrences)
        {
            if (occurrence.Phase is ReminderPhase.Done or ReminderPhase.Cancelled) continue;
            // A user-requested snooze whose deadline is still ahead is not a missed alarm.
            if (occurrence.Phase == ReminderPhase.DueSnoozed && occurrence.SnoozeAt > now) continue;
            if (occurrence.At <= now)
            {
                occurrence.Phase = ReminderPhase.Cancelled;
                occurrence.SnoozeAt = null;
                occurrence.RoundStartedAt = null;
            }
            else if (occurrence.Phase == ReminderPhase.Early ||
                     occurrence.Phase == ReminderPhase.EarlySnoozed && occurrence.SnoozeAt <= now)
            {
                // Do not replay an old early alert, but keep its future due trigger and cursor.
                occurrence.Phase = ReminderPhase.Waiting;
                occurrence.SnoozeAt = null;
                occurrence.RoundStartedAt = null;
            }
        }
        foreach (var item in book.Items.Where(Active))
        {
            if (item.CheckedThrough < now) item.CheckedThrough = now;
            // Keep the original record/dates; only exhausted standalone alarms stop being enabled.
            if (!item.Calendar && item.Repeat is ReminderRepeat.Once or ReminderRepeat.Dates &&
                ReminderSchedule.Next(item, book, now) == null &&
                !book.Occurrences.Any(o => o.RuleId == item.Id &&
                    o.Phase == ReminderPhase.DueSnoozed && o.SnoozeAt > now))
                item.Enabled = false;
        }
    }
    public static void Reconcile(ReminderBook book)
    {
        foreach (var occurrence in book.Occurrences)
        {
            var item = book.Items.FirstOrDefault(i => i.Id == occurrence.RuleId);
            if (item?.EarlyEnabled == true || occurrence.Phase is not
                (ReminderPhase.Early or ReminderPhase.EarlySnoozed or ReminderPhase.AcknowledgedEarly)) continue;
            // Disabling the advance notice must not suppress the scheduled due alarm.
            occurrence.Phase = ReminderPhase.Waiting;
            occurrence.SnoozeAt = null;
            occurrence.RoundStartedAt = null;
            occurrence.Revision++;
        }
        book.Occurrences.RemoveAll(o => !book.Items.Any(i => i.Id==o.RuleId && ReminderSchedule.OccursOn(i,book,o.At) &&
            (i.Calendar && o.Phase is ReminderPhase.Done or ReminderPhase.Cancelled || Active(i) && o.At.TimeOfDay==i.Start.TimeOfDay)));
    }
    public static bool Advance(ReminderBook book, DateTime now)
    {
        bool changed=false;
        foreach(var i in book.Items.Where(Active))
        {
            // Runtime resume keeps bounded catch-up; PrepareStartup skips time while the app was closed.
            DateTime? next=ReminderSchedule.Next(i,book,i.CheckedThrough);
            if(next is DateTime at && !book.Occurrences.Any(o=>o.RuleId==i.Id && o.At==at))
            {
                bool early=i.EarlyEnabled==true && !i.Relative && at.AddMinutes(-ReminderSchedule.LimitEarlyMinutes(i.EarlyMinutes))>i.CheckedThrough;
                if(at<=now || early && at.AddMinutes(-ReminderSchedule.LimitEarlyMinutes(i.EarlyMinutes))<=now)
                {
                    book.Occurrences.Add(new() { RuleId=i.Id,At=at,Phase=at<=now?ReminderPhase.Due:ReminderPhase.Early });
                    // Future instances are now tracked by their own stable occurrence record.
                    i.CheckedThrough=at<=now?now:at; changed=true;
                }
            }
        }
        foreach(var o in book.Occurrences)
        {
            if(o.Phase is ReminderPhase.Done or ReminderPhase.Cancelled) continue;
            if(o.Phase is ReminderPhase.Waiting or ReminderPhase.Early or ReminderPhase.EarlySnoozed or ReminderPhase.AcknowledgedEarly && now>=o.At)
            { o.Phase=ReminderPhase.Due;o.SnoozeAt=null;o.RoundStartedAt=now;o.Revision++;changed=true; }
            else if(o.SnoozeAt is DateTime snooze && snooze<=now)
            { o.Phase=o.Phase==ReminderPhase.EarlySnoozed?ReminderPhase.Early:ReminderPhase.Due;o.SnoozeAt=null;o.RoundStartedAt=now;o.Revision++;changed=true; }
            if(o.Phase==ReminderPhase.Due)
            {
                if(o.RoundStartedAt==null){o.RoundStartedAt=now;changed=true;}
                if((book.Preferences.Sound||book.Preferences.Animation) && now>=o.RoundStartedAt.Value.AddSeconds(MaximumRoundSeconds))
                {
                    Snooze(book,o.RuleId,o.At,o.Phase,now);
                    changed=true;
                }
            }
        }
        // Old terminal records are safe to prune only after the persisted schedule cursor has crossed them.
        changed |= book.Occurrences.RemoveAll(o => o.At<now.AddDays(-7) && o.Phase is ReminderPhase.Done or ReminderPhase.Cancelled && book.Items.Any(i=>i.Id==o.RuleId && i.CheckedThrough>=o.At))>0;
        return changed;
    }
    public static bool ActivateEarlyIfWithinWindow(ReminderBook book,ReminderItem item,DateTime now)
    {
        if(!Active(item)||item.Relative||item.EarlyEnabled!=true)return false;
        DateTime? next=ReminderSchedule.Next(item,book,now);
        if(next is not DateTime at||at<=now||at.AddMinutes(-ReminderSchedule.LimitEarlyMinutes(item.EarlyMinutes))>now||
            book.Occurrences.Any(o=>o.RuleId==item.Id&&o.At==at))return false;
        book.Occurrences.Add(new ReminderOccurrence{RuleId=item.Id,At=at,Phase=ReminderPhase.Early});
        item.CheckedThrough=at;
        return true;
    }
    public static void Acknowledge(ReminderBook b, Guid id, DateTime at, ReminderPhase expected)
    {
        var o=b.Occurrences.FirstOrDefault(x=>x.RuleId==id && x.At==at);
        if(o==null || o.Phase!=expected) return;
        o.Phase=expected==ReminderPhase.Early?ReminderPhase.AcknowledgedEarly:ReminderPhase.Done;o.SnoozeAt=null;o.RoundStartedAt=null;o.Revision++;
        if(expected is ReminderPhase.Due or ReminderPhase.DueSnoozed)
        {
            ReminderItem? item=b.Items.FirstOrDefault(i=>i.Id==id);
            if(item is {Calendar:false,Relative:false,Enabled:true} && (item.Repeat is ReminderRepeat.Once or ReminderRepeat.Dates) && ReminderSchedule.Next(item,b,at)==null)
                item.Enabled=false;
        }
    }
    public static void Snooze(ReminderBook b,Guid id,DateTime at,ReminderPhase expected,DateTime now)
    {
        var o=b.Occurrences.FirstOrDefault(x=>x.RuleId==id && x.At==at);
        if(o==null || o.Phase!=expected) return;
        o.Phase=expected==ReminderPhase.Early?ReminderPhase.EarlySnoozed:ReminderPhase.DueSnoozed;
        o.RoundStartedAt=null;o.Revision++;
        o.SnoozeAt=expected==ReminderPhase.Early && now.AddMinutes(10)>=at?at:now.AddMinutes(10);
    }
    public static void Cancel(ReminderBook b,Guid id,DateTime at)
    {
        var o=b.Occurrences.FirstOrDefault(x=>x.RuleId==id && x.At==at);
        if(o==null||o.Phase is ReminderPhase.Done or ReminderPhase.Cancelled)return;
        o.Phase=ReminderPhase.Cancelled;o.SnoozeAt=null;o.RoundStartedAt=null;o.Revision++;
        var item=b.Items.FirstOrDefault(i=>i.Id==id);
        if(item is {Calendar:false,Relative:false,Enabled:true} &&
            (item.Repeat is ReminderRepeat.Once or ReminderRepeat.Dates) &&
            ReminderSchedule.Next(item,b,at)==null)
            item.Enabled=false;
    }
    public static ReminderUndoSnapshot? CaptureUndo(ReminderBook b,Guid id,DateTime at)
    {
        var item=b.Items.FirstOrDefault(i=>i.Id==id);
        var occurrence=b.Occurrences.FirstOrDefault(o=>o.RuleId==id&&o.At==at);
        return item==null||occurrence==null?null:new ReminderUndoSnapshot(id,at,occurrence.Phase,
            occurrence.SnoozeAt,occurrence.RoundStartedAt,occurrence.Revision,item.Enabled,item.Start,item.CheckedThrough);
    }
    public static bool TryUndo(ReminderBook b,ReminderUndoSnapshot before,ReminderPhase after,DateTime now)
    {
        var item=b.Items.FirstOrDefault(i=>i.Id==before.RuleId);
        if(item==null||item.Start!=before.ItemStart||item.CheckedThrough!=before.ItemCheckedThrough)return false;
        var occurrence=b.Occurrences.FirstOrDefault(o=>o.RuleId==before.RuleId&&o.At==before.At);
        if(occurrence==null)
        {
            // Completing the final standalone occurrence disables the item, so Reconcile prunes its record.
            if(after is not (ReminderPhase.Done or ReminderPhase.Cancelled)||item.Calendar||item.Enabled||!before.ItemEnabled)return false;
            occurrence=new ReminderOccurrence{RuleId=before.RuleId,At=before.At,Phase=after,Revision=before.Revision+1};
            b.Occurrences.Add(occurrence);
        }
        else if(occurrence.Phase!=after||occurrence.Revision!=before.Revision+1)return false;
        if(item.Enabled!=before.ItemEnabled && !((after is ReminderPhase.Done or ReminderPhase.Cancelled)&&before.ItemEnabled&&!item.Enabled))return false;
        item.Enabled=before.ItemEnabled;
        bool due=now>=before.At&&(before.Phase is ReminderPhase.Early or ReminderPhase.AcknowledgedEarly or ReminderPhase.Waiting);
        occurrence.Phase=due?ReminderPhase.Due:before.Phase;
        occurrence.SnoozeAt=due?null:before.SnoozeAt;
        occurrence.RoundStartedAt=due||before.Phase==ReminderPhase.Due?now:before.RoundStartedAt;
        occurrence.Revision++;
        return true;
    }
    public static void SetEnabled(ReminderBook b,Guid id,bool enabled,DateTime now)
    {
        var i=b.Items.FirstOrDefault(x=>x.Id==id);if(i==null)return;
        i.Enabled=enabled && i.HasTime;i.CheckedThrough=now;i.ReminderCreated=true;
        b.Occurrences.RemoveAll(o=>o.RuleId==id);i.PendingAt=null;i.SnoozeUntil=null;
        // A finished standalone alarm is still a useful preset. Switching it on
        // schedules the next occurrence at its saved time without opening edit.
        if(i.Enabled&&!i.Calendar&&ReminderSchedule.Next(i,b,now)==null)
        {
            if(i.Relative)
            {
                i.Start=now.AddSeconds(Math.Max(1,i.DurationSeconds));
                i.PausedSeconds=null;
            }
            else
            {
                DateTime next=now.Date+i.Start.TimeOfDay;
                if(next<=now)next=next.AddDays(1);
                if(i.Repeat==ReminderRepeat.Dates)
                {
                    if(!i.Dates.Contains(next.Date))i.Dates.Add(next.Date);
                }
                else i.Start=next;
            }
        }
    }
    public static double Remaining(ReminderItem i,DateTime now) => i.PausedSeconds ?? Math.Max(0,(i.Start-now).TotalSeconds);
    public static void Pause(ReminderBook b,Guid id,DateTime now)
    {
        var i=b.Items.First(x=>x.Id==id);if(!i.Relative || !i.Enabled || i.Start<=now)return;
        i.PausedSeconds=Remaining(i,now);b.Occurrences.RemoveAll(o=>o.RuleId==id);
    }
    public static void Resume(ReminderBook b,Guid id,DateTime now)
    {
        var i=b.Items.First(x=>x.Id==id);if(i.PausedSeconds is not double seconds)return;
        i.Start=now.AddSeconds(seconds);i.CheckedThrough=now;i.PausedSeconds=null;
    }
}
