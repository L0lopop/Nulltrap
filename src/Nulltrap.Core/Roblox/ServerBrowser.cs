using System.Globalization;
using System.Text.Json;

namespace Nulltrap.Core.Roblox;

public sealed record GameServer(string Id, int Playing, int MaxPlayers, double Fps)
{
    public int Free => MaxPlayers > Playing ? MaxPlayers - Playing : 0;

    public bool Full => MaxPlayers > 0 && Playing >= MaxPlayers;

    public string Short => Id.Length >= 8 ? Id[..8] : Id;
}

public sealed record ServerPage(IReadOnlyList<GameServer> Servers, string? Cursor)
{
    public static readonly ServerPage Empty = new([], null);

    public bool More => !string.IsNullOrWhiteSpace(Cursor);
}

public enum ServerOrder
{
    Fullest,
    Emptiest,
}

public sealed class ServerBrowser
{
    public const int PerPage = 100;

    public const int Attempts = 3;

    public static readonly TimeSpan Breath = TimeSpan.FromMilliseconds(900);

    private const string Endpoint = "https://games.roblox.com/v1/games/";

    private readonly HttpClient _http;

    public ServerBrowser(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    public static bool IsServerId(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && Guid.TryParseExact(value.Trim(), "D", out Guid _);

    public static string Address(long placeId, ServerOrder order, bool skipFull, string? cursor)
    {
        string address = string.Create(
            CultureInfo.InvariantCulture,
            $"{Endpoint}{placeId}/servers/Public?limit={PerPage}&sortOrder={(order == ServerOrder.Emptiest ? "Asc" : "Desc")}");

        if (skipFull)
        {
            address += "&excludeFullGames=true";
        }

        return string.IsNullOrWhiteSpace(cursor)
            ? address
            : address + "&cursor=" + Uri.EscapeDataString(cursor);
    }

    public static ServerPage Read(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return ServerPage.Empty;
        }

        try
        {
            using JsonDocument page = JsonDocument.Parse(payload);
            JsonElement root = page.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return ServerPage.Empty;
            }

            string? cursor = root.TryGetProperty("nextPageCursor", out JsonElement next)
                && next.ValueKind == JsonValueKind.String
                    ? next.GetString()
                    : null;

            if (!root.TryGetProperty("data", out JsonElement servers)
                || servers.ValueKind != JsonValueKind.Array)
            {
                return new ServerPage([], cursor);
            }

            var found = new List<GameServer>();

            foreach (JsonElement server in servers.EnumerateArray())
            {
                if (!server.TryGetProperty("id", out JsonElement id)
                    || id.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                string? value = id.GetString();

                if (!IsServerId(value))
                {
                    continue;
                }

                found.Add(new GameServer(
                    value!.Trim(),
                    Whole(server, "playing"),
                    Whole(server, "maxPlayers"),
                    Fraction(server, "fps")));
            }

            return new ServerPage(found, cursor);
        }
        catch (JsonException)
        {
            return ServerPage.Empty;
        }
    }

    public async Task<ServerPage> PageAsync(
        long placeId,
        ServerOrder order = ServerOrder.Fullest,
        bool skipFull = true,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (placeId <= 0)
        {
            return ServerPage.Empty;
        }

        string address = Address(placeId, order, skipFull, cursor);

        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            if (attempt > 0)
            {
                await Task.Delay(Breath, cancellationToken).ConfigureAwait(false);
            }

            string? payload = await FetchAsync(address, cancellationToken).ConfigureAwait(false);

            if (payload is null)
            {
                continue;
            }

            ServerPage page = Read(payload);

            if (page.Servers.Count > 0 || page.More)
            {
                return page;
            }

            return ServerPage.Empty;
        }

        return ServerPage.Empty;
    }

    private async Task<string?> FetchAsync(string address, CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage answer = await _http
                .GetAsync(address, cancellationToken)
                .ConfigureAwait(false);

            return answer.IsSuccessStatusCode
                ? await answer.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)
                : null;
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    private static int Whole(JsonElement server, string field) =>
        server.TryGetProperty(field, out JsonElement found)
        && found.ValueKind == JsonValueKind.Number
        && found.TryGetInt32(out int value)
            ? value
            : 0;

    private static double Fraction(JsonElement server, string field) =>
        server.TryGetProperty(field, out JsonElement found)
        && found.ValueKind == JsonValueKind.Number
        && found.TryGetDouble(out double value)
            ? value
            : 0;
}
