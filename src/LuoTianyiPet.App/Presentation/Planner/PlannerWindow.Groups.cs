using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LuoTianyiPet.Core;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace LuoTianyiPet.App;

internal sealed partial class PlannerWindow
{
    private enum ManageFilter { All, Active, Ended }

    private sealed class ScheduleProjection
    {
        public ReminderItem Item { get; set; } = null!;
        public List<DateTime> Occurrences { get; set; } = [];
        public DateTime? Next { get; set; }
        public DateTime? Last { get; set; }
        public int PastCount { get; set; }
        public int FutureCount { get; set; }
        public bool IsFinite { get; set; }
        public bool IsEnded { get; set; }
    }

    private static string GroupLabel(ReminderItem item, int total)
    {
        if (item.Repeat == ReminderRepeat.Once) return "单日日程";
        if (item.Repeat == ReminderRepeat.Dates) return $"多日期 · {total}次";
        return "重复日程 · " + Repeats[(int)item.Repeat];
    }

    private static string WeekdayLabel(DateTime day) => "一二三四五六日"[((int)day.DayOfWeek + 6) % 7].ToString();
    private static DateTime At(ReminderItem item, DateTime day) => day.Date + (item.HasTime ? item.Start.TimeOfDay : TimeSpan.Zero);
    private static bool IsFuture(ReminderItem item, DateTime day, DateTime now) => item.HasTime ? At(item, day) > now : day.Date >= now.Date;

    private static string FormatOccurrence(ReminderItem item, DateTime day, DateTime now, bool prefixNext = false)
    {
        string date = day.Year == now.Year ? $"{day:M月d日} 周{WeekdayLabel(day)}" : $"{day:yyyy年M月d日} 周{WeekdayLabel(day)}";
        string time = item.HasTime ? $" · {item.Start:HH:mm}" : " · 未设时间";
        return (prefixNext ? "下次：" : "") + date + time;
    }

    private static string FormatLast(ReminderItem item, DateTime day, DateTime now)
    {
        string date = day.Year == now.Year ? $"{day:M月d日} 周{WeekdayLabel(day)}" : $"{day:yyyy年M月d日} 周{WeekdayLabel(day)}";
        return item.HasTime ? $"{date} · {item.Start:HH:mm}" : $"{date} · 未设时间";
    }

    private static string FormatRange(IReadOnlyList<DateTime> occurrences)
    {
        DateTime first = occurrences[0], last = occurrences[occurrences.Count - 1];
        if (first.Year == last.Year) return $"{first:yyyy年M月d日} — {last:M月d日}";
        return $"{first:yyyy年M月d日} — {last:yyyy年M月d日}";
    }

    private static IEnumerable<DateTime> ExplicitOccurrences(ReminderItem item)
    {
        IEnumerable<DateTime> days = item.Repeat == ReminderRepeat.Once ? [item.Start.Date] : item.Dates.Select(d => d.Date);
        return days.Where(day => day >= item.Start.Date && !item.ExcludedDates.Contains(day)).Distinct().OrderBy(day => day);
    }

    private DateTime? NextDateWithoutTime(ReminderItem item, DateTime now)
    {
        DateTime first = now.Date < item.Start.Date ? item.Start.Date : now.Date;
        for (DateTime day = first; day <= ReminderSchedule.MaximumDate; day = day.AddDays(1))
            if (ReminderSchedule.OccursOn(item, _service.Book, day) && !item.ExcludedDates.Contains(day.Date)) return day;
        return null;
    }

    private DateTime? NextOccurrence(ReminderItem item, DateTime now)
    {
        if (item.Repeat is ReminderRepeat.Once or ReminderRepeat.Dates)
        {
            DateTime? day = ExplicitOccurrences(item).Where(d => IsFuture(item, d, now)).Cast<DateTime?>().FirstOrDefault();
            return day is DateTime value ? At(item, value) : null;
        }
        return item.HasTime ? ReminderSchedule.Next(item, _service.Book, now) : NextDateWithoutTime(item, now);
    }

    private ScheduleProjection Project(ReminderItem item, DateTime now)
    {
        if (item.Repeat is ReminderRepeat.Once or ReminderRepeat.Dates)
        {
            List<DateTime> occurrences = ExplicitOccurrences(item).ToList();
            int future = occurrences.Count(day => IsFuture(item, day, now));
            int past = occurrences.Count - future;
            DateTime? next = occurrences.Where(day => IsFuture(item, day, now)).Select(day => (DateTime?)At(item, day)).FirstOrDefault();
            DateTime? last = occurrences.Where(day => !IsFuture(item, day, now)).Select(day => (DateTime?)At(item, day)).LastOrDefault();
            bool ended = item.Repeat==ReminderRepeat.Once ? item.Start.Date<now.Date : future==0;
            return new ScheduleProjection { Item = item, Occurrences = occurrences, Next = next, Last = last, PastCount = past, FutureCount = future, IsFinite = true, IsEnded = ended };
        }

        DateTime? upcoming = NextOccurrence(item, now);
        bool isEnded = upcoming == null;
        DateTime? lastOccurrence = item.CheckedThrough > item.Start ? item.CheckedThrough : item.Start;
        return new ScheduleProjection { Item = item, Occurrences = [], Next = upcoming, Last = lastOccurrence, PastCount = 0, FutureCount = upcoming == null ? 0 : 1, IsFinite = false, IsEnded = isEnded };
    }

    private static bool Matches(ScheduleProjection projection, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        return projection.Item.Title.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0 || projection.Item.Notes.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0;
    }

    private static string SingleStatus(ScheduleProjection projection, DateTime now)
    {
        if (projection.IsEnded) return "已结束";
        if (projection.Item.Start.Date == now.Date) return "今天";
        return "未开始";
    }

    private static string MultiSummary(ScheduleProjection projection) => projection.IsEnded ? $"已发生{projection.PastCount}次 · 已结束" : $"已发生{projection.PastCount}次 · 未来{projection.FutureCount}次";

    private void ShowOverlay(UIElement content, bool editor = false)
    {
        Grid overlay = new() { Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(72, 37, 63, 107)) };
        bool calendarEditor = editor && content is FrameworkElement element && Equals(element.Tag, "CalendarEditor");
        double editorPadding=_pageSize switch{"mini"=>18,"standard"=>20,"comfortable"=>22,_=>24};
        Border card = new() { Background = Brushes.White, CornerRadius = new CornerRadius(14), Padding = new Thickness(editor ? editorPadding+(calendarEditor?0:2) : 24), BorderBrush = PlannerTheme.Line, BorderThickness = new Thickness(1), HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16) };
        // Keep the shadow on a separate, text-free visual. An effect on card rasterizes its text.
        Border shadow=new(){Background=Brushes.White,CornerRadius=card.CornerRadius,Margin=card.Margin,HorizontalAlignment=card.HorizontalAlignment,VerticalAlignment=card.VerticalAlignment,IsHitTestVisible=false,Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=30,ShadowDepth=6,Opacity=.14,Color=System.Windows.Media.Color.FromRgb(32,65,105)}};
        shadow.SetBinding(WidthProperty,new System.Windows.Data.Binding("ActualWidth"){Source=card});
        shadow.SetBinding(HeightProperty,new System.Windows.Data.Binding("ActualHeight"){Source=card});
        if (editor)
        {
            if (content is FrameworkElement editorContent)
            {
                // Native-size pages need room for the dialog's padding, border
                // and outer margins as well as its form. Otherwise the fixed
                // form height clips the action row inside the card.
                editorContent.MaxWidth = Math.Max(1, _shell.Width - card.Margin.Left - card.Margin.Right - card.Padding.Left - card.Padding.Right - card.BorderThickness.Left - card.BorderThickness.Right);
                editorContent.MaxHeight = Math.Max(1, _shell.Height - card.Margin.Top - card.Margin.Bottom - card.Padding.Top - card.Padding.Bottom - card.BorderThickness.Top - card.BorderThickness.Bottom);
            }
            card.Child = content;
        }
        else card.Child = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = Math.Max(250, _shell.Height - 130) };
        overlay.Children.Add(shadow);overlay.Children.Add(card); _root.IsEnabled = false;
        if (_shell.Children.Count > 1) _shell.Children[_shell.Children.Count - 1].IsEnabled = false;
        _shell.Children.Add(overlay);
    }

    private void CloseTopOverlay()
    {
        if (_shell.Children.Count > 1) _shell.Children.RemoveAt(_shell.Children.Count - 1);
        _shell.Children[_shell.Children.Count - 1].IsEnabled = true;
    }

    private void ConfirmDelete(IEnumerable<Guid> ids, DateTime? day)
    {
        Guid[] selected = ids.Distinct().ToArray();
        var items = _service.Book.Items.Where(i => selected.Contains(i.Id)).ToList();
        if (items.Count == 0) return;
        StackPanel panel = new() { Width = 440 };
        bool alarmOnly = items.All(i => !i.Calendar);
        StackPanel titleRow = Row();
        titleRow.Children.Add(new Border { Child = new TextBlock { Text = "!", FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }, Width = 30, Height = 30, Background = PlannerTheme.Danger, CornerRadius = new CornerRadius(15), Margin = new Thickness(0, 2, 10, 2), VerticalAlignment = VerticalAlignment.Center });
        titleRow.Children.Add(Text(alarmOnly ? "删除提醒？" : day == null ? "删除整组日程？" : "仅删除这一天？", 20, FontWeights.SemiBold)); panel.Children.Add(titleRow);
        string impact = day is DateTime date ? $"仅移除 {date:M月d日} 的日程及提醒，其他日期保持不变。" : alarmOnly ? "将删除提醒及其全部待处理提示。" : $"将删除 {items.Count} 组日程，以及它们关联的全部日期和提醒。";
        panel.Children.Add(Text(impact));
        if (items.Count == 1 && !alarmOnly) panel.Children.Add(Text(GroupLabel(items[0], items[0].Repeat == ReminderRepeat.Dates ? items[0].Dates.Count : 1), 13));
        StackPanel actions = Row(); actions.HorizontalAlignment = System.Windows.HorizontalAlignment.Right; actions.Children.Add(Action("取消", CloseTopOverlay));
        var confirm = AsyncAction(alarmOnly ? "确认删除" : day == null ? "确认删除整组" : "确认删除这一天", async () => { await Execute(b => { if (day is DateTime d) ReminderSchedule.DeleteDate(b, selected[0], d); else ReminderSchedule.DeleteGroups(b, selected); }); _editing = false; Render(); });
        confirm.Name = "ConfirmGroupDelete"; confirm.Background = PlannerTheme.Danger; confirm.Foreground = Brushes.White; actions.Children.Add(confirm); panel.Children.Add(actions); ShowOverlay(panel);
    }

    private Button FilterButton(string name, string label, bool selected, Action click)
    {
        bool mini=_pageSize=="mini";Button button = new() { Name = name, Content = label, Width = mini?70:78, Height = mini?30:32, Margin = new Thickness(2), Padding = new Thickness(7, 4, 7, 4), Background = selected ? PlannerTheme.AccentSoft : Brushes.Transparent, Foreground = selected ? PlannerTheme.Accent : PlannerTheme.Ink, BorderThickness = new Thickness(0), FontSize = mini?12:14 };
        button.Click += (_, _) => click(); return button;
    }

    private TextBox CreateManageSearch(Grid host)
    {
        TextBox input = new() { Name = "ManageSearch", Text = _manageQuery, Padding = new Thickness(36, 7, 8, 7), FontSize = _pageSize=="mini"?12:14, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "搜索日程标题或备注" };
        host.Children.Add(input);
        var icon = PlannerTheme.Icon("search", 19, PlannerTheme.Muted, 0); icon.HorizontalAlignment = HorizontalAlignment.Left; icon.Margin = new Thickness(13, 0, 0, 0); icon.IsHitTestVisible = false; host.Children.Add(icon);
        TextBlock placeholder = Text("搜索日程标题或备注…", _pageSize=="mini"?11:13, foreground: PlannerTheme.Muted); placeholder.Margin = new Thickness(36, 0, 5, 0); placeholder.IsHitTestVisible = false; placeholder.Visibility = string.IsNullOrEmpty(input.Text) ? Visibility.Visible : Visibility.Collapsed; host.Children.Add(placeholder);
        input.TextChanged += (_, _) => { _manageQuery = input.Text; placeholder.Visibility = string.IsNullOrEmpty(input.Text) ? Visibility.Visible : Visibility.Collapsed; RenderGroupCards(); };
        return input;
    }

    private void RenderGroups()
    {
        _body.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        bool stackedHeader=Width<980;Grid page = new() { Margin = new Thickness(_pageSize=="mini"?16:22, 8, _pageSize=="mini"?16:22, 20) };
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid titleRow = new() { Margin = new Thickness(2, 4, 2, 12) };
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });if(!stackedHeader)titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });if(stackedHeader)titleRow.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });if(stackedHeader)titleRow.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        StackPanel title = Row();
        var back = Chip("← 返回日历", () => { _manage = false; _manageQuery = ""; Render(); }); back.Name = "BackToCalendar"; back.Width = _pageSize=="mini"?94:106; back.Height = _pageSize=="mini"?34:38; back.FontSize = _pageSize=="mini"?12:13; back.Margin = new Thickness(0, 0, 12, 0); title.Children.Add(back);
        title.Children.Add(new Border { Width = 1, Height = 24, Background = PlannerTheme.Line, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center });
        title.Children.Add(Text("管理日程", _pageSize=="mini"?17:19, FontWeights.SemiBold));var titleHint=Text("集中查看、查找日程记录", _pageSize=="mini"?11:12, foreground: PlannerTheme.Muted);titleHint.Name="ManageSubtitle";titleHint.Margin=new Thickness(8,0,0,0);title.Children.Add(titleHint); Grid.SetColumn(title, 0); titleRow.Children.Add(title);
        StackPanel right = Row();right.HorizontalAlignment=HorizontalAlignment.Right;right.Margin=stackedHeader?new Thickness(0,8,0,0):new Thickness(0); Grid searchFrame = new() { Name="ManageSearchFrame",Width = _pageSize=="mini"?188:206, Height = _pageSize=="mini"?36:40, Margin = new Thickness(0, 0, 8, 0) }; CreateManageSearch(searchFrame); right.Children.Add(searchFrame);
        Border filterFrame = new() { Height = _pageSize=="mini"?36:40, Width = _pageSize=="mini"?228:252, BorderBrush = PlannerTheme.Line, BorderThickness = new Thickness(1), Background = Brushes.White, CornerRadius = new CornerRadius(9), Padding = new Thickness(3) };
        StackPanel filters = Row(); filters.Children.Add(FilterButton("ManageFilterAll", "全部", _manageFilter == ManageFilter.All, () => { _manageFilter = ManageFilter.All; RenderGroups(); })); filters.Children.Add(FilterButton("ManageFilterActive", "未结束", _manageFilter == ManageFilter.Active, () => { _manageFilter = ManageFilter.Active; RenderGroups(); })); filters.Children.Add(FilterButton("ManageFilterEnded", "已结束", _manageFilter == ManageFilter.Ended, () => { _manageFilter = ManageFilter.Ended; RenderGroups(); })); filterFrame.Child = filters; right.Children.Add(filterFrame); Grid.SetColumn(right, stackedHeader?0:1);Grid.SetRow(right,stackedHeader?1:0); titleRow.Children.Add(right);
        Grid.SetRow(titleRow, 0); page.Children.Add(titleRow);
        _manageCards = new StackPanel { Name = "ManageScheduleCards" }; Grid.SetRow(_manageCards, 1); page.Children.Add(_manageCards); _body.Content = new Border{Child=page,Background=Brushes.White,CornerRadius=new CornerRadius(16)}; RenderGroupCards();
    }

    private void RenderGroupCards()
    {
        if (_manageCards == null) return;
        _manageCards.Children.Clear(); DateTime now = DateTime.Now;
        List<ScheduleProjection> projections = _service.Book.Items.Where(i => i.Calendar).Select(i => Project(i, now)).Where(p => Matches(p, _manageQuery)).ToList();
        IEnumerable<ScheduleProjection> filtered = _manageFilter switch { ManageFilter.Active => projections.Where(p => !p.IsEnded), ManageFilter.Ended => projections.Where(p => p.IsEnded), _ => projections };
        List<ScheduleProjection> sorted = filtered.OrderBy(p => p.IsEnded ? 1 : 0).ThenBy(p => p.IsEnded ? -(p.Last?.Ticks ?? DateTime.MinValue.Ticks) : (p.Next?.Date.Ticks ?? p.Item.Start.Date.Ticks)).ThenBy(p => p.IsEnded ? 0 : p.Item.HasTime ? 0 : 1).ThenBy(p => p.IsEnded ? 0 : p.Next?.TimeOfDay.Ticks ?? 0).ToList();
        if (sorted.Count == 0)
        {
            StackPanel empty = new() { Margin = new Thickness(18, 32, 18, 32), HorizontalAlignment = HorizontalAlignment.Center }; empty.Children.Add(Text(_manageQuery.Length > 0 ? "没有找到匹配的日程" : _manageFilter == ManageFilter.Ended ? "暂无已结束日程" : "暂无未结束日程", 18, FontWeights.SemiBold)); empty.Children.Add(Text(_manageQuery.Length > 0 ? "请尝试标题或备注中的其他关键词。" : "日程创建后会在这里按时间顺序显示。", 14, foreground: PlannerTheme.Muted)); _manageCards.Children.Add(empty); return;
        }
        Grid cards = new() { Name = "ManageScheduleGrid" }; int columns = ActualWidth > 0 && ActualWidth < 760 ? 1 : 2;
        for (int column = 0; column < columns; column++) cards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int row = 0; row < (sorted.Count + columns - 1) / columns; row++) cards.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int index = 0; index < sorted.Count; index++) { Border card = ScheduleCard(sorted[index], now); Grid.SetRow(card, index / columns); Grid.SetColumn(card, index % columns); cards.Children.Add(card); }
        _manageCards.Children.Add(cards);
    }

    private Border ScheduleCard(ScheduleProjection projection, DateTime now)
    {
        ReminderItem item = projection.Item; bool small=_pageSize is "mini" or "standard";Grid layout = new() { MinHeight = small?96:104 };
        for (int row = 0; row < 6; row++) layout.RowDefinitions.Add(new RowDefinition { Height = row == 3 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
        Grid header = new(); header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid name = new(); name.ColumnDefinitions.Add(new(){Width=GridLength.Auto});name.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        var nameText=Text(string.IsNullOrWhiteSpace(item.Title)?"未命名日程":item.Title,small?15:17,FontWeights.SemiBold);nameText.MaxWidth=small?200:240;nameText.TextWrapping=TextWrapping.NoWrap;nameText.TextTrimming=TextTrimming.CharacterEllipsis;name.Children.Add(nameText);
        int total=projection.IsFinite?projection.Occurrences.Count:0;
        var type=new Border{Child=Text(GroupLabel(item,total),small?10:11,FontWeights.SemiBold,PlannerTheme.Accent),Background=PlannerTheme.AccentSoft,CornerRadius=new CornerRadius(12),Padding=new Thickness(6,3,6,3),Margin=new Thickness(5,0,3,0),VerticalAlignment=VerticalAlignment.Center};type.HorizontalAlignment=HorizontalAlignment.Left;Grid.SetColumn(type,1);name.Children.Add(type);header.Children.Add(name);
        Button edit=Action("编辑",()=>{_occurrenceDate=null;Edit(item,true);});edit.Name="EditSchedule"+item.Id.ToString("N");edit.Width=small?52:58;edit.Height=small?30:32;edit.FontSize=small?11:12;edit.Padding=new Thickness(6,3,6,3);edit.Margin=new Thickness(5,0,0,0);Grid.SetColumn(edit,1);header.Children.Add(edit);layout.Children.Add(header);
        TextBlock notes = Text(string.IsNullOrWhiteSpace(item.Notes) ? "" : item.Notes, small?12:13, foreground: PlannerTheme.Muted); notes.MaxHeight = small?34:38; notes.TextTrimming = TextTrimming.CharacterEllipsis; notes.Margin = new Thickness(3, 5, 3, 4); Grid.SetRow(notes, 1); layout.Children.Add(notes);
        if (item.Repeat == ReminderRepeat.Once)
        {
            DateTime day = projection.Occurrences.Count > 0 ? projection.Occurrences[0] : item.Start.Date; StackPanel dateLine = Row(); dateLine.Children.Add(PlannerTheme.Icon("calendar", small?16:18, PlannerTheme.Muted, 6)); dateLine.Children.Add(Text(FormatOccurrence(item, day, now), small?12:13, FontWeights.SemiBold)); Grid.SetRow(dateLine, 2); layout.Children.Add(dateLine);
        }
        else
        {
            bool endedRange = projection.IsEnded && projection.IsFinite && projection.Occurrences.Count > 0;
            StackPanel nextLine = Row(); nextLine.Children.Add(PlannerTheme.Icon(endedRange ? "calendar" : "clock", small?16:18, PlannerTheme.Muted, 6));
            if (endedRange) nextLine.Children.Add(Text(FormatRange(projection.Occurrences), small?12:13, FontWeights.SemiBold));
            else if (projection.Next is DateTime next) nextLine.Children.Add(Text(FormatOccurrence(item, next.Date, now, true), small?12:13, FontWeights.SemiBold));
            else if (projection.Last is DateTime last) nextLine.Children.Add(Text(FormatLast(item, last.Date, now), small?12:13, FontWeights.SemiBold));
            else nextLine.Children.Add(Text("暂无后续安排", small?12:13, FontWeights.SemiBold));
            Grid.SetRow(nextLine, 2); layout.Children.Add(nextLine);
            if (projection.IsFinite && projection.FutureCount > 0)
            {
                WrapPanel chips = new() { Margin = new Thickness(1, 2, 1, 2) }; List<DateTime> future = projection.Occurrences.Where(day => IsFuture(item, day, now)).Take(7).ToList();
                int chipYear=now.Year;
                foreach (DateTime day in future) { string chipLabel=day.Year==chipYear?day.ToString("M/d",CultureInfo.InvariantCulture):day.ToString("yyyy/M/d",CultureInfo.InvariantCulture);chipYear=day.Year;chips.Children.Add(new Border { Child = Text(chipLabel, small?10:11, foreground: PlannerTheme.Ink), Background = Brushes.White, BorderBrush = PlannerTheme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(6, 3, 6, 3), Margin = new Thickness(0, 2, 4, 2) });}
                int remaining = projection.FutureCount - future.Count; if (remaining > 0) chips.Children.Add(Text($"+{remaining}天", 14, foreground: PlannerTheme.Muted)); Grid.SetRow(chips, 3); layout.Children.Add(chips);
            }
        }
        Border divider = new() { Height = 1, Background = PlannerTheme.Line, Margin = new Thickness(3, 5, 3, 5) }; Grid.SetRow(divider, 4); layout.Children.Add(divider);
        string footer = item.Repeat == ReminderRepeat.Once ? $"状态：{SingleStatus(projection, now)}" : projection.IsFinite ? MultiSummary(projection) : projection.IsEnded ? "状态：已结束" : "状态：进行中"; TextBlock bottom = Text(footer, small?11:12, foreground: projection.IsEnded ? PlannerTheme.Muted : PlannerTheme.Ink); bottom.Margin = new Thickness(3, 0, 3, 2); Grid.SetRow(bottom, 5); layout.Children.Add(bottom);
        return new Border { Child = layout, Background = Brushes.White, BorderBrush = PlannerTheme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(small?12:15, small?10:12, small?12:15, small?9:11), Margin = new Thickness(4, 5, 4, 5) };
    }
}
