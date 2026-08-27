using Nulltrap.Core.Deployment;

namespace Nulltrap.App;

public enum LaunchAction
{
    Menu,
    Install,
    LaunchPlayer,
    LaunchStudio,
    Uninstall,
}

public sealed record LaunchArguments
{
    public required LaunchAction Action { get; init; }

    public string? RobloxUri { get; init; }

    public bool Quiet { get; init; }

    public BinaryType BinaryType => Action == LaunchAction.LaunchStudio
        ? BinaryType.WindowsStudio64
        : BinaryType.WindowsPlayer;

    public static LaunchArguments Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        LaunchAction action = LaunchAction.Menu;
        string? uri = null;
        bool quiet = false;

        for (int index = 0; index < args.Count; index++)
        {
            string argument = args[index];

            if (argument.StartsWith("roblox", StringComparison.OrdinalIgnoreCase)
                && argument.Contains("://", StringComparison.Ordinal))
            {
                uri = argument;

                if (action == LaunchAction.Menu)
                {
                    action = argument.StartsWith("roblox-studio", StringComparison.OrdinalIgnoreCase)
                        ? LaunchAction.LaunchStudio
                        : LaunchAction.LaunchPlayer;
                }

                continue;
            }

            switch (argument.TrimStart('-', '/').ToLowerInvariant())
            {
                case "player":
                    action = LaunchAction.LaunchPlayer;
                    break;
                case "studio":
                    action = LaunchAction.LaunchStudio;
                    break;
                case "install":
                    action = LaunchAction.Install;
                    break;
                case "uninstall":
                    action = LaunchAction.Uninstall;
                    break;
                case "quiet":
                    quiet = true;
                    break;
            }
        }

        return new LaunchArguments
        {
            Action = action,
            RobloxUri = uri,
            Quiet = quiet,
        };
    }
}
