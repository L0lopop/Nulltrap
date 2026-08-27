using System.IO;
using System.Windows;

using Nulltrap.Core.Settings;

namespace Nulltrap.App;

public partial class SettingsWindow : Window
{
    private readonly SettingsStore _store;
    private readonly NulltrapSettings _settings;

    public SettingsWindow()
    {
        InitializeComponent();

        _store = App.Services.Settings;
        _settings = _store.Load();

        RegisterStudioBox.IsChecked = _settings.RegisterStudio;
        DesktopShortcutBox.IsChecked = _settings.DesktopShortcut;
        StartMenuShortcutBox.IsChecked = _settings.StartMenuShortcut;
        CloseAfterLaunchBox.IsChecked = _settings.CloseAfterLaunch;
        ConfirmMultipleInstancesBox.IsChecked = _settings.ConfirmMultipleInstances;
        KeepDownloadCacheBox.IsChecked = _settings.KeepDownloadCache;
        ChannelBox.Text = _settings.Channel;

        ShowCacheSize();
    }

    private void ShowCacheSize()
    {
        string downloads = App.Services.Paths.Downloads;

        if (!Directory.Exists(downloads))
        {
            CacheSizeText.Text = "nothing cached";
            ClearCacheButton.IsEnabled = false;
            return;
        }

        var files = new DirectoryInfo(downloads).GetFiles("*", SearchOption.AllDirectories);
        long bytes = files.Sum(file => file.Length);

        CacheSizeText.Text = files.Length == 0
            ? "nothing cached"
            : $"{files.Length} packages, {bytes / 1024.0 / 1024.0:N0} MB";

        ClearCacheButton.IsEnabled = files.Length > 0;
    }

    private void OnClearCache(object sender, RoutedEventArgs e)
    {
        string downloads = App.Services.Paths.Downloads;

        try
        {
            if (Directory.Exists(downloads))
            {
                foreach (string file in Directory.GetFiles(downloads))
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(failure.Message, "Nulltrap", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        ShowCacheSize();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        _settings.RegisterStudio = RegisterStudioBox.IsChecked == true;
        _settings.DesktopShortcut = DesktopShortcutBox.IsChecked == true;
        _settings.StartMenuShortcut = StartMenuShortcutBox.IsChecked == true;
        _settings.CloseAfterLaunch = CloseAfterLaunchBox.IsChecked == true;
        _settings.ConfirmMultipleInstances = ConfirmMultipleInstancesBox.IsChecked == true;
        _settings.KeepDownloadCache = KeepDownloadCacheBox.IsChecked == true;
        _settings.Channel = string.IsNullOrWhiteSpace(ChannelBox.Text)
            ? Core.Deployment.DeploymentChannel.DefaultName
            : ChannelBox.Text.Trim();

        _store.Save(_settings);

        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
