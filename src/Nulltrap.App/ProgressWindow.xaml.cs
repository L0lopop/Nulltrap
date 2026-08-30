using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

using Nulltrap.Core.Bootstrapping;
using Nulltrap.Core.Localization;

namespace Nulltrap.App;

public partial class ProgressWindow : ChromeWindow
{
    private readonly CancellationTokenSource _cancellation = new();

    private double _fraction;
    private bool _waiting;

    public ProgressWindow()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Redraw();
    }

    public CancellationToken CancellationToken => _cancellation.Token;

    public IProgress<BootstrapProgress> Progress => new Progress<BootstrapProgress>(Apply);

    public void ShowWaiting(string message)
    {
        _waiting = true;
        _fraction = 1;
        StatusText.Text = message;
        CancelButton.Content = Strings.Get("action.cancel");
        Redraw();
    }

    public void ShowFailure(string message)
    {
        StatusText.Text = message;
        StatusText.Foreground = (Brush)FindResource("DangerBrush");
        StatusText.FontSize = 12;
        CancelButton.Visibility = Visibility.Visible;
        CancelButton.Content = Strings.Get("action.close");
        _waiting = false;
        _fraction = 0;
        Redraw();
    }

    private void Apply(BootstrapProgress progress)
    {
        _waiting = false;
        StatusText.Text = progress.Message;
        _fraction = progress.Fraction;
        Redraw();
    }

    private void Redraw()
    {
        double available = Math.Max(0, ActualWidth - 70);

        ProgressFill.BeginAnimation(WidthProperty, new DoubleAnimation
        {
            To = available * Math.Clamp(_fraction, 0, 1),
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });

        ProgressFill.BeginAnimation(OpacityProperty, _waiting
            ? new DoubleAnimation
            {
                From = 1,
                To = 0.34,
                Duration = TimeSpan.FromMilliseconds(760),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            }
            : null);
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        _cancellation.Cancel();
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _cancellation.Cancel();
        _cancellation.Dispose();
        base.OnClosed(e);
    }
}
