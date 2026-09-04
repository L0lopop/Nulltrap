using System.IO;
using System.Windows;
using System.Windows.Controls;

using Nulltrap.Core.Localization;
using Nulltrap.Core.Roblox;
using Nulltrap.Core.Sessions;

namespace Nulltrap.App;

public sealed class TrayIcon : IDisposable
{
    private const int TipLimit = 63;

    private readonly System.Windows.Forms.NotifyIcon _icon;
    private readonly Window _host;
    private readonly ContextMenu _menu;
    private readonly MenuItem _standing;
    private readonly MenuItem _server;
    private readonly MenuItem _open;
    private readonly MenuItem _play;
    private readonly MenuItem _close;
    private readonly MenuItem _quit;

    private bool _gone;

    public TrayIcon()
    {
        _host = new Window
        {
            Width = 0,
            Height = 0,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            Left = -32000,
            Top = -32000,
        };

        _host.Show();
        _host.Hide();

        _standing = new MenuItem { IsEnabled = false };
        _server = new MenuItem { IsEnabled = false, Visibility = Visibility.Collapsed };
        _open = new MenuItem { Header = Strings.Get("tray.open") };
        _play = new MenuItem { Header = Strings.Get("tray.play") };
        _close = new MenuItem { Header = Strings.Get("tray.closeRoblox"), Visibility = Visibility.Collapsed };
        _quit = new MenuItem { Header = Strings.Get("tray.quit") };

        _open.Click += (_, _) => Opened?.Invoke(this, EventArgs.Empty);
        _play.Click += (_, _) => Played?.Invoke(this, EventArgs.Empty);
        _close.Click += (_, _) => AppServices.CloseRoblox();
        _quit.Click += (_, _) => Quit?.Invoke(this, EventArgs.Empty);

        _menu = new ContextMenu
        {
            Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint,
        };

        _menu.Items.Add(_standing);
        _menu.Items.Add(_server);
        _menu.Items.Add(new Separator());
        _menu.Items.Add(_open);
        _menu.Items.Add(_play);
        _menu.Items.Add(_close);
        _menu.Items.Add(new Separator());
        _menu.Items.Add(_quit);

        _icon = new System.Windows.Forms.NotifyIcon
        {
            Icon = Ours(),
            Text = "Nulltrap",
            Visible = true,
        };

        _icon.MouseClick += OnClicked;
        _icon.DoubleClick += (_, _) => Opened?.Invoke(this, EventArgs.Empty);

        Idle();
    }

    public event EventHandler? Opened;

    public event EventHandler? Played;

    public event EventHandler? Quit;

    public void Idle()
    {
        _standing.Header = Strings.Get("tray.idle");
        _server.Visibility = Visibility.Collapsed;
        _close.Visibility = Visibility.Collapsed;
        _play.Visibility = Visibility.Visible;
        Tip("Nulltrap");
    }

    public void Playing(string game, ServerPlace? place, ServerFacts? facts)
    {
        _standing.Header = Strings.Get("tray.playing", game);
        _play.Visibility = Visibility.Collapsed;
        _close.Visibility = Visibility.Visible;

        string? about = About(place, facts);

        _server.Header = about;
        _server.Visibility = about is null ? Visibility.Collapsed : Visibility.Visible;

        Tip($"Nulltrap · {game}");
    }

    public void Whisper(string title, string text) =>
        _icon.ShowBalloonTip(4000, title, text, System.Windows.Forms.ToolTipIcon.None);

    public void Dispose()
    {
        if (_gone)
        {
            return;
        }

        _gone = true;
        _icon.Visible = false;
        _icon.Dispose();
        _host.Close();
    }

    private static string? About(ServerPlace? place, ServerFacts? facts)
    {
        var parts = new List<string>();

        if (place is not null)
        {
            parts.Add(place.Describe);
        }

        if (facts is { MaxPlayers: > 0 })
        {
            parts.Add(Strings.Get("notice.seats", facts.Playing, facts.MaxPlayers));
        }

        if (facts is { Ping: > 0 })
        {
            parts.Add(Strings.Get("notice.ping", facts.Ping));
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    private static System.Drawing.Icon Ours()
    {
        System.Windows.Resources.StreamResourceInfo? found =
            Application.GetResourceStream(new Uri("pack://application:,,,/Nulltrap.ico"));

        if (found is null)
        {
            return System.Drawing.SystemIcons.Application;
        }

        using Stream stream = found.Stream;

        return new System.Drawing.Icon(stream, System.Windows.Forms.SystemInformation.SmallIconSize);
    }

    private void Tip(string text) =>
        _icon.Text = text.Length <= TipLimit ? text : text[..(TipLimit - 1)] + "…";

    private void OnClicked(object? sender, System.Windows.Forms.MouseEventArgs e)
    {
        if (e.Button != System.Windows.Forms.MouseButtons.Right)
        {
            return;
        }

        _host.Show();
        _host.Activate();
        _host.Hide();

        _menu.IsOpen = true;
    }
}
