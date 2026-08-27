using System.Text.Json;

using Nulltrap.Platform.Abstractions;

namespace Nulltrap.Core.State;

public sealed class InstallStateStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    private readonly string _path;

    public InstallStateStore(IApplicationPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _path = Path.Combine(paths.Root, "State.json");
    }

    public InstallState Load()
    {
        if (!File.Exists(_path))
        {
            return new InstallState();
        }

        try
        {
            return JsonSerializer.Deserialize<InstallState>(File.ReadAllText(_path), Options)
                ?? new InstallState();
        }
        catch (JsonException)
        {
            return new InstallState();
        }
    }

    public void Save(InstallState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

        string staging = _path + ".tmp";
        File.WriteAllText(staging, JsonSerializer.Serialize(state, Options));
        File.Move(staging, _path, overwrite: true);
    }
}
