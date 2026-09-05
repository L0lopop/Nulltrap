using System.Text.Json.Serialization;

using Nulltrap.Core.Deployment;
using Nulltrap.Core.Maintenance;
using Nulltrap.Core.Presence;

namespace Nulltrap.Core.Settings;

public sealed record NulltrapSettings
{
    [JsonPropertyName("setupCompleted")]
    public bool SetupCompleted { get; set; }

    [JsonPropertyName("language")]
    public string Language { get; set; } = Localization.Strings.Fallback;

    [JsonPropertyName("theme")]
    public AppTheme Theme { get; set; } = AppTheme.Nulltrap;

    [JsonPropertyName("channel")]
    public string Channel { get; set; } = DeploymentChannel.DefaultName;

    [JsonPropertyName("registerStudio")]
    public bool RegisterStudio { get; set; }

    [JsonPropertyName("desktopShortcut")]
    public bool DesktopShortcut { get; set; } = true;

    [JsonPropertyName("startMenuShortcut")]
    public bool StartMenuShortcut { get; set; } = true;

    [JsonPropertyName("keepDownloadCache")]
    public bool KeepDownloadCache { get; set; } = true;

    [JsonPropertyName("closeAfterLaunch")]
    public bool CloseAfterLaunch { get; set; } = true;

    [JsonPropertyName("stayInTray")]
    public bool StayInTray { get; set; } = true;

    [JsonPropertyName("runAtStartup")]
    public bool RunAtStartup { get; set; }

    [JsonPropertyName("closeRobloxOnLeave")]
    public bool CloseRobloxOnLeave { get; set; }

    [JsonPropertyName("confirmMultipleInstances")]
    public bool ConfirmMultipleInstances { get; set; } = true;

    [JsonPropertyName("monitoring")]
    public bool Monitoring { get; set; } = true;

    [JsonPropertyName("serverNotice")]
    public bool ServerNotice { get; set; }

    [JsonPropertyName("discordPresence")]
    public bool DiscordPresence { get; set; } = true;

    [JsonPropertyName("discordHeadline")]
    public PresenceHeadline DiscordHeadline { get; set; } = PresenceHeadline.GameName;

    [JsonPropertyName("discordSubline")]
    public PresenceSubline DiscordSubline { get; set; } = PresenceSubline.Creator;

    [JsonPropertyName("discordShowElapsed")]
    public bool DiscordShowElapsed { get; set; } = true;

    [JsonPropertyName("discordShowGameIcon")]
    public bool DiscordShowGameIcon { get; set; } = true;

    [JsonPropertyName("discordShowGameButton")]
    public bool DiscordShowGameButton { get; set; } = true;

    [JsonPropertyName("discordShowAccount")]
    public bool DiscordShowAccount { get; set; } = true;

    [JsonPropertyName("discordAllowJoin")]
    public bool DiscordAllowJoin { get; set; } = true;

    [JsonPropertyName("discordApplicationId")]
    public string DiscordApplicationId { get; set; } = string.Empty;

    [JsonPropertyName("cacheSweep")]
    public CacheSweep CacheSweep { get; set; } = CacheSweep.Never;

    [JsonPropertyName("lastCacheSweep")]
    public DateTimeOffset? LastCacheSweep { get; set; }

    [JsonPropertyName("trimMemory")]
    public bool TrimMemory { get; set; } = true;

    [JsonPropertyName("enabledPlugins")]
    public List<string> EnabledPlugins { get; init; } = [];

    [JsonPropertyName("mods")]
    public bool Mods { get; set; } = true;

    [JsonPropertyName("automaticClientUpdates")]
    public bool AutomaticClientUpdates { get; set; } = true;

    [JsonPropertyName("updateNotice")]
    public bool UpdateNotice { get; set; } = true;

    [JsonPropertyName("lastUpdateCheck")]
    public DateTimeOffset? LastUpdateCheck { get; set; }

    [JsonIgnore]
    public PresenceOptions PresenceOptions => new()
    {
        Headline = DiscordHeadline,
        Subline = DiscordSubline,
        ShowElapsed = DiscordShowElapsed,
        ShowGameIcon = DiscordShowGameIcon,
        ShowGameButton = DiscordShowGameButton,
        ShowAccount = DiscordShowAccount,
        AllowJoin = DiscordAllowJoin,
    };

    [JsonIgnore]
    public DeploymentChannel DeploymentChannel =>
        string.IsNullOrWhiteSpace(Channel) ? Deployment.DeploymentChannel.Default : new DeploymentChannel(Channel);
}
