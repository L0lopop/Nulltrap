namespace Nulltrap.Platform.Abstractions;

public interface IInstanceLock : IDisposable
{
    string Name { get; }

    bool IsHeld { get; }
}

public interface IInstanceLockFactory
{
    bool TryAcquire(string name, out IInstanceLock instanceLock);

    bool IsHeldByAnotherProcess(string name);
}
