using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nulltrap.Core.Localization;

public sealed record ChangelogEntry
{
    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("date")]
    public string? Date { get; init; }

    [JsonPropertyName("en")]
    public IReadOnlyList<string> English { get; init; } = [];

    [JsonPropertyName("ru")]
    public IReadOnlyList<string> Russian { get; init; } = [];

    [JsonPropertyName("nextEn")]
    public IReadOnlyList<string> NextEnglish { get; init; } = [];

    [JsonPropertyName("nextRu")]
    public IReadOnlyList<string> NextRussian { get; init; } = [];

    public IReadOnlyList<string> For(string language) =>
        Russian.Count > 0 && language.Equals("ru", StringComparison.OrdinalIgnoreCase) ? Russian : English;

    public IReadOnlyList<string> NextFor(string language) =>
        NextRussian.Count > 0 && language.Equals("ru", StringComparison.OrdinalIgnoreCase)
            ? NextRussian
            : NextEnglish;
}

public static class Changelog
{
    public static IReadOnlyList<ChangelogEntry> Entries { get; } = Read();

    private static IReadOnlyList<ChangelogEntry> Read()
    {
        using Stream? stream = typeof(Changelog).Assembly
            .GetManifestResourceStream("Nulltrap.Core.Localization.changelog.json");

        if (stream is null)
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<ChangelogEntry>>(stream) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
