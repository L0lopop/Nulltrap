using Nulltrap.Platform.Abstractions;

namespace Nulltrap.Core.Installation;

public sealed record InstallOptions(
    bool CreateDesktopShortcut = true,
    bool CreateStartMenuShortcut = true,
    bool RegisterPlayer = true,
    bool RegisterStudio = false);

public sealed record InstallReport(
    string ExecutablePath,
    bool CopiedExecutable,
    bool ReplacedAnotherLauncher,
    string? PreviousPlayerHandler);

public sealed class Installer
{
    public const string ProductName = "Nulltrap";

    private readonly IApplicationPaths _paths;
    private readonly IProtocolRegistrar _protocols;
    private readonly IShortcutManager _shortcuts;
    private readonly IUninstallEntry _uninstall;

    public Installer(
        IApplicationPaths paths,
        IProtocolRegistrar protocols,
        IShortcutManager shortcuts,
        IUninstallEntry uninstall)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(protocols);
        ArgumentNullException.ThrowIfNull(shortcuts);
        ArgumentNullException.ThrowIfNull(uninstall);

        _paths = paths;
        _protocols = protocols;
        _shortcuts = shortcuts;
        _uninstall = uninstall;
    }

    public string InstalledExecutablePath => Path.Combine(_paths.Root, $"{ProductName}.exe");

    public bool IsInstalled =>
        File.Exists(InstalledExecutablePath)
        && _protocols.IsRegistered(LaunchTarget.Player, InstalledExecutablePath);

    public bool IsRunningFromInstall(string currentExecutablePath) =>
        string.Equals(
            Path.GetFullPath(currentExecutablePath),
            Path.GetFullPath(InstalledExecutablePath),
            StringComparison.OrdinalIgnoreCase);

    public InstallReport Install(
        string currentExecutablePath,
        string version,
        InstallOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentExecutablePath);

        options ??= new InstallOptions();
        _paths.EnsureCreated();

        string target = InstalledExecutablePath;
        bool copied = false;

        if (!IsRunningFromInstall(currentExecutablePath))
        {
            File.Copy(currentExecutablePath, target, overwrite: true);
            copied = true;
        }

        string? previous = _protocols.GetRegisteredHandler(LaunchTarget.Player);
        bool replaced = previous is not null
            && !string.Equals(Path.GetFullPath(previous), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase);

        if (options.RegisterPlayer)
        {
            _protocols.Register(LaunchTarget.Player, target);
        }

        if (options.RegisterStudio)
        {
            _protocols.Register(LaunchTarget.Studio, target);
        }

        if (options.CreateDesktopShortcut)
        {
            _shortcuts.Create(ShortcutLocation.Desktop, ProductName, target, string.Empty, ProductName);
        }

        if (options.CreateStartMenuShortcut)
        {
            _shortcuts.Create(ShortcutLocation.StartMenu, ProductName, target, string.Empty, ProductName);
        }

        _uninstall.Write(new UninstallEntryInfo(
            ProductName,
            version,
            ProductName,
            _paths.Root,
            target,
            EstimateSizeKilobytes()));

        return new InstallReport(target, copied, replaced, previous);
    }

    public void Uninstall(bool keepDownloads = false)
    {
        _protocols.Unregister(LaunchTarget.Player);
        _protocols.Unregister(LaunchTarget.Studio);

        _shortcuts.Remove(ShortcutLocation.Desktop, ProductName);
        _shortcuts.Remove(ShortcutLocation.StartMenu, ProductName);

        _uninstall.Remove();

        Delete(_paths.Versions);
        Delete(_paths.Modifications);
        Delete(_paths.Logs);

        if (!keepDownloads)
        {
            Delete(_paths.Downloads);
        }
    }

    private long EstimateSizeKilobytes()
    {
        if (!Directory.Exists(_paths.Root))
        {
            return 0;
        }

        try
        {
            long bytes = new DirectoryInfo(_paths.Root)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(file => file.Length);

            return bytes / 1024;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private static void Delete(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
        }
    }
}
