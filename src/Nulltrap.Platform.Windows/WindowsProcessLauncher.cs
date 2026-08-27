using System.ComponentModel;
using System.Diagnostics;

using Nulltrap.Platform.Abstractions;

namespace Nulltrap.Platform.Windows;

public sealed class WindowsProcessLauncher : IProcessLauncher
{
    public int Start(string executablePath, string arguments, string? workingDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments ?? string.Empty,
            WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(executablePath) ?? string.Empty,
            UseShellExecute = false,
        };

        using Process? process = Process.Start(startInfo);

        if (process is null)
        {
            throw new InvalidOperationException($"Windows refused to start {executablePath}.");
        }

        return process.Id;
    }

    public bool IsRunning(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (Exception failure) when (failure is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return false;
        }
    }
}
