namespace Nulltrap.Platform.Abstractions;

public interface IApplicationPaths
{
    string Root { get; }

    string Versions { get; }

    string Downloads { get; }

    string Modifications { get; }

    string Logs { get; }

    void EnsureCreated();
}
