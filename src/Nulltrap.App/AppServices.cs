using System.IO;
using System.Net.Http;
using System.Reflection;

using Nulltrap.Core.Bootstrapping;
using Nulltrap.Core.FastFlags;
using Nulltrap.Core.Installation;
using Nulltrap.Core.Modifications;
using Nulltrap.Core.Packages;
using Nulltrap.Core.Presence;
using Nulltrap.Core.Roblox;
using Nulltrap.Core.Sessions;
using Nulltrap.Core.Settings;
using Nulltrap.Core.State;
using Nulltrap.Core.Updating;
using Nulltrap.Platform.Abstractions;
using Nulltrap.Platform.Windows;

using CoreDeploymentClient = Nulltrap.Core.Deployment.DeploymentClient;

namespace Nulltrap.App;

public sealed class AppServices : IDisposable
{
    private readonly HttpClient _http;

    public AppServices()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"Nulltrap/{Version}");

        Paths = new WindowsApplicationPaths();
        Protocols = new WindowsProtocolRegistrar();
        Shortcuts = new WindowsShortcutManager();
        UninstallEntry = new WindowsUninstallEntry();
        ProcessLauncher = new WindowsProcessLauncher();
        Remover = new WindowsDeferredRemover();

        StateStore = new InstallStateStore(Paths);
        Settings = new SettingsStore(Paths);
        FastFlags = new FastFlagManager(Paths);
        Deployment = new CoreDeploymentClient(_http);
        Downloader = new PackageDownloader(_http, Paths);
        Bootstrapper = new ClientBootstrapper(Deployment, Downloader, Paths, StateStore);
        Installer = new Installer(Paths, Protocols, Shortcuts, UninstallEntry);
        Jobs = new InstallJobs(Bootstrapper);
        Updates = new ClientUpdateWatcher(Deployment, StateStore, Jobs);
        Mods = Bootstrapper.Mods;
        Mods.Enabled = Settings.Load().Mods;

        Games = new GameInfoClient(_http);
        Discover = new DiscoverClient(_http);
        Accounts = new AccountInfoClient(_http);
        LauncherUpdates = new LauncherUpdateClient(_http);
        Sessions = new SessionTracker();
        History = new SessionHistoryStore(Paths);
        Recorder = new SessionRecorder(Sessions, Games, History);
        LogWatcher = new RobloxLogWatcher(RobloxLogWatcher.DefaultDirectory, Sessions);
        PresenceTransports = new WindowsPresenceTransportFactory();
    }

    public static string Version { get; } =
        typeof(AppServices).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "0.0.0";

    public static string CurrentExecutablePath { get; } =
        Environment.ProcessPath
        ?? Path.Combine(AppContext.BaseDirectory, $"{Installer.ProductName}.exe");

    public IApplicationPaths Paths { get; }

    public IProtocolRegistrar Protocols { get; }

    public IShortcutManager Shortcuts { get; }

    public IUninstallEntry UninstallEntry { get; }

    public IProcessLauncher ProcessLauncher { get; }

    public IDeferredRemover Remover { get; }

    public InstallStateStore StateStore { get; }

    public SettingsStore Settings { get; }

    public FastFlagManager FastFlags { get; }

    public CoreDeploymentClient Deployment { get; }

    public PackageDownloader Downloader { get; }

    public ClientBootstrapper Bootstrapper { get; }

    public Installer Installer { get; }

    public InstallJobs Jobs { get; }

    public ClientUpdateWatcher Updates { get; }

    public ModManager Mods { get; }

    public GameInfoClient Games { get; }

    public DiscoverClient Discover { get; }

    public AccountInfoClient Accounts { get; }

    public LauncherUpdateClient LauncherUpdates { get; }

    public SessionTracker Sessions { get; }

    public SessionHistoryStore History { get; }

    public SessionRecorder Recorder { get; }

    public RobloxLogWatcher LogWatcher { get; }

    public IPresenceTransportFactory PresenceTransports { get; }

    public PresenceService? Presence { get; private set; }

    public void KeepHandlersRegistered()
    {
        if (!Installer.IsInstalled)
        {
            return;
        }

        string handler = Installer.InstalledExecutablePath;

        foreach (LaunchTarget target in Enum.GetValues<LaunchTarget>())
        {
            try
            {
                if (!Protocols.IsRegistered(target, handler))
                {
                    Protocols.Register(target, handler);
                }
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
            }
        }
    }

    public void StartClientUpdates()
    {
        NulltrapSettings settings = Settings.Load();

        if (!settings.AutomaticClientUpdates)
        {
            return;
        }

        Updates.Channel = settings.DeploymentChannel;
        Updates.Start();
    }

    public void StartTracking()
    {
        Recorder.Start();
        LogWatcher.Start();
    }

    public void StartPresence()
    {
        NulltrapSettings settings = Settings.Load();

        Presence?.Dispose();
        Presence = null;

        string applicationId = PresenceService.ApplicationId(settings.DiscordApplicationId);

        if (!settings.DiscordPresence || string.IsNullOrWhiteSpace(applicationId))
        {
            return;
        }

        var discord = new DiscordPresenceClient(PresenceTransports, applicationId);
        Presence = new PresenceService(discord, Games, Sessions, Accounts)
        {
            Options = settings.PresenceOptions,
        };

        Presence.Start();
    }

    public void Dispose()
    {
        Updates.Dispose();
        Jobs.Dispose();
        Recorder.Dispose();
        Presence?.Dispose();
        LogWatcher.Dispose();
        _http.Dispose();
    }
}
