using System.Text.Json;

using Nulltrap.Platform.Abstractions;

namespace Nulltrap.Core.Sessions;

public sealed class SessionHistoryStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    private readonly Lock _gate = new();
    private readonly string _path;

    public SessionHistoryStore(IApplicationPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _path = Path.Combine(paths.Root, "History.json");
    }

    public SessionHistory Load()
    {
        lock (_gate)
        {
            return Read();
        }
    }

    public void Add(PlayedSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        lock (_gate)
        {
            SessionHistory history = Read();
            history.Add(session);
            Write(history);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            Write(new SessionHistory());
        }
    }

    private SessionHistory Read()
    {
        if (!File.Exists(_path))
        {
            return new SessionHistory();
        }

        try
        {
            return JsonSerializer.Deserialize<SessionHistory>(File.ReadAllText(_path), Options)
                ?? new SessionHistory();
        }
        catch (Exception failure) when (failure is JsonException or IOException)
        {
            return new SessionHistory();
        }
    }

    private void Write(SessionHistory history)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

        string staging = _path + ".tmp";
        File.WriteAllText(staging, JsonSerializer.Serialize(history, Options));
        File.Move(staging, _path, overwrite: true);
    }
}
