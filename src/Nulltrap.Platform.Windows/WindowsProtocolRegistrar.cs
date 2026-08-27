using Microsoft.Win32;

using Nulltrap.Platform.Abstractions;

namespace Nulltrap.Platform.Windows;

public sealed class WindowsProtocolRegistrar : IProtocolRegistrar
{
    public const string DefaultClassesRoot = @"Software\Classes";

    private readonly string _classesRoot;

    public WindowsProtocolRegistrar()
        : this(DefaultClassesRoot)
    {
    }

    public WindowsProtocolRegistrar(string classesRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(classesRoot);
        _classesRoot = classesRoot.Trim('\\');
    }

    private static readonly IReadOnlyDictionary<LaunchTarget, ProtocolDefinition> Definitions =
        new Dictionary<LaunchTarget, ProtocolDefinition>
        {
            [LaunchTarget.Player] = new(
                ["roblox", "roblox-player"],
                "URL: Roblox Protocol",
                "-player \"%1\""),
            [LaunchTarget.Studio] = new(
                ["roblox-studio", "roblox-studio-auth"],
                "URL: Roblox Studio Protocol",
                "-studio \"%1\""),
        };

    public bool IsRegistered(LaunchTarget target, string handlerPath)
    {
        string? current = GetRegisteredHandler(target);

        return current is not null
            && string.Equals(
                Path.GetFullPath(current),
                Path.GetFullPath(handlerPath),
                StringComparison.OrdinalIgnoreCase);
    }

    public string? GetRegisteredHandler(LaunchTarget target)
    {
        ProtocolDefinition definition = Definitions[target];

        using RegistryKey? command = Registry.CurrentUser.OpenSubKey(
            $@"{_classesRoot}\{definition.Schemes[0]}\shell\open\command");

        if (command?.GetValue(null) is not string value || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return ExtractExecutable(value);
    }

    public void Register(LaunchTarget target, string handlerPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handlerPath);

        ProtocolDefinition definition = Definitions[target];

        foreach (string scheme in definition.Schemes)
        {
            using RegistryKey scheme_ = Registry.CurrentUser.CreateSubKey($@"{_classesRoot}\{scheme}");
            scheme_.SetValue(null, definition.DisplayName);
            scheme_.SetValue("URL Protocol", string.Empty);

            using (RegistryKey icon = scheme_.CreateSubKey("DefaultIcon"))
            {
                icon.SetValue(null, $"{handlerPath},0");
            }

            using RegistryKey command = scheme_.CreateSubKey(@"shell\open\command");
            command.SetValue(null, $"\"{handlerPath}\" {definition.Arguments}");
        }
    }

    public void Unregister(LaunchTarget target)
    {
        foreach (string scheme in Definitions[target].Schemes)
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree($@"{_classesRoot}\{scheme}", throwOnMissingSubKey: false);
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static string? ExtractExecutable(string command)
    {
        command = command.Trim();

        if (command.StartsWith('"'))
        {
            int closing = command.IndexOf('"', 1);
            return closing > 1 ? command[1..closing] : null;
        }

        int space = command.IndexOf(' ');
        return space < 0 ? command : command[..space];
    }

    private sealed record ProtocolDefinition(
        string[] Schemes,
        string DisplayName,
        string Arguments);
}
