using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace Nulltrap.Core.Roblox;

public sealed record ServerPlace(string Country, string? City)
{
    public string Describe => string.IsNullOrWhiteSpace(City) ? Country : $"{Country} · {City}";
}

public sealed class ServerLocator
{
    private const string Endpoint = "https://ipinfo.io/";

    private readonly HttpClient _http;
    private readonly ConcurrentDictionary<string, ServerPlace> _seen = new();

    public ServerLocator(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    public static bool Routable(string? address)
    {
        if (!IPAddress.TryParse(address, out IPAddress? parsed)
            || parsed.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        byte[] octets = parsed.GetAddressBytes();

        return octets[0] switch
        {
            0 or 10 or 127 => false,
            172 => octets[1] is < 16 or > 31,
            192 => octets[1] != 168,
            169 => octets[1] != 254,
            >= 224 => false,
            _ => true,
        };
    }

    public static string CountryName(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length != 2)
        {
            return string.Empty;
        }

        string upper = code.Trim().ToUpperInvariant();
        string? named = Localization.Strings.Look("country." + upper);

        if (named is not null)
        {
            return named;
        }

        try
        {
            return new RegionInfo(upper).EnglishName;
        }
        catch (ArgumentException)
        {
            return upper;
        }
    }

    public static ServerPlace? Read(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            using JsonDocument page = JsonDocument.Parse(payload);
            JsonElement root = page.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string country = CountryName(Text(root, "country"));

            if (country.Length == 0)
            {
                return null;
            }

            string? city = Text(root, "city");

            return new ServerPlace(country, string.IsNullOrWhiteSpace(city) ? null : city);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement root, string field) =>
        root.TryGetProperty(field, out JsonElement found) && found.ValueKind == JsonValueKind.String
            ? found.GetString()
            : null;

    public async Task<ServerPlace?> DescribeAsync(string? address, CancellationToken cancellationToken = default)
    {
        if (!Routable(address))
        {
            return null;
        }

        if (_seen.TryGetValue(address!, out ServerPlace? known))
        {
            return known;
        }

        try
        {
            using HttpResponseMessage answer = await _http
                .GetAsync($"{Endpoint}{address}/json", cancellationToken)
                .ConfigureAwait(false);

            if (!answer.IsSuccessStatusCode)
            {
                return null;
            }

            string payload = await answer.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            ServerPlace? place = Read(payload);

            if (place is not null)
            {
                _seen[address!] = place;
            }

            return place;
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }
}
