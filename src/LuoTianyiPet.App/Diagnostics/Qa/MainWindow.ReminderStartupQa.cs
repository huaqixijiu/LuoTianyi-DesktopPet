using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using LuoTianyiPet.Core;
using LuoTianyiPet.Platform.Windows;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    private async Task VerifyReminderStartupQaAsync(string path, Action<bool, string> check, Action<Window, string> snapshot)
    {
        var paths = new LocalAppPaths(Path.Combine(path, "StartupUserData"));
        var store = new ReminderStore(paths);
        DateTime now = DateTime.Now;
        ReminderItem once = new() { Title = "离线已过期闹钟", Start = now.AddHours(-2), CheckedThrough = now.AddDays(-2), EarlyEnabled = false };
        ReminderItem daily = new() { Title = "未来继续每日提醒", Start = now.AddDays(-1), CheckedThrough = now.AddDays(-2), Repeat = ReminderRepeat.Daily, EarlyEnabled = false };
        ReminderItem calendar = new() { Calendar = true, Title = "保留历史日程", Notes = "历史内容仍可查看", Start = now.AddHours(-1), CheckedThrough = now.AddDays(-2), EarlyEnabled = false };
        // Keep this one outside its advance window: startup must silently skip
        // an early notice that was missed while the app was closed.
        ReminderItem early = new() { Title = "稍后正式到点", Start = now.AddHours(2), CheckedThrough = now.AddHours(2), EarlyEnabled = true };
        ReminderBook seed = new() { EngineVersion = 1, Items = [once, daily, calendar, early] };
        seed.Occurrences.Add(new() { RuleId = calendar.Id, At = calendar.Start, Phase = ReminderPhase.Due });
        seed.Occurrences.Add(new() { RuleId = early.Id, At = early.Start, Phase = ReminderPhase.Early });
        await store.SaveAsync(seed);
        using var startup = new ReminderService(paths);
        int publications = 0;
        startup.Changed += () =>
        {
            publications++;
            check(!startup.Book.Occurrences.Any(o => o.Phase is ReminderPhase.Due or ReminderPhase.Early), "startup never publishes expired ringing instances");
        };
        await startup.LoadAsync();
        check(publications > 0, "startup publishes normalized saved book");
        check(startup.Book.Items.Count == 4 && !startup.Book.Items.Single(i => i.Id == once.Id).Enabled, "startup keeps exhausted standalone alarm as disabled record");
        check(startup.Book.Items.Single(i => i.Id == daily.Id).Enabled && ReminderSchedule.Next(startup.Book.Items.Single(i => i.Id == daily.Id), startup.Book, DateTime.Now) > now, "startup keeps next daily occurrence");
        check(startup.Book.Items.Single(i => i.Id == calendar.Id).Notes == calendar.Notes && startup.Book.Occurrences.Single(o => o.RuleId == calendar.Id).Phase == ReminderPhase.Cancelled, "startup retains calendar history without active alert");
        check(startup.Book.Occurrences.Single(o => o.RuleId == early.Id).Phase == ReminderPhase.Waiting, "startup early alert waits silently for future due");
        var persisted = await store.LoadAsync();
        check(!persisted.Items.Single(i => i.Id == once.Id).Enabled && persisted.Occurrences.All(o => o.Phase is ReminderPhase.Cancelled or ReminderPhase.Waiting), "startup silence and cursors persist atomically");
        using (var reload = new ReminderService(paths))
        {
            await reload.LoadAsync();
            check(reload.Book.Items.Count == 4 && !reload.Book.Occurrences.Any(o => o.Phase is ReminderPhase.Due or ReminderPhase.Early), "second startup does not replay expired alerts");
        }
        var originalService = _reminders;
        try
        {
            _reminders = startup;
            RefreshReminderCardCore(true);
            check(_reminderCard == null || !_reminderCard.IsVisible, "real reminder presenter shows no card after expired-only startup");
            var history = new PlannerWindow(startup, true);
            try
            {
                history.Show(); await Task.Delay(150);
                snapshot(history, "32-startup-missed-alarms");
            }
            finally { history.Close(); }
        }
        finally { _reminders = originalService; }

        // A restart during the active early window must keep every saved
        // occurrence visible, rather than collapsing the list to whichever
        // occurrence happens to be regenerated first.
        var currentPaths = new LocalAppPaths(Path.Combine(path, "StartupCurrentEarlyUserData"));
        var currentStore = new ReminderStore(currentPaths);
        DateTime currentNow = DateTime.Now;
        ReminderBook currentSeed = new() { EngineVersion = 1 };
        for (int index = 0; index < 3; index++)
        {
            ReminderItem item = new()
            {
                Title = $"重启后提前提醒 {index + 1}",
                Start = currentNow.AddMinutes(8 + index * 5),
                CheckedThrough = currentNow.AddMinutes(8 + index * 5),
                EarlyEnabled = true,
                ReminderCreated = true,
            };
            currentSeed.Items.Add(item);
            currentSeed.Occurrences.Add(new() { RuleId = item.Id, At = item.Start, Phase = ReminderPhase.Early });
        }
        await currentStore.SaveAsync(currentSeed);
        using (var current = new ReminderService(currentPaths))
        {
            await current.LoadAsync();
            var previous = _reminders;
            try
            {
                _reminders = current;
                _reminderCardKey = "";
                _reminderCardOccurrenceKey = "";
                _quickReminderExpanded = false;
                RefreshReminderCardCore(true);
                check(current.Book.Occurrences.Count == 3 &&
                    current.Book.Occurrences.All(o => o.Phase == ReminderPhase.Early),
                    "restart preserves all three active early occurrences");
                check(_reminderCard is { IsVisible: true, IsExpanded: true },
                    "restart presents the restored early reminders as one expanded card");
                snapshot(this, "33-startup-current-multiple-early");
            }
            finally
            {
                _reminders = previous;
                _reminderCard?.Hide();
                _reminderCardKey = "";
                _reminderCardOccurrenceKey = "";
            }
        }

        // Simulate a write failure without changing permissions or touching real user data.
        var blockedPaths = new LocalAppPaths(Path.Combine(path, "StartupBlockedUserData"));
        var blockedStore = new ReminderStore(blockedPaths);
        await blockedStore.SaveAsync(seed);
        string file = Path.Combine(blockedPaths.RootDirectory, "reminders.json");
        string original = File.ReadAllText(file);
        using var locked = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var blocked = new ReminderService(blockedPaths);
        bool failed = false, published = false;
        blocked.Changed += () => published = true;
        try { await blocked.LoadAsync(); }
        catch (IOException) { failed = true; }
        var timer = (DispatcherTimer)typeof(ReminderService).GetField("_timer", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(blocked)!;
        check(failed && !published && !timer.IsEnabled, "failed startup save publishes nothing and cannot schedule stale alarms");
        check(File.ReadAllText(file) == original, "failed startup save preserves original reminders");
    }
}
