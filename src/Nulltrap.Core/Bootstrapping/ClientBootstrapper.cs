using Nulltrap.Core.Deployment;
using Nulltrap.Core.Packages;
using Nulltrap.Core.State;
using Nulltrap.Platform.Abstractions;

namespace Nulltrap.Core.Bootstrapping;

public sealed record BootstrapResult(
    BinaryType BinaryType,
    string Version,
    string VersionGuid,
    string VersionDirectory,
    string ExecutablePath,
    bool WasAlreadyInstalled);

public sealed class ClientBootstrapper
{
    private readonly DeploymentClient _deployment;
    private readonly PackageDownloader _downloader;
    private readonly IApplicationPaths _paths;
    private readonly InstallStateStore _state;

    public ClientBootstrapper(
        DeploymentClient deployment,
        PackageDownloader downloader,
        IApplicationPaths paths,
        InstallStateStore state)
    {
        ArgumentNullException.ThrowIfNull(deployment);
        ArgumentNullException.ThrowIfNull(downloader);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(state);

        _deployment = deployment;
        _downloader = downloader;
        _paths = paths;
        _state = state;
    }

    public async Task<BootstrapResult> EnsureUpToDateAsync(
        BinaryType binaryType,
        DeploymentChannel channel = default,
        IProgress<BootstrapProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();

        progress?.Report(BootstrapProgress.For(BootstrapStage.Connecting, "Contacting Roblox"));
        DeploymentMirror mirror = await _deployment.ResolveFastestMirrorAsync(cancellationToken)
            .ConfigureAwait(false);

        progress?.Report(BootstrapProgress.For(BootstrapStage.CheckingVersion, "Checking for updates"));
        ClientVersion version = await _deployment
            .GetClientVersionAsync(binaryType, channel, cancellationToken)
            .ConfigureAwait(false);

        string versionDirectory = Path.Combine(_paths.Versions, version.VersionGuid);
        string executablePath = Path.Combine(versionDirectory, binaryType.ToExecutableName());

        InstallState state = _state.Load();
        InstalledClient? installed = state.Get(binaryType);

        if (installed?.VersionGuid == version.VersionGuid && File.Exists(executablePath))
        {
            progress?.Report(BootstrapProgress.For(BootstrapStage.Ready, "Up to date"));

            return new BootstrapResult(
                binaryType, version.Version, version.VersionGuid,
                versionDirectory, executablePath, WasAlreadyInstalled: true);
        }

        PackageManifest manifest = await _deployment
            .GetPackageManifestAsync(mirror, version.VersionGuid, cancellationToken)
            .ConfigureAwait(false);

        var downloadProgress = new Progress<PackageDownloadProgress>(report =>
            progress?.Report(BootstrapProgress.Within(
                BootstrapStage.Downloading,
                $"Downloading Roblox {version.Version}",
                report.Fraction)));

        IReadOnlyDictionary<string, string> downloads = await _downloader
            .DownloadAsync(mirror, manifest, downloadProgress, cancellationToken)
            .ConfigureAwait(false);

        var installProgress = new Progress<PackageInstallProgress>(report =>
            progress?.Report(BootstrapProgress.Within(
                BootstrapStage.Installing,
                $"Installing {report.Current}",
                report.Fraction)));

        var installer = new PackageInstaller(binaryType);
        await installer
            .InstallAsync(versionDirectory, manifest, downloads, installProgress, cancellationToken)
            .ConfigureAwait(false);

        if (!File.Exists(executablePath))
        {
            throw new PackageInstallException(
                $"{binaryType.ToExecutableName()} is missing after installing {version.VersionGuid}.");
        }

        state.Set(binaryType, new InstalledClient
        {
            VersionGuid = version.VersionGuid,
            Version = version.Version,
            Channel = string.IsNullOrEmpty(channel.Name) ? DeploymentChannel.DefaultName : channel.Name,
        });
        _state.Save(state);

        progress?.Report(BootstrapProgress.For(BootstrapStage.Cleaning, "Tidying up"));
        RemoveSupersededVersions(state);

        progress?.Report(BootstrapProgress.For(BootstrapStage.Ready, "Ready"));

        return new BootstrapResult(
            binaryType, version.Version, version.VersionGuid,
            versionDirectory, executablePath, WasAlreadyInstalled: false);
    }

    public void RemoveSupersededVersions(InstallState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!Directory.Exists(_paths.Versions))
        {
            return;
        }

        var keep = state.Clients.Values
            .Select(client => client.VersionGuid)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (string directory in Directory.GetDirectories(_paths.Versions))
        {
            if (keep.Contains(Path.GetFileName(directory)))
            {
                continue;
            }

            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
