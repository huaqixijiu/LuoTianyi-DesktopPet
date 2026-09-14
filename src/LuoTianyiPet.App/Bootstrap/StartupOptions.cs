using System.IO;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.App;

internal sealed class StartupOptions
{
    private StartupOptions(IReadOnlyCollection<string> arguments)
    {
        Arguments = arguments;
        IsNotificationSettingsQa = arguments.Contains("--qa-notification-settings", StringComparer.OrdinalIgnoreCase);
        IsPreviewOrQaRun = arguments.Any(argument =>
            argument.StartsWith("--preview-", StringComparison.OrdinalIgnoreCase) ||
            argument.StartsWith("--qa-", StringComparison.OrdinalIgnoreCase));
        IsPortable = arguments.Contains("--portable", StringComparer.OrdinalIgnoreCase) ||
            File.Exists(Path.Combine(AppContext.BaseDirectory, "LUOTIANYI_PET_PORTABLE.marker")) ||
            File.Exists(Path.Combine(AppContext.BaseDirectory, "PORTABLE_TEST_PACKAGE.marker"));
        IsMusicSettingsFeedbackQa = arguments.Contains(
            "--qa-music-settings-feedback",
            StringComparer.OrdinalIgnoreCase);
        InstanceId = BuildInstanceId(arguments, IsPreviewOrQaRun);
        UsesSoftwareRendering = UsesSoftwareRenderingFor(arguments);
        SimulateMissingAssets = arguments.Contains("--qa-missing-assets", StringComparer.OrdinalIgnoreCase);
        InitialVisualState = GetInitialVisualState(arguments);
        PreviewExit = arguments.Contains("--preview-exit", StringComparer.OrdinalIgnoreCase);
        PreviewMusicTransition = arguments.Contains("--preview-music-transition", StringComparer.OrdinalIgnoreCase);
        PreviewBodyHitDebug = arguments.Contains("--preview-body-hit-debug", StringComparer.OrdinalIgnoreCase);
        PreviewDragCycle = arguments.Contains("--preview-drag-cycle", StringComparer.OrdinalIgnoreCase);
        PreviewBottomControlsLayout = arguments.Contains("--qa-bottom-controls", StringComparer.OrdinalIgnoreCase);
        PreviewTopControlsLayout = arguments.Contains("--qa-top-controls", StringComparer.OrdinalIgnoreCase);
        PreviewMediaControls = arguments.Contains("--qa-media-controls", StringComparer.OrdinalIgnoreCase) ||
            arguments.Contains("--qa-shortcut-menu", StringComparer.OrdinalIgnoreCase) ||
            PreviewBottomControlsLayout || PreviewTopControlsLayout;
        PreviewTrackInfo = arguments.Contains("--qa-track-info", StringComparer.OrdinalIgnoreCase) ||
            PreviewBottomControlsLayout || PreviewTopControlsLayout;
        PreviewLiveTrackInfo = arguments.Contains("--qa-track-info-live", StringComparer.OrdinalIgnoreCase);
        PreviewLiveApplicationVolume = arguments.Contains(
            "--qa-application-volume",
            StringComparer.OrdinalIgnoreCase);
        PreviewLiveCloudMusicControl = arguments.Contains(
            "--qa-cloudmusic-control-live",
            StringComparer.OrdinalIgnoreCase);
        PreviewMediaControls |= PreviewLiveCloudMusicControl;
        PreviewSettings = arguments.Contains("--qa-settings", StringComparer.OrdinalIgnoreCase);
        PreviewTray = arguments.Contains("--qa-tray", StringComparer.OrdinalIgnoreCase);
        PreviewSystemResume = arguments.Contains("--qa-system-resume", StringComparer.OrdinalIgnoreCase);
        PreviewLongIdle = CrystalLongIdlePreviewModeParser.Parse(arguments);
        PreviewLongIdleDecoration = CrystalSleepDecorationPreviewParser.Parse(arguments);
        PreviewLongIdleRightEdge = arguments.Contains("--qa-long-idle-right-edge", StringComparer.OrdinalIgnoreCase);
        PreviewGenshinLaunch = arguments.Contains("--qa-genshin-launch", StringComparer.OrdinalIgnoreCase);
        PreviewGenshinCameo = arguments.Contains("--qa-genshin-cameo", StringComparer.OrdinalIgnoreCase);
        PreviewBunChase = arguments.Contains("--qa-bun-chase", StringComparer.OrdinalIgnoreCase);
        PreviewMessageNotification = ParseMessageNotification(arguments);
        PreviewEdgeDock = ParseEdgeDock(arguments);
        PreviewBodyReaction = GetArgumentValue(arguments, "--preview-body-reaction=");
        PreviewFeedback = GetArgumentValue(arguments, "--qa-feedback=");
        ShowQaTaskbar = arguments.Contains("--qa-window", StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> Arguments { get; }
    public bool IsNotificationSettingsQa { get; }
    public bool IsPreviewOrQaRun { get; }
    public bool IsPortable { get; }
    public bool IsMusicSettingsFeedbackQa { get; }
    public string InstanceId { get; }
    public bool UsesSoftwareRendering { get; }
    public bool SimulateMissingAssets { get; }
    public PetVisualState InitialVisualState { get; }
    public bool PreviewExit { get; }
    public bool PreviewMusicTransition { get; }
    public bool PreviewBodyHitDebug { get; }
    public bool PreviewDragCycle { get; }
    public bool PreviewMediaControls { get; }
    public bool PreviewLiveCloudMusicControl { get; }
    public bool PreviewLiveApplicationVolume { get; }
    public bool PreviewTrackInfo { get; }
    public bool PreviewLiveTrackInfo { get; }
    public bool PreviewSettings { get; }
    public bool PreviewTray { get; }
    public bool PreviewSystemResume { get; }
    public CrystalLongIdlePreviewMode PreviewLongIdle { get; }
    public CrystalSleepDecoration? PreviewLongIdleDecoration { get; }
    public bool PreviewLongIdleRightEdge { get; }
    public bool PreviewGenshinLaunch { get; }
    public bool PreviewGenshinCameo { get; }
    public bool PreviewBunChase { get; }
    public MessageProvider? PreviewMessageNotification { get; }
    public EdgeDockSide? PreviewEdgeDock { get; }
    public bool PreviewBottomControlsLayout { get; }
    public bool PreviewTopControlsLayout { get; }
    public string? PreviewBodyReaction { get; }
    public string? PreviewFeedback { get; }
    public bool ShowQaTaskbar { get; }
    public bool PersistSettings => !IsPreviewOrQaRun;

    public static StartupOptions Parse(IReadOnlyCollection<string> arguments) => new(arguments);

    public AppSettings ApplySettings(AppSettings settings)
    {
        string? style = GetArgumentValue(Arguments, "--qa-full-body-style=");
        if (!IsPreviewOrQaRun || style is not (
                AppearanceOptionIds.FullBodyLongHair or
                AppearanceOptionIds.FullBodyCrystalDress or
                AppearanceOptionIds.FullBodyClassicCatEars))
        {
            return settings;
        }

        return settings with
        {
            Appearance = settings.Appearance with { FullBodyStyle = style },
        };
    }

    private static string BuildInstanceId(IReadOnlyCollection<string> arguments, bool isPreviewOrQaRun)
    {
        const string applicationId = "LuoTianyiPet.App";
        string instanceId = isPreviewOrQaRun ? $"{applicationId}.QA" : applicationId;
        if (arguments.Contains("--qa-planner", StringComparer.OrdinalIgnoreCase)) instanceId += ".Planner";
        if (arguments.Contains("--qa-preserve-drag", StringComparer.OrdinalIgnoreCase)) instanceId += ".PreserveDrag";
        if (arguments.Contains("--qa-recycle-direction", StringComparer.OrdinalIgnoreCase)) instanceId += ".RecycleDirection";
        if (arguments.Contains("--qa-animation-edges", StringComparer.OrdinalIgnoreCase)) instanceId += ".AnimationEdges";
        if (arguments.Contains("--qa-fishing", StringComparer.OrdinalIgnoreCase)) instanceId = $"{applicationId}.QA.Fishing";
        if (arguments.Contains("--qa-drag-edges", StringComparer.OrdinalIgnoreCase)) instanceId = $"{applicationId}.QA.DragEdges";
        if (arguments.Contains("--qa-top-drag", StringComparer.OrdinalIgnoreCase)) instanceId = $"{applicationId}.QA.TopDrag";
        if (arguments.Contains("--qa-quick-actions", StringComparer.OrdinalIgnoreCase)) instanceId = $"{applicationId}.QA.QuickActions";
        if (arguments.Contains("--qa-stable-layout", StringComparer.OrdinalIgnoreCase)) instanceId = $"{applicationId}.QA.StableLayout";
        if (arguments.Contains("--qa-message-details", StringComparer.OrdinalIgnoreCase)) instanceId += ".MessageDetails";
        if (arguments.Contains("--qa-afternoon-greeting", StringComparer.OrdinalIgnoreCase)) instanceId += ".AfternoonGreeting";
        if (arguments.Contains("--qa-music-settings-feedback", StringComparer.OrdinalIgnoreCase)) instanceId += ".MusicSettingsFeedback";
        return instanceId;
    }

    private static bool UsesSoftwareRenderingFor(IReadOnlyCollection<string> arguments) =>
        arguments.Contains("--qa-recycle-direction", StringComparer.OrdinalIgnoreCase) ||
        arguments.Contains("--qa-animation-edges", StringComparer.OrdinalIgnoreCase) ||
        arguments.Contains("--qa-fishing", StringComparer.OrdinalIgnoreCase) ||
        arguments.Contains("--qa-stable-layout", StringComparer.OrdinalIgnoreCase) ||
        arguments.Contains("--qa-music-settings-feedback", StringComparer.OrdinalIgnoreCase);

    private static MessageProvider? ParseMessageNotification(IReadOnlyCollection<string> arguments)
    {
        return GetArgumentValue(arguments, "--qa-message-notification=")?.ToLowerInvariant() switch
        {
            "qq" => MessageProvider.Qq,
            "wechat" or "weixin" => MessageProvider.WeChat,
            _ => null,
        };
    }

    private static EdgeDockSide? ParseEdgeDock(IReadOnlyCollection<string> arguments)
    {
        return GetArgumentValue(arguments, "--qa-edge-dock=")?.ToLowerInvariant() switch
        {
            "left" => EdgeDockSide.Left,
            "right" => EdgeDockSide.Right,
            "bottom" => EdgeDockSide.Bottom,
            _ => null,
        };
    }

    private static string? GetArgumentValue(IEnumerable<string> arguments, string prefix) =>
        arguments.FirstOrDefault(argument => argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?
            .Split(new[] { '=' }, 2)[1];

    private static PetVisualState GetInitialVisualState(IReadOnlyCollection<string> arguments)
    {
        PetContinuousState continuousState = arguments.Contains(
            "--preview-music",
            StringComparer.OrdinalIgnoreCase)
                ? PetContinuousState.MusicPlaying
                : PetContinuousState.Idle;
        return new PetVisualState(PetDisplayMode.FullBodyInteractive, continuousState);
    }
}
