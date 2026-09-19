namespace LuoTianyiPet.Core.Tests;

public class CalendarEditingTests
{
    [Fact]
    public void SixtyMinuteEarlyReminderCanBeginOnPreviousDay()
    {
        DateTime day=new(2026,9,20);
        var item=new ReminderItem{Calendar=true,Title="跨日提醒",Start=day.AddMinutes(30),CheckedThrough=day.AddMinutes(-40),Enabled=true,EarlyEnabled=true,EarlyMinutes=60};
        var book=new ReminderBook{EngineVersion=1,Items=[item]};
        Assert.True(ReminderEngine.Advance(book,day.AddMinutes(-30)));
        var occurrence=Assert.Single(book.Occurrences);Assert.Equal(item.Start,occurrence.At);Assert.Equal(ReminderPhase.Early,occurrence.Phase);
    }

    [Theory]
    [InlineData("8", "", 8, 0)]
    [InlineData("18", "", 18, 0)]
    [InlineData("830", "", 8, 30)]
    [InlineData("930", "", 9, 30)]
    [InlineData("1230", "", 12, 30)]
    [InlineData("1830", "", 18, 30)]
    [InlineData("9:3", "", 9, 30)]
    [InlineData("8:05", "", 8, 5)]
    [InlineData("08", "3", 8, 30)]
    [InlineData("08", "05", 8, 5)]
    [InlineData("9", "30", 9, 30)]
    [InlineData("0", "0", 0, 0)]
    [InlineData("23", "59", 23, 59)]
    public void SegmentedTimeSupportsCompactInput(string hours,string minutes,int h,int m)
    { Assert.True(CalendarEditing.TryTime(hours,minutes,out var value));Assert.Equal(new TimeSpan(h,m,0),value); }

    [Theory]
    [InlineData("", "")]
    [InlineData("25:00", "")]
    [InlineData("12:75", "")]
    [InlineData("24", "0")]
    [InlineData("-1", "0")]
    [InlineData("abc", "")]
    [InlineData("8:", "")]
    [InlineData("12", "99")]
    public void InvalidTimeDoesNotClamp(string h,string m)
    { Assert.False(CalendarEditing.TryTime(h,m,out _)); }

    [Fact]
    public void DeleteFutureRetainsHistoryAndGroupIdentity()
    {
        DateTime now=new(2026,9,19,12,0,0);
        var item=new ReminderItem{Calendar=true,Title="课程",Repeat=ReminderRepeat.Dates,Start=now.AddDays(-2).Date.AddHours(9),Dates=[now.AddDays(-2).Date,now.Date,now.AddDays(2).Date]};
        var other=new ReminderItem{Start=now.AddDays(3)};var book=new ReminderBook{Items=[item,other]};
        Assert.Equal(1,CalendarEditing.DeleteFuture(book,item.Id,now));Assert.Equal(2,item.Dates.Count);Assert.Equal(ReminderRepeat.Dates,item.Repeat);Assert.Same(item,book.Items[0]);Assert.Same(other,book.Items[1]);
        Assert.Null(ReminderSchedule.Next(item,book,now));ReminderSchedule.Validate(book);
    }

    [Fact]
    public void SaveRetainsLockedDatesTerminalReminderAndStableOrder()
    {
        DateTime now=new(2026,9,19,12,0,0),past=now.AddDays(-1).Date;
        var original=new ReminderItem{Calendar=true,Title="课程",Repeat=ReminderRepeat.Dates,Start=past.AddHours(9),Dates=[past,now.AddDays(1).Date]};
        var other=new ReminderItem();var book=new ReminderBook{Items=[original,other],Occurrences=[new(){RuleId=original.Id,At=original.Start,Phase=ReminderPhase.Done}]};
        var edit=new ReminderItem{Id=original.Id,Calendar=true,Title="新课程",Repeat=ReminderRepeat.Once,Start=now.AddDays(2).Date.AddHours(10),Dates=[now.AddDays(2).Date],Enabled=true};
        CalendarEditing.Save(book,edit,now);ReminderEngine.Reconcile(book);ReminderEngine.Advance(book,now);
        Assert.Equal(new[]{past,now.AddDays(2).Date},edit.Dates);Assert.Equal(ReminderRepeat.Dates,edit.Repeat);Assert.Equal(edit.Id,book.Items[0].Id);Assert.Same(other,book.Items[1]);Assert.Equal(ReminderPhase.Done,Assert.Single(book.Occurrences).Phase);Assert.Equal(now,edit.CheckedThrough);
    }

    [Fact]
    public void ClearingTimeDoesNotReplayPastOrKeepFutureAlarm()
    {
        DateTime now=new(2026,9,19,12,0,0),past=now.AddDays(-1).Date;
        var item=new ReminderItem{Calendar=true,Title="课程",Repeat=ReminderRepeat.Dates,Start=past.AddHours(9),Dates=[past,now.AddDays(1).Date]};
        var book=new ReminderBook{Items=[item],Occurrences=[new(){RuleId=item.Id,At=item.Start,Phase=ReminderPhase.Done},new(){RuleId=item.Id,At=now.AddDays(1).Date.AddHours(9),Phase=ReminderPhase.Early}]};
        var edit=new ReminderItem{Id=item.Id,Calendar=true,Title=item.Title,Start=past,HasTime=false,Enabled=false,Repeat=item.Repeat,Dates=item.Dates};CalendarEditing.Save(book,edit,now);ReminderEngine.Reconcile(book);ReminderEngine.Advance(book,now);
        Assert.Equal(ReminderPhase.Done,Assert.Single(book.Occurrences).Phase);Assert.Null(ReminderSchedule.Next(edit,book,now));Assert.Equal(2,edit.Dates.Count);
    }
}
