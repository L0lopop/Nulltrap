using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

using Nulltrap.Core.Localization;
using Nulltrap.Core.Roblox;

namespace Nulltrap.App;

public partial class ServerDialog : ChromeWindow
{
    private readonly long _place;
    private readonly List<GameServer> _listed = [];

    private CancellationTokenSource? _work;
    private ToggleButton? _picked;
    private Button? _more;
    private string? _cursor;
    private bool _filling;
    private int _round;

    private ServerDialog(long placeId, string? title)
    {
        InitializeComponent();

        _place = placeId;
        PlaceText.Text = string.IsNullOrWhiteSpace(title)
            ? Strings.Get("server.title")
            : title;

        _filling = true;
        OrderBox.Items.Add(new SettingsWindow.Choice(
            Strings.Get("server.orderFullest"), ServerOrder.Fullest));
        OrderBox.Items.Add(new SettingsWindow.Choice(
            Strings.Get("server.orderEmptiest"), ServerOrder.Emptiest));
        OrderBox.SelectedIndex = 0;
        _filling = false;

        Loaded += OnOpened;
        Closed += OnClosed;
    }

    public string? Chosen { get; private set; }

    public static bool Ask(Window? owner, long placeId, string? title, out string? serverId)
    {
        serverId = null;

        if (placeId <= 0)
        {
            return true;
        }

        var asking = new ServerDialog(placeId, title);

        if (owner is not null && !ReferenceEquals(owner, asking))
        {
            asking.Owner = owner;
        }

        if (asking.ShowDialog() != true)
        {
            return false;
        }

        serverId = asking.Chosen;

        return true;
    }

    private ServerOrder Order =>
        OrderBox.SelectedItem is SettingsWindow.Choice { Value: ServerOrder order }
            ? order
            : ServerOrder.Fullest;

    private void OnOpened(object sender, RoutedEventArgs e) => _ = FillAsync(fresh: true);

    private void OnClosed(object? sender, EventArgs e)
    {
        _work?.Cancel();
        _work = null;
    }

    private void OnOrderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_filling && IsLoaded)
        {
            _ = FillAsync(fresh: true);
        }
    }

    private void OnHideFullChanged(object sender, RoutedEventArgs e)
    {
        if (!_filling && IsLoaded)
        {
            _ = FillAsync(fresh: true);
        }
    }

    private void OnMore(object sender, RoutedEventArgs e) => _ = FillAsync(fresh: false);

    private async Task FillAsync(bool fresh)
    {
        CancellationTokenSource older = _work ?? new CancellationTokenSource();
        var mine = new CancellationTokenSource();

        _work = mine;
        older.Cancel();

        CancellationToken stop = mine.Token;
        int round = ++_round;

        if (fresh)
        {
            _listed.Clear();
            _cursor = null;
            _picked = null;
            ListPanel.Children.Clear();
            CountText.Text = string.Empty;
            Say(Strings.Get("server.loading"));
        }
        else if (_more is not null)
        {
            _more.IsEnabled = false;
            _more.Content = Strings.Get("server.loading");
        }

        Refresh();

        try
        {
            ServerPage page = await App.Services.Servers
                .PageAsync(_place, Order, HideFullBox.IsChecked == true, _cursor, stop)
                .ConfigureAwait(true);

            if (round != _round || stop.IsCancellationRequested)
            {
                return;
            }

            if (!fresh && page.Servers.Count == 0)
            {
                Draw();
                return;
            }

            _cursor = page.Cursor;

            foreach (GameServer server in page.Servers)
            {
                if (!_listed.Any(known => string.Equals(known.Id, server.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    _listed.Add(server);
                }
            }

            Draw();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void Draw()
    {
        ListPanel.Children.Clear();
        _more = null;

        if (_listed.Count == 0)
        {
            Say(Strings.Get("server.empty"));
            CountText.Text = string.Empty;
            Refresh();
            return;
        }

        Say(null);
        CountText.Text = Strings.Get(
            "server.count",
            _listed.Count.ToString("N0", CultureInfo.CurrentCulture));

        foreach (GameServer server in _listed)
        {
            ListPanel.Children.Add(RowFor(server));
        }

        if (!string.IsNullOrWhiteSpace(_cursor))
        {
            _more = new Button
            {
                Style = (Style)FindResource("Quiet"),
                Content = Strings.Get("server.more"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 2),
                MinWidth = 160,
            };

            _more.Click += OnMore;
            ListPanel.Children.Add(_more);
        }

        Refresh();
    }

    private ToggleButton RowFor(GameServer server)
    {
        var count = new TextBlock
        {
            Text = Strings.Get(
                "server.players",
                server.Playing.ToString("N0", CultureInfo.CurrentCulture),
                server.MaxPlayers.ToString("N0", CultureInfo.CurrentCulture)),
            Style = (Style)FindResource("RowTitle"),
        };

        var note = new TextBlock
        {
            Text = Strings.Get("server.fps", Math.Round(server.Fps).ToString("N0", CultureInfo.CurrentCulture))
                + " · " + server.Short,
            Style = (Style)FindResource("RowHint"),
        };

        var lines = new StackPanel { Margin = new Thickness(0, 0, 70, 0) };
        lines.Children.Add(count);
        lines.Children.Add(note);

        var seats = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };

        seats.Children.Add(new TextBlock
        {
            Text = server.Free.ToString("N0", CultureInfo.CurrentCulture),
            FontSize = 17,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = (System.Windows.Media.Brush)FindResource(
                server.Free > 0 ? "TextBrush" : "TextSoftBrush"),
        });

        seats.Children.Add(new TextBlock
        {
            Text = Strings.Get("server.free"),
            FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = (System.Windows.Media.Brush)FindResource("TextSoftBrush"),
        });

        var body = new Grid();
        body.Children.Add(lines);
        body.Children.Add(seats);

        var row = new ToggleButton
        {
            Style = (Style)FindResource("ServerRow"),
            Content = body,
            Tag = server.Id,
        };

        row.Checked += OnRowPicked;
        row.Unchecked += OnRowDropped;

        return row;
    }

    private void OnRowPicked(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton row)
        {
            return;
        }

        if (_picked is not null && !ReferenceEquals(_picked, row))
        {
            _picked.IsChecked = false;
        }

        _picked = row;

        if (ServerIdBox.Text.Length > 0)
        {
            ServerIdBox.Text = string.Empty;
        }

        Refresh();
    }

    private void OnRowDropped(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(_picked, sender))
        {
            _picked = null;
        }

        Refresh();
    }

    private void OnServerIdChanged(object sender, TextChangedEventArgs e)
    {
        string typed = ServerIdBox.Text.Trim();

        if (typed.Length > 0 && _picked is not null)
        {
            _picked.IsChecked = false;
            _picked = null;
        }

        IdNoteText.Text = typed.Length > 0 && !ServerBrowser.IsServerId(typed)
            ? Strings.Get("server.badId")
            : Strings.Get("server.byIdHint");

        Refresh();
    }

    private void Say(string? line)
    {
        StatusText.Text = line ?? string.Empty;
        StatusText.Visibility = string.IsNullOrEmpty(line) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Refresh() => JoinButton.IsEnabled = Picked() is not null;

    private string? Picked()
    {
        string typed = ServerIdBox.Text.Trim();

        if (typed.Length > 0)
        {
            return ServerBrowser.IsServerId(typed) ? typed : null;
        }

        return _picked?.Tag as string;
    }

    private void OnJoin(object sender, RoutedEventArgs e)
    {
        string? chosen = Picked();

        if (chosen is null)
        {
            return;
        }

        Chosen = chosen;
        DialogResult = true;
        Close();
    }

    private void OnAny(object sender, RoutedEventArgs e)
    {
        Chosen = null;
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();
}
