using System.Text.Json.Serialization;

namespace Nulltrap.Core.Profiles;

public sealed record GameProfile
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("flags")]
    public Dictionary<string, string> Flags { get; init; } = new(StringComparer.Ordinal);

    [JsonPropertyName("places")]
    public List<long> Places { get; init; } = [];

    [JsonPropertyName("mods")]
    public bool? Mods { get; set; }
}

public sealed record ProfileBook
{
    [JsonPropertyName("profiles")]
    public List<GameProfile> Profiles { get; init; } = [];

    public GameProfile? For(long placeId) =>
        placeId <= 0 ? null : Profiles.FirstOrDefault(profile => profile.Places.Contains(placeId));

    public bool Holds(string name) =>
        Profiles.Any(profile => Same(profile.Name, name));

    public GameProfile? Find(string? name) =>
        name is null ? null : Profiles.FirstOrDefault(profile => Same(profile.Name, name));

    public string FreeName(string wanted)
    {
        string trimmed = wanted.Trim();

        if (trimmed.Length == 0)
        {
            trimmed = "Profile";
        }

        if (!Holds(trimmed))
        {
            return trimmed;
        }

        for (int attempt = 2; attempt < 1000; attempt++)
        {
            string tried = $"{trimmed} {attempt}";

            if (!Holds(tried))
            {
                return tried;
            }
        }

        return trimmed + " " + Guid.NewGuid().ToString("N")[..4];
    }

    public static Dictionary<string, string> Overlay(
        IReadOnlyDictionary<string, string> baseFlags,
        GameProfile? profile)
    {
        ArgumentNullException.ThrowIfNull(baseFlags);

        var merged = new Dictionary<string, string>(baseFlags, StringComparer.Ordinal);

        if (profile is null)
        {
            return merged;
        }

        foreach ((string name, string value) in profile.Flags)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                merged.Remove(name);
                continue;
            }

            merged[name] = value;
        }

        return merged;
    }

    private static bool Same(string one, string other) =>
        string.Equals(one.Trim(), other.Trim(), StringComparison.OrdinalIgnoreCase);
}
