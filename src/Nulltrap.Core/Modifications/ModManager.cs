using Nulltrap.Core.FastFlags;
using Nulltrap.Platform.Abstractions;

namespace Nulltrap.Core.Modifications;

public sealed record ModFile(string RelativePath, long Size, DateTimeOffset ChangedAt);

public sealed record ModOutcome(int Applied, int Reverted);

public sealed class ModManager
{
    public const string ManifestName = ".nulltrap-mods";
    public const string BackupFolder = ".nulltrap-originals";

    private readonly IApplicationPaths _paths;

    public ModManager(IApplicationPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _paths = paths;
    }

    public bool Enabled { get; set; } = true;

    public string SourceDirectory => _paths.Modifications;

    public IReadOnlyList<ModFile> List() => List(SourceDirectory);

    public static IReadOnlyList<ModFile> List(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        if (!Directory.Exists(root))
        {
            return [];
        }

        return new DirectoryInfo(root)
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .Select(file => new
            {
                File = file,
                Relative = Path.GetRelativePath(root, file.FullName)
                    .Replace(Path.DirectorySeparatorChar, '/'),
            })
            .Where(entry => Carries(entry.Relative))
            .Select(entry => new ModFile(
                entry.Relative,
                entry.File.Length,
                new DateTimeOffset(entry.File.LastWriteTimeUtc, TimeSpan.Zero)))
            .OrderBy(mod => mod.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool Carries(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        string[] parts = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0 || parts.Any(part => part.StartsWith('.')))
        {
            return false;
        }

        return !parts[0].Equals(FastFlagManager.SettingsFolder, StringComparison.OrdinalIgnoreCase);
    }

    public ModOutcome ApplyTo(string versionDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionDirectory);

        if (!Directory.Exists(versionDirectory))
        {
            return new ModOutcome(0, 0);
        }

        string manifest = Path.Combine(versionDirectory, ManifestName);
        string backups = Path.Combine(versionDirectory, BackupFolder);

        HashSet<string> was = Read(manifest);
        IReadOnlyList<ModFile> wanted = Enabled ? List() : [];
        var now = wanted.Select(mod => mod.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        int reverted = 0;

        foreach (string relative in was.Where(relative => !now.Contains(relative)))
        {
            if (Restore(versionDirectory, backups, relative))
            {
                reverted++;
            }
        }

        int applied = 0;

        foreach (ModFile mod in wanted)
        {
            if (Overlay(versionDirectory, backups, mod.RelativePath))
            {
                applied++;
            }
        }

        Write(manifest, now);
        Tidy(backups);

        return new ModOutcome(applied, reverted);
    }

    private bool Overlay(string versionDirectory, string backups, string relative)
    {
        string source = Path.Combine(SourceDirectory, relative.Replace('/', Path.DirectorySeparatorChar));
        string target = Path.Combine(versionDirectory, relative.Replace('/', Path.DirectorySeparatorChar));
        string backup = Path.Combine(backups, relative.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(source))
        {
            return false;
        }

        try
        {
            if (File.Exists(target) && !File.Exists(backup))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                File.Copy(target, backup, overwrite: true);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: true);

            return true;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool Restore(string versionDirectory, string backups, string relative)
    {
        string target = Path.Combine(versionDirectory, relative.Replace('/', Path.DirectorySeparatorChar));
        string backup = Path.Combine(backups, relative.Replace('/', Path.DirectorySeparatorChar));

        try
        {
            if (File.Exists(backup))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(backup, target, overwrite: true);
                File.Delete(backup);
                return true;
            }

            if (File.Exists(target))
            {
                File.Delete(target);
                return true;
            }
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
        }

        return false;
    }

    private static HashSet<string> Read(string manifest)
    {
        var kept = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(manifest))
        {
            return kept;
        }

        try
        {
            foreach (string line in File.ReadAllLines(manifest))
            {
                string trimmed = line.Trim();

                if (trimmed.Length > 0)
                {
                    kept.Add(trimmed);
                }
            }
        }
        catch (IOException)
        {
        }

        return kept;
    }

    private static void Write(string manifest, IEnumerable<string> applied)
    {
        try
        {
            string[] lines = applied.OrderBy(line => line, StringComparer.OrdinalIgnoreCase).ToArray();

            if (lines.Length == 0)
            {
                File.Delete(manifest);
                return;
            }

            File.WriteAllLines(manifest, lines);
        }
        catch (IOException)
        {
        }
    }

    private static void Tidy(string backups)
    {
        if (!Directory.Exists(backups))
        {
            return;
        }

        try
        {
            foreach (string directory in Directory.GetDirectories(backups, "*", SearchOption.AllDirectories)
                         .OrderByDescending(path => path.Length))
            {
                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                }
            }

            if (!Directory.EnumerateFileSystemEntries(backups).Any())
            {
                Directory.Delete(backups);
            }
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
        }
    }
}
