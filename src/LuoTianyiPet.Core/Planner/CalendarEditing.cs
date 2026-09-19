using System.Globalization;

namespace LuoTianyiPet.Core;

/// <summary>Calendar input and finite-group edits, independent of WPF.</summary>
public static class CalendarEditing
{
    public static bool TryTime(string hours, string minutes, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        hours = hours.Trim(); minutes = minutes.Trim();
        if (hours.Contains(":"))
        {
            string[] parts = hours.Split(':');
            if (parts.Length != 2 || parts[1].Length == 0) return false;
            hours = parts[0]; minutes = parts[1];
        }
        else if (hours.Length is 3 or 4)
        {
            minutes = hours.Substring(hours.Length - 2); hours = hours.Substring(0, hours.Length - 2);
        }
        if (hours.Length is < 1 or > 2 || !hours.All(c => c is >= '0' and <= '9')) return false;
        if (minutes.Length == 0) minutes = "00";
        if (minutes.Length == 1) minutes += "0";
        if (minutes.Length != 2 || !minutes.All(c => c is >= '0' and <= '9')) return false;
        int h = int.Parse(hours, CultureInfo.InvariantCulture), m = int.Parse(minutes, CultureInfo.InvariantCulture);
        if (h > 23 || m > 59) return false;
        time = new TimeSpan(h, m, 0); return true;
    }

    public static bool IsPast(ReminderItem item, DateTime day, DateTime now) => item.HasTime
        ? day.Date + item.Start.TimeOfDay <= now : day.Date < now.Date;

    public static List<DateTime> Dates(ReminderItem item) => (item.Repeat == ReminderRepeat.Once ? new[] { item.Start.Date }.AsEnumerable() : item.Dates)
        .Where(d => !item.ExcludedDates.Contains(d.Date)).Select(d => d.Date).Distinct().OrderBy(d => d).ToList();

    public static int DeleteFuture(ReminderBook book, Guid id, DateTime now)
    {
        var item = book.Items.FirstOrDefault(i => i.Id == id);
        if (item == null || item.Repeat != ReminderRepeat.Dates) return 0;
        DateTime[] future = Dates(item).Where(day => !IsPast(item, day, now)).ToArray();
        foreach (DateTime day in future) ReminderSchedule.DeleteDate(book, id, day);
        book.Occurrences.RemoveAll(o => o.RuleId == id && future.Contains(o.At.Date));
        return future.Length;
    }

    public static void Save(ReminderBook book, ReminderItem item, DateTime now)
    {
        int index = book.Items.FindIndex(i => i.Id == item.Id);
        var original = index >= 0 ? book.Items[index] : null;
        if (original?.Repeat == ReminderRepeat.Dates)
        {
            var past = Dates(original).Where(day => IsPast(original, day, now));
            item.Dates = item.Dates.Concat(past).Distinct().OrderBy(day => day).ToList();
            // A group remains a group when its last future date is removed.
            item.Repeat = ReminderRepeat.Dates;
            item.Start = item.Dates.Min().Date + item.Start.TimeOfDay;
        }
        item.CheckedThrough = now;
        if (original != null)
        {
            item.Sound = original.Sound; item.ShowCountdown = original.ShowCountdown;
            foreach (var occurrence in book.Occurrences.Where(o => o.RuleId == item.Id && o.At <= now && o.Phase is not (ReminderPhase.Done or ReminderPhase.Cancelled)))
            {
                if (!item.Enabled || !item.HasTime || occurrence.At.TimeOfDay != item.Start.TimeOfDay)
                { occurrence.Phase = ReminderPhase.Cancelled; occurrence.SnoozeAt = null; occurrence.RoundStartedAt = null; }
            }
        }
        book.Occurrences.RemoveAll(o => o.RuleId == item.Id && o.At > now &&
            (!item.Enabled || !item.HasTime || !Dates(item).Contains(o.At.Date) || o.At.TimeOfDay != item.Start.TimeOfDay));
        if (index >= 0) book.Items[index] = item; else book.Items.Add(item);
    }
}
