using System.Windows;

namespace Nulltrap.App;

public partial class SecondClientDialog : ChromeWindow
{
    public SecondClientDialog() => InitializeComponent();

    public static bool Allowed(Window? owner)
    {
        if (!App.Services.Settings.Load().ConfirmMultipleInstances || !AppServices.RobloxIsRunning())
        {
            return true;
        }

        var asking = new SecondClientDialog();

        if (owner is not null && !ReferenceEquals(owner, asking))
        {
            asking.Owner = owner;
        }

        return asking.ShowDialog() == true;
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();
}
