using System.Windows;
using System.Windows.Media;

using Nulltrap.Core.Bootstrapping;

namespace Nulltrap.App;

public partial class ProgressWindow : ChromeWindow
{
    private readonly CancellationTokenSource _cancellation = new();

    private double _fraction;

    public ProgressWindow()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Redraw();
    }

    public CancellationToken CancellationToken => _cancellation.Token;

    public IProgress<BootstrapProgress> Progress => new Progress<BootstrapProgress>(Apply);

    public void ShowFailure(string message)
    {
        StatusText.Text = message;
        StatusText.Foreground = (Brush)FindResource("DangerBrush");
        StatusText.FontSize = 12;
        CancelButton.Content = "Close";
        _fraction = 0;
        Redraw();
    }

    private void Apply(BootstrapProgress progress)
    {
        StatusText.Text = progress.Message;
        _fraction = progress.Fraction;
        Redraw();
    }

    private void Redraw()
    {
        double available = Math.Max(0, ActualWidth - 70);

        ProgressFill.BeginAnimation(WidthProperty, new System.Windows.Media.Animation.DoubleAnimation
        {
            To = available * Math.Clamp(_fraction, 0, 1),
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new System.Windows.Media.Animation.CubicEase
            {
                EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut,
            },
        });
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
