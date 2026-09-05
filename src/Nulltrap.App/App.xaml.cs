using System.Windows;

using Nulltrap.Core.Bootstrapping;
using Nulltrap.Core.Installation;
using Nulltrap.Core.Launching;
using Nulltrap.Core.Localization;
using Nulltrap.Platform.Abstractions;

namespace Nulltrap.App;

public partial class App : Application
{
    private static readonly TimeSpan ClientWindowTimeout = TimeSpan.FromMinutes(3);

    private static readonly TimeSpan MemoryRelief = TimeSpan.FromMinutes(2);

    private static readonly TimeSpan UpdateCheckEvery = TimeSpan.FromHours(6);

    private readonly System.Windows.Threading.DispatcherTimer _relief = new() { Interval = MemoryRelief };

    private readonly System.Windows.Threading.DispatcherTimer _pulse = new() { Interval = TimeSpan.FromSeconds(5) };

    private const string WatchLock = "Nulltrap.Background";

    private AppServices? _services;

    private TrayIcon? _tray;

    private IInstanceLock? _watch;

    private bool _asking;

    public static AppServices Services =>
        (Current as App)?._services
        ?? throw new InvalidOperationException("Services are not available yet.");

    private void OnJoinedServer(object? sender, Core.Sessions.RobloxSession session)
    {
        if (_services is null)
        {
            return;
        }

        bool notice = _services.Settings.Load().ServerNotice;
        bool listening = _services.Plugins.Found.Any(plugin => plugin.Running);
        bool showing = _tray is not null;

        if (!notice && !listening && !showing)
        {
            return;
        }

        _ = Dispatcher.InvokeAsync(async () =>
        {
            Core.Roblox.GameInfo? game = await _services.Games
                .DescribeAsync(session.UniverseId)
                .ConfigureAwait(true);

            Core.Roblox.ServerPlace? place = await _services.Locator
                .DescribeAsync(session.ServerAddress)
                .ConfigureAwait(true);

            if (listening)
            {
                _services.Plugins.Tell(Told(session, game?.Name, place?.Country), joined: true);
            }

            Core.Roblox.ServerFacts? facts = notice || showing
                ? await _services.Servers.FindAsync(session.PlaceId, session.JobId).ConfigureAwait(true)
                : null;

            _tray?.Playing(
                game?.Name ?? Core.Localization.Strings.Get("activity.unknownGame"),
                place,
                facts);

            if (!notice)
            {
                return;
            }

            NoticeWindow.Announce(
                game?.Name ?? Core.Localization.Strings.Get("activity.unknownGame"),
                Where(place, game),
                Numbers(facts),
                game?.IconUrl);
        });
    }

    private void OnMovedServer(object? sender, Core.Sessions.RobloxSession session) =>
        _services?.Plugins.Tell(Told(session, null, null), joined: false);

    private void OnLeftServer(object? sender, Core.Sessions.RobloxSession session)
    {
        if (_services is null)
        {
            return;
        }

        _tray?.Idle();

        _services.Plugins.Tell(Told(session, null, null), joined: false);

        if (session.IsIdentified && _services.Settings.Load().CloseRobloxOnLeave)
        {
            AppServices.CloseRoblox();
        }
    }

    private static string Where(Core.Roblox.ServerPlace? place, Core.Roblox.GameInfo? game)
    {
        var parts = new List<string>
        {
            place?.Describe ?? Core.Localization.Strings.Get("notice.unknownPlace"),
        };

        if (game is { Playing: > 0 })
        {
            parts.Add(Core.Localization.Strings.Get("notice.online", game.Playing.ToString("N0")));
        }

        return string.Join(" · ", parts);
    }

    private static string? Numbers(Core.Roblox.ServerFacts? facts)
    {
        if (facts is null)
        {
            return null;
        }

        var parts = new List<string>();

        if (facts.MaxPlayers > 0)
        {
            parts.Add(Core.Localization.Strings.Get("notice.seats", facts.Playing, facts.MaxPlayers));
        }

        if (facts.Fps > 0)
        {
            parts.Add(Core.Localization.Strings.Get("notice.tick", facts.Fps));
        }

        if (facts.Ping > 0)
        {
            parts.Add(Core.Localization.Strings.Get("notice.ping", facts.Ping));
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    private static Plugins.PluginSession Told(
        Core.Sessions.RobloxSession session,
        string? game,
        string? country) => new(
            session.PlaceId,
            session.UniverseId,
            game,
            session.ServerAddress,
            country);

    public void NudgeUpdateCheck() => _ = TellAboutUpdateAsync();

    private bool Standing => Windows.OfType<MainWindow>().Any(window => window.IsVisible);

    private async Task TellAboutUpdateAsync()
    {
        if (_services is null || _asking || !Standing)
        {
            return;
        }

        Core.Settings.NulltrapSettings asked = _services.Settings.Load();

        if (!asked.UpdateNotice
            || (asked.LastUpdateCheck is { } last && DateTimeOffset.UtcNow - last < UpdateCheckEvery))
        {
            return;
        }

        _asking = true;

        Core.Updating.LauncherRelease? release;

        try
        {
            release = await _services.LauncherUpdates
                .LatestAsync(AppServices.Version)
                .ConfigureAwait(true);
        }
        finally
        {
            _asking = false;
        }

        Core.Settings.NulltrapSettings fresh = _services.Settings.Load();

        fresh.LastUpdateCheck = DateTimeOffset.UtcNow;
        _services.Settings.Save(fresh);

        if (release is not { Newer: true } || !Standing)
        {
            return;
        }

        NoticeWindow.Announce(
            Strings.Get("news.newVersion", release.Version),
            Strings.Get("news.noticeBody"),
            chosen: ShowNews);
    }

    private void ShowNews()
    {
        if (Windows.OfType<SettingsWindow>().FirstOrDefault() is { } standing)
        {
            standing.GoTo("News");
            standing.Activate();
            return;
        }

        Surface()?.OpenSettings("News");
    }

    private void OnPulse(object? sender, EventArgs e)
    {
        if (_services is null
            || _services.Sessions.State == Core.Sessions.SessionState.Idle
            || AppServices.RobloxIsRunning())
        {
            return;
        }

        _services.Sessions.GiveUp();
    }

    private void OnMemoryRelief(object? sender, EventArgs e)
    {
        if (_services?.Settings.Load().TrimMemory == true)
        {
            _services.Memory.Trim();
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _services = new AppServices();
        Core.Settings.NulltrapSettings chosen = _services.Settings.Load();

        _services.ApplyStartup();

        Strings.Use(chosen.Language);
        Themes.Apply(chosen.Theme);
        _services.KeepHandlersRegistered();
        _services.Installer.ForgetRetired();
        _services.StartPlugins();
        _services.StartTracking();
        _services.StartClientUpdates();
        _services.StartPresence();
        _services.Sessions.Joined += OnJoinedServer;
        _services.Sessions.Left += OnLeftServer;
        _services.Sessions.Moved += OnMovedServer;

        _ = Task.Run(() => _services.SweepCache());

        _relief.Tick += OnMemoryRelief;
        _relief.Start();

        _pulse.Tick += OnPulse;
        _pulse.Start();

        LaunchArguments arguments = LaunchArguments.Parse(e.Args);

        if (arguments.Action is not (LaunchAction.Setup or LaunchAction.Install or LaunchAction.Uninstall))
        {
            Linger();
        }

        switch (arguments.Action)
        {
            case LaunchAction.Setup:
                ShowSetup();
                break;

            case LaunchAction.Install:
                RunInstall(arguments);
                break;

            case LaunchAction.Uninstall:
                RunUninstall(arguments);
                break;

            case LaunchAction.Background:
                if (!Watching)
                {
                    Shutdown();
                }

                break;

            case LaunchAction.LaunchPlayer:
            case LaunchAction.LaunchStudio:
                _ = RunLaunchAsync(arguments);
                break;

            default:
                ShowHome();
                break;
        }
    }

    public bool Linger()
    {
        if (_services is null || !_services.Settings.Load().StayInTray)
        {
            return false;
        }

        if (_tray is not null)
        {
            return true;
        }

        if (!_services.Instances.TryAcquire(WatchLock, out IInstanceLock held))
        {
            return false;
        }

        _watch = held;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _tray = new TrayIcon();
        _tray.Opened += (_, _) => Surface();
        _tray.Toggled += (_, _) => Flip();
        _tray.Played += (_, _) => Surface()?.LaunchPlayer();
        _tray.Quit += (_, _) => Shutdown();

        return true;
    }

    public bool Watching => _tray is not null;

    public void ApplyTray()
    {
        if (_services is null)
        {
            return;
        }

        if (_services.Settings.Load().StayInTray)
        {
            Linger();
            return;
        }

        if (_tray is null)
        {
            return;
        }

        _tray.Dispose();
        _tray = null;

        _watch?.Dispose();
        _watch = null;

        ShutdownMode = ShutdownMode.OnLastWindowClose;
    }

    private void Flip()
    {
        if (Windows.OfType<MainWindow>().FirstOrDefault() is { IsVisible: true } standing)
        {
            standing.Hide();
            return;
        }

        Surface();
    }

    private MainWindow? Surface()
    {
        if (Windows.OfType<MainWindow>().FirstOrDefault() is { } standing)
        {
            standing.Show();
            standing.WindowState = WindowState.Normal;
            standing.Activate();

            return standing;
        }

        if (!Services.Settings.Load().SetupCompleted && !Services.Installer.IsInstalled)
        {
            ShowSetup();
            return null;
        }

        var fresh = new MainWindow();
        fresh.Show();
        fresh.Activate();

        return fresh;
    }

    private void ShowHome()
    {
        if (Services.Settings.Load().SetupCompleted || Services.Installer.IsInstalled)
        {
            new MainWindow().Show();
            return;
        }

        ShowSetup();
    }

    private void ShowSetup()
    {
        var setup = new SetupWindow();
        setup.Closed += (_, _) =>
        {
            if (setup.LaunchRequested)
            {
                var home = new MainWindow();
                home.Show();
                home.LaunchPlayer();
                return;
            }

            var main = new MainWindow();
            main.Show();

            if (setup.SettingsRequested)
            {
                main.OpenSettings();
            }
        };

        setup.Show();
    }

    private void RunInstall(LaunchArguments arguments)
    {
        try
        {
            Core.Settings.NulltrapSettings settings = Services.Settings.Load();

            InstallReport report = Services.Installer.Install(
                AppServices.CurrentExecutablePath,
                AppServices.Version,
                new InstallOptions(
                    settings.DesktopShortcut,
                    settings.StartMenuShortcut,
                    RegisterPlayer: true,
                    settings.RegisterStudio));

            if (!arguments.Quiet)
            {
                string replaced = report.ReplacedAnotherLauncher && report.PreviousPlayerHandler is not null
                    ? $"\n\nIt replaced:\n{report.PreviousPlayerHandler}"
                    : string.Empty;

                MessageBox.Show(
                    $"Nulltrap is installed and now handles Roblox launches.{replaced}",
                    "Nulltrap",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception failure)
        {
            if (!arguments.Quiet)
            {
                MessageBox.Show(failure.Message, "Nulltrap", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            Environment.ExitCode = 1;
        }

        Shutdown();
    }

    private void RunUninstall(LaunchArguments arguments)
    {
        Removal removal = Removal.LauncherOnly;

        if (!arguments.Quiet)
        {
            var asking = new RemoveDialog(Services.Paths.Root);

            if (asking.ShowDialog() != true)
            {
                Shutdown();
                return;
            }

            removal = asking.Chosen;
        }

        Sweep(Services.Installer.Uninstall(removal, Services.Settings.Load().KeepDownloadCache), removal);

        if (!arguments.Quiet)
        {
            MessageBox.Show(
                Strings.Get(removal == Removal.Everything ? "remove.goneAll" : "remove.gone"),
                "Nulltrap",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        Shutdown();
    }

    public static void Sweep(UninstallReport report, Removal removal)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (removal == Removal.LauncherOnly)
        {
            Core.Settings.NulltrapSettings kept = Services.Settings.Load();

            kept.SetupCompleted = false;
            Services.Settings.Save(kept);
        }

        if (report.WaitingToGo is not null)
        {
            Services.Remover.RemoveAfterExit(report.WaitingToGo);
        }
    }

    private async Task RunLaunchAsync(LaunchArguments arguments)
    {
        if (arguments.BinaryType == Core.Deployment.BinaryType.WindowsPlayer
            && !SecondClientDialog.Allowed(null))
        {
            if (!Linger())
            {
                Shutdown();
            }

            return;
        }

        var window = new ProgressWindow();
        window.Show();

        try
        {
            BootstrapResult result = await Services.Bootstrapper.EnsureUpToDateAsync(
                arguments.BinaryType,
                Services.Settings.Load().DeploymentChannel,
                window.Progress,
                window.CancellationToken);

            Services.PrepareFor(RobloxUri.PlaceFrom(arguments.RobloxUri), result.VersionDirectory);

            int client = Services.ProcessLauncher.Start(
                result.ExecutablePath,
                ClientArguments.ForUri(arguments.BinaryType, arguments.RobloxUri),
                result.VersionDirectory);

            window.ShowWaiting(Strings.Get("progress.waitingForRoblox"));

            try
            {
                await Services.ProcessLauncher
                    .WaitForWindowAsync(client, ClientWindowTimeout, window.CancellationToken)
                    .ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                Services.ProcessLauncher.Stop(client);
                throw;
            }

            window.Close();

            if (!Linger())
            {
                Shutdown();
            }
        }
        catch (OperationCanceledException)
        {
            Shutdown();
        }
        catch (Exception failure)
        {
            window.ShowFailure(failure.Message);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _watch?.Dispose();
        _services?.Dispose();
        base.OnExit(e);
    }
}
