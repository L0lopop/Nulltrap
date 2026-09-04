using System.Text.Encodings.Web;
using System.Text.Json;

using Nulltrap.Core.FastFlags;
using Nulltrap.Platform.Abstractions;

namespace Nulltrap.Core.Profiles;

public sealed class ProfileStore
{
    public const string FileName = "Profiles.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _path;

    public ProfileStore(IApplicationPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _path = Path.Combine(paths.Root, FileName);
    }

    public string SourcePath => _path;

    public ProfileBook Load()
    {
        if (!File.Exists(_path))
        {
            return new ProfileBook();
        }

        try
        {
            return JsonSerializer.Deserialize<ProfileBook>(File.ReadAllText(_path), Options)
                ?? new ProfileBook();
        }
        catch (Exception failure) when (failure is JsonException or IOException)
        {
            return new ProfileBook();
        }
    }

    public void Save(ProfileBook book)
    {
        ArgumentNullException.ThrowIfNull(book);

        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

        string staging = _path + ".tmp";
        File.WriteAllText(staging, JsonSerializer.Serialize(book, Options));
        File.Move(staging, _path, overwrite: true);
    }

    public GameProfile? ApplyTo(string versionDirectory, long placeId, IReadOnlyDictionary<string, string> baseFlags)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionDirectory);
        ArgumentNullException.ThrowIfNull(baseFlags);

        GameProfile? chosen = Load().For(placeId);

        if (chosen is null)
        {
            return null;
        }

        Dictionary<string, string> merged = ProfileBook.Overlay(baseFlags, chosen);

        string target = Path.Combine(
            versionDirectory,
            FastFlagManager.SettingsFolder,
            FastFlagManager.SettingsFile);

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, JsonSerializer.Serialize(merged, Options));

        return chosen;
    }
}
