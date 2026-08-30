using System.IO;
using System.Windows;
using System.Windows.Controls;

using Nulltrap.Core.FastFlags;
using Nulltrap.Core.Localization;

namespace Nulltrap.App;

public partial class FlagDialog : ChromeWindow
{
    private readonly Func<string, string, string?> _check;

    public FlagDialog(Func<string, string, string?> check)
    {
        ArgumentNullException.ThrowIfNull(check);

        _check = check;
        InitializeComponent();
        Loaded += (_, _) => NameBox.Focus();
    }

    public IReadOnlyDictionary<string, string> Chosen { get; private set; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private void OnTab(object sender, RoutedEventArgs e)
    {
        if (PanelOne is null || PanelJson is null)
        {
            return;
        }

        bool one = TabOne.IsChecked == true;

        PanelOne.Visibility = one ? Visibility.Visible : Visibility.Collapsed;
        PanelJson.Visibility = one ? Visibility.Collapsed : Visibility.Visible;

        Review();
    }

    private void OnTyped(object sender, TextChangedEventArgs e) => Review();

    private void OnBase64Changed(object sender, RoutedEventArgs e) => Review();

    private void OnSuggest(object sender, RoutedEventArgs e)
    {
        Suggestions.PlacementTarget = SuggestButton;
        Suggestions.IsOpen = true;
    }

    private void OnSuggested(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Header: string value })
        {
            ValueBox.Text = value;
        }
    }

    private void OnImportFile(object sender, RoutedEventArgs e)
    {
        var pick = new Microsoft.Win32.OpenFileDialog
        {
            DefaultExt = ".json",
            Filter = Strings.Get("flags.jsonFiles"),
        };

        if (pick.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            JsonBox.Text = File.ReadAllText(pick.FileName);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            ProblemText.Text = failure.Message;
        }
    }

    private void Review()
    {
        Chosen = Gather(out string? problem);

        ProblemText.Text = problem ?? string.Empty;
        ConfirmButton.IsEnabled = problem is null && Chosen.Count > 0;
    }

    private Dictionary<string, string> Gather(out string? problem)
    {
        var empty = new Dictionary<string, string>(StringComparer.Ordinal);

        if (TabOne.IsChecked == true)
        {
            string name = NameBox.Text.Trim();
            string value = ValueBox.Text.Trim();

            if (name.Length == 0 && value.Length == 0)
            {
                problem = null;
                return empty;
            }

            problem = _check(name, value);

            return problem is null
                ? new Dictionary<string, string>(StringComparer.Ordinal) { [name] = value }
                : empty;
        }

        string text = JsonBox.Text.Trim();

        if (text.Length == 0)
        {
            problem = null;
            return empty;
        }

        IReadOnlyDictionary<string, string>? read = FlagText.Read(text, Base64Box.IsChecked == true);

        if (read is null)
        {
            problem = Strings.Get(Base64Box.IsChecked == true ? "flags.badBase64" : "flags.badJson");
            return empty;
        }

        var kept = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach ((string name, string value) in read)
        {
            problem = _check(name, value);

            if (problem is not null)
            {
                return empty;
            }

            kept[name] = value;
        }

        problem = kept.Count == 0 ? Strings.Get("flags.badJson") : null;

        return kept;
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();
}
