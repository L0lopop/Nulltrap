using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

using Nulltrap.Core.Bootstrapping;
using Nulltrap.Core.Deployment;
using Nulltrap.Core.FastFlags;
using Nulltrap.Core.Installation;
using Nulltrap.Core.Localization;
using Nulltrap.Core.Modifications;
using Nulltrap.Core.Presence;
using Nulltrap.Core.Roblox;
using Nulltrap.Core.Sessions;
using Nulltrap.Core.Settings;
using Nulltrap.Core.State;
using Nulltrap.Platform.Abstractions;

namespace Nulltrap.App;

public partial class SettingsWindow : ChromeWindow
{
    private const string FpsFlag = "DFIntTaskSchedulerTargetFps";
    private const string FpsCapFlag = "FFlagTaskSchedulerLimitTargetFpsTo2402";
    private const int DefaultTargetFps = 9999;
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

    private static readonly RobloxSession SampleSession = new()
    {
        JobId = "807e45a6-4f14-4673-8b85-d82c98c8713e",
        PlaceId = 920587237,
        UniverseId = 245662005,
        ServerAddress = "128.116.115.14",
        ServerPort = 49872,
        StartedAt = DateTimeOffset.UtcNow.AddMinutes(-7),
    };

    private static readonly GameInfo SampleGame = new()
    {
        UniverseId = 245662005,
        Name = "Neon Racers",
        CreatorName = "Starfall Studio",
        RootPlaceId = 920587237,
        Playing = 12480,
        IconUrl = "https://tr.rbxcdn.com/sample",
    };

    private static readonly AccountInfo SampleAccount = new(2374586903, "Roblox683038", "Roblox683038", null);

    private static readonly (string? Value, string Key)[] MsaaLevels =
    [
        (null, "graphics.automatic"),
        ("1", "graphics.msaa1"),
        ("2", "graphics.msaa2"),
        ("4", "graphics.msaa4"),
    ];

    private readonly TransferRate _rate = new();
    private readonly Dictionary<BinaryType, ClientVersion> _available = [];
    private readonly SettingsStore _store;
    private readonly NulltrapSettings _settings;
    private readonly FastFlagManager _fastFlags;
    private readonly Dictionary<string, string> _flags;

    private BinaryType _target = BinaryType.WindowsPlayer;
    private double _transfer;
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
        DesktopShortcutBox.IsChecked = _settings.DesktopShortcut;
        StartMenuShortcutBox.IsChecked = _settings.StartMenuShortcut;
        KeepDownloadCacheBox.IsChecked = _settings.KeepDownloadCache;
        ChannelBox.Text = _settings.Channel;
        AutomaticClientUpdatesBox.IsChecked = _settings.AutomaticClientUpdates;
        ModsEnabledBox.IsChecked = _settings.Mods;
        PresenceEnabledBox.IsChecked = _settings.DiscordPresence;
        HeadGameNameBox.IsChecked = _settings.DiscordHeadline.HasFlag(PresenceHeadline.GameName);
        HeadPlayingBox.IsChecked = _settings.DiscordHeadline.HasFlag(PresenceHeadline.PlayingRoblox);
        SubCreatorBox.IsChecked = _settings.DiscordSubline.HasFlag(PresenceSubline.Creator);
        SubPlayersBox.IsChecked = _settings.DiscordSubline.HasFlag(PresenceSubline.PlayerCount);
        SubServerBox.IsChecked = _settings.DiscordSubline.HasFlag(PresenceSubline.ServerRegion);
        PresenceAccountBox.IsChecked = _settings.DiscordShowAccount;
        PresenceJoinBox.IsChecked = _settings.DiscordAllowJoin;
        PresenceElapsedBox.IsChecked = _settings.DiscordShowElapsed;
        PresenceIconBox.IsChecked = _settings.DiscordShowGameIcon;
        PresenceButtonBox.IsChecked = _settings.DiscordShowGameButton;

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

        App.Services.Jobs.Changed += OnJobChanged;
        Closed += (_, _) => App.Services.Jobs.Changed -= OnJobChanged;

        _loaded = true;

        Show("Graphics");

        foreach ((string page, string[] groups) in Groups)
        {
            ShowGroup(page, groups[0]);
        }

        RefreshFacts();
        ShowTarget();
    }

    public void GoTo(string page)
    {
        if (FindName("Nav" + page) is RadioButton nav)
        {
            nav.IsChecked = true;
        }

        Show(page);
    }

    public sealed record Choice(string Label, object? Value);

    private static void FillChoices(ComboBox box, (string? Value, string Key)[] choices, string? current)
    {
        box.Items.Clear();

        foreach ((string? value, string key) in choices)
        {
            var item = new Choice(Strings.Get(key), value);
            box.Items.Add(item);

            if (value == current)
            {
                box.SelectedItem = item;
            }
        }

        box.SelectedItem ??= box.Items[0];
    }

    private static void FillChoices<TValue>(ComboBox box, (TValue Value, string Key)[] choices, TValue current)
        where TValue : struct, Enum
    {
        box.Items.Clear();

        foreach ((TValue value, string key) in choices)
        {
            var item = new Choice(Strings.Get(key), value);
            box.Items.Add(item);

            if (value.Equals(current))
            {
                box.SelectedItem = item;
            }
        }

        box.SelectedItem ??= box.Items[0];
    }

    private static string? ChosenValue(ComboBox box) =>
        (box.SelectedItem as Choice)?.Value as string;

    private static TValue Chosen<TValue>(ComboBox box, TValue fallback)
        where TValue : struct, Enum =>
        (box.SelectedItem as Choice)?.Value is TValue value ? value : fallback;

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
                    FontSize = 12,
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
        ShowPresencePreview();
        RepairButton.IsEnabled = App.Services.Installer.IsInstalled;
        AboutVersionText.Text = $"Version {AppServices.Version}";

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

        if (string.IsNullOrWhiteSpace(PresenceService.ApplicationId(_settings.DiscordApplicationId)))
        {
            return Strings.Get("presence.notConfigured");
        }

        PresenceActivity? showing = App.Services.Presence?.Last;

        return showing?.Details is null
            ? Strings.Get("presence.waiting")
            : Strings.Get("presence.active", showing.Details);
    }

    private PresenceOptions PresenceShape() => new()
    {
        Headline = (HeadGameNameBox.IsChecked == true ? PresenceHeadline.GameName : PresenceHeadline.Nothing)
            | (HeadPlayingBox.IsChecked == true ? PresenceHeadline.PlayingRoblox : PresenceHeadline.Nothing),
        Subline = (SubCreatorBox.IsChecked == true ? PresenceSubline.Creator : PresenceSubline.Nothing)
            | (SubPlayersBox.IsChecked == true ? PresenceSubline.PlayerCount : PresenceSubline.Nothing)
            | (SubServerBox.IsChecked == true ? PresenceSubline.ServerRegion : PresenceSubline.Nothing),
        ShowElapsed = PresenceElapsedBox.IsChecked == true,
        ShowGameIcon = PresenceIconBox.IsChecked == true,
        ShowGameButton = PresenceButtonBox.IsChecked == true,
        ShowAccount = PresenceAccountBox.IsChecked == true,
        AllowJoin = PresenceJoinBox.IsChecked == true,
    };

    private void ShowPresencePreview()
    {
        bool on = PresenceEnabledBox.IsChecked == true;
        PresenceOptions shape = PresenceShape();
        PresenceActivity sample = PresenceService.Compose(SampleSession, SampleGame, shape, SampleAccount);

        PresencePreview.Opacity = on ? 1 : 0.4;
        PreviewDetails.Text = sample.Details;
        PreviewState.Text = sample.State ?? string.Empty;
        PreviewState.Visibility = sample.State is null ? Visibility.Collapsed : Visibility.Visible;
        PreviewElapsed.Text = Strings.Get("presence.elapsedSample");
        PreviewElapsed.Visibility = shape.ShowElapsed ? Visibility.Visible : Visibility.Collapsed;
        PreviewIcon.Visibility = shape.ShowGameIcon ? Visibility.Visible : Visibility.Collapsed;
        PreviewIconGlyph.Visibility = shape.ShowGameIcon ? Visibility.Visible : Visibility.Collapsed;

        PreviewAccount.Visibility = sample.SmallText is null ? Visibility.Collapsed : Visibility.Visible;
        PreviewAccountGlyph.Text = sample.SmallText?[..1].ToUpperInvariant() ?? string.Empty;

        PresenceButton? join = sample.Buttons.FirstOrDefault(button => button.Url.Contains("gameInstanceId", StringComparison.Ordinal));
        PresenceButton? page = sample.Buttons.FirstOrDefault(button => !button.Url.Contains("gameInstanceId", StringComparison.Ordinal));

        PreviewJoinText.Text = join?.Label ?? string.Empty;
        PreviewJoin.Visibility = join is null ? Visibility.Collapsed : Visibility.Visible;
        PreviewButtonText.Text = page?.Label ?? string.Empty;
        PreviewButton.Visibility = page is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnToggleCustomRpc(object sender, RoutedEventArgs e)
    {
        bool opening = CustomRpcHeader.IsChecked == true;

        CustomRpcTurn.BeginAnimation(
            System.Windows.Media.RotateTransform.AngleProperty,
            new System.Windows.Media.Animation.DoubleAnimation
            {
                To = opening ? -180 : 0,
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new System.Windows.Media.Animation.CubicEase
                {
                    EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut,
                },
            });

        if (!opening)
        {
            CustomRpcBody.Visibility = Visibility.Collapsed;
            return;
        }

        CustomRpcBody.Visibility = Visibility.Visible;
        Arrive(CustomRpcBody);
    }

    private void OnPresenceChanged(object sender, RoutedEventArgs e)
    {
        if (_loaded)
        {
            ShowPresencePreview();
        }
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
            : $"{files.Length:N0} {noun}, {Sizes.Describe(files.Sum(file => file.Length))}";
    }

    private static readonly string[] Pages =
        ["General", "Graphics", "Launcher", "Presence", "Versions", "Activity", "Mods", "Flags", "About"];

    private static readonly Dictionary<string, string[]> Groups = new(StringComparer.Ordinal)
    {
        ["Graphics"] = ["GraphicsQuality", "GraphicsSpeed", "GraphicsFlags"],
        ["Versions"] = ["VersionsPlayer", "VersionsStorage"],
        ["Flags"] = ["FlagsFrames", "FlagsReady", "FlagsEditor"],
    };

    private static readonly Dictionary<string, (string Page, string Group)> Tabs = new(StringComparer.Ordinal)
    {
        ["GraphicsQuality"] = ("Graphics", "GraphicsQuality"),
        ["GraphicsSpeed"] = ("Graphics", "GraphicsSpeed"),
        ["GraphicsFlags"] = ("Graphics", "GraphicsFlags"),
        ["VersionsPlayer"] = ("Versions", "VersionsPlayer"),
        ["VersionsStudio"] = ("Versions", "VersionsPlayer"),
        ["VersionsStorage"] = ("Versions", "VersionsStorage"),
        ["FlagsFrames"] = ("Flags", "FlagsFrames"),
        ["FlagsReady"] = ("Flags", "FlagsReady"),
        ["FlagsEditor"] = ("Flags", "FlagsEditor"),
    };

    private void Show(string page)
    {
        foreach (string name in Pages)
        {
            if (FindName("Page" + name) is not FrameworkElement panel)
            {
                continue;
            }

            bool wanted = name == page;
            panel.Visibility = wanted ? Visibility.Visible : Visibility.Collapsed;

            if (wanted)
            {
                Arrive(panel);
            }
        }

        string key = char.ToLowerInvariant(page[0]) + page[1..];
        PageTitle.Text = Strings.Get($"page.{key}.title");
        PageSubtitle.Text = Strings.Get($"page.{key}.subtitle");
    }

    private static void Arrive(FrameworkElement panel)
    {
        var slide = new System.Windows.Media.TranslateTransform();
        panel.RenderTransform = slide;

        var ease = new System.Windows.Media.Animation.CubicEase
        {
            EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut,
        };

        panel.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(190),
            EasingFunction = ease,
        });

        slide.BeginAnimation(
            System.Windows.Media.TranslateTransform.YProperty,
            new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 10,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(240),
                EasingFunction = ease,
            });
    }

    private void ShowGroup(string page, string group)
    {
        if (!Groups.TryGetValue(page, out string[]? groups))
        {
            return;
        }

        foreach (string name in groups)
        {
            if (FindName(name) is not FrameworkElement panel)
            {
                continue;
            }

            bool wanted = name == group;
            panel.Visibility = wanted ? Visibility.Visible : Visibility.Collapsed;

            if (wanted && _loaded)
            {
                Arrive(panel);
            }
        }
    }

    private void OnSubTab(object sender, RoutedEventArgs e)
    {
        if (!_loaded
            || sender is not RadioButton { Tag: string tab }
            || !Tabs.TryGetValue(tab, out (string Page, string Group) chosen))
        {
            return;
        }

        ShowGroup(chosen.Page, chosen.Group);

        if (chosen.Group == "VersionsStorage")
        {
            RefreshFacts();
            return;
        }

        if (chosen.Page != "Versions")
        {
            return;
        }

        BinaryType picked = tab == "VersionsStudio"
            ? BinaryType.WindowsStudio64
            : BinaryType.WindowsPlayer;

        if (picked == _target)
        {
            return;
        }

        _target = picked;
        _rate.Reset();
        ShowTarget();
        _ = CheckAsync(_target);
    }

    private void OnNavigate(object sender, RoutedEventArgs e)
    {
        if (_loaded && sender is RadioButton { Tag: string page })
        {
            Show(page);
            RefreshFacts();

            if (page == "Versions")
            {
                ShowGroup("Versions", TabVersionsStorage.IsChecked == true ? "VersionsStorage" : "VersionsPlayer");
                ShowTarget();
                _ = CheckAsync(_target);
            }

            if (page == "Activity")
            {
                BuildActivity();
            }

            if (page == "Mods")
            {
                BuildMods();
            }

            if (page == "Flags")
            {
                BuildReady();
                BuildFlags();
            }
        }
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

    private DeploymentChannel ChosenChannel() =>
        string.IsNullOrWhiteSpace(ChannelBox.Text)
            ? DeploymentChannel.Default
            : new DeploymentChannel(ChannelBox.Text.Trim());

    private void ShowTarget()
    {
        InstalledClient? installed = App.Services.StateStore.Load().Get(_target);

        InstalledVersionText.Text = installed is null
            ? Strings.Get("deployment.notDownloaded")
            : installed.Version;
        InstalledGuidText.Text = installed?.VersionGuid ?? string.Empty;
        InstalledGuidText.Visibility = installed is null ? Visibility.Collapsed : Visibility.Visible;
        RemoveVersionButton.IsEnabled = installed is not null && !App.Services.Jobs.IsRunning(_target);

        ClientVersion? latest = _available.GetValueOrDefault(_target);
        AvailableVersionText.Text = latest?.Version ?? Strings.Get("versions.unknown");
        AvailableChannelText.Text = latest is null
            ? string.Empty
            : Strings.Get("versions.channelIs", ChosenChannel().Name);
        AvailableChannelText.Visibility = latest is null ? Visibility.Collapsed : Visibility.Visible;

        DownloadButton.Content = Strings.Get(
            installed is null ? "versions.download"
            : latest is not null && latest.VersionGuid != installed.VersionGuid ? "versions.update"
            : "versions.reinstall");

        ShowJob(App.Services.Jobs.Of(_target));
    }

    private void ShowJob(InstallJob? job)
    {
        bool running = App.Services.Jobs.IsRunning(_target);

        DownloadButton.Visibility = running ? Visibility.Collapsed : Visibility.Visible;
        CancelDownloadButton.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        RemoveVersionButton.IsEnabled = RemoveVersionButton.IsEnabled && !running;

        if (job is null)
        {
            TransferTitle.Text = Strings.Get("versions.idle");
            TransferMessage.Text = Strings.Get("versions.idleHint");
            TransferBytes.Text = string.Empty;
            TransferRateText.Text = string.Empty;
            _transfer = 0;
            DrawTransfer();
            return;
        }

        TransferTitle.Text = Strings.Get(
            job.Failure is not null ? "versions.failed"
            : job.Cancelled ? "versions.cancelled"
            : job.Result is not null ? "versions.done"
            : "versions.working");

        TransferMessage.Text = job.Progress.Message;
        TransferMessage.Foreground = (System.Windows.Media.Brush)FindResource(
            job.Failure is null ? "TextSoftBrush" : "DangerBrush");

        _transfer = job.Settled && job.Result is null ? 0 : job.Progress.Fraction;
        DrawTransfer();

        if (job.Progress.BytesTotal > 0)
        {
            _rate.Add(job.Progress.BytesCompleted, DateTimeOffset.UtcNow);
            TransferBytes.Text = Strings.Get(
                "versions.transferred",
                Sizes.Describe(job.Progress.BytesCompleted),
                Sizes.Describe(job.Progress.BytesTotal));
            TransferRateText.Text = Sizes.Rate(_rate.BytesPerSecond());
        }
        else if (job.Settled)
        {
            _rate.Reset();
            TransferRateText.Text = string.Empty;
        }
    }

    private void OnJobChanged(object? sender, InstallJob job) =>
        Dispatcher.BeginInvoke(() =>
        {
            if (job.BinaryType != _target)
            {
                return;
            }

            if (job.Settled)
            {
                ShowTarget();
                RefreshFacts();
                return;
            }

            ShowJob(job);
        });

    private void OnTransferTrackResized(object sender, SizeChangedEventArgs e) => DrawTransfer();

    private void DrawTransfer()
    {
        double wanted = Math.Max(0, TransferTrack.ActualWidth) * Math.Clamp(_transfer, 0, 1);

        TransferFill.BeginAnimation(WidthProperty, new System.Windows.Media.Animation.DoubleAnimation
        {
            To = wanted,
            Duration = TimeSpan.FromMilliseconds(320),
            EasingFunction = new System.Windows.Media.Animation.CubicEase
            {
                EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut,
            },
        });
    }

    private void OnDownload(object sender, RoutedEventArgs e)
    {
        _rate.Reset();

        if (App.Services.Jobs.Start(_target, ChosenChannel()))
        {
            ShowTarget();
        }
    }

    private void OnCancelDownload(object sender, RoutedEventArgs e) => App.Services.Jobs.Cancel(_target);

    private async void OnCheckVersion(object sender, RoutedEventArgs e) => await CheckAsync(_target);

    private async Task CheckAsync(BinaryType binaryType)
    {
        CheckVersionButton.IsEnabled = false;
        AvailableVersionText.Text = Strings.Get("versions.checking");

        try
        {
            ClientVersion version = await App.Services.Deployment.GetClientVersionAsync(
                binaryType, ChosenChannel());

            _available[binaryType] = version;
            App.Services.Bootstrapper.Adopt(binaryType, version, ChosenChannel());
        }
        catch (Exception failure)
        {
            _available.Remove(binaryType);
            AvailableVersionText.Text = failure.Message;
        }
        finally
        {
            CheckVersionButton.IsEnabled = true;
        }

        if (binaryType == _target)
        {
            ShowTarget();
        }
    }

    private void OnRemoveVersion(object sender, RoutedEventArgs e)
    {
        InstallState state = App.Services.StateStore.Load();

        if (state.Get(_target) is null
            || MessageBox.Show(
                Strings.Get("versions.confirmRemove"),
                "Nulltrap",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        state.Remove(_target);
        App.Services.StateStore.Save(state);
        App.Services.Bootstrapper.RemoveSupersededVersions(state);

        ShowTarget();
        RefreshFacts();
    }

    private void BuildActivity()
    {
        SessionHistory history = App.Services.History.Load();
        DateTimeOffset now = DateTimeOffset.Now;

        PlayedTodayText.Text = Clocks.Describe(history.Since(now.Date));
        PlayedWeekText.Text = Clocks.Describe(history.Since(now.Date.AddDays(-6)));
        PlayedTotalText.Text = Clocks.Describe(history.Total());

        FavouritesPanel.Children.Clear();

        foreach (PlayedGame game in history.ByGame(5))
        {
            FavouritesPanel.Children.Add(Line(
                game.Name,
                Strings.Get("activity.visits", game.Visits),
                Clocks.Describe(game.Total)));
        }

        if (FavouritesPanel.Children.Count == 0)
        {
            FavouritesPanel.Children.Add(Faint(Strings.Get("activity.nothingYet")));
        }

        RecentPanel.Children.Clear();

        foreach (PlayedSession played in App.Services.History.Load().Sessions.Take(20))
        {
            RecentPanel.Children.Add(Line(
                played.Name ?? Strings.Get("activity.unknownGame"),
                played.StartedAt.ToLocalTime().ToString("g") + (played.Server is null ? string.Empty : "  ·  " + played.Server),
                Clocks.Describe(played.Duration)));
        }

        if (RecentPanel.Children.Count == 0)
        {
            RecentPanel.Children.Add(Faint(Strings.Get("activity.nothingYet")));
        }

        ClearHistoryButton.IsEnabled = history.Sessions.Count > 0;
    }

    private Grid Line(string title, string detail, string trailing)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel();
        left.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
        });
        left.Children.Add(new TextBlock
        {
            Text = detail,
            FontSize = 12,
            Margin = new Thickness(0, 2, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = (System.Windows.Media.Brush)FindResource("TextSoftBrush"),
        });

        var right = new TextBlock
        {
            Text = trailing,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 0, 0),
            Foreground = (System.Windows.Media.Brush)FindResource("PurpleBrightBrush"),
        };

        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 1);
        row.Children.Add(left);
        row.Children.Add(right);

        return row;
    }

    private TextBlock Faint(string text) => new()
    {
        Text = text,
        FontSize = 12,
        Foreground = (System.Windows.Media.Brush)FindResource("TextSoftBrush"),
    };

    private void OnClearHistory(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                Strings.Get("activity.confirmClear"),
                "Nulltrap",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        App.Services.History.Clear();
        BuildActivity();
    }

    private void BuildMods()
    {
        ModsFolderText.Text = App.Services.Mods.SourceDirectory;
        ModsEnabledBox.IsChecked = _settings.Mods;

        IReadOnlyList<ModFile> files = App.Services.Mods.List();

        ModsPanel.Children.Clear();

        foreach (ModFile file in files)
        {
            ModsPanel.Children.Add(Line(
                file.RelativePath,
                file.ChangedAt.ToLocalTime().ToString("g"),
                Sizes.Describe(file.Size)));
        }

        if (files.Count == 0)
        {
            ModsPanel.Children.Add(Faint(Strings.Get("mods.noFiles")));
        }

        InstalledClient? player = App.Services.StateStore.Load().Get(BinaryType.WindowsPlayer);

        ModsAppliedText.Text = player is null
            ? Strings.Get("mods.noClient")
            : Strings.Get("mods.readyFor", player.Version);

        ApplyModsButton.IsEnabled = player is not null;
    }

    private void OnModsToggled(object sender, RoutedEventArgs e)
    {
        if (_loaded)
        {
            BuildMods();
        }
    }

    private void OnOpenMods(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(App.Services.Mods.SourceDirectory);
        Open(App.Services.Mods.SourceDirectory);
    }

    private void OnApplyMods(object sender, RoutedEventArgs e)
    {
        InstalledClient? player = App.Services.StateStore.Load().Get(BinaryType.WindowsPlayer);

        if (player is null)
        {
            return;
        }

        App.Services.Mods.Enabled = ModsEnabledBox.IsChecked == true;
        ModOutcome outcome = App.Services.Mods.ApplyTo(
            Path.Combine(App.Services.Paths.Versions, player.VersionGuid));

        ModsAppliedText.Text = Strings.Get("mods.outcome", outcome.Applied, outcome.Reverted);
        ShowSaved();
    }

    private sealed record Ready(string Flag, string TitleKey, string HintKey, (string? Value, string Key)[] Choices);

    private static readonly Ready[] ReadyMade =
    [
        new("FFlagDebugSkyGray", "ready.skyGray", "ready.skyGrayHint",
            [(null, "ready.off"), ("True", "ready.on")]),
        new("DFFlagDebugPauseVoxelizer", "ready.pauseVoxelizer", "ready.pauseVoxelizerHint",
            [(null, "ready.off"), ("True", "ready.on")]),
        new("FFlagHandleAltEnterFullscreenManually", "ready.altEnter", "ready.altEnterHint",
            [(null, "ready.off"), ("True", "ready.on")]),
        new("DFIntDebugFRMQualityLevelOverride", "ready.quality", "ready.qualityHint",
            [(null, "ready.off"), ("1", "graphics.qualityLevel1"), ("10", "graphics.qualityLevel10"), ("21", "graphics.qualityLevel21")]),
        new("FIntGrassMovementReducedMotionFactor", "ready.grassMotion", "ready.grassMotionHint",
            [(null, "ready.off"), ("0", "ready.still"), ("50", "ready.gentle"), ("100", "ready.normal")]),
        new("DFIntCSGLevelOfDetailSwitchingDistance", "ready.geometry", "ready.geometryHint",
            [(null, "ready.off"), ("50", "ready.near"), ("100", "ready.normal"), ("250", "ready.far")]),
    ];

    private static readonly string[] OwnedElsewhere =
    [
        FpsFlag, FpsCapFlag, TextureQualityEnabledFlag, TextureQualityFlag, MsaaFlag,
        DisableDpiScaleFlag, PreferD3D11Flag, PreferVulkanFlag, PreferOpenGLFlag,
        MaxGrassFlag, MinGrassFlag,
        .. ReadyMade.Select(ready => ready.Flag),
    ];

    private void BuildReady()
    {
        if (ReadyPanel.Children.Count > 0)
        {
            return;
        }

        foreach (Ready ready in ReadyMade)
        {
            var card = new Border { Style = (Style)FindResource("RowCard") };
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel { Margin = new Thickness(0, 0, 20, 0) };
            left.Children.Add(new TextBlock
            {
                Text = Strings.Get(ready.TitleKey),
                Style = (Style)FindResource("RowTitle"),
            });
            left.Children.Add(new TextBlock
            {
                Text = Strings.Get(ready.HintKey),
                Style = (Style)FindResource("RowHint"),
            });

            var box = new ComboBox
            {
                Style = (Style)FindResource("Dropdown"),
                Width = 200,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = ready.Flag,
            };
            FillChoices(box, ready.Choices, _flags.GetValueOrDefault(ready.Flag));
            box.SelectionChanged += OnReadyChanged;

            Grid.SetColumn(left, 0);
            Grid.SetColumn(box, 1);
            row.Children.Add(left);
            row.Children.Add(box);
            card.Child = row;

            ReadyPanel.Children.Add(card);
        }
    }

    private void OnReadyChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded || sender is not ComboBox { Tag: string flag } box)
        {
            return;
        }

        SetFlag(flag, ChosenValue(box) ?? string.Empty);
        _fastFlags.Save(_flags);
        RefreshFacts();
    }

    private static readonly string[] KnownPrefixes =
        ["FFlag", "DFFlag", "FInt", "DFInt", "FString", "DFString", "FLog", "DFLog", "SFFlag"];

    private void BuildFlags()
    {
        FlagsPanel.Children.Clear();

        KeyValuePair<string, string>[] mine = _flags
            .Where(pair => !OwnedElsewhere.Contains(pair.Key, StringComparer.Ordinal))
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach ((string name, string value) in mine)
        {
            FlagsPanel.Children.Add(FlagRow(name, value));
        }

        if (mine.Length == 0)
        {
            FlagsPanel.Children.Add(Faint(Strings.Get("flags.none")));
        }

        int ignored = FastFlagAllowlist.RejectedIn(mine.Select(pair => pair.Key)).Count;

        FlagCountText.Text = mine.Length == 0
            ? Strings.Get("flags.editorOnlyYours")
            : Strings.Get("flags.count", mine.Length, ignored);
    }

    private Grid FlagRow(string name, string value)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        bool allowed = FastFlagAllowlist.IsAllowed(name);

        var left = new StackPanel();
        left.Children.Add(new TextBlock
        {
            Text = name,
            FontSize = 13,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
        });
        left.Children.Add(new TextBlock
        {
            Text = allowed ? Strings.Get("flags.applied") : Strings.Get("flags.ignored"),
            FontSize = 12,
            Margin = new Thickness(0, 2, 0, 0),
            Foreground = (System.Windows.Media.Brush)FindResource(allowed ? "TextSoftBrush" : "DangerBrush"),
        });

        var shown = new TextBlock
        {
            Text = value,
            FontSize = 13,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 16, 0),
            Foreground = (System.Windows.Media.Brush)FindResource("PurpleBrightBrush"),
        };

        var remove = new System.Windows.Controls.Button
        {
            Content = Strings.Get("flags.remove"),
            Style = (Style)FindResource("Quiet"),
            MinWidth = 104,
            VerticalAlignment = VerticalAlignment.Center,
            Tag = name,
        };
        remove.Click += OnRemoveFlag;

        Grid.SetColumn(left, 0);
        Grid.SetColumn(shown, 1);
        Grid.SetColumn(remove, 2);
        row.Children.Add(left);
        row.Children.Add(shown);
        row.Children.Add(remove);

        return row;
    }

    private void OnAddFlag(object sender, RoutedEventArgs e)
    {
        string name = FlagNameBox.Text.Trim();
        string value = FlagValueBox.Text.Trim();
        string? problem = OwnedElsewhere.Contains(name, StringComparer.Ordinal)
            ? Strings.Get("flags.systemOwned")
            : Problem(name, value);

        FlagProblemText.Text = problem ?? string.Empty;
        FlagProblemText.Visibility = problem is null ? Visibility.Collapsed : Visibility.Visible;

        if (problem is not null)
        {
            return;
        }

        _flags[name] = value;
        _fastFlags.Save(_flags);

        FlagNameBox.Text = string.Empty;
        FlagValueBox.Text = string.Empty;

        BuildFlags();
        RefreshFacts();
        ShowSaved();
    }

    private static string? Problem(string name, string value)
    {
        if (name.Length == 0)
        {
            return Strings.Get("flags.needName");
        }

        if (value.Length == 0)
        {
            return Strings.Get("flags.needValue");
        }

        if (name.Any(letter => !char.IsLetterOrDigit(letter) && letter != '_'))
        {
            return Strings.Get("flags.badName");
        }

        return KnownPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal))
            ? null
            : Strings.Get("flags.badPrefix", string.Join(", ", KnownPrefixes));
    }

    private void OnRemoveFlag(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string name })
        {
            return;
        }

        _flags.Remove(name);
        _fastFlags.Save(_flags);

        BuildFlags();
        RefreshFacts();
    }

    private void OnOpenFlagFile(object sender, RoutedEventArgs e)
    {
        _fastFlags.Save(_flags);
        Open(Path.GetDirectoryName(_fastFlags.SourcePath)!);
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        _settings.CloseAfterLaunch = CloseAfterLaunchBox.IsChecked == true;
        _settings.ConfirmMultipleInstances = ConfirmMultipleInstancesBox.IsChecked == true;
        _settings.DesktopShortcut = DesktopShortcutBox.IsChecked == true;
        _settings.StartMenuShortcut = StartMenuShortcutBox.IsChecked == true;
        _settings.KeepDownloadCache = KeepDownloadCacheBox.IsChecked == true;
        _settings.Channel = string.IsNullOrWhiteSpace(ChannelBox.Text)
            ? DeploymentChannel.DefaultName
            : ChannelBox.Text.Trim();
        _settings.AutomaticClientUpdates = AutomaticClientUpdatesBox.IsChecked == true;
        PresenceOptions shape = PresenceShape();
        _settings.Mods = ModsEnabledBox.IsChecked == true;
        _settings.DiscordPresence = PresenceEnabledBox.IsChecked == true;
        _settings.DiscordHeadline = shape.Headline;
        _settings.DiscordSubline = shape.Subline;
        _settings.DiscordShowElapsed = shape.ShowElapsed;
        _settings.DiscordShowGameIcon = shape.ShowGameIcon;
        _settings.DiscordShowGameButton = shape.ShowGameButton;
        _settings.DiscordShowAccount = shape.ShowAccount;
        _settings.DiscordAllowJoin = shape.AllowJoin;

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
        App.Services.Mods.Enabled = _settings.Mods;
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
