using System.Text;
using System.Text.Json;

namespace Nulltrap.Core.FastFlags;

public static class FlagText
{
    public static IReadOnlyDictionary<string, string>? Read(string text, bool base64 = false)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (base64 && !TryDecode(text, out text))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(text);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var found = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (JsonProperty pair in document.RootElement.EnumerateObject())
            {
                string? value = Flatten(pair.Value);

                if (value is null)
                {
                    return null;
                }

                found[pair.Name] = value;
            }

            return found;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryDecode(string text, out string decoded)
    {
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(text.Trim()));
            return true;
        }
        catch (Exception failure) when (failure is FormatException or DecoderFallbackException)
        {
            decoded = string.Empty;
            return false;
        }
    }

    private static string? Flatten(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "True",
        JsonValueKind.False => "False",
        _ => null,
    };
}
