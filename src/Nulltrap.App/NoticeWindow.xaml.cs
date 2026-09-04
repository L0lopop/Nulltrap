using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Nulltrap.App;

public partial class NoticeWindow : Window
{
    private static readonly TimeSpan OnScreen = TimeSpan.FromSeconds(8);
    private const int IconPixels = 96;
    private const double Slide = 12;

    private static NoticeWindow? _showing;

    private readonly DispatcherTimer _clock = new() { Interval = OnScreen };

    public NoticeWindow()
    {
        InitializeComponent();

        _clock.Tick += (_, _) => Fade();
        Loaded += OnLoaded;
        Closed += (_, _) => _clock.Stop();
    }

    public static void Announce(string title, string body, string? tail = null, string? iconUrl = null)
    {
        _showing?.Close();

        var notice = new NoticeWindow();

        notice.NoticeTitle.Text = title;
        notice.NoticeBody.Text = body;
        notice.NoticeTail.Text = tail ?? string.Empty;
        notice.NoticeTail.Visibility = string.IsNullOrWhiteSpace(tail) ? Visibility.Collapsed : Visibility.Visible;
        notice.NoticeIcon.Source = Art(iconUrl);

        _showing = notice;
        notice.Closed += (_, _) =>
        {
            if (ReferenceEquals(_showing, notice))
            {
                _showing = null;
            }
        };

        notice.Show();
    }

    private static ImageSource? Art(string? iconUrl)
    {
        if (!Uri.TryCreate(iconUrl, UriKind.Absolute, out Uri? found)
            || (found.Scheme != Uri.UriSchemeHttp && found.Scheme != Uri.UriSchemeHttps))
        {
            return Ours();
        }

        try
        {
            var art = new BitmapImage();

            art.BeginInit();
            art.UriSource = found;
            art.DecodePixelWidth = IconPixels;
            art.EndInit();

            return art;
        }
        catch (Exception failure) when (failure is UriFormatException or NotSupportedException or InvalidOperationException)
        {
            return Ours();
        }
    }

    private static ImageSource Ours()
    {
        var art = new BitmapImage();

        art.BeginInit();
        art.UriSource = new Uri("pack://application:,,,/Nulltrap.png");
        art.DecodePixelWidth = IconPixels;
        art.EndInit();

        return art;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Settle();
        SizeChanged += (_, _) => Settle();

        BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(220),
        });

        Lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
        {
            From = -Slide,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(260),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });

        _clock.Start();
    }

    private void Settle()
    {
        Rect free = SystemParameters.WorkArea;

        double left = free.Right - ActualWidth;
        double top = free.Top;

        Left = Math.Max(free.Left, Math.Min(left, free.Right - ActualWidth));
        Top = Math.Max(free.Top, Math.Min(top, free.Bottom - ActualHeight));
    }

    private void OnClicked(object sender, MouseButtonEventArgs e) => Fade();

    private void Fade()
    {
        _clock.Stop();

        var leaving = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(220),
        };

        leaving.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, leaving);
    }
}
