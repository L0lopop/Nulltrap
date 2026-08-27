using System.Windows;

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
        StatusText.Text = "Could not launch Roblox";
        DetailText.Text = message;
        DetailText.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
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
        double available = Math.Max(0, ActualWidth - 54);
        ProgressFill.Width = available * Math.Clamp(_fraction, 0, 1);
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
