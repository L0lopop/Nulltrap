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

    private readonly System.Windows.Threading.DispatcherTimer _relief = new() { Interval = MemoryRelief };

    private AppServices? _services;

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

        if (!notice && !listening)
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

            if (!notice)
            {
                return;
            }

            Core.Roblox.ServerFacts? facts = await _services.Servers
                .FindAsync(session.PlaceId, session.JobId)
                .ConfigureAwait(true);

            NoticeWindow.Announce(
                game?.Name ?? Core.Localization.Strings.Get("activity.unknownGame"),
                Where(place, game),
                Numbers(facts),
                game?.IconUrl);
        });
    }

    private void OnLeftServer(object? sender, Core.Sessions.RobloxSession session)
    {
        if (_services is null)
        {
            return;
        }

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

        _ = Task.Run(() => _services.SweepCache());

        _relief.Tick += OnMemoryRelief;
        _relief.Start();

        LaunchArguments arguments = LaunchArguments.Parse(e.Args);

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

            case LaunchAction.LaunchPlayer:
            case LaunchAction.LaunchStudio:
                _ = RunLaunchAsync(arguments);
                break;

            default:
                ShowHome();
                break;
        }
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
            Shutdown();
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
        _services?.Dispose();
        base.OnExit(e);
    }
}
