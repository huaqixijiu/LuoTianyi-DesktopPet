using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LuoTianyiPet.Core;
using LuoTianyiPet.Platform.Windows;

// Developer-only host: the real PlannerWindow and service, without pet startup,
// external integrations or the real user's settings. No production source changes.
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Expected a NEW evidence directory");
        string output = Path.GetFullPath(args[0]);
        if (Directory.Exists(output)) throw new IOException("Use a fresh evidence directory");
        Directory.CreateDirectory(output);
        var app = new Application();
        app.Startup += async (_, _) =>
        {
            try
            {
                Assembly assembly = typeof(LuoTianyiPet.App.App).Assembly;
                Type serviceType = assembly.GetType("LuoTianyiPet.App.ReminderService", true)!;
                object service = Activator.CreateInstance(serviceType, new LocalAppPaths(Path.Combine(output, "UserData")))!;
                await (Task)serviceType.GetMethod("LoadAsync")!.Invoke(service, null)!;
                Action<ReminderBook> seed = book =>
                {
                    book.Preferences.Sound = false;
                    book.Preferences.Animation = false;
                    book.Items.Add(new ReminderItem { Calendar = true, Title = "视觉验收示例", Notes = "仅隔离测试数据", Start = DateTime.Today.AddDays(1).AddHours(10), Enabled = false });
                };
                await (Task)serviceType.GetMethod("ChangeAsync")!.Invoke(service, new object[] { seed })!;
                var window = (Window)Activator.CreateInstance(assembly.GetType("LuoTianyiPet.App.PlannerWindow", true)!, service, false)!;
                int capture = 0;
                void Capture()
                {
                    window.UpdateLayout();
                    string stem = Path.Combine(output, $"render-{++capture:000}");
                    var dpi = VisualTreeHelper.GetDpi(window);
                    var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(window);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using (var stream = File.Create(stem + ".png")) encoder.Save(stream);
                    File.WriteAllText(stem + ".json", System.Text.Json.JsonSerializer.Serialize(new { kind = "WPF RenderTargetBitmap; not desktop capture", widthDip = window.ActualWidth, heightDip = window.ActualHeight, displayDpiX = dpi.PixelsPerInchX, displayDpiY = dpi.PixelsPerInchY, capturedUtc = DateTime.UtcNow }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                }
                window.ContentRendered += (_, _) => Capture();
                window.PreviewKeyDown += (_, e) => { if (e.Key == Key.F8) { Capture(); e.Handled = true; } };
                window.Closed += (_, _) => ((IDisposable)service).Dispose();
                window.Show();
                File.WriteAllText(Path.Combine(output, "ready.txt"), "Real PlannerWindow ready; F8 captures rendered surface; close window to exit.");
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(output, "FAILED.txt"), ex.ToString()); app.Shutdown(1); }
        };
        app.Run();
    }
}
