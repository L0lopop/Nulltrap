using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Nulltrap.App;

[TemplatePart(Name = CellsPart, Type = typeof(UniformGrid))]
[TemplatePart(Name = LessPart, Type = typeof(ButtonBase))]
[TemplatePart(Name = MorePart, Type = typeof(ButtonBase))]
public sealed class StepBar : Control
{
    public const string CellsPart = "PART_Cells";
    public const string LessPart = "PART_Less";
    public const string MorePart = "PART_More";

    public static readonly DependencyProperty StepsProperty =
        DependencyProperty.Register(
            nameof(Steps),
            typeof(int),
            typeof(StepBar),
            new PropertyMetadata(10, OnShapeChanged));

    public static readonly DependencyProperty LowestProperty =
        DependencyProperty.Register(
            nameof(Lowest),
            typeof(int),
            typeof(StepBar),
            new PropertyMetadata(0, OnShapeChanged));

    public static readonly DependencyProperty StepProperty =
        DependencyProperty.Register(
            nameof(Step),
            typeof(int),
            typeof(StepBar),
            new FrameworkPropertyMetadata(
                0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnStepChanged));

    private UniformGrid? _cells;
    private bool _dragging;

    static StepBar() =>
        DefaultStyleKeyProperty.OverrideMetadata(typeof(StepBar), new FrameworkPropertyMetadata(typeof(StepBar)));

    public event EventHandler? Changed;

    public int Steps
    {
        get => (int)GetValue(StepsProperty);
        set => SetValue(StepsProperty, value);
    }

    public int Lowest
    {
        get => (int)GetValue(LowestProperty);
        set => SetValue(LowestProperty, value);
    }

    public int Step
    {
        get => (int)GetValue(StepProperty);
        set => SetValue(StepProperty, Math.Clamp(value, Lowest, Steps));
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _cells = GetTemplateChild(CellsPart) as UniformGrid;

        if (_cells is not null)
        {
            _cells.Cursor = Cursors.Hand;
            _cells.MouseLeftButtonDown += OnPressed;
            _cells.MouseMove += OnDragged;
            _cells.MouseLeftButtonUp += OnReleased;
            _cells.LostMouseCapture += (_, _) => _dragging = false;
        }

        if (GetTemplateChild(LessPart) is ButtonBase less)
        {
            less.Click += (_, _) => Step--;
        }

        if (GetTemplateChild(MorePart) is ButtonBase more)
        {
            more.Click += (_, _) => Step++;
        }

        Rebuild();
    }

    private static void OnShapeChanged(DependencyObject holder, DependencyPropertyChangedEventArgs e) =>
        (holder as StepBar)?.Rebuild();

    private static void OnStepChanged(DependencyObject holder, DependencyPropertyChangedEventArgs e)
    {
        if (holder is not StepBar bar)
        {
            return;
        }

        bar.Paint();
        bar.Changed?.Invoke(bar, EventArgs.Empty);
    }

    private void Rebuild()
    {
        if (_cells is null)
        {
            return;
        }

        _cells.Children.Clear();
        _cells.Columns = Math.Max(1, Steps);

        for (int index = 1; index <= Steps; index++)
        {
            _cells.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(2, 0, 2, 0),
                Tag = index,
            });
        }

        Paint();
    }

    private void OnPressed(object sender, MouseButtonEventArgs e)
    {
        if (_cells is null || !IsEnabled)
        {
            return;
        }

        _dragging = _cells.CaptureMouse();
        Aim(e.GetPosition(_cells).X);
        e.Handled = true;
    }

    private void OnDragged(object sender, MouseEventArgs e)
    {
        if (_dragging && _cells is not null)
        {
            Aim(e.GetPosition(_cells).X);
        }
    }

    private void OnReleased(object sender, MouseButtonEventArgs e)
    {
        if (_dragging)
        {
            _dragging = false;
            _cells?.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void Aim(double x)
    {
        if (_cells is null || _cells.ActualWidth <= 0)
        {
            return;
        }

        if (x < 0)
        {
            Step = Lowest;
            return;
        }

        double cell = _cells.ActualWidth / Math.Max(1, Steps);

        Step = Math.Clamp((int)(x / cell) + 1, Lowest, Steps);
    }

    private void Paint()
    {
        if (_cells is null)
        {
            return;
        }

        var filled = (Brush)FindResource("AccentBrush");
        var empty = (Brush)FindResource("SurfaceHoverBrush");

        foreach (Border cell in _cells.Children.OfType<Border>())
        {
            cell.Background = cell.Tag is int index && index <= Step ? filled : empty;
        }
    }
}
