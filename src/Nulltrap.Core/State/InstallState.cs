using System.Text.Json.Serialization;

using Nulltrap.Core.Deployment;

namespace Nulltrap.Core.State;

public sealed record InstalledClient
{
    [JsonPropertyName("versionGuid")]
    public required string VersionGuid { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("channel")]
    public string Channel { get; init; } = DeploymentChannel.DefaultName;

    [JsonPropertyName("installedAt")]
    public DateTimeOffset InstalledAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class InstallState
{
    [JsonPropertyName("clients")]
    public Dictionary<string, InstalledClient> Clients { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    public InstalledClient? Get(BinaryType binaryType) =>
        Clients.GetValueOrDefault(binaryType.ToApiName());

    public void Set(BinaryType binaryType, InstalledClient client) =>
        Clients[binaryType.ToApiName()] = client;

    public void Remove(BinaryType binaryType) =>
        Clients.Remove(binaryType.ToApiName());
}
