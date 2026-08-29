using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Nulltrap.Core.Roblox;

public sealed record AccountInfo(long UserId, string Name, string DisplayName, string? AvatarUrl);

public sealed class AccountInfoClient
{
    private const string UserUrl = "https://users.roblox.com/v1/users/";
    private const string HeadshotUrl =
        "https://thumbnails.roblox.com/v1/users/avatar-headshot?size=48x48&format=Png&userIds=";

    private readonly HttpClient _http;

    public AccountInfoClient(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    public async Task<AccountInfo?> DescribeAsync(long userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return null;
        }

        UserRecord? user = await Read<UserRecord>(UserUrl + userId, cancellationToken).ConfigureAwait(false);

        if (user is null || user.Id == 0 || string.IsNullOrWhiteSpace(user.Name))
        {
            return null;
        }

        HeadshotResponse? headshots = await Read<HeadshotResponse>(HeadshotUrl + userId, cancellationToken)
            .ConfigureAwait(false);

        string? avatar = headshots?.Data
            .FirstOrDefault(record => record.TargetId == userId && record.State == "Completed")
            ?.ImageUrl;

        return new AccountInfo(
            user.Id,
            user.Name,
            string.IsNullOrWhiteSpace(user.DisplayName) ? user.Name : user.DisplayName,
            avatar);
    }

    private async Task<TRecord?> Read<TRecord>(string url, CancellationToken cancellationToken)
        where TRecord : class
    {
        try
        {
            using HttpResponseMessage answer = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);

            return answer.IsSuccessStatusCode
                ? await answer.Content.ReadFromJsonAsync<TRecord>(cancellationToken).ConfigureAwait(false)
                : null;
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }
}

internal sealed record UserRecord
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }
}

internal sealed record HeadshotResponse
{
    [JsonPropertyName("data")]
    public List<HeadshotRecord> Data { get; init; } = [];
}

internal sealed record HeadshotRecord
{
    [JsonPropertyName("targetId")]
    public long TargetId { get; init; }

    [JsonPropertyName("state")]
    public string? State { get; init; }

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; init; }
}
