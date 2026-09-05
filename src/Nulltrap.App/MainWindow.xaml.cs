using System.Diagnostics;
using System.IO;
using System.Windows;

using Nulltrap.Core.Bootstrapping;
using Nulltrap.Core.Deployment;
using Nulltrap.Core.Installation;
using Nulltrap.Core.Launching;
using Nulltrap.Core.Localization;
using Nulltrap.Core.Settings;
using Nulltrap.Core.State;
using Nulltrap.Platform.Abstractions;

namespace Nulltrap.App;

public partial class MainWindow : ChromeWindow
{
    private const string RepositoryUrl = "https://github.com/L0lopop/Nulltrap";

    private static readonly TimeSpan ClientWindowTimeout = TimeSpan.FromMinutes(3);

    public MainWindow()
    {
        InitializeComponent();
        Refresh();
    }

    private void Refresh()
    {
        Installer installer = App.Services.Installer;
        InstallState state = App.Services.StateStore.Load();
        NulltrapSettings settings = App.Services.Settings.Load();

        bool installed = installer.IsInstalled;

        VersionText.Text = $"Version {AppServices.Version}";

        InstalledClient? player = state.Get(BinaryType.WindowsPlayer);
        InstalledClient? studio = state.Get(BinaryType.WindowsStudio64);

        PlayHint.Text = player is null
            ? Strings.Get("home.firstLaunch")
            : Strings.Get("home.robloxVersion", player.Version);

        StudioHint.Text = studio is null ? Strings.Get("deployment.notDownloaded") : studio.Version;

        ClientText.Text = settings.Channel == DeploymentChannel.DefaultName
            ? string.Empty
            : Strings.Get("home.channel", settings.Channel);


        if (installed)
        {
            InstallLink.Visibility = Visibility.Collapsed;
        }
        else
        {
            InstallLink.Visibility = Visibility.Visible;
        }
    }

    public void LaunchPlayer() => _ = LaunchAsync(BinaryType.WindowsPlayer);

    private void Retire()
    {
        if (Application.Current is App app && app.Linger())
        {
            Hide();
            return;
        }

        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (Application.Current is App { Watching: true })
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    public void LaunchGame(long placeId) => _ = LaunchAsync(BinaryType.WindowsPlayer, placeId);

    public void OpenSettings() => OpenSettings("Home");

    private void OnLaunchPlayer(object sender, RoutedEventArgs e) => _ = LaunchAsync(BinaryType.WindowsPlayer);

    private void OnLaunchStudio(object sender, RoutedEventArgs e) => _ = LaunchAsync(BinaryType.WindowsStudio64);

    private void OnSettings(object sender, RoutedEventArgs e) => OpenSettings("Home");

    private void OnAbout(object sender, RoutedEventArgs e) => OpenSettings("About");

    private void OpenSettings(string page)
    {
        var settings = new SettingsWindow { Owner = this };
        settings.GoTo(page);
        settings.ShowDialog();
        Refresh();
    }

    private void OnOpenRepository(object sender, RoutedEventArgs e) => OpenExternal(RepositoryUrl);

    private void OnOpenInstallFolder(object sender, RoutedEventArgs e)
    {
        string root = App.Services.Paths.Root;

        if (Directory.Exists(root))
        {
            OpenExternal(root);
        }
    }

    private static void OpenExternal(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception failure)
        {
            MessageBox.Show(failure.Message, "Nulltrap", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task LaunchAsync(BinaryType binaryType, long placeId = 0)
    {
        if (binaryType == BinaryType.WindowsPlayer && !SecondClientDialog.Allowed(this))
        {
            return;
        }

        PlayButton.IsEnabled = false;
        StudioButton.IsEnabled = false;

        var window = new ProgressWindow { Owner = this };
        window.Show();

        try
        {
            NulltrapSettings settings = App.Services.Settings.Load();

            BootstrapResult result = await App.Services.Bootstrapper.EnsureUpToDateAsync(
                binaryType,
                settings.DeploymentChannel,
                window.Progress,
                window.CancellationToken);

            App.Services.PrepareFor(placeId, result.VersionDirectory);

            int client = App.Services.ProcessLauncher.Start(
                result.ExecutablePath,
                placeId > 0 ? ClientArguments.ForGame(placeId) : ClientArguments.ForMenu(binaryType),
                result.VersionDirectory);

            window.ShowWaiting(Strings.Get("progress.waitingForRoblox"));

            try
            {
                await App.Services.ProcessLauncher
                    .WaitForWindowAsync(client, ClientWindowTimeout, window.CancellationToken)
                    .ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                App.Services.ProcessLauncher.Stop(client);
                throw;
            }

            window.Close();

            if (settings.CloseAfterLaunch)
            {
                Retire();
                return;
            }

            if (settings.TrimMemory)
            {
                App.Services.Memory.Trim();
            }
        }
        catch (OperationCanceledException)
        {
            window.Close();
        }
        catch (Exception failure)
        {
            window.ShowFailure(failure.Message);
        }
        finally
        {
            PlayButton.IsEnabled = true;
            StudioButton.IsEnabled = true;
            Refresh();
        }
    }

    private void OnInstall(object sender, RoutedEventArgs e)
    {
        try
        {
            NulltrapSettings settings = App.Services.Settings.Load();

            InstallReport report = App.Services.Installer.Install(
                AppServices.CurrentExecutablePath,
                AppServices.Version,
                new InstallOptions(
                    settings.DesktopShortcut,
                    settings.StartMenuShortcut,
                    RegisterPlayer: true,
                    settings.RegisterStudio));

            if (report.ReplacedAnotherLauncher && report.PreviousPlayerHandler is not null)
            {
                MessageBox.Show(
                    Strings.Get("install.replaced", report.PreviousPlayerHandler),
                    "Nulltrap",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception failure)
        {
            MessageBox.Show(failure.Message, "Nulltrap", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        Refresh();
    }
}
