using System.Text.Json;
using System.Text.Json.Serialization;

using Nulltrap.Platform.Abstractions;

namespace Nulltrap.Core.Settings;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;

    public SettingsStore(IApplicationPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _path = Path.Combine(paths.Root, "Settings.json");
    }

    public NulltrapSettings Load()
    {
        if (!File.Exists(_path))
        {
            return new NulltrapSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<NulltrapSettings>(File.ReadAllText(_path), Options)
                ?? new NulltrapSettings();
        }
        catch (JsonException)
        {
            return new NulltrapSettings();
        }
    }

    public void Save(NulltrapSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

        string staging = _path + ".tmp";
        File.WriteAllText(staging, JsonSerializer.Serialize(settings, Options));
        File.Move(staging, _path, overwrite: true);
    }
}
