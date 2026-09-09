using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

using Nulltrap.Platform.Abstractions;

namespace Nulltrap.Core.Roblox;

public sealed record RegionPing
{
    [JsonPropertyName("region")]
    public string Region { get; init; } = string.Empty;

    [JsonPropertyName("seen")]
    public int Seen { get; set; }

    [JsonPropertyName("total")]
    public long Total { get; set; }

    [JsonPropertyName("last")]
    public DateTimeOffset Last { get; set; }

    [JsonIgnore]
    public int Typical => Seen == 0 ? 0 : (int)Math.Round((double)Total / Seen);
}

public sealed class RegionPings
{
    public const string FileName = "Regions.json";
    public const int Keep = 60;

    private static readonly JsonSerializerOptions Shape = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _path;
    private readonly Lock _gate = new();

    public RegionPings(IApplicationPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _path = Path.Combine(paths.Root, FileName);
    }

    public string SourcePath => _path;

    public static bool Sound(int ping) => ping is > 0 and < 2000;

    public IReadOnlyList<RegionPing> Load()
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<RegionPing>>(File.ReadAllText(_path), Shape) ?? [];
        }
        catch (Exception failure) when (failure is JsonException or IOException)
        {
            return [];
        }
    }

    public int Typical(string? region)
    {
        if (string.IsNullOrWhiteSpace(region))
        {
            return 0;
        }

        return Load()
            .FirstOrDefault(known => string.Equals(known.Region, region, StringComparison.OrdinalIgnoreCase))
            ?.Typical ?? 0;
    }

    public void Record(string? region, int ping)
    {
        if (string.IsNullOrWhiteSpace(region) || !Sound(ping))
        {
            return;
        }

        lock (_gate)
        {
            List<RegionPing> known = [.. Load()];

            RegionPing? mine = known.FirstOrDefault(
                entry => string.Equals(entry.Region, region, StringComparison.OrdinalIgnoreCase));

            if (mine is null)
            {
                mine = new RegionPing { Region = region };
                known.Add(mine);
            }

            mine.Seen++;
            mine.Total += ping;
            mine.Last = DateTimeOffset.UtcNow;

            if (mine.Seen > Keep)
            {
                mine.Total = mine.Typical * (long)Keep;
                mine.Seen = Keep;
            }

            List<RegionPing> kept = [.. known.OrderByDescending(entry => entry.Last).Take(Keep)];

            Save(kept);
        }
    }

    private void Save(List<RegionPing> known)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

            string staging = $"{_path}.{Environment.ProcessId}.tmp";

            File.WriteAllText(staging, JsonSerializer.Serialize(known, Shape));
            File.Move(staging, _path, overwrite: true);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
        }
    }
}
