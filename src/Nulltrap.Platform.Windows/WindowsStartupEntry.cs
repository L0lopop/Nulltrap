using Microsoft.Win32;

using Nulltrap.Platform.Abstractions;

namespace Nulltrap.Platform.Windows;

public sealed class WindowsStartupEntry : IStartupEntry
{
    public const string DefaultRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ValueName = "Nulltrap";

    private readonly string _runKey;

    public WindowsStartupEntry()
        : this(DefaultRunKey)
    {
    }

    public WindowsStartupEntry(string runKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runKey);
        _runKey = runKey.Trim('\\');
    }

    public static string Quote(string executablePath, string arguments) =>
        string.IsNullOrWhiteSpace(arguments)
            ? $"\"{executablePath}\""
            : $"\"{executablePath}\" {arguments.Trim()}";

    public string? Current()
    {
        try
        {
            using RegistryKey? run = Registry.CurrentUser.OpenSubKey(_runKey);

            return run?.GetValue(ValueName) as string;
        }
        catch (Exception failure) when (failure is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    public bool IsRegistered(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        string? written = Current();

        if (written is null)
        {
            return false;
        }

        string wanted = Path.GetFullPath(executablePath);
        string trimmed = written.Trim();
        int close = trimmed.StartsWith('"') ? trimmed.IndexOf('"', 1) : -1;
        string named = close > 0 ? trimmed[1..close] : trimmed.Split(' ')[0];

        try
        {
            return string.Equals(Path.GetFullPath(named), wanted, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception failure) when (failure is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    public void Register(string executablePath, string arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        using RegistryKey run = Registry.CurrentUser.CreateSubKey(_runKey);

        run.SetValue(ValueName, Quote(executablePath, arguments), RegistryValueKind.String);
    }

    public void Remove()
    {
        try
        {
            using RegistryKey? run = Registry.CurrentUser.OpenSubKey(_runKey, writable: true);

            if (run?.GetValue(ValueName) is not null)
            {
                run.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception failure) when (failure is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
        }
    }
}
