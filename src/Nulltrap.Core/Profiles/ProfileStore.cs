using System.Text.Encodings.Web;
using System.Text.Json;

using Nulltrap.Core.FastFlags;
using Nulltrap.Core.Roblox;
using Nulltrap.Platform.Abstractions;

namespace Nulltrap.Core.Profiles;

public sealed class ProfileStore
{
    public const string FileName = "Profiles.json";
    public const string BaselineFileName = "SettingsBefore.json";

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
        BaselinePath = Path.Combine(paths.Root, BaselineFileName);
    }

    public string SourcePath => _path;

    public string BaselinePath { get; }

    public void ApplySettings(GameProfile? profile) =>
        ApplySettings(profile, UserGameSettings.DefaultPath, BaselinePath);

    public static void ApplySettings(GameProfile? profile, string settingsPath, string baselinePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(baselinePath);

        Dictionary<string, string> before = ReadBaseline(baselinePath);
        bool wanted = profile is { Settings.Count: > 0 };

        if (!wanted && before.Count == 0)
        {
            return;
        }

        var roblox = new UserGameSettings(settingsPath);

        if (!roblox.Load())
        {
            return;
        }

        foreach ((string name, string value) in before)
        {
            Write(roblox, name, value);
        }

        var fresh = new Dictionary<string, string>(StringComparer.Ordinal);

        if (wanted)
        {
            foreach ((string name, string value) in profile!.Settings)
            {
                string? standing = Read(roblox, name);

                if (standing is not null)
                {
                    fresh[name] = standing;
                }

                Write(roblox, name, value);
            }
        }

        roblox.Save();
        KeepBaseline(baselinePath, fresh);
    }

    public static string? Read(UserGameSettings roblox, string name)
    {
        ArgumentNullException.ThrowIfNull(roblox);

        if (roblox.Number(name) is double number)
        {
            return number.ToString("0.#########", System.Globalization.CultureInfo.InvariantCulture);
        }

        return roblox.Flag(name) is bool flag ? flag ? "true" : "false" : null;
    }

    public static void Write(UserGameSettings roblox, string name, string value)
    {
        ArgumentNullException.ThrowIfNull(roblox);

        if (bool.TryParse(value, out bool flag))
        {
            roblox.SetFlag(name, flag);
            return;
        }

        if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double number))
        {
            roblox.SetNumber(name, number, number % 1 == 0 ? 0 : 3);
        }
    }

    private static Dictionary<string, string> ReadBaseline(string path)
    {
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
                ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (Exception failure) when (failure is JsonException or IOException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static void KeepBaseline(string path, Dictionary<string, string> values)
    {
        try
        {
            if (values.Count == 0)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(values, Options));
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
        }
    }

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

    public GameProfile? ApplyTo(
        string versionDirectory,
        long placeId,
        IReadOnlyDictionary<string, string> baseFlags,
        IReadOnlyDictionary<string, string>? asked = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionDirectory);
        ArgumentNullException.ThrowIfNull(baseFlags);

        GameProfile? chosen = Load().For(placeId);

        if (chosen is null && (asked is null || asked.Count == 0))
        {
            return null;
        }

        var beneath = new Dictionary<string, string>(baseFlags, StringComparer.Ordinal);

        foreach ((string name, string value) in asked ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                beneath[name] = value;
            }
        }

        Dictionary<string, string> merged = ProfileBook.Overlay(beneath, chosen);

        string target = Path.Combine(
            versionDirectory,
            FastFlagManager.SettingsFolder,
            FastFlagManager.SettingsFile);

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, JsonSerializer.Serialize(merged, Options));

        return chosen;
    }
}
