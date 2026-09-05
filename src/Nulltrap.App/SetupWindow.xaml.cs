using System.IO;
using System.Windows;

using Nulltrap.Core.Bootstrapping;
using Nulltrap.Core.Deployment;
using Nulltrap.Core.Installation;
using Nulltrap.Core.Localization;
using Nulltrap.Core.Modifications;
using Nulltrap.Core.Settings;
using Nulltrap.Platform.Abstractions;

namespace Nulltrap.App;

public partial class SetupWindow : ChromeWindow
{
    private enum Step
    {
        Welcome,
        Options,
        Mods,
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

        OfferMods();
        Render();
    }

    private readonly Dictionary<string, System.Windows.Controls.CheckBox> _offered =
        new(StringComparer.OrdinalIgnoreCase);

    public bool LaunchRequested { get; private set; }

    public bool SettingsRequested { get; private set; }

    private void Render()
    {
        StepWelcome.Visibility = Visible(Step.Welcome);
        StepOptions.Visibility = Visible(Step.Options);
        StepMods.Visibility = Visible(Step.Mods);
        StepDownload.Visibility = Visible(Step.Download);
        StepProgress.Visibility = Visible(Step.Progress);
        StepDone.Visibility = Visible(Step.Done);

        (StepTitle.Text, StepSubtitle.Text) = _step switch
        {
            Step.Welcome => (Strings.Get("setup.welcomeTitle"), Strings.Get("setup.welcomeSub")),
            Step.Options => (Strings.Get("setup.optionsTitle"), Strings.Get("setup.optionsSub")),
            Step.Mods => (Strings.Get("setup.modsTitle"), Strings.Get("setup.modsSub")),
            Step.Download => (Strings.Get("setup.downloadTitle"), Strings.Get("setup.downloadSub")),
            Step.Progress => (Strings.Get("setup.progressTitle"), Strings.Get("setup.progressSub")),
            _ => (Strings.Get("setup.doneTitle"), Strings.Get("setup.doneSub")),
        };

        StepCounter.Text = _step == Step.Done
            ? string.Empty
            : Strings.Get("setup.step", (int)_step + 1, (int)Step.Progress + 1);

        BackButton.Visibility = _step is Step.Options or Step.Mods or Step.Download
            ? Visibility.Visible
            : Visibility.Hidden;

        SecondaryButton.Visibility = _step == Step.Done ? Visibility.Visible : Visibility.Collapsed;

        NextButton.Content = _step switch
        {
            Step.Download => Strings.Get("setup.install"),
            Step.Progress => Strings.Get("setup.working"),
            Step.Done => Strings.Get("setup.launch"),
            _ => Strings.Get("action.continue"),
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
        _step = _step switch
        {
            Step.Download => Step.Mods,
            Step.Mods => Step.Options,
            _ => Step.Welcome,
        };

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
                _step = Step.Mods;
                Render();
                break;

            case Step.Mods:
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

    private void OfferMods()
    {
        if (BuiltInMods.All.Count == 0)
        {
            ModsOffered.Children.Add(new System.Windows.Controls.TextBlock
            {
                Style = (Style)FindResource("RowHint"),
                Margin = new Thickness(4, 0, 0, 0),
                Text = Strings.Get("builtin.none"),
            });

            return;
        }

        foreach (BuiltInMod mod in BuiltInMods.All)
        {
            var title = new System.Windows.Controls.TextBlock
            {
                Style = (Style)FindResource("RowTitle"),
                Text = Strings.Get(mod.NameKey),
            };

            var hint = new System.Windows.Controls.TextBlock
            {
                Style = (Style)FindResource("RowHint"),
                Text = Strings.Get(mod.HintKey),
            };

            var words = new System.Windows.Controls.StackPanel { Margin = new Thickness(0, 0, 60, 0) };

            words.Children.Add(title);
            words.Children.Add(hint);

            var box = new System.Windows.Controls.CheckBox
            {
                Style = (Style)FindResource("Switch"),
                HorizontalAlignment = HorizontalAlignment.Right,
            };

            var row = new System.Windows.Controls.Grid();

            row.Children.Add(words);
            row.Children.Add(box);

            ModsOffered.Children.Add(new System.Windows.Controls.Border
            {
                Style = (Style)FindResource("RowCard"),
                Child = row,
            });

            System.Windows.Automation.AutomationProperties.SetAutomationId(box, "Offer_" + mod.Id);

            _offered[mod.Id] = box;
        }
    }

    private int TakeMods()
    {
        string folder = App.Services.Mods.SourceDirectory;
        int taken = 0;

        foreach ((string id, System.Windows.Controls.CheckBox box) in _offered)
        {
            if (box.IsChecked == true && BuiltInMods.Apply(id, folder))
            {
                taken++;
            }
        }

        return taken;
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

            int picked = TakeMods();

            if (picked > 0)
            {
                summary.Add(Strings.Get("setup.modsPicked", picked));
            }

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
