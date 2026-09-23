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

    public static string ForGame(long placeId, string? serverId = null)
    {
        if (placeId <= 0)
        {
            return AppFlag;
        }

        string link = ExperienceLink + placeId.ToString(CultureInfo.InvariantCulture);

        if (Roblox.ServerBrowser.IsServerId(serverId))
        {
            link += "&gameInstanceId=" + Uri.EscapeDataString(serverId!.Trim());
        }

        return $"{AppFlag} {DeepLinkFlag} {Quote(link + "&joinAttemptOrigin=" + JoinOrigin)}";
    }

    public static string ForUri(BinaryType binaryType, string? uri) =>
        string.IsNullOrWhiteSpace(uri) ? ForMenu(binaryType) : Quote(uri.Trim());

    private static string Quote(string value) => $"\"{value.Replace("\"", string.Empty, StringComparison.Ordinal)}\"";
}
