namespace Nulltrap.Platform.Abstractions;

public enum LaunchTarget
{
    Player,
    Studio,
}

public interface IProtocolRegistrar
{
    bool IsRegistered(LaunchTarget target, string handlerPath);

    void Register(LaunchTarget target, string handlerPath);

    void Unregister(LaunchTarget target);

    string? GetRegisteredHandler(LaunchTarget target);
}
