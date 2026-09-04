using Nulltrap.Platform.Abstractions;

namespace Nulltrap.Core.Installation;

public sealed record InstallOptions(
    bool CreateDesktopShortcut = true,
    bool CreateStartMenuShortcut = true,
    bool RegisterPlayer = true,
    bool RegisterStudio = false);

public enum Removal
{
    LauncherOnly,
    Everything,
}

public sealed record UninstallReport(string? WaitingToGo);

public sealed record InstallReport(
    string ExecutablePath,
    bool CopiedExecutable,
    bool ReplacedAnotherLauncher,
    string? PreviousPlayerHandler);

public sealed class Installer
{
    public const string ProductName = "Nulltrap";
    public const string StateFile = "State.json";

    private static readonly string[] Keepsakes = ["Settings.json", StateFile, "History.json"];

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

    public string RetiredExecutablePath => InstalledExecutablePath + ".old";

    public void ForgetRetired()
    {
        try
        {
            if (File.Exists(RetiredExecutablePath))
            {
                File.Delete(RetiredExecutablePath);
            }
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
        }
    }

    public void Replace(string freshExecutable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(freshExecutable);

        if (!File.Exists(freshExecutable))
        {
            throw new FileNotFoundException(freshExecutable);
        }

        ForgetRetired();

        if (File.Exists(InstalledExecutablePath))
        {
            File.Move(InstalledExecutablePath, RetiredExecutablePath);
        }

        try
        {
            File.Move(freshExecutable, InstalledExecutablePath);
        }
        catch (IOException)
        {
            if (File.Exists(RetiredExecutablePath) && !File.Exists(InstalledExecutablePath))
            {
                File.Move(RetiredExecutablePath, InstalledExecutablePath);
            }

            throw;
        }
    }

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

    public UninstallReport Uninstall(Removal removal, bool keepDownloadCache = false)
    {
        _protocols.Unregister(LaunchTarget.Player);
        _protocols.Unregister(LaunchTarget.Studio);

        _shortcuts.Remove(ShortcutLocation.Desktop, ProductName);
        _shortcuts.Remove(ShortcutLocation.StartMenu, ProductName);

        _uninstall.Remove();

        Delete(_paths.Versions);
        Delete(_paths.Logs);

        if (!keepDownloadCache || removal == Removal.Everything)
        {
            Delete(_paths.Downloads);
        }

        if (removal == Removal.LauncherOnly)
        {
            Erase(Path.Combine(_paths.Root, StateFile));

            return new UninstallReport(File.Exists(InstalledExecutablePath) ? InstalledExecutablePath : null);
        }

        Delete(_paths.Modifications);

        foreach (string file in Keepsakes)
        {
            Erase(Path.Combine(_paths.Root, file));
        }

        return new UninstallReport(Directory.Exists(_paths.Root) ? _paths.Root : null);
    }

    private static void Erase(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
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
