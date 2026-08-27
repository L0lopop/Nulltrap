using System.IO.Compression;

using Nulltrap.Core.Deployment;

namespace Nulltrap.Core.Packages;

public sealed record PackageInstallProgress(int PackagesCompleted, int PackagesTotal, string Current)
{
    public double Fraction => PackagesTotal == 0 ? 0 : (double)PackagesCompleted / PackagesTotal;
}

public sealed class PackageInstaller
{
    private const string AppSettings =
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <Settings>
        	<ContentFolder>content</ContentFolder>
        	<BaseUrl>http://www.roblox.com</BaseUrl>
        </Settings>

        """;

    private readonly BinaryType _binaryType;
    private readonly IReadOnlyDictionary<string, string> _directories;

    public PackageInstaller(BinaryType binaryType)
    {
        _binaryType = binaryType;
        _directories = PackageDirectoryMap.For(binaryType);
    }

    public async Task InstallAsync(
        string versionDirectory,
        PackageManifest manifest,
        IReadOnlyDictionary<string, string> downloads,
        IProgress<PackageInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionDirectory);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(downloads);

        PackageEntry[] wanted = manifest.Where(PackageDirectoryMap.IsInstallable).ToArray();

        string[] unmapped = wanted
            .Where(entry => !_directories.ContainsKey(entry.Name))
            .Select(entry => entry.Name)
            .ToArray();

        if (unmapped.Length > 0)
        {
            throw new PackageInstallException(
                $"Roblox is shipping packages Nulltrap has no destination for: {string.Join(", ", unmapped)}. "
                + "The package directory map needs updating before this version can be installed.");
        }

        Directory.CreateDirectory(versionDirectory);

        int completed = 0;

        foreach (PackageEntry entry in wanted)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!downloads.TryGetValue(entry.Name, out string? archive))
            {
                throw new PackageInstallException($"{entry.Name} was never downloaded.");
            }

            string target = Path.GetFullPath(
                Path.Combine(versionDirectory, _directories[entry.Name].Replace('/', Path.DirectorySeparatorChar)));

            await Task.Run(() => Extract(archive, target, cancellationToken), cancellationToken)
                .ConfigureAwait(false);

            completed++;
            progress?.Report(new PackageInstallProgress(completed, wanted.Length, entry.Name));
        }

        await File.WriteAllTextAsync(
                Path.Combine(versionDirectory, "AppSettings.xml"),
                AppSettings,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static void Extract(string archivePath, string targetDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(targetDirectory);

        string root = Path.GetFullPath(targetDirectory);

        using ZipArchive archive = ZipFile.OpenRead(archivePath);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            string destination = Path.GetFullPath(Path.Combine(root, entry.FullName));

            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new PackageInstallException(
                    $"{Path.GetFileName(archivePath)} contains an entry that would write outside "
                    + $"the version folder: {entry.FullName}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
        }
    }
}
