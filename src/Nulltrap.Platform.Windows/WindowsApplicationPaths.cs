using Nulltrap.Platform.Abstractions;

namespace Nulltrap.Platform.Windows;

public sealed class WindowsApplicationPaths : IApplicationPaths
{
    public const string FolderName = "Nulltrap";

    public WindowsApplicationPaths()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            FolderName))
    {
    }

    public WindowsApplicationPaths(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        Root = Path.GetFullPath(root);
    }

    public string Root { get; }

    public string Versions => Path.Combine(Root, "Versions");

    public string Downloads => Path.Combine(Root, "Downloads");

    public string Modifications => Path.Combine(Root, "Modifications");

    public string Logs => Path.Combine(Root, "Logs");

    public string ExecutablePath => Path.Combine(Root, "Nulltrap.exe");

    public void EnsureCreated()
    {
        foreach (string path in new[] { Root, Versions, Downloads, Modifications, Logs })
        {
            Directory.CreateDirectory(path);
        }
    }
}
