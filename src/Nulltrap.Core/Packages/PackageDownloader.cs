using System.Security.Cryptography;

using Nulltrap.Core.Deployment;
using Nulltrap.Platform.Abstractions;

namespace Nulltrap.Core.Packages;

public sealed record PackageDownloadProgress(long BytesCompleted, long BytesTotal)
{
    public double Fraction => BytesTotal == 0 ? 0 : (double)BytesCompleted / BytesTotal;
}

public sealed class PackageDownloader
{
    private const int MaxConcurrentDownloads = 4;
    private const int RetryAttempts = 3;

    private readonly HttpClient _http;
    private readonly IApplicationPaths _paths;

    public PackageDownloader(HttpClient http, IApplicationPaths paths)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(paths);

        _http = http;
        _paths = paths;
    }

    public async Task<IReadOnlyDictionary<string, string>> DownloadAsync(
        DeploymentMirror mirror,
        PackageManifest manifest,
        IProgress<PackageDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mirror);
        ArgumentNullException.ThrowIfNull(manifest);

        Directory.CreateDirectory(_paths.Downloads);

        PackageEntry[] wanted = manifest.Where(PackageDirectoryMap.IsInstallable).ToArray();
        long total = wanted.Sum(entry => entry.PackedSize);
        long completed = 0;

        var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var gate = new SemaphoreSlim(MaxConcurrentDownloads);
        var writeLock = new object();

        async Task RunAsync(PackageEntry entry)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                string path = await FetchAsync(mirror, manifest.VersionGuid, entry, cancellationToken)
                    .ConfigureAwait(false);

                lock (writeLock)
                {
                    results[entry.Name] = path;
                    completed += entry.PackedSize;
                    progress?.Report(new PackageDownloadProgress(completed, total));
                }
            }
            finally
            {
                gate.Release();
            }
        }

        await Task.WhenAll(wanted.Select(RunAsync)).ConfigureAwait(false);

        return results;
    }

    private async Task<string> FetchAsync(
        DeploymentMirror mirror,
        string versionGuid,
        PackageEntry entry,
        CancellationToken cancellationToken)
    {
        string cached = Path.Combine(_paths.Downloads, entry.Checksum);

        if (File.Exists(cached) && await MatchesAsync(cached, entry, cancellationToken).ConfigureAwait(false))
        {
            return cached;
        }

        Uri source = mirror.GetPackageUri(versionGuid, entry.Name);
        string staging = cached + ".partial";

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                await using (FileStream file = File.Create(staging))
                {
                    await using Stream body = await _http
                        .GetStreamAsync(source, cancellationToken)
                        .ConfigureAwait(false);

                    await body.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
                }

                if (!await MatchesAsync(staging, entry, cancellationToken).ConfigureAwait(false))
                {
                    throw new PackageDownloadException(
                        $"{entry.Name} downloaded from {mirror} does not match its checksum.");
                }

                File.Move(staging, cached, overwrite: true);
                return cached;
            }
            catch (Exception failure) when (failure is not OperationCanceledException
                                            && attempt < RetryAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (File.Exists(staging))
                {
                    try
                    {
                        File.Delete(staging);
                    }
                    catch (IOException)
                    {
                    }
                }
            }
        }
    }

    private static async Task<bool> MatchesAsync(
        string path,
        PackageEntry entry,
        CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);

        if (!info.Exists || info.Length != entry.PackedSize)
        {
            return false;
        }

        await using FileStream stream = File.OpenRead(path);
        byte[] digest = await MD5.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);

        return Convert.ToHexStringLower(digest)
            .Equals(entry.Checksum, StringComparison.OrdinalIgnoreCase);
    }
}
