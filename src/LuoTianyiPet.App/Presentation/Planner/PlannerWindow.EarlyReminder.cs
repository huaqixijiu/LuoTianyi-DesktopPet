using System.Windows;
using LuoTianyiPet.Core;
using TextBox = System.Windows.Controls.TextBox;

namespace LuoTianyiPet.App;

internal sealed partial class PlannerWindow
{
    // Both editors use the same field, units, range and save-time validation.
    private static TextBox CreateEarlyMinutesInput(int minutes) => new()
    {
        Name = "EarlyMinutes",
        Text = ReminderSchedule.LimitEarlyMinutes(minutes).ToString(),
        Width = 76,
        Height = 34,
        Padding = new Thickness(8, 5, 8, 5),
        TextAlignment = TextAlignment.Center,
        ToolTip = "提前 1～60 分钟，到点仍会提醒"
    };
}
