using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

using Nulltrap.Core.Deployment;
using Nulltrap.Core.FastFlags;
using Nulltrap.Core.Installation;
using Nulltrap.Core.Localization;
using Nulltrap.Core.Presence;
using Nulltrap.Core.Settings;
using Nulltrap.Core.State;
using Nulltrap.Platform.Abstractions;

namespace Nulltrap.App;

public partial class SettingsWindow : ChromeWindow
{
    private const string FpsFlag = "DFIntTaskSchedulerTargetFps";
    private const string FpsCapFlag = "FFlagTaskSchedulerLimitTargetFpsTo2402";
    private const int DefaultTargetFps = 240;
    private const string TextureQualityEnabledFlag = "DFFlagTextureQualityOverrideEnabled";
    private const string TextureQualityFlag = "DFIntTextureQualityOverride";
    private const string MsaaFlag = "FIntDebugForceMSAASamples";
    private const string DisableDpiScaleFlag = "DFFlagDisableDPIScale";
    private const string PreferD3D11Flag = "FFlagDebugGraphicsPreferD3D11";
    private const string PreferVulkanFlag = "FFlagDebugGraphicsPreferVulkan";
    private const string PreferOpenGLFlag = "FFlagDebugGraphicsPreferOpenGL";
    private const string MaxGrassFlag = "FIntFRMMaxGrassDistance";
    private const string MinGrassFlag = "FIntFRMMinGrassDistance";

    private static readonly (string? Value, string Key)[] GraphicsApis =
    [
        (null, "graphics.apiAuto"),
        (PreferD3D11Flag, "graphics.apiD3D11"),
        (PreferVulkanFlag, "graphics.apiVulkan"),
        (PreferOpenGLFlag, "graphics.apiOpenGL"),
    ];

    private static readonly (string? Value, string Key)[] GrassDistances =
    [
        (null, "graphics.automatic"),
        ("0", "graphics.grassOff"),
        ("40", "graphics.grassNear"),
        ("80", "graphics.grassNormal"),
        ("200", "graphics.grassFar"),
    ];

    private static readonly (string? Value, string Key)[] MsaaLevels =
    [
        (null, "graphics.automatic"),
        ("1", "graphics.msaa1"),
        ("2", "graphics.msaa2"),
        ("4", "graphics.msaa4"),
    ];

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
        PresenceEnabledBox.IsChecked = _settings.DiscordPresence;
        PresenceButtonBox.IsChecked = _settings.DiscordShowGameButton;
        PresenceAppIdBox.Text = _settings.DiscordApplicationId;

        TextureQualityEnabledBox.IsChecked =
            _flags.GetValueOrDefault(TextureQualityEnabledFlag, "False").Equals("True", StringComparison.OrdinalIgnoreCase);

        bool unlocked = _flags.ContainsKey(FpsFlag);
        UnlockFpsBox.IsChecked = unlocked;
        FpsBox.Text = _flags.GetValueOrDefault(FpsFlag, DefaultTargetFps.ToString());
        FpsBox.IsEnabled = unlocked;
        FillChoices(MsaaBox, MsaaLevels, _flags.GetValueOrDefault(MsaaFlag));
        FillChoices(GrassBox, GrassDistances, _flags.GetValueOrDefault(MaxGrassFlag));
        FillChoices(GraphicsApis_Box(), GraphicsApis, ChosenGraphicsApi());

        bool overridingTexture =
            _flags.GetValueOrDefault(TextureQualityEnabledFlag, "False").Equals("True", StringComparison.OrdinalIgnoreCase);
        TextureQualitySlider.Value =
            int.TryParse(_flags.GetValueOrDefault(TextureQualityFlag), out int level) ? Math.Clamp(level, 0, 9) : 3;
        TextureQualitySlider.IsEnabled = overridingTexture;
        ShowTextureQuality();
        DisableDpiScaleBox.IsChecked =
            _flags.GetValueOrDefault(DisableDpiScaleFlag, "False").Equals("True", StringComparison.OrdinalIgnoreCase);

        FpsWarning.Text = Strings.Get(
            FastFlagAllowlist.IsAllowed(FpsFlag) ? "graphics.fpsAllowed" : "graphics.fpsUnlisted");

        BuildLanguageButtons();
        BuildChangelog();

        _loaded = true;

        Show("Graphics");
        RefreshFacts();
    }

    private static void FillChoices(ComboBox box, (string? Value, string Key)[] choices, string? current)
    {
        box.Items.Clear();

        foreach ((string? value, string key) in choices)
        {
            var item = new ComboBoxItem { Content = Strings.Get(key), Tag = value };
            box.Items.Add(item);

            if (value == current)
            {
                box.SelectedItem = item;
            }
        }

        box.SelectedItem ??= box.Items[0];
    }

    private static string? ChosenValue(ComboBox box) =>
        (box.SelectedItem as ComboBoxItem)?.Tag as string;

    private ComboBox GraphicsApis_Box() => GraphicsApiBox;

    private string? ChosenGraphicsApi()
    {
        foreach ((string? flag, _) in GraphicsApis)
        {
            if (flag is not null
                && _flags.GetValueOrDefault(flag, "False").Equals("True", StringComparison.OrdinalIgnoreCase))
            {
                return flag;
            }
        }

        return null;
    }

    private void ShowTextureQuality()
    {
        TextureQualityValue.Text = TextureQualitySlider.IsEnabled
            ? Strings.Get("graphics.qualityLevel", (int)TextureQualitySlider.Value)
            : Strings.Get("graphics.qualityAuto");
    }

    private void OnTextureQualityToggled(object sender, RoutedEventArgs e)
    {
        TextureQualitySlider.IsEnabled = TextureQualityEnabledBox.IsChecked == true;
        ShowTextureQuality();
    }

    private void OnTextureQualityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loaded)
        {
            ShowTextureQuality();
        }
    }

    private void BuildChangelog()
    {
        ChangelogPanel.Children.Clear();

        foreach (ChangelogEntry entry in Changelog.Entries)
        {
            var heading = new TextBlock
            {
                Text = entry.Date is null ? entry.Version : $"{entry.Version} — {entry.Date}",
                FontSize = 12,
                Margin = new Thickness(0, 4, 0, 6),
                Foreground = (System.Windows.Media.Brush)FindResource("PurpleBrightBrush"),
            };
            ChangelogPanel.Children.Add(heading);

            foreach (string line in entry.For(_settings.Language))
            {
                ChangelogPanel.Children.Add(new TextBlock
                {
                    Text = "·  " + line,
                    FontSize = 11,
                    Margin = new Thickness(0, 0, 0, 3),
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = (System.Windows.Media.Brush)FindResource("TextSoftBrush"),
                });
            }
        }
    }

    private void BuildLanguageButtons()
    {
        LanguageButtons.Children.Clear();

        foreach (Language language in Strings.Available)
        {
            bool active = language.Code == _settings.Language;

            var button = new System.Windows.Controls.Button
            {
                Content = language.NativeName,
                Style = (Style)FindResource(active ? "Accent" : "Quiet"),
                Margin = new Thickness(0, 0, 8, 0),
                MinWidth = 92,
                Tag = language.Code,
            };

            button.Click += OnLanguageChosen;
            LanguageButtons.Children.Add(button);
        }
    }

    private void OnLanguageChosen(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string code } || code == _settings.Language)
        {
            return;
        }

        _settings.Language = code;
        _store.Save(_settings);
        Strings.Use(code);

        var replacement = new SettingsWindow { Owner = Owner };
        Close();
        replacement.ShowDialog();
    }

    private void RefreshFacts()
    {
        SidebarVersion.Text = $"Nulltrap {AppServices.Version}";
        SidebarLocation.Text = App.Services.Paths.Root;
        LauncherLocationText.Text = App.Services.Paths.Root;
        PresenceStatusText.Text = DescribePresence();
        RepairButton.IsEnabled = App.Services.Installer.IsInstalled;
        AboutVersionText.Text = $"Version {AppServices.Version}";

        bool installed = App.Services.Installer.IsInstalled;
        string? handler = App.Services.Protocols.GetRegisteredHandler(LaunchTarget.Player);

        RegisterPlayerBox.IsChecked = installed;
        RegisterPlayerBox.IsEnabled = installed || handler is null;

        HandlerText.Text = handler ?? Strings.Get("integration.noHandler");

        InstallState state = App.Services.StateStore.Load();
        InstalledClient? player = state.Get(BinaryType.WindowsPlayer);
        InstalledClient? studio = state.Get(BinaryType.WindowsStudio64);

        InstalledClientsText.Text = string.Join(
            "\n",
            $"Player: {(player is null ? Strings.Get("deployment.notDownloaded") : $"{player.Version} ({player.VersionGuid})")}",
            $"Studio: {(studio is null ? Strings.Get("deployment.notDownloaded") : $"{studio.Version} ({studio.VersionGuid})")}");

        string[] applied = _flags.Keys.Where(FastFlagAllowlist.IsAllowed).ToArray();
        string[] ignored = FastFlagAllowlist.RejectedIn(_flags.Keys).ToArray();

        FlagSummaryText.Text = _flags.Count == 0
            ? Strings.Get("graphics.noFlags")
            : Strings.Get("graphics.flagsApplied", applied.Length, _flags.Count)
              + (ignored.Length == 0
                  ? string.Empty
                  : Strings.Get("graphics.flagsIgnored", string.Join(", ", ignored)));

        CacheSizeText.Text = Describe(App.Services.Paths.Downloads, Strings.Get("storage.packages"));
        VersionsSizeText.Text = Describe(App.Services.Paths.Versions, Strings.Get("storage.files"));
        ClearCacheButton.IsEnabled = Directory.Exists(App.Services.Paths.Downloads)
            && Directory.EnumerateFiles(App.Services.Paths.Downloads).Any();
    }

    private string DescribePresence()
    {
        if (!_settings.DiscordPresence)
        {
            return Strings.Get("presence.off");
        }

        if (string.IsNullOrWhiteSpace(_settings.DiscordApplicationId))
        {
            return Strings.Get("presence.notConfigured");
        }

        PresenceActivity? showing = App.Services.Presence?.Last;

        return showing?.Details is null
            ? Strings.Get("presence.waiting")
            : Strings.Get("presence.active", showing.Details);
    }

    private static string Describe(string path, string noun)
    {
        if (!Directory.Exists(path))
        {
            return Strings.Get("storage.nothing");
        }

        FileInfo[] files = new DirectoryInfo(path).GetFiles("*", SearchOption.AllDirectories);

        return files.Length == 0
            ? Strings.Get("storage.nothing")
            : $"{files.Length:N0} {noun}, {files.Sum(file => file.Length) / 1024.0 / 1024.0:N0} MB";
    }

    private void Show(string page)
    {
        PageGeneral.Visibility = page == "General" ? Visibility.Visible : Visibility.Collapsed;
        PageIntegration.Visibility = page == "Integration" ? Visibility.Visible : Visibility.Collapsed;
        PageGraphics.Visibility = page == "Graphics" ? Visibility.Visible : Visibility.Collapsed;
        PageShortcuts.Visibility = page == "Shortcuts" ? Visibility.Visible : Visibility.Collapsed;
        PageLauncher.Visibility = page == "Launcher" ? Visibility.Visible : Visibility.Collapsed;
        PagePresence.Visibility = page == "Presence" ? Visibility.Visible : Visibility.Collapsed;
        PageDeployment.Visibility = page == "Deployment" ? Visibility.Visible : Visibility.Collapsed;
        PageStorage.Visibility = page == "Storage" ? Visibility.Visible : Visibility.Collapsed;
        PageAbout.Visibility = page == "About" ? Visibility.Visible : Visibility.Collapsed;

        string key = char.ToLowerInvariant(page[0]) + page[1..];
        PageTitle.Text = Strings.Get($"page.{key}.title");
        PageSubtitle.Text = Strings.Get($"page.{key}.subtitle");
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

    private void OnRepair(object sender, RoutedEventArgs e)
    {
        try
        {
            App.Services.Installer.Install(
                App.Services.Installer.InstalledExecutablePath,
                AppServices.Version,
                new InstallOptions(
                    _settings.DesktopShortcut,
                    _settings.StartMenuShortcut,
                    RegisterPlayer: true,
                    _settings.RegisterStudio));

            MessageBox.Show(
                Strings.Get("launcher.repaired"),
                "Nulltrap",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception failure)
        {
            MessageBox.Show(failure.Message, "Nulltrap", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        RefreshFacts();
    }

    private void OnUninstall(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                Strings.Get("confirm.uninstall"),
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
        _settings.DiscordPresence = PresenceEnabledBox.IsChecked == true;
        _settings.DiscordShowGameButton = PresenceButtonBox.IsChecked == true;
        _settings.DiscordApplicationId = PresenceAppIdBox.Text.Trim();

        _store.Save(_settings);

        ApplyFrameRate();
        bool overridingTexture = TextureQualityEnabledBox.IsChecked == true;
        SetFlag(TextureQualityEnabledFlag, overridingTexture ? "True" : string.Empty);
        SetFlag(TextureQualityFlag, overridingTexture ? ((int)TextureQualitySlider.Value).ToString() : string.Empty);
        SetFlag(MsaaFlag, ChosenValue(MsaaBox) ?? string.Empty);

        string? api = ChosenValue(GraphicsApiBox);
        foreach ((string? flag, _) in GraphicsApis)
        {
            if (flag is not null)
            {
                SetFlag(flag, flag == api ? "True" : string.Empty);
            }
        }

        string? grass = ChosenValue(GrassBox);
        SetFlag(MaxGrassFlag, grass ?? string.Empty);
        SetFlag(MinGrassFlag, grass ?? string.Empty);
        SetFlag(DisableDpiScaleFlag, DisableDpiScaleBox.IsChecked == true ? "True" : string.Empty);

        _fastFlags.Save(_flags);
        App.Services.StartPresence();

        RefreshFacts();
        ShowSaved();
    }

    private void ShowSaved()
    {
        SavedToastText.Text = Strings.Get("settings.saved");

        var fade = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames();
        fade.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(
            1, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160))));
        fade.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(
            1, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.4))));
        fade.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(
            0, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3))));

        SavedToast.BeginAnimation(OpacityProperty, fade);
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
