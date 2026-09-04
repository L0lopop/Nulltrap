using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;

using Microsoft.Win32;

namespace Nulltrap.App;

public class ChromeWindow : Window
{
    private const int WindowCornerPreference = 33;
    private const int RoundedCorners = 2;
    private const int ImmersiveDarkMode = 20;
    private const int SystemBackdropType = 38;
    private const int MicaBackdrop = 2;
    private const int NoBackdrop = 1;
    private const int GetMinMaxInfo = 0x0024;
    private const int MonitorNearest = 2;
    private const int SettingChanged = 0x001A;
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static readonly DependencyProperty ShowMinimizeProperty =
        DependencyProperty.Register(
            nameof(ShowMinimize),
            typeof(bool),
            typeof(ChromeWindow),
            new PropertyMetadata(true));

    public static readonly DependencyProperty ShowMaximizeProperty =
        DependencyProperty.Register(
            nameof(ShowMaximize),
            typeof(bool),
            typeof(ChromeWindow),
            new PropertyMetadata(false));

    public static readonly DependencyProperty ShowCaptionProperty =
        DependencyProperty.Register(
            nameof(ShowCaption),
            typeof(bool),
            typeof(ChromeWindow),
            new PropertyMetadata(true));

    public static readonly DependencyProperty HasBackdropProperty =
        DependencyProperty.Register(
            nameof(HasBackdrop),
            typeof(bool),
            typeof(ChromeWindow),
            new PropertyMetadata(false));

    private HwndSource? _source;

    public ChromeWindow()
    {
        CommandBindings.Add(new CommandBinding(
            SystemCommands.MinimizeWindowCommand,
            (_, _) => Collapse(),
            (_, e) => e.CanExecute = ResizeMode != ResizeMode.NoResize));

        CommandBindings.Add(new CommandBinding(
            SystemCommands.MaximizeWindowCommand,
            (_, _) => SystemCommands.MaximizeWindow(this),
            (_, e) => e.CanExecute = ResizeMode == ResizeMode.CanResize));

        CommandBindings.Add(new CommandBinding(
            SystemCommands.RestoreWindowCommand,
            (_, _) => SystemCommands.RestoreWindow(this),
            (_, e) => e.CanExecute = ResizeMode == ResizeMode.CanResize));

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

    public bool ShowMaximize
    {
        get => (bool)GetValue(ShowMaximizeProperty);
        set => SetValue(ShowMaximizeProperty, value);
    }

    public bool ShowCaption
    {
        get => (bool)GetValue(ShowCaptionProperty);
        set => SetValue(ShowCaptionProperty, value);
    }

    public bool HasBackdrop
    {
        get => (bool)GetValue(HasBackdropProperty);
        private set => SetValue(HasBackdropProperty, value);
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

        _source = HwndSource.FromHwnd(handle);
        _source?.AddHook(Filter);

        Shade();
        Ask(handle, WindowCornerPreference, RoundedCorners);
        Dress();

        Themes.Changed += OnThemeChanged;
        Closed += (_, _) => Themes.Changed -= OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        Shade();
        Dress();
    }

    private void Shade()
    {
        if (_source is not null)
        {
            Ask(_source.Handle, ImmersiveDarkMode, Themes.IsLight ? 0 : 1);
        }
    }

    private void Dress()
    {
        if (_source?.CompositionTarget is null)
        {
            return;
        }

        nint handle = _source.Handle;
        bool wanted = SeeThrough();

        if (!Ask(handle, SystemBackdropType, wanted ? MicaBackdrop : NoBackdrop))
        {
            return;
        }

        var sheet = new Margins
        {
            Left = wanted ? -1 : 0,
            Right = wanted ? -1 : 0,
            Top = wanted ? -1 : 0,
            Bottom = wanted ? -1 : 0,
        };

        DwmExtendFrameIntoClientArea(handle, ref sheet);

        _source.CompositionTarget.BackgroundColor = wanted
            ? System.Windows.Media.Colors.Transparent
            : Themes.IsLight
                ? System.Windows.Media.Colors.White
                : System.Windows.Media.Colors.Black;

        HasBackdrop = wanted;
    }

    private static bool SeeThrough()
    {
        try
        {
            using RegistryKey? personalize = Registry.CurrentUser.OpenSubKey(PersonalizeKey);

            return personalize?.GetValue("EnableTransparency") is int on && on != 0;
        }
        catch (Exception failure) when (failure is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    private nint Filter(nint handle, int message, nint parameter, nint argument, ref bool handled)
    {
        if (message == SettingChanged)
        {
            Dress();
            return 0;
        }

        if (message != GetMinMaxInfo)
        {
            return 0;
        }

        nint monitor = MonitorFromWindow(handle, MonitorNearest);

        if (monitor == 0)
        {
            return 0;
        }

        var details = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };

        if (!GetMonitorInfo(monitor, ref details))
        {
            return 0;
        }

        MinMaxInfo bounds = Marshal.PtrToStructure<MinMaxInfo>(argument);

        bounds.MaximisedPosition = new Point
        {
            X = details.Work.Left - details.Screen.Left,
            Y = details.Work.Top - details.Screen.Top,
        };

        bounds.MaximisedSize = new Point
        {
            X = details.Work.Right - details.Work.Left,
            Y = details.Work.Bottom - details.Work.Top,
        };

        bounds.MinimumTrackSize = new Point
        {
            X = (int)Math.Round(MinWidth),
            Y = (int)Math.Round(MinHeight),
        };

        Marshal.StructureToPtr(bounds, argument, fDeleteOld: true);
        handled = true;

        return 0;
    }

    private static bool Ask(nint handle, int attribute, int value)
    {
        try
        {
            return DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int)) == 0;
        }
        catch (Exception failure) when (failure is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
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

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmExtendFrameIntoClientArea(nint window, ref Margins margins);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint window, int preference);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo details);

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point Reserved;
        public Point MaximisedSize;
        public Point MaximisedPosition;
        public Point MinimumTrackSize;
        public Point MaximumTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public Rectangle Screen;
        public Rectangle Work;
        public int Flags;
    }
}
