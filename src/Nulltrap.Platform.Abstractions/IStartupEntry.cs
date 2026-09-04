namespace Nulltrap.Platform.Abstractions;

public interface IStartupEntry
{
    bool IsRegistered(string executablePath);

    void Register(string executablePath, string arguments);

    void Remove();
}
