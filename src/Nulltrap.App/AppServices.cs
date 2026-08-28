using System.IO;
using System.Net.Http;
using System.Reflection;

using Nulltrap.Core.Bootstrapping;
using Nulltrap.Core.FastFlags;
using Nulltrap.Core.Installation;
using Nulltrap.Core.Packages;
using Nulltrap.Core.Presence;
using Nulltrap.Core.Roblox;
using Nulltrap.Core.Sessions;
using Nulltrap.Core.Settings;
using Nulltrap.Core.State;
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

        StateStore = new InstallStateStore(Paths);
        Settings = new SettingsStore(Paths);
        FastFlags = new FastFlagManager(Paths);
        Deployment = new CoreDeploymentClient(_http);
        Downloader = new PackageDownloader(_http, Paths);
        Bootstrapper = new ClientBootstrapper(Deployment, Downloader, Paths, StateStore);
        Installer = new Installer(Paths, Protocols, Shortcuts, UninstallEntry);
        Jobs = new InstallJobs(Bootstrapper);

        Games = new GameInfoClient(_http);
        Sessions = new SessionTracker();
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

    public InstallStateStore StateStore { get; }

    public SettingsStore Settings { get; }

    public FastFlagManager FastFlags { get; }

    public CoreDeploymentClient Deployment { get; }

    public PackageDownloader Downloader { get; }

    public ClientBootstrapper Bootstrapper { get; }

    public Installer Installer { get; }

    public InstallJobs Jobs { get; }

    public GameInfoClient Games { get; }

    public SessionTracker Sessions { get; }

    public RobloxLogWatcher LogWatcher { get; }

    public IPresenceTransportFactory PresenceTransports { get; }

    public PresenceService? Presence { get; private set; }

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
        Presence = new PresenceService(discord, Games, Sessions)
        {
            Options = settings.PresenceOptions,
        };

        Presence.Start();
        LogWatcher.Start();
    }

    public void Dispose()
    {
        Jobs.Dispose();
        Presence?.Dispose();
        LogWatcher.Dispose();
        _http.Dispose();
    }
}
