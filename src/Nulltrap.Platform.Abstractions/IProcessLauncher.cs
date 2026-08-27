namespace Nulltrap.Platform.Abstractions;

public interface IProcessLauncher
{
    int Start(string executablePath, string arguments, string? workingDirectory = null);

    bool IsRunning(int processId);
}
