using System.Text.Json.Serialization;

using Nulltrap.Core.Deployment;

namespace Nulltrap.Core.Settings;

public sealed record NulltrapSettings
{
    [JsonPropertyName("setupCompleted")]
    public bool SetupCompleted { get; set; }

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

    public DeploymentChannel DeploymentChannel =>
        string.IsNullOrWhiteSpace(Channel) ? Deployment.DeploymentChannel.Default : new DeploymentChannel(Channel);
}
