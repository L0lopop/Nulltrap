using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Nulltrap.Core.Updating;

public sealed record LauncherRelease(string Version, string Url, DateTimeOffset PublishedAt, bool Newer);

public sealed class LauncherUpdateClient
{
    public const string Repository = "L0lopop/Nulltrap";

    private const string LatestUrl = "https://api.github.com/repos/" + Repository + "/releases/latest";

    private readonly HttpClient _http;

    public LauncherUpdateClient(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    public static bool IsNewer(string? offered, string? running)
    {
        if (!TryRead(offered, out Version? there) || !TryRead(running, out Version? here))
        {
            return false;
        }

        return there > here;
    }

    public static bool TryRead(string? text, out Version? version)
    {
        version = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string trimmed = text.Trim().TrimStart('v', 'V');
        int cut = trimmed.IndexOfAny(['-', '+']);

        if (cut > 0)
        {
            trimmed = trimmed[..cut];
        }

        return Version.TryParse(trimmed, out version);
    }

    public async Task<LauncherRelease?> LatestAsync(string running, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestUrl);
        request.Headers.Add("Accept", "application/vnd.github+json");

        try
        {
            using HttpResponseMessage answer = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!answer.IsSuccessStatusCode)
            {
                return null;
            }

            ReleaseRecord? record = await answer.Content
                .ReadFromJsonAsync<ReleaseRecord>(cancellationToken)
                .ConfigureAwait(false);

            if (record is null || string.IsNullOrWhiteSpace(record.TagName) || record.Draft)
            {
                return null;
            }

            return new LauncherRelease(
                record.TagName.TrimStart('v', 'V'),
                record.HtmlUrl ?? "https://github.com/" + Repository + "/releases",
                record.PublishedAt ?? DateTimeOffset.UtcNow,
                IsNewer(record.TagName, running));
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }
}

internal sealed record ReleaseRecord
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; init; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; init; }

    [JsonPropertyName("draft")]
    public bool Draft { get; init; }

    [JsonPropertyName("published_at")]
    public DateTimeOffset? PublishedAt { get; init; }
}
