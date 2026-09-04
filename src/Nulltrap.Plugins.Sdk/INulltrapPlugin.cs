namespace Nulltrap.Plugins;

public sealed record PluginSession(
    long PlaceId,
    long UniverseId,
    string? GameName,
    string? ServerAddress,
    string? ServerCountry);

public interface IPluginHost
{
    string LauncherVersion { get; }

    string DataDirectory { get; }

    void Log(string message);

    void AskForFlag(string name, string value);

    event EventHandler<PluginSession>? Joined;

    event EventHandler<PluginSession>? Left;
}

public interface INulltrapPlugin
{
    string Name { get; }

    string Author { get; }

    string Version { get; }

    void Start(IPluginHost host);

    void Stop();
}
