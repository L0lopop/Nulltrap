using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

using Nulltrap.Core.Deployment;
using Nulltrap.Core.FastFlags;
using Nulltrap.Core.Settings;
using Nulltrap.Core.State;
using Nulltrap.Platform.Abstractions;

namespace Nulltrap.App;

public partial class SettingsWindow : ChromeWindow
{
    private static readonly IReadOnlyDictionary<string, (string Title, string Subtitle)> Pages =
        new Dictionary<string, (string, string)>
        {
            ["General"] = ("General", "How Nulltrap behaves while you use it."),
            ["Integration"] = ("Integration", "Which launches Nulltrap takes over from Roblox."),
            ["Graphics"] = ("Graphics", "Client settings Roblox still lets a launcher change."),
            ["Shortcuts"] = ("Shortcuts", "Where Nulltrap appears on this machine."),
            ["Deployment"] = ("Deployment", "Which build of Roblox gets downloaded."),
            ["Storage"] = ("Storage", "What Nulltrap keeps on disk and how much of it."),
            ["About"] = ("About Nulltrap", "Version, source and removal."),
        };

    private const string FpsFlag = "DFIntTaskSchedulerTargetFps";
    private const string FpsCapFlag = "FFlagTaskSchedulerLimitTargetFpsTo2402";
    private const int DefaultTargetFps = 240;
    private const string TextureQualityEnabledFlag = "DFFlagTextureQualityOverrideEnabled";
    private const string TextureQualityFlag = "DFIntTextureQualityOverride";
    private const string MsaaFlag = "FIntDebugForceMSAASamples";
    private const string DisableDpiScaleFlag = "DFFlagDisableDPIScale";

    private readonly SettingsStore _store;
    private readonly NulltrapSettings _settings;
    private readonly FastFlagManager _fastFlags;
    private readonly Dictionary<string, string> _flags;

    private bool _loaded;

    public SettingsWindow()
    {
        InitializeComponent();

        _store = App.Services.Settings;
        _settings = _store.Load();
        _fastFlags = App.Services.FastFlags;
        _flags = _fastFlags.Load();

        CloseAfterLaunchBox.IsChecked = _settings.CloseAfterLaunch;
        ConfirmMultipleInstancesBox.IsChecked = _settings.ConfirmMultipleInstances;
        RegisterStudioBox.IsChecked = _settings.RegisterStudio;
        DesktopShortcutBox.IsChecked = _settings.DesktopShortcut;
        StartMenuShortcutBox.IsChecked = _settings.StartMenuShortcut;
        KeepDownloadCacheBox.IsChecked = _settings.KeepDownloadCache;
        ChannelBox.Text = _settings.Channel;
        AutomaticClientUpdatesBox.IsChecked = _settings.AutomaticClientUpdates;

        bool unlocked = _flags.ContainsKey(FpsFlag);
        UnlockFpsBox.IsChecked = unlocked;
        FpsBox.Text = _flags.GetValueOrDefault(FpsFlag, DefaultTargetFps.ToString());
        FpsBox.IsEnabled = unlocked;
        TextureQualityEnabledBox.IsChecked =
            _flags.GetValueOrDefault(TextureQualityEnabledFlag, "False").Equals("True", StringComparison.OrdinalIgnoreCase);
        TextureQualityBox.Text = _flags.GetValueOrDefault(TextureQualityFlag, string.Empty);
        MsaaBox.Text = _flags.GetValueOrDefault(MsaaFlag, string.Empty);
        DisableDpiScaleBox.IsChecked =
            _flags.GetValueOrDefault(DisableDpiScaleFlag, "False").Equals("True", StringComparison.OrdinalIgnoreCase);

        FpsWarning.Text = FastFlagAllowlist.IsAllowed(FpsFlag)
            ? "This setting is on Roblox's allowlist and applies."
            : "Roblox's September 2025 allowlist announcement did not include this setting, so the client "
              + "may ignore it. Every other launcher uses exactly these two flags, so if it works there it "
              + "works here. Check with Shift+F5 in game after launching.";

        _loaded = true;

        Show("General");
        RefreshFacts();
    }

    private void RefreshFacts()
    {
        SidebarVersion.Text = $"Nulltrap {AppServices.Version}";
        SidebarLocation.Text = App.Services.Paths.Root;
        AboutVersionText.Text = $"Version {AppServices.Version}";

        bool installed = App.Services.Installer.IsInstalled;
        string? handler = App.Services.Protocols.GetRegisteredHandler(LaunchTarget.Player);

        RegisterPlayerBox.IsChecked = installed;
        RegisterPlayerBox.IsEnabled = installed || handler is null;

        HandlerText.Text = handler is null
            ? "Nothing is registered. Roblox will use its own bootstrapper."
            : handler;

        InstallState state = App.Services.StateStore.Load();
        InstalledClient? player = state.Get(BinaryType.WindowsPlayer);
        InstalledClient? studio = state.Get(BinaryType.WindowsStudio64);

        InstalledClientsText.Text = string.Join(
            "\n",
            $"Player: {(player is null ? "not downloaded" : $"{player.Version} ({player.VersionGuid})")}",
            $"Studio: {(studio is null ? "not downloaded" : $"{studio.Version} ({studio.VersionGuid})")}");

        string[] applied = _flags.Keys.Where(FastFlagAllowlist.IsAllowed).ToArray();
        string[] ignored = FastFlagAllowlist.RejectedIn(_flags.Keys).ToArray();

        FlagSummaryText.Text = _flags.Count == 0
            ? "No client settings are set, so Roblox runs with its own defaults."
            : $"{applied.Length} of {_flags.Count} settings are on Roblox's allowlist and will apply."
              + (ignored.Length == 0 ? string.Empty : $" Ignored by the client: {string.Join(", ", ignored)}.");

        CacheSizeText.Text = Describe(App.Services.Paths.Downloads, "packages");
        VersionsSizeText.Text = Describe(App.Services.Paths.Versions, "files");
        ClearCacheButton.IsEnabled = Directory.Exists(App.Services.Paths.Downloads)
            && Directory.EnumerateFiles(App.Services.Paths.Downloads).Any();
    }

    private static string Describe(string path, string noun)
    {
        if (!Directory.Exists(path))
        {
            return "nothing stored";
        }

        FileInfo[] files = new DirectoryInfo(path).GetFiles("*", SearchOption.AllDirectories);

        return files.Length == 0
            ? "nothing stored"
            : $"{files.Length:N0} {noun}, {files.Sum(file => file.Length) / 1024.0 / 1024.0:N0} MB";
    }

    private void Show(string page)
    {
        PageGeneral.Visibility = page == "General" ? Visibility.Visible : Visibility.Collapsed;
        PageIntegration.Visibility = page == "Integration" ? Visibility.Visible : Visibility.Collapsed;
        PageGraphics.Visibility = page == "Graphics" ? Visibility.Visible : Visibility.Collapsed;
        PageShortcuts.Visibility = page == "Shortcuts" ? Visibility.Visible : Visibility.Collapsed;
        PageDeployment.Visibility = page == "Deployment" ? Visibility.Visible : Visibility.Collapsed;
        PageStorage.Visibility = page == "Storage" ? Visibility.Visible : Visibility.Collapsed;
        PageAbout.Visibility = page == "About" ? Visibility.Visible : Visibility.Collapsed;

        (string title, string subtitle) = Pages[page];
        PageTitle.Text = title;
        PageSubtitle.Text = subtitle;
    }

    private void OnNavigate(object sender, RoutedEventArgs e)
    {
        if (_loaded && sender is RadioButton { Tag: string page })
        {
            Show(page);
            RefreshFacts();
        }
    }

    private void OnRegistrationChanged(object sender, RoutedEventArgs e)
    {
        if (!_loaded)
        {
            return;
        }

        try
        {
            string handler = App.Services.Installer.InstalledExecutablePath;

            if (sender == RegisterPlayerBox)
            {
                if (RegisterPlayerBox.IsChecked == true)
                {
                    App.Services.Protocols.Register(LaunchTarget.Player, handler);
                }
                else
                {
                    App.Services.Protocols.Unregister(LaunchTarget.Player);
                }
            }
            else
            {
                if (RegisterStudioBox.IsChecked == true)
                {
                    App.Services.Protocols.Register(LaunchTarget.Studio, handler);
                }
                else
                {
                    App.Services.Protocols.Unregister(LaunchTarget.Studio);
                }
            }
        }
        catch (Exception failure)
        {
            MessageBox.Show(failure.Message, "Nulltrap", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        RefreshFacts();
    }

    private void OnShortcutsChanged(object sender, RoutedEventArgs e)
    {
        if (!_loaded)
        {
            return;
        }

        string target = App.Services.Installer.InstalledExecutablePath;

        Apply(ShortcutLocation.Desktop, DesktopShortcutBox.IsChecked == true);
        Apply(ShortcutLocation.StartMenu, StartMenuShortcutBox.IsChecked == true);

        void Apply(ShortcutLocation location, bool wanted)
        {
            try
            {
                if (wanted && File.Exists(target))
                {
                    App.Services.Shortcuts.Create(location, "Nulltrap", target, string.Empty, "Nulltrap");
                }
                else if (!wanted)
                {
                    App.Services.Shortcuts.Remove(location, "Nulltrap");
                }
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private void OnClearCache(object sender, RoutedEventArgs e)
    {
        try
        {
            foreach (string file in Directory.GetFiles(App.Services.Paths.Downloads))
            {
                File.Delete(file);
            }
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(failure.Message, "Nulltrap", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        RefreshFacts();
    }

    private void OnOpenFolder(object sender, RoutedEventArgs e) => Open(App.Services.Paths.Root);

    private void OnOpenRepository(object sender, RoutedEventArgs e) =>
        Open("https://github.com/L0lopop/Nulltrap");

    private static void Open(string target)
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

    private void OnUninstall(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                "Remove Nulltrap and the Roblox client it downloaded?",
                "Nulltrap",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        App.Services.Installer.Uninstall(_settings.KeepDownloadCache);
        DialogResult = true;
        Close();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        _settings.CloseAfterLaunch = CloseAfterLaunchBox.IsChecked == true;
        _settings.ConfirmMultipleInstances = ConfirmMultipleInstancesBox.IsChecked == true;
        _settings.RegisterStudio = RegisterStudioBox.IsChecked == true;
        _settings.DesktopShortcut = DesktopShortcutBox.IsChecked == true;
        _settings.StartMenuShortcut = StartMenuShortcutBox.IsChecked == true;
        _settings.KeepDownloadCache = KeepDownloadCacheBox.IsChecked == true;
        _settings.Channel = string.IsNullOrWhiteSpace(ChannelBox.Text)
            ? DeploymentChannel.DefaultName
            : ChannelBox.Text.Trim();
        _settings.AutomaticClientUpdates = AutomaticClientUpdatesBox.IsChecked == true;

        _store.Save(_settings);

        ApplyFrameRate();
        SetFlag(TextureQualityEnabledFlag, TextureQualityEnabledBox.IsChecked == true ? "True" : string.Empty);
        SetFlag(TextureQualityFlag, TextureQualityBox.Text);
        SetFlag(MsaaFlag, MsaaBox.Text);
        SetFlag(DisableDpiScaleFlag, DisableDpiScaleBox.IsChecked == true ? "True" : string.Empty);

        _fastFlags.Save(_flags);

        DialogResult = true;
        Close();
    }

    private void OnUnlockFpsChanged(object sender, RoutedEventArgs e)
    {
        FpsBox.IsEnabled = UnlockFpsBox.IsChecked == true;

        if (FpsBox.IsEnabled && string.IsNullOrWhiteSpace(FpsBox.Text))
        {
            FpsBox.Text = DefaultTargetFps.ToString();
        }
    }

    private void ApplyFrameRate()
    {
        if (UnlockFpsBox.IsChecked != true
            || !int.TryParse(FpsBox.Text.Trim(), out int target)
            || target <= 0)
        {
            _flags.Remove(FpsFlag);
            _flags.Remove(FpsCapFlag);
            return;
        }

        _flags[FpsFlag] = target.ToString();

        // Roblox clamps the scheduler to 240 unless this is turned off.
        if (target > DefaultTargetFps)
        {
            _flags[FpsCapFlag] = "False";
        }
        else
        {
            _flags.Remove(FpsCapFlag);
        }
    }

    private void SetFlag(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _flags.Remove(name);
            return;
        }

        _flags[name] = value.Trim();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
