using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class ReminderStartupTests
{
    private static readonly DateTime Now = new(2026, 9, 19, 12, 0, 0);
    private static ReminderItem Item() => new()
    {
        Title = "保留记录", Notes = "不删除历史", Start = Now.AddDays(-3),
        CheckedThrough = Now.AddDays(-4), EarlyEnabled = false, ReminderCreated = true
    };
    private static ReminderBook Book(ReminderItem item) => new() { EngineVersion = 1, Items = [item] };

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void MissedFiniteReminderKeepsRecordWithoutCatchUp(bool calendar, bool countdown)
    {
        var item = Item(); item.Calendar = calendar; item.Relative = countdown;
        var book = Book(item);
        ReminderEngine.PrepareStartup(book, Now);
        ReminderEngine.Reconcile(book);
        Assert.False(ReminderEngine.Advance(book, Now));
        Assert.Empty(book.Occurrences);
        Assert.Same(item, Assert.Single(book.Items));
        Assert.Equal("不删除历史", item.Notes);
        Assert.Equal(calendar, item.Enabled);
        Assert.Equal(Now, item.CheckedThrough);
    }

    [Theory]
    [InlineData(ReminderRepeat.Daily)]
    [InlineData(ReminderRepeat.Weekly)]
    [InlineData(ReminderRepeat.Workdays)]
    [InlineData(ReminderRepeat.RestDays)]
    [InlineData(ReminderRepeat.Dates)]
    public void RepeatingRuleSkipsOfflineOccurrencesButFiresNext(ReminderRepeat repeat)
    {
        var item = Item(); item.Repeat = repeat;
        item.Weekdays = [DayOfWeek.Wednesday];
        item.Dates = [item.Start.Date, Now.AddDays(2).Date];
        var book = Book(item);
        ReminderEngine.PrepareStartup(book, Now);
        Assert.True(item.Enabled);
        Assert.False(ReminderEngine.Advance(book, Now));
        var next = ReminderSchedule.Next(item, book, Now);
        Assert.NotNull(next); Assert.True(next > Now);
        ReminderEngine.Advance(book, next!.Value);
        var occurrence = Assert.Single(book.Occurrences);
        Assert.Equal(next, occurrence.At); Assert.Equal(ReminderPhase.Due, occurrence.Phase);
    }

    [Fact]
    public void ExhaustedDatesCloseWithoutDeletingDates()
    {
        var item = Item(); item.Repeat = ReminderRepeat.Dates;
        item.Dates = [item.Start.Date, Now.AddDays(-1).Date];
        var book = Book(item);
        ReminderEngine.PrepareStartup(book, Now);
        Assert.False(item.Enabled); Assert.Equal(2, item.Dates.Count);
        Assert.False(ReminderEngine.Advance(book, Now));
    }

    [Theory]
    [InlineData(ReminderPhase.Waiting)]
    [InlineData(ReminderPhase.Early)]
    [InlineData(ReminderPhase.EarlySnoozed)]
    [InlineData(ReminderPhase.AcknowledgedEarly)]
    [InlineData(ReminderPhase.Due)]
    [InlineData(ReminderPhase.DueSnoozed)]
    public void PersistedOverdueInstancesBecomeSilentHistory(ReminderPhase phase)
    {
        var item = Item(); item.Calendar = true;
        var book = Book(item);
        book.Occurrences.Add(new() { RuleId = item.Id, At = item.Start, Phase = phase,
            SnoozeAt = Now.AddMinutes(-1), RoundStartedAt = Now.AddMinutes(-5) });
        ReminderEngine.PrepareStartup(book, Now);
        ReminderEngine.Reconcile(book);
        var occurrence = Assert.Single(book.Occurrences);
        Assert.Equal(ReminderPhase.Cancelled, occurrence.Phase);
        Assert.Null(occurrence.SnoozeAt); Assert.Null(occurrence.RoundStartedAt);
        Assert.False(ReminderEngine.Advance(book, Now));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissedEarlyAlertWaitsSilentlyForFutureDue(bool persisted)
    {
        var item = Item(); item.Start = Now.AddMinutes(10); item.EarlyEnabled = true;
        var book = Book(item);
        if (persisted) ReminderEngine.Advance(book, Now.AddMinutes(-10));
        ReminderEngine.PrepareStartup(book, Now);
        Assert.DoesNotContain(book.Occurrences, o => o.Phase is ReminderPhase.Early or ReminderPhase.Due);
        if (persisted) Assert.Equal(ReminderPhase.Waiting, Assert.Single(book.Occurrences).Phase);
        Assert.False(ReminderEngine.Advance(book, Now));
        ReminderEngine.Advance(book, item.Start);
        Assert.Equal(ReminderPhase.Due, Assert.Single(book.Occurrences).Phase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EarlySnoozeKeepsFutureDeadlineButSkipsMissedDeadline(bool future)
    {
        var item = Item(); item.Start = Now.AddMinutes(20); item.EarlyEnabled = true;
        var book = Book(item);
        ReminderEngine.Advance(book, Now.AddMinutes(-10));
        ReminderEngine.Snooze(book, item.Id, item.Start, ReminderPhase.Early, Now.AddMinutes(future ? -5 : -15));
        ReminderEngine.PrepareStartup(book, Now);
        Assert.Equal(future ? ReminderPhase.EarlySnoozed : ReminderPhase.Waiting, Assert.Single(book.Occurrences).Phase);
        Assert.False(ReminderEngine.Advance(book, Now));
        ReminderEngine.Advance(book, future ? Now.AddMinutes(5) : item.Start);
        Assert.Equal(future ? ReminderPhase.Early : ReminderPhase.Due, book.Occurrences[0].Phase);
    }

    [Fact]
    public void FutureDueSnoozeRemainsEnabledAndFiresAtRequestedTime()
    {
        var item = Item(); item.Start = Now.AddMinutes(-5);
        var book = Book(item);
        ReminderEngine.Advance(book, item.Start);
        ReminderEngine.Snooze(book, item.Id, item.Start, ReminderPhase.Due, item.Start);
        ReminderEngine.PrepareStartup(book, Now);
        ReminderEngine.Reconcile(book);
        Assert.True(item.Enabled);
        Assert.False(ReminderEngine.Advance(book, Now));
        ReminderEngine.Advance(book, Now.AddMinutes(5));
        Assert.Equal(ReminderPhase.Due, Assert.Single(book.Occurrences).Phase);
    }

    [Fact]
    public void FutureReminderAndFutureEarlyAlertRemainScheduled()
    {
        var item = Item(); item.Start = Now.AddHours(1); item.EarlyEnabled = true;
        var book = Book(item);
        ReminderEngine.PrepareStartup(book, Now);
        Assert.True(item.Enabled); Assert.False(ReminderEngine.Advance(book, Now));
        ReminderEngine.Advance(book, Now.AddMinutes(30));
        Assert.Equal(ReminderPhase.Early, Assert.Single(book.Occurrences).Phase);
    }

    [Fact]
    public void PausedCountdownKeepsItsRemainingTime()
    {
        var item = Item(); item.Relative = true; item.PausedSeconds = 75;
        var book = Book(item);
        ReminderEngine.PrepareStartup(book, Now);
        Assert.True(item.Enabled); Assert.Equal(75, item.PausedSeconds);
        Assert.False(ReminderEngine.Advance(book, Now));
        ReminderEngine.Resume(book, item.Id, Now);
        ReminderEngine.Advance(book, Now.AddSeconds(75));
        Assert.Equal(ReminderPhase.Due, Assert.Single(book.Occurrences).Phase);
    }

    [Fact]
    public void StartupBoundarySkipsDueNowAndIsIdempotent()
    {
        var item = Item(); item.Start = Now; item.Calendar = true;
        var book = Book(item);
        ReminderEngine.PrepareStartup(book, Now); ReminderEngine.PrepareStartup(book, Now);
        Assert.False(ReminderEngine.Advance(book, Now));
        Assert.Empty(book.Occurrences); Assert.Single(book.Items);
    }

    [Fact]
    public void LegacyPendingIsMigratedBeforeBeingSilenced()
    {
        var item = Item(); item.Calendar = true; item.PendingAt = item.Start;
        var book = Book(item); book.EngineVersion = 0;
        ReminderEngine.PrepareStartup(book, Now); ReminderEngine.Reconcile(book);
        Assert.Equal(1, book.EngineVersion); Assert.Null(item.PendingAt);
        Assert.Equal(ReminderPhase.Cancelled, Assert.Single(book.Occurrences).Phase);
        Assert.False(ReminderEngine.Advance(book, Now));
    }

    [Fact]
    public void CancelledFutureEarlyOccurrenceDoesNotBlockFollowingRepeat()
    {
        var item = Item(); item.Start = Now.AddMinutes(10); item.EarlyEnabled = true; item.Repeat = ReminderRepeat.Daily;
        var book = Book(item);
        ReminderEngine.Advance(book, Now.AddMinutes(-10)); ReminderEngine.Cancel(book, item.Id, item.Start);
        ReminderEngine.PrepareStartup(book, Now);
        Assert.Equal(item.Start, item.CheckedThrough);
        ReminderEngine.Advance(book, item.Start.AddDays(1));
        Assert.Contains(book.Occurrences, o => o.At == item.Start.AddDays(1) && o.Phase == ReminderPhase.Due);
    }

    [Fact]
    public void RunningAppStillHandlesMissedTickWithoutStartupPolicy()
    {
        var item = Item(); var book = Book(item);
        Assert.True(ReminderEngine.Advance(book, Now));
        Assert.Equal(ReminderPhase.Due, Assert.Single(book.Occurrences).Phase);
    }
}
