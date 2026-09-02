using System.Windows;

using Nulltrap.Core.Installation;

namespace Nulltrap.App;

public partial class RemoveDialog : ChromeWindow
{
    public RemoveDialog(string where)
    {
        InitializeComponent();
        WhereText.Text = where;
    }

    public Removal Chosen { get; private set; } = Removal.LauncherOnly;

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        Chosen = EverythingChoice.IsChecked == true ? Removal.Everything : Removal.LauncherOnly;
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();
}
