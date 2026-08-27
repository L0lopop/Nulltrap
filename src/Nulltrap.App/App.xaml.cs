using System.Windows;

using Nulltrap.Core.Bootstrapping;
using Nulltrap.Platform.Abstractions;

namespace Nulltrap.App;

public partial class App : Application
{
    private AppServices? _services;

    public static AppServices Services =>
        (Current as App)?._services
        ?? throw new InvalidOperationException("Services are not available yet.");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _services = new AppServices();
        LaunchArguments arguments = LaunchArguments.Parse(e.Args);

        switch (arguments.Action)
        {
            case LaunchAction.Uninstall:
                RunUninstall(arguments);
                break;

            case LaunchAction.LaunchPlayer:
            case LaunchAction.LaunchStudio:
                _ = RunLaunchAsync(arguments);
                break;

            default:
                new MainWindow().Show();
                break;
        }
    }

    private void RunUninstall(LaunchArguments arguments)
    {
        if (!arguments.Quiet)
        {
            MessageBoxResult answer = MessageBox.Show(
                "Remove Nulltrap and the Roblox client it downloaded?",
                "Nulltrap",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
            {
                Shutdown();
                return;
            }
        }

        Services.Installer.Uninstall();

        if (!arguments.Quiet)
        {
            MessageBox.Show("Nulltrap has been removed.", "Nulltrap", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        Shutdown();
    }

    private async Task RunLaunchAsync(LaunchArguments arguments)
    {
        var window = new ProgressWindow();
        window.Show();

        try
        {
            BootstrapResult result = await Services.Bootstrapper.EnsureUpToDateAsync(
                arguments.BinaryType,
                cancellationToken: window.CancellationToken,
                progress: window.Progress);

            Services.ProcessLauncher.Start(
                result.ExecutablePath,
                arguments.RobloxUri ?? string.Empty,
                result.VersionDirectory);

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
