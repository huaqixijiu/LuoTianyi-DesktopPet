using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Automation;

internal static class Program
{
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint process);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr h);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr h, int attribute, out Rect value, int size);
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 2 && args.Length != 3) throw new ArgumentException("Usage: WindowCapture.exe observed-window-handle output.png OR handle --invoke observed-AutomationId");
            var handle = new IntPtr(long.Parse(args[0]));
            GetWindowThreadProcessId(handle, out uint processId);
            using var process = Process.GetProcessById((int)processId);
            if (process.ProcessName != "UiReviewHost") throw new InvalidOperationException("Only isolated UiReviewHost capture is allowed");
            if (args.Length == 3)
            {
                if (args[1] != "--invoke") throw new ArgumentException("Unknown operation");
                var root = AutomationElement.FromHandle(handle);
                var matches = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, args[2]));
                if (matches.Count != 1 || !matches[0].Current.IsEnabled || matches[0].Current.IsOffscreen) throw new InvalidOperationException("Expected exactly one visible enabled target");
                if (!matches[0].TryGetCurrentPattern(InvokePattern.Pattern, out object pattern)) throw new InvalidOperationException("Target does not support InvokePattern");
                ((InvokePattern)pattern).Invoke();
                Console.WriteLine("Invoked isolated UIA control; reobserve state before next action");
                return 0;
            }
            if (GetForegroundWindow() != handle || !IsWindowVisible(handle) || IsIconic(handle)) throw new InvalidOperationException("Target must be visible and foreground");
            SetProcessDPIAware();
            if (DwmGetWindowAttribute(handle, 9, out Rect rect, 16) != 0) throw new InvalidOperationException("Cannot obtain physical window bounds");
            var bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
            if (bounds.Width <= 0 || bounds.Height <= 0 || !System.Windows.Forms.SystemInformation.VirtualScreen.Contains(bounds)) throw new InvalidOperationException("Window is offscreen or has invalid bounds");
            using var bitmap = new Bitmap(bounds.Width, bounds.Height);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
            string output = Path.GetFullPath(args[1]);
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            bitmap.Save(output, ImageFormat.Png);
            File.WriteAllText(output + ".json", System.Text.Json.JsonSerializer.Serialize(new { kind = "foreground desktop crop", width = bounds.Width, height = bounds.Height, utc = DateTime.UtcNow, limitation = "Inspect for occlusion; no occluded or offscreen capture guarantee" }));
            Console.WriteLine("Captured isolated foreground window");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e.Message); return 2; }
    }
}
