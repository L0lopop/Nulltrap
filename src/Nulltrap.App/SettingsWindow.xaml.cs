using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;

using Nulltrap.Core.Bootstrapping;
using Nulltrap.Core.Deployment;
using Nulltrap.Core.FastFlags;
using Nulltrap.Core.Installation;
using Nulltrap.Core.Localization;
using Nulltrap.Core.Maintenance;
using Nulltrap.Core.Modifications;
using Nulltrap.Core.Presence;
using Nulltrap.Core.Plugins;
using Nulltrap.Core.Profiles;
using Nulltrap.Core.Roblox;
using Nulltrap.Core.Sessions;
using Nulltrap.Core.Settings;
using Nulltrap.Core.State;
using Nulltrap.Core.Updating;
using Nulltrap.Platform.Abstractions;

namespace Nulltrap.App;

public partial class SettingsWindow : ChromeWindow
{
    private const string FpsFlag = "DFIntTaskSchedulerTargetFps";
    private const string FpsCapFlag = "FFlagTaskSchedulerLimitTargetFpsTo2402";
    private const int DefaultTargetFps = 9999;
    private const int RobloxFrameCeiling = 240;
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

    private static ServerPlace SamplePlace => new(ServerLocator.CountryName("DE"), "Frankfurt am Main");

    private static readonly (int Value, string Key)[] QualityModes =
    [
        (0, "graphics.qualityAutomatic"),
        (1, "graphics.qualityManual"),
    ];

    private static readonly (int Value, string Key)[] OptimizationModes =
    [
        (0, "graphics.optimizationSpeed"),
        (1, "graphics.optimizationBalance"),
        (2, "graphics.optimizationQuality"),
    ];

    private static readonly (int Value, string Key)[] CameraModes =
    [
        (0, "graphics.cameraDefault"),
        (1, "graphics.cameraClassic"),
        (2, "graphics.cameraFollow"),
        (3, "graphics.cameraOrbital"),
        (4, "graphics.cameraToggle"),
    ];

    private static readonly (int Value, string Key)[] MovementModes =
    [
        (0, "graphics.movementDefault"),
        (1, "graphics.movementKeyboard"),
        (2, "graphics.movementClick"),
    ];

    private static readonly (string? Value, string Key)[] TextureLevels =
    [
        (null, "graphics.automatic"),
        ("0", "graphics.texture0"),
        ("1", "graphics.texture1"),
        ("2", "graphics.texture2"),
        ("3", "graphics.texture3"),
    ];

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
    private LauncherRelease? _release;
    private const int TilesInARow = 5;
    private const int GenreSample = 12;
    private const int TilePixels = 360;
    private const int AvatarPixels = 132;
    private const double TileGap = 12;

    private static readonly TimeSpan HomeFreshness = TimeSpan.FromMinutes(10);

    private bool _loaded;

    private bool _building;
    private int _homeShownFor = -1;
    private DateTimeOffset _homeShownAt = DateTimeOffset.MinValue;

    private readonly HashSet<string> _chosen = new(StringComparer.Ordinal);

    private readonly UserGameSettings _roblox = new(UserGameSettings.DefaultPath);

    private readonly System.Windows.Threading.DispatcherTimer _robloxSettle = new()
    {
        Interval = TimeSpan.FromMilliseconds(600),
    };

    private FileSystemWatcher? _robloxWatcher;
    private bool _robloxMine;

    private static readonly GridLength[] FlagColumns =
    [
        new(34),
        new(132),
        new(86),
        new(1, GridUnitType.Star),
        new(200),
    ];

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



        TrimMemoryBox.IsChecked = _settings.TrimMemory;
        StayInTrayBox.IsChecked = _settings.StayInTray;
        RunAtStartupBox.IsChecked = _settings.RunAtStartup;
        CloseRobloxOnLeaveBox.IsChecked = _settings.CloseRobloxOnLeave;
        UpdateNoticeBox.IsChecked = _settings.UpdateNotice;
        FillSweepPlans();
        ShowCacheSize();

        MonitoringBox.IsChecked = _settings.Monitoring;
        ServerNoticeBox.IsChecked = _settings.ServerNotice;
        ShowMonitoring();

        bool unlocked = _flags.ContainsKey(FpsFlag);
        UnlockFpsBox.IsChecked = unlocked;
        FpsBox.Text = _flags.GetValueOrDefault(FpsFlag, RobloxFrameCeiling.ToString());
        FpsBox.IsEnabled = unlocked;
        FillChoices(MsaaBox, MsaaLevels, _flags.GetValueOrDefault(MsaaFlag));
        FillChoices(GrassBox, GrassDistances, _flags.GetValueOrDefault(MaxGrassFlag));
        FillChoices(GraphicsApis_Box(), GraphicsApis, ChosenGraphicsApi());
        _robloxSettle.Tick += OnRobloxSettled;
        WatchRobloxSettings();
        ShowRobloxSettings();

        bool overridingTexture =
            _flags.GetValueOrDefault(TextureQualityEnabledFlag, "False").Equals("True", StringComparison.OrdinalIgnoreCase);
        FillChoices(TextureQualityBox, TextureLevels, overridingTexture ? _flags.GetValueOrDefault(TextureQualityFlag) : null);
        DisableDpiScaleBox.IsChecked =
            _flags.GetValueOrDefault(DisableDpiScaleFlag, "False").Equals("True", StringComparison.OrdinalIgnoreCase);

        BuildProfiles();
        BuildPlugins();
        BuildLanguageButtons();
        BuildThemeButtons();
        BuildChangelog();

        App.Services.Jobs.Changed += OnJobChanged;
        Closed += (_, _) => App.Services.Jobs.Changed -= OnJobChanged;

        _loaded = true;

        Show("Home");

        foreach ((string page, string[] groups) in Groups)
        {
            ShowGroup(page, groups[0]);
        }

        RefreshFacts();
        ShowTarget();
        _ = BuildHomeAsync();
    }

    public void GoTo(string page)
    {
        if (FindName("Nav" + page) is RadioButton nav)
        {
            nav.IsChecked = true;
        }

        Show(page);
    }

    public sealed record Choice(string Label, object? Value)
    {
        public override string ToString() => Label;
    }

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

    private void WatchRobloxSettings()
    {
        string? folder = Path.GetDirectoryName(UserGameSettings.DefaultPath);

        if (folder is null || !Directory.Exists(folder))
        {
            return;
        }

        _robloxWatcher = new FileSystemWatcher(folder, UserGameSettings.FileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
        };

        _robloxWatcher.Changed += OnRobloxFileTouched;
        _robloxWatcher.Created += OnRobloxFileTouched;
        _robloxWatcher.Renamed += OnRobloxFileTouched;
        _robloxWatcher.EnableRaisingEvents = true;
    }

    private void OnRobloxFileTouched(object sender, FileSystemEventArgs e) =>
        Dispatcher.BeginInvoke(() =>
        {
            _robloxSettle.Stop();
            _robloxSettle.Start();
        });

    private void OnRobloxSettled(object? sender, EventArgs e)
    {
        _robloxSettle.Stop();

        if (_robloxMine)
        {
            _robloxMine = false;
            return;
        }

        ShowRobloxSettings();
        ShowFlagNotice(Strings.Get("graphics.pulled"));
    }

    private void ShowRobloxSettings()
    {
        _roblox.Load();

        ReadStep(QualityLevelSlider, "SavedQualityLevel", fallback: 10);
        ReadChoice(QualityModeBox, "SavedQualityLevel", QualityModes, whenZero: 0, otherwise: 1);
        ReadChoice(OptimizationBox, "GraphicsOptimizationMode", OptimizationModes);
        ReadFlag(MaxQualityBox, "MaxQualityEnabled");
        ReadFlag(VignetteBox, "VignetteEnabled", fallback: true);

        ReadFlag(FullscreenBox, "Fullscreen");
        ReadFlag(StartMaximisedBox, "StartMaximized", fallback: true);

        ReadStep(MasterVolumeSlider, "MasterVolume", scale: 10, fallback: 0.5);
        ReadStep(VoiceVolumeSlider, "VoiceChatVolume", scale: 10, fallback: 1);
        ReadStep(PartyVolumeSlider, "PartyVoiceVolume", scale: 10, fallback: 1);
        ReadHaptics();

        ReadStep(TransparencySlider, "PreferredTransparency", scale: 10, fallback: 1);
        ReadStep(TextSizeSlider, "PreferredTextSize", fallback: 1);
        ReadFlag(ReducedMotionBox, "ReducedMotion");
        ReadFlag(UiNavigationBox, "UiNavigationKeyBindEnabled", fallback: true);
        ReadFlag(PerformanceStatsBox, "PerformanceStatsVisible");
        ReadFlag(MicroProfilerBox, "OnScreenProfilerEnabled");
        ReadFlag(PlayerListBox, "PlayerListVisible", fallback: true);
        ReadFlag(ChatVisibleBox, "ChatVisible");
        ReadFlag(PlayerNamesBox, "PlayerNamesEnabled", fallback: true);
        ReadFlag(BadgesBox, "BadgeVisible", fallback: true);

        ReadStep(SensitivitySlider, "MouseSensitivity", fallback: 1);
        ReadFlag(CameraInvertedBox, "CameraYInverted");
        ReadChoice(CameraModeBox, "ComputerCameraMovementMode", CameraModes);
        ReadChoice(MovementModeBox, "ComputerMovementMode", MovementModes);
        ReadFlag(VrBox, "VREnabled");

        ShowSliderValues();
        ShowQualityMode();
        ShowRobloxOwner();
    }

    private void ShowQualityMode()
    {
        if (IsInitialized && QualityModeBox.IsEnabled)
        {
            QualityLevelSlider.IsEnabled = Picked(QualityModeBox, 1) == 1;
        }
    }

    private void OnQualityModeChanged(object sender, SelectionChangedEventArgs e) => ShowQualityMode();

    private void ReadFlag(CheckBox box, string name, bool fallback = false)
    {
        bool? kept = _roblox.Flag(name);

        box.IsEnabled = kept.HasValue;
        box.IsChecked = kept ?? fallback;
        box.Tag = name;
    }

    private void ReadStep(StepBar bar, string name, double scale = 1, double fallback = 0)
    {
        double? kept = _roblox.Number(name);

        bar.IsEnabled = kept.HasValue;
        bar.Step = (int)Math.Round(Math.Clamp((kept ?? fallback) * scale, bar.Lowest, bar.Steps));
        bar.Tag = name;
    }

    private void ReadText(TextBox box, string name, int fallback)
    {
        double? kept = _roblox.Number(name);

        box.IsEnabled = kept.HasValue;
        box.Text = ((int)(kept ?? fallback)).ToString(CultureInfo.CurrentCulture);
    }

    private void ReadChoice(
        ComboBox box,
        string name,
        (int Value, string Key)[] choices,
        int? whenZero = null,
        int otherwise = 0)
    {
        double? kept = _roblox.Number(name);
        int current = (int)(kept ?? 0);

        if (whenZero is not null)
        {
            current = current == 0 ? whenZero.Value : otherwise;
        }

        box.Items.Clear();

        foreach ((int value, string key) in choices)
        {
            var item = new Choice(Strings.Get(key), value);
            box.Items.Add(item);

            if (value == current)
            {
                box.SelectedItem = item;
            }
        }

        box.SelectedItem ??= box.Items[0];
        box.IsEnabled = kept.HasValue;
    }

    private void ReadHaptics()
    {
        double? kept = _roblox.Number("HapticStrength");

        HapticsBox.IsEnabled = kept.HasValue;
        HapticsBox.IsChecked = (kept ?? 1) > 0;
    }

    private static int Picked(ComboBox box, int fallback) =>
        (box.SelectedItem as Choice)?.Value is int value ? value : fallback;

    private void SaveRobloxSettings()
    {
        if (!_roblox.Loaded)
        {
            return;
        }

        bool manual = Picked(QualityModeBox, 1) == 1;
        WriteStep(QualityLevelSlider, manual ? QualityLevelSlider.Step : 0);
        WriteChoice(OptimizationBox, "GraphicsOptimizationMode");
        WriteFlag(MaxQualityBox);
        WriteFlag(VignetteBox);
        _roblox.SetFlag("VignetteEnabledCustomOption", VignetteBox.IsChecked == true);

        WriteFlag(FullscreenBox);
        WriteFlag(StartMaximisedBox);

        WriteStep(MasterVolumeSlider, MasterVolumeSlider.Step / (double)10, decimals: 3);
        WriteStep(VoiceVolumeSlider, VoiceVolumeSlider.Step / (double)10, decimals: 3);
        WriteStep(PartyVolumeSlider, PartyVolumeSlider.Step / (double)10, decimals: 3);

        if (HapticsBox.IsEnabled)
        {
            _roblox.SetNumber("HapticStrength", HapticsBox.IsChecked == true ? 1 : 0);
        }

        WriteStep(TransparencySlider, TransparencySlider.Step / (double)10, decimals: 3);
        WriteStep(TextSizeSlider, TextSizeSlider.Step);
        WriteFlag(ReducedMotionBox);
        WriteFlag(UiNavigationBox);
        WriteFlag(PerformanceStatsBox);
        WriteFlag(MicroProfilerBox);
        WriteFlag(PlayerListBox);
        WriteFlag(ChatVisibleBox);
        WriteFlag(PlayerNamesBox);
        WriteFlag(BadgesBox);

        WriteStep(SensitivitySlider, SensitivitySlider.Step);
        WriteFlag(CameraInvertedBox);

        if (WriteChoice(CameraModeBox, "ComputerCameraMovementMode"))
        {
            _roblox.SetFlag("ComputerCameraMovementChanged", true);
        }

        if (WriteChoice(MovementModeBox, "ComputerMovementMode"))
        {
            _roblox.SetFlag("ComputerMovementChanged", true);
        }

        WriteFlag(VrBox);

        _robloxMine = _roblox.Save();
        ShowRobloxOwner();
    }

    private void ShowRobloxOwner()
    {
        string told = Strings.Get(RobloxIsRunning() ? "graphics.ownWhileOpen" : "graphics.ownHint");

        foreach (TextBlock line in new[] { RobloxOwner1, RobloxOwner2, RobloxOwner3, RobloxOwner4, RobloxOwner5 })
        {
            line.Text = told;
        }
    }

    private static bool RobloxIsRunning()
    {
        try
        {
            return System.Diagnostics.Process.GetProcessesByName("RobloxPlayerBeta").Length > 0;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private void WriteFlag(CheckBox box)
    {
        if (box is { IsEnabled: true, Tag: string name })
        {
            _roblox.SetFlag(name, box.IsChecked == true);
        }
    }

    private void WriteStep(StepBar bar, double value, int decimals = 0)
    {
        if (bar is { IsEnabled: true, Tag: string name })
        {
            _roblox.SetNumber(name, value, decimals);
        }
    }

    private bool WriteChoice(ComboBox box, string name)
    {
        if (!box.IsEnabled)
        {
            return false;
        }

        double? before = _roblox.Number(name);
        int chosen = Picked(box, (int)(before ?? 0));

        _roblox.SetNumber(name, chosen);

        return before is not null && (int)before.Value != chosen;
    }

    private void ShowSliderValues()
    {
        if (!IsInitialized)
        {
            return;
        }

        QualityLevelSliderValue.Text = Strings.Get("graphics.levelValue", QualityLevelSlider.Step);
        MasterVolumeSliderValue.Text = Percent(MasterVolumeSlider.Step);
        VoiceVolumeSliderValue.Text = Percent(VoiceVolumeSlider.Step);
        PartyVolumeSliderValue.Text = Percent(PartyVolumeSlider.Step);
        TransparencySliderValue.Text = Percent(TransparencySlider.Step);
        TextSizeSliderValue.Text = Strings.Get(TextSizes[Math.Clamp(TextSizeSlider.Step, 1, 4) - 1]);
        SensitivitySliderValue.Text = SensitivitySlider.Step.ToString(CultureInfo.CurrentCulture);
    }

    private static readonly string[] TextSizes =
        ["graphics.textDefault", "graphics.textLarge", "graphics.textLarger", "graphics.textLargest"];

    private static string Percent(int step) =>
        (step * 10).ToString(CultureInfo.CurrentCulture) + "%";

    private void OnStepBarChanged(object? sender, EventArgs e) => ShowSliderValues();

    private async Task BuildHomeAsync(bool force = false)
    {
        SessionHistory history = App.Services.History.Load();

        if (!force && _homeShownFor == history.Sessions.Count && DateTimeOffset.UtcNow - _homeShownAt < HomeFreshness)
        {
            return;
        }

        _homeShownFor = history.Sessions.Count;
        _homeShownAt = DateTimeOffset.UtcNow;

        ShowPlaytime(history);

        Task profile = BuildProfileAsync();

        try
        {
            await BuildGamesAsync(history).ConfigureAwait(true);
        }
        finally
        {
            await profile.ConfigureAwait(true);
        }
    }

    private void ShowPlaytime(SessionHistory history)
    {
        ProfileTotal.Text = Clocks.Short(history.Total());
        ProfileToday.Text = Clocks.Short(history.Since(DateTimeOffset.Now.Date));
        ProfileGames.Text = history.Sessions
            .Where(played => played.UniverseId > 0)
            .Select(played => played.UniverseId)
            .Distinct()
            .Count()
            .ToString("N0", CultureInfo.CurrentCulture);
    }

    private async Task BuildProfileAsync()
    {
        long userId = App.Services.Sessions.Current?.UserId ?? 0;

        if (userId <= 0)
        {
            userId = RobloxIdentity.FromLogs(RobloxLogWatcher.DefaultDirectory);
        }

        if (userId <= 0)
        {
            ProfileName.Text = Strings.Get("home.noAccount");
            ProfileHandle.Text = Strings.Get("home.noAccountHint");
            ProfileStats.Visibility = Visibility.Collapsed;
            return;
        }

        ProfileStats.Visibility = Visibility.Visible;

        if (ProfileAvatar.Source is null)
        {
            ProfileName.Text = Strings.Get("home.loadingAccount");
            ProfileHandle.Text = string.Empty;
        }

        AccountInfo? account = await App.Services.Accounts.DescribeAsync(userId);

        if (account is null)
        {
            ProfileName.Text = Strings.Get("home.noAccount");
            ProfileHandle.Text = Strings.Get("home.noAccountHint");
            return;
        }

        ProfileName.Text = account.DisplayName;
        ProfileHandle.Text = "@" + account.Name;

        if (account.AvatarUrl is not null)
        {
            ProfileAvatar.Source = Picture(account.AvatarUrl, AvatarPixels);
        }
    }

    private async Task BuildGamesAsync(SessionHistory history)
    {
        long[] played = history.Sessions
            .Where(session => session.UniverseId > 0)
            .Select(session => session.UniverseId)
            .Distinct()
            .ToArray();

        IReadOnlyList<PlayedGame> recent = history.ByGame(TilesInARow);
        Task<IReadOnlyList<DiscoveredGame>> charts = App.Services.Discover.PopularAsync();

        IReadOnlyDictionary<long, GameInfo> known = await App.Services.Games
            .DescribeManyAsync([.. played.Take(GenreSample), .. recent.Select(game => game.UniverseId)]);

        BuildRecentGames(recent, known);

        string[] genres = played
            .Take(GenreSample)
            .Select(known.GetValueOrDefault)
            .Select(game => game?.Genre)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        IReadOnlyList<DiscoveredGame> pool = await charts;
        IReadOnlyList<DiscoveredGame> picked = Recommendations.Pick(pool, played, genres, TilesInARow);

        RecommendedEmpty.Visibility = picked.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RecommendedPanel.Children.Clear();

        if (picked.Count == 0)
        {
            return;
        }

        IReadOnlyDictionary<long, GameInfo> art = await App.Services.Games
            .DescribeManyAsync([.. picked.Select(game => game.UniverseId)]);

        foreach (DiscoveredGame game in picked)
        {
            RecommendedPanel.Children.Add(Tile(
                game.Name,
                Strings.Get("home.playingNow", game.Playing.ToString("N0", CultureInfo.CurrentCulture)),
                art.GetValueOrDefault(game.UniverseId)?.IconUrl,
                game.RootPlaceId));
        }

        Arrive(RecommendedPanel);
    }

    private void BuildRecentGames(IReadOnlyList<PlayedGame> recent, IReadOnlyDictionary<long, GameInfo> known)
    {
        RecentGamesPanel.Children.Clear();
        RecentEmpty.Visibility = recent.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        foreach (PlayedGame game in recent)
        {
            GameInfo? info = known.GetValueOrDefault(game.UniverseId);

            RecentGamesPanel.Children.Add(Tile(
                info?.Name ?? game.Name,
                Strings.Get("home.playedFor", Clocks.Short(game.Total)) + " \u00b7 " + HowLongAgo(game.LastPlayed),
                info?.IconUrl,
                info?.RootPlaceId ?? 0));
        }
    }

    private StackPanel Tile(string title, string caption, string? iconUrl, long placeId)
    {
        var picture = new Image { Stretch = System.Windows.Media.Stretch.UniformToFill };

        if (iconUrl is not null)
        {
            picture.Source = Picture(iconUrl, TilePixels);
        }

        var art = new Border
        {
            CornerRadius = new CornerRadius(10),
            Background = (System.Windows.Media.Brush)FindResource("SurfaceHoverBrush"),
            ClipToBounds = true,
            Child = picture,
        };

        art.SetBinding(HeightProperty, new System.Windows.Data.Binding(nameof(ActualWidth))
        {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.Self),
        });

        var name = new TextBlock
        {
            Text = title,
            FontSize = 13,
            Margin = new Thickness(0, 10, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = title,
            Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
        };

        var note = new TextBlock
        {
            Text = caption,
            FontSize = 12,
            Margin = new Thickness(0, 2, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = (System.Windows.Media.Brush)FindResource("TextSoftBrush"),
        };

        var play = new Button
        {
            Style = (Style)FindResource("BluePlay"),
            Content = Strings.Get("home.playThis"),
            Margin = new Thickness(0, 10, 0, 0),
            Tag = placeId,
        };

        play.Click += OnPlayTile;

        var stack = new StackPanel { Margin = new Thickness(0, 0, TileGap, 0) };
        stack.Children.Add(art);
        stack.Children.Add(name);
        stack.Children.Add(note);
        stack.Children.Add(play);

        return stack;
    }

    private void OnPlayTile(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: long place } && place > 0)
        {
            Play(place);
        }
    }

    private void Play(long placeId)
    {
        if (Owner is MainWindow home)
        {
            Close();
            home.LaunchGame(placeId);
            return;
        }

        Open($"https://www.roblox.com/games/{placeId}");
    }

    private static string HowLongAgo(DateTimeOffset moment)
    {
        int days = (int)(DateTimeOffset.Now.Date - moment.ToLocalTime().Date).TotalDays;

        return days switch
        {
            <= 0 => Strings.Get("home.today"),
            1 => Strings.Get("home.yesterday"),
            _ => Strings.Get("home.daysAgo", days),
        };
    }

    private static System.Windows.Media.Imaging.BitmapImage Picture(string url, int pixels)
    {
        var picture = new System.Windows.Media.Imaging.BitmapImage();
        picture.BeginInit();
        picture.UriSource = new Uri(url, UriKind.Absolute);
        picture.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        picture.DecodePixelWidth = pixels;
        picture.EndInit();

        return picture;
    }

    private async Task CheckUpdateAsync()
    {
        CheckUpdateButton.IsEnabled = false;
        UpdateStateText.Text = Strings.Get("news.checking");

        LauncherRelease? release = await App.Services.LauncherUpdates.LatestAsync(AppServices.Version);

        CheckUpdateButton.IsEnabled = true;
        _release = release;

        if (release is null)
        {
            UpdateStateText.Text = Strings.Get("news.noReleases", AppServices.Version);
            UpdateBanner.Visibility = Visibility.Collapsed;
            return;
        }

        UpdateStateText.Text = release.Newer
            ? Strings.Get("news.behind", AppServices.Version, release.Version)
            : Strings.Get("news.current", AppServices.Version);

        UpdateBannerTitle.Text = Strings.Get("news.newVersion", release.Version);
        UpdateBannerHint.Text = Strings.Get("news.newVersionHint", release.PublishedAt.ToLocalTime().ToString("d"));
        UpdateBanner.Visibility = release.Newer ? Visibility.Visible : Visibility.Collapsed;
        InstallReleaseButton.IsEnabled = release.Download is not null;
        InstallReleaseButton.Visibility = release.Download is null ? Visibility.Collapsed : Visibility.Visible;

        if (release.Newer)
        {
            Arrive(UpdateBanner);
        }
    }

    private async void OnCheckUpdate(object sender, RoutedEventArgs e) => await CheckUpdateAsync();

    private void OnOpenRelease(object sender, RoutedEventArgs e)
    {
        if (_release is not null)
        {
            Open(_release.Url);
        }
    }

    private async void OnInstallRelease(object sender, RoutedEventArgs e)
    {
        if (_release?.Download is null)
        {
            UpdateBannerHint.Text = Strings.Get("news.noFile");
            return;
        }

        InstallReleaseButton.IsEnabled = false;
        UpdateBannerHint.Text = Strings.Get("news.fetching");

        string fresh = Path.Combine(Path.GetTempPath(), $"Nulltrap-{_release.Version}.exe");

        try
        {
            await using (Stream coming = await App.Services.LauncherUpdates
                .FetchAsync(_release.Download)
                .ConfigureAwait(true))
            await using (FileStream landing = File.Create(fresh))
            {
                await coming.CopyToAsync(landing).ConfigureAwait(true);
            }

            if (new FileInfo(fresh).Length < 1024)
            {
                throw new IOException(Strings.Get("news.tooSmall"));
            }

            App.Services.Installer.Replace(fresh);

            Process.Start(new ProcessStartInfo(App.Services.Installer.InstalledExecutablePath)
            {
                UseShellExecute = true,
            })?.Dispose();

            Application.Current.Shutdown();
        }
        catch (Exception failure) when (failure is IOException or HttpRequestException or UnauthorizedAccessException or TaskCanceledException)
        {
            UpdateBannerHint.Text = Strings.Get("news.updateFailed", failure.Message);
            InstallReleaseButton.IsEnabled = true;

            try
            {
                File.Delete(fresh);
            }
            catch (IOException)
            {
            }
        }
    }

    private void BuildPlanned()
    {
        PlannedPanel.Children.Clear();

        foreach (string line in Changelog.Entries.SelectMany(entry => entry.NextFor(_settings.Language)))
        {
            PlannedPanel.Children.Add(Faint("·  " + line));
        }

        if (PlannedPanel.Children.Count == 0)
        {
            PlannedPanel.Children.Add(Faint(Strings.Get("news.nothingPlanned")));
        }
    }

    private void BuildChangelog()
    {
        BuildPlanned();
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

    private void BuildThemeButtons()
    {
        ThemeButtons.Children.Clear();

        foreach (AppTheme theme in Enum.GetValues<AppTheme>())
        {
            bool active = theme == _settings.Theme;

            var button = new System.Windows.Controls.Button
            {
                Content = Strings.Get("theme." + theme.ToString().ToLowerInvariant()),
                Style = (Style)FindResource(active ? "Accent" : "Quiet"),
                Margin = new Thickness(0, 0, 8, 0),
                MinWidth = 104,
                Tag = theme,
            };

            button.Click += OnThemeChosen;
            ThemeButtons.Children.Add(button);
        }
    }

    private void OnThemeChosen(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: AppTheme theme } || theme == _settings.Theme)
        {
            return;
        }

        _settings.Theme = theme;
        _store.Save(_settings);
        Themes.Apply(theme);
        BuildThemeButtons();
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
        ShowPresencePreview();
        RepairButton.IsEnabled = App.Services.Installer.IsInstalled;
        AboutVersionText.Text = $"Version {AppServices.Version}";

        CacheSizeText.Text = Describe(App.Services.Paths.Downloads, Strings.Get("storage.packages"));
        VersionsSizeText.Text = Describe(App.Services.Paths.Versions, Strings.Get("storage.files"));
        ClearCacheButton.IsEnabled = Directory.Exists(App.Services.Paths.Downloads)
            && Directory.EnumerateFiles(App.Services.Paths.Downloads).Any();
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
        PresenceActivity sample = PresenceService.Compose(
            SampleSession, SampleGame, shape, SampleAccount, SamplePlace);

        PresencePreview.Opacity = on ? 1 : 0.4;
        PreviewDetails.Text = sample.Details;
        PreviewState.Text = sample.State ?? string.Empty;
        PreviewState.Visibility = sample.State is null ? Visibility.Collapsed : Visibility.Visible;
        PreviewElapsed.Text = Strings.Get("presence.elapsedSample");
        PreviewElapsed.Visibility = shape.ShowElapsed ? Visibility.Visible : Visibility.Collapsed;
        PreviewIcon.Visibility = shape.ShowGameIcon ? Visibility.Visible : Visibility.Collapsed;
        PreviewIconGlyph.Visibility = shape.ShowGameIcon ? Visibility.Visible : Visibility.Collapsed;
        PreviewIcon.ToolTip = sample.LargeText;

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
        ["Home", "General", "Graphics", "Launcher", "Integrations", "Versions", "Modifications", "Flags", "News", "About"];

    private static readonly Dictionary<string, string[]> Groups = new(StringComparer.Ordinal)
    {
        ["Graphics"] = ["GraphicsQuality", "GraphicsFrames", "GraphicsSound", "GraphicsInterface", "GraphicsControl", "GraphicsReady"],
        ["Versions"] = ["VersionsPlayer", "VersionsStorage"],
        ["Integrations"] = ["IntegrationsActivity", "IntegrationsDiscord"],
        ["Modifications"] = ["ModsProfiles", "ModsFiles", "ModsPlugins"],
        ["General"] = ["GeneralStart", "GeneralUpkeep"],
    };

    private static readonly Dictionary<string, (string Page, string Group)> Tabs = new(StringComparer.Ordinal)
    {
        ["GraphicsQuality"] = ("Graphics", "GraphicsQuality"),
        ["GraphicsSound"] = ("Graphics", "GraphicsSound"),
        ["GraphicsInterface"] = ("Graphics", "GraphicsInterface"),
        ["GraphicsControl"] = ("Graphics", "GraphicsControl"),
        ["GraphicsReady"] = ("Graphics", "GraphicsReady"),
        ["GraphicsFrames"] = ("Graphics", "GraphicsFrames"),
        ["VersionsPlayer"] = ("Versions", "VersionsPlayer"),
        ["VersionsStudio"] = ("Versions", "VersionsPlayer"),
        ["VersionsStorage"] = ("Versions", "VersionsStorage"),
        ["ModsProfiles"] = ("Modifications", "ModsProfiles"),
        ["ModsFiles"] = ("Modifications", "ModsFiles"),
        ["ModsPlugins"] = ("Modifications", "ModsPlugins"),
        ["IntegrationsActivity"] = ("Integrations", "IntegrationsActivity"),
        ["IntegrationsDiscord"] = ("Integrations", "IntegrationsDiscord"),
        ["GeneralStart"] = ("General", "GeneralStart"),
        ["GeneralUpkeep"] = ("General", "GeneralUpkeep"),
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

            if (page == "Graphics")
            {
                BuildReady();
                ShowRobloxOwner();
            }

            if (page == "Flags")
            {
                BuildFlags();
            }

            if (page == "News")
            {
                _ = CheckUpdateAsync();
            }

            if (page == "Home")
            {
                _ = BuildHomeAsync();
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
        var asking = new RemoveDialog(App.Services.Paths.Root) { Owner = this };

        if (asking.ShowDialog() != true)
        {
            return;
        }

        App.Sweep(App.Services.Installer.Uninstall(asking.Chosen, _settings.KeepDownloadCache), asking.Chosen);

        MessageBox.Show(
            Strings.Get(asking.Chosen == Removal.Everything ? "remove.goneAll" : "remove.gone"),
            "Nulltrap",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        Application.Current.Shutdown();
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

        PlayedWeekText.Text = Clocks.Describe(history.Since(now.Date.AddDays(-6)));

        PlayedSession[] counted = history.Sessions.Where(played => played.Duration > TimeSpan.Zero).ToArray();

        SessionCountText.Text = counted.Length.ToString("N0", CultureInfo.CurrentCulture);
        AverageSessionText.Text = counted.Length == 0
            ? Clocks.Describe(TimeSpan.Zero)
            : Clocks.Describe(TimeSpan.FromSeconds(counted.Average(played => played.Duration.TotalSeconds)));
        LongestSessionText.Text = counted.Length == 0
            ? Clocks.Describe(TimeSpan.Zero)
            : Clocks.Describe(counted.Max(played => played.Duration));

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

    private void BuildMods()
    {
        ModsFolderText.Text = App.Services.Mods.SourceDirectory;

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
        _chosen.Clear();

        bool withPreset = ShowPresetBox.IsChecked == true;
        string wanted = FlagSearchBox.Text.Trim();

        FlagSearchHint.Visibility = wanted.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        KeyValuePair<string, string>[] shown = _flags
            .Where(pair => withPreset || !OwnedElsewhere.Contains(pair.Key, StringComparer.Ordinal))
            .Where(pair => wanted.Length == 0
                || pair.Key.Contains(wanted, StringComparison.OrdinalIgnoreCase)
                || pair.Value.Contains(wanted, StringComparison.OrdinalIgnoreCase))
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach ((string name, string value) in shown)
        {
            FlagsPanel.Children.Add(FlagRow(name, value));
        }

        FlagsEmpty.Visibility = shown.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        FlagsEmptyText.Text = wanted.Length > 0
            ? Strings.Get("flags.emptyFiltered")
            : _flags.Count > 0 && !withPreset
                ? Strings.Get("flags.emptyPresetOnly")
                : Strings.Get("flags.emptyNone");

        ShowFlagFacts();
    }

    private void ShowFlagFacts()
    {
        string[] mine = _flags.Keys
            .Where(name => !OwnedElsewhere.Contains(name, StringComparer.Ordinal))
            .ToArray();

        int wasted = mine.Count(name => !FastFlagAllowlist.IsAllowed(name));
        int share = mine.Length == 0 ? 0 : (int)Math.Round(wasted * 100.0 / mine.Length);

        FlagFactsText.Text = Strings.Get("flags.facts", _flags.Count, mine.Length, share);
        RemoveChosenButton.IsEnabled = _chosen.Count > 0;
        RemoveAllButton.IsEnabled = mine.Length > 0;
    }

    private Border FlagRow(string name, string value)
    {
        bool preset = OwnedElsewhere.Contains(name, StringComparer.Ordinal);
        bool allowed = FastFlagAllowlist.IsAllowed(name);

        var row = new Grid();
        foreach (GridLength width in FlagColumns)
        {
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
        }

        var tick = new CheckBox
        {
            Style = (Style)FindResource("Tick"),
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = !preset,
            Tag = name,
        };

        tick.Checked += OnChooseFlag;
        tick.Unchecked += OnChooseFlag;

        var tag = new Border
        {
            Background = (System.Windows.Media.Brush)FindResource("SurfaceHoverBrush"),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3, 8, 3),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = Strings.Get(allowed ? TagOf(name) : "flags.tagUnknown"),
                FontSize = 11,
                Foreground = (System.Windows.Media.Brush)FindResource(allowed ? "TextSoftBrush" : "DangerBrush"),
            },
        };

        var mark = new TextBlock
        {
            Text = preset ? "\uE73E" : "\uE711",
            FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (System.Windows.Media.Brush)FindResource(preset ? "PurpleBrightBrush" : "RuleBrush"),
        };

        var title = new TextBlock
        {
            Text = name,
            FontSize = 13,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 14, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = name,
            Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
        };

        var field = new TextBox
        {
            Text = value,
            Style = (Style)FindResource("Field"),
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            VerticalAlignment = VerticalAlignment.Center,
            IsReadOnly = preset,
            Tag = name,
        };

        field.LostFocus += OnFlagValueEdited;

        Grid.SetColumn(tick, 0);
        Grid.SetColumn(tag, 1);
        Grid.SetColumn(mark, 2);
        Grid.SetColumn(title, 3);
        Grid.SetColumn(field, 4);
        row.Children.Add(tick);
        row.Children.Add(tag);
        row.Children.Add(mark);
        row.Children.Add(title);
        row.Children.Add(field);

        return new Border
        {
            Padding = new Thickness(14, 7, 14, 7),
            BorderBrush = (System.Windows.Media.Brush)FindResource("RuleBrush"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = row,
        };
    }

    private static string TagOf(string name) =>
        FastFlagAllowlist.Definitions.FirstOrDefault(definition => definition.Name == name)?.Category switch
        {
            FastFlagCategory.Geometry => "flags.tagGeometry",
            FastFlagCategory.Rendering => "flags.tagRendering",
            FastFlagCategory.UserInterface => "flags.tagInterface",
            _ => "flags.tagUnknown",
        };

    private void OnChooseFlag(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: string name } tick)
        {
            return;
        }

        if (tick.IsChecked == true)
        {
            _chosen.Add(name);
        }
        else
        {
            _chosen.Remove(name);
        }

        RemoveChosenButton.IsEnabled = _chosen.Count > 0;
    }

    private void OnFlagValueEdited(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox { Tag: string name } field || !_flags.TryGetValue(name, out string? stored))
        {
            return;
        }

        string value = field.Text.Trim();

        if (value.Length == 0 || value == stored)
        {
            field.Text = stored;
            return;
        }

        _flags[name] = value;
        _fastFlags.Save(_flags);
        ShowFlagFacts();
        ShowSaved();
    }

    private void OnFlagSearch(object sender, TextChangedEventArgs e)
    {
        if (_loaded)
        {
            BuildFlags();
        }
    }

    private void OnFlagFilterChanged(object sender, RoutedEventArgs e)
    {
        if (_loaded)
        {
            BuildFlags();
        }
    }

    private void OnRemoveChosenFlags(object sender, RoutedEventArgs e)
    {
        foreach (string name in _chosen)
        {
            _flags.Remove(name);
        }

        _fastFlags.Save(_flags);
        BuildFlags();
        ShowSaved();
    }

    private void OnRemoveAllFlags(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                Strings.Get("flags.confirmRemoveAll"),
                "Nulltrap",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        foreach (string name in _flags.Keys
            .Where(name => !OwnedElsewhere.Contains(name, StringComparer.Ordinal))
            .ToArray())
        {
            _flags.Remove(name);
        }

        _fastFlags.Save(_flags);
        BuildFlags();
        ShowSaved();
    }

    private void OnCopyFlags(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(FlagsAsJson());
            ShowFlagNotice(Strings.Get("flags.copied"));
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            ShowFlagNotice(Strings.Get("flags.copyFailed"));
        }
    }

    private void OnExportFlags(object sender, RoutedEventArgs e)
    {
        var save = new Microsoft.Win32.SaveFileDialog
        {
            FileName = FastFlagManager.SettingsFile,
            DefaultExt = ".json",
            Filter = Strings.Get("flags.jsonFiles"),
        };

        if (save.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            File.WriteAllText(save.FileName, FlagsAsJson());
            ShowFlagNotice(Strings.Get("flags.exported"));
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            ShowFlagNotice(failure.Message);
        }
    }

    private static readonly System.Text.Json.JsonSerializerOptions Readable = new() { WriteIndented = true };

    private string FlagsAsJson() =>
        System.Text.Json.JsonSerializer.Serialize(
            _flags.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            Readable);

    private void ShowFlagNotice(string message)
    {
        FlagNoticeText.Text = message;

        FlagNoticeText.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 1,
            To = 0,
            BeginTime = TimeSpan.FromSeconds(2),
            Duration = TimeSpan.FromMilliseconds(500),
        });
    }

    private void OnAddFlag(object sender, RoutedEventArgs e)
    {
        var dialog = new FlagDialog(Vet) { Owner = this };

        if (dialog.ShowDialog() != true || dialog.Chosen.Count == 0)
        {
            return;
        }

        foreach ((string name, string value) in dialog.Chosen)
        {
            _flags[name] = value;
        }

        _fastFlags.Save(_flags);

        ShowPresetBox.IsChecked = false;
        FlagSearchBox.Text = string.Empty;

        BuildFlags();
        ShowFlagNotice(Strings.Get("flags.added", dialog.Chosen.Count));
        ShowSaved();
    }

    private static string? Vet(string name, string value) =>
        OwnedElsewhere.Contains(name, StringComparer.Ordinal)
            ? Strings.Get("flags.systemOwned")
            : Problem(name, value);

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

    private void OnOpenFlagFile(object sender, RoutedEventArgs e)
    {
        _fastFlags.Save(_flags);
        Open(Path.GetDirectoryName(_fastFlags.SourcePath)!);
    }

    protected override void OnClosed(EventArgs e)
    {
        _robloxSettle.Stop();
        _robloxWatcher?.Dispose();
        _robloxWatcher = null;

        base.OnClosed(e);
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        _settings.CacheSweep = ChosenSweep();
        _settings.TrimMemory = TrimMemoryBox.IsChecked == true;
        _settings.Monitoring = MonitoringBox.IsChecked == true;
        _settings.ServerNotice = ServerNoticeBox.IsChecked == true;
        _settings.CloseAfterLaunch = CloseAfterLaunchBox.IsChecked == true;
        _settings.CloseRobloxOnLeave = CloseRobloxOnLeaveBox.IsChecked == true;
        _settings.StayInTray = StayInTrayBox.IsChecked == true;
        _settings.RunAtStartup = RunAtStartupBox.IsChecked == true;
        _settings.ConfirmMultipleInstances = ConfirmMultipleInstancesBox.IsChecked == true;
        _settings.DesktopShortcut = DesktopShortcutBox.IsChecked == true;
        _settings.StartMenuShortcut = StartMenuShortcutBox.IsChecked == true;
        _settings.KeepDownloadCache = KeepDownloadCacheBox.IsChecked == true;
        _settings.Channel = string.IsNullOrWhiteSpace(ChannelBox.Text)
            ? DeploymentChannel.DefaultName
            : ChannelBox.Text.Trim();
        _settings.AutomaticClientUpdates = AutomaticClientUpdatesBox.IsChecked == true;
        _settings.UpdateNotice = UpdateNoticeBox.IsChecked == true;
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
        string? texture = ChosenValue(TextureQualityBox);
        SetFlag(TextureQualityEnabledFlag, texture is null ? string.Empty : "True");
        SetFlag(TextureQualityFlag, texture ?? string.Empty);
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
        SaveProfiles();
        SaveRobloxSettings();
        App.Services.Mods.Enabled = _settings.Mods;
        App.Services.ApplyMonitoring();
        App.Services.ApplyStartup();

        if (Application.Current is App running)
        {
            running.ApplyTray();
        }

        App.Services.StartPlugins();
        BuildPlugins();
        App.Services.StartPresence();

        RefreshFacts();
        ShowSaved();
    }

    private static readonly (CacheSweep Plan, string Key)[] SweepPlans =
    [
        (CacheSweep.Never, "cache.never"),
        (CacheSweep.EachStart, "cache.eachStart"),
        (CacheSweep.Daily, "cache.daily"),
        (CacheSweep.Weekly, "cache.weekly"),
    ];

    private void FillSweepPlans()
    {
        CacheSweepBox.Items.Clear();

        foreach ((CacheSweep plan, string key) in SweepPlans)
        {
            var item = new Choice(Strings.Get(key), plan);
            CacheSweepBox.Items.Add(item);

            if (plan == _settings.CacheSweep)
            {
                CacheSweepBox.SelectedItem = item;
            }
        }

        CacheSweepBox.SelectedIndex = CacheSweepBox.SelectedIndex < 0 ? 0 : CacheSweepBox.SelectedIndex;
    }

    private CacheSweep ChosenSweep() =>
        CacheSweepBox.SelectedItem is Choice { Value: CacheSweep plan } ? plan : CacheSweep.Never;

    private void ShowCacheSize()
    {
        _ = Task.Run(() => App.Services.Cache.Weigh()).ContinueWith(
            weighed =>
            {
                if (weighed.IsCompletedSuccessfully)
                {
                    SweepSizeText.Text = Strings.Get("general.cacheSize", Megabytes(weighed.Result));
                }
            },
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    private static string Megabytes(long bytes) =>
        (bytes / (double)(1024 * 1024)).ToString("N0", CultureInfo.CurrentCulture);

    private void OnSweepNow(object sender, RoutedEventArgs e)
    {
        if (AppServices.RobloxIsRunning())
        {
            SweepSizeText.Text = Strings.Get("general.cacheBusy");
            return;
        }

        SweepNowButton.IsEnabled = false;
        SweepSizeText.Text = Strings.Get("general.cacheWorking");

        _ = Task.Run(() => App.Services.SweepCache(asked: true)).ContinueWith(
            swept =>
            {
                SweepNowButton.IsEnabled = true;

                if (!swept.IsCompletedSuccessfully)
                {
                    SweepSizeText.Text = Strings.Get("general.cacheBusy");
                    return;
                }

                SweepSizeText.Text = swept.Result is { } report
                    ? Strings.Get("general.cacheSwept", Megabytes(report.Bytes), report.Files.ToString("N0", CultureInfo.CurrentCulture))
                    : Strings.Get("general.cacheBusy");
            },
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    private readonly ProfileStore _profiles = App.Services.Profiles;

    private ProfileBook _book = App.Services.Profiles.Load();

    private GameProfile? _profile;

    private static readonly (bool? Wanted, string Key)[] ModChoices =
    [
        (null, "profiles.modsAsSet"),
        (true, "profiles.modsOn"),
        (false, "profiles.modsOff"),
    ];

    private void BuildProfiles()
    {
        _building = true;
        ProfileBox.Items.Clear();

        foreach (GameProfile profile in _book.Profiles)
        {
            ProfileBox.Items.Add(new Choice(profile.Name, profile));
        }

        _profile ??= _book.Profiles.FirstOrDefault();

        if (_profile is not null && !_book.Profiles.Contains(_profile))
        {
            _profile = _book.Profiles.FirstOrDefault();
        }

        ProfileBox.SelectedItem = ProfileBox.Items
            .OfType<Choice>()
            .FirstOrDefault(item => ReferenceEquals(item.Value, _profile));

        _building = false;

        ShowProfile();
    }

    private void ShowProfile()
    {
        bool any = _profile is not null;

        ProfileBody.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        ProfilesEmpty.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
        ProfileBox.IsEnabled = _book.Profiles.Count > 0;
        RemoveProfileButton.IsEnabled = any;

        if (_profile is null)
        {
            return;
        }

        ProfileNameBox.Text = _profile.Name;

        FillPlayed();
        BuildProfilePlaces();
        BuildProfileFlags();
        ShowProfileSettings();

        ProfileModsBox.Items.Clear();

        foreach ((bool? wanted, string key) in ModChoices)
        {
            var item = new Choice(Strings.Get(key), wanted);
            ProfileModsBox.Items.Add(item);

            if (wanted == _profile.Mods)
            {
                ProfileModsBox.SelectedItem = item;
            }
        }
    }

    private void FillPlayed()
    {
        ProfileRecentBox.Items.Clear();

        foreach (PlayedSession played in Played())
        {
            ProfileRecentBox.Items.Add(new Choice(
                played.Name ?? played.PlaceId.ToString(CultureInfo.CurrentCulture),
                played.PlaceId));
        }

        ProfileRecentBox.SelectedIndex = ProfileRecentBox.Items.Count > 0 ? 0 : -1;
        ProfileRecentBox.IsEnabled = ProfileRecentBox.Items.Count > 0;
    }

    private static PlayedSession[] Played() =>
        App.Services.History.Load().Sessions
            .Where(played => played.PlaceId > 0)
            .DistinctBy(played => played.PlaceId)
            .Take(25)
            .ToArray();

    private static readonly (string? Value, string Key)[] ProfileFullscreenChoices =
    [
        (null, "profiles.leave"),
        ("true", "profiles.on"),
        ("false", "profiles.off"),
    ];

    private static readonly (string? Value, string Key)[] ProfileOptimizationChoices =
    [
        (null, "profiles.leave"),
        ("0", "graphics.optimizationSpeed"),
        ("1", "graphics.optimizationBalance"),
        ("2", "graphics.optimizationQuality"),
    ];

    private static readonly (string Setting, string Box)[] ProfileSettings =
    [
        ("SavedQualityLevel", "ProfileQualityBox"),
        ("GraphicsOptimizationMode", "ProfileOptimizationBox"),
        ("Fullscreen", "ProfileFullscreenBox"),
        ("MasterVolume", "ProfileVolumeBox"),
    ];

    private void ShowProfileSettings()
    {
        FillQuality();
        FillVolume();
        FillKnown(ProfileOptimizationBox, ProfileOptimizationChoices);
        FillKnown(ProfileFullscreenBox, ProfileFullscreenChoices);

        foreach ((string setting, string box) in ProfileSettings)
        {
            if (FindName(box) is ComboBox picker)
            {
                PickSetting(picker, _profile?.Settings.GetValueOrDefault(setting));
            }
        }
    }

    private void FillQuality()
    {
        ProfileQualityBox.Items.Clear();
        ProfileQualityBox.Items.Add(new Choice(Strings.Get("profiles.leave"), null));
        ProfileQualityBox.Items.Add(new Choice(Strings.Get("profiles.auto"), "0"));

        for (int level = 1; level <= 10; level++)
        {
            ProfileQualityBox.Items.Add(new Choice(
                Strings.Get("graphics.levelValue", level),
                level.ToString(CultureInfo.InvariantCulture)));
        }
    }

    private void FillVolume()
    {
        ProfileVolumeBox.Items.Clear();
        ProfileVolumeBox.Items.Add(new Choice(Strings.Get("profiles.leave"), null));

        for (int step = 0; step <= 10; step++)
        {
            ProfileVolumeBox.Items.Add(new Choice(
                (step * 10).ToString(CultureInfo.CurrentCulture) + " %",
                (step / 10.0).ToString("0.###", CultureInfo.InvariantCulture)));
        }
    }

    private void FillKnown(ComboBox box, (string? Value, string Key)[] choices)
    {
        box.Items.Clear();

        foreach ((string? value, string key) in choices)
        {
            box.Items.Add(new Choice(Strings.Get(key), value));
        }
    }

    private static void PickSetting(ComboBox box, string? wanted)
    {
        foreach (Choice choice in box.Items.OfType<Choice>())
        {
            if (string.Equals(choice.Value as string, wanted, StringComparison.OrdinalIgnoreCase))
            {
                box.SelectedItem = choice;
                return;
            }
        }

        box.SelectedIndex = 0;
    }

    private void KeepProfileSettings()
    {
        if (_profile is null)
        {
            return;
        }

        foreach ((string setting, string box) in ProfileSettings)
        {
            if (FindName(box) is not ComboBox picker)
            {
                continue;
            }

            if (picker.SelectedItem is Choice { Value: string chosen } && chosen.Length > 0)
            {
                _profile.Settings[setting] = chosen;
                continue;
            }

            _profile.Settings.Remove(setting);
        }
    }

    private void BuildProfilePlaces()
    {
        ProfilePlacesPanel.Children.Clear();

        if (_profile is null)
        {
            return;
        }

        Dictionary<long, string> known = Played()
            .Where(played => played.Name is not null)
            .ToDictionary(played => played.PlaceId, played => played.Name!);

        foreach (long place in _profile.Places.ToArray())
        {
            ProfilePlacesPanel.Children.Add(PlaceRow(
                place,
                known.GetValueOrDefault(place, Strings.Get("profiles.unnamedPlace"))));
        }

        if (_profile.Places.Count == 0)
        {
            ProfilePlacesPanel.Children.Add(Faint(Strings.Get("profiles.noPlaces")));
        }
    }

    private Grid PlaceRow(long place, string name)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = new TextBlock
        {
            Text = name,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
        };

        var id = new TextBlock
        {
            Text = place.ToString(CultureInfo.CurrentCulture),
            FontSize = 12,
            Margin = new Thickness(0, 2, 0, 0),
            Foreground = (System.Windows.Media.Brush)FindResource("TextSoftBrush"),
        };

        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(title);
        left.Children.Add(id);
        Grid.SetColumn(left, 0);

        var drop = new System.Windows.Controls.Button
        {
            Style = (Style)FindResource("Quiet"),
            Content = Strings.Get("profiles.dropPlace"),
            MinWidth = 104,
            VerticalAlignment = VerticalAlignment.Center,
            Tag = place,
        };

        drop.Click += OnDropPlace;
        Grid.SetColumn(drop, 1);

        row.Children.Add(left);
        row.Children.Add(drop);

        return row;
    }

    private void BuildProfileFlags()
    {
        ProfileFlagsPanel.Children.Clear();

        if (_profile is null)
        {
            return;
        }

        foreach ((string name, string value) in _profile.Flags.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            ProfileFlagsPanel.Children.Add(ProfileFlagRow(name, value));
        }

        if (_profile.Flags.Count == 0)
        {
            ProfileFlagsPanel.Children.Add(Faint(Strings.Get("profiles.noFlags")));
        }
    }

    private Grid ProfileFlagRow(string name, string value)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = new TextBlock
        {
            Text = name,
            FontSize = 13,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 14, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = name,
            Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
        };

        Grid.SetColumn(title, 0);

        var field = new TextBox
        {
            Text = value,
            Style = (Style)FindResource("Field"),
            VerticalAlignment = VerticalAlignment.Center,
            Tag = name,
        };

        field.TextChanged += OnProfileFlagEdited;
        Grid.SetColumn(field, 1);

        var drop = new System.Windows.Controls.Button
        {
            Style = (Style)FindResource("Quiet"),
            Content = Strings.Get("profiles.dropPlace"),
            MinWidth = 104,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Tag = name,
        };

        drop.Click += OnDropProfileFlag;
        Grid.SetColumn(drop, 2);

        row.Children.Add(title);
        row.Children.Add(field);
        row.Children.Add(drop);

        return row;
    }

    private void OnProfileChosen(object sender, SelectionChangedEventArgs e)
    {
        if (_building || ProfileBox.SelectedItem is not Choice { Value: GameProfile picked })
        {
            return;
        }

        KeepProfileSettings();

        _profile = picked;
        ShowProfile();
    }

    private void OnAddProfile(object sender, RoutedEventArgs e)
    {
        var fresh = new GameProfile { Name = _book.FreeName(Strings.Get("profiles.fresh")) };

        _book.Profiles.Add(fresh);
        _profile = fresh;

        BuildProfiles();
        ProfileNameBox.Focus();
        ProfileNameBox.SelectAll();
    }

    private void OnRemoveProfile(object sender, RoutedEventArgs e)
    {
        if (_profile is null
            || MessageBox.Show(
                Strings.Get("profiles.confirmRemove", _profile.Name),
                "Nulltrap",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        _book.Profiles.Remove(_profile);
        _profile = null;

        BuildProfiles();
    }

    private void OnProfileNameLeft(object sender, RoutedEventArgs e)
    {
        if (_profile is null)
        {
            return;
        }

        string wanted = ProfileNameBox.Text.Trim();

        if (wanted.Length == 0 || string.Equals(wanted, _profile.Name, StringComparison.Ordinal))
        {
            ProfileNameBox.Text = _profile.Name;
            return;
        }

        _book.Profiles.Remove(_profile);
        _profile.Name = _book.FreeName(wanted);
        _book.Profiles.Add(_profile);

        BuildProfiles();
    }

    private void OnAddPlayedPlace(object sender, RoutedEventArgs e)
    {
        if (ProfileRecentBox.SelectedItem is Choice { Value: long place })
        {
            KeepPlace(place);
        }
    }

    private void OnAddPlace(object sender, RoutedEventArgs e)
    {
        if (long.TryParse(ProfilePlaceBox.Text.Trim(), out long place))
        {
            ProfilePlaceBox.Clear();
            KeepPlace(place);
        }
    }

    private void KeepPlace(long place)
    {
        if (_profile is null || place <= 0 || _profile.Places.Contains(place))
        {
            return;
        }

        _profile.Places.Add(place);
        BuildProfilePlaces();
    }

    private void OnDropPlace(object sender, RoutedEventArgs e)
    {
        if (_profile is not null && sender is System.Windows.Controls.Button { Tag: long place })
        {
            _profile.Places.Remove(place);
            BuildProfilePlaces();
        }
    }

    private void OnAddProfileFlag(object sender, RoutedEventArgs e)
    {
        if (_profile is null)
        {
            return;
        }

        var dialog = new FlagDialog(Problem) { Owner = this };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        foreach ((string name, string value) in dialog.Chosen)
        {
            _profile.Flags[name] = value;
        }

        BuildProfileFlags();
    }

    private void OnProfileFlagEdited(object sender, TextChangedEventArgs e)
    {
        if (_profile is not null && sender is TextBox { Tag: string name } field)
        {
            _profile.Flags[name] = field.Text.Trim();
        }
    }

    private void OnDropProfileFlag(object sender, RoutedEventArgs e)
    {
        if (_profile is not null && sender is System.Windows.Controls.Button { Tag: string name })
        {
            _profile.Flags.Remove(name);
            BuildProfileFlags();
        }
    }

    private void SaveProfiles()
    {
        if (_profile is not null && ProfileNameBox.Text.Trim().Length > 0)
        {
            OnProfileNameLeft(ProfileNameBox, new RoutedEventArgs());
        }

        if (_profile is not null && ProfileModsBox.SelectedItem is Choice picked)
        {
            _profile.Mods = picked.Value as bool?;
        }

        KeepProfileSettings();

        _profiles.Save(_book);
    }

    private void BuildPlugins()
    {
        PluginsFolderText.Text = App.Services.Plugins.Folder;
        PluginsPanel.Children.Clear();

        IReadOnlyList<PluginInfo> found = App.Services.Plugins.Found;

        foreach (PluginInfo plugin in found)
        {
            PluginsPanel.Children.Add(PluginCard(plugin));
        }

        PluginsEmpty.Visibility = found.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private Border PluginCard(PluginInfo plugin)
    {
        string key = PluginKeeper.KeyFor(plugin.File);

        var lines = new StackPanel { Margin = new Thickness(0, 0, 60, 0) };

        lines.Children.Add(new TextBlock
        {
            Text = plugin.Running ? plugin.Name : key,
            Style = (Style)FindResource("RowTitle"),
        });

        lines.Children.Add(new TextBlock
        {
            Text = plugin.Running
                ? Strings.Get("plugins.by", plugin.Author, plugin.Version)
                : Path.GetFileName(plugin.File),
            Style = (Style)FindResource("RowHint"),
        });

        if (plugin.Trouble is not null)
        {
            lines.Children.Add(new TextBlock
            {
                Text = plugin.Trouble == "plugins.noEntry"
                    ? Strings.Get("plugins.noEntry")
                    : Strings.Get("plugins.failed", plugin.Trouble),
                Style = (Style)FindResource("RowHint"),
                Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush"),
            });
        }

        var chosen = new CheckBox
        {
            Style = (Style)FindResource("Switch"),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            IsChecked = _settings.EnabledPlugins.Contains(key, StringComparer.OrdinalIgnoreCase),
            Tag = key,
        };

        chosen.Click += OnPluginSwitched;

        var row = new Grid();
        row.Children.Add(lines);
        row.Children.Add(chosen);

        return new Border
        {
            Style = (Style)FindResource("RowCard"),
            BorderBrush = plugin.Trouble is null
                ? (System.Windows.Media.Brush)FindResource("RuleBrush")
                : (System.Windows.Media.Brush)FindResource("DangerBrush"),
            Child = row,
        };
    }

    private void OnPluginSwitched(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: string key } box)
        {
            return;
        }

        _settings.EnabledPlugins.RemoveAll(name => string.Equals(name, key, StringComparison.OrdinalIgnoreCase));

        if (box.IsChecked == true)
        {
            _settings.EnabledPlugins.Add(key);
        }
    }

    private void OnOpenPluginsFolder(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(App.Services.Plugins.Folder);
        Open(App.Services.Plugins.Folder);
    }

    private void OnReloadPlugins(object sender, RoutedEventArgs e)
    {
        _store.Save(_settings);
        App.Services.StartPlugins();
        BuildPlugins();
        ShowSaved();
    }

    private void OnMonitoringChanged(object sender, RoutedEventArgs e) => ShowMonitoring();

    private void ShowMonitoring()
    {
        bool watching = MonitoringBox.IsChecked == true;

        ServerNoticeBox.IsEnabled = watching;
        CloseRobloxOnLeaveBox.IsEnabled = watching;
        PresenceLocked.Visibility = watching ? Visibility.Collapsed : Visibility.Visible;

        foreach (UIElement child in IntegrationsDiscord.Children)
        {
            if (!ReferenceEquals(child, PresenceLocked))
            {
                child.IsEnabled = watching;
            }
        }
    }

    private void OnSaveAndPlay(object sender, RoutedEventArgs e)
    {
        OnSave(sender, e);

        if (Owner is MainWindow home)
        {
            Close();
            home.LaunchPlayer();
        }
    }

    private void ShowSaved()
    {
        SavedToastText.Text = Strings.Get("settings.saved");
        SavedToastNote.Text = Strings.Get("settings.savedNote");

        var fade = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames();
        fade.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(
            1, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160))));
        fade.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(
            1, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromSeconds(4.6))));
        fade.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(
            0, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromSeconds(5.2))));

        SavedToast.BeginAnimation(OpacityProperty, fade);
        SavedToastLift.BeginAnimation(
            System.Windows.Media.TranslateTransform.YProperty,
            new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 12,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new System.Windows.Media.Animation.CubicEase
                {
                    EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut,
                },
            });
    }

    private void OnUnlockFpsChanged(object sender, RoutedEventArgs e)
    {
        bool unlocked = UnlockFpsBox.IsChecked == true;
        FpsBox.IsEnabled = unlocked;

        if (!unlocked)
        {
            FpsBox.Text = RobloxFrameCeiling.ToString();
            return;
        }

        string asked = FpsBox.Text.Trim();

        if (asked.Length == 0 || asked == RobloxFrameCeiling.ToString())
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
