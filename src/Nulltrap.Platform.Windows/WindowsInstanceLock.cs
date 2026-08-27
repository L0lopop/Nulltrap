using Nulltrap.Platform.Abstractions;

namespace Nulltrap.Platform.Windows;

public sealed class WindowsInstanceLockFactory : IInstanceLockFactory
{
    public bool TryAcquire(string name, out IInstanceLock instanceLock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var mutex = new Mutex(initiallyOwned: false, Qualify(name), out _);

        bool acquired;

        try
        {
            acquired = mutex.WaitOne(TimeSpan.Zero, exitContext: false);
        }
        catch (AbandonedMutexException)
        {
            acquired = true;
        }

        if (!acquired)
        {
            mutex.Dispose();
            instanceLock = NullInstanceLock.Instance;
            return false;
        }

        instanceLock = new WindowsInstanceLock(name, mutex);
        return true;
    }

    public bool IsHeldByAnotherProcess(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Mutex.TryOpenExisting(Qualify(name), out Mutex? existing) && Release(existing);
    }

    private static bool Release(Mutex mutex)
    {
        mutex.Dispose();
        return true;
    }

    private static string Qualify(string name) => $"Local\\Nulltrap-{name}";
}

internal sealed class WindowsInstanceLock : IInstanceLock
{
    private Mutex? _mutex;

    public WindowsInstanceLock(string name, Mutex mutex)
    {
        Name = name;
        _mutex = mutex;
    }

    public string Name { get; }

    public bool IsHeld => _mutex is not null;

    public void Dispose()
    {
        if (_mutex is null)
        {
            return;
        }

        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
        }

        _mutex.Dispose();
        _mutex = null;
    }
}

internal sealed class NullInstanceLock : IInstanceLock
{
    public static readonly NullInstanceLock Instance = new();

    public string Name => string.Empty;

    public bool IsHeld => false;

    public void Dispose()
    {
    }
}
