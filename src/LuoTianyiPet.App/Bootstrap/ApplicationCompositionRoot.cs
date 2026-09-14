using System.IO;
using System.Text.Json;
using LuoTianyiPet.Animation;
using LuoTianyiPet.Core;
using LuoTianyiPet.Platform.Windows;

namespace LuoTianyiPet.App;

internal sealed class ApplicationCompositionRoot
{
    private readonly StartupOptions _options;
    private readonly bool _isPortable;
    private readonly IAppLogger _logger;

    public ApplicationCompositionRoot(StartupOptions options, IAppLogger logger)
    {
        _options = options;
        _isPortable = options.IsPortable;
        _logger = logger;
    }

    public MainWindow CreateMainWindow(
        AppSettings settings,
        ISettingsStore settingsStore,
        AnimationCatalog? animationCatalog)
    {
        IAudioSessionProbe? audioSessionProbe = settings.Media.EnableCloudMusicDetection &&
            (!_options.IsPreviewOrQaRun || _options.PreviewLiveCloudMusicControl)
                ? new CoreAudioSessionProbe()
                : null;
        IApplicationVolumeService? applicationVolumeService =
            !_options.IsPreviewOrQaRun || _options.PreviewLiveApplicationVolume
                ? CreateApplicationVolumeService(settings)
                : null;
        Win32ShortcutInputBackend mediaInputBackend = new();
        IMediaCommandSender mediaCommandSender = new WindowsMediaCommandSender(
            mediaInputBackend,
            settings.Media,
            settings.Safety);
        IMediaApplicationLauncher mediaApplicationLauncher = new WindowsMediaApplicationLauncher(
            mediaInputBackend,
            settings.Safety);
        IStartupRegistrationService? startupRegistrationService = !_options.IsPreviewOrQaRun &&
            ApplicationRuntime.ExecutablePath is string executablePath
                ? new WindowsStartupRegistrationService(
                    executablePath,
                    _isPortable,
                    TryGetPackageFamilyName())
                : null;
        IMediaTrackInfoSource? mediaTrackInfoSource =
            !_options.IsPreviewOrQaRun || _options.PreviewLiveTrackInfo || _options.PreviewLiveCloudMusicControl
                ? new SystemMediaTrackInfoSource()
                : null;
        ISystemResumeSource? systemResumeSource = !_options.IsPreviewOrQaRun
            ? new WindowsSystemResumeSource()
            : null;
        string[] genshinProcessNames = TextParsing.SplitAndTrim(
            settings.Genshin.ProcessNames ?? string.Empty,
            ';');
        IProtectedGameProcessMonitor? protectedGameMonitor =
            settings.Genshin.EnableIntegration && !_options.IsPreviewOrQaRun && genshinProcessNames.Length > 0
                ? new PollingProtectedGameProcessMonitor(
                    genshinProcessNames,
                    TimeSpan.FromMilliseconds(
                        settings.Genshin.StatusPollIntervalMilliseconds > 0
                            ? settings.Genshin.StatusPollIntervalMilliseconds
                            : GenshinPreferences.DefaultStatusPollIntervalMilliseconds))
                : null;
        IForegroundApplicationProbe? foregroundApplicationProbe = !_options.IsPreviewOrQaRun
            ? new WindowsForegroundApplicationProbe()
            : null;
        MessageProviderMatcher messageProviderMatcher = new(settings.Notifications);
        IMessageNotificationSource? messageNotificationSource = !_options.IsPreviewOrQaRun || _options.PreviewSettings
            ? new WindowsMessageNotificationSource(messageProviderMatcher)
            : null;
        IDesktopItemDisappearanceSource? desktopItemDisappearanceSource = !_options.IsPreviewOrQaRun
            ? new WindowsDesktopItemDisappearanceSource()
            : null;

        return new MainWindow(
            settings,
            settingsStore,
            _logger,
            animationCatalog,
            audioSessionProbe,
            applicationVolumeService,
            mediaCommandSender,
            mediaApplicationLauncher,
            startupRegistrationService,
            mediaTrackInfoSource,
            new WindowsUserIdleTimeSource(),
            systemResumeSource,
            protectedGameMonitor,
            foregroundApplicationProbe,
            messageNotificationSource,
            new WindowsRecycleBinService(),
            desktopItemDisappearanceSource,
            new WindowsWindowWorkAreaProvider(),
            _options.InitialVisualState,
            _options.PreviewExit,
            _options.PreviewMusicTransition,
            _options.PreviewBodyHitDebug,
            _options.PreviewDragCycle,
            _options.PreviewMediaControls,
            _options.PreviewLiveCloudMusicControl,
            _options.PreviewTrackInfo,
            _options.PreviewLiveTrackInfo,
            _options.PreviewSettings,
            _options.PreviewTray,
            _options.PreviewSystemResume,
            _options.PreviewLongIdle,
            _options.PreviewLongIdleDecoration,
            _options.PreviewLongIdleRightEdge,
            _options.PreviewGenshinLaunch,
            _options.PreviewGenshinCameo,
            _options.PreviewBunChase,
            _options.PreviewMessageNotification,
            _options.PreviewEdgeDock,
            _options.PreviewBottomControlsLayout,
            _options.PreviewTopControlsLayout,
            _options.PreviewBodyReaction,
            _options.PreviewFeedback,
            _options.ShowQaTaskbar,
            _options.PersistSettings);
    }

    public static AnimationCatalog? LoadAnimationCatalog(IAppLogger logger)
    {
        string assetsRoot = RuntimeAssetLocator.AssetsRoot;
        string catalogPath = RuntimeAssetLocator.AnimationCatalogPath;
        try
        {
            AnimationCatalog catalog = AnimationCatalog.Load(assetsRoot, catalogPath);
            logger.Info("animation.catalog_loaded", $"Loaded {catalog.Assets.Count} animations.");
            return catalog;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            logger.Error("animation.catalog_failed", exception);
            return null;
        }
    }

    private IApplicationVolumeService? CreateApplicationVolumeService(AppSettings settings)
    {
        try
        {
            return new CoreAudioApplicationVolumeService(
                settings.Media.TargetProcessName,
                settings.Safety);
        }
        catch (Exception exception) when (
            exception is System.Runtime.InteropServices.COMException or
            InvalidOperationException or ArgumentException or UnauthorizedAccessException)
        {
            _logger.Error("volume.application_session_initialization_failed", exception);
            return null;
        }
    }

    private static string? TryGetPackageFamilyName()
    {
        try
        {
            return global::Windows.ApplicationModel.Package.Current.Id.FamilyName;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }
}
