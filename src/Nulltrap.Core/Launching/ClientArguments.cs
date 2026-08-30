using System.Globalization;

using Nulltrap.Core.Deployment;

namespace Nulltrap.Core.Launching;

public static class ClientArguments
{
    public const string JoinOrigin = "Nulltrap";

    private const string AppFlag = "--app";
    private const string DeepLinkFlag = "--deeplink";
    private const string ExperienceLink = "roblox://experiences/start?placeId=";

    public static string ForMenu(BinaryType binaryType) =>
        binaryType == BinaryType.WindowsStudio64 ? string.Empty : AppFlag;

    public static string ForGame(long placeId) =>
        placeId <= 0
            ? AppFlag
            : $"{AppFlag} {DeepLinkFlag} {Quote(ExperienceLink + placeId.ToString(CultureInfo.InvariantCulture) + "&joinAttemptOrigin=" + JoinOrigin)}";

    public static string ForUri(BinaryType binaryType, string? uri) =>
        string.IsNullOrWhiteSpace(uri) ? ForMenu(binaryType) : Quote(uri.Trim());

    private static string Quote(string value) => $"\"{value.Replace("\"", string.Empty, StringComparison.Ordinal)}\"";
}
