using System.Text.Json.Serialization;

namespace Nulltrap.Core.Deployment;

public sealed record ClientVersion
{
    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("clientVersionUpload")]
    public required string VersionGuid { get; init; }

    [JsonPropertyName("bootstrapperVersion")]
    public string? BootstrapperVersion { get; init; }

    [JsonPropertyName("nextClientVersionUpload")]
    public string? NextVersionGuid { get; init; }

    [JsonPropertyName("nextClientVersion")]
    public string? NextVersion { get; init; }
}
