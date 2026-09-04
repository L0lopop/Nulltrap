using System.Text.Json;

using Nulltrap.Platform.Abstractions;

namespace Nulltrap.Core.FastFlags;

public sealed class FastFlagManager
{
    public const string SettingsFolder = "ClientSettings";
    public const string SettingsFile = "ClientAppSettings.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    private readonly IApplicationPaths _paths;

    public FastFlagManager(IApplicationPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _paths = paths;
    }

    public string SourcePath =>
        Path.Combine(_paths.Modifications, SettingsFolder, SettingsFile);

    public Dictionary<string, string> Load()
    {
        if (!File.Exists(SourcePath))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(SourcePath))
                ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    public void Save(IReadOnlyDictionary<string, string> flags)
    {
        ArgumentNullException.ThrowIfNull(flags);

        string directory = Path.GetDirectoryName(SourcePath)!;
        Directory.CreateDirectory(directory);

        var kept = flags
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        string staging = $"{SourcePath}.{Environment.ProcessId}.tmp";
        File.WriteAllText(staging, JsonSerializer.Serialize(kept, Options));
        File.Move(staging, SourcePath, overwrite: true);
    }

    public bool ApplyTo(string versionDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionDirectory);

        string target = Path.Combine(versionDirectory, SettingsFolder, SettingsFile);

        if (!File.Exists(SourcePath))
        {
            if (File.Exists(target))
            {
                File.Delete(target);
            }

            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(SourcePath, target, overwrite: true);
        return true;
    }
}
