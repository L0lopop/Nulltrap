using System.ComponentModel;
using System.Diagnostics;

using Nulltrap.Platform.Abstractions;

namespace Nulltrap.Platform.Windows;

public sealed class WindowsDeferredRemover : IDeferredRemover
{
    public const int WaitSeconds = 4;

    public bool RemoveAfterExit(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!Path.IsPathFullyQualified(path) || path.TrimEnd().EndsWith(':'))
        {
            return false;
        }

        string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        string root = Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar) ?? string.Empty;

        if (root.Length == 0 || string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string sweep = Directory.Exists(full) ? $"rd /s /q \"{full}\"" : $"del /f /q \"{full}\"";

        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            Arguments = $"/c ping -n {WaitSeconds + 1} 127.0.0.1 > nul & {sweep}",
            WorkingDirectory = Environment.SystemDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            using Process? sweeper = Process.Start(startInfo);

            return sweeper is not null;
        }
        catch (Exception failure) when (failure is Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }
}
