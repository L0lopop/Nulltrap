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

    public IReadOnlyList<string> For(string language) =>
        language.Equals("ru", StringComparison.OrdinalIgnoreCase) && Russian.Count > 0
            ? Russian
            : English;
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
