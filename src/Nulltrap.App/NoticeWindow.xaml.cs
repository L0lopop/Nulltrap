using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Nulltrap.App;

public partial class NoticeWindow : Window
{
    private static readonly TimeSpan OnScreen = TimeSpan.FromSeconds(8);

    private static NoticeWindow? _showing;

    private readonly DispatcherTimer _clock = new() { Interval = OnScreen };

    public NoticeWindow()
    {
        InitializeComponent();

        _clock.Tick += (_, _) => Fade();
        Loaded += OnLoaded;
        Closed += (_, _) => _clock.Stop();
    }

    public static void Announce(string title, string body)
    {
        _showing?.Close();

        var notice = new NoticeWindow();
        notice.NoticeTitle.Text = title;
        notice.NoticeBody.Text = body;

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

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Rect free = SystemParameters.WorkArea;

        Left = free.Right - ActualWidth;
        Top = free.Bottom - ActualHeight;

        BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(220),
        });

        Lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
        {
            From = 22,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(260),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });

        _clock.Start();
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
