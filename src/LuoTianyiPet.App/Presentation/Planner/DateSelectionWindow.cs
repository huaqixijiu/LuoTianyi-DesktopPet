using System.Windows;
using FontFamily = System.Windows.Media.FontFamily;
using System.Windows.Controls;
using System.Windows.Media;
using LuoTianyiPet.Core;
using Button = System.Windows.Controls.Button;
namespace LuoTianyiPet.App;
internal sealed class DateSelectionWindow : Window
{
    public HashSet<DateTime> Selection { get; }
    private DateTime _month;
    private readonly StackPanel _panel = new() { Margin = new Thickness(16) };
    private readonly Dictionary<DateTime, Button> _dayButtons = [];
    private readonly HashSet<DateTime> _dragVisited = [];
    private TextBlock? _selectionCount;
    private bool _dragging;
    private bool _dragMoved;
    private Point _dragLastPoint;
    private DateTime? _dragLastDay;
    private bool _suppressNextClick;
    public DateSelectionWindow(IEnumerable<DateTime> dates, DateTime month)
    {
        Selection = new(dates.Select(d => d.Date)); _month = new(month.Year, month.Month, 1);
        Title = "选择日期"; Width = 390; FontSize = 14; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        FontFamily = new FontFamily("Microsoft YaHei UI"); Background = Brushes.White; Content = _panel;
        PlannerTheme.Apply(this); Render();
    }
    private Button Make(string text, Action click) { Button b = new() { Content = text, Margin = new Thickness(2), Padding = new Thickness(7) }; b.Click += (_, _) => click(); return b; }
    private void Render()
    {
        _dragging = false;
        _dragMoved = false;
        _dragVisited.Clear();
        _dayButtons.Clear();
        System.Windows.Input.Mouse.Capture(null);
        _panel.Children.Clear();
        DockPanel nav = new(); var prev = Make("‹", () => Move(-1)); var next = Make("›", () => Move(1));
        DockPanel.SetDock(prev, Dock.Left); DockPanel.SetDock(next, Dock.Right); nav.Children.Add(prev); nav.Children.Add(next);
        nav.Children.Add(new TextBlock { Text = _month.ToString("yyyy年M月"), TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 17 }); _panel.Children.Add(nav);
        Grid grid = new(); for (int c = 0; c < 7; c++) grid.ColumnDefinitions.Add(new()); for (int r = 0; r < 7; r++) grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        for (int c = 0; c < 7; c++) { TextBlock t = new() { Text = "一二三四五六日"[c].ToString(), TextAlignment = TextAlignment.Center, Margin = new Thickness(4, 10, 4, 8) }; Grid.SetColumn(t,c); grid.Children.Add(t); }
        DateTime start = _month.AddDays(-((int)_month.DayOfWeek + 6) % 7);
        for (int i = 0; i < 42; i++)
        {
            DateTime day = start.AddDays(i); Button b = Make(day.Day.ToString(), () => { if (_suppressNextClick) { _suppressNextClick = false; return; } Toggle(day); });
            b.Name = "Date" + day.ToString("yyyyMMdd"); b.ToolTip = day.ToString("yyyy年M月d日") + " " + CalendarLabels.FullLunar(day);
            b.IsEnabled = day >= ReminderSchedule.MinimumDate && day <= ReminderSchedule.MaximumDate;
            b.Tag = day;
            b.PreviewMouseLeftButtonDown += (_, e) => BeginDrag(b, grid, day, e);
            b.PreviewMouseMove += (_, e) => ContinueDrag(grid, e);
            b.PreviewMouseLeftButtonUp += (_, e) => EndDrag(e);
            _dayButtons[day] = b;
            Grid.SetColumn(b,i%7); Grid.SetRow(b,i/7+1); grid.Children.Add(b);
        }
        _panel.Children.Add(grid); StackPanel footer = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0,12,0,0) };
        _selectionCount = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4) };
        footer.Children.Add(_selectionCount);
        footer.Children.Add(Make("清空", () => { Selection.Clear(); UpdateSelectionVisuals(); })); footer.Children.Add(Make("取消", () => DialogResult = false)); var ok = Make("确定", () => DialogResult = true); ok.Name = "ConfirmDates"; ok.Background = PlannerTheme.Accent; ok.Foreground = Brushes.White; footer.Children.Add(ok); _panel.Children.Add(footer);
        UpdateSelectionVisuals();
    }
    private void Move(int delta) { DateTime next = _month.AddMonths(delta); if(next >= ReminderSchedule.MinimumDate && next <= ReminderSchedule.MaximumDate) { _month=next; Render(); } }

    private void Toggle(DateTime day)
    {
        if (!Selection.Add(day)) Selection.Remove(day);
        UpdateSelectionVisuals();
    }

    private void BeginDrag(Button button, Grid grid, DateTime day, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton != System.Windows.Input.MouseButton.Left || !button.IsEnabled) return;
        _dragging = true;
        _dragMoved = false;
        _dragVisited.Clear();
        _dragLastPoint = System.Windows.Input.Mouse.GetPosition(grid);
        _dragLastDay = null;
        System.Windows.Input.Mouse.Capture(button, System.Windows.Input.CaptureMode.Element);
    }

    private void ContinueDrag(Grid grid, System.Windows.Input.MouseEventArgs e)
    {
        if (!_dragging || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;
        Point point = System.Windows.Input.Mouse.GetPosition(grid);
        if ((point - _dragLastPoint).Length < 1) return;
        ApplyDragPath(grid, _dragLastPoint, point);
        _dragLastPoint = point;
    }

    private void EndDrag(System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_dragging || e.ChangedButton != System.Windows.Input.MouseButton.Left) return;
        bool moved = _dragMoved;
        _dragging = false;
        _dragMoved = false;
        _dragLastDay = null;
        if (moved)
        {
            System.Windows.Input.Mouse.Capture(null);
            _suppressNextClick = true;
            Dispatcher.BeginInvoke(new Action(() => _suppressNextClick = false), System.Windows.Threading.DispatcherPriority.Input);
            e.Handled = true;
        }
    }

    private void ApplyDragPath(Grid grid, Point from, Point to)
    {
        double distance = (to - from).Length;
        int samples = Math.Max(1, (int)Math.Ceiling(distance / 6));
        for (int i = 0; i <= samples; i++)
        {
            double ratio = i / (double)samples;
            Point point = new(from.X + (to.X - from.X) * ratio, from.Y + (to.Y - from.Y) * ratio);
            if (!TryGetDate(grid, point, out DateTime day)) continue;
            if (_dragLastDay is DateTime previous && previous != day) _dragMoved = true;
            if (_dragLastDay == day) continue;
            _dragLastDay = day;
            if (!_dragVisited.Add(day)) continue;
            if (!Selection.Add(day)) Selection.Remove(day);
        }
        UpdateSelectionVisuals();
    }

    private static bool TryGetDate(Grid grid, Point point, out DateTime day)
    {
        DependencyObject? current = VisualTreeHelper.HitTest(grid, point)?.VisualHit;
        while (current is not null && current != grid)
        {
            if (current is Button button && button.IsEnabled && button.Tag is DateTime value)
            {
                day = value.Date;
                return true;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        day = default;
        return false;
    }

    private void UpdateSelectionVisuals()
    {
        foreach (var pair in _dayButtons)
        {
            bool selected = Selection.Contains(pair.Key);
            pair.Value.Background = selected ? PlannerTheme.Accent : Brushes.Transparent;
            pair.Value.Foreground = selected ? Brushes.White : pair.Key.Month == _month.Month ? Brushes.DarkSlateGray : Brushes.Gray;
        }
        if (_selectionCount is not null) _selectionCount.Text = $"已选 {Selection.Count} 天";
    }
}
