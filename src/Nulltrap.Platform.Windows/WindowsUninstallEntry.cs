using Microsoft.Win32;

using Nulltrap.Platform.Abstractions;

namespace Nulltrap.Platform.Windows;

public sealed class WindowsUninstallEntry : IUninstallEntry
{
    public const string DefaultKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Nulltrap";

    private readonly string _keyPath;

    public WindowsUninstallEntry()
        : this(DefaultKeyPath)
    {
    }

    public WindowsUninstallEntry(string keyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);
        _keyPath = keyPath.Trim('\\');
    }

    public bool Exists
    {
        get
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(_keyPath);
            return key is not null;
        }
    }

    public void Write(UninstallEntryInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        using RegistryKey key = Registry.CurrentUser.CreateSubKey(_keyPath);

        key.SetValue("DisplayName", info.DisplayName);
        key.SetValue("DisplayVersion", info.DisplayVersion);
        key.SetValue("Publisher", info.Publisher);
        key.SetValue("DisplayIcon", info.ExecutablePath);
        key.SetValue("InstallLocation", info.InstallLocation);
        key.SetValue("UninstallString", $"\"{info.ExecutablePath}\" -uninstall");
        key.SetValue("QuietUninstallString", $"\"{info.ExecutablePath}\" -uninstall -quiet");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("EstimatedSize", (int)Math.Min(info.EstimatedSizeKilobytes, int.MaxValue), RegistryValueKind.DWord);
        key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
    }

    public void Remove()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false);
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
