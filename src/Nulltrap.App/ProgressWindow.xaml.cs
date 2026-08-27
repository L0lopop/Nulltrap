using System.Windows;

using Nulltrap.Core.Bootstrapping;

namespace Nulltrap.App;

public partial class ProgressWindow : Window
{
    private readonly CancellationTokenSource _cancellation = new();

    public ProgressWindow()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Redraw();
    }

    public CancellationToken CancellationToken => _cancellation.Token;

    public IProgress<BootstrapProgress> Progress =>
        new Progress<BootstrapProgress>(Apply);

    private double _fraction;

    private void Apply(BootstrapProgress progress)
    {
        StatusText.Text = progress.Message;
        _fraction = progress.Fraction;
        Redraw();
    }

    private void Redraw()
    {
        double available = Math.Max(0, ActualWidth - 64);
        ProgressFill.Width = available * Math.Clamp(_fraction, 0, 1);
    }

    public void ShowFailure(string message)
    {
        StatusText.Text = message;
        CancelButton.Content = "Close";
        _fraction = 0;
        Redraw();
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
