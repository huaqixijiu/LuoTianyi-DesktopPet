using System.IO;
using System.Windows;
using System.Windows.Threading;
using LuoTianyiPet.Animation;
using LuoTianyiPet.Core;
using LuoTianyiPet.Platform.Windows;

namespace LuoTianyiPet.App;

public partial class App : Application
{
    internal LocalAppPaths ReminderPaths { get; private set; } = new();

    private SingleInstanceGuard? _singleInstance;
    private IAppLogger? _logger;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        StartupOptions options = StartupOptions.Parse(e.Args);

        if (options.IsNotificationSettingsQa)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Shutdown(await SettingsWindow.RunNotificationSettingsQaAsync());
            return;
        }

        if (options.UsesSoftwareRendering)
        {
            System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        }

        _singleInstance = SingleInstanceGuard.Acquire(options.InstanceId);
        if (!_singleInstance.IsPrimaryInstance)
        {
            Shutdown();
            return;
        }

        LocalAppPaths paths = options.IsPortable
            ? LocalAppPaths.CreatePortable(AppContext.BaseDirectory)
            : new LocalAppPaths();
        if (options.IsMusicSettingsFeedbackQa)
        {
            paths = new LocalAppPaths(Path.Combine(
                AppContext.BaseDirectory,
                "MusicSettingsFeedbackQa",
                "UserData"));
        }

        ReminderPaths = paths;
        _logger = new FileAppLogger(paths);
        JsonSettingsStore settingsStore = new(paths);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        try
        {
            _logger.Info("app.storage_mode", options.IsPortable ? "Portable." : "Installed.");
            AppSettings settings = await settingsStore.LoadAsync();
            settings = options.ApplySettings(settings);
            AnimationCatalog? animationCatalog = options.SimulateMissingAssets
                ? null
                : ApplicationCompositionRoot.LoadAnimationCatalog(_logger);
            if (options.SimulateMissingAssets)
            {
                _logger.Info("animation.catalog_qa_missing", "QA fallback mode enabled.");
            }

            if (options.PreviewMediaControls || options.PreviewTrackInfo || options.PreviewLiveTrackInfo)
            {
                settings = settings with { Media = settings.Media with { ShowMusicIslands = true } };
            }

            ApplicationCompositionRoot compositionRoot = new(options, _logger);
            MainWindow window = compositionRoot.CreateMainWindow(
                settings,
                settingsStore,
                animationCatalog);
            MainWindow = window;
            window.Show();
            _logger.Info("app.started", "Runtime animation window started.");
        }
        catch (Exception exception)
        {
            _logger.Error("app.start_failed", exception);
            MessageBox.Show(
                "洛天依桌宠启动失败，诊断信息已保存到本地日志。",
                "洛天依桌宠",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.Info("app.exited", $"Exit code: {e.ApplicationExitCode}.");
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.Error("app.dispatcher_unhandled", e.Exception);
        e.Handled = true;
        MessageBox.Show(
            "桌宠遇到未处理错误并将安全退出。",
            "洛天依桌宠",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        Shutdown(-1);
    }

    private void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            _logger?.Error("app.domain_unhandled", exception);
        }
    }
}
