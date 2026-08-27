using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using Nulltrap.Core.Bootstrapping;
using Nulltrap.Core.Deployment;
using Nulltrap.Core.Installation;
using Nulltrap.Core.Settings;
using Nulltrap.Core.State;
using Nulltrap.Platform.Abstractions;

namespace Nulltrap.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Refresh();
    }

    private void Refresh()
    {
        StatusPanel.Children.Clear();

        Installer installer = App.Services.Installer;
        bool installed = installer.IsInstalled;
        InstallState state = App.Services.StateStore.Load();
        NulltrapSettings settings = App.Services.Settings.Load();

        AddRow("Nulltrap", AppServices.Version);
        AddRow("Installed", installed ? "yes" : "no");
        AddRow("Location", App.Services.Paths.Root);
        AddRow("Channel", settings.Channel);

        string? handler = App.Services.Protocols.GetRegisteredHandler(LaunchTarget.Player);
        AddRow("Roblox opens with", handler ?? "the official bootstrapper");

        InstalledClient? player = state.Get(BinaryType.WindowsPlayer);
        InstalledClient? studio = state.Get(BinaryType.WindowsStudio64);

        PlayHint.Text = player is null ? "not downloaded" : player.Version;
        StudioHint.Text = studio is null ? "not downloaded" : studio.Version;

        InstallButton.Content = installed ? "Uninstall" : "Install";

        FooterText.Text = installed
            ? "Nulltrap handles Roblox launches on this account."
            : "Not installed yet. Installing needs no administrator rights.";
    }

    private void AddRow(string label, string value)
    {
        var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelBlock = new TextBlock
        {
            Text = label,
            FontSize = 12,
            Foreground = (Brush)FindResource("TextSoftBrush"),
        };

        var valueBlock = new TextBlock
        {
            Text = value,
            FontSize = 12,
            FontFamily = new FontFamily("Consolas, Cascadia Mono, Courier New"),
            Foreground = (Brush)FindResource("TextBrush"),
            TextWrapping = TextWrapping.Wrap,
        };

        Grid.SetColumn(valueBlock, 1);
        row.Children.Add(labelBlock);
        row.Children.Add(valueBlock);

        StatusPanel.Children.Add(row);
    }

    private void OnLaunchPlayer(object sender, RoutedEventArgs e) => _ = LaunchAsync(BinaryType.WindowsPlayer);

    private void OnLaunchStudio(object sender, RoutedEventArgs e) => _ = LaunchAsync(BinaryType.WindowsStudio64);

    private void OnSettings(object sender, RoutedEventArgs e)
    {
        new SettingsWindow { Owner = this }.ShowDialog();
        Refresh();
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
        Installer installer = App.Services.Installer;

        try
        {
            if (installer.IsInstalled)
            {
                if (MessageBox.Show(
                        "Remove Nulltrap and the Roblox client it downloaded?",
                        "Nulltrap",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    return;
                }

                installer.Uninstall(App.Services.Settings.Load().KeepDownloadCache);
            }
            else
            {
                NulltrapSettings settings = App.Services.Settings.Load();

                InstallReport report = installer.Install(
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
                        $"Nulltrap now handles Roblox launches.\n\nIt replaced:\n{report.PreviousPlayerHandler}",
                        "Nulltrap",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
        }
        catch (Exception failure)
        {
            MessageBox.Show(failure.Message, "Nulltrap", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        Refresh();
    }
}
