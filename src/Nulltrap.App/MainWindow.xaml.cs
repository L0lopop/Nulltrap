using System.Diagnostics;
using System.IO;
using System.Windows;

using Nulltrap.Core.Bootstrapping;
using Nulltrap.Core.Deployment;
using Nulltrap.Core.Installation;
using Nulltrap.Core.Localization;
using Nulltrap.Core.Settings;
using Nulltrap.Core.State;
using Nulltrap.Platform.Abstractions;

namespace Nulltrap.App;

public partial class MainWindow : ChromeWindow
{
    private const string RepositoryUrl = "https://github.com/L0lopop/Nulltrap";

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

    public void OpenSettings() => OpenSettings("Graphics");

    private void OnLaunchPlayer(object sender, RoutedEventArgs e) => _ = LaunchAsync(BinaryType.WindowsPlayer);

    private void OnLaunchStudio(object sender, RoutedEventArgs e) => _ = LaunchAsync(BinaryType.WindowsStudio64);

    private void OnSettings(object sender, RoutedEventArgs e) => OpenSettings("Graphics");

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

    private async Task LaunchAsync(BinaryType binaryType)
    {
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

            App.Services.ProcessLauncher.Start(result.ExecutablePath, string.Empty, result.VersionDirectory);
            window.Close();

            if (settings.CloseAfterLaunch)
            {
                Close();
                return;
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
