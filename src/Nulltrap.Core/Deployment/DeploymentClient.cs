using System.Net;
using System.Text.Json;

using Nulltrap.Core.Packages;

namespace Nulltrap.Core.Deployment;

public sealed class DeploymentClient
{
    private const string VersionStudioMarker = "version-012732894899482c";

    private static readonly Uri PrimaryClientSettings =
        new("https://clientsettingscdn.roblox.com");

    private static readonly Uri FallbackClientSettings =
        new("https://clientsettings.roblox.com");

    private static readonly HttpStatusCode[] UnknownChannelCodes =
    [
        HttpStatusCode.Unauthorized,
        HttpStatusCode.Forbidden,
        HttpStatusCode.NotFound,
    ];

    private readonly HttpClient _http;
    private readonly IReadOnlyList<DeploymentMirror> _mirrors;
    private readonly TimeSpan _priorityStagger;

    public DeploymentClient(HttpClient http)
        : this(http, DeploymentMirror.Known, TimeSpan.FromSeconds(1))
    {
    }

    public DeploymentClient(
        HttpClient http,
        IReadOnlyList<DeploymentMirror> mirrors,
        TimeSpan priorityStagger)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(mirrors);

        if (mirrors.Count == 0)
        {
            throw new ArgumentException("At least one mirror is required.", nameof(mirrors));
        }

        _http = http;
        _mirrors = mirrors;
        _priorityStagger = priorityStagger;
    }

    public async Task<DeploymentMirror> ResolveFastestMirrorAsync(
        CancellationToken cancellationToken = default)
    {
        using var raceTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        List<Task<DeploymentMirror>> probes = _mirrors
            .Select(mirror => ProbeAsync(mirror, raceTokenSource.Token))
            .ToList();

        List<Exception> failures = [];

        try
        {
            while (probes.Count > 0)
            {
                Task<DeploymentMirror> finished = await Task.WhenAny(probes)
                    .ConfigureAwait(false);

                probes.Remove(finished);

                if (finished.IsCompletedSuccessfully)
                {
                    return await finished.ConfigureAwait(false);
                }

                if (finished.Exception?.InnerException is { } failure)
                {
                    failures.Add(failure);
                }
            }
        }
        finally
        {
            await raceTokenSource.CancelAsync().ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();

        throw new NoReachableMirrorException(
            "Could not reach any Roblox setup mirror. Check the network connection.",
            failures);
    }

    public async Task<ClientVersion> GetClientVersionAsync(
        BinaryType binaryType,
        DeploymentChannel channel = default,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(channel.Name))
        {
            channel = DeploymentChannel.Default;
        }

        string path = channel.IsDefault
            ? $"/v2/client-version/{binaryType.ToApiName()}"
            : $"/v2/client-version/{binaryType.ToApiName()}/channel/{Uri.EscapeDataString(channel.Name)}";

        try
        {
            return await GetClientVersionFromAsync(PrimaryClientSettings, path, channel, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception primaryFailure) when (primaryFailure is not UnknownChannelException
                                               and not OperationCanceledException)
        {
            try
            {
                return await GetClientVersionFromAsync(FallbackClientSettings, path, channel, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception fallbackFailure) when (fallbackFailure is not UnknownChannelException
                                                   and not OperationCanceledException)
            {
                throw new DeploymentException(
                    "Could not determine the current Roblox client version.",
                    fallbackFailure);
            }
        }
    }

    public async Task<PackageManifest> GetPackageManifestAsync(
        DeploymentMirror mirror,
        string versionGuid,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mirror);
        ArgumentException.ThrowIfNullOrWhiteSpace(versionGuid);

        Uri manifestUri = mirror.GetPackageManifestUri(versionGuid);

        using HttpResponseMessage response = await _http
            .GetAsync(manifestUri, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new DeploymentException(
                $"{mirror} has no package manifest for version {versionGuid}.");
        }

        response.EnsureSuccessStatusCode();

        string content = await response.Content
            .ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        return PackageManifest.Parse(versionGuid, content);
    }

    private async Task<ClientVersion> GetClientVersionFromAsync(
        Uri host,
        string path,
        DeploymentChannel channel,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http
            .GetAsync(new Uri(host, path), cancellationToken)
            .ConfigureAwait(false);

        if (!channel.IsDefault && UnknownChannelCodes.Contains(response.StatusCode))
        {
            throw new UnknownChannelException(channel);
        }

        response.EnsureSuccessStatusCode();

        await using Stream body = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        ClientVersion? version = await JsonSerializer
            .DeserializeAsync<ClientVersion>(body, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (version is null || string.IsNullOrWhiteSpace(version.VersionGuid))
        {
            throw new DeploymentException($"{host.Host} returned no usable version information.");
        }

        return version;
    }

    private async Task<DeploymentMirror> ProbeAsync(
        DeploymentMirror mirror,
        CancellationToken cancellationToken)
    {
        if (mirror.Priority > 0)
        {
            await Task.Delay(_priorityStagger * mirror.Priority, cancellationToken)
                .ConfigureAwait(false);
        }

        var probeUri = new Uri($"{mirror.BaseUri.AbsoluteUri.TrimEnd('/')}/versionStudio");

        using HttpResponseMessage response = await _http
            .GetAsync(probeUri, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        string body = await response.Content
            .ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!string.Equals(body.Trim(), VersionStudioMarker, StringComparison.Ordinal))
        {
            throw new DeploymentException(
                $"{mirror} is not serving Roblox content: expected '{VersionStudioMarker}', got '{Truncate(body)}'.");
        }

        return mirror;
    }

    private static string Truncate(string value)
    {
        string collapsed = value.Trim().ReplaceLineEndings(" ");
        return collapsed.Length <= 64 ? collapsed : collapsed[..64] + "...";
    }
}
