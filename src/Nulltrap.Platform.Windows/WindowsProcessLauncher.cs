using System.ComponentModel;
using System.Diagnostics;

using Nulltrap.Platform.Abstractions;

namespace Nulltrap.Platform.Windows;

public sealed class WindowsProcessLauncher : IProcessLauncher
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

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

    public async Task<bool> WaitForWindowAsync(
        int processId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                using Process process = Process.GetProcessById(processId);

                if (process.HasExited)
                {
                    return false;
                }

                process.Refresh();

                if (process.MainWindowHandle != 0)
                {
                    return true;
                }
            }
            catch (Exception failure) when (failure is ArgumentException or InvalidOperationException or Win32Exception)
            {
                return false;
            }

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    public bool Stop(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);

            if (process.HasExited)
            {
                return false;
            }

            process.Kill(entireProcessTree: true);
            return true;
        }
        catch (Exception failure) when (failure is ArgumentException or InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return false;
        }
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
