namespace Nulltrap.Platform.Abstractions;

public interface IProcessLauncher
{
    int Start(string executablePath, string arguments, string? workingDirectory = null);

    bool IsRunning(int processId);

    Task<bool> WaitForWindowAsync(int processId, TimeSpan timeout, CancellationToken cancellationToken = default);

    bool Stop(int processId);
}
