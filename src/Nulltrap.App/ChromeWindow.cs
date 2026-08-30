using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;

namespace Nulltrap.App;

public class ChromeWindow : Window
{
    private const int WindowCornerPreference = 33;
    private const int RoundedCorners = 2;

    public static readonly DependencyProperty ShowMinimizeProperty =
        DependencyProperty.Register(
            nameof(ShowMinimize),
            typeof(bool),
            typeof(ChromeWindow),
            new PropertyMetadata(true));

    public static readonly DependencyProperty ShowCaptionProperty =
        DependencyProperty.Register(
            nameof(ShowCaption),
            typeof(bool),
            typeof(ChromeWindow),
            new PropertyMetadata(true));

    public ChromeWindow()
    {
        CommandBindings.Add(new CommandBinding(
            SystemCommands.MinimizeWindowCommand,
            (_, _) => Collapse(),
            (_, e) => e.CanExecute = ResizeMode != ResizeMode.NoResize));

        CommandBindings.Add(new CommandBinding(
            SystemCommands.CloseWindowCommand,
            (_, _) => SystemCommands.CloseWindow(this)));

        Opacity = 0;
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
    }

    public bool ShowMinimize
    {
        get => (bool)GetValue(ShowMinimizeProperty);
        set => SetValue(ShowMinimizeProperty, value);
    }

    public bool ShowCaption
    {
        get => (bool)GetValue(ShowCaptionProperty);
        set => SetValue(ShowCaptionProperty, value);
    }

    protected override void OnStateChanged(EventArgs e)
    {
        if (WindowState == WindowState.Minimized && Perch() is { } owner)
        {
            WindowState = WindowState.Normal;
            owner.WindowState = WindowState.Minimized;
            return;
        }

        base.OnStateChanged(e);
    }

    private void Collapse()
    {
        if (Perch() is { } owner)
        {
            owner.WindowState = WindowState.Minimized;
            return;
        }

        SystemCommands.MinimizeWindow(this);
    }

    private Window? Perch()
    {
        if (ShowInTaskbar)
        {
            return null;
        }

        Window? owner = Owner;

        while (owner is not null && !owner.ShowInTaskbar)
        {
            owner = owner.Owner;
        }

        return owner;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        nint handle = new WindowInteropHelper(this).Handle;

        if (handle == 0)
        {
            return;
        }

        int preference = RoundedCorners;

        try
        {
            DwmSetWindowAttribute(handle, WindowCornerPreference, ref preference, sizeof(int));
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (SystemParameters.MenuAnimation is false)
        {
            Opacity = 1;
            return;
        }

        BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(160),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
}
