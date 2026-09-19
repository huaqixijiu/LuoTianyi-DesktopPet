using System.Windows;
using System.Windows.Input;
using TextBox = System.Windows.Controls.TextBox;

namespace LuoTianyiPet.App;

// Two visible segments, with input routing independent of save-time validation.
internal sealed class PlannerTimeInput
{
    private readonly TextBox _hour, _minute;
    private bool _routing;
    private string? _compactMinutes;
    public PlannerTimeInput(TextBox hour, TextBox minute)
    {
        _hour = hour; _minute = minute;
        foreach (var box in new[] { hour, minute })
        {
            box.MaxLength = 2;
            box.PreviewTextInput += (_, e) => { e.Handled = true; Input(box, e.Text); };
            box.PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Back) { e.Handled = Backspace(box); }
                else if (e.Key is Key.Left or Key.Right or Key.Home or Key.End or Key.Delete) _compactMinutes = null;
                if (e.Key == Key.Space) e.Handled = true;
            };
            box.GotKeyboardFocus += (_, _) => { if (!_routing) { _compactMinutes = null; box.SelectAll(); } };
            box.PreviewMouseLeftButtonDown += (_, _) => _compactMinutes = null;
            System.Windows.DataObject.AddPastingHandler(box, (_, e) =>
            {
                e.CancelCommand();
                if (e.DataObject.GetData(System.Windows.DataFormats.UnicodeText) is string text) Input(box, text);
            });
        }
    }
    private void Focus(TextBox box)
    {
        _routing = true;
        try { box.Focus(); box.CaretIndex = box.Text.Length; box.SelectionLength = 0; }
        finally { _routing = false; }
    }
    internal void Input(TextBox source, string text)
    {
        if (text.Length == 0 || text.Any(c => c < '0' || c > '9')) return;
        foreach (char digit in text)
        {
            if (_compactMinutes != null && _compactMinutes.Length >= 3)
            {
                if (_compactMinutes.Length == 4) continue;
                _compactMinutes += digit;
                _hour.Text = _compactMinutes.Substring(0, 2);
                _minute.Text = _compactMinutes.Substring(2);
                Focus(_hour);
                continue;
            }
            string candidate = source.Text.Remove(source.SelectionStart, source.SelectionLength).Insert(source.SelectionStart, digit.ToString());
            if (source == _hour)
            {
                if (candidate.Length <= 2) { _hour.Text = candidate; _hour.CaretIndex = candidate.Length; }
                else { _hour.Text = candidate.Substring(0, 2); _minute.Text = candidate.Substring(2, 1); Focus(_minute); source = _minute; }
            }
            else
            {
                if (candidate.Length <= 2) { _minute.Text = candidate; _minute.CaretIndex = candidate.Length; }
                else
                {
                    _compactMinutes = candidate;
                    _hour.Text = candidate.Substring(0, 1);
                    _minute.Text = candidate.Substring(1, 2);
                    Focus(_hour);
                }
            }
        }
    }
    internal bool Backspace(TextBox source)
    {
        _compactMinutes = null;
        if (source == _minute && source.SelectionLength == 0 && source.CaretIndex == 0)
        {
            if (_hour.Text.Length > 0) _hour.Text = _hour.Text.Substring(0, _hour.Text.Length - 1);
            Focus(_hour); return true;
        }
        return false;
    }
}
