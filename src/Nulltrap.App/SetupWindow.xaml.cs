using System.IO;
using System.Windows;

using Nulltrap.Core.Bootstrapping;
using Nulltrap.Core.Deployment;
using Nulltrap.Core.Installation;
using Nulltrap.Core.Settings;
using Nulltrap.Platform.Abstractions;

namespace Nulltrap.App;

public partial class SetupWindow : ChromeWindow
{
    private enum Step
    {
        Welcome,
        Options,
        Download,
        Progress,
        Done,
    }

    private readonly CancellationTokenSource _cancellation = new();

    private Step _step = Step.Welcome;
    private bool _finished;

    public SetupWindow()
    {
        InitializeComponent();

        LocationText.Text = App.Services.Installer.InstalledExecutablePath;

        Render();
    }

    public bool LaunchRequested { get; private set; }

    public bool SettingsRequested { get; private set; }

    private void Render()
    {
        StepWelcome.Visibility = Visible(Step.Welcome);
        StepOptions.Visibility = Visible(Step.Options);
        StepDownload.Visibility = Visible(Step.Download);
        StepProgress.Visibility = Visible(Step.Progress);
        StepDone.Visibility = Visible(Step.Done);

        (StepTitle.Text, StepSubtitle.Text) = _step switch
        {
            Step.Welcome => ("Welcome to Nulltrap", "Read this before installing."),
            Step.Options => ("How it should behave", "All of this can be changed later."),
            Step.Download => ("What to download now", "Roblox is downloaded from Roblox's own servers."),
            Step.Progress => ("Setting up", "This takes a few minutes on a first install."),
            _ => ("All done", "Nulltrap is installed and registered."),
        };

        StepCounter.Text = _step == Step.Done ? string.Empty : $"Step {(int)_step + 1} of 4";

        BackButton.Visibility = _step is Step.Options or Step.Download
            ? Visibility.Visible
            : Visibility.Hidden;

        SecondaryButton.Visibility = _step == Step.Done ? Visibility.Visible : Visibility.Collapsed;

        NextButton.Content = _step switch
        {
            Step.Download => "Install",
            Step.Progress => "Working",
            Step.Done => "Launch Roblox",
            _ => "Continue",
        };

        NextButton.IsEnabled = _step switch
        {
            Step.Welcome => AgreeBox.IsChecked == true,
            Step.Progress => false,
            _ => true,
        };

        Visibility Visible(Step step) => _step == step ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnAgreeChanged(object sender, RoutedEventArgs e) => Render();

    private void OnBack(object sender, RoutedEventArgs e)
    {
        _step = _step == Step.Download ? Step.Options : Step.Welcome;
        Render();
    }

    private void OnNext(object sender, RoutedEventArgs e)
    {
        switch (_step)
        {
            case Step.Welcome:
                _step = Step.Options;
                Render();
                break;

            case Step.Options:
                _step = Step.Download;
                Render();
                break;

            case Step.Download:
                _step = Step.Progress;
                Render();
                _ = RunAsync();
                break;

            case Step.Done:
                LaunchRequested = true;
                _finished = true;
                Close();
                break;
        }
    }

    private void OnSecondary(object sender, RoutedEventArgs e)
    {
        SettingsRequested = true;
        _finished = true;
        Close();
    }

    private async Task RunAsync()
    {
        var summary = new List<string>();

        try
        {
            NulltrapSettings settings = App.Services.Settings.Load();
            settings.DesktopShortcut = DesktopShortcutBox.IsChecked == true;
            settings.StartMenuShortcut = StartMenuShortcutBox.IsChecked == true;

            Report("Installing Nulltrap", 0.04, App.Services.Installer.InstalledExecutablePath);

            InstallReport report = App.Services.Installer.Install(
                AppServices.CurrentExecutablePath,
                AppServices.Version,
                new InstallOptions(
                    settings.DesktopShortcut,
                    settings.StartMenuShortcut,
                    settings.RegisterStudio));

            summary.Add($"Installed to {App.Services.Paths.Root}");

            if (report.ReplacedAnotherLauncher && report.PreviousPlayerHandler is not null)
            {
                summary.Add($"Replaced {Path.GetFileName(report.PreviousPlayerHandler)} as the Roblox handler");
            }

            if (DownloadPlayerBox.IsChecked == true)
            {
                BootstrapResult player = await DownloadAsync(BinaryType.WindowsPlayer, settings, 0.05, 0.65);
                summary.Add($"Roblox {player.Version} downloaded");
            }

            if (DownloadStudioBox.IsChecked == true)
            {
                BootstrapResult studio = await DownloadAsync(BinaryType.WindowsStudio64, settings, 0.65, 1.0);
                summary.Add($"Roblox Studio {studio.Version} downloaded");
            }

            settings.SetupCompleted = true;
            App.Services.Settings.Save(settings);

            Report("Ready", 1, string.Empty);

            DoneSummary.Text = string.Join("\n", summary);
            _step = Step.Done;
            Render();
        }
        catch (OperationCanceledException)
        {
            Close();
        }
        catch (Exception failure)
        {
            ProgressStatus.Text = "Setup could not finish";
            ProgressDetail.Text = failure.Message;
            ProgressDetail.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
            NextButton.Content = "Close";
            NextButton.IsEnabled = true;
            _step = Step.Done;
            DoneSummary.Text = failure.Message;
        }
    }

    private async Task<BootstrapResult> DownloadAsync(
        BinaryType binaryType,
        NulltrapSettings settings,
        double from,
        double to)
    {
        string label = binaryType == BinaryType.WindowsPlayer ? "Roblox" : "Roblox Studio";

        var progress = new Progress<BootstrapProgress>(report =>
            Report(report.Message, from + (to - from) * report.Fraction, label));

        return await App.Services.Bootstrapper.EnsureUpToDateAsync(
            binaryType,
            settings.DeploymentChannel,
            progress,
            _cancellation.Token);
    }

    private void Report(string status, double fraction, string detail)
    {
        ProgressStatus.Text = status;
        ProgressDetail.Text = detail;

        double available = Math.Max(0, StepProgress.ActualWidth - 34);
        ProgressFill.Width = available * Math.Clamp(fraction, 0, 1);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_step == Step.Progress && !_finished)
        {
            if (MessageBox.Show(
                    "Setup is still running. Stop it?",
                    "Nulltrap",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            _cancellation.Cancel();
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _cancellation.Cancel();
        _cancellation.Dispose();
        base.OnClosed(e);
    }
}
