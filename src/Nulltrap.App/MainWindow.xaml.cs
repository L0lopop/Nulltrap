using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using Nulltrap.Core.Bootstrapping;
using Nulltrap.Core.Deployment;
using Nulltrap.Core.Installation;
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

        AddRow("Nulltrap", AppServices.Version);
        AddRow("Installed", installed ? "yes" : "no");
        AddRow("Location", App.Services.Paths.Root);

        string? handler = App.Services.Protocols.GetRegisteredHandler(LaunchTarget.Player);
        AddRow("Roblox opens with", handler ?? "the official bootstrapper");

        InstalledClient? client = App.Services.StateStore.Load().Get(BinaryType.WindowsPlayer);
        AddRow("Roblox client", client is null ? "not downloaded yet" : $"{client.Version}");

        InstallButton.Content = installed ? "Uninstall" : "Install";

        FooterText.Text = installed
            ? "Nulltrap handles Roblox launches on this account. Uninstalling restores the previous handler."
            : "Installing copies Nulltrap into your user profile and makes it handle Roblox launches. "
              + "No administrator rights are needed.";
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

                installer.Uninstall();
            }
            else
            {
                InstallReport report = installer.Install(
                    AppServices.CurrentExecutablePath,
                    AppServices.Version);

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

    private async void OnLaunch(object sender, RoutedEventArgs e)
    {
        LaunchButton.IsEnabled = false;

        var window = new ProgressWindow { Owner = this };
        window.Show();

        try
        {
            BootstrapResult result = await App.Services.Bootstrapper.EnsureUpToDateAsync(
                BinaryType.WindowsPlayer,
                cancellationToken: window.CancellationToken,
                progress: window.Progress);

            App.Services.ProcessLauncher.Start(result.ExecutablePath, string.Empty, result.VersionDirectory);
            window.Close();
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
            LaunchButton.IsEnabled = true;
            Refresh();
        }
    }
}
