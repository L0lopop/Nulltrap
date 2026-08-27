using System.Windows;
using System.Windows.Input;

namespace Nulltrap.App;

public class ChromeWindow : Window
{
    public static readonly DependencyProperty ShowMinimizeProperty =
        DependencyProperty.Register(
            nameof(ShowMinimize),
            typeof(bool),
            typeof(ChromeWindow),
            new PropertyMetadata(true));

    public ChromeWindow()
    {
        CommandBindings.Add(new CommandBinding(
            SystemCommands.MinimizeWindowCommand,
            (_, _) => SystemCommands.MinimizeWindow(this),
            (_, e) => e.CanExecute = ResizeMode != ResizeMode.NoResize));

        CommandBindings.Add(new CommandBinding(
            SystemCommands.CloseWindowCommand,
            (_, _) => SystemCommands.CloseWindow(this)));
    }

    public bool ShowMinimize
    {
        get => (bool)GetValue(ShowMinimizeProperty);
        set => SetValue(ShowMinimizeProperty, value);
    }
}
