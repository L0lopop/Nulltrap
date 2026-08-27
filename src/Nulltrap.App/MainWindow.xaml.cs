using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using Nulltrap.Core;

namespace Nulltrap.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ShowDiagnostics();
    }

    private void ShowDiagnostics()
    {
        Assembly app = typeof(MainWindow).Assembly;

        string version = app.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? app.GetName().Version?.ToString()
            ?? "unknown";

        AddRow("Version", version);
        AddRow("Runtime", RuntimeInformation.FrameworkDescription);
        AddRow("Architecture", RuntimeInformation.ProcessArchitecture.ToString());
        AddRow("Core assembly", CoreAssembly.Reference.GetName().Name ?? "not loaded");
    }

    private void AddRow(string label, string value)
    {
        var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelBlock = new TextBlock
        {
            Text = label,
            FontSize = 12,
            Foreground = (Brush)FindResource("TextSoftBrush"),
        };

        var valueBlock = new TextBlock
        {
            Text = value,
            FontSize = 12,
            FontFamily = new FontFamily("Consolas, Cascadia Mono, Courier New"),
            Foreground = (Brush)FindResource("TextBrush"),
            TextWrapping = TextWrapping.Wrap,
        };

        Grid.SetColumn(valueBlock, 1);
        row.Children.Add(labelBlock);
        row.Children.Add(valueBlock);

        DiagnosticsPanel.Children.Add(row);
    }
}
