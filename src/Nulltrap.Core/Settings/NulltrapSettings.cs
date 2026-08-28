using System.Text.Json.Serialization;

using Nulltrap.Core.Deployment;
using Nulltrap.Core.Presence;

namespace Nulltrap.Core.Settings;

public sealed record NulltrapSettings
{
    [JsonPropertyName("setupCompleted")]
    public bool SetupCompleted { get; set; }

    [JsonPropertyName("language")]
    public string Language { get; set; } = Localization.Strings.Fallback;

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

    [JsonPropertyName("confirmMultipleInstances")]
    public bool ConfirmMultipleInstances { get; set; } = true;

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

    [JsonPropertyName("discordApplicationId")]
    public string DiscordApplicationId { get; set; } = string.Empty;

    [JsonPropertyName("mods")]
    public bool Mods { get; set; } = true;

    [JsonPropertyName("automaticClientUpdates")]
    public bool AutomaticClientUpdates { get; set; } = true;

    [JsonIgnore]
    public PresenceOptions PresenceOptions => new()
    {
        Headline = DiscordHeadline,
        Subline = DiscordSubline,
        ShowElapsed = DiscordShowElapsed,
        ShowGameIcon = DiscordShowGameIcon,
        ShowGameButton = DiscordShowGameButton,
    };

    [JsonIgnore]
    public DeploymentChannel DeploymentChannel =>
        string.IsNullOrWhiteSpace(Channel) ? Deployment.DeploymentChannel.Default : new DeploymentChannel(Channel);
}
