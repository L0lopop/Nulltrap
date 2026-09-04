using Nulltrap.Core.Deployment;

namespace Nulltrap.Core.Launching;

public enum LaunchAction
{
    Menu,
    Setup,
    Install,
    LaunchPlayer,
    LaunchStudio,
    Uninstall,
    Background,
}

public sealed record LaunchArguments
{
    private static readonly string[] PlayerSchemes =
    [
        "roblox-player:",
        "roblox:",
    ];

    private static readonly string[] StudioSchemes =
    [
        "roblox-studio-auth:",
        "roblox-studio:",
    ];

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

        foreach (string argument in args)
        {
            if (string.IsNullOrWhiteSpace(argument))
            {
                continue;
            }

            if (TryReadUri(argument, StudioSchemes))
            {
                uri = argument;
                action = LaunchAction.LaunchStudio;
                continue;
            }

            if (TryReadUri(argument, PlayerSchemes))
            {
                uri = argument;

                if (action != LaunchAction.LaunchStudio)
                {
                    action = LaunchAction.LaunchPlayer;
                }

                continue;
            }

            switch (argument.TrimStart('-', '/').ToLowerInvariant())
            {
                case "player":
                    if (action != LaunchAction.LaunchStudio)
                    {
                        action = LaunchAction.LaunchPlayer;
                    }

                    break;
                case "studio":
                    action = LaunchAction.LaunchStudio;
                    break;
                case "setup":
                    action = LaunchAction.Setup;
                    break;
                case "install":
                    action = LaunchAction.Install;
                    break;
                case "uninstall":
                    action = LaunchAction.Uninstall;
                    break;
                case "background":
                    action = LaunchAction.Background;
                    break;
                case "quiet":
                case "silent":
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

    private static bool TryReadUri(string argument, IEnumerable<string> schemes) =>
        schemes.Any(scheme => argument.StartsWith(scheme, StringComparison.OrdinalIgnoreCase));
}
