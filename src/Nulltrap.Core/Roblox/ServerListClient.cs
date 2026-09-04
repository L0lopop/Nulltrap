using System.Text.Json;

namespace Nulltrap.Core.Roblox;

public sealed record ServerFacts(int Playing, int MaxPlayers, int Ping, int Fps);

public sealed class ServerListClient
{
    public const int PagesToWalk = 2;

    private static readonly TimeSpan Breath = TimeSpan.FromMilliseconds(900);

    private const string Endpoint = "https://games.roblox.com/v1/games/{0}/servers/Public?limit=100";

    private readonly HttpClient _http;

    public ServerListClient(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    public static ServerFacts? Read(string payload, string? jobId, out string? cursor)
    {
        cursor = null;

        if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrWhiteSpace(jobId))
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

            if (root.TryGetProperty("nextPageCursor", out JsonElement next) && next.ValueKind == JsonValueKind.String)
            {
                cursor = next.GetString();
            }

            if (!root.TryGetProperty("data", out JsonElement servers) || servers.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (JsonElement server in servers.EnumerateArray())
            {
                if (!server.TryGetProperty("id", out JsonElement id)
                    || id.ValueKind != JsonValueKind.String
                    || !string.Equals(id.GetString(), jobId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return new ServerFacts(
                    Whole(server, "playing"),
                    Whole(server, "maxPlayers"),
                    Whole(server, "ping"),
                    Whole(server, "fps"));
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task<ServerFacts?> FindAsync(long placeId, string? jobId, CancellationToken cancellationToken = default)
    {
        if (placeId <= 0 || string.IsNullOrWhiteSpace(jobId))
        {
            return null;
        }

        string address = string.Format(System.Globalization.CultureInfo.InvariantCulture, Endpoint, placeId);
        string? cursor = null;

        for (int page = 0; page < PagesToWalk; page++)
        {
            if (page > 0)
            {
                await Task.Delay(Breath, cancellationToken).ConfigureAwait(false);
            }

            string wanted = cursor is null ? address : $"{address}&cursor={Uri.EscapeDataString(cursor)}";
            string? payload = await AskAsync(wanted, retry: page == 0, cancellationToken).ConfigureAwait(false);

            if (payload is null)
            {
                return null;
            }

            ServerFacts? found = Read(payload, jobId, out cursor);

            if (found is not null)
            {
                return found;
            }

            if (string.IsNullOrWhiteSpace(cursor))
            {
                return null;
            }
        }

        return null;
    }

    private async Task<string?> AskAsync(string address, bool retry, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < (retry ? 2 : 1); attempt++)
        {
            if (attempt > 0)
            {
                await Task.Delay(Breath, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                using HttpResponseMessage answer = await _http.GetAsync(address, cancellationToken).ConfigureAwait(false);

                if (answer.IsSuccessStatusCode)
                {
                    return await answer.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
            {
                return null;
            }
        }

        return null;
    }

    private static int Whole(JsonElement server, string field) =>
        server.TryGetProperty(field, out JsonElement found) && found.ValueKind == JsonValueKind.Number
            ? (int)Math.Round(found.GetDouble())
            : 0;
}
