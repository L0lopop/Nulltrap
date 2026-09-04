using System.Text.RegularExpressions;

namespace Nulltrap.Core.Launching;

public static partial class RobloxUri
{
    public static long PlaceFrom(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return 0;
        }

        string plain = Unescape(uri);
        Match found = Place().Match(plain);

        return found.Success && long.TryParse(found.Groups[1].Value, out long placeId) && placeId > 0
            ? placeId
            : 0;
    }

    private static string Unescape(string uri)
    {
        string plain = uri;

        for (int round = 0; round < 3; round++)
        {
            string next;

            try
            {
                next = Uri.UnescapeDataString(plain);
            }
            catch (UriFormatException)
            {
                return plain;
            }

            if (string.Equals(next, plain, StringComparison.Ordinal))
            {
                return plain;
            }

            plain = next;
        }

        return plain;
    }

    [GeneratedRegex(@"place[_\-]?id\s*[=:]\s*(\d{1,19})", RegexOptions.IgnoreCase)]
    private static partial Regex Place();
}
